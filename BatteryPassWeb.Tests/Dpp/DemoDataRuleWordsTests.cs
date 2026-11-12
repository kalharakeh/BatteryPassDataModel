using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 69: a real battery ID refused on demo product data is said so that a person understands it the first time,
/// with the way out. The same sentence answers wherever the rule does (creating, publishing, the API).
/// </summary>
public sealed class DemoDataRuleWordsTests
{
    [Fact]
    public void TheRefusal_ShouldSayWhatIsDemo_AndTheWayOut() =>
        Assert.Equal(
            "The serial number is a real battery ID, but the batch, its model or the economic operator you chose is demo data, and real battery IDs go only on real product data. Choose a test ID instead, or make real product data with \"Copy as real product data\" on the battery family's page (and \"Copy as real\" for a demo operator under Sites and operators).",
            DemoDataRule.Message);

    private const string Family = "make real product data with \"Copy as real product data\" on the battery family's page";

    [Theory]
    [InlineData(true, false, false, "the batch you chose is", Family + ".")]
    [InlineData(false, true, false, "the batch's model you chose is", Family + ".")]
    [InlineData(false, false, true, "the economic operator you chose is", "copy the operator as real with \"Copy as real\" under Sites and operators.")]
    [InlineData(true, true, false, "the batch and its model you chose are", Family + ".")]
    [InlineData(true, true, true, "the batch, its model and the economic operator you chose are", Family + " and \"Copy as real\" for the operator under Sites and operators.")]
    [InlineData(false, true, true, "the batch's model and the economic operator you chose are", Family + " and \"Copy as real\" for the operator under Sites and operators.")]
    public void TheRefusal_ShouldNameEachRecordThatIsDemo_AndItsWayOut(bool batch, bool model, bool economicOperator, string which, string wayOut) =>
        Assert.Equal(
            $"The serial number is a real battery ID, but {which} demo data, and real battery IDs go only on real product data. Choose a test ID instead, or {wayOut}",
            DemoDataRule.MessageFor(
                new ProductionBatch { Id = "b", ModelId = "m", SiteId = "s", Year = 2026, IsDemo = batch },
                new BatteryModel { Id = "m", FamilyId = "f", Name = "M", IsDemo = model },
                new EconomicOperator { Id = "o", Name = "O", IsDemo = economicOperator }));

    [Fact]
    public void TheOldSentence_ShouldBeGone() =>
        Assert.DoesNotContain("Use a demo ID for it", DemoDataRule.Message, StringComparison.Ordinal);
}
