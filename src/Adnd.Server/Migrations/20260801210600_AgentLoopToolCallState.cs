using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgentLoopToolCallState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ToolCalls",
                table: "ToolCallCoordinators",
                type: "jsonb",
                nullable: false,
                // The JsonElement converter maps Undefined to the literal "null"; the
                // scaffolded "" default is not valid jsonb and would fail the ALTER TABLE.
                defaultValue: "null");

            migrationBuilder.AddColumn<Guid>(
                name: "AgentCallId",
                table: "GMToolCalls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresConfirmation",
                table: "GMToolCalls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetPlayerId",
                table: "GMToolCalls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ToolIndex",
                table: "GMToolCalls",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToolCalls",
                table: "ToolCallCoordinators");

            migrationBuilder.DropColumn(
                name: "AgentCallId",
                table: "GMToolCalls");

            migrationBuilder.DropColumn(
                name: "RequiresConfirmation",
                table: "GMToolCalls");

            migrationBuilder.DropColumn(
                name: "TargetPlayerId",
                table: "GMToolCalls");

            migrationBuilder.DropColumn(
                name: "ToolIndex",
                table: "GMToolCalls");
        }
    }
}
