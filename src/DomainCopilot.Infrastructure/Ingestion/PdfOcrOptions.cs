namespace DomainCopilot.Infrastructure.Ingestion;

public sealed class PdfOcrOptions
{
    public string TesseractExecutablePath { get; init; } = string.Empty;

    public string PdfToPpmExecutablePath { get; init; } = string.Empty;

    public string Language { get; init; } = "eng";

    public int Dpi { get; init; } = 300;

    public int TimeoutSeconds { get; init; } = 90;

    public int MinimumDirectPdfTextCharacters { get; init; } = 40;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TesseractExecutablePath) ||
            !File.Exists(TesseractExecutablePath))
        {
            throw new InvalidOperationException(
                "PdfOcr:TesseractExecutablePath must point to tesseract.exe.");
        }

        if (string.IsNullOrWhiteSpace(PdfToPpmExecutablePath) ||
            !File.Exists(PdfToPpmExecutablePath))
        {
            throw new InvalidOperationException(
                "PdfOcr:PdfToPpmExecutablePath must point to pdftoppm.exe.");
        }

        if (string.IsNullOrWhiteSpace(Language))
        {
            throw new InvalidOperationException(
                "PdfOcr:Language is required.");
        }

        if (Dpi is < 150 or > 600)
        {
            throw new InvalidOperationException(
                "PdfOcr:Dpi must be between 150 and 600.");
        }

        if (TimeoutSeconds is < 10 or > 300)
        {
            throw new InvalidOperationException(
                "PdfOcr:TimeoutSeconds must be between 10 and 300.");
        }

        if (MinimumDirectPdfTextCharacters is < 1 or > 1_000)
        {
            throw new InvalidOperationException(
                "PdfOcr:MinimumDirectPdfTextCharacters must be between 1 and 1000.");
        }
    }
}