using System.Reflection;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>Finding S06, the manager's sixth point: the account page checks the current password, so it has the sign-in's rate limit.</summary>
public sealed class AccountPageRateLimitTests
{
    [Fact]
    public void TheAccountPagesSave_ShouldHaveTheSignInRateLimit()
    {
        var save = typeof(AccountController).GetMethods().Single(method => method.Name == nameof(AccountController.Save) && method.GetCustomAttribute<HttpPostAttribute>() is not null);

        Assert.Equal(SecurityRateLimitPolicyNames.Login, save.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }
}
