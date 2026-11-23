namespace HerProof.Application.Assessments;

public record IncomingIntegrityEvent(string EventType, DateTime OccurredAtUtc, string MetadataJson, string ClientSeq);

public record IngestionResult(IReadOnlyList<IncomingIntegrityEvent> ToPersist, IReadOnlyList<string> AcceptedClientSeqs, IReadOnlyList<string> Gaps);

/// <summary>Pure logic for the integrity-events ingestion endpoint (docs/05 P13, docs/07 section 6):
/// "server dedupes by (session_id, client_seq) and records gaps". Dedupe and gap detection are
/// informational only — a gap is never evidence of anything by itself (docs/07 section 7: thresholds
/// only choose what to highlight, never change a score or status).</summary>
public static class IntegrityEventIngestion
{
    public static IngestionResult Process(IReadOnlyList<IncomingIntegrityEvent> incoming, IReadOnlyCollection<string> alreadyPersistedClientSeqs)
    {
        var alreadySeen = new HashSet<string>(alreadyPersistedClientSeqs);
        var toPersist = new List<IncomingIntegrityEvent>();
        var accepted = new List<string>();

        foreach (var @event in incoming)
        {
            accepted.Add(@event.ClientSeq); // Already-seen events are still "accepted" from the client's
                                            // point of view — it should stop retrying them either way.
            if (alreadySeen.Add(@event.ClientSeq))
            {
                toPersist.Add(@event);
            }
        }

        var allSeqs = alreadySeen.Union(incoming.Select(e => e.ClientSeq))
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .OrderBy(n => n)
            .ToList();

        var gaps = new List<string>();
        for (var i = 1; i < allSeqs.Count; i++)
        {
            for (var missing = allSeqs[i - 1] + 1; missing < allSeqs[i]; missing++)
            {
                gaps.Add(missing.ToString());
            }
        }

        return new IngestionResult(toPersist, accepted, gaps);
    }
}
