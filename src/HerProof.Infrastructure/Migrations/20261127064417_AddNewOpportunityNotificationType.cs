using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewOpportunityNotificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications",
                sql: "\"notification_type\" IN ('CompetencyVerified', 'CompetencyNotDemonstrated', 'RecruiterInterest', 'NewOpportunity')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_notification_type_enum",
                table: "notifications",
                sql: "\"notification_type\" IN ('CompetencyVerified', 'CompetencyNotDemonstrated', 'RecruiterInterest')");
        }
    }
}
