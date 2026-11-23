using System.Security.Claims;
using System.Text.Json;
using HerProof.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace HerProof.Web.Authorization;

/// <summary>Wraps the default authorization result handler to audit every permission failure
/// (CLAUDE.md P03), without changing the 401/403 response the user sees.</summary>
public class AuditingAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _inner = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Succeeded)
        {
            var auditLogger = context.RequestServices.GetService(typeof(IAuditLogger)) as IAuditLogger;
            if (auditLogger is not null)
            {
                var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                Guid? userId = Guid.TryParse(userIdValue, out var id) ? id : null;
                var detail = JsonSerializer.Serialize(new { context.Request.Path, context.Request.Method });

                await auditLogger.LogAsync(userId, "PermissionDenied", "HttpRequest", Guid.Empty, detail, context.RequestAborted);
            }
        }

        await _inner.HandleAsync(next, context, policy, authorizeResult);
    }
}
