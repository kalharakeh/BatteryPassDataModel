using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 81 (the owner, 13 Nov 2026): a model may have a serial number format, letters, digits and dashes as they
/// must appear and <c>#</c> for a digit (<c>VPC8C05s-#####</c>). A format is exact about its letters' places, not their
/// case. The owner's three: Core800 <c>VPC8C05-#####</c>, Core800s <c>VPC8C05s-#####</c>, Core700 <c>VPC7C05-#####</c>.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SerialFormatTests(MongoFixture mongo)
{
    private const string Core800 = "VPC8C05-#####";
    private const string Core800s = "VPC8C05s-#####";
    private const string Core700 = "VPC7C05-#####";

    private DunsIdentifierScheme Scheme() =>
        new(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl })), new DppStore(mongo.NewContext()));

    [Theory]
    [InlineData(Core800, "VPC8C05-00001", true)]
    [InlineData(Core800, "vpc8c05-00001", true)] // letters without regard to case, like serial numbers
    [InlineData(Core800, "VPC8C05s-00001", false)] // the mix-up most likely on the line: one more letter
    [InlineData(Core800, "VPC8C05S-00001", false)]
    [InlineData(Core800, "VPC7C05-00001", false)]
    [InlineData(Core800, "VPC8C05-0001", false)] // four digits
    [InlineData(Core800, "VPC8C05-000001", false)]
    [InlineData(Core800, "VPC8C05-0000A", false)] // # is a digit
    [InlineData(Core800, "VPC8C0500001", false)] // the dash must be where the format has it
    [InlineData(Core800s, "VPC8C05s-00001", true)]
    [InlineData(Core800s, "VPC8C05S-00001", true)]
    [InlineData(Core800s, "VPC8C05-00001", false)]
    [InlineData(Core800s, "VPC7C05-00001", false)]
    [InlineData(Core700, "VPC7C05-00001", true)]
    [InlineData(Core700, "VPC8C05-00001", false)]
    [InlineData("vpc8c05S-#####", "VPC8C05s-00042", true)] // a format's own letters, in any case
    [InlineData("AB-##-C#", "ab-12-c3", true)]
    [InlineData("AB-##-C#", "ab-12-3c", false)]
    [InlineData(null, "ANYTHING-1", true)] // a model without a format accepts anything
    [InlineData("", "ANYTHING-1", true)]
    [InlineData(Core800, "", false)]
    [InlineData(Core800, null, false)]
    public void ASerialNumber_ShouldFitAFormat_WhenItsLettersDigitsAndDashesAreWhereTheFormatHasThem(string? format, string? serial, bool fits) =>
        Assert.Equal(fits, SerialFormats.Fits(format, serial));

    [Theory]
    [InlineData(Core800)]
    [InlineData(Core800s)]
    [InlineData(Core700)]
    [InlineData("#")]
    [InlineData("A-#-b-9")]
    [InlineData("A234567890-234567890-234567890-###")] // 34 characters: the longest serial number
    public void AFormat_ShouldBeAccepted_WithLettersDigitsSingleDashesAndAtLeastOneDigitSign(string format) =>
        Assert.Null(SerialFormats.Problem(format, Scheme()));

    [Theory]
    [InlineData("VPC8C05-00001", SerialFormats.NeedsADigitSign)] // no #: that is one serial number, not a format
    [InlineData("VPC8C05", SerialFormats.NeedsADigitSign)]
    [InlineData("VPC8C05 #####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05--#####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("-VPC8C05-#####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05-#####-", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05_#####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05-\\d{5}", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05ß-#####", SerialFormats.OnlyTheseCharacters)]
    [InlineData("VPC8C05-?????", SerialFormats.OnlyTheseCharacters)]
    public void AFormat_ShouldBeRefused_WithOtherCharactersOrWithoutADigitSign(string format, string problem) =>
        Assert.Equal(problem, SerialFormats.Problem(format, Scheme()));

    [Fact]
    public void AFormat_ShouldStayWithinWhatASerialNumberMayBe_AsTheIdSchemeSaysIt()
    {
        // The limit is the ID scheme's own (34 characters with the owner's settings), not a number written here.
        var tooLong = SerialFormats.Problem("A234567890-234567890-234567890-####", Scheme());
        var test = SerialFormats.Problem("TEST-####", Scheme());

        Assert.Equal($"a serial number of this format can't make a battery ID: {DunsIdentifierScheme.TooLong(34)}", tooLong);
        Assert.Equal($"a serial number of this format can't make a battery ID: {DunsIdentifierScheme.StartsWithTest}", test);
    }

    [Theory]
    [InlineData(Core800s, "VPC8C05s-00001")]
    [InlineData(Core700, "VPC7C05-00001")]
    [InlineData("#", "1")]
    [InlineData("AB-##-C#", "AB-00-C1")]
    public void TheExample_ShouldBeASerialNumberThatFits(string format, string example)
    {
        Assert.Equal(example, SerialFormats.Example(format));
        Assert.True(SerialFormats.Fits(format, example));
        Assert.True(IdentifierForms.IsSerial(example));
    }

    [Theory]
    [InlineData(" VPC8C05s-##### ", Core800s)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ATypedFormat_ShouldBeTrimmed_AndNothingTypedMeansNoFormat(string? typed, string? format) =>
        Assert.Equal(format, SerialFormats.Read(typed));

    [Fact]
    public void TwoFormats_ShouldBeTheSame_WhateverTheCaseOfTheirLetters()
    {
        Assert.True(SerialFormats.Same("VPC8C05s-#####", "vpc8c05S-#####"));
        Assert.False(SerialFormats.Same(Core800, Core800s)); // one more letter: different formats
        Assert.False(SerialFormats.Same(Core800, null));
        Assert.False(SerialFormats.Same(null, null));
    }

    [Fact]
    public void TheRefusal_ShouldNameTheModelAndItsFormat_InAPlainSentence() =>
        Assert.Equal(
            "serial number VPC7C05-00001 doesn't fit the serial number format of model Core800, VPC8C05-#####",
            SerialFormats.Misfit("VPC7C05-00001", "Core800", Core800));
}
