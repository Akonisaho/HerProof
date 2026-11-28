using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruiterSeenAndAssessorVerifiedRoadmap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roadmap_step_completions_assessment_session_id",
                table: "roadmap_step_completions");

            migrationBuilder.AlterColumn<Guid>(
                name: "assessment_session_id",
                table: "roadmap_step_completions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "verified_by_assessor",
                table: "roadmap_step_completions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "recruiter_seen_at_utc",
                table: "candidate_outreaches",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "verified_by_assessor",
                table: "roadmap_step_completions");

            migrationBuilder.DropColumn(
                name: "recruiter_seen_at_utc",
                table: "candidate_outreaches");

            migrationBuilder.AlterColumn<Guid>(
                name: "assessment_session_id",
                table: "roadmap_step_completions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_roadmap_step_completions_assessment_session_id",
                table: "roadmap_step_completions",
                column: "assessment_session_id");
        }
    }
}
