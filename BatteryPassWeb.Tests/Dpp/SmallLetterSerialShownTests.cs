using System.IO.Compression;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using Microsoft.Extensions.DependencyInjection;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 80, what people read: the serial number is kept and shown as it was released, small s included, in the
/// passport (#11), the lists, the Excel export and the registry's CSV, and the IDs carry it the same way.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SmallLetterSerialShownTests(MongoFixture mongo)
{
    [Fact]
    public async Task ThePassportTheListsAndTheFiles_ShouldShowTheSerialAsReleased_InTheIdsToo()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var battery = await PublishedAsync(rig, c7);

        // The signed passport: #11 as released, and #6 and #5 with the small s too.
        var published = (await rig.Services.GetRequiredService<PassportReader>().CurrentAsync(battery.Id))!.Passport.ToJsonString();
        Assert.Contains($"\"batterySerialNumber\":\"{Serial}\"", published, StringComparison.Ordinal);
        Assert.Contains($"\"uniqueProductIdentifier\":\"{Base}/25S/{Item}\"", published, StringComparison.Ordinal);
        Assert.Contains($"\"digitalProductPassportId\":\"{Base}/dpp/{Item}/v1\"", published, StringComparison.Ordinal);
        Assert.DoesNotContain("VPC8C05S", published, StringComparison.Ordinal); // nowhere in capitals: one form

        // The team's list and its row.
        var row = Assert.Single((await rig.Services.GetRequiredService<BatteryPages>().ListAsync("VPC8C05")).Rows);
        Assert.Equal((Serial, $"{Base}/25S/{Item}"), (row.SerialNumber, row.Identifier));

        // The released IDs' Excel file, for the labels.
        var items = await pool.ListAsync(new ReleasedIdFilter(Query: "vpc8c05s"));
        using var zip = new ZipArchive(new MemoryStream(BatteryIdPool.Export(items, new Dictionary<string, string>())));
        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
        var cells = Regex.Matches(sheet, "<t xml:space=\"preserve\">(.*?)</t>").Select(cell => cell.Groups[1].Value).ToArray();
        Assert.Contains(Serial, cells);
        Assert.Contains(Item, cells);
        Assert.Contains($"{Base}/25S/{Item}", cells);
        Assert.DoesNotContain("VPC8C05S-00001", cells);

        // The CSV for the EU registry names the battery by its ID, in its one form.
        var csv = BatteryPages.RegistryCsv(await rig.Services.GetRequiredService<BatteryPages>().NotInTheRegistryAsync());
        Assert.Contains($"{Base}/25S/{Item}", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("VPC8C05S", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHelp_ShouldSayWhatASerialNumberMayHold_AndThatCapitalAndSmallLettersAreTheSame()
    {
        var guide = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Guide.cshtml"));
        var form = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "BatteryIds", "Index.cshtml"));

        Assert.Contains("A serial number holds letters, digits and single dashes only, at most 34 characters.", guide, StringComparison.Ordinal);
        Assert.Contains("Capital and small letters count as the same: VPC8C05s-00001 and VPC8C05S-00001 are one serial number.", guide, StringComparison.Ordinal);
        Assert.Contains("It is kept and shown as you typed it, in the battery ID and the passport ID too (UN778474481-VPC8C05s-00001); an address typed in other capitals opens the same passport.", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("has it in capitals", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("capital letters, digits and single dashes only", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Letters, digits and single dashes only. Capital and small letters count as the same.", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Capital letters, digits and single dashes only.", form, StringComparison.Ordinal);
    }
}
