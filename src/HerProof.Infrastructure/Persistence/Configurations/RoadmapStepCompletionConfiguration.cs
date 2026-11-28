using HerProof.Domain.Competencies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HerProof.Infrastructure.Persistence.Configurations;

/// <summary>A candidate completes a given goal's given competency step only once.</summary>
public class RoadmapStepCompletionConfiguration : IEntityTypeConfiguration<RoadmapStepCompletion>
{
    public void Configure(EntityTypeBuilder<RoadmapStepCompletion> builder)
    {
        builder.HasIndex(x => new { x.CandidateId, x.Goal, x.CompetencyId }).IsUnique();
    }
}
