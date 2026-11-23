using HerProof.Domain.Opportunities;
using HerProof.Domain.Passports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HerProof.Infrastructure.Persistence.Configurations;

/// <summary>docs/02 rule 5: unique constraints beyond user email (already unique via
/// ASP.NET Identity's normalized email index).</summary>
public class SkillsPassportConfiguration : IEntityTypeConfiguration<SkillsPassport>
{
    public void Configure(EntityTypeBuilder<SkillsPassport> builder)
    {
        builder.HasIndex(p => p.PassportNumber).IsUnique();
    }
}

public class PassportCredentialConfiguration : IEntityTypeConfiguration<PassportCredential>
{
    public void Configure(EntityTypeBuilder<PassportCredential> builder)
    {
        builder.HasIndex(c => c.CredentialIdentifier).IsUnique();
    }
}

public class OpportunityCompetencyConfiguration : IEntityTypeConfiguration<OpportunityCompetency>
{
    public void Configure(EntityTypeBuilder<OpportunityCompetency> builder)
    {
        builder.HasIndex(oc => new { oc.OpportunityId, oc.CompetencyId }).IsUnique();
    }
}
