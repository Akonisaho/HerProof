using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    related_competency_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notification_notification_type_enum", "\"notification_type\" IN ('CompetencyVerified', 'CompetencyNotDemonstrated', 'RecruiterInterest')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_candidate_id",
                table: "notifications",
                column: "candidate_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
