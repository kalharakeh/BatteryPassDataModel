using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (decisions 3 and 8): the start check on a real start of the app, the built BatteryPassWeb.dll run as its own process
/// in an empty folder with only the settings given here. Production doesn't start with only some of the four EU_REGISTRY_
/// settings, and the message names the missing ones. The process is this test's own and is stopped by its ID.
/// </summary>
public sealed class RegistryTokenStartTests
{
    private static readonly string[] AppPrefixes =
    [
        "EU_REGISTRY_", "MONGODB_", "APP_BASE_URL", "DPP_", "BATTERY_PASS_", "ENABLE_", "DEMO_", "ASPNETCORE_", "DOTNET_ENVIRONMENT",
        "SESSION_", "EXTERNAL_API_", "FIRST_TEAM", "HEALTH", "REQUIRE_HTTPS", "TRUSTED_PROXY", "POWER_AUTOMATE",
    ];

    /// <summary>What a Production site sets anyway, so that the start reaches the EU registry's settings.</summary>
    internal static Dictionary<string, string> Production() => new()
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Production",
        ["MONGODB_DB"] = "r3-start-test",
        ["MONGODB_URI"] = "mongodb://127.0.0.1:9/?serverSelectionTimeoutMS=2000&connectTimeoutMS=2000",
        ["BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64"] = Convert.ToBase64String(ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKey()),
        ["APP_BASE_URL"] = "https://www.example.test/",
        ["DPP_ID_BASE_URL"] = "https://www.example.test",
    };

    [Theory]
    [InlineData("EU_REGISTRY_ISSUER,EU_REGISTRY_JWKS_URL", "Not set: EU_REGISTRY_AUDIENCE, EU_REGISTRY_ORG_ID.")]
    [InlineData("EU_REGISTRY_ORG_ID", "Not set: EU_REGISTRY_ISSUER, EU_REGISTRY_JWKS_URL, EU_REGISTRY_AUDIENCE.")]
    public async Task OnlySomeSettingsInProduction_ShouldStopTheAppsStart_NamingTheMissingOnes(string set, string message)
    {
        var settings = Production();
        var values = new Dictionary<string, string>
        {
            ["EU_REGISTRY_ISSUER"] = RegistryTokenTesting.Issuer,
            ["EU_REGISTRY_JWKS_URL"] = RegistryTokenTesting.JwksUrl,
            ["EU_REGISTRY_AUDIENCE"] = "https://www.example.test",
            ["EU_REGISTRY_ORG_ID"] = RegistryTokenTesting.Organisation,
        };
        foreach (var name in set.Split(','))
        {
            settings[name] = values[name];
        }

        var (exited, output) = await RunAsync(settings, until: null, TimeSpan.FromSeconds(60));

        Assert.True(exited, output);
        Assert.Contains("The EU registry's token check needs all four", output, StringComparison.Ordinal);
        Assert.Contains(message, output, StringComparison.Ordinal);
        Assert.DoesNotContain(RegistryTokenTesting.Organisation, output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs the app with these settings only, until it exits, its output holds <paramref name="until"/>, or the time is up;
    /// then stops it by its own process ID. Gives whether it exited by itself, and everything it wrote.
    /// </summary>
    internal static async Task<(bool Exited, string Output)> RunAsync(Dictionary<string, string> settings, string? until, TimeSpan limit)
    {
        var folder = Directory.CreateTempSubdirectory("r3-start-");
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = folder.FullName, // no .env.local here
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "BatteryPassWeb.dll"));
        info.ArgumentList.Add("--urls");
        info.ArgumentList.Add("http://127.0.0.1:0");
        foreach (var name in info.Environment.Keys.Where(name => AppPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            info.Environment.Remove(name);
        }

        foreach (var (name, value) in settings)
        {
            info.Environment[name] = value;
        }

        var output = new StringBuilder();
        var seen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Take(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (output)
            {
                output.AppendLine(line);
                if (until is not null && output.ToString().Contains(until, StringComparison.Ordinal))
                {
                    seen.TrySetResult();
                }
            }
        }

        using var process = new Process { StartInfo = info };
        process.OutputDataReceived += (_, line) => Take(line.Data);
        process.ErrorDataReceived += (_, line) => Take(line.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var exited = process.WaitForExitAsync();
        await Task.WhenAny(exited, seen.Task, Task.Delay(limit));
        var byItself = process.HasExited;
        if (!byItself)
        {
            process.Kill(entireProcessTree: true); // this test's own process only
            await process.WaitForExitAsync();
        }
        else
        {
            await exited; // all output read
        }

        try
        {
            folder.Delete(recursive: true);
        }
        catch (IOException)
        {
        }

        lock (output)
        {
            return (byItself, output.ToString());
        }
    }
}
