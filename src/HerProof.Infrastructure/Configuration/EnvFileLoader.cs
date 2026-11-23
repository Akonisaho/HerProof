namespace HerProof.Infrastructure.Configuration;

/// <summary>Loads a `.env` file (the one `.env.example` at the repo root documents) into real process
/// environment variables, so ASP.NET Core's built-in `AddEnvironmentVariables()` configuration
/// provider picks them up exactly like it would any other env var — no new dependency, since .NET
/// doesn't read `.env` files on its own. A real environment variable set some other way always wins
/// (never overwritten), so this is purely a local-dev convenience, not a second source of truth.
/// Must run before `WebApplication.CreateBuilder`/`Host.CreateApplicationBuilder`, since those snapshot
/// environment variables into configuration at that point.</summary>
public static class EnvFileLoader
{
    public static void LoadFromNearestEnvFile(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                Load(candidate);
                return;
            }

            directory = directory.Parent;
        }
    }

    private static void Load(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"');

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
