using HerProof.Application.Repositories;

namespace HerProof.Application.Sandbox;

/// <summary>Materialises fetched, pinned-commit content to a throwaway directory so it can be
/// mounted read-only into the sandbox container. Always cleaned up, even on failure — this directory
/// must never outlive the single analysis it was created for (docs/03: minimise what's retained).</summary>
public static class SandboxWorkspace
{
    public static async Task<T> WithMaterializedContentAsync<T>(FetchedRepositoryContent content, Func<string, Task<T>> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"herproof-sandbox-input-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;

            foreach (var file in content.Files)
            {
                var destination = Path.GetFullPath(Path.Combine(directory, file.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!destination.StartsWith(root, StringComparison.Ordinal))
                {
                    // Repo content is untrusted input (CLAUDE.md rule 7): a path like "../../x" must
                    // never be allowed to write outside the throwaway workspace.
                    continue;
                }

                var destinationDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                await File.WriteAllTextAsync(destination, file.Content);
            }

            return await action(directory);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: OS temp cleanup will eventually reclaim it.
            }
        }
    }
}
