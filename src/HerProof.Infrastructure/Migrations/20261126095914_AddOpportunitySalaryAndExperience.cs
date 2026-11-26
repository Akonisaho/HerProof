using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunitySalaryAndExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "experience_years_required",
                table: "opportunities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "salary_max",
                table: "opportunities",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "salary_min",
                table: "opportunities",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "experience_years_required",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "salary_max",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "salary_min",
                table: "opportunities");
        }
    }
}
