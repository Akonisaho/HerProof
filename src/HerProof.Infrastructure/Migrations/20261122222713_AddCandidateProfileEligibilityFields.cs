using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfileEligibilityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "candidate_profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "field_of_study",
                table: "candidate_profiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "field_of_study",
                table: "candidate_profiles");
        }
    }
}
