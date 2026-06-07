using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCombatEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure pgvector extension is available (needed for vector columns)
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.CreateTable(
                name: "Combats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentRound = table.Column<int>(type: "integer", nullable: false),
                    CurrentTurnIndex = table.Column<int>(type: "integer", nullable: false),
                    InitiativeCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<JsonElement>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Combats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Combats_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Combats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LLMPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    BaseModel = table.Column<string>(type: "text", nullable: false),
                    EndpointUrl = table.Column<string>(type: "text", nullable: true),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    Temperature = table.Column<float>(type: "real", nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    TopP = table.Column<float>(type: "real", nullable: false),
                    FrequencyPenalty = table.Column<float>(type: "real", nullable: true),
                    PresencePenalty = table.Column<float>(type: "real", nullable: true),
                    Stream = table.Column<bool>(type: "boolean", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true),
                    EmbeddingEndpointUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraParams = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LLMPresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LLMPresets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CombatEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CombatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    TurnIndex = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ActorName = table.Column<string>(type: "text", nullable: false),
                    TargetName = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatEvents_Combats_CombatId",
                        column: x => x.CombatId,
                        principalTable: "Combats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CombatParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CombatId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantType = table.Column<string>(type: "text", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    NpcId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Initiative = table.Column<int>(type: "integer", nullable: false),
                    InitiativeCount = table.Column<int>(type: "integer", nullable: false),
                    CurrentHP = table.Column<int>(type: "integer", nullable: false),
                    MaxHP = table.Column<int>(type: "integer", nullable: false),
                    AC = table.Column<int>(type: "integer", nullable: false),
                    InitiativeBonus = table.Column<int>(type: "integer", nullable: true),
                    Conditions = table.Column<string>(type: "jsonb", nullable: false),
                    TemporaryHP = table.Column<string>(type: "jsonb", nullable: true),
                    SavingThrows = table.Column<string>(type: "jsonb", nullable: true),
                    DeathSaveState = table.Column<string>(type: "jsonb", nullable: true),
                    Notes = table.Column<JsonElement>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatParticipants_Combats_CombatId",
                        column: x => x.CombatId,
                        principalTable: "Combats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CombatParticipants_NPCs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "NPCs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CombatParticipants_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LLMInteractionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PresetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderType = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: true),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    UserPrompt = table.Column<string>(type: "text", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true),
                    RequestJson = table.Column<string>(type: "text", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: true),
                    Origin = table.Column<string>(type: "text", nullable: false),
                    OriginGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginAgent = table.Column<string>(type: "text", nullable: true),
                    OriginAction = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LLMInteractionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LLMInteractionLogs_LLMPresets_PresetId",
                        column: x => x.PresetId,
                        principalTable: "LLMPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LLMInteractionLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CombatEvents_CombatId",
                table: "CombatEvents",
                column: "CombatId");

            migrationBuilder.CreateIndex(
                name: "IX_CombatParticipants_CombatId",
                table: "CombatParticipants",
                column: "CombatId");

            migrationBuilder.CreateIndex(
                name: "IX_CombatParticipants_NpcId",
                table: "CombatParticipants",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_CombatParticipants_PlayerId",
                table: "CombatParticipants",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_GameId",
                table: "Combats",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_SessionId",
                table: "Combats",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LLMInteractionLogs_PresetId",
                table: "LLMInteractionLogs",
                column: "PresetId");

            migrationBuilder.CreateIndex(
                name: "IX_LLMInteractionLogs_UserId_OriginGameId_StartedAt",
                table: "LLMInteractionLogs",
                columns: new[] { "UserId", "OriginGameId", "StartedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_LLMPresets_UserId_Name",
                table: "LLMPresets",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CombatEvents");

            migrationBuilder.DropTable(
                name: "CombatParticipants");

            migrationBuilder.DropTable(
                name: "LLMInteractionLogs");

            migrationBuilder.DropTable(
                name: "Combats");

            migrationBuilder.DropTable(
                name: "LLMPresets");
        }
    }
}
