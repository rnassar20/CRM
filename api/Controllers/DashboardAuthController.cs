using System.Security.Claims;
using Crm.Api.Data;
using Crm.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

/// <summary>
/// Hangfire dashboard sessions use a separate cookie scheme (not the API's JWT), because the
/// dashboard is served to a browser. Credentials are validated against the same Users table as
/// the regular login; only Admins are allowed in.
/// </summary>
[ApiController]
[Route("dashboard")]
public class DashboardAuthController(AppDbContext db) : ControllerBase
{
    public const string CookieScheme = "DashboardCookie";

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult LoginPage([FromQuery] string? error = null, [FromQuery] string? returnUrl = null)
    {
        return Content(DashboardLoginPage.Html(error, returnUrl), "text/html");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromForm] DashboardLoginRequest request, [FromQuery] string? returnUrl = null)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Content(DashboardLoginPage.Html("Invalid email or password."), "text/html");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        await HttpContext.SignInAsync(CookieScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme)));

        // Only allow local relative redirects to avoid open-redirect via the ReturnUrl parameter.
        var target = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//")
            ? "/hangfire"
            : returnUrl;
        return Redirect(target);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieScheme);
        return Redirect("/dashboard/login");
    }
}

public record DashboardLoginRequest(string Email = "", string Password = "");

/// <summary>Minimal dependency-free login page for the Hangfire dashboard.</summary>
public static class DashboardLoginPage
{
    public static string Html(string? error = null, string? returnUrl = null)
    {
        var errorBox = string.IsNullOrWhiteSpace(error)
            ? ""
            : $"""<div class="error">{error}</div>""";
        var action = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//")
            ? "/dashboard/login"
            : $"/dashboard/login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Sign in - CRM Hangfire</title>
              <style>
                body { font-family: -apple-system, "Segoe UI", Roboto, sans-serif; background: #1c232b; color: #e8eef4; margin: 0; display: flex; min-height: 100vh; align-items: center; justify-content: center; }
                .card { background: #26323d; padding: 2rem 2.5rem; border-radius: 10px; box-shadow: 0 8px 30px rgba(0,0,0,.35); width: 100%; max-width: 340px; }
                h1 { font-size: 1.15rem; margin: 0 0 1.5rem; }
                label { display: block; font-size: .85rem; margin: 1rem 0 .35rem; }
                input { width: 100%; box-sizing: border-box; padding: .6rem .7rem; border-radius: 6px; border: 1px solid #3a4856; background: #1c232b; color: #e8eef4; font-size: .95rem; }
                button { margin-top: 1.6rem; width: 100%; padding: .65rem; border: 0; border-radius: 6px; background: #4f8cff; color: #fff; font-size: .95rem; cursor: pointer; }
                button:hover { background: #3c79f0; }
                .error { background: #5a2a2a; border: 1px solid #a05050; color: #ffd7d7; padding: .6rem .8rem; border-radius: 6px; font-size: .85rem; margin-bottom: 1rem; }
                .hint { margin-top: 1.4rem; font-size: .78rem; color: #8fa3b4; }
              </style>
            </head>
            <body>
              <form class="card" method="post" action="{{action}}">
                <h1>CRM Jobs Dashboard</h1>
                {{errorBox}}
                <label for="email">Email</label>
                <input id="email" name="Email" type="email" autocomplete="username" required autofocus>
                <label for="password">Password</label>
                <input id="password" name="Password" type="password" autocomplete="current-password" required>
                <button type="submit">Sign in</button>
                <div class="hint">Admin accounts only. Uses the same credentials as the CRM.</div>
              </form>
            </body>
            </html>
            """;
    }
}
