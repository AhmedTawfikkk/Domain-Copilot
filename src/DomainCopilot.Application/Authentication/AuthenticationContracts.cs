namespace DomainCopilot.Application.Authentication;

public sealed record RegisterUserCommand(
    string Email,
    string DisplayName,
    string Password,
    string Role);

public sealed record LoginUserCommand(
    string Email,
    string Password);

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    string Role);

public interface IUserAuthenticationService
{
    Task<AuthenticatedUser> RegisterAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> AuthenticateAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken = default);
}
