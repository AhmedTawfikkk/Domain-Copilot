using System.Text;
using DomainCopilot.Application.Documents.Ingestion;
using DomainCopilot.Infrastructure.Ingestion;
using Moq;

namespace DomainCopilot.Application.Tests.Ingestion;

public sealed class PdfTextExtractorTests
{
    [Fact]
    public async Task ExtractAsync_WhenPdfPageHasNoDirectText_UsesOcrPage()
    {
        var ocrService = new Mock<IPdfOcrService>();

        ocrService
            .Setup(service => service.ExtractAsync(
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ExtractedPage(
                    1,
                    "SECTION 7 LIMITATION OF LIABILITY",
                    0.72)
            });

        var options = new PdfOcrOptions
        {
            MinimumDirectPdfTextCharacters = 40
        };

        var extractor = new PdfTextExtractor(
            ocrService.Object,
            options);

        await using var pdfStream = CreateEmptyPdfStream();

        var pages = await extractor.ExtractAsync(pdfStream);

        var page = Assert.Single(pages);

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(
            "SECTION 7 LIMITATION OF LIABILITY",
            page.Text);
        Assert.Equal(0.72, page.ExtractionConfidence, 2);

        ocrService.Verify(
            service => service.ExtractAsync(
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static MemoryStream CreateEmptyPdfStream()
    {
        var stream = new MemoryStream();
        var objectOffsets = new List<long>();

        Write(stream, "%PDF-1.4\n");

        objectOffsets.Add(stream.Position);
        Write(
            stream,
            "1 0 obj\n" +
            "<< /Type /Catalog /Pages 2 0 R >>\n" +
            "endobj\n");

        objectOffsets.Add(stream.Position);
        Write(
            stream,
            "2 0 obj\n" +
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n" +
            "endobj\n");

        objectOffsets.Add(stream.Position);
        Write(
            stream,
            "3 0 obj\n" +
            "<< /Type /Page /Parent 2 0 R " +
            "/MediaBox [0 0 612 792] >>\n" +
            "endobj\n");

        var xrefOffset = stream.Position;

        Write(stream, "xref\n0 4\n");
        Write(stream, "0000000000 65535 f \n");

        foreach (var objectOffset in objectOffsets)
        {
            Write(stream, $"{objectOffset:D10} 00000 n \n");
        }

        Write(
            stream,
            "trailer\n" +
            "<< /Size 4 /Root 1 0 R >>\n" +
            "startxref\n" +
            $"{xrefOffset}\n" +
            "%%EOF\n");

        stream.Position = 0;

        return stream;
    }

    private static void Write(
        Stream stream,
        string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);

        stream.Write(bytes, 0, bytes.Length);
    }
}