using DomainCopilot.Application.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class ClauseAwareChunker : IClauseChunker
    {
        private const int MaximumChunkCharacters = 2_000;

        private static readonly Regex ClauseHeadingPattern = new(
            @"^(?<section>\d+(?:\.\d+){0,4})[.)]?\s+(?<title>[A-Za-z][A-Za-z \-,/&]{2,120})$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public IReadOnlyList<ChunkDraft> Chunk(
            IReadOnlyList<ExtractedPage> pages)
        {
            var chunks = new List<ChunkDraft>();
            var chunkIndex = 0;

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
                        null,
                        page.PageNumber,
                        ref chunkIndex);

                    continue;
                }

                for (var index = 0; index < matches.Count; index++)
                {
                    var heading = matches[index];

                    var endIndex = index + 1 < matches.Count
                        ? matches[index + 1].Index
                        : page.Text.Length;

                    var sectionText = page.Text[
                        heading.Index..endIndex].Trim();

                    var clauseOrSection =
                        $"{heading.Groups["section"].Value} " +
                        heading.Groups["title"].Value;

                    AddChunkParts(
                        chunks,
                        sectionText,
                        clauseOrSection,
                        page.PageNumber,
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
            ref int chunkIndex)
        {
            var paragraphs = text.Split(
                "\n\n",
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

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
                    ref chunkIndex);
            }
        }

        private static void AddChunk(
            ICollection<ChunkDraft> chunks,
            string content,
            string? clauseOrSection,
            int pageNumber,
            ref int chunkIndex)
        {
            var finalContent = content.Trim();

            if (string.IsNullOrWhiteSpace(finalContent))
            {
                return;
            }

            chunks.Add(new ChunkDraft(
                finalContent,
                clauseOrSection,
                pageNumber,
                chunkIndex++,
                new Dictionary<string, string>
                {
                    ["source"] = "UserUpload",
                    ["page"] = pageNumber.ToString(),
                    ["section_clause"] = clauseOrSection ?? "unknown",
                    ["version"] = "1.0"
                }));
        }
    }

}