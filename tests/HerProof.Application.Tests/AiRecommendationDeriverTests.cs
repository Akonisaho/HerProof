using HerProof.Application.Verification;
using HerProof.Domain.Interviews;
using Xunit;

namespace HerProof.Application.Tests;

public class AiRecommendationDeriverTests
{
    [Fact]
    public void No_evidence_at_all_suggests_insufficient_evidence_never_a_verdict()
    {
        var result = AiRecommendationDeriver.Derive([], hasRepositoryFindings: false);

        Assert.Equal(AiSuggestion.InsufficientEvidence, result);
    }

    [Fact]
    public void Any_not_demonstrated_verdict_suggests_not_demonstrated_overall()
    {
        var result = AiRecommendationDeriver.Derive([Verdict.Understood, Verdict.NotDemonstrated], hasRepositoryFindings: true);

        Assert.Equal(AiSuggestion.NotDemonstrated, result);
    }

    [Fact]
    public void All_understood_suggests_verified()
    {
        var result = AiRecommendationDeriver.Derive([Verdict.Understood, Verdict.Understood], hasRepositoryFindings: true);

        Assert.Equal(AiSuggestion.Verified, result);
    }

    [Fact]
    public void Mixed_partial_and_understood_suggests_developing()
    {
        var result = AiRecommendationDeriver.Derive([Verdict.Understood, Verdict.Partial], hasRepositoryFindings: true);

        Assert.Equal(AiSuggestion.Developing, result);
    }

    [Fact]
    public void Repository_findings_alone_with_no_interview_suggests_developing_not_verified()
    {
        // Static/LLM repo evidence alone is never enough to suggest Verified — that requires the
        // interview's understanding check too (docs/00 differentiator #6: two independent proofs).
        var result = AiRecommendationDeriver.Derive([], hasRepositoryFindings: true);

        Assert.Equal(AiSuggestion.Developing, result);
    }
}
