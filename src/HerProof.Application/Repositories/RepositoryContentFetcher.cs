namespace HerProof.Application.Repositories;

public record FetchedRepositoryFile(string Path, string Content);

public record FetchedRepositoryContent(string CommitSha, IReadOnlyList<FetchedRepositoryFile> Files);

/// <summary>Orchestrates P07's "fetch tree, manifests, README, test files and selected source files
/// at that SHA" (docs/05). Selection rules live in RepositoryContentSelector (pure, tested); this
/// just does the I/O. The repository analysis pipeline (P08) consumes this.</summary>
public class RepositoryContentFetcher(IGitHubClient gitHubClient)
{
    public async Task<FetchedRepositoryContent> FetchAsync(string owner, string repo, string commitSha, CancellationToken cancellationToken = default)
    {
        var tree = await gitHubClient.GetTreeAsync(owner, repo, commitSha, cancellationToken);
        var selected = RepositoryContentSelector.Select(tree);

        var files = new List<FetchedRepositoryFile>(selected.Count);
        foreach (var entry in selected)
        {
            var content = await gitHubClient.GetFileContentAsync(owner, repo, commitSha, entry.Path, cancellationToken);
            files.Add(new FetchedRepositoryFile(entry.Path, content));
        }

        return new FetchedRepositoryContent(commitSha, files);
    }
}
