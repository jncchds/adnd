using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitGMCreatorRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_GameMasterId",
                table: "Games");

            migrationBuilder.RenameColumn(
                name: "GameMasterId",
                table: "Games",
                newName: "LLMPresetId");

            migrationBuilder.RenameIndex(
                name: "IX_Games_GameMasterId",
                table: "Games",
                newName: "IX_Games_LLMPresetId");

            migrationBuilder.AddColumn<int>(
                name: "GMStatus",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastGMAction",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastGMActionAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_LLMPresets_LLMPresetId",
                table: "Games",
                column: "LLMPresetId",
                principalTable: "LLMPresets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_LLMPresets_LLMPresetId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GMStatus",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "LastGMAction",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "LastGMActionAt",
                table: "Games");

            migrationBuilder.RenameColumn(
                name: "LLMPresetId",
                table: "Games",
                newName: "GameMasterId");

            migrationBuilder.RenameIndex(
                name: "IX_Games_LLMPresetId",
                table: "Games",
                newName: "IX_Games_GameMasterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_GameMasterId",
                table: "Games",
                column: "GameMasterId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
