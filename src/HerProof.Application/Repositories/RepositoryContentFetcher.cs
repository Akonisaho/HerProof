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

    /// <summary>Fetches only the given paths — one GitHub API call per file, no tree listing at all.
    /// Used when starting an interview: the repository's already-stored findings already name exactly
    /// which files matter, so there is no reason to re-fetch and re-select from the whole repo again
    /// (the original P08 analysis pass already did that broad work once). Cuts a ~40-call, 90-second
    /// interview start down to a handful of calls for the files actually being asked about.</summary>
    public async Task<FetchedRepositoryContent> FetchSelectedAsync(string owner, string repo, string commitSha, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var files = new List<FetchedRepositoryFile>(paths.Count);
        foreach (var path in paths.Distinct())
        {
            var content = await gitHubClient.GetFileContentAsync(owner, repo, commitSha, path, cancellationToken);
            files.Add(new FetchedRepositoryFile(path, content));
        }

        return new FetchedRepositoryContent(commitSha, files);
    }
}
