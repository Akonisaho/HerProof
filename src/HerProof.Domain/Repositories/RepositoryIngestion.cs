using HerProof.Domain.Common;

namespace HerProof.Domain.Repositories;

public class GitHubConnection : Entity
{
    public Guid CandidateId { get; init; }
    public string ExternalAccountId { get; init; }

    /// <summary>Ciphertext only, produced by ASP.NET Data Protection (CLAUDE.md rule 10, docs/02 rule 3).</summary>
    public byte[] EncryptedToken { get; private set; }

    public DateTime ConnectedAtUtc { get; init; }
    public bool IsActive { get; private set; } = true;

    private GitHubConnection()
    {
        ExternalAccountId = string.Empty;
        EncryptedToken = [];
    }

    public GitHubConnection(Guid candidateId, string externalAccountId, byte[] encryptedToken, DateTime connectedAtUtc)
    {
        CandidateId = candidateId;
        ExternalAccountId = externalAccountId;
        EncryptedToken = encryptedToken;
        ConnectedAtUtc = connectedAtUtc;
    }

    public void Revoke()
    {
        IsActive = false;
        EncryptedToken = [];
    }
}

public enum RepositoryVisibility
{
    Public,
    Private,
}

public enum ContributorVerificationStatus
{
    NotChecked,
    Confirmed,
    NotConfirmed,
}

/// <summary>ADR-06: commit SHA is pinned at submission for reproducibility and fairness.</summary>
public class Repository : Entity
{
    public Guid CandidateId { get; init; }
    public string RepositoryName { get; init; }
    public string RepositoryUrl { get; init; }
    public string Branch { get; init; }
    public string CommitSha { get; init; }
    public RepositoryVisibility Visibility { get; init; }
    public DateTime SubmittedAtUtc { get; init; }

    /// <summary>docs/05: was the candidate actually confirmed as a contributor to this repository via
    /// GitHub's real contributor data? Flagged, never blocking — see GitHubContributorVerifier.</summary>
    public ContributorVerificationStatus ContributorStatus { get; private set; } = ContributorVerificationStatus.NotChecked;

    private Repository()
    {
        RepositoryName = string.Empty;
        RepositoryUrl = string.Empty;
        Branch = string.Empty;
        CommitSha = string.Empty;
    }

    public Repository(Guid candidateId, string repositoryName, string repositoryUrl, string branch, string commitSha, RepositoryVisibility visibility, DateTime submittedAtUtc)
    {
        CandidateId = candidateId;
        RepositoryName = repositoryName;
        RepositoryUrl = repositoryUrl;
        Branch = branch;
        CommitSha = commitSha;
        Visibility = visibility;
        SubmittedAtUtc = submittedAtUtc;
    }

    public void SetContributorStatus(ContributorVerificationStatus status) => ContributorStatus = status;
}

public enum RepositoryAnalysisStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    NeedsRetry,
}

public class RepositoryAnalysis : Entity
{
    private readonly List<RepositoryFinding> _findings = [];

    public Guid RepositoryId { get; init; }
    public RepositoryAnalysisStatus Status { get; private set; } = RepositoryAnalysisStatus.Pending;
    public string TechnologiesDetectedJson { get; private set; }
    public string? Summary { get; private set; }
    public string? ModelVersion { get; private set; }
    public string? PromptVersion { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; private set; }
    public ExecutionReport? ExecutionReport { get; private set; }

    /// <summary>docs/05 P18: set only by DbSeeder's pre-analysed demo repository, so the candidate-
    /// facing UI can show a "Demo data" banner rather than presenting cached results as live ones.</summary>
    public bool IsDemoData { get; private set; }

    public IReadOnlyCollection<RepositoryFinding> Findings => _findings;

    private RepositoryAnalysis()
    {
        TechnologiesDetectedJson = "[]";
    }

    public RepositoryAnalysis(Guid repositoryId, DateTime createdAtUtc)
    {
        RepositoryId = repositoryId;
        TechnologiesDetectedJson = "[]";
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkInProgress() => Status = RepositoryAnalysisStatus.InProgress;

    public void MarkNeedsRetry() => Status = RepositoryAnalysisStatus.NeedsRetry;

    /// <summary>A finding whose file/line reference cannot be verified against the pinned commit
    /// must be discarded before this is called (docs/01 section 6).</summary>
    public RepositoryFinding AddFinding(Guid competencyId, string findingType, string filePath, string lineReference, string description, string evidence, string confidence)
    {
        var finding = new RepositoryFinding(Id, competencyId, findingType, filePath, lineReference, description, evidence, confidence);
        _findings.Add(finding);
        return finding;
    }

    public void Complete(string technologiesDetectedJson, string summary, string modelVersion, string promptVersion, DateTime completedAtUtc)
    {
        TechnologiesDetectedJson = technologiesDetectedJson;
        Summary = summary;
        ModelVersion = modelVersion;
        PromptVersion = promptVersion;
        Status = RepositoryAnalysisStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Fail() => Status = RepositoryAnalysisStatus.Failed;

    public void MarkAsDemoData() => IsDemoData = true;

    public ExecutionReport RecordExecution(bool restoreOk, bool buildOk, int testsPassed, int testsFailed, string? logExcerpt, string? sandboxImageDigest)
    {
        ExecutionReport = new ExecutionReport(Id, restoreOk, buildOk, testsPassed, testsFailed, logExcerpt, sandboxImageDigest);
        return ExecutionReport;
    }
}

public class RepositoryFinding : Entity
{
    public Guid AnalysisId { get; init; }
    public Guid CompetencyId { get; init; }
    public string FindingType { get; init; }
    public string FilePath { get; init; }
    public string LineReference { get; init; }
    public string Description { get; init; }
    public string Evidence { get; init; }
    public string Confidence { get; init; }

    private RepositoryFinding()
    {
        FindingType = string.Empty;
        FilePath = string.Empty;
        LineReference = string.Empty;
        Description = string.Empty;
        Evidence = string.Empty;
        Confidence = string.Empty;
    }

    public RepositoryFinding(Guid analysisId, Guid competencyId, string findingType, string filePath, string lineReference, string description, string evidence, string confidence)
    {
        AnalysisId = analysisId;
        CompetencyId = competencyId;
        FindingType = findingType;
        FilePath = filePath;
        LineReference = lineReference;
        Description = description;
        Evidence = evidence;
        Confidence = confidence;
    }
}

/// <summary>If the sandbox is unavailable, this is never created with fabricated results — the
/// analysis reports "not executed" instead (docs/01 section 15).</summary>
public class ExecutionReport : Entity
{
    public Guid AnalysisId { get; init; }
    public bool RestoreOk { get; init; }
    public bool BuildOk { get; init; }
    public int TestsPassed { get; init; }
    public int TestsFailed { get; init; }
    public string? LogExcerpt { get; init; }
    public string? SandboxImageDigest { get; init; }

    private ExecutionReport()
    {
    }

    public ExecutionReport(Guid analysisId, bool restoreOk, bool buildOk, int testsPassed, int testsFailed, string? logExcerpt, string? sandboxImageDigest)
    {
        AnalysisId = analysisId;
        RestoreOk = restoreOk;
        BuildOk = buildOk;
        TestsPassed = testsPassed;
        TestsFailed = testsFailed;
        LogExcerpt = logExcerpt;
        SandboxImageDigest = sandboxImageDigest;
    }
}
