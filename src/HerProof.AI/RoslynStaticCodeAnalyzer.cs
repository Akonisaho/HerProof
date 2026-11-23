using HerProof.Application.Repositories;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HerProof.AI;

/// <summary>Deterministic C# evidence via Roslyn syntax trees (ADR-09). One finding per
/// (competency, file): enough to cite real evidence without flooding the report with one row per
/// syntax node. Entirely offline — no network, no LLM, nothing to verify afterwards.</summary>
public class RoslynStaticCodeAnalyzer : IStaticCodeAnalyzer
{
    public IReadOnlyList<StaticFinding> Analyze(IReadOnlyList<FetchedRepositoryFile> files)
    {
        var findings = new List<StaticFinding>();

        foreach (var file in files.Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new StaticFinding("C#", "language", file.Path, null, "Contains C# source.", "File extension .cs"));

            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(file.Content, path: file.Path);
            }
            catch (Exception)
            {
                continue; // Malformed or unparsable file: skip analysis, keep the language finding above.
            }

            var root = tree.GetRoot();
            findings.AddRange(AnalyzeOopEvidence(root, file.Path));
            findings.AddRange(AnalyzeDependencyInjectionEvidence(root, file.Path));
            findings.AddRange(AnalyzeApiEvidence(root, file.Path));
            findings.AddRange(AnalyzeErrorHandlingEvidence(root, file.Path));
            findings.AddRange(AnalyzeTestingEvidence(root, file.Path));
            findings.AddRange(AnalyzeSqlEvidence(root, file.Path));
        }

        return findings;
    }

    private static IEnumerable<StaticFinding> AnalyzeOopEvidence(SyntaxNode root, string path)
    {
        var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
        if (types.Count == 0)
        {
            yield break;
        }

        var first = types[0];
        var classCount = types.OfType<ClassDeclarationSyntax>().Count();
        var interfaceCount = types.OfType<InterfaceDeclarationSyntax>().Count();
        yield return new StaticFinding(
            "OOP", "type-declarations", path, LineOf(first),
            $"Declares {classCount} class(es) and {interfaceCount} interface(s).",
            first.Identifier.Text);
    }

    private static IEnumerable<StaticFinding> AnalyzeDependencyInjectionEvidence(SyntaxNode root, string path)
    {
        var constructorsWithInterfaceParams = root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(ctor => ctor.ParameterList.Parameters.Any(p =>
                p.Type is IdentifierNameSyntax { Identifier.Text: var name } && name.StartsWith('I') && name.Length > 1 && char.IsUpper(name[1])));

        if (constructorsWithInterfaceParams is not null)
        {
            yield return new StaticFinding(
                "Dependency injection", "constructor-injection", path, LineOf(constructorsWithInterfaceParams),
                "Constructor accepts one or more interface-typed dependencies.",
                constructorsWithInterfaceParams.ParameterList.ToString());
        }
    }

    private static IEnumerable<StaticFinding> AnalyzeApiEvidence(SyntaxNode root, string path)
    {
        var controller = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal)
                || c.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString().Contains("ApiController", StringComparison.Ordinal))));

        if (controller is not null)
        {
            yield return new StaticFinding(
                "API development", "controller", path, LineOf(controller),
                $"Defines controller '{controller.Identifier.Text}'.", controller.Identifier.Text);
            yield return new StaticFinding(
                "ASP.NET Core", "controller", path, LineOf(controller),
                $"Defines controller '{controller.Identifier.Text}'.", controller.Identifier.Text);
        }
    }

    private static IEnumerable<StaticFinding> AnalyzeErrorHandlingEvidence(SyntaxNode root, string path)
    {
        var tryStatement = root.DescendantNodes().OfType<TryStatementSyntax>().FirstOrDefault();
        if (tryStatement is not null)
        {
            yield return new StaticFinding(
                "Error handling", "try-catch", path, LineOf(tryStatement),
                "Uses a try/catch block.", tryStatement.Catches.FirstOrDefault()?.ToString() ?? "try");
        }
    }

    private static IEnumerable<StaticFinding> AnalyzeTestingEvidence(SyntaxNode root, string path)
    {
        var testMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.AttributeLists.Any(al => al.Attributes.Any(a =>
                a.Name.ToString() is "Fact" or "Theory" or "Test" or "TestMethod")));

        if (testMethod is not null)
        {
            yield return new StaticFinding(
                "Testing", "test-method", path, LineOf(testMethod),
                $"Defines test method '{testMethod.Identifier.Text}'.", testMethod.Identifier.Text);
        }
    }

    private static IEnumerable<StaticFinding> AnalyzeSqlEvidence(SyntaxNode root, string path)
    {
        var sqlInvocation = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(i => i.Expression.ToString().Contains("FromSqlRaw", StringComparison.Ordinal)
                || i.Expression.ToString().Contains("ExecuteSqlRaw", StringComparison.Ordinal)
                || i.Expression.ToString().Contains("SqlCommand", StringComparison.Ordinal));

        if (sqlInvocation is not null)
        {
            yield return new StaticFinding(
                "SQL", "raw-sql-call", path, LineOf(sqlInvocation),
                "Executes a raw SQL statement.", sqlInvocation.ToString());
        }
    }

    private static string LineOf(SyntaxNode node) => (node.GetLocation().GetLineSpan().StartLinePosition.Line + 1).ToString();
}
