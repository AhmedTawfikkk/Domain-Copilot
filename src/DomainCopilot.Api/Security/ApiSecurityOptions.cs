namespace DomainCopilot.Api.Security;

public sealed class ApiSecurityOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || ApiKey.Length < 24)
        {
            throw new InvalidOperationException(
                "ApiSecurity:ApiKey must contain at least 24 characters.");
        }
    }
}
