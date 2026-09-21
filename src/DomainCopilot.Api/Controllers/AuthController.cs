using DomainCopilot.Api.Contracts;
using DomainCopilot.Api.Security;
using DomainCopilot.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserAuthenticationService _authenticationService;
    private readonly ICookieUserPrincipalFactory _principalFactory;

    public AuthController(
        IUserAuthenticationService authenticationService,
        ICookieUserPrincipalFactory principalFactory)
    {
        _authenticationService = authenticationService;
        _principalFactory = principalFactory;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserResponse>> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _authenticationService.RegisterAsync(
                new RegisterUserCommand(
                    request.Email,
                    request.DisplayName,
                    request.Password,
                    request.Role),
                cancellationToken);

            await SignInAsync(user);

            return CreatedAtAction(nameof(Me), ToResponse(user));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<CurrentUserResponse>> Login(
        [FromBody] LoginUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _authenticationService.AuthenticateAsync(
            new LoginUserCommand(request.Email, request.Password),
            cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { error = "Email or password is incorrect." });
        }

        await SignInAsync(user);

        return Ok(ToResponse(user));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me()
    {
        var response = CurrentUserResponseFactory.From(User);

        return response is null ? Unauthorized() : Ok(response);
    }

    private Task SignInAsync(AuthenticatedUser user) =>
        HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            _principalFactory.Create(user));

    private static CurrentUserResponse ToResponse(AuthenticatedUser user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role);
}
