using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryContributorStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contributor_status",
                table: "repositories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "NotChecked");

            migrationBuilder.AddCheckConstraint(
                name: "ck_repository_contributor_status_enum",
                table: "repositories",
                sql: "\"contributor_status\" IN ('NotChecked', 'Confirmed', 'NotConfirmed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_repository_contributor_status_enum",
                table: "repositories");

            migrationBuilder.DropColumn(
                name: "contributor_status",
                table: "repositories");
        }
    }
}
