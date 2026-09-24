using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeDocumentFileHashUniquePerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_FileHash",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileHash_OwnerId",
                table: "Documents",
                columns: new[] { "FileHash", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_FileHash_OwnerId",
                table: "Documents");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FileHash",
                table: "Documents",
                column: "FileHash",
                unique: true);
        }
    }
}
