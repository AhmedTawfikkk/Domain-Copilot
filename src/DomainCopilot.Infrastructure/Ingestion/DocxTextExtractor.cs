using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DomainCopilot.Application.Documents.Ingestion;


namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class DocxTextExtractor : ITextExtractor
    {
        public bool CanExtract(string fileName) =>
            Path.GetExtension(fileName)
                .Equals(".docx", StringComparison.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            Stream content,
            CancellationToken cancellationToken = default)
        {
            using var document = WordprocessingDocument.Open(content, false);

            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidOperationException(
                    "The DOCX document does not contain a body.");

            var text = string.Join(
                Environment.NewLine + Environment.NewLine,
                body.Descendants<Paragraph>()
                    .Select(paragraph => paragraph.InnerText)
                    .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)));

            IReadOnlyList<ExtractedPage> pages =
            [
                new ExtractedPage(1, text)
            ];

            return Task.FromResult(pages);
        }
    }
}
