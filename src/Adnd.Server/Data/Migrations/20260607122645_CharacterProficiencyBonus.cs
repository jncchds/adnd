using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CharacterProficiencyBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProficiencyBonus",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProficiencyBonus",
                table: "Characters");
        }
    }
}
