using HerProof.Application.Audit;
using Xunit;

namespace HerProof.Application.Tests;

public class RetentionPolicyTests
{
    [Fact]
    public void A_fresh_transcript_is_not_past_retention()
    {
        var now = DateTime.UtcNow;

        Assert.False(RetentionPolicy.IsPastTranscriptRetention(now.AddDays(-1), now));
    }

    [Fact]
    public void A_transcript_right_at_the_window_edge_is_past_retention()
    {
        var now = DateTime.UtcNow;

        Assert.True(RetentionPolicy.IsPastTranscriptRetention(now - RetentionPolicy.TranscriptRetentionWindow, now));
    }

    [Fact]
    public void An_old_transcript_is_past_retention()
    {
        var now = DateTime.UtcNow;

        Assert.True(RetentionPolicy.IsPastTranscriptRetention(now.AddDays(-200), now));
    }
}
