using System.Reflection;
using System.Text;

namespace DomainCopilot.Application.Documents.Review;

public sealed class ClauseExtractionPromptTemplate
    : IClauseExtractionPromptTemplate
{
    private const string SystemPromptResourceName =
        "DomainCopilot.Application.Documents.Review.Prompts.ClauseExtraction.System.v1.md";

    private const string UserPromptResourceName =
        "DomainCopilot.Application.Documents.Review.Prompts.ClauseExtraction.User.v1.md";

    private readonly string _systemPromptTemplate;
    private readonly string _userPromptTemplate;

    public ClauseExtractionPromptTemplate()
    {
        var assembly = typeof(ClauseExtractionPromptTemplate).Assembly;

        _systemPromptTemplate = ReadEmbeddedResource(
            assembly,
            SystemPromptResourceName);

        _userPromptTemplate = ReadEmbeddedResource(
            assembly,
            UserPromptResourceName);
    }

    public string Version =>
        "ClauseExtraction.System.v1 + ClauseExtraction.User.v1";

    public ClauseExtractionPrompts Render(
        IReadOnlyList<ReviewSourceChunk> chunks,
        int maximumChunkCharacters)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            throw new ArgumentException(
                "At least one source chunk is required.",
                nameof(chunks));
        }

        var evidence = BuildEvidence(chunks, maximumChunkCharacters);

        var chunkIds = string.Join(
    Environment.NewLine,
    chunks.Select(chunk => $"- {chunk.DocumentChunkId}"));

        var userPrompt = _userPromptTemplate
     .Replace(
         "{{chunkIds}}",
         chunkIds,
         StringComparison.Ordinal)
     .Replace(
         "{{evidence}}",
         evidence,
         StringComparison.Ordinal);

        return new ClauseExtractionPrompts(
            _systemPromptTemplate,
            userPrompt);
    }

    private static string BuildEvidence(
        IReadOnlyList<ReviewSourceChunk> chunks,
        int maximumChunkCharacters)
    {
        var builder = new StringBuilder();

        foreach (var chunk in chunks)
        {
            builder.AppendLine(
                $"<contract-chunk id=\"{chunk.DocumentChunkId}\" " +
                $"section=\"{chunk.ClauseOrSection ?? "unknown"}\" " +
                $"page=\"{chunk.PageNumber?.ToString() ?? "unknown"}\" " +
                $"lowConfidence=\"{chunk.LowConfidence}\">");

            builder.AppendLine(
                Truncate(chunk.Content, maximumChunkCharacters));

            builder.AppendLine("</contract-chunk>");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string Truncate(
        string value,
        int maximumLength)
    {
        return value.Length <= maximumLength
            ? value
            : value[..maximumLength];
    }

    private static string ReadEmbeddedResource(
        Assembly assembly,
        string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded prompt resource '{resourceName}' was not found.");
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Trim();
    }
}