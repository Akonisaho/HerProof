using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCandidateLikesWithOutreach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_likes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications");

            migrationBuilder.CreateTable(
                name: "candidate_outreaches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruiter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outreach_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_outreaches", x => x.id);
                    table.CheckConstraint("ck_candidate_outreach_outreach_type_enum", "\"outreach_type\" IN ('CultureFitInterview', 'JobOffer')");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications",
                sql: "\"notification_type\" IN ('CompetencyVerified', 'CompetencyNotDemonstrated', 'RecruiterInterest', 'NewOpportunity', 'CultureFitInterviewInvite', 'JobOffer')");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_outreaches_candidate_id",
                table: "candidate_outreaches",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_outreaches_recruiter_user_id",
                table: "candidate_outreaches",
                column: "recruiter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_outreaches_recruiter_user_id_candidate_id",
                table: "candidate_outreaches",
                columns: new[] { "recruiter_user_id", "candidate_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_outreaches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications");

            migrationBuilder.CreateTable(
                name: "candidate_likes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recruiter_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_likes", x => x.id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications",
                sql: "\"notification_type\" IN ('CompetencyVerified', 'CompetencyNotDemonstrated', 'RecruiterInterest', 'NewOpportunity')");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_likes_candidate_id",
                table: "candidate_likes",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_likes_recruiter_user_id",
                table: "candidate_likes",
                column: "recruiter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_likes_recruiter_user_id_candidate_id",
                table: "candidate_likes",
                columns: new[] { "recruiter_user_id", "candidate_id" },
                unique: true);
        }
    }
}
