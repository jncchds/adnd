using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlotWeaverDynamicGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to PlotThreads table
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "PlotThreads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "Momentum",
                table: "PlotThreads",
                type: "real",
                nullable: false,
                defaultValue: 0.0f);

            migrationBuilder.AddColumn<float>(
                name: "RelevanceScore",
                table: "PlotThreads",
                type: "real",
                nullable: false,
                defaultValue: 0.0f);

            migrationBuilder.AddColumn<bool>(
                name: "IsDynamic",
                table: "PlotThreads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MilestoneEvents",
                table: "PlotThreads",
                type: "jsonb",
                nullable: true);

            // Create PlotReviews table
            migrationBuilder.CreateTable(
                name: "PlotReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Trigger = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Updates = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlotReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlotReviews_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlotReviews_GameId_ReviewedAt",
                table: "PlotReviews",
                columns: new[] { "GameId", "ReviewedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlotReviews");

            migrationBuilder.DropColumn(name: "Category", table: "PlotThreads");
            migrationBuilder.DropColumn(name: "Momentum", table: "PlotThreads");
            migrationBuilder.DropColumn(name: "RelevanceScore", table: "PlotThreads");
            migrationBuilder.DropColumn(name: "IsDynamic", table: "PlotThreads");
            migrationBuilder.DropColumn(name: "MilestoneEvents", table: "PlotThreads");
        }
    }
}
