using HerProof.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HerProof.Infrastructure.Persistence.Configurations;

/// <summary>No uniqueness constraint - unlike the old "like" toggle, more than one message from the
/// same recruiter to the same candidate over time is legitimate (an interview invite today, a job
/// offer weeks later are both real, separate outreach), so every row is kept, append-only in spirit.</summary>
public class CandidateOutreachConfiguration : IEntityTypeConfiguration<CandidateOutreach>
{
    public void Configure(EntityTypeBuilder<CandidateOutreach> builder)
    {
        builder.HasIndex(x => new { x.RecruiterUserId, x.CandidateId });
    }
}
