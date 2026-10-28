using System.Text.Json;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Keeps the catalogue in step with the V3 file (design section 11). Expectations are derived from the file's own
/// wording, and wording the test doesn't recognise fails, so a new file version can't slip through. When the file
/// changes, regenerate the extract with scripts/dpp/extract_longlist.py and run this test.
/// </summary>
public sealed partial class LongListComplianceTests
{
    // Our classifications that the file doesn't state directly (design Appendix A; decisions 11 and 16).
    private static readonly int[] SystemAttributes = [1, 2, 3, 4, 5];
    private static readonly int[] RestatedAttributes = [59, 69, 74, 79, 81];

    private const string PurchaserAccess =
        "- persons with a legitimate interest: o the natural or legal person who has legally purchased the battery or third party on behalf) and - market surveillance authorities";
    private const string AuthoritiesAccess = "Notified bodies, market surveillance authorities and the Commission";

    private static readonly Lazy<IReadOnlyList<FileRow>> Rows = new(ReadRows);

    public static TheoryData<int> FileAttributeNumbers()
    {
        var data = new TheoryData<int>();
        foreach (var row in Rows.Value)
        {
            data.Add(row.No);
        }

        return data;
    }

    [Fact]
    public void Catalogue_ShouldHaveExactlyTheFilesAttributes()
    {
        Assert.Equal(73, Rows.Value.Count);
        Assert.Equal(Rows.Value.Select(r => r.No).Order(), AttributeCatalog.All.Select(a => a.Number).Order());
    }

    [Theory]
    [MemberData(nameof(FileAttributeNumbers))]
    public void Attribute_ShouldMatchTheFile(int number)
    {
        var row = Rows.Value.Single(r => r.No == number);
        var attribute = AttributeCatalog.ByNumber(number);

        Assert.Equal(Normalize(row.Attribute), attribute.Name);
        Assert.Equal(ExpectedAudience(row.Access), attribute.Audience);
        Assert.Equal(ExpectedSharedWith(row.Access), attribute.SharedWith);
        Assert.Equal(ExpectedLevel(number, row.Granularity), attribute.Level);
        Assert.Equal(ExpectedChange(number, row.StaticDynamic, row.UpdateRequirement), attribute.Change);
        Assert.Equal(ExpectedRequirement(row.Oi), attribute.Requirement);
        Assert.Equal(ExpectedFeb2027(row.Feb27), attribute.MandatoryFromFeb2027);
        Assert.Equal(ExpectedUnit(row.Unit), attribute.Unit);
    }

    private static Audience ExpectedAudience(string access)
    {
        var text = Normalize(access);
        if (text == "Public")
        {
            return Audience.Public;
        }

        if (text == AuthoritiesAccess)
        {
            return Audience.Authorities;
        }

        if (text == PurchaserAccess)
        {
            return Audience.Purchaser;
        }

        var lower = text.ToLowerInvariant();
        if (lower.StartsWith("- persons with a legitimate interest:", StringComparison.Ordinal)
            && lower.Contains("legally purchased the battery", StringComparison.Ordinal)
            && lower.Contains("the commission", StringComparison.Ordinal)
            && lower.Contains("market surveillance authorities", StringComparison.Ordinal)
            && ExpectedSharedWith(access) != LegitimateInterestGroups.None)
        {
            return Audience.Shared;
        }

        throw new InvalidDataException($"Unrecognised access wording: {text}");
    }

    private static LegitimateInterestGroups ExpectedSharedWith(string access)
    {
        var lower = Normalize(access).ToLowerInvariant();
        var groups = LegitimateInterestGroups.None;
        if (lower.Contains("battery repair, repurposing and remanufacturing operators", StringComparison.Ordinal))
        {
            groups |= LegitimateInterestGroups.Repair;
        }

        if (lower.Contains("recyclers, battery treatment operators", StringComparison.Ordinal))
        {
            groups |= LegitimateInterestGroups.Recycling;
        }

        if (lower.Contains("authorized treatment facilities", StringComparison.Ordinal))
        {
            groups |= LegitimateInterestGroups.Treatment;
        }

        return groups;
    }

