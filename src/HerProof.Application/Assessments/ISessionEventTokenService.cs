namespace HerProof.Application.Assessments;

/// <summary>Scoped only to posting integrity events for one session — not a general access token.
/// STUB(HP-025): a pragmatic stand-in until full OAuth2 PKCE (ADR-08) exists; see docs/STUBS.md.</summary>
public interface ISessionEventTokenService
{
    string Issue(Guid sessionId, DateTime nowUtc);

    Guid? Validate(string token, DateTime nowUtc);
}
