using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            // Backfill ownership for documents that pre-date object-level
            // authorization. Every legacy document was uploaded by the demo
            // reviewer account, so it inherits that account as its owner.
            migrationBuilder.Sql("""
                UPDATE "Documents"
                SET "OwnerId" = (
                    SELECT "Id" FROM "AspNetUsers"
                    WHERE "Email" = 'eval.lawyer@test.local'
                    LIMIT 1)
                WHERE "OwnerId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Documents");
        }
    }
}
