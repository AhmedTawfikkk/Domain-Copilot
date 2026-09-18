using DomainCopilot.Application.Documents.Ingestion;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DomainCopilot.Infrastructure.Ingestion;

public sealed class PdfTextExtractor : ITextExtractor
{
    private readonly IPdfOcrService _pdfOcrService;
    private readonly PdfOcrOptions _ocrOptions;

    public PdfTextExtractor(
        IPdfOcrService pdfOcrService,
        PdfOcrOptions ocrOptions)
    {
        _pdfOcrService = pdfOcrService;
        _ocrOptions = ocrOptions;
    }

    public bool CanExtract(string fileName) =>
        Path.GetExtension(fileName)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanSeek)
        {
            throw new InvalidOperationException(
                "PDF extraction requires a seekable stream.");
        }

        content.Position = 0;

        IReadOnlyList<ExtractedPage> directPages;

        using (var document = PdfDocument.Open(content))
        {
            directPages = document.GetPages()
                .Select(page =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return new ExtractedPage(
                        page.Number,
                        ContentOrderTextExtractor.GetText(page),
                        1.0);
                })
                .ToList();
        }

        var needsOcr = directPages.Any(page =>
            !HasSufficientDirectText(page.Text));

        if (!needsOcr)
        {
            return directPages;
        }

        content.Position = 0;

        var ocrPages = await _pdfOcrService.ExtractAsync(
            content,
            cancellationToken);

        var ocrPagesByNumber = ocrPages.ToDictionary(
            page => page.PageNumber);

        return directPages
            .Select(page =>
            {
                if (HasSufficientDirectText(page.Text))
                {
                    return page;
                }

                if (!ocrPagesByNumber.TryGetValue(
                        page.PageNumber,
                        out var ocrPage))
                {
                    throw new InvalidOperationException(
                        $"OCR did not return page {page.PageNumber}.");
                }

                return ocrPage;
            })
            .ToList();
    }

    private bool HasSufficientDirectText(string value)
    {
        var meaningfulCharacters = value.Count(
            character => char.IsLetterOrDigit(character));

        return meaningfulCharacters >=
               _ocrOptions.MinimumDirectPdfTextCharacters;
    }
}