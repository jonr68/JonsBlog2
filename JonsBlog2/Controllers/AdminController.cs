using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JonsBlog2.Options;
using JonsBlog2.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JonsBlog2.Controllers;

[ApiController]
[Route("admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly AdminLoginOptions _adminLoginOptions;

    public AdminController(IWebHostEnvironment environment, IOptions<AdminLoginOptions> adminLoginOptions)
    {
        _environment = environment;
        _adminLoginOptions = adminLoginOptions.Value;
    }

    [AllowAnonymous]
    [HttpGet("login-page")]
    public IActionResult GetLoginPage()
    {
        var loginPagePath = Path.Combine(_environment.ContentRootPath, "WebPages", "AdminLoginPage.html");

        if (!System.IO.File.Exists(loginPagePath))
        {
            return NotFound();
        }

        return PhysicalFile(loginPagePath, "text/html; charset=utf-8");
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        var usernameMatches = SecureEquals(request.Username, _adminLoginOptions.Username);
        var passwordMatches = AdminPasswordHasher.VerifyPassword(request.Password, _adminLoginOptions.PasswordHash);

        if (!usernameMatches || !passwordMatches)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, _adminLoginOptions.Username),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return Ok(new { message = "Login successful." });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logout successful." });
    }

    [AllowAnonymous]
    [HttpGet("status")]
    public IActionResult Status()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        return Ok(new
        {
            isAuthenticated,
            username = isAuthenticated ? User.Identity?.Name : null
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            username = User.Identity?.Name,
            role = "Admin"
        });
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        try
        {
            return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    public sealed record AdminLoginRequest(string Username, string Password);
}
