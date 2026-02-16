using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace blog_keycloak_series.Web.Controllers;

[Route("[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    [HttpGet("Login")]
    public IActionResult Login(string returnUrl = "/")
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl
        };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/"
        };

        // This will automatically handle both local and Keycloak logout
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return SignOut(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("LogoutCallback")]
    public IActionResult LogoutCallback()
    {
        return RedirectToRoute("/");
    }

    [HttpGet("Claims")]
    public IActionResult Claims()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok(claims);
        }
        return Unauthorized();
    }
}