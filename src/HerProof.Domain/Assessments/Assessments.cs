using HerProof.Domain.Common;

namespace HerProof.Domain.Assessments;

public enum AssessmentStatus
{
    Draft,
    Published,
    Archived,
}

public class Assessment : Entity
{
    private readonly List<AssessmentCompetency> _competencies = [];
    private readonly List<RubricCriterion> _rubricCriteria = [];
    private readonly List<AssessmentQuestion> _questions = [];

    public string Title { get; private set; }
    public string Description { get; private set; }
    public string AssessmentType { get; private set; }
    public int DurationMinutes { get; private set; }
    public Guid CreatedBy { get; init; }
    public AssessmentStatus Status { get; private set; } = AssessmentStatus.Draft;

    public IReadOnlyCollection<AssessmentCompetency> Competencies => _competencies;
    public IReadOnlyCollection<RubricCriterion> RubricCriteria => _rubricCriteria;
    public IReadOnlyCollection<AssessmentQuestion> Questions => _questions;

    private Assessment()
    {
        Title = string.Empty;
        Description = string.Empty;
        AssessmentType = string.Empty;
    }

    public Assessment(string title, string description, string assessmentType, int durationMinutes, Guid createdBy)
    {
        Title = title;
        Description = description;
        AssessmentType = assessmentType;
        DurationMinutes = durationMinutes;
        CreatedBy = createdBy;
    }

    public AssessmentCompetency CoverCompetency(Guid competencyId, decimal weight)
    {
        var mapping = new AssessmentCompetency(Id, competencyId, weight);
        _competencies.Add(mapping);
        return mapping;
    }

    public RubricCriterion AddRubricCriterion(string name, decimal weight, string descriptor)
    {
        var criterion = new RubricCriterion(Id, name, weight, descriptor);
        _rubricCriteria.Add(criterion);
        return criterion;
    }

    public AssessmentQuestion AddQuestion(int order, string title, string prompt, string starterCode, string fileName, AssessmentQuestionType questionType)
    {
        var question = new AssessmentQuestion(Id, order, title, prompt, starterCode, fileName, questionType);
        _questions.Add(question);
        return question;
    }

    public void Publish() => Status = AssessmentStatus.Published;

    public void Archive() => Status = AssessmentStatus.Archived;
}

/// <summary>A structured practical question (docs/05 P13 polish): "write this feature" vs "find and
/// fix the bug in this code", each with its own starter code shown in its own editor tab — not just
/// one freeform textarea with no prompt at all.</summary>
public enum AssessmentQuestionType
{
    Implement,
    FixBug,
}

public class AssessmentQuestion : Entity
{
    public Guid AssessmentId { get; init; }
    public int Order { get; init; }
    public string Title { get; private set; }
    public string Prompt { get; private set; }
    public string StarterCode { get; private set; }
    public string FileName { get; private set; }
    public AssessmentQuestionType QuestionType { get; private set; }

    private AssessmentQuestion()
    {
        Title = string.Empty;
        Prompt = string.Empty;
        StarterCode = string.Empty;
        FileName = string.Empty;
    }

    public AssessmentQuestion(Guid assessmentId, int order, string title, string prompt, string starterCode, string fileName, AssessmentQuestionType questionType)
    {
        AssessmentId = assessmentId;
        Order = order;
        Title = title;
        Prompt = prompt;
        StarterCode = starterCode;
        FileName = fileName;
        QuestionType = questionType;
    }
}

public class AssessmentCompetency : Entity
{
    public Guid AssessmentId { get; init; }
    public Guid CompetencyId { get; init; }
    public decimal Weight { get; init; }

    private AssessmentCompetency()
    {
    }

    public AssessmentCompetency(Guid assessmentId, Guid competencyId, decimal weight)
    {
        AssessmentId = assessmentId;
        CompetencyId = competencyId;
        Weight = weight;
    }
}

public class RubricCriterion : Entity
{
    public Guid AssessmentId { get; init; }
    public string Name { get; init; }
    public decimal Weight { get; init; }
    public string Descriptor { get; init; }

    private RubricCriterion()
    {
        Name = string.Empty;
        Descriptor = string.Empty;
    }

    public RubricCriterion(Guid assessmentId, string name, decimal weight, string descriptor)
    {
        AssessmentId = assessmentId;
        Name = name;
        Weight = weight;
        Descriptor = descriptor;
    }
}

/// <summary>State machine per docs/01 section 10.</summary>
public enum AssessmentSessionStatus
{
    Invited,
    ConsentGiven,
    InProgress,
    Interrupted,
    Submitted,
    TimedOut,
    Evaluated,
    Reviewed,
}

public class AssessmentSession : Entity
{
    private readonly List<IntegrityEvent> _integrityEvents = [];

    public Guid AssessmentId { get; init; }
    public Guid CandidateId { get; init; }
    public AssessmentSessionStatus Status { get; private set; } = AssessmentSessionStatus.Invited;
    public DateTime? ConsentAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public bool AccommodationGranted { get; private set; }
    public AssessmentSubmission? Submission { get; private set; }

    public IReadOnlyCollection<IntegrityEvent> IntegrityEvents => _integrityEvents;

    private AssessmentSession()
    {
    }

    public AssessmentSession(Guid assessmentId, Guid candidateId)
    {
        AssessmentId = assessmentId;
        CandidateId = candidateId;
    }

