using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class FourChannelChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsOOC flag to messages table
            migrationBuilder.AddColumn<bool>(
                name: "isOOC",
                table: "Messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Add WhisperToId column for single-target whispers
            migrationBuilder.AddColumn<Guid>(
                name: "WhisperToId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_WhisperToId",
                table: "Messages",
                column: "WhisperToId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Players_WhisperToId",
                table: "Messages",
                column: "WhisperToId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Players_WhisperToId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_WhisperToId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "isOOC",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "WhisperToId",
                table: "Messages");
        }
    }
}
