using HerProof.Application.Assessments;
using Xunit;

namespace HerProof.Application.Tests;

public class IntegrityEventIngestionTests
{
    private static IncomingIntegrityEvent Event(string seq) => new("focus_lost", DateTime.UtcNow, "{}", seq);

    [Fact]
    public void New_events_are_queued_for_persistence_and_accepted()
    {
        var result = IntegrityEventIngestion.Process([Event("0"), Event("1")], alreadyPersistedClientSeqs: []);

        Assert.Equal(2, result.ToPersist.Count);
        Assert.Equal(["0", "1"], result.AcceptedClientSeqs);
    }

    [Fact]
    public void Already_persisted_events_are_deduped_but_still_reported_as_accepted()
    {
        // The client retries a batch after a flaky connection: the server must not double-insert, but
        // must still tell the client these were accepted so it stops retrying them (docs/07 section 6).
        var result = IntegrityEventIngestion.Process([Event("0"), Event("1")], alreadyPersistedClientSeqs: ["0"]);

        Assert.Single(result.ToPersist);
        Assert.Equal("1", result.ToPersist[0].ClientSeq);
        Assert.Equal(["0", "1"], result.AcceptedClientSeqs);
    }

    [Fact]
    public void No_gaps_are_reported_for_a_contiguous_sequence()
    {
        var result = IntegrityEventIngestion.Process([Event("0"), Event("1"), Event("2")], []);

        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void A_missing_sequence_number_is_reported_as_a_gap()
    {
        // client_seq 1 never arrived (lost or still offline) — docs/07 section 6: "records gaps".
        var result = IntegrityEventIngestion.Process([Event("0"), Event("2")], []);

        Assert.Equal(["1"], result.Gaps);
    }

    [Fact]
    public void Gap_detection_considers_both_already_persisted_and_incoming_events()
    {
        var result = IntegrityEventIngestion.Process([Event("3")], alreadyPersistedClientSeqs: ["0", "1"]);

        Assert.Equal(["2"], result.Gaps);
    }

    [Fact]
    public void Duplicate_events_within_the_same_batch_are_only_persisted_once()
    {
        var result = IntegrityEventIngestion.Process([Event("0"), Event("0")], []);

        Assert.Single(result.ToPersist);
    }
}
