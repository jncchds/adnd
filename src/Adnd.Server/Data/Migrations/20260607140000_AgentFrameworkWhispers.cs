using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations;

/// <inheritdoc />
public partial class AgentFrameworkWhispers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new columns to Games table
        migrationBuilder.AddColumn<Guid>(
            name: "gamemasterid",
            table: "games",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "gamestate",
            table: "games",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "gameparameters",
            table: "games",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "plotseed",
            table: "games",
            type: "text",
            nullable: true);

        // Add GameMasterId foreign key
        migrationBuilder.CreateIndex(
            name: "IX_games_gamemasterid",
            table: "games",
            column: "gamemasterid");

        migrationBuilder.AddForeignKey(
            name: "FK_games_users_gamemasterid",
            table: "games",
            column: "gamemasterid",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        // Add new columns to Players table
        migrationBuilder.AddColumn<string[]>(
            name: "whispergroups",
            table: "players",
            type: "text[]",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "canwhisper",
            table: "players",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        // Add new columns to Messages table for whispers
        migrationBuilder.AddColumn<Guid>(
            name: "whisperfromid",
            table: "messages",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "whisperTarget",
            table: "messages",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_messages_whisperfromid",
            table: "messages",
            column: "whisperfromid");

        migrationBuilder.AddForeignKey(
            name: "FK_messages_players_whisperfromid",
            table: "messages",
            column: "whisperfromid",
            principalTable: "players",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);

        // Create AgentCalls table
        migrationBuilder.CreateTable(
            name: "agent_calls",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                gameid = table.Column<Guid>(type: "uuid", nullable: false),
                sessionid = table.Column<Guid>(type: "uuid", nullable: true),
                fromagent = table.Column<int>(type: "integer", nullable: false),
                toagent = table.Column<int>(type: "integer", nullable: false),
                action = table.Column<int>(type: "integer", nullable: false),
                input = table.Column<string>(type: "text", nullable: true),
                output = table.Column<string>(type: "text", nullable: true),
                outputmessage = table.Column<string>(type: "text", nullable: true),
                status = table.Column<int>(type: "integer", nullable: false),
                error = table.Column<string>(type: "text", nullable: true),
                createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                startedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completedat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                durationms = table.Column<int>(type: "integer", nullable: false),
                metadata = table.Column<string>(type: "jsonb", nullable: true),
                parentcallid = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_agent_calls", x => x.id);
                table.ForeignKey(
                    name: "FK_agent_calls_agent_calls_parentcallid",
                    column: x => x.parentcallid,
                    principalTable: "agent_calls",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_agent_calls_games_gameid",
                    column: x => x.gameid,
                    principalTable: "games",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_agent_calls_games_sessionid",
                    column: x => x.sessionid,
                    principalTable: "games",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        // Create Whispers table
        migrationBuilder.CreateTable(
            name: "whispers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                gameid = table.Column<Guid>(type: "uuid", nullable: false),
                sessionid = table.Column<Guid>(type: "uuid", nullable: false),
                fromplayerid = table.Column<Guid>(type: "uuid", nullable: false),
                targets = table.Column<string>(type: "text", nullable: false),
                targetplayerids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                content = table.Column<string>(type: "text", nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                createdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_whispers", x => x.id);
                table.ForeignKey(
                    name: "FK_whispers_games_gameid",
                    column: x => x.gameid,
                    principalTable: "games",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_whispers_games_sessionid",
                    column: x => x.sessionid,
                    principalTable: "games",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_whispers_players_fromplayerid",
                    column: x => x.fromplayerid,
                    principalTable: "players",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_whispers_gameid_createdat",
            table: "whispers",
            columns: new[] { "gameid", "createdat" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_whispers_sessionid",
            table: "whispers",
            column: "sessionid");

        migrationBuilder.CreateIndex(
            name: "IX_whispers_fromplayerid",
            table: "whispers",
            column: "fromplayerid");

        migrationBuilder.CreateIndex(
            name: "IX_agent_calls_gameid_status_createdat",
            table: "agent_calls",
            columns: new[] { "gameid", "status", "createdat" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "IX_agent_calls_sessionid",
            table: "agent_calls",
            column: "sessionid");

        migrationBuilder.CreateIndex(
            name: "IX_agent_calls_parentcallid",
            table: "agent_calls",
            column: "parentcallid");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop tables
        migrationBuilder.DropTable(name: "whispers");
        migrationBuilder.DropTable(name: "agent_calls");

        // Drop columns from Games
        migrationBuilder.DropIndex(name: "IX_games_gamemasterid", table: "games");
        migrationBuilder.DropForeignKey(name: "FK_games_users_gamemasterid", table: "games");
        migrationBuilder.DropColumn(name: "gamestate", table: "games");
        migrationBuilder.DropColumn(name: "gameparameters", table: "games");
        migrationBuilder.DropColumn(name: "plotseed", table: "games");
        migrationBuilder.DropColumn(name: "gamemasterid", table: "games");

        // Drop columns from Players
        migrationBuilder.DropColumn(name: "whispergroups", table: "players");
        migrationBuilder.DropColumn(name: "canwhisper", table: "players");

        // Drop columns from Messages
        migrationBuilder.DropIndex(name: "IX_messages_whisperfromid", table: "messages");
        migrationBuilder.DropForeignKey(name: "FK_messages_players_whisperfromid", table: "messages");
        migrationBuilder.DropColumn(name: "whisperfromid", table: "messages");
        migrationBuilder.DropColumn(name: "whisperTarget", table: "messages");
    }
}
