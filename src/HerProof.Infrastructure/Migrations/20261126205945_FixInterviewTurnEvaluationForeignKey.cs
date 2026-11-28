using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerProof.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixInterviewTurnEvaluationForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_interview_turns_interview_evaluations_evaluation_id",
                table: "interview_turns");

            migrationBuilder.DropIndex(
                name: "ix_interview_turns_evaluation_id",
                table: "interview_turns");

            migrationBuilder.DropIndex(
                name: "ix_interview_evaluations_turn_id",
                table: "interview_evaluations");

            migrationBuilder.DropColumn(
                name: "evaluation_id",
                table: "interview_turns");

            migrationBuilder.CreateIndex(
                name: "ix_interview_evaluations_turn_id",
                table: "interview_evaluations",
                column: "turn_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_interview_evaluations_interview_turns_turn_id",
                table: "interview_evaluations",
                column: "turn_id",
                principalTable: "interview_turns",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_interview_evaluations_interview_turns_turn_id",
                table: "interview_evaluations");

            migrationBuilder.DropIndex(
                name: "ix_interview_evaluations_turn_id",
                table: "interview_evaluations");

            migrationBuilder.AddColumn<Guid>(
                name: "evaluation_id",
                table: "interview_turns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_interview_turns_evaluation_id",
                table: "interview_turns",
                column: "evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_interview_evaluations_turn_id",
                table: "interview_evaluations",
                column: "turn_id");

            migrationBuilder.AddForeignKey(
                name: "fk_interview_turns_interview_evaluations_evaluation_id",
                table: "interview_turns",
                column: "evaluation_id",
                principalTable: "interview_evaluations",
                principalColumn: "id");
        }
    }
}
