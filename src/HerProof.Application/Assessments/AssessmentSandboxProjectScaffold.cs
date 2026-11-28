using HerProof.Application.Sandbox;

namespace HerProof.Application.Assessments;

/// <summary>Wraps a candidate's submitted files into an actually-buildable, actually-testable project
/// before the sandbox sees them - without this, the sandbox's build step has nothing to operate on at
/// all, regardless of whether execution is enabled. Pinned package/library versions match exactly
/// what each language's sandbox image bakes in offline (docker/Dockerfile.sandbox,
/// docker/Dockerfile.sandbox.java) - network is disabled at container level (CLAUDE.md rule 6), so
/// only exactly these versions, already present in the image, can ever be used.</summary>
public static class AssessmentSandboxProjectScaffold
{
    public const string TestSdkVersion = "17.12.0";
    public const string XunitVersion = "2.9.3";
    public const string XunitRunnerVersion = "2.8.2";

    private const string CsprojContent = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>disable</Nullable>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="{TestSdkVersion}" />
            <PackageReference Include="xunit" Version="{XunitVersion}" />
            <PackageReference Include="xunit.runner.visualstudio" Version="{XunitRunnerVersion}" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>Builds the file set the sandbox will actually build/test: the candidate's own
    /// submitted files, any language-specific project scaffolding, and one hidden test file per
    /// question that has one - never shown to the candidate, authored by whoever created the
    /// assessment. C# gets a generated .csproj (xUnit); Java needs no project file at all - the
    /// image's own JUnit Platform Console Standalone jar runs directly against compiled .class files.</summary>
    public static List<SubmittedFile> Build(
        IReadOnlyList<SubmittedFile> submittedFiles,
        IReadOnlyList<(string FileName, string? HiddenTestCode)> questions,
        SandboxLanguage language = SandboxLanguage.CSharp)
    {
        var files = new List<SubmittedFile>(submittedFiles);
        var testIndex = 0;

        if (language == SandboxLanguage.Java)
        {
            foreach (var (_, hiddenTestCode) in questions)
            {
                if (string.IsNullOrWhiteSpace(hiddenTestCode))
                {
                    continue;
                }

                testIndex++;
                files.Add(new SubmittedFile($"HiddenTest{testIndex}.java", hiddenTestCode));
            }

            return files;
        }

        files.Add(new SubmittedFile("Submission.csproj", CsprojContent));
        foreach (var (_, hiddenTestCode) in questions)
        {
            if (string.IsNullOrWhiteSpace(hiddenTestCode))
            {
                continue;
            }

            testIndex++;
            files.Add(new SubmittedFile($"HiddenTests{testIndex}.cs", hiddenTestCode));
        }

        return files;
    }
}
