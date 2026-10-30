using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Step 3d-1: the old v1.2 app's code is gone; what the new app still uses stays (design 13 and 14).</summary>
[Collection(MongoCollection.Name)]
public sealed class OldAppRemovedTests(MongoFixture mongo)
{
    /// <summary>The repository's root folder, found from the web project.</summary>
    private static string RepoRoot => Path.GetDirectoryName(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs")))!;

    /// <summary>The old pages, services and models; the folder of the v1.2 data model stays as reference.</summary>
    public static TheoryData<string> RemovedFiles() => new(
        "web/Controllers/FilesApiController.cs",
        "web/Services/ProductTemplateService.cs",
        "web/Services/ProductTemplateModels.cs",
        "web/Services/PassportRepository.cs",
        "web/Services/BatteryRepository.cs",
        "web/Services/BatteryPassCanonicalDataCatalog.cs",
        "web/Services/DemoRequiredDataCompletionService.cs",
        "web/Services/EditableFieldPolicyService.cs",
        "web/Services/DataCompletionPolicyService.cs",
        "web/Services/PassportTrustWorkflowService.cs",
        "web/Services/PassportViewModelFactory.cs",
        "web/Services/BatteryIdService.cs",
        "web/Models/Trust/TrustModels.cs",
        "web/Models/ViewModels/PassportViewModel.cs",
        "web/Models/ViewModels/EditPassportViewModel.cs",
        "web/Views/Admin/EditPassport.cshtml",
        "web/Views/Admin/Conformance.cshtml",
        "web/Views/Admin/Audit.cshtml",
        "web/Views/Admin/Revisions.cshtml",
        "web/Views/ClusterAdmin/Passports.cshtml",
        "web/Views/ClusterAdmin/BatteryPassports.cshtml",
        "web/Views/Shared/_BatteryTable.cshtml",
        "web/Views/Home/Search.cshtml",
        "web/wwwroot/images/sample-battery.png");

    [Theory]
    [MemberData(nameof(RemovedFiles))]
    public void TheOldAppsFile_ShouldBeGone(string path)
    {
        Assert.False(File.Exists(Path.Combine([RepoRoot, .. path.Split('/')])), $"{path} should be gone");
    }

    [Fact]
    public void TheV12DataModel_ShouldStayInTheRepositoryAsReferenceButNotBeRead()
    {
        Assert.True(Directory.Exists(Path.Combine(RepoRoot, "BatteryPass", "io.BatteryPass.GeneralProductInformation")));
        foreach (var source in Directory.GetFiles(Path.Combine(RepoRoot, "web"), "*.cs", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("io.BatteryPass.", File.ReadAllText(source), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheApp_ShouldRegisterNoneOfTheOldServices()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        foreach (var service in new[] { "PassportRepository", "BatteryRepository", "ProductTemplateService", "PassportTrustWorkflowService", "BatteryIdService", "EnableDemoData", "ID_GENERATION_SECRET" })
        {
            Assert.DoesNotContain(service, program, StringComparison.Ordinal);
        }

        Assert.Null(typeof(BatteryPassOptions).GetProperty("EnableDemoData"));
        Assert.Null(typeof(BatteryPassOptions).GetProperty("IdGenerationSecret"));
        foreach (var settings in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var text = File.ReadAllText(DppTestFiles.RepoFile("web", settings));
            Assert.DoesNotContain("EnableDemoData", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IdGenerationSecret", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Startup_ShouldGiveEachClusterItsDefaultTokensAndCreateNoSampleData()
    {
        var context = mongo.NewContext();
        var clusters = new ClusterRepository(context);
        var tokens = new ExternalApiRepository(context, new ExternalApiSecurityService("3d1-test-key-for-the-token-hashes-0000"));
        await clusters.UpsertClusterAsync("cluster-north", "Customer North");

        await new ExternalApiInitializer(tokens, clusters).InitializeAsync();
        await new ExternalApiInitializer(tokens, clusters).InitializeAsync();

        var created = await tokens.ListTokensAsync();
        Assert.Equal(["read", "readWrite"], created.Select(token => BsonHelpers.GetString(token, "accessMode")).Order());
        Assert.All(created, token => Assert.Equal("cluster-north", BsonHelpers.GetString(token, "autoClusterId")));
        Assert.DoesNotContain(created, token => token.GetValue("isSample", false).ToBoolean());
        Assert.Equal(["cluster-north"], (await clusters.ListClustersAsync()).Select(cluster => BsonHelpers.GetString(cluster, "clusterId")));
    }

    [Fact]
    public void TheHelpPage_ShouldOfferNoSampleTokens()
    {
        Assert.Equal([typeof(BatteryPassWeb.Dpp.Pages.BatteryPages)], typeof(HelpController).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Null(typeof(BatteryPassWeb.Models.ViewModels.ExternalApiHelpViewModel).GetProperty("SampleReadToken"));
        Assert.DoesNotContain("SAMPLEBATTERYPASSPORT", File.ReadAllText(DppTestFiles.RepoFile("web", "Services", "ExternalApiInitializer.cs")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Index", "/cluster-admin/users")]
    [InlineData("Passports", "/registry")]
    [InlineData("Secrets", "/cluster-admin/api-tokens")]
    public void TheClusterAdminsOldAddresses_ShouldLeadToTheirNewPages(string action, string expected)
    {
        var controller = new ClusterAdminController(
            new AccessControlService(new ClusterRepository(mongo.NewContext())),
            new ApplicationAuditService(mongo.NewContext()),
            new ClusterRepository(mongo.NewContext()),
            new ExternalApiRepository(mongo.NewContext(), new ExternalApiSecurityService("3d1-test-key-for-the-token-hashes-0000")))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
        };

        var result = (IActionResult)typeof(ClusterAdminController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!.Invoke(controller, null)!;

        Assert.Equal(expected, Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public void TheClusterAdminsTabs_ShouldBeTheirUsersAndApiTokens()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));

        Assert.DoesNotContain("Managed passports", tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/users\"", tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/api-tokens\"", tabs, StringComparison.Ordinal);
    }
}
