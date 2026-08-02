using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adnd.Server.Migrations
{
    /// <inheritdoc />
    public partial class ClearPrivateMessageEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Embeddings used to be generated for every message in the session, OOC chat and
            // whispers included. Nothing queries Message.Embedding yet, so those vectors have
            // never surfaced — but they are private content sitting in a column whose whole
            // purpose is to be similarity-searched, so clear them before anything reads it.
            // MessageVisibility.IsVisibleToNarration is now the write-side gate.
            migrationBuilder.Sql("""
                UPDATE "Messages"
                SET "Embedding" = NULL
                WHERE "IsOOC" = TRUE
                   OR "WhisperFromId" IS NOT NULL
                   OR "WhisperToId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deleted vectors cannot be restored, and re-generating them would mean re-leaking
            // the content this migration exists to remove.
        }
    }
}
