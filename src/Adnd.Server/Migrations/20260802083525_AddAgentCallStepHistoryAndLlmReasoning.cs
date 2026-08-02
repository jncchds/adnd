using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentCallStepHistoryAndLlmReasoning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reasoning",
                table: "LLMInteractionLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StepHistory",
                table: "AgentCalls",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reasoning",
                table: "LLMInteractionLogs");

            migrationBuilder.DropColumn(
                name: "StepHistory",
                table: "AgentCalls");
        }
    }
}
