using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class CombatZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Zones",
                table: "Combats",
                type: "jsonb",
                nullable: false,
                // EF scaffolds "" here, which is not valid JSON and fails the ALTER TABLE.
                // Existing combats simply have no zones.
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ZoneId",
                table: "CombatParticipants",
                type: "text",
                nullable: true);

            // Combats started before this were never shown to anyone, never had the party in
            // them and could not be ended by the narrator, so every one is still "Active".
            // Left alone, the oldest would count as the fight in progress and block
            // startCombat for good. 2 = CombatStatus.Finished.
            migrationBuilder.Sql("""UPDATE "Combats" SET "Status" = 2 WHERE "Status" <> 2;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Zones",
                table: "Combats");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "CombatParticipants");
        }
    }
}
