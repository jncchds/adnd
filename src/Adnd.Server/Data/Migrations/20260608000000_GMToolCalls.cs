using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class GMToolCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GMToolCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    ToolCallId = table.Column<string>(type: "text", nullable: true),
                    Arguments = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true),
                    OutputMessage = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RequiresConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    ParentToolCallId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GMToolCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GMToolCalls_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GMToolCalls_GMToolCalls_ParentToolCallId",
                        column: x => x.ParentToolCallId,
                        principalTable: "GMToolCalls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GMToolCalls_Games_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GMToolCalls_Players_ConfirmedBy",
                        column: x => x.ConfirmedBy,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GMToolCalls_GameId",
                table: "GMToolCalls",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GMToolCalls_ParentToolCallId",
                table: "GMToolCalls",
                column: "ParentToolCallId");

            migrationBuilder.CreateIndex(
                name: "IX_GMToolCalls_SessionId",
                table: "GMToolCalls",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GMToolCalls_Status",
                table: "GMToolCalls",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GMToolCalls");
        }
    }
}
