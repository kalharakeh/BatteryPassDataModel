namespace BatteryPassWeb.Tests;

public sealed class ExternalApiBatteryCreateTests
{
    [Fact]
    public void AdminBatteryCreate_ShouldUseSharedCreationServiceAndKeepEditableFormHook()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("BatteryCreationService", source);
        Assert.Contains("_batteryCreationService.CreateBatteryAsync", source);
        Assert.Contains("CustomizeBatteryBeforeInsert", source);
        Assert.Contains("ApplyBatteryEditableFormValues(form, editablePolicy, forCreation: true)", source);
        Assert.Contains("ApplyPassportForm(battery, filteredForm, createNow)", source);
        Assert.Contains("new BatteryCreationActor(CurrentActor(), \"admin-ui\", \"admin-ui\")", source);
        Assert.DoesNotContain("await _batteryRepository.CreateBatteryAsync(battery, cancellationToken);", source);
    }

    [Fact]
    public void ExternalApi_ShouldExposeCreateBatteryEndpointWithWriteAccessAndSharedService()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("[HttpPost(\"batteries\")]", source);
        Assert.Contains("CreateBattery(", source);
        Assert.Contains("ExternalTokenRequirement.Write", source);
        Assert.Contains("_batteryCreationService.CreateBatteryAsync", source);
        Assert.Contains("BatteryCreationClusterScope.FromToken(auth.TokenContext!)", source);
        Assert.Contains("createdByTokenId", source);
        Assert.Contains("createPassportPath", source);
    }

    [Fact]
    public void ExternalApiCreateBattery_ShouldParseOnlyExplicitCreationFields()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));

        Assert.Contains("ReadBatteryCreationCommand", source);
        Assert.Contains("ReadString(payload, \"batteryFamily\")", source);
        Assert.Contains("ReadString(payload, \"productId\")", source);
        Assert.Contains("ReadString(payload, \"batteryModel\")", source);
        Assert.Contains("ReadString(payload, \"productVersion\")", source);
        Assert.Contains("ReadString(payload, \"softwareVersion\")", source);
        Assert.Contains("ReadString(payload, \"serialNumber\")", source);
        Assert.Contains("ReadString(payload, \"clusterId\")", source);
        Assert.DoesNotContain("BsonDocument.Parse", source);
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
