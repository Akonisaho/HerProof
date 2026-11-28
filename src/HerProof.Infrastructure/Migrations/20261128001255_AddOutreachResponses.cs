using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutreachResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "related_outreach_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "responded_at_utc",
                table: "candidate_outreaches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response_message",
                table: "candidate_outreaches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "response_status",
                table: "candidate_outreaches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidate_outreach_response_status_enum",
                table: "candidate_outreaches",
                sql: "\"response_status\" IN ('Pending', 'Accepted', 'Declined')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_candidate_outreach_response_status_enum",
                table: "candidate_outreaches");

            migrationBuilder.DropColumn(
                name: "related_outreach_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "responded_at_utc",
                table: "candidate_outreaches");

            migrationBuilder.DropColumn(
                name: "response_message",
                table: "candidate_outreaches");

            migrationBuilder.DropColumn(
                name: "response_status",
                table: "candidate_outreaches");
        }
    }
}
