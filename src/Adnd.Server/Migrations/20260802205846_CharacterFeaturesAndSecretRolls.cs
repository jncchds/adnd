using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class CharacterFeaturesAndSecretRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSecret",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RerollCharacterId",
                table: "GMToolCalls",
                type: "uuid",
                nullable: true);

            // EF scaffolds jsonb NOT NULL DEFAULT '' for a new JsonElement column and '' is
            // not valid JSON, so the ALTER TABLE fails outright. An empty array is both valid
            // and the right starting value: every existing character has no features yet.
            migrationBuilder.AddColumn<string>(
                name: "Features",
                table: "Characters",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Race",
                table: "Characters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSecret",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "RerollCharacterId",
                table: "GMToolCalls");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Race",
                table: "Characters");
        }
    }
}
