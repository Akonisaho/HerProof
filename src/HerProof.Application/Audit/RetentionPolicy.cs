namespace HerProof.Application.Audit;

/// <summary>docs/05 P17, CLAUDE.md rule 9 (retention): interview transcripts are the most privacy-
/// sensitive text HerProof stores at length. Verification records, audit log and review decisions
/// are never subject to this — they are append-only history that must survive, and retention is
/// honoured instead by anonymising the data subject (DataSubjectController.Delete), not by deleting
/// the record that something was verified.</summary>
public static class RetentionPolicy
{
    public static readonly TimeSpan TranscriptRetentionWindow = TimeSpan.FromDays(180);

    public const string RedactedPlaceholder = "[redacted by retention policy]";

    public static bool IsPastTranscriptRetention(DateTime createdAtUtc, DateTime nowUtc) =>
        nowUtc - createdAtUtc >= TranscriptRetentionWindow;
}
