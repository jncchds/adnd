using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class HighPriorityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActionsRemaining",
                table: "CombatParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BonusActionsRemaining",
                table: "CombatParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<JsonElement>(
                name: "FreeActions",
                table: "CombatParticipants",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovementsRemaining",
                table: "CombatParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReactionsRemaining",
                table: "CombatParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Background",
                table: "Characters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "BackgroundFeatures",
                table: "Characters",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "BackgroundProficiencies",
                table: "Characters",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "BackgroundSkills",
                table: "Characters",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpellAttackBonus",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpellSaveDC",
                table: "Characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "SpellSlots",
                table: "Characters",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonElement>(
                name: "SpellcastingAbility",
                table: "Characters",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionsRemaining",
                table: "CombatParticipants");

            migrationBuilder.DropColumn(
                name: "BonusActionsRemaining",
                table: "CombatParticipants");

            migrationBuilder.DropColumn(
                name: "FreeActions",
                table: "CombatParticipants");

            migrationBuilder.DropColumn(
                name: "MovementsRemaining",
                table: "CombatParticipants");

            migrationBuilder.DropColumn(
                name: "ReactionsRemaining",
                table: "CombatParticipants");

            migrationBuilder.DropColumn(
                name: "Background",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BackgroundFeatures",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BackgroundProficiencies",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BackgroundSkills",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellAttackBonus",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellSaveDC",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellSlots",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SpellcastingAbility",
                table: "Characters");
        }
    }
}
