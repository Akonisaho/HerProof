namespace HerProof.Application.Opportunities;

/// <summary>An application may only ever attach credentials the candidate actually owns (docs/05 P16:
/// "bursary application attaches selected passport credentials") — this is the one place that
/// filter is enforced, so a forged form field can never attach someone else's credential.</summary>
public static class AppliedCredentialSelector
{
    public static IReadOnlyList<string> Select(IReadOnlyCollection<string> ownedCredentialIdentifiers, IReadOnlyCollection<string> requestedIdentifiers)
    {
        var owned = ownedCredentialIdentifiers.ToHashSet();
        return requestedIdentifiers.Where(owned.Contains).Distinct().ToList();
    }
}
