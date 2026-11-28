using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateLikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_likes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recruiter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_likes", x => x.id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_likes");
        }
    }
}
