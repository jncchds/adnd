using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations;

/// <inheritdoc />
public partial class LLMPresetsAndInteractionLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create LLMPresets table
        migrationBuilder.CreateTable(
            name: "llm_presets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                provider_type = table.Column<string>(type: "text", nullable: false),
                base_model = table.Column<string>(type: "text", nullable: false),
                endpoint_url = table.Column<string>(type: "text", nullable: true),
                api_key = table.Column<string>(type: "text", nullable: true),
                temperature = table.Column<float>(type: "real", nullable: false),
                max_tokens = table.Column<int>(type: "integer", nullable: false),
                top_p = table.Column<float>(type: "real", nullable: false),
                frequency_penalty = table.Column<float>(type: "real", nullable: true),
                presence_penalty = table.Column<float>(type: "real", nullable: true),
                stream = table.Column<bool>(type: "boolean", nullable: false),
                embedding_model = table.Column<string>(type: "text", nullable: true),
                embedding_endpoint_url = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                extra_params = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_llm_presets", x => x.id);
                table.ForeignKey(
                    name: "FK_llm_presets_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_llm_presets_user_id_name",
            table: "llm_presets",
            columns: new[] { "user_id", "name" },
            unique: true);

        // Create LLMInteractionLogs table
        migrationBuilder.CreateTable(
            name: "llm_interaction_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                preset_id = table.Column<Guid>(type: "uuid", nullable: true),
                provider_type = table.Column<string>(type: "text", nullable: false),
                model = table.Column<string>(type: "text", nullable: false),
                prompt_tokens = table.Column<int>(type: "integer", nullable: true),
                completion_tokens = table.Column<int>(type: "integer", nullable: true),
                total_tokens = table.Column<int>(type: "integer", nullable: true),
                duration_ms = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                success = table.Column<bool>(type: "boolean", nullable: false),
                error = table.Column<string>(type: "text", nullable: true),
                system_prompt = table.Column<string>(type: "text", nullable: true),
                user_prompt = table.Column<string>(type: "text", nullable: true),
                response = table.Column<string>(type: "text", nullable: true),
                request_json = table.Column<string>(type: "text", nullable: true),
                response_json = table.Column<string>(type: "text", nullable: true),
                origin = table.Column<string>(type: "text", nullable: false),
                origin_game_id = table.Column<Guid>(type: "uuid", nullable: true),
                origin_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                origin_agent = table.Column<string>(type: "text", nullable: true),
                origin_action = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_llm_interaction_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_llm_interaction_logs_llm_presets_preset_id",
                    column: x => x.preset_id,
                    principalTable: "llm_presets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_llm_interaction_logs_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_llm_interaction_logs_user_id_origin_game_id_started_at",
            table: "llm_interaction_logs",
            columns: new[] { "user_id", "origin_game_id", "started_at" },
            descending: new[] { false, false, true });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "llm_interaction_logs");
        migrationBuilder.DropTable(name: "llm_presets");
    }
}
