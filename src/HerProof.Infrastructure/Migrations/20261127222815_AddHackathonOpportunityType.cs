using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHackathonOpportunityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunity_opportunity_type_enum",
                table: "opportunities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunity_opportunity_type_enum",
                table: "opportunities",
                sql: "\"opportunity_type\" IN ('Job', 'Internship', 'Learnership', 'Bursary', 'Hackathon')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunity_opportunity_type_enum",
                table: "opportunities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunity_opportunity_type_enum",
                table: "opportunities",
                sql: "\"opportunity_type\" IN ('Job', 'Internship', 'Learnership', 'Bursary')");
        }
    }
}
