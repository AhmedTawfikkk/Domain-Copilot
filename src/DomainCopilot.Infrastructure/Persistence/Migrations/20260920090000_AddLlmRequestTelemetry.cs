using System;
using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DomainCopilot.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomainCopilotDbContext))]
[Migration("20260920090000_AddLlmRequestTelemetry")]
public partial class AddLlmRequestTelemetry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LlmRequestTelemetry",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CorrelationId = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true),
                Provider = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false),
                Model = table.Column<string>(
                    type: "character varying(150)",
                    maxLength: 150,
                    nullable: false),
                Operation = table.Column<string>(
                    type: "character varying(50)",
                    maxLength: 50,
                    nullable: false),
                InputTokens = table.Column<int>(type: "integer", nullable: true),
                OutputTokens = table.Column<int>(type: "integer", nullable: true),
                TotalTokens = table.Column<int>(type: "integer", nullable: true),
                EstimatedCostUsd = table.Column<decimal>(
                    type: "numeric(18,8)",
                    precision: 18,
                    scale: 8,
                    nullable: true),
                Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                WasCancelled = table.Column<bool>(type: "boolean", nullable: false),
                FailureReason = table.Column<string>(
                    type: "character varying(1000)",
                    maxLength: 1000,
                    nullable: true),
                DurationMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "timestamp with time zone",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LlmRequestTelemetry", entry => entry.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LlmRequestTelemetry_CorrelationId",
            table: "LlmRequestTelemetry",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_LlmRequestTelemetry_CreatedAtUtc",
            table: "LlmRequestTelemetry",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_LlmRequestTelemetry_Provider_Operation_CreatedAtUtc",
            table: "LlmRequestTelemetry",
            columns: new[] { "Provider", "Operation", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LlmRequestTelemetry");
    }
}
