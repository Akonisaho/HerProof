using HerProof.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HerProof.Infrastructure.Persistence.Configurations;

/// <summary>Without this, EF Core's convention for the InterviewTurn.Evaluation reference guesses the
/// wrong direction: it adds a phantom "evaluation_id" FK column on interview_turns (the principal),
/// requiring the evaluation row to exist before the turn can be inserted - impossible when both are
/// new rows in the same SaveChanges, since InterviewEvaluation.TurnId already points the other way
/// (evaluation is the dependent). This makes TurnId the one real FK, so a turn can always be inserted
/// first (append-only, CLAUDE.md rule 8) and its evaluation inserted afterwards, same order the code
/// already builds them in.</summary>
public class InterviewTurnConfiguration : IEntityTypeConfiguration<InterviewTurn>
{
    public void Configure(EntityTypeBuilder<InterviewTurn> builder)
    {
        builder.HasOne(t => t.Evaluation)
            .WithOne()
            .HasForeignKey<InterviewEvaluation>(e => e.TurnId);
    }
}
