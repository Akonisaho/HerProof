using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidatePreferredCompetencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_preferred_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_preferred_competencies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_preferred_competencies_candidate_id",
                table: "candidate_preferred_competencies",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidate_preferred_competencies_competency_id",
                table: "candidate_preferred_competencies",
                column: "competency_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_preferred_competencies");
        }
    }
}
