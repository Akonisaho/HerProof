using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnershipInquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "partnership_inquiries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organisation_name = table.Column<string>(type: "text", nullable: false),
                    contact_name = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    inquiry_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partnership_inquiries", x => x.id);
                    table.CheckConstraint("ck_partnership_inquiry_inquiry_type_enum", "\"inquiry_type\" IN ('HiringPartner', 'BursaryProvider', 'Other')");
                    table.CheckConstraint("ck_partnership_inquiry_status_enum", "\"status\" IN ('New', 'Contacted', 'Closed')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partnership_inquiries");
        }
    }
}
