using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadmapStepCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roadmap_step_completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal = table.Column<string>(type: "text", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roadmap_step_completions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_step_completions_assessment_session_id",
                table: "roadmap_step_completions",
                column: "assessment_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_step_completions_candidate_id",
                table: "roadmap_step_completions",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_step_completions_candidate_id_goal_competency_id",
                table: "roadmap_step_completions",
                columns: new[] { "candidate_id", "goal", "competency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_step_completions_competency_id",
                table: "roadmap_step_completions",
                column: "competency_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roadmap_step_completions");
        }
    }
}
