namespace HerProof.Application.Assessments;

/// <summary>What's stored in AssessmentSubmission.SubmissionReference as JSON (STUB(HP-028):
/// single-file plain text for now, not the zipped workspace docs/07 section 9 describes). One shared
/// type for both serializing (AssessmentSessionsController) and deserializing (the worker) — the
/// property-casing mismatch that caused a NullReferenceException here once is exactly what sharing
/// the type, plus JsonSerializerDefaults.Web everywhere, prevents from happening again.</summary>
public record SubmittedFile(string Path, string Content);
