using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>
/// Review 1 of N1, point 3 (decision 55, finding W03): the texts a customer, a reader or a visitor reaches that the page
/// walk can't open (messages after an action, a token of an older kind, an empty list) use the decided words: "customer"
/// on their pages, "cluster" only on the team's and in the API; "Your account"; "Log in".
/// </summary>
public sealed class PinnedWordsTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    [Fact]
    public void TheHelp_ShouldSayCustomerAdmins_InThePartEveryoneReads()
    {
        var guide = Read("web", "Views", "Help", "Guide.cshtml");

        Assert.Contains("customer admins see no notice and leave no line.", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("cluster admins see no notice", guide, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("readWriteSign")]
    [InlineData("sign")]
    public void AnOlderTokensNote_ShouldUseThePagesWord(string mode)
    {
        Assert.Contains("customer token", ClusterTokenRules.StillDoes(mode, "customer"), StringComparison.Ordinal);
        Assert.DoesNotContain("cluster", ClusterTokenRules.StillDoes(mode, "customer"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cluster token", ClusterTokenRules.StillDoes(mode), StringComparison.Ordinal); // the team's page
        Assert.Contains("@ClusterTokenRules.StillDoes(token.AccessMode, word)", Read("web", "Views", "Shared", "_ApiTokenList.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCustomerAdminsTokenPage_ShouldSayCustomer()
    {
        Assert.Contains("Also batteries with no @word", Read("web", "Views", "Shared", "_ApiTokenList.cshtml"), StringComparison.Ordinal);
        Assert.Contains("\"No API tokens yet.\"", Read("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"), StringComparison.Ordinal);
        var controller = Read("web", "Controllers", "ClusterAdminController.cs");
        Assert.Contains("\"Customers saved.\"", controller, StringComparison.Ordinal);
        Assert.Contains("Text(Request.Form, \"name\", \"API token\")", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCustomerAdminsUserMessages_ShouldSayCustomer()
    {
        Assert.Equal("No customer has this ID.", BatteryPassWeb.Controllers.ClusterAdminController.NoSuchCustomer);
        Assert.DoesNotContain("ClusterRepository.NoSuchCluster;", Read("web", "Controllers", "ClusterAdminController.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheReceivingCustomer_ShouldReadCustomer_WhenTheSenderLostItsAuthorisation()
    {
        Assert.StartsWith("The sending customer's authorisation", WrittenAuthorisation.GiverMissing, StringComparison.Ordinal);
        Assert.StartsWith("the sending customer's authorisation", WrittenAuthorisation.GiverRefusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUploadForAnotherCustomersBattery_ShouldSayCustomer_OnThePage_AndKeepTheApisWord()
    {
        Assert.Equal("this battery belongs to another cluster", UploadService.NotYours); // the API's answer
        Assert.Contains("UploadService.NotYours => \"This battery belongs to another customer", Read("web", "Dpp", "Pages", "UploadPages.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheAccountAndLogInMessages_ShouldUseTheBarsNames()
    {
        Assert.Contains("\"Your account\"", AccessControlService.NotYourOwnAccount, StringComparison.Ordinal);
        Assert.DoesNotContain("Use Account at the top", AccessControlService.NotYourOwnAccount, StringComparison.Ordinal);
        var login = Read("web", "Controllers", "LoginController.cs");
        Assert.Contains("\"Password changed. Log in with your new password.\"", login, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign in with your new password", login, StringComparison.Ordinal);
        Assert.DoesNotContain("sign in", Read("web", "Views", "Help", "Guide.cshtml"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Review 2 of N1, point 4: the customer admin's battery History (moves and hand-overs) says "customer".</summary>
    [Fact]
    public void TheCustomerAdminsHistory_ShouldSayCustomer()
    {
        var history = Read("web", "Dpp", "Audit", "ClusterHistory.cs");

        foreach (var old in new[] { "\"Another cluster\"", "to your cluster\"", "to another cluster\"", "asked another cluster", "asked your cluster", "moved to another cluster" })
        {
            Assert.DoesNotContain(old, history, StringComparison.Ordinal);
        }
    }
}
