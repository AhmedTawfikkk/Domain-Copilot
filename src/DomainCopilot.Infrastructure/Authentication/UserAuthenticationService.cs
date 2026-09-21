using DomainCopilot.Application.Authentication;
using Microsoft.AspNetCore.Identity;

namespace DomainCopilot.Infrastructure.Authentication;

public sealed class UserAuthenticationService : IUserAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;

    public UserAuthenticationService(
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    public async Task<AuthenticatedUser> RegisterAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = ValidateEmail(command.Email);
        var displayName = ValidateDisplayName(command.DisplayName);
        var role = ValidateRole(command.Role);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        var createResult = await _userManager.CreateAsync(user, command.Password);

        if (!createResult.Succeeded)
        {
            throw new ArgumentException(FormatErrors(createResult), nameof(command));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Account was created but role assignment failed: {FormatErrors(roleResult)}");
        }

        return new AuthenticatedUser(user.Id, user.Email!, displayName, role);
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Email) ||
            string.IsNullOrWhiteSpace(command.Password))
        {
            return null;
        }

        var user = await _userManager.FindByEmailAsync(command.Email.Trim());

        if (user is null || !await _userManager.CheckPasswordAsync(user, command.Password))
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.Contains(IdentityRoleNames.Counsel)
            ? IdentityRoleNames.Counsel
            : IdentityRoleNames.Lawyer;

        return new AuthenticatedUser(user.Id, user.Email!, user.DisplayName, role);
    }

    private static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            !System.Net.Mail.MailAddress.TryCreate(email, out _))
        {
            throw new ArgumentException("Enter a valid email address.", nameof(email));
        }

        return email.Trim();
    }

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.Trim().Length is < 2 or > 200)
        {
            throw new ArgumentException(
                "Display name must be between 2 and 200 characters.",
                nameof(displayName));
        }

        return displayName.Trim();
    }

    private static string ValidateRole(string role)
    {
        if (string.Equals(role, IdentityRoleNames.Lawyer, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityRoleNames.Lawyer;
        }

        if (string.Equals(role, IdentityRoleNames.Counsel, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityRoleNames.Counsel;
        }

        throw new ArgumentException("Role must be Lawyer or Counsel.", nameof(role));
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
