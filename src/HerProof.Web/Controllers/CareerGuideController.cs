using HerProof.AI;
using HerProof.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerProof.Web.Controllers;

public record CareerGuideChatRequest(string ConversationSoFar, string Message);

/// <summary>The "not sure where to start?" chat widget's backend - a candidate's typed message is
/// untrusted input, wrapped as data for CareerGuideEngine, never as an instruction (CLAUDE.md rule 7).
/// Uses whatever IAIService Program.cs wired up (Groq if configured, else a safe fallback) - never a
/// new AI integration of its own.</summary>
[Authorize(Roles = nameof(UserRole.Candidate))]
[Route("career-guide")]
public class CareerGuideController(CareerGuideEngine engine) : Controller
{
    [HttpPost("chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat([FromBody] CareerGuideChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest();
        }

        var reply = await engine.ReplyAsync(request.ConversationSoFar ?? string.Empty, request.Message, cancellationToken);
        return Json(new { reply = reply.Reply, suggestedGoal = reply.SuggestedGoal });
    }
}
