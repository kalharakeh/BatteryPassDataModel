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
