using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Accounts;

/// <summary>
/// Finding U03: the login page says a temporary password "has been sent" only after someone asked for one, and it says
/// how to get an account.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class LoginPageWordsTests(MongoFixture mongo)
{
    private static string Page => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Login", "Index.cshtml"));

    [Fact]
    public void TheLoginPage_ShouldNotSaySentBeforeARequest()
    {
        Assert.DoesNotContain("has been sent", Page, StringComparison.Ordinal);
        Assert.Contains("TempData[\"ForgotPasswordMessage\"]", Page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLoginPage_ShouldSayHowToGetAnAccount() =>
        Assert.Contains("No account yet? Ask your contact at Scania.", Page, StringComparison.Ordinal);

    [Fact]
    public async Task ARequest_ShouldThenSayWhatHappens()
    {
        using var rig = await AccountRig.StartAsync(mongo);
        var page = rig.LoginPage();

        await page.ForgotPassword(AccountRig.NorthUser, CancellationToken.None);

        Assert.Equal("If an account exists, a temporary password has been sent.", page.TempData["ForgotPasswordMessage"]);
    }
}
