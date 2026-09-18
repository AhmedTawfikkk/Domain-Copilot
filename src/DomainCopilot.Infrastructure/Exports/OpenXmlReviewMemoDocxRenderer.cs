using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DomainCopilot.Application.Documents.Review;
using System.Globalization;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace DomainCopilot.Infrastructure.Exports;

public sealed class OpenXmlReviewMemoDocxRenderer
    : IReviewMemoDocxRenderer
{
    private const string BorderColor = "D9D9D9";
    private const string HeaderFill = "1F4E78";
    private const string HeaderTextColor = "FFFFFF";

    public byte[] Render(ReviewMemoExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   true))
        {
            var mainPart = document.AddMainDocumentPart();

            mainPart.Document = new WordDocument(
                new Body());

            var body = mainPart.Document.Body!;

            AddTitle(body, "Contract Review Memo");
            AddMetadata(body, source);
            AddMemoContent(body, source.Content);
            AddRiskFindings(body, source);
            AddCitationRegister(body, source);

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static void AddTitle(
        Body body,
        string title)
    {
        body.Append(CreateParagraph(
            title,
            bold: true,
            fontSize: "32",
            spacingAfter: "240"));
    }

    private static void AddMetadata(
        Body body,
        ReviewMemoExportSource source)
    {
        body.Append(CreateParagraph(
            $"Memo ID: {source.MemoId}",
            fontSize: "20"));

        body.Append(CreateParagraph(
            $"Document ID: {source.DocumentId}",
            fontSize: "20"));

        body.Append(CreateParagraph(
            $"Approved by: {source.DecidedBy ?? "Unknown"}",
            fontSize: "20"));

        body.Append(CreateParagraph(
            $"Approved at: {FormatDate(source.DecidedAtUtc)}",
            fontSize: "20",
            spacingAfter: "240"));
    }

    private static void AddMemoContent(
        Body body,
        string markdown)
    {
        body.Append(CreateHeading(
            "Memo Content"));

        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                body.Append(CreateParagraph(
                    string.Empty,
                    spacingAfter: "80"));

                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                body.Append(CreateHeading(line[3..]));

                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                body.Append(CreateHeading(line[2..]));

                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal))
            {
                body.Append(CreateMarkdownParagraph(
                    $"• {line[2..]}",
                    leftIndent: "360"));

                continue;
            }

            body.Append(CreateMarkdownParagraph(line));
        }
    }

    private static void AddRiskFindings(
        Body body,
        ReviewMemoExportSource source)
    {
        body.Append(CreateHeading("Risk Findings"));

        if (source.RiskFindings.Count == 0)
        {
            body.Append(CreateParagraph(
                "No playbook deviations were identified.",
                spacingAfter: "160"));

            return;
        }

        var citationLabels = source.Citations
            .ToDictionary(
                citation => citation.DocumentChunkId,
                citation => $"C{citation.CitationOrder + 1}");

        var table = CreateTable(
            "Severity",
            "Finding",
            "Rationale",
            "Recommendation",
            "Source");

        foreach (var finding in source.RiskFindings)
        {
            var sourceLabel = finding.DocumentChunkId is null
                ? "Playbook gap"
                : citationLabels.TryGetValue(
                    finding.DocumentChunkId.Value,
                    out var label)
                    ? $"[{label}]"
                    : "Not cited";

            table.Append(CreateDataRow(
                finding.Severity,
                $"{finding.RuleId}: {finding.Title}",
                finding.Rationale,
                finding.Recommendation,
                sourceLabel));
        }

        body.Append(table);
    }

    private static void AddCitationRegister(
        Body body,
        ReviewMemoExportSource source)
    {
        body.Append(CreateHeading("Verified Source References"));

        var table = CreateTable(
            "Citation",
            "Document",
            "Section",
            "Page",
            "OCR Confidence");

        foreach (var citation in source.Citations)
        {
            table.Append(CreateDataRow(
                $"[C{citation.CitationOrder + 1}]",
                citation.FileName,
                citation.ClauseOrSection ?? "Unspecified section",
                citation.PageNumber?.ToString() ?? "Unknown",
                citation.ExtractionConfidence.ToString(
                    "P0",
                    CultureInfo.InvariantCulture) +
                (citation.LowConfidence ? " — low confidence" : string.Empty)));
        }

        body.Append(table);
    }

    private static Table CreateTable(
        params string[] headers)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth
                {
                    Type = TableWidthUnitValues.Pct,
                    Width = "5000"
                },
                new TableBorders(
                    new TopBorder
                    {
                        Val = BorderValues.Single,
                        Size = 4,
                        Color = BorderColor
                    },
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 4,
                        Color = BorderColor
                    },
                    new LeftBorder
                    {
                        Val = BorderValues.Single,
                        Size = 4,
                        Color = BorderColor
                    },
                    new RightBorder
                    {
                        Val = BorderValues.Single,
                        Size = 4,
                        Color = BorderColor
                    },
                    new InsideHorizontalBorder
                    {
                        Val = BorderValues.Single,
                        Size = 4,
                        Color = BorderColor
                    },
                    new InsideVerticalBorder
                    {
                        Val = BorderValues.Single,
                        Size = 4,
                        Color = BorderColor
                    }),
                new TableCellMarginDefault(
                    new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })));

        var headerRow = new TableRow();

        foreach (var header in headers)
        {
            headerRow.Append(CreateCell(
                header,
                bold: true,
                fill: HeaderFill,
                textColor: HeaderTextColor));
        }

        table.Append(headerRow);

        return table;
    }

    private static TableRow CreateDataRow(
        params string[] values)
    {
        var row = new TableRow();

        foreach (var value in values)
        {
            row.Append(CreateCell(value));
        }

        return row;
    }

    private static TableCell CreateCell(
        string value,
        bool bold = false,
        string? fill = null,
        string? textColor = null)
    {
        var cellProperties = new TableCellProperties(
            new TableCellVerticalAlignment
            {
                Val = TableVerticalAlignmentValues.Center
            });

        if (!string.IsNullOrWhiteSpace(fill))
        {
            cellProperties.Append(
                new Shading
                {
                    Fill = fill,
                    Val = ShadingPatternValues.Clear
                });
        }

        return new TableCell(
            cellProperties,
            CreateParagraph(
                value,
                bold,
                fontSize: "18",
                textColor: textColor,
                spacingAfter: "0"));
    }

    private static Paragraph CreateHeading(
        string value)
    {
        return CreateParagraph(
            value,
            bold: true,
            fontSize: "26",
            spacingBefore: "260",
            spacingAfter: "120");
    }

    private static Paragraph CreateMarkdownParagraph(
        string value,
        string? leftIndent = null,
        string? spacingAfter = "120")
    {
        var paragraphProperties = new ParagraphProperties();

        if (!string.IsNullOrWhiteSpace(leftIndent))
        {
            paragraphProperties.Indentation = new Indentation
            {
                Left = leftIndent
            };
        }

        paragraphProperties.SpacingBetweenLines = new SpacingBetweenLines
        {
            After = spacingAfter
        };

        var paragraph = new Paragraph(paragraphProperties);
        var segments = value.Split("**", StringSplitOptions.None);

        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length == 0)
            {
                continue;
            }

            var runProperties = new RunProperties(
                new FontSize { Val = "22" });

            if (index % 2 == 1)
            {
                runProperties.Bold = new Bold();
            }

            paragraph.Append(new Run(
                runProperties,
                new Text(segments[index])
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));
        }

        return paragraph;
    }

    private static Paragraph CreateParagraph(
        string value,
        bool bold = false,
        string fontSize = "22",
        string? textColor = null,
        string? leftIndent = null,
        string? spacingBefore = null,
        string? spacingAfter = "120")
    {
        var paragraphProperties = new ParagraphProperties();

        if (!string.IsNullOrWhiteSpace(leftIndent))
        {
            paragraphProperties.Indentation =
                new Indentation
                {
                    Left = leftIndent
                };
        }

        paragraphProperties.SpacingBetweenLines =
            new SpacingBetweenLines
            {
                Before = spacingBefore,
                After = spacingAfter
            };

        var runProperties = new RunProperties(
            new FontSize { Val = fontSize });

        if (bold)
        {
            runProperties.Bold = new Bold();
        }

        if (!string.IsNullOrWhiteSpace(textColor))
        {
            runProperties.Color =
                new Color
                {
                    Val = textColor
                };
        }

        return new Paragraph(
            paragraphProperties,
            new Run(
                runProperties,
                new Text(value)
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));
    }

    private static string FormatDate(DateTime? value)
    {
        return value is null
            ? "Unknown"
            : value.Value.ToUniversalTime()
                .ToString("yyyy-MM-dd HH:mm 'UTC'");
    }
}
