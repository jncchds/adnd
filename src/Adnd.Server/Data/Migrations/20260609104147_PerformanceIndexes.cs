using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlotThreads_GameId",
                table: "PlotThreads");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SessionId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Combats_GameId",
                table: "Combats");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Entity = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlotThreads_GameId_Status",
                table: "PlotThreads",
                columns: new[] { "GameId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlotThreads_GameId_Title",
                table: "PlotThreads",
                columns: new[] { "GameId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SessionId_CreatedAt",
                table: "Messages",
                columns: new[] { "SessionId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_LLMInteractionLogs_StartedAt",
                table: "LLMInteractionLogs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_GameId_Status",
                table: "Combats",
                columns: new[] { "GameId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_PlotThreads_GameId_Status",
                table: "PlotThreads");

            migrationBuilder.DropIndex(
                name: "IX_PlotThreads_GameId_Title",
                table: "PlotThreads");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SessionId_CreatedAt",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_LLMInteractionLogs_StartedAt",
                table: "LLMInteractionLogs");

            migrationBuilder.DropIndex(
                name: "IX_Combats_GameId_Status",
                table: "Combats");

            migrationBuilder.CreateIndex(
                name: "IX_PlotThreads_GameId",
                table: "PlotThreads",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SessionId",
                table: "Messages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_GameId",
                table: "Combats",
                column: "GameId");
        }
    }
}
