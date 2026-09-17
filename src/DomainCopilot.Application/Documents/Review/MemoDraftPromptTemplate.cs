using System.Reflection;
using System.Text;

namespace DomainCopilot.Application.Documents.Review;

public sealed class MemoDraftPromptTemplate : IMemoDraftPromptTemplate
{
    private const string SystemPromptResourceName =
        "DomainCopilot.Application.Documents.Review.prompts.MemoDraft.System.v1.md";

    private const string UserPromptResourceName =
        "DomainCopilot.Application.Documents.Review.prompts.MemoDraft.User.v1.md";

    private const int MaximumEvidenceCharacters = 1_500;

    private readonly string _systemPromptTemplate;
    private readonly string _userPromptTemplate;

    public MemoDraftPromptTemplate()
    {
        var assembly = typeof(MemoDraftPromptTemplate).Assembly;

        _systemPromptTemplate = ReadEmbeddedResource(
            assembly,
            SystemPromptResourceName);

        _userPromptTemplate = ReadEmbeddedResource(
            assembly,
            UserPromptResourceName);
    }

    public string Version =>
        "MemoDraft.System.v1 + MemoDraft.User.v1";

    public MemoDraftPrompts Render(MemoDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DocumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID is required.",
                nameof(request));
        }

        var documentMetadata =
            $"File name: {request.FileName}{Environment.NewLine}" +
            $"Source: {request.Source}{Environment.NewLine}" +
            $"Version: {request.Version}{Environment.NewLine}" +
            $"Document ID: {request.DocumentId}";

        var userPrompt = _userPromptTemplate
            .Replace(
                "{{documentMetadata}}",
                documentMetadata,
                StringComparison.Ordinal)
            .Replace(
                "{{extractedClauses}}",
                BuildExtractedClauses(request.ExtractedClauses),
                StringComparison.Ordinal)
            .Replace(
                "{{riskFindings}}",
                BuildRiskFindings(request.RiskFindings),
                StringComparison.Ordinal);

        return new MemoDraftPrompts(
            _systemPromptTemplate,
            userPrompt);
    }

    private static string BuildExtractedClauses(
        IReadOnlyList<ExtractedClause> clauses)
    {
        if (clauses.Count == 0)
        {
            return "No extracted clauses were supplied.";
        }

        var builder = new StringBuilder();

        foreach (var clause in clauses)
        {
            builder.AppendLine(
                $"<extracted-clause chunkId=\"{clause.DocumentChunkId}\" " +
                $"type=\"{clause.ClauseType}\" " +
                $"section=\"{clause.ClauseOrSection ?? "unknown"}\" " +
                $"page=\"{clause.PageNumber?.ToString() ?? "unknown"}\" " +
                $"confidence=\"{clause.Confidence:F2}\">");

            builder.AppendLine($"Summary: {clause.Summary}");
            builder.AppendLine(
                $"Evidence: {Truncate(clause.EvidenceText)}");

            builder.AppendLine("</extracted-clause>");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string BuildRiskFindings(
        IReadOnlyList<RiskFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "No playbook deviations were identified.";
        }

        var builder = new StringBuilder();

        foreach (var finding in findings)
        {
            builder.AppendLine(
                $"<risk-finding ruleId=\"{finding.RuleId}\" " +
                $"severity=\"{finding.Severity}\" " +
                $"clauseType=\"{finding.ClauseType}\" " +
                $"sourceChunkId=\"{finding.DocumentChunkId?.ToString() ?? "none"}\">");

            builder.AppendLine($"Title: {finding.Title}");
            builder.AppendLine($"Rationale: {finding.Rationale}");
            builder.AppendLine($"Recommendation: {finding.Recommendation}");

            builder.AppendLine("</risk-finding>");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaximumEvidenceCharacters
            ? value
            : value[..MaximumEvidenceCharacters];
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