using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "assessments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "CSharp");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assessment_language_enum",
                table: "assessments",
                sql: "\"language\" IN ('CSharp', 'Java')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_assessment_language_enum",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "language",
                table: "assessments");
        }
    }
}
