using System.Threading.RateLimiting;
using HerProof.AI;
using HerProof.Application.AI;
using HerProof.Application.Assessments;
using HerProof.Application.Audit;
using HerProof.Application.Authorization;
using HerProof.Application.Notifications;
using HerProof.Application.Repositories;
using HerProof.Application.Speech;
using HerProof.Infrastructure.Assessments;
using HerProof.Infrastructure.Audit;
using HerProof.Infrastructure.Authorization;
using HerProof.Infrastructure.Identity;
using HerProof.Infrastructure.Notifications;
using HerProof.Infrastructure.Persistence;
using HerProof.Infrastructure.Repositories;
using HerProof.Infrastructure.Speech;
using HerProof.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

HerProof.Infrastructure.Configuration.EnvFileLoader.LoadFromNearestEnvFile(AppContext.BaseDirectory);

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddControllersWithViews(options =>
    {
        // docs/03 section 3: antiforgery on all state-changing forms, applied globally rather than
        // per-action so a new form can never forget it.
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddDbContext<HerProofDbContext>(options => options
        .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new AppendOnlyInterceptor()));

    // STUB: /readyz does not yet check the database connection; add a DbContext health check in P02 follow-up.
    builder.Services.AddHealthChecks();

    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // docs/03 section 3: hashed passwords (Identity default), lockout, email confirmation.
        options.SignIn.RequireConfirmedAccount = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.Password.RequiredLength = 10;
    })
        .AddEntityFrameworkStores<HerProofDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        // docs/03 section 3.
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

    builder.Services.AddAuthorization(options => options.AddHerProofPolicies());

    // Audits every 401/403 without changing the response the user sees (CLAUDE.md P03).
    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationMiddlewareResultHandler>();

    builder.Services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();
    builder.Services.AddScoped<IAuditLogger, EfAuditLogger>();

    var brevoApiKey = builder.Configuration["Brevo:ApiKey"];
    if (string.IsNullOrWhiteSpace(brevoApiKey))
    {
        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
    }
    else
    {
        builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");
            client.DefaultRequestHeaders.Add("api-key", brevoApiKey);
            client.DefaultRequestHeaders.Add("accept", "application/json");
        });
    }

    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient<IGitHubClient, GitHubClient>(client =>
    {
        // docs/05 P07: public repos only for now, unauthenticated. GitHub requires a User-Agent.
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HerProof-Hackathon/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    });
    builder.Services.AddScoped<RepositoryContentFetcher>();
    builder.Services.AddSingleton<ISessionEventTokenService, HmacSessionEventTokenService>();
    builder.Services.AddSingleton<IStaticCodeAnalyzer, RoslynStaticCodeAnalyzer>();
    builder.Services.AddScoped<RepositoryAnalysisOrchestrator>();
    builder.Services.AddScoped(sp =>
        new ThandiInterviewEngine(sp.GetRequiredService<IAIService>(), builder.Configuration["Interviewer:Name"] ?? "Thandi"));

    var speechKey = builder.Configuration["Speech:Key"];
    var speechRegion = builder.Configuration["Speech:Region"];
    if (string.IsNullOrWhiteSpace(speechKey) || string.IsNullOrWhiteSpace(speechRegion))
    {
        builder.Services.AddSingleton<ISpeechToText, FakeSpeechToText>();
        builder.Services.AddSingleton<ITextToSpeech, FakeTextToSpeech>();
    }
    else
    {
        builder.Services.AddHttpClient<ISpeechToText, AzureSpeechToText>(client =>
        {
            client.BaseAddress = new Uri($"https://{speechRegion}.stt.speech.microsoft.com/");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);
        });
        builder.Services.AddHttpClient<AzureTextToSpeech>(client =>
        {
            client.BaseAddress = new Uri($"https://{speechRegion}.tts.speech.microsoft.com/");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);
        });
        // Cached by text hash (docs/06 section 9 point 1): wrap, don't replace, the real provider.
        builder.Services.AddSingleton<ITextToSpeech>(sp =>
            new CachingTextToSpeech(sp.GetRequiredService<AzureTextToSpeech>(), sp.GetRequiredService<IMemoryCache>()));
    }

    var groqApiKey = builder.Configuration["Groq:ApiKey"];
    var aiApiKey = builder.Configuration["AI:ApiKey"];
    if (!string.IsNullOrWhiteSpace(groqApiKey))
    {
        // Free-tier provider (console.groq.com, no card required) — tried first since it costs nothing.
        builder.Services.AddHttpClient("GroqAiService", client =>
        {
            client.BaseAddress = new Uri("https://api.groq.com/");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {groqApiKey}");
        });
        var groqModel = builder.Configuration["Groq:Model"];
        builder.Services.AddScoped<IAIService>(sp =>
        {
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("GroqAiService");
            return new GroqAiService(client, string.IsNullOrWhiteSpace(groqModel) ? "openai/gpt-oss-120b" : groqModel);
        });
    }
    else if (!string.IsNullOrWhiteSpace(aiApiKey))
    {
        builder.Services.AddHttpClient("AnthropicAiService", client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.DefaultRequestHeaders.Add("x-api-key", aiApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });
        var aiModel = builder.Configuration["AI:Model"];
        builder.Services.AddScoped<IAIService>(sp =>
        {
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("AnthropicAiService");
            return new AnthropicAiService(client, string.IsNullOrWhiteSpace(aiModel) ? "claude-sonnet-4-5" : aiModel);
        });
    }
    else
    {
        // No provider key configured: the real, documented fallback (docs/01 section 15), not a
        // placeholder. Exercises the exact same prompt-injection and verification logic as a real model.
        builder.Services.AddSingleton<IAIService, FakeAiService>();
    }

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

        // Tighter, named limits on sensitive/expensive endpoints (docs/03 section 12), on top of the
        // global one above. Partitioned per-IP, since most of these are reachable before or regardless
        // of login (auth itself, and anonymous repo/interview abuse attempts).
        static Func<HttpContext, RateLimitPartition<string>> PerIpPartition(int permitLimit, TimeSpan window) =>
            httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 });

        options.AddPolicy("auth", PerIpPartition(10, TimeSpan.FromMinutes(1)));
        options.AddPolicy("upload", PerIpPartition(5, TimeSpan.FromMinutes(1)));
        options.AddPolicy("analysis", PerIpPartition(5, TimeSpan.FromMinutes(1)));
        options.AddPolicy("interview", PerIpPartition(20, TimeSpan.FromMinutes(1)));
        options.AddPolicy("partner", PerIpPartition(5, TimeSpan.FromMinutes(1)));
    });

    var app = builder.Build();

    // STUB: seeds on every Development startup rather than via a separate CLI command; revisit if
    // that becomes slow. Never runs outside Development (CLAUDE.md P02).
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HerProofDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        await DbSeeder.SeedAsync(db, userManager, roleManager, seedDemoData: true);
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // Friendly HTML error pages for browser navigation; API clients under /api get their own plain
    // status codes untouched, since re-executing to an HTML view would break JSON consumers (Electron).
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/api"),
        branch => branch.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}"));

    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; media-src 'self' blob:; object-src 'none'; frame-ancestors 'none'";
        // Microphone is allowed for this origin only (P11's voice interview, candidate-initiated,
        // consent-gated) — camera and geolocation stay denied; HerProof never uses either.
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(self), geolocation=()";
        await next();
    });

    app.UseStaticFiles();

    app.UseRouting();
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapHealthChecks("/healthz");
    app.MapHealthChecks("/readyz");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "HerProof.Web terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Exposes the entry point to WebApplicationFactory&lt;Program&gt; in integration tests.</summary>
public partial class Program
{
}
