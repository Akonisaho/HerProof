using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "learning_progresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learning_resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_progresses", x => x.id);
                    table.CheckConstraint("ck_learning_progress_status_enum", "\"status\" IN ('NotStarted', 'InProgress', 'Completed')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_learning_progresses_candidate_id",
                table: "learning_progresses",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_progresses_learning_resource_id",
                table: "learning_progresses",
                column: "learning_resource_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_progresses");
        }
    }
}
