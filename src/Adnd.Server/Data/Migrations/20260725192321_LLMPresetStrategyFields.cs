using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class LLMPresetStrategyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToolArgs",
                table: "ToolCallCoordinators");

            migrationBuilder.DropColumn(
                name: "ToolName",
                table: "ToolCallCoordinators");

            migrationBuilder.AddColumn<string>(
                name: "ReasoningEffort",
                table: "LLMPresets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeoutMs",
                table: "LLMPresets",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "ExpirationTime",
                table: "GMToolCalls",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0),
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasoningEffort",
                table: "LLMPresets");

            migrationBuilder.DropColumn(
                name: "TimeoutMs",
                table: "LLMPresets");

            migrationBuilder.AddColumn<string>(
                name: "ToolArgs",
                table: "ToolCallCoordinators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToolName",
                table: "ToolCallCoordinators",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "ExpirationTime",
                table: "GMToolCalls",
                type: "interval",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");
        }
    }
}
