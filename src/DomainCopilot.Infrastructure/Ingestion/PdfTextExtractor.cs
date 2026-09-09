using DomainCopilot.Application.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DomainCopilot.Infrastructure.Ingestion
{
    public sealed class PdfTextExtractor : ITextExtractor
    {
        public bool CanExtract(string fileName) =>
            Path.GetExtension(fileName)
                .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
            Stream content,
            CancellationToken cancellationToken = default)
        {
            using var document = PdfDocument.Open(content);

            var pages = document.GetPages()
                .Select(page =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return new ExtractedPage(
                        page.Number,
                        ContentOrderTextExtractor.GetText(page));
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<ExtractedPage>>(pages);
        }
    }
}
