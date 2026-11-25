using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Publication;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R01: each battery model answers three yes/no questions, and a conditional value they decide is needed when the answer
/// is yes. "Not answered" blocks publishing; "no" means not needed. The recycled-content values (#49 to #52) and the
/// extensions E1 to E4 stay as they were: no question, no blocking.
/// </summary>
public sealed class ModelQuestionsTests
{
    private static readonly PublicationChecks Checks = new(DppStoreTesting.Schemas);
    private static readonly int[] Cycling = [71, 72, 74, 82, 83, 84, 85];

    [Fact]
    public void TheQuestions_ShouldDecideTheNineConditionalValuesDueByFeb2027()
    {
        Assert.Equal([19], ModelQuestions.Decides[ModelQuestion.CommercialWarranty]);
        Assert.Equal(Cycling, ModelQuestions.Decides[ModelQuestion.CyclingUse]);
        Assert.Equal([24, 1009], ModelQuestions.Decides[ModelQuestion.CadmiumOrLeadAboveLimit]); // E9, which symbol (R1, O13)
        var decided = ModelQuestions.Decides.Values.SelectMany(numbers => numbers).Order();
        var conditionalDueByFeb2027 = AttributeCatalog.All
            .Where(attribute => attribute.Requirement == Requirement.Conditional && attribute.MandatoryFromFeb2027)
            .Select(attribute => attribute.Number)
            .Order();
        Assert.Equal(conditionalDueByFeb2027, decided);
    }

    [Theory]
    [InlineData(true, Need.Required)]
    [InlineData(false, Need.NotNeeded)]
    [InlineData(null, Need.Unanswered)]
    public void ACyclingValue_ShouldFollowTheAnswer(bool? answer, Need expected)
    {
        var answers = new ModelAnswers(true, answer, true);

        Assert.All(Cycling, number => Assert.Equal(expected, PassportNeeds.NeedOf(AttributeCatalog.ByNumber(number), answers)));
    }

    [Fact]
    public void RecycledContentAndTheExtensions_ShouldStayOptionalWhateverTheAnswers()
    {
        var optional = AttributeCatalog.All.Where(attribute => attribute.Number is >= 49 and <= 52 || AttributeCatalog.TemperatureTimes.Contains(attribute)).ToArray();

        Assert.Equal(8, optional.Length);
        foreach (var answers in new[] { ModelAnswers.NotAnswered, new ModelAnswers(true, true, true), new ModelAnswers(false, false, false) })
        {
            Assert.All(optional, attribute => Assert.Equal(Need.Optional, PassportNeeds.NeedOf(attribute, answers)));
        }
    }

    [Fact]
    public void AModelWithoutAnswers_ShouldBlockPublishingWithOneLine()
    {
        var report = Checks.Check(Seed(), ModelAnswers.NotAnswered);

        Assert.False(report.CanPublish);
        Assert.Equal([PassportNeeds.QuestionsNotAnswered], report.Blocking);
    }

    [Fact]
    public void AModelWithOneQuestionUnanswered_ShouldStillBlock()
    {
        var report = Checks.Check(Seed(), new ModelAnswers(true, true, null));

        Assert.Contains(PassportNeeds.QuestionsNotAnswered, report.Blocking);
    }

    [Fact]
    public void CyclingYesWithoutTheCycleLife_ShouldBlockAndSayWhy()
    {
        var passport = Seed();
        Performance(passport).Remove("expectedLifetimeNumberOfChargeDischargeCycles");

        var report = Checks.Check(passport, DppTestSeed.Answers);

        Assert.False(report.CanPublish);
        Assert.Contains("#82 Expected lifetime - Number of charge-discharge cycles is missing (needed because this model is made for cycling use)", report.Blocking);
    }

    [Fact]
    public void CyclingNo_ShouldPublishWithoutTheSevenCycleValues()
    {
        var passport = Seed();
        foreach (var number in Cycling)
        {
            Performance(passport).Remove(AttributeCatalog.ByNumber(number).Property);
        }

        var report = Checks.Check(passport, DppTestSeed.Answers with { CyclingUse = false });

        Assert.True(report.CanPublish, string.Join("; ", report.Blocking));
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void WarrantyYesWithoutTheWarranty_ShouldBlock()
    {
        var passport = Seed();
        passport["batteryPassportData"]!["identifiersAndProductData"]!.AsObject().Remove("warrantyPeriod");

        var report = Checks.Check(passport, DppTestSeed.Answers);

        Assert.Contains("#19 Warranty period of the battery is missing (needed because this model comes with a commercial warranty)", report.Blocking);
    }

    [Fact]
    public void AValueTheAnswerSaysIsntNeeded_ShouldStillBePublishedWhenFilledIn()
    {
        var passport = Seed();

        var report = Checks.Check(passport, new ModelAnswers(false, false, false));

        Assert.True(report.CanPublish);
        Assert.NotNull(Performance(passport)["expectedLifetimeNumberOfChargeDischargeCycles"]); // the assembler keeps it
    }

    [Theory]
    [InlineData(true, "Needed: this model is made for cycling use.")]
    [InlineData(false, "Not needed: this model isn't made for cycling use.")]
    [InlineData(null, "Needed only if this model is made for cycling use.")]
    public void TheEditorLine_ShouldSayWhetherThisModelNeedsTheValue(bool? cycling, string expected)
    {
        Assert.Equal(expected, PassportNeeds.EditorLine(AttributeCatalog.ByNumber(82), new ModelAnswers(true, cycling, true)));
    }

    [Fact]
    public void RecycledContent_ShouldKeepItsConditionFromTheFile()
    {
        Assert.Equal(AttributeCatalog.ConditionOf(AttributeCatalog.ByNumber(49)), PassportNeeds.EditorLine(AttributeCatalog.ByNumber(49), new ModelAnswers(true, true, true)));
    }

    private static JsonObject Seed()
    {
        var seed = DppTestSeed.Load();
        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    private static JsonObject Performance(JsonObject passport) => passport["batteryPassportData"]!["performanceAndDurability"]!.AsObject();
}
