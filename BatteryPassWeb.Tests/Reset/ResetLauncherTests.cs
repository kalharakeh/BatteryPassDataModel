using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Reset;

/// <summary>Step 3d-2: the owner's launcher asks for the password, points only at battery_pass_v3 and says where the safety copy is.</summary>
public sealed class ResetLauncherTests
{
    private static readonly string Launcher = File.ReadAllText(DppTestFiles.RepoFile("tools", "V3Reset", "v3-reset.ps1"));

    [Fact]
    public void ThePassword_ShouldBeAskedEachTimeAndKeptOnlyInTheProcess()
    {
        Assert.Contains("Read-Host \"Atlas password for $User\" -AsSecureString", Launcher, StringComparison.Ordinal);
        Assert.Contains("Remove-Item Env:MONGODB_URI, Env:MONGODB_DB", Launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Content", Launcher, StringComparison.Ordinal);
        Assert.DoesNotContain("[Environment]::SetEnvironmentVariable", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDatabase_ShouldOnlyBeBatteryPassV3()
    {
        Assert.Contains("$Database = \"battery_pass_v3\"", Launcher, StringComparison.Ordinal);
        Assert.Contains("$env:MONGODB_DB = $Database", Launcher, StringComparison.Ordinal);
        Assert.Contains("@${ClusterHost}/${Database}?retryWrites=true&w=majority\"", Launcher, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Launcher, "\\$env:MONGODB_DB ="));
    }

    [Fact]
    public void TheApp_ShouldBeClosedFirst()
    {
        Assert.Contains("Get-Process -Name \"BatteryPassWeb\"", Launcher, StringComparison.Ordinal);
        Assert.Contains("Close the Battery Pass app first", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void ARealRun_ShouldNeedRunAndSayWhereTheSafetyCopyIs()
    {
        Assert.Contains("[switch]$Run", Launcher, StringComparison.Ordinal);
        Assert.Contains("reset --decisions $Decisions --run --copy $copy", Launcher, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project $project -- reset --decisions $Decisions\r", Launcher.Replace("\r\n", "\n").Replace("\n", "\r\n"), StringComparison.Ordinal);
        Assert.Contains("The safety copy is in $copy", Launcher, StringComparison.Ordinal);
        Assert.Contains("delete the folder once you're satisfied", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFolders_ShouldBeOutsideTheRepository()
    {
        Assert.Contains("Join-Path $HOME \"battery-pass-v3-review-$stamp\"", Launcher, StringComparison.Ordinal);
        Assert.Contains("Join-Path $HOME \"battery-pass-v3-safety-copy-$stamp\"", Launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReadme_ShouldGiveTheStepsInOrder()
    {
        var readme = File.ReadAllText(DppTestFiles.RepoFile("tools", "V3Reset", "README.md"));

        var steps = new[] { "1. **Review**", "2. **Decide**", "3. **Dry run**", "4. **Reset**", "5. **Check**" }.Select(step => readme.IndexOf(step, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, steps);
        Assert.Equal(steps.Order(), steps);
    }
}
