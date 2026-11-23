using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HerProof.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations` / `database update` can build the
/// context without spinning up the full Web host. Reads ConnectionStrings__Default like the app
/// does, falling back to the docker compose default for local development.</summary>
public class HerProofDbContextFactory : IDesignTimeDbContextFactory<HerProofDbContext>
{
    public HerProofDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5435;Database=herproof;Username=herproof;Password=dev_only_change_me";

        var optionsBuilder = new DbContextOptionsBuilder<HerProofDbContext>();
        optionsBuilder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

        return new HerProofDbContext(optionsBuilder.Options);
    }
}
