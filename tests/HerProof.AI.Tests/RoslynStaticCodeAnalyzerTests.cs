using HerProof.AI;
using HerProof.Application.Repositories;
using Xunit;

namespace HerProof.AI.Tests;

public class RoslynStaticCodeAnalyzerTests
{
    private readonly RoslynStaticCodeAnalyzer _analyzer = new();

    [Fact]
    public void Any_cs_file_produces_a_csharp_language_finding()
    {
        var files = new[] { new FetchedRepositoryFile("Program.cs", "class Program { static void Main() { } }") };

        var findings = _analyzer.Analyze(files);

        Assert.Contains(findings, f => f.CompetencyName == "C#");
    }

    [Fact]
    public void Non_cs_files_are_not_analyzed()
    {
        var files = new[] { new FetchedRepositoryFile("README.md", "# Hello") };

        var findings = _analyzer.Analyze(files);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detects_oop_evidence_from_class_and_interface_declarations()
    {
        var content = "public interface IOrderRepository { } public class OrderRepository : IOrderRepository { }";
        var findings = _analyzer.Analyze([new FetchedRepositoryFile("src/Order.cs", content)]);

        Assert.Contains(findings, f => f.CompetencyName == "OOP");
    }

    [Fact]
    public void Detects_dependency_injection_from_constructor_with_interface_parameter()
    {
        var content = """
            public class OrderService
            {
                private readonly IOrderRepository _repository;
                public OrderService(IOrderRepository repository) { _repository = repository; }
            }
            """;

        var findings = _analyzer.Analyze([new FetchedRepositoryFile("src/OrderService.cs", content)]);

        Assert.Contains(findings, f => f.CompetencyName == "Dependency injection");
    }

    [Fact]
    public void Detects_api_controller_evidence()
    {
        var content = """
            [ApiController]
            public class OrdersController : ControllerBase
            {
                public IActionResult Get() => Ok();
            }
            """;

        var findings = _analyzer.Analyze([new FetchedRepositoryFile("src/OrdersController.cs", content)]);

        Assert.Contains(findings, f => f.CompetencyName == "API development");
        Assert.Contains(findings, f => f.CompetencyName == "ASP.NET Core");
    }

    [Fact]
    public void Detects_error_handling_from_try_catch()
    {
        var content = "public class X { void M() { try { } catch (Exception) { } } }";

        var findings = _analyzer.Analyze([new FetchedRepositoryFile("src/X.cs", content)]);

        Assert.Contains(findings, f => f.CompetencyName == "Error handling");
    }

    [Fact]
    public void Detects_testing_evidence_from_fact_attribute()
    {
        var content = "public class OrderServiceTests { [Fact] public void It_works() { } }";

        var findings = _analyzer.Analyze([new FetchedRepositoryFile("tests/OrderServiceTests.cs", content)]);

        Assert.Contains(findings, f => f.CompetencyName == "Testing");
    }

    [Fact]
    public void Detects_sql_evidence_from_raw_sql_calls()
    {
        var content = "public class X { void M(DbContext db) { db.Database.ExecuteSqlRaw(\"DELETE FROM Orders\"); } }";

        var findings = _analyzer.Analyze([new FetchedRepositoryFile("src/X.cs", content)]);

        Assert.Contains(findings, f => f.CompetencyName == "SQL");
    }

    [Fact]
    public void Every_finding_has_a_file_path_that_was_actually_analyzed()
    {
        var content = "public class OrderService { public OrderService(IOrderRepository r) { } }";
        var files = new[] { new FetchedRepositoryFile("src/OrderService.cs", content) };

        var findings = _analyzer.Analyze(files);

        Assert.All(findings, f => Assert.Equal("src/OrderService.cs", f.FilePath));
    }

    [Fact]
    public void Malformed_cs_file_does_not_throw()
    {
        var files = new[] { new FetchedRepositoryFile("src/Broken.cs", "this is not { valid c# at all +++ ---") };

        var exception = Record.Exception(() => _analyzer.Analyze(files));

        Assert.Null(exception);
    }
}
