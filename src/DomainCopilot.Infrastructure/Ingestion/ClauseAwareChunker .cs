using DomainCopilot.Application.Documents.Ingestion;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class ClauseAwareChunker : IClauseChunker
    {
        private const int MaximumChunkCharacters = 2_000;
        private const int MinimumChunkCharacters = 15;

        private static readonly Regex ClauseHeadingPattern = new(
     @"^[ \t]*(?<clause>(?:ARTICLE|SECTION)[ \t]+\d+(?:\.\d+)*|\d+\.\d+|\d+\.)[ \t]+(?<title>[A-Z0-9][A-Za-z0-9 \t\-,/&]{2,60})(?=\r?\n|\.|[ \t]*$)",
     RegexOptions.Compiled | RegexOptions.Multiline);

        public IReadOnlyList<ChunkDraft> Chunk(
            IReadOnlyList<ExtractedPage> pages)
        {
            var chunks = new List<ChunkDraft>();
            var chunkIndex = 0;
            string? currentClause = null;

            foreach (var page in pages)
            {
                if (string.IsNullOrWhiteSpace(page.Text))
                {
                    continue;
                }

                var matches = ClauseHeadingPattern.Matches(page.Text);

                if (matches.Count == 0)
                {
                    AddChunkParts(
                        chunks,
                        page.Text,
                        currentClause,
                        page.PageNumber,
                        page.ExtractionConfidence,
                        ref chunkIndex);

                    continue;
                }

                if (matches[0].Index > 0)
                {
                    var leadingText = page.Text[..matches[0].Index].Trim();

                    if (leadingText.Length >= MinimumChunkCharacters)
                    {
                        AddChunkParts(
                            chunks,
                            leadingText,
                            currentClause,
                            page.PageNumber,
                            page.ExtractionConfidence,
                            ref chunkIndex);
                    }
                }

                for (var index = 0; index < matches.Count; index++)
                {
                    var heading = matches[index];
                    var endIndex = index + 1 < matches.Count
                        ? matches[index + 1].Index
                        : page.Text.Length;

                    var sectionText = page.Text[heading.Index..endIndex].Trim();

                    var clauseNumber = heading.Groups["clause"].Value.Trim();
                    var titleText = heading.Groups["title"].Value.Trim();
                    currentClause = $"{clauseNumber} {titleText}";

                    if (sectionText.Length < MinimumChunkCharacters &&
                        index + 1 < matches.Count)
                    {
                        continue;
                    }

                    AddChunkParts(
                        chunks,
                        sectionText,
                        currentClause,
                        page.PageNumber,
                        page.ExtractionConfidence,
                        ref chunkIndex);
                }
            }

            return chunks;
        }

        private static void AddChunkParts(
            ICollection<ChunkDraft> chunks,
            string text,
            string? clauseOrSection,
            int pageNumber,
            double extractionConfidence,
            ref int chunkIndex)
        {
            var paragraphs = Regex.Split(text, @"(?:\r?\n){2,}")
                .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
                .Select(paragraph => paragraph.Trim())
                .ToArray();

            if (paragraphs.Length == 0)
            {
                return;
            }

            var buffer = new StringBuilder();

            foreach (var paragraph in paragraphs)
            {
                if (buffer.Length > 0 &&
                    buffer.Length + paragraph.Length + 2 >
                    MaximumChunkCharacters)
                {
                    AddChunk(
                        chunks,
                        buffer.ToString(),
                        clauseOrSection,
                        pageNumber,
                        extractionConfidence,
                        ref chunkIndex);

                    buffer.Clear();
                }

                if (buffer.Length > 0)
                {
                    buffer.AppendLine().AppendLine();
                }

                buffer.Append(paragraph);
            }

            if (buffer.Length > 0)
            {
                AddChunk(
                    chunks,
                    buffer.ToString(),
                    clauseOrSection,
                    pageNumber,
                    extractionConfidence,
                    ref chunkIndex);
            }
        }

        private static void AddChunk(
            ICollection<ChunkDraft> chunks,
            string content,
            string? clauseOrSection,
            int pageNumber,
            double extractionConfidence,
            ref int chunkIndex)
        {
            var finalContent = content.Trim();

            if (finalContent.Length < MinimumChunkCharacters)
            {
                return;
            }

            var normalizedConfidence = Math.Clamp(
                extractionConfidence,
                0.0,
                1.0);

            chunks.Add(new ChunkDraft(
                finalContent,
                clauseOrSection,
                pageNumber,
                chunkIndex++,
                new Dictionary<string, string>
                {
                    ["source"] = "UserUpload",
                    ["page"] = pageNumber.ToString(
                        CultureInfo.InvariantCulture),
                    ["section_clause"] = clauseOrSection ?? "unknown",
                    ["version"] = "1.0",
                    ["extraction_confidence"] = normalizedConfidence.ToString(
                        "F2",
                        CultureInfo.InvariantCulture)
                },
                normalizedConfidence));
        }
    }
}