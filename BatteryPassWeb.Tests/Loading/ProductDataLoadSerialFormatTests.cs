using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1b: the workbook's serial number format (decision 81) is loaded through S1's own rule and save. A format the
/// rule refuses is a problem line in its words; the check shows the old and new format, counts and names the batteries
/// that wouldn't fit (Load is the yes the model's page asks for) and notes a model with the very same format; the format
/// is never the first model's; none removes it; a second load changes nothing.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadSerialFormatTests(MongoFixture mongo)
{
    private const string Format = "model|SerialFormat";

    private static async Task<BatteryModel> ModelAsync(LoadRig rig, string name) =>
        (await rig.Store.ListAsync<BatteryModel>()).Single(model => model.Name == name);

    [Fact]
    public async Task TheFormatsAsSent_ShouldBeLoaded_EachModelItsOwn_AndAuditedAsTheModelsPageDoes()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core());
        var result = await rig.LoadAsync(Core());

        Assert.True(result.Loaded, result.Message);
        Assert.Contains(new LoadChangeLine("Serial number format", string.Empty, "VPC8C05s-#####"), check.Records.Single(record => record.Name == "Core800s").Changes);
        Assert.Equal("VPC8C05-#####", (await ModelAsync(rig, "Core800")).SerialFormat);
        Assert.Equal("VPC8C05s-#####", (await ModelAsync(rig, "Core800s")).SerialFormat);
        Assert.Equal("VPC7C05-#####", (await ModelAsync(rig, "Core700")).SerialFormat);
        Assert.Equal(3, (await rig.AuditAsync(ProductDataPages.SerialFormatEvent)).Count);
    }

    [Fact]
    public async Task AnEmptyFormatCell_ShouldNotTakeTheFirstModelsFormat()
    {
        var rig = await EmptyAsync(mongo);

        Assert.True((await rig.LoadAsync(Core().Clear(ModelSheet, Format, "Core700"))).Loaded);

        Assert.Null((await ModelAsync(rig, "Core700")).SerialFormat);
    }

    [Theory]
    [InlineData("VPC8C05_#####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05-ABCDE", SerialFormats.NeedsADigitSign)]
    public async Task AFormatTheRuleRefuses_ShouldBeAProblemInTheRulesWords(string format, string sentence)
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core().Set(ModelSheet, Format, "Core800s", format));

        SameLines([$"Models, Serial number format, Core800s: {format}: {sentence}"], check.Texts());
    }

    [Fact]
    public async Task ASecondLoad_ShouldChangeNothing_AndNoneShouldRemoveTheFormat()
    {
        var rig = await EmptyAsync(mongo);
        await rig.LoadAsync(Core());

        var again = await rig.CheckAsync(Core());
        var removed = await rig.LoadAsync(Core().Set(ModelSheet, Format, "Core700", "none"));

        Assert.False(again.HasChanges);
        Assert.True(removed.Loaded, removed.Message);
        Assert.Contains(new LoadChangeLine("Serial number format", "VPC7C05-#####", "(removed)"), removed.Check.Records.Single(record => record.Name == "Core700").Changes);
        Assert.Null((await ModelAsync(rig, "Core700")).SerialFormat);
        Assert.Equal("VPC8C05s-#####", (await ModelAsync(rig, "Core800s")).SerialFormat);
    }

    [Fact]
    public async Task TheVerySameFormat_ShouldBeANote_InTheWorkbookAndInTheApp()
    {
        var rig = await EmptyAsync(mongo);
        await rig.LoadAsync(Core());

        var check = await rig.CheckAsync(Core().Set(ModelSheet, Format, "Core700", "vpc8c05-#####"));

        Assert.Empty(check.Texts());
        var core700 = check.Records.Single(record => record.Name == "Core700");
        Assert.Contains("Core800 has the very same serial number format.", core700.Notes);
        Assert.DoesNotContain("Core800 in this workbook has the very same serial number format.", core700.Notes); // said once
    }

    [Fact]
    public async Task TwoNewModelsWithTheVerySameFormat_ShouldEachHaveTheNote()
    {
        var rig = await EmptyAsync(mongo);

        var check = await rig.CheckAsync(Core().Set(ModelSheet, Format, "Core700", "VPC8C05s-#####"));

        Assert.Empty(check.Texts());
        Assert.Contains("Core700 in this workbook has the very same serial number format.", check.Records.Single(record => record.Name == "Core800s").Notes);
        Assert.Contains("Core800s in this workbook has the very same serial number format.", check.Records.Single(record => record.Name == "Core700").Notes);
    }

    [Fact]
    public async Task BatteriesThatDontFit_ShouldBeCountedAndNamed_AndLoadSavesTheFormatAllTheSame()
    {
        // The seed battery VPC8C05-6788 (four digits) is of model EXB-NMC-100: the format VPC8C05-##### doesn't fit it.
        var rig = Around(await PublicationTesting.RigAsync(mongo));
        var workbook = FilledWorkbook.Empty()
            .Set(FamilySheet, "family|Name", "Core", "Test battery family")
            .Set(ModelSheet, "model|Name", "Core800", "EXB-NMC-100")
            .Clear(ModelSheet, "model|Name", "Core800s")
            .Clear(ModelSheet, "model|Name", "Core700");

        var check = await rig.CheckAsync(workbook, []);
        var result = await rig.LoadAsync(workbook, []);

        Assert.Empty(check.Texts());
        var model = check.Records.Single(record => record.Name == "EXB-NMC-100");
        Assert.Contains(new LoadChangeLine("Serial number format", string.Empty, "VPC8C05-#####"), model.Changes);
        Assert.Contains("1 battery of EXB-NMC-100 doesn't fit the format VPC8C05-#####: VPC8C05-6788. It stays as it is; new IDs, new batteries and ID swaps must fit it. Loading saves the format all the same.", model.Notes);
        Assert.True(result.Loaded, result.Message);
        Assert.Equal("VPC8C05-#####", (await rig.Store.GetAsync<BatteryModel>("model-test"))!.SerialFormat);
    }
}
