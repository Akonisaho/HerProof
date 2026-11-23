using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_skills_passports_passport_number",
                table: "skills_passports",
                column: "passport_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_passport_credentials_credential_identifier",
                table: "passport_credentials",
                column: "credential_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_competencies_opportunity_id_competency_id",
                table: "opportunity_competencies",
                columns: new[] { "opportunity_id", "competency_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_skills_passports_passport_number",
                table: "skills_passports");

            migrationBuilder.DropIndex(
                name: "ix_passport_credentials_credential_identifier",
                table: "passport_credentials");

            migrationBuilder.DropIndex(
                name: "ix_opportunity_competencies_opportunity_id_competency_id",
                table: "opportunity_competencies");
        }
    }
}
