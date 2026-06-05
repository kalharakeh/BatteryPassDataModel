using System.Security.Claims;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace BatteryPassWeb.Controllers;

[Route("login")]
public class LoginController : Controller
{
    private readonly AuthService _authService;
    private readonly ApplicationAuditService _applicationAuditService;
    private readonly AuthenticationSessionService _authenticationSessionService;

    public LoginController(
        AuthService authService,
        ApplicationAuditService applicationAuditService,
        AuthenticationSessionService authenticationSessionService)
    {
        _authService = authService;
        _applicationAuditService = applicationAuditService;
        _authenticationSessionService = authenticationSessionService;
    }

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? next, [FromQuery] string? returnUrl, [FromQuery] bool switchAccount)
    {
        var safeReturnUrl = SafeInteractiveReturnUrl(next) ?? SafeInteractiveReturnUrl(returnUrl);
        if (User.Identity?.IsAuthenticated == true && !switchAccount)
        {
            return Redirect(safeReturnUrl ?? "/admin");
        }

        return View(new LoginViewModel { ReturnUrl = safeReturnUrl ?? string.Empty });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LoginViewModel model, CancellationToken cancellationToken)
    {
        var loginResult = await _authService.AuthenticateLoginAsync(model.Email, model.Password, cancellationToken);
        if (loginResult.Status == LoginAuthenticationStatus.RequiresTemporaryPasswordChange)
        {
            return View("ChangeTemporaryPassword", new TemporaryPasswordChangeViewModel
            {
                Email = loginResult.Email,
                StatusMessage = "You must choose a new password before continuing."
            });
        }

        if (loginResult.Status != LoginAuthenticationStatus.Authenticated || loginResult.Principal == null)
        {
            model.ErrorMessage = "Invalid email or password.";
            return View(model);
        }

        var principal = loginResult.Principal;
        await _authenticationSessionService.SignInAsync(HttpContext, principal, cancellationToken);

        var safeReturnUrl = SafeInteractiveReturnUrl(model.ReturnUrl);
        if (!string.IsNullOrWhiteSpace(safeReturnUrl))
        {
            return Redirect(safeReturnUrl);
        }

        if (principal.IsInRole("admin"))
        {
            return Redirect("/admin");
        }

        return Redirect("/");
    }

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword([FromForm] string email, CancellationToken cancellationToken)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        await _authService.CreatePasswordResetAsync(
            email,
            remoteIp,
            RequestBaseUrl(),
            cancellationToken);
        await _applicationAuditService.AppendApplicationAuditEventAsync(
            "security.passwordReset.requested",
            string.IsNullOrWhiteSpace(email) ? "anonymous" : email.Trim().ToLowerInvariant(),
            "anonymous",
            "login",
            "Password reset requested.",
            new BsonDocument
            {
                ["email"] = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant(),
                ["remoteIp"] = remoteIp
            },
            "account",
            string.IsNullOrWhiteSpace(email) ? "anonymous" : email.Trim().ToLowerInvariant(),
            cancellationToken: cancellationToken);
        TempData["ForgotPasswordMessage"] = "If an account exists, a temporary password has been sent.";
        return Redirect("/login");
    }

    [HttpGet("change-temporary-password")]
    public IActionResult ChangeTemporaryPassword()
    {
        return View(new TemporaryPasswordChangeViewModel());
    }

    [HttpPost("change-temporary-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeTemporaryPassword(TemporaryPasswordChangeViewModel model, CancellationToken cancellationToken)
    {
        if (!model.Password.Equals(model.PasswordConfirmation, StringComparison.Ordinal))
        {
            model.ErrorMessage = "Passwords do not match.";
            model.Password = string.Empty;
            model.PasswordConfirmation = string.Empty;
            return View(model);
        }

        var success = await _authService.ConsumeTemporaryPasswordAsync(
            model.Email,
            model.TemporaryPassword,
            model.Password,
            cancellationToken);
        if (!success)
        {
            model.ErrorMessage = "The temporary password is invalid or expired.";
            model.TemporaryPassword = string.Empty;
            model.Password = string.Empty;
            model.PasswordConfirmation = string.Empty;
            return View(model);
        }

        TempData["ForgotPasswordMessage"] = "Password changed. Sign in with your new password.";
        await _applicationAuditService.AppendApplicationAuditEventAsync(
            "security.password.changed",
            model.Email.Trim().ToLowerInvariant(),
            "account",
            "login",
            "Password changed through temporary password flow.",
            new BsonDocument { ["email"] = model.Email.Trim().ToLowerInvariant() },
            "account",
            model.Email.Trim().ToLowerInvariant(),
            cancellationToken: cancellationToken);
        return Redirect("/login");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";

    private string? SafeInteractiveReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return null;
        }

        var pathOnly = returnUrl.Split('?', '#')[0];
        if (pathOnly.Equals("/api", StringComparison.OrdinalIgnoreCase)
            || pathOnly.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return returnUrl;
    }
}
