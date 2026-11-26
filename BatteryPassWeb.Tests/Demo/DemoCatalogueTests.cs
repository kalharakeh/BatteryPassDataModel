using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: what the demo data is made of, with the old demo's errors fixed (decision 32).</summary>
public sealed class DemoCatalogueTests
{
    private static readonly DemoCatalogue Catalogue = DemoCatalogue.Load();

    [Fact]
    public void TheCatalogue_ShouldHaveTheOldDemosFamiliesAndModels()
    {
        Assert.Equal(["Compact", "Core"], Catalogue.Families.Select(family => family.Name));
        Assert.Equal([("Compact 7M", "compact", 7), ("Compact 13M", "compact", 13), ("Core", "core", 10)], Catalogue.Models.Select(model => (model.Name, model.Family, model.Modules)));
        Assert.All(Catalogue.Models, model => Assert.False(string.IsNullOrWhiteSpace(model.ModuleDescription)));
    }

    [Theory]
    [InlineData("compact-7m", 400, 320, 460, 320)]
    [InlineData("compact-13m", 720, 576, 828, 565)]
    [InlineData("core", 640, 512, 736, 470)]
    public void EachModel_ShouldHaveItsOwnVoltagesAndMass(string key, int nominal, int minimum, int maximum, int mass)
    {
        var values = Catalogue.Models.Single(model => model.Key == key).Values;

        Assert.Equal(nominal, values["nominalVoltage"]!["voltValue"]!.GetValue<int>());
        Assert.Equal(minimum, values["minimumVoltage"]!["voltValue"]!.GetValue<int>());
        Assert.Equal(maximum, values["maximumVoltage"]!["voltValue"]!.GetValue<int>());
        Assert.Equal(mass, values["batteryMass"]!["gramKilogramValue"]!.GetValue<int>());
    }

    [Fact]
    public void TheCarbonStages_ShouldAddUpToTheFootprint()
    {
        string[] stages = ["contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage", "contributionOfMainProductProductionLifecycleStage", "contributionOfDistributionLifecycleStage", "contributionOfEndOfLifeAndRecyclingLifecycleStage"];
        foreach (var batch in Catalogue.Batches)
        {
            var total = batch.Values["batteryCarbonFootprintPerFunctionalUnit"]!["kgCO2EquivalentPerKilowattHourValue"]!.GetValue<decimal>();
            Assert.Equal(total, stages.Sum(stage => batch.Values[stage]!["kgCO2EquivalentPerKilowattHourValue"]!.GetValue<decimal>()));
        }
    }

    [Fact]
    public void TheHazardousSubstance_ShouldBeARealOneWithItsOwnCasNumber()
    {
        foreach (var model in Catalogue.Models)
        {
            var substance = model.Values["hazardousSubstancesAndTheirImpact"]!.AsArray().Single()!;
            Assert.Equal(("Lithium hexafluorophosphate", "21324-40-3"), (substance["chemicalName"]!.GetValue<string>(), substance["identifier"]!.GetValue<string>()));
        }
    }

    [Fact]
    public void TheText_ShouldHaveNoGarbledCharacters()
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(DppTestFiles.RepoFile("web", "Dpp", "Demo", "demo-catalogue.json")));

        Assert.DoesNotContain("Â", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", text, StringComparison.Ordinal);
        Assert.Contains("°C", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBatteries_ShouldBeInSeveralStates()
    {
        Assert.Equal(["DEMO-C7M-0001", "DEMO-C7M-0003", "DEMO-C13M-0001", "DEMO-CORE-0001", "DEMO-CORE-0003"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
        Assert.Equal(["DEMO-C7M-0001 Repaired", "DEMO-C13M-0001 Repurposed", "DEMO-CORE-0003 Waste"], Catalogue.Packs.Where(pack => pack.Status is not null).Select(pack => $"{pack.Serial} {pack.Status!.What}"));
        Assert.Equal(["DEMO-C7M-0001"], Catalogue.Packs.Where(pack => pack.NewVersion is not null).Select(pack => pack.Serial));
        Assert.Equal(2, Catalogue.Packs.Count(pack => pack.Accident is not null));
        Assert.Equal(2, Catalogue.Packs.Count(pack => !pack.Values.ContainsKey("manufacturingDate")));
        Assert.All(Catalogue.Packs, pack => Assert.False(pack.Values.ContainsKey("batteryStatus"), "the status is set when the battery is made (step 4 changes it)"));
    }

    [Fact]
    public void TheAccounts_ShouldCoverEveryRoleButTheTeamAdmin()
    {
        Assert.Equal(["commission", "liRecycling", "liRepair", "liTreatment", "marketSurveillanceAuthority", "member", "notifiedBody"], Catalogue.Accounts.Select(account => account.Role).Distinct().Order());
        Assert.All(Catalogue.Accounts, account => Assert.EndsWith("@example.test", account.Email, StringComparison.Ordinal));
        Assert.Equal(["demo-fleet", "demo-north", "demo-south"], Catalogue.Clusters.Select(cluster => cluster.Id).Order());
        Assert.Equal(3, Catalogue.Accounts.Count(account => account.ClusterRole == "clusterAdmin"));
    }

    [Fact]
    public void TheHandOverExample_ShouldGoFromOnePublishedClusterBatteryToAnother()
    {
        var handedOver = Assert.Single(Catalogue.Packs, pack => pack.HandOverTo is not null);

        Assert.Equal(("DEMO-CORE-0001", "demo-fleet", "demo-north"), (handedOver.Serial, handedOver.Cluster, handedOver.HandOverTo));
        Assert.True(handedOver.Publish);
        Assert.Contains(Catalogue.Clusters, cluster => cluster.Id == handedOver.HandOverTo);
    }

    [Fact]
    public void ThePictures_ShouldBeTheOldDemosPngFiles()
    {
        foreach (var family in Catalogue.Families)
        {
            Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], DemoCatalogue.Picture(family.Picture).Take(4));
        }
    }

    [Fact]
    public void ADemoDocument_ShouldBeAPdfThatOpens()
    {
        var pdf = Encoding.ASCII.GetString(DemoPdf.Make("Dismantling manual - Compact 7M", "Model document (with notes)."));

        Assert.StartsWith("%PDF-1.7\n", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", pdf, StringComparison.Ordinal);
        Assert.Contains("(Dismantling manual - Compact 7M) Tj", pdf, StringComparison.Ordinal);
        Assert.Contains("(Model document \\(with notes\\).) Tj", pdf, StringComparison.Ordinal);
        var startxref = int.Parse(pdf[(pdf.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref\n", pdf[startxref..], StringComparison.Ordinal);
    }

    [Fact]
    public void TheDemoMark_ShouldBeOnEveryKindOfRecordTheSeedMakes()
    {
        foreach (var type in new[] { typeof(BatteryFamily), typeof(BatteryModel), typeof(ManufacturingSite), typeof(ProductionBatch), typeof(EconomicOperator), typeof(BatteryPack) })
        {
            Assert.Equal(typeof(bool), type.GetProperty("IsDemo")?.PropertyType);
        }

        Assert.Null(new BatteryPassOptions().DemoUsersPassword);
        Assert.Contains("DEMO_USERS_PASSWORD", File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")), StringComparison.Ordinal);
    }
}
