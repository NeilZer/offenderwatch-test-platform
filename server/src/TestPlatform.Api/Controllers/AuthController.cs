using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestPlatform.Api.Contracts;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Controllers;

/// <summary>B-07 — sign in / out and "who am I".</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(UserStore users) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Login(LoginInput input)
    {
        var user = users.Validate(input.Username, input.Password);
        if (user is null) return Unauthorized(new ProblemDetails { Status = 401, Title = "Wrong username or password" });

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role)], AuthSetup.CookieScheme);
        await HttpContext.SignInAsync(AuthSetup.CookieScheme, new ClaimsPrincipal(identity));
        return ToDto(user.Username, user.Role);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSetup.CookieScheme);
        return NoContent();
    }

    [HttpGet("me")]
    public ActionResult<UserDto> Me() =>
        ToDto(User.Identity!.Name!, User.FindFirstValue(ClaimTypes.Role) ?? Roles.Viewer);

    private static UserDto ToDto(string name, string role) => new(name, role, role == Roles.Operator);
}