    public void GrantAccommodation() => AccommodationGranted = true;

    /// <summary>Consent screen lists exactly what is recorded before this is called (docs/01 section 8).</summary>
    public void GiveConsent(DateTime nowUtc)
    {
        Status = AssessmentSessionStatus.ConsentGiven;
        ConsentAtUtc = nowUtc;
    }

    public void Start(DateTime nowUtc)
    {
        Status = AssessmentSessionStatus.InProgress;
        StartedAtUtc = nowUtc;
    }

    /// <summary>A connection/device gap is recorded as "connection lost", never as misconduct
    /// (docs/01 section 8). Resuming never re-penalises the candidate.</summary>
    public void Interrupt() => Status = AssessmentSessionStatus.Interrupted;

    public void Resume() => Status = AssessmentSessionStatus.InProgress;

    /// <summary>Events are flags for human review only; this never auto-fails a candidate
    /// (CLAUDE.md rule 5).</summary>
    public IntegrityEvent RecordIntegrityEvent(string eventType, DateTime occurredAtUtc, string metadataJson, string clientSeq)
    {
        var @event = new IntegrityEvent(Id, eventType, occurredAtUtc, metadataJson, clientSeq);
        _integrityEvents.Add(@event);
        return @event;
    }

    public AssessmentSubmission Submit(string submissionReference, DateTime submittedAtUtc)
    {
        Status = AssessmentSessionStatus.Submitted;
        SubmittedAtUtc = submittedAtUtc;
        Submission = new AssessmentSubmission(Id, submissionReference, submittedAtUtc);
        return Submission;
    }

    /// <summary>Time limit reached: work auto-submitted, same rule as a manual submit (docs/01 section 10).</summary>
    public AssessmentSubmission TimeOut(string submissionReference, DateTime nowUtc)
    {
        Status = AssessmentSessionStatus.TimedOut;
        Submission = new AssessmentSubmission(Id, submissionReference, nowUtc);
        return Submission;
    }

    public void MarkEvaluated() => Status = AssessmentSessionStatus.Evaluated;

    public void MarkReviewed() => Status = AssessmentSessionStatus.Reviewed;
}

/// <summary>Append-only (docs/02 rule 1). Only the documented event list may ever be written here
/// (docs/07 section 5) — no screen capture, no keylogging, no clipboard reads.</summary>
public class IntegrityEvent : Entity, IAppendOnly
{
    public Guid SessionId { get; init; }
    public string EventType { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string MetadataJson { get; init; }

    /// <summary>Per-session monotonic counter so the server can detect gaps/duplicates after
    /// offline sync (docs/02).</summary>
    public string ClientSeq { get; init; }

    private IntegrityEvent()
    {
        EventType = string.Empty;
        MetadataJson = "{}";
        ClientSeq = string.Empty;
    }

    public IntegrityEvent(Guid sessionId, string eventType, DateTime occurredAtUtc, string metadataJson, string clientSeq)
    {
        SessionId = sessionId;
        EventType = eventType;
        OccurredAtUtc = occurredAtUtc;
        MetadataJson = metadataJson;
        ClientSeq = clientSeq;
    }
}

public class AssessmentSubmission : Entity
{
    public Guid SessionId { get; init; }
    public string SubmissionReference { get; init; }
    public string? TestResultsJson { get; private set; }
    public DateTime SubmittedAtUtc { get; init; }
    public AssessmentReview? Review { get; private set; }

    private AssessmentSubmission()
    {
        SubmissionReference = string.Empty;
    }

    public AssessmentSubmission(Guid sessionId, string submissionReference, DateTime submittedAtUtc)
    {
        SessionId = sessionId;
        SubmissionReference = submissionReference;
        SubmittedAtUtc = submittedAtUtc;
    }

    /// <summary>If the sandbox is unavailable, never fabricate results: store "not executed"
    /// instead (docs/01 section 15).</summary>
    public void RecordAutomatedResults(string testResultsJson) => TestResultsJson = testResultsJson;

    public AssessmentReview AddReview(Guid assessorId, string criterionScoresJson, string feedback, string reviewStatus, DateTime reviewedAtUtc)
    {
        Review = new AssessmentReview(Id, assessorId, criterionScoresJson, feedback, reviewStatus, reviewedAtUtc);
        return Review;
    }
}

/// <summary>Append-only (docs/02 rule 1). Only role Assessor may create this
/// (CLAUDE.md rule 1 — enforced in the Application layer, not here).</summary>
public class AssessmentReview : Entity, IAppendOnly
{
    public Guid SubmissionId { get; init; }
    public Guid AssessorId { get; init; }
    public string CriterionScoresJson { get; init; }
    public string Feedback { get; init; }
    public string ReviewStatus { get; init; }
    public DateTime ReviewedAtUtc { get; init; }

    private AssessmentReview()
    {
        CriterionScoresJson = "{}";
        Feedback = string.Empty;
        ReviewStatus = string.Empty;
    }

    public AssessmentReview(Guid submissionId, Guid assessorId, string criterionScoresJson, string feedback, string reviewStatus, DateTime reviewedAtUtc)
    {
        SubmissionId = submissionId;
        AssessorId = assessorId;
        CriterionScoresJson = criterionScoresJson;
        Feedback = feedback;
        ReviewStatus = reviewStatus;
        ReviewedAtUtc = reviewedAtUtc;
    }
}