    private static AttributeLevel ExpectedLevel(int number, string granularity)
    {
        var text = Normalize(granularity);
        if (SystemAttributes.Contains(number))
        {
            return AttributeLevel.PassportSystem;
        }

        if (RestatedAttributes.Contains(number))
        {
            // Decision 11: a model value, re-stated per pack at a status change.
            return text == "Battery model"
                ? AttributeLevel.ModelRestatedPerPack
                : throw new InvalidDataException($"#{number}: re-stated attributes must be model-level in the file, found '{text}'");
        }

        return text switch
        {
            "Battery model" => AttributeLevel.Model,
            "Individual battery" => AttributeLevel.Pack,
            "Battery model per calendar year and per manufacturing site, i.e. batch level" => AttributeLevel.Batch,
            "Battery model per manufacturing site" => AttributeLevel.Site,
            _ => throw new InvalidDataException($"#{number}: unrecognised granularity '{text}'"),
        };
    }

    private static ChangeRule ExpectedChange(int number, string staticDynamic, string updateRequirement)
    {
        if (RestatedAttributes.Contains(number))
        {
            return ChangeRule.RestatedOnStatusChange;
        }

        if (number == 2)
        {
            // The file's DIN note on #2 defines the DPP status lifecycle (design 4.2).
            return ChangeRule.Lifecycle;
        }

        var kind = Normalize(staticDynamic);
        var update = Normalize(updateRequirement);
        return (kind, update) switch
        {
            ("Static", "n.a.") => ChangeRule.Fixed,
            ("Static", "After change of service provider") => ChangeRule.IfProviderChanges,
            ("Dynamic", "upon any update of the battery passport") => ChangeRule.OnPublishedOrLiveUpdate,
            ("Dynamic", "upon any update of the battery passport that includes archiving") => ChangeRule.NewPerVersion,
            ("Dynamic", "upon placement on market and battery status change") => ChangeRule.OnStatusChange,
            ("Dynamic", _) when update.StartsWith("depending on individual definition of", StringComparison.Ordinal) => ChangeRule.Live,
            _ => throw new InvalidDataException($"#{number}: unrecognised update rule '{kind}' / '{update}'"),
        };
    }

    private static Requirement ExpectedRequirement(string oi) => Normalize(oi) switch
    {
        "M" => Requirement.Mandatory,
        "(M)" => Requirement.MandatoryEspr,
        "C" => Requirement.Conditional,
        var other => throw new InvalidDataException($"Unrecognised requirement '{other}'"),
    };

    private static bool ExpectedFeb2027(string feb27) => Normalize(feb27) switch
    {
        "yes" => true,
        "no" => false,
        var other => throw new InvalidDataException($"Unrecognised Feb 2027 value '{other}'"),
    };

    private static string? ExpectedUnit(string unit) => Normalize(unit) switch
    {
        "" or "n.a." => null,
        "Ah or mAh (for LMT)" => "Ah", // Other industrial batteries use Ah.
        "g or kg" => "kg", // The app stores kg.
        var other => other,
    };

    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static IReadOnlyList<FileRow> ReadRows()
    {
        var path = DppTestFiles.RepoFile("BatteryPassWeb.Tests", "Dpp", "dpp-v3-longlist-extract.json");
        var extract = JsonSerializer.Deserialize<ExtractFile>(File.ReadAllText(path), JsonSerializerOptions.Web)
            ?? throw new InvalidDataException("The extract is empty.");
        return extract.Rows;
    }

    private sealed record ExtractFile(IReadOnlyList<FileRow> Rows);

    private sealed record FileRow(
        int No,
        string Attribute,
        string Oi,
        string Feb27,
        string Unit,
        string Format,
        string Access,
        string StaticDynamic,
        string UpdateRequirement,
        string Granularity);
}
