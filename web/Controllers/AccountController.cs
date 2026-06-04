using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

[Authorize]
[Route("account")]
public sealed class AccountController : Controller
{
    private readonly ClusterRepository _clusterRepository;
    private readonly AuthService _authService;
    private readonly ApplicationAuditService _applicationAuditService;

    public AccountController(
        ClusterRepository clusterRepository,
        AuthService authService,
        ApplicationAuditService applicationAuditService)
    {
        _clusterRepository = clusterRepository;
        _authService = authService;
        _applicationAuditService = applicationAuditService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildModelAsync(cancellationToken));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CancellationToken cancellationToken)
    {
        var currentEmail = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
        var newEmail = Text(Request.Form, "email").Trim().ToLowerInvariant();
        var name = Text(Request.Form, "name", currentEmail);
        var password = Text(Request.Form, "password");
        var passwordConfirmation = Text(Request.Form, "passwordConfirmation");

        if (string.IsNullOrWhiteSpace(newEmail))
        {
            var model = await BuildModelAsync(cancellationToken);
            return View("Index", new AccountProfileViewModel
            {
                Email = model.Email,
                Name = model.Name,
                RoleLabel = model.RoleLabel,
                ErrorMessage = "Email is required."
            });
        }

        if (!string.IsNullOrWhiteSpace(password) && !password.Equals(passwordConfirmation, StringComparison.Ordinal))
        {
            var model = await BuildModelAsync(cancellationToken);
            return View("Index", new AccountProfileViewModel
            {
                Email = model.Email,
                Name = name,
                RoleLabel = model.RoleLabel,
                ErrorMessage = "Passwords do not match."
            });
        }

        if (!newEmail.Equals(currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _clusterRepository.GetUserByEmailAsync(newEmail, cancellationToken);
            if (existing != null)
            {
                var model = await BuildModelAsync(cancellationToken);
                return View("Index", new AccountProfileViewModel
                {
                    Email = model.Email,
                    Name = name,
                    RoleLabel = model.RoleLabel,
                    ErrorMessage = "That email is already used by another account."
                });
            }
        }

        var passwordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : BCryptNet.HashPassword(password);
        await _clusterRepository.UpdateUserProfileAsync(currentEmail, name, passwordHash, cancellationToken);
        await _clusterRepository.UpdateUserEmailAsync(currentEmail, newEmail, cancellationToken);
        await _applicationAuditService.AppendApplicationAuditEventAsync(
            "account.profile.updated",
            currentEmail,
            "account",
            "account-ui",
            "Account profile updated.",
            new BsonDocument
            {
                ["previousEmail"] = currentEmail,
                ["newEmail"] = newEmail,
                ["name"] = name,
                ["emailChanged"] = !newEmail.Equals(currentEmail, StringComparison.OrdinalIgnoreCase),
                ["passwordChanged"] = !string.IsNullOrWhiteSpace(passwordHash)
            },
            "account",
            newEmail,
            cancellationToken: cancellationToken);

        var principal = await _authService.CreatePrincipalForUserAsync(newEmail, cancellationToken);
        if (principal != null)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        return View("Index", new AccountProfileViewModel
        {
            Email = newEmail,
            Name = name,
            RoleLabel = principal == null ? AccessControlService.DisplayRoleLabel(User) : AccessControlService.DisplayRoleLabel(principal),
            StatusMessage = "Account updated."
        });
    }

    private async Task<AccountProfileViewModel> BuildModelAsync(CancellationToken cancellationToken)
    {
        var email = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
        var user = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        var name = user == null ? User.Identity?.Name ?? email : BsonHelpers.GetString(user, "name");
        return new AccountProfileViewModel
        {
            Email = email,
            Name = string.IsNullOrWhiteSpace(name) ? email : name,
            RoleLabel = AccessControlService.DisplayRoleLabel(User),
            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty
        };
    }

    private static string Text(IFormCollection form, string key, string fallback = "")
    {
        var value = form[key].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
