namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryCreateTests
{
    [Fact]
    public void AdminBatteryCreate_ShouldUseSharedCreationServiceAndKeepEditableFormHook()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.DoesNotContain("await _batteryRepository.CreateBatteryAsync(battery, cancellationToken);", source);
    }

    [Fact]
    public void ExternalApi_ShouldExposeCreateBatteryEndpointWithWriteAccessAndSharedService()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));

        Assert.Contains("[HttpPost(\"batteries\")]", source);
        Assert.Contains("CreateBattery(", source);
        Assert.Contains("TeamTokenAsync(ExternalTokenRequirement.Write, TeamOnlyCreate", source);
        Assert.Contains("writing.CreateAsync", source);
        Assert.Contains("publishPath", source);
    }

    [Fact]
    public void ExternalApiCreateBattery_ShouldParseOnlyExplicitCreationFields()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));

        Assert.Contains("Text(body, \"batchId\")", source);
        Assert.Contains("Text(body, \"operatorId\")", source);
        Assert.Contains("Text(body, \"clusterId\")", source);
        Assert.Contains("body.TryGetProperty(\"values\"", source);
        Assert.DoesNotContain("batteryFamily\")", source);
        Assert.DoesNotContain("softwareVersion\")", source);
        Assert.DoesNotContain("BsonDocument.Parse", source);
    }

    [Fact]
    public void ExternalApiHelp_ShouldDocumentCreatingADraftBatteryInABatch()
    {
        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("@Model.BasePath/batteries</code><small>Create a draft battery", help);
        Assert.Contains("@Model.BasePath/batches</code>", help);
        Assert.Contains("@Model.BasePath/operators</code>", help);
        Assert.Contains("batterySerialNumber", help);
        Assert.Contains("batchId", help);
        Assert.Contains("operatorId", help);
        Assert.Contains("clusterId", help);
        Assert.Contains("SN-API-REPLACE-ME", help);
        Assert.DoesNotContain("softwareVersion", help);
        Assert.DoesNotContain("passport snapshot", help);
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
