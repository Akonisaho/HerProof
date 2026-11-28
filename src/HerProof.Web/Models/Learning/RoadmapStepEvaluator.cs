using System.Text.Json;

namespace HerProof.Web.Models.Learning;

/// <summary>Turns an AssessmentSubmission's already-real, automated TestResultsJson (produced by
/// HerProof.AssessmentWorker's sandbox run - never a human, never fabricated) into a pass/fail for one
/// roadmap step. A step passes only when the sandbox actually built the code and every test passed
/// (overallScore == 1, per AssessmentScoring.Score) - the same bar a real CI pipeline would use.</summary>
public static class RoadmapStepEvaluator
{
    public static bool Passed(string? testResultsJson)
    {
        if (string.IsNullOrWhiteSpace(testResultsJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(testResultsJson);
            return doc.RootElement.TryGetProperty("overallScore", out var scoreElement)
                && scoreElement.TryGetDecimal(out var score)
                && score >= 1m;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
