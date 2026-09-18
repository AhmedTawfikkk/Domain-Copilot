using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DomainCopilot.Application.Documents.Ingestion;

namespace DomainCopilot.Infrastructure.Ingestion;

public sealed class TesseractPdfOcrService : IPdfOcrService
{
    private static readonly Regex PageNumberPattern = new(
        @"-(?<page>\d+)\.png$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly PdfOcrOptions _options;

    public TesseractPdfOcrService(PdfOcrOptions options)
    {
        _options = options;
    }

    public async Task<IReadOnlyList<ExtractedPage>> ExtractAsync(
        Stream pdfContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfContent);

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"domain-copilot-ocr-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var inputPdfPath = Path.Combine(
                temporaryDirectory,
                "source.pdf");

            var imagePrefix = Path.Combine(
                temporaryDirectory,
                "page");

            await using (var output = new FileStream(
                inputPdfPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                useAsync: true))
            {
                await pdfContent.CopyToAsync(
                    output,
                    cancellationToken);
            }

            await RunProcessAsync(
                _options.PdfToPpmExecutablePath,
                new[]
                {
                    "-png",
                    "-r",
                    _options.Dpi.ToString(CultureInfo.InvariantCulture),
                    inputPdfPath,
                    imagePrefix
                },
                cancellationToken);

            var pageImages = Directory
                .GetFiles(temporaryDirectory, "page-*.png")
                .Select(path => new
                {
                    Path = path,
                    PageNumber = GetPageNumber(path)
                })
                .OrderBy(item => item.PageNumber)
                .ToList();

            if (pageImages.Count == 0)
            {
                throw new InvalidOperationException(
                    "Poppler did not produce any page images for OCR.");
            }

            var pages = new List<ExtractedPage>(pageImages.Count);

            foreach (var image in pageImages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                pages.Add(await ExtractPageAsync(
                    image.Path,
                    image.PageNumber,
                    cancellationToken));
            }

            return pages;
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    private async Task<ExtractedPage> ExtractPageAsync(
        string imagePath,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var result = await RunProcessAsync(
            _options.TesseractExecutablePath,
            new[]
            {
                imagePath,
                "stdout",
                "-l",
                _options.Language,
                "--psm",
                "6",
                "tsv"
            },
            cancellationToken);

        var lines = new List<OcrLine>();
        var confidences = new List<double>();

        foreach (var row in result.StandardOutput.Split(
                     Environment.NewLine,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (row.StartsWith("level\t", StringComparison.Ordinal))
            {
                continue;
            }

            var values = row.Split('\t');

            if (values.Length < 12 ||
                !int.TryParse(values[2], out var blockNumber) ||
!int.TryParse(values[3], out var paragraphNumber) ||
!int.TryParse(values[4], out var lineNumber))
            {
                continue;
            }

            var text = values[11].Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var currentLine = lines.LastOrDefault();

            if (currentLine is null ||
                currentLine.BlockNumber != blockNumber ||
                currentLine.ParagraphNumber != paragraphNumber ||
                currentLine.LineNumber != lineNumber)
            {
                currentLine = new OcrLine(
                    blockNumber,
                    paragraphNumber,
                    lineNumber);

                lines.Add(currentLine);
            }

            currentLine.Words.Add(text);

            if (double.TryParse(
                    values[10],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var confidence) &&
                confidence >= 0)
            {
                confidences.Add(confidence / 100.0);
            }
        }

        var extractedText = string.Join(
            Environment.NewLine,
            lines.Select(line => string.Join(" ", line.Words)))
            .Trim();

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException(
                $"OCR did not extract readable text from page {pageNumber}.");
        }

        var extractionConfidence = confidences.Count == 0
            ? 0.0
            : Math.Clamp(confidences.Average(), 0.0, 1.0);

        return new ExtractedPage(
            pageNumber,
            extractedText,
            extractionConfidence);
    }

    private async Task<ProcessResult> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Could not start OCR process '{executablePath}'.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutSource.CancelAfter(
            TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException(
                $"OCR process exceeded {_options.TimeoutSeconds} seconds.");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"OCR process failed with exit code {process.ExitCode}: " +
                standardError.Trim());
        }

        return new ProcessResult(
            standardOutput,
            standardError);
    }

    private static int GetPageNumber(string path)
    {
        var match = PageNumberPattern.Match(path);

        if (!match.Success ||
            !int.TryParse(
                match.Groups["page"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var pageNumber))
        {
            throw new InvalidOperationException(
                $"Could not determine page number from '{path}'.");
        }

        return pageNumber;
    }
    private sealed class OcrLine
    {
        public OcrLine(
            int blockNumber,
            int paragraphNumber,
            int lineNumber)
        {
            BlockNumber = blockNumber;
            ParagraphNumber = paragraphNumber;
            LineNumber = lineNumber;
        }

        public int BlockNumber { get; }

        public int ParagraphNumber { get; }

        public int LineNumber { get; }

        public List<string> Words { get; } = [];
    }
    private sealed record ProcessResult(
        string StandardOutput,
        string StandardError);
}