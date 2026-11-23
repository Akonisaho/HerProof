using HerProof.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HerProof.Integration.Tests;

/// <summary>Every test class that needs the web host joins this collection instead of using
/// IClassFixture directly, so they share one HerProofWebApplicationFactory and never spin up two
/// in parallel — doing that triggers a known flaky WebApplicationFactory host-resolution race
/// ("entry point exited without ever building an IHost") when two instances of the same
/// WebApplicationFactory&lt;Program&gt; build concurrently on different xUnit threads.</summary>
[CollectionDefinition(Name)]
public class WebAppFactoryCollection : ICollectionFixture<HerProofWebApplicationFactory>
{
    public const string Name = "WebAppFactory";
}

public class HerProofWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<HerProofDbContext>>();
            services.AddDbContext<HerProofDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            });
        });

        builder.UseEnvironment("Testing");
    }
}
