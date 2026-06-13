using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentSessionIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid?>(
                name: "CurrentSessionId",
                table: "Games",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_CurrentSessionId",
                table: "Games",
                column: "CurrentSessionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_GameSessions_CurrentSessionId",
                table: "Games",
                column: "CurrentSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_GameSessions_CurrentSessionId",
                table: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Games_CurrentSessionId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CurrentSessionId",
                table: "Games");
        }
    }
}
