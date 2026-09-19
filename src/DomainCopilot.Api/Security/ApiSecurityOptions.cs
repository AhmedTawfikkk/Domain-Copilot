namespace DomainCopilot.Api.Security;

public sealed class ApiSecurityOptions
{
    public string LawyerApiKey { get; init; } = string.Empty;

    public string CounselApiKey { get; init; } = string.Empty;

    public void Validate()
    {
        ValidateKey(
            LawyerApiKey,
            "ApiSecurity:LawyerApiKey");

        ValidateKey(
            CounselApiKey,
            "ApiSecurity:CounselApiKey");

        if (string.Equals(
                LawyerApiKey,
                CounselApiKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "ApiSecurity lawyer and counsel API keys must be different.");
        }
    }

    private static void ValidateKey(
        string apiKey,
        string configurationName)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 24)
        {
            throw new InvalidOperationException(
                $"{configurationName} must contain at least 24 characters.");
        }
    }
}