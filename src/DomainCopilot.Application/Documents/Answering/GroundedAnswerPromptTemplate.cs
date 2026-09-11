using System.Reflection;
using System.Text;
using DomainCopilot.Application.Documents.Retrieval;

namespace DomainCopilot.Application.Documents.Answering;

public sealed class GroundedAnswerPromptTemplate : IGroundedAnswerPromptTemplate
{
    private const string SystemPromptResourceName =
        "DomainCopilot.Application.Documents.Answering.Prompts.GroundedAnswer.System.v1.md";

    private const string UserPromptResourceName =
        "DomainCopilot.Application.Documents.Answering.Prompts.GroundedAnswer.User.v1.md";

    private const int MaximumChunkCharacters = 3_500;

    private readonly string _systemPromptTemplate;
    private readonly string _userPromptTemplate;

    public GroundedAnswerPromptTemplate()
    {
        var assembly = typeof(GroundedAnswerPromptTemplate).Assembly;

        _systemPromptTemplate = ReadEmbeddedResource(
            assembly,
            SystemPromptResourceName);

        _userPromptTemplate = ReadEmbeddedResource(
            assembly,
            UserPromptResourceName);
    }

    public string Version => "GroundedAnswer.System.v1 + GroundedAnswer.User.v1";

    public GroundedAnswerPrompts Render(
        string question,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(chunks);

        var evidence = BuildEvidence(chunks);

        var userPrompt = _userPromptTemplate
            .Replace("{{question}}", question.Trim(), StringComparison.Ordinal)
            .Replace("{{evidence}}", evidence, StringComparison.Ordinal);

        return new GroundedAnswerPrompts(
            SystemPrompt: _systemPromptTemplate,
            UserPrompt: userPrompt);
    }

    private static string BuildEvidence(IReadOnlyList<RetrievedChunk> chunks)
    {
        var builder = new StringBuilder();

        foreach (var chunk in chunks)
        {
            var content = Truncate(chunk.Content, MaximumChunkCharacters);

            builder.AppendLine(
                $"<evidence-chunk id=\"{chunk.DocumentChunkId}\" " +
                $"document=\"{chunk.FileName}\" " +
                $"source=\"{chunk.Source}\" " +
                $"version=\"{chunk.Version}\" " +
                $"page=\"{chunk.PageNumber?.ToString() ?? "unknown"}\" " +
                $"section=\"{chunk.ClauseOrSection ?? "unknown"}\">");

            builder.AppendLine(content);
            builder.AppendLine("</evidence-chunk>");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string Truncate(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value;
        }

        return value[..maximumLength];
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