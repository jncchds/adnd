using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmInteractionLogStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "LLMInteractionLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "LLMInteractionLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "LLMInteractionLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Pre-existing rows were only ever written after the call finished; mark them
            // Completed (2) rather than leaving them at the new-row default of Pending (0).
            migrationBuilder.Sql(
                "UPDATE \"LLMInteractionLogs\" SET \"Status\" = 2, \"CompletedAt\" = \"StartedAt\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "LLMInteractionLogs");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "LLMInteractionLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LLMInteractionLogs");
        }
    }
}
