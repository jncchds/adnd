using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class EFRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanWhisper",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<string>>(
                name: "WhisperGroups",
                table: "Players",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WhisperFromId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhisperTarget",
                table: "Messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameMasterId",
                table: "Games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameParameters",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameState",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlotSeed",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromAgent = table.Column<int>(type: "integer", nullable: false),
                    ToAgent = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: true),
                    Output = table.Column<string>(type: "text", nullable: true),
                    OutputMessage = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    ParentCallId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentCalls_AgentCalls_ParentCallId",
                        column: x => x.ParentCallId,
                        principalTable: "AgentCalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentCalls_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AgentCalls_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Whispers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Targets = table.Column<string>(type: "text", nullable: false),
                    TargetPlayerIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Whispers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Whispers_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Whispers_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Whispers_Players_FromPlayerId",
                        column: x => x.FromPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_WhisperFromId",
                table: "Messages",
                column: "WhisperFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameMasterId",
                table: "Games",
                column: "GameMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCalls_GameId_Status_CreatedAt",
                table: "AgentCalls",
                columns: new[] { "GameId", "Status", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCalls_ParentCallId",
                table: "AgentCalls",
                column: "ParentCallId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentCalls_SessionId",
                table: "AgentCalls",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Whispers_FromPlayerId",
                table: "Whispers",
                column: "FromPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Whispers_GameId_CreatedAt",
                table: "Whispers",
                columns: new[] { "GameId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Whispers_SessionId",
                table: "Whispers",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_GameMasterId",
                table: "Games",
                column: "GameMasterId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Players_WhisperFromId",
                table: "Messages",
                column: "WhisperFromId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_GameMasterId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Players_WhisperFromId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "AgentCalls");

            migrationBuilder.DropTable(
                name: "Whispers");

            migrationBuilder.DropIndex(
                name: "IX_Messages_WhisperFromId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Games_GameMasterId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CanWhisper",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WhisperGroups",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WhisperFromId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "WhisperTarget",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "GameMasterId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameParameters",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "GameState",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "PlotSeed",
                table: "Games");
        }
    }
}
