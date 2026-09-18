using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChunkExtractionConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ExtractionConfidence",
                table: "DocumentChunks",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentChunks_ExtractionConfidence",
                table: "DocumentChunks",
                sql: "\"ExtractionConfidence\" >= 0.0 AND \"ExtractionConfidence\" <= 1.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentChunks_ExtractionConfidence",
                table: "DocumentChunks");

            migrationBuilder.DropColumn(
                name: "ExtractionConfidence",
                table: "DocumentChunks");
        }
    }
}
