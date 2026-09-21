namespace DomainCopilot.Api.Contracts;

public sealed record RegisterUserRequest(
    string Email,
    string DisplayName,
    string Password,
    string Role);

public sealed record LoginUserRequest(
    string Email,
    string Password);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role);
