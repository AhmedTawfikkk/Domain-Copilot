using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewMemoEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewMemoCitations",
                columns: table => new
                {
                    ReviewMemoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitationOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewMemoCitations", x => new { x.ReviewMemoId, x.DocumentChunkId });
                    table.ForeignKey(
                        name: "FK_ReviewMemoCitations_DocumentChunks_DocumentChunkId",
                        column: x => x.DocumentChunkId,
                        principalTable: "DocumentChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewMemoCitations_ReviewMemos_ReviewMemoId",
                        column: x => x.ReviewMemoId,
                        principalTable: "ReviewMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewMemoRiskFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewMemoId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClauseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: false),
                    DocumentChunkId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewMemoRiskFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewMemoRiskFindings_ReviewMemos_ReviewMemoId",
                        column: x => x.ReviewMemoId,
                        principalTable: "ReviewMemos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewMemoCitations_DocumentChunkId",
                table: "ReviewMemoCitations",
                column: "DocumentChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewMemoCitations_ReviewMemoId_CitationOrder",
                table: "ReviewMemoCitations",
                columns: new[] { "ReviewMemoId", "CitationOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewMemoRiskFindings_ReviewMemoId_DisplayOrder",
                table: "ReviewMemoRiskFindings",
                columns: new[] { "ReviewMemoId", "DisplayOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewMemoCitations");

            migrationBuilder.DropTable(
                name: "ReviewMemoRiskFindings");
        }
    }
}
