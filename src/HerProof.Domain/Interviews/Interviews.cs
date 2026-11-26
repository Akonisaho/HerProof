using HerProof.Domain.Common;

namespace HerProof.Domain.Interviews;

public enum InterviewMode
{
    Voice,
    Text,
}

public enum InterviewStatus
{
    InProgress,
    Completed,
    Abandoned,
}

public class Interview : Entity
{
    private readonly List<InterviewQuestion> _questions = [];

    public Guid CandidateId { get; init; }
    public Guid RepositoryId { get; init; }

    /// <summary>Null means a general, opportunity-independent interview: the candidate is proving a
    /// competency for her own Skills Passport, not for one specific posting (docs/05: "evidence before
    /// verification" shouldn't require an opportunity to exist first).</summary>
    public Guid? OpportunityId { get; init; }
    public InterviewMode Mode { get; private set; }
    public InterviewStatus Status { get; private set; } = InterviewStatus.InProgress;
    public string InterviewerPersona { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>docs/05 P18: set only by DbSeeder's pre-generated demo interview, so the candidate-
    /// facing UI can show a "Demo data" banner rather than presenting cached results as live ones.</summary>
    public bool IsDemoData { get; private set; }

    public IReadOnlyCollection<InterviewQuestion> Questions => _questions;

    private Interview()
    {
        InterviewerPersona = string.Empty;
    }

    public Interview(Guid candidateId, Guid repositoryId, Guid? opportunityId, InterviewMode mode, string interviewerPersona, DateTime startedAtUtc)
    {
        CandidateId = candidateId;
        RepositoryId = repositoryId;
        OpportunityId = opportunityId;
        Mode = mode;
        InterviewerPersona = interviewerPersona;
        StartedAtUtc = startedAtUtc;
    }

    /// <summary>Fallback when microphone/speech is unavailable (docs/01 section 15): same rubric, no
    /// penalty for switching.</summary>
    public void SwitchToText() => Mode = InterviewMode.Text;

    public InterviewQuestion AskQuestion(Guid competencyId, string questionText, string? snippetFilePath, string? snippetLines, int questionOrder)
    {
        var question = new InterviewQuestion(Id, competencyId, questionText, snippetFilePath, snippetLines, questionOrder);
        _questions.Add(question);
        return question;
    }

    public void Complete(DateTime completedAtUtc)
    {
        Status = InterviewStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Abandon() => Status = InterviewStatus.Abandoned;

    public void MarkAsDemoData() => IsDemoData = true;
}

public class InterviewQuestion : Entity
{
    private readonly List<InterviewTurn> _turns = [];

    public Guid InterviewId { get; init; }
    public Guid CompetencyId { get; init; }
    public string QuestionText { get; init; }
    public string? SnippetFilePath { get; init; }
    public string? SnippetLines { get; init; }
    public int QuestionOrder { get; init; }

    public IReadOnlyCollection<InterviewTurn> Turns => _turns;

    private InterviewQuestion()
    {
        QuestionText = string.Empty;
    }

    public InterviewQuestion(Guid interviewId, Guid competencyId, string questionText, string? snippetFilePath, string? snippetLines, int questionOrder)
    {
        InterviewId = interviewId;
        CompetencyId = competencyId;
        QuestionText = questionText;
        SnippetFilePath = snippetFilePath;
        SnippetLines = snippetLines;
        QuestionOrder = questionOrder;
    }

    /// <summary>Probe ladder: maximum three probes per question (docs/01 section 7, docs/06).</summary>
    public InterviewTurn AddTurn(TurnKind turnKind, string promptText, DateTime createdAtUtc)
    {
        var probeCount = _turns.Count(t => t.TurnKind == TurnKind.Probe);
        if (turnKind == TurnKind.Probe && probeCount >= 3)
        {
            throw new InvalidOperationException("Maximum of three probes per question has been reached.");
        }

        var turn = new InterviewTurn(Id, _turns.Count, turnKind, promptText, createdAtUtc);
        _turns.Add(turn);
        return turn;
    }
}

public enum TurnKind
{
    Question,
    Probe,
    Clarify,
}

/// <summary>Append-only (docs/02 rule 1). Transcript is corrected by the candidate before scoring
/// (docs/01 section 7) but the raw transcript is never deleted or overwritten in place.</summary>
public class InterviewTurn : Entity, IAppendOnly
{
    public Guid QuestionId { get; init; }
    public int TurnIndex { get; init; }
    public TurnKind TurnKind { get; init; }
    public string PromptText { get; init; }
    public string? TranscriptRaw { get; private set; }
    public string? TranscriptConfirmed { get; private set; }
    public string? RecordingReference { get; private set; }
    public int? DurationSeconds { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public InterviewEvaluation? Evaluation { get; private set; }

    private InterviewTurn()
    {
        PromptText = string.Empty;
    }

    public InterviewTurn(Guid questionId, int turnIndex, TurnKind turnKind, string promptText, DateTime createdAtUtc)
    {
        QuestionId = questionId;
        TurnIndex = turnIndex;
        TurnKind = turnKind;
        PromptText = promptText;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>New InterviewTurn rows are added for corrections; this single in-memory turn is only
    /// mutated before it is first persisted (append-only applies to persisted rows, docs/02 rule 1).</summary>
    public void RecordAnswer(string transcriptRaw, string? recordingReference, int durationSeconds)
    {
        TranscriptRaw = transcriptRaw;
        RecordingReference = recordingReference;
        DurationSeconds = durationSeconds;
    }

    public void ConfirmTranscript(string transcriptConfirmed) => TranscriptConfirmed = transcriptConfirmed;

    public InterviewEvaluation Evaluate(Verdict verdict, string evidenceSummary, string codeClaimsVsAnswerJson)
    {
        Evaluation = new InterviewEvaluation(Id, verdict, evidenceSummary, codeClaimsVsAnswerJson);
        return Evaluation;
    }
}

/// <summary>Verdict wording is deliberately non-accusatory (docs/00 section 2, item 5): "not
/// demonstrated through this evidence", never "cheater" or similar, anywhere this is displayed.</summary>
public enum Verdict
{
    Understood,
    Partial,
    NotDemonstrated,
}

public class InterviewEvaluation : Entity
{
    public Guid TurnId { get; init; }
    public Verdict Verdict { get; init; }
    public string EvidenceSummary { get; init; }
    public string CodeClaimsVsAnswerJson { get; init; }
    public string ReviewerStatus { get; private set; }

    private InterviewEvaluation()
    {
        EvidenceSummary = string.Empty;
        CodeClaimsVsAnswerJson = "{}";
        ReviewerStatus = "Pending";
    }

    public InterviewEvaluation(Guid turnId, Verdict verdict, string evidenceSummary, string codeClaimsVsAnswerJson)
    {
        TurnId = turnId;
        Verdict = verdict;
        EvidenceSummary = evidenceSummary;
        CodeClaimsVsAnswerJson = codeClaimsVsAnswerJson;
        ReviewerStatus = "Pending";
    }

    public void MarkReviewed() => ReviewerStatus = "Reviewed";
}
