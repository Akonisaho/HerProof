using HerProof.Application.Repositories;
using Xunit;

namespace HerProof.Application.Tests;

public class RepositoryContentSelectorTests
{
    [Fact]
    public void Skips_vendored_directories()
    {
        var tree = new[]
        {
            new GitHubTreeEntry("node_modules/some-pkg/index.js", "blob", 100),
            new GitHubTreeEntry("bin/Debug/App.dll", "blob", 100),
            new GitHubTreeEntry("obj/Debug/Temp.cs", "blob", 100),
            new GitHubTreeEntry("src/Program.cs", "blob", 100),
        };

        var selected = RepositoryContentSelector.Select(tree);

        Assert.Single(selected);
        Assert.Equal("src/Program.cs", selected[0].Path);
    }

    [Fact]
    public void Skips_disallowed_binary_like_extensions()
    {
        var tree = new[]
        {
            new GitHubTreeEntry("logo.png", "blob", 100),
            new GitHubTreeEntry("app.exe", "blob", 100),
            new GitHubTreeEntry("Program.cs", "blob", 100),
        };

        var selected = RepositoryContentSelector.Select(tree);

        Assert.Single(selected);
        Assert.Equal("Program.cs", selected[0].Path);
    }

    [Fact]
    public void Ignores_tree_entries_that_are_directories_not_files()
    {
        var tree = new[]
        {
            new GitHubTreeEntry("src", "tree", null),
            new GitHubTreeEntry("src/Program.cs", "blob", 100),
        };

        var selected = RepositoryContentSelector.Select(tree);

        Assert.Single(selected);
    }

    [Fact]
    public void Readme_is_selected_first()
    {
        var tree = new[]
        {
            new GitHubTreeEntry("src/Program.cs", "blob", 100),
            new GitHubTreeEntry("README.md", "blob", 100),
        };

        var selected = RepositoryContentSelector.Select(tree);

        Assert.Equal("README.md", selected[0].Path);
    }

    [Fact]
    public void Respects_the_max_file_count_cap()
    {
        var tree = Enumerable.Range(0, RepositoryContentSelector.MaxFiles + 50)
            .Select(i => new GitHubTreeEntry($"src/File{i:D4}.cs", "blob", 10))
            .ToArray();

        var selected = RepositoryContentSelector.Select(tree);

        Assert.Equal(RepositoryContentSelector.MaxFiles, selected.Count);
    }

    [Fact]
    public void Respects_the_max_total_bytes_cap()
    {
        var tree = new[]
        {
            new GitHubTreeEntry("src/Huge.cs", "blob", RepositoryContentSelector.MaxTotalBytes),
            new GitHubTreeEntry("src/AnotherFile.cs", "blob", 10),
        };

        var selected = RepositoryContentSelector.Select(tree);

        // The huge file alone fills the budget; a later equally-ranked small file does not also fit
        // because the running total already hit the cap (a simple "first N that fit" greedy approach).
        Assert.Single(selected);
    }
}
