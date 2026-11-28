using HerProof.AI;
using HerProof.Application.AI;
using HerProof.Application.Audit;
using HerProof.Application.Repositories;
using HerProof.Application.Sandbox;
using HerProof.AssessmentWorker;
using HerProof.Infrastructure.Audit;
using HerProof.Infrastructure.Persistence;
using HerProof.Infrastructure.Repositories;
using HerProof.Infrastructure.Sandbox;
using Microsoft.EntityFrameworkCore;
using Serilog;

HerProof.Infrastructure.Configuration.EnvFileLoader.LoadFromNearestEnvFile(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddDbContext<HerProofDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(new AppendOnlyInterceptor()));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IGitHubClient, GitHubClient>(client =>
{
    // Unauthenticated requests are capped at 60/hour per IP — the actual repo-analysis workload
    // (fetching dozens of files per submission) is exactly what exhausts that fastest, so this is the
    // more important of the two places to apply the optional free token (see HerProof.Web's own
    // registration for the same logic).
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HerProof-Hackathon/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.Timeout = TimeSpan.FromSeconds(45);

    var gitHubToken = builder.Configuration["GitHub:Token"];
    if (!string.IsNullOrWhiteSpace(gitHubToken))
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", gitHubToken);
    }
});
builder.Services.AddScoped<RepositoryContentFetcher>();
builder.Services.AddSingleton<IStaticCodeAnalyzer, RoslynStaticCodeAnalyzer>();
builder.Services.AddScoped<RepositoryAnalysisOrchestrator>();

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
    builder.Services.AddSingleton<IAIService, FakeAiService>();
}

builder.Services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();
builder.Services.AddScoped<IAuditLogger, EfAuditLogger>();
builder.Services.AddHostedService<RepositoryAnalysisBackgroundService>();
builder.Services.AddHostedService<AssessmentEvaluationBackgroundService>();
builder.Services.AddHostedService<TranscriptRetentionBackgroundService>();

var host = builder.Build();
host.Run();
