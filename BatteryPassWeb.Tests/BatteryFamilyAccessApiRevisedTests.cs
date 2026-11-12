using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class BatteryFamilyAccessApiRevisedTests
{


    [Fact]
    public void UserFacingViews_ShouldNotUseRemovedIdentityLabels()
    {
        var files = new[]
        {
            RepoFile("web", "Views", "Registry", "Index.cshtml"),
            RepoFile("web", "Views", "Passport", "Summary.cshtml"),
            RepoFile("web", "Views", "Passport", "Detail.cshtml"),
            RepoFile("web", "Views", "Admin", "Clusters.cshtml")
        };

        var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));

        Assert.DoesNotContain(">Name <", text);
        Assert.DoesNotContain(">Model Number<", text);
        Assert.DoesNotContain("Product/battery version", text);
        Assert.DoesNotContain(">Product<", text);
        Assert.DoesNotContain("Product templates", text);
    }

    [Fact]
    public void ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign()
    {
        var api = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));

        Assert.Contains("ExternalTokenAccessMode.Sign", repository);
        Assert.Contains("ExternalTokenRequirement.Sign", api);
        Assert.Contains("tokens.ValidateTokenAsync", api);
        Assert.Contains("[HttpPost(\"batteries/{id}/publish\")]", api);
        Assert.DoesNotContain("X-Battery-Secret", api);
        Assert.DoesNotContain("ValidateBatterySecretAsync", api);
        Assert.DoesNotContain("UpsertBatterySecretAsync", repository);
        Assert.Contains("DropCollectionAsync(\"batterySecrets\"", repository);
        Assert.DoesNotContain("EnsureBatterySecret", initializer);
    }

    [Fact]
    public void AccountAndClusterGuardrails_ShouldBePresent()
    {
        var accountController = RepoFile("web", "Controllers", "AccountController.cs");
        var accountView = RepoFile("web", "Views", "Account", "Index.cshtml");
        var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
        var authService = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));

        Assert.True(File.Exists(accountController));
        Assert.True(File.Exists(accountView));
        Assert.Contains("CountMembershipsByClusterAsync", clusterRepository);
        Assert.Contains("ForceDeleteClusterAsync", adminController);
        Assert.Contains("I understand this deletes linked cluster data", adminController);
        Assert.Contains("CreatePrincipalForUserAsync", authService);
        Assert.Contains("UpdateUserEmailAsync", clusterRepository);
        Assert.Contains("UpdateUserProfileAsync", clusterRepository);
        Assert.Contains("RightsOverAccountAsync", clusterAdminController); // refuses your own account, among others (finding R19)
        Assert.Contains("You can't change your own account here.", File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs")));
        Assert.Contains("href=\"/account\"", layout);
        Assert.Contains("@customerName / @customerRole", layout);
    }

    [Fact]
    public void LocalAdminApiTokenManagement_ShouldBeScopedToAdministeredClusters()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var view = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));

        Assert.Contains("[HttpGet(\"api-tokens\")]", controller);
        Assert.Contains("CreateClusterApiToken", controller);
        Assert.Contains("DeleteClusterApiToken", controller);
        Assert.Contains("RegenerateClusterApiToken", controller);
        Assert.Contains("ValidateClusterTokenScopeAsync", controller);
        Assert.Contains("CanAdministerClusterAsync", controller);
        Assert.Contains("/cluster-admin/api-tokens/create", view);
        Assert.Contains("name=\"accessMode\" value=\"read\"", view); // a cluster token reads, and with write sends live values (step 3e)
        Assert.Contains("name=\"accessMode\" value=\"readWrite\"", view);
        Assert.DoesNotContain("value=\"readWriteSign\"", view);
        Assert.DoesNotContain("value=\"sign\"", view);
        Assert.DoesNotContain("Create, validate, sign and publish passport", view);
        Assert.Contains("\"/cluster-admin/api-tokens\", false, false", view);
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
