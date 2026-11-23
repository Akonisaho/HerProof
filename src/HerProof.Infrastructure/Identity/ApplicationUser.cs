using Microsoft.AspNetCore.Identity;

namespace HerProof.Infrastructure.Identity;

/// <summary>Full auth behaviour (register, confirm, lockout, roles/policies) lands in P03.
/// This is the EF Core identity shape only, needed now so P02's foreign keys resolve.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
