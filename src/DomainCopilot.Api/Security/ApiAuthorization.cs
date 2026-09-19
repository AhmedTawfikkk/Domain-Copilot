namespace DomainCopilot.Api.Security;

public static class ApiRoles
{
    public const string Lawyer = "Lawyer";

    public const string Counsel = "Counsel";
}

public static class ApiAuthorizationPolicies
{
    public const string Lawyer = "lawyer";

    public const string LawyerOrCounsel = "lawyer-or-counsel";

    public const string Counsel = "counsel";
}