using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 80 (the owner, 13 Nov 2026): a serial number may hold small letters (Core800s: <c>VPC8C05s-00001</c>);
/// capital and small letters are the same serial number; the battery ID and the passport ID are built from it in
/// capitals, in one function; and an ID is read whichever way its letters are typed.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SmallLetterSerialFormsTests(MongoFixture mongo)
{
    private const string Base = "https://www.sib-scania-eubp.eu";

    private static DunsIdentifierScheme Scheme(DppStore store) =>
        new(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl })), store);

    private static BatteryPack Pack(string serial, bool demo = false)
    {
        var pack = new BatteryPack { Id = $"pack-{Guid.NewGuid():N}", BatchId = "batch-1", OperatorId = "operator-1", IsDemo = demo };
        pack.Values["batterySerialNumber"] = new AttributeValue { Value = JsonValue.Create(serial), GeneratedAtUtc = DateTime.UtcNow, Source = "form" };
        return pack;
    }

    [Theory]
    [InlineData("VPC8C05s-00001", true)]
    [InlineData("vpc8c05s-00001", true)]
    [InlineData("VPC8C05S-00001", true)]
    [InlineData("VPC7C05-00001", true)]
    [InlineData("abc", true)]
    [InlineData("VPC8C05s--00001", false)] // two dashes
    [InlineData("-VPC8C05s-00001", false)]
    [InlineData("VPC8C05s-00001-", false)]
    [InlineData("VPC8C05s 00001", false)]
    [InlineData("VPC8C05ß-00001", false)] // only a to z
    [InlineData("VPC8C05é-00001", false)]
    [InlineData("VPC8C05s_00001", false)]
    [InlineData("", false)]
    public void ASerialNumber_ShouldHoldCapitalAndSmallLettersDigitsAndSingleDashes(string serial, bool allowed) =>
        Assert.Equal(allowed, IdentifierForms.IsSerial(serial));

    [Fact]
    public void TheRuleInWords_ShouldAllowSmallLetters_AndSayTheyCountAsTheSame()
    {
        Assert.Contains("letters A–Z or a–z, digits 0–9 and single dashes", IdentifierForms.SerialRule, StringComparison.Ordinal);
        Assert.Contains("Capital and small letters count as the same", IdentifierForms.SerialRule, StringComparison.Ordinal);
        Assert.DoesNotContain("no lower case", IdentifierForms.SerialRule, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("VPC8C05s-00001", "VPC8C05S-00001", true)]
    [InlineData("vpc8c05s-00001", "VPC8C05S-00001", true)]
    [InlineData("VPC8C05s00001", "VPC8C05S-00001", true)] // the dash doesn't count either, as before (6d-5)
    [InlineData("VPC8C05-00001", "VPC8C05s-00001", false)] // one more letter: another serial number
    [InlineData("VPC7C05-00001", "VPC8C05-00001", false)]
    [InlineData("", "", false)]
    public void TwoSerialNumbers_ShouldBeTheSame_WhenTheirLettersAndDigitsAre(string one, string other, bool same) =>
        Assert.Equal(same, IdentifierForms.SameSerial(one, other));

    [Fact]
    public void TheCaseOfAnIdsLetters_ShouldBeDecidedInOneFunction_Capitals()
    {
        // The owner may still choose to keep the small letter in the ID: that is this one function, and the expected
        // values of the tests named "…InCapitals" in the SmallLetterSerial test files.
        Assert.Equal("VPC8C05S-00001", IdentifierForms.SerialInId("VPC8C05s-00001"));
        Assert.Equal("VPC7C05-00001", IdentifierForms.SerialInId("VPC7C05-00001")); // nothing changes for a serial without a small letter

        // Nothing else in the ID code changes a letter's case: the scheme's file has this function's and the key's (Clean).
        var scheme = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Identifiers", "DunsIdentifierScheme.cs"));
        var pool = File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Identifiers", "BatteryIdPool.cs"));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(scheme, @"\.ToUpperInvariant\(\)").Count);
        Assert.DoesNotContain(".ToUpperInvariant()", pool, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheBatteryIdAndThePassportId_ShouldBeBuiltFromTheSerialInCapitals()
    {
        var scheme = Scheme(new DppStore(mongo.NewContext()));

        var made = await scheme.NewPackIdentifierAsync(Pack("VPC8C05s-00001"));

        Assert.Null(made.Problem);
        Assert.Equal($"{Base}/25S/UN778474481-VPC8C05S-00001", made.Identifier);
        Assert.Equal($"{Base}/dpp/UN778474481-VPC8C05S-00001/v1", scheme.NewPassportIdentifier(made.Identifier!, 1));
        Assert.Matches("^[A-Z0-9-]{1,50}$", IdentifierForms.Short(made.Identifier!)); // ISO/IEC 15459: capitals, digits and the dash
        Assert.Equal(made.Identifier, scheme.ItemFor("VPC8C05S-00001", demo: false).Identifier); // capital and small: one ID
        Assert.Equal(made.Identifier, scheme.ItemFor("vpc8c05s-00001", demo: false).Identifier);
    }

    [Fact]
    public void ATestId_ShouldKeepItsTestMarkInCapitals_WhateverTheSerialsCase()
    {
        var scheme = Scheme(new DppStore(mongo.NewContext()));

        var test = scheme.ItemFor("vpc8c05s-00009", demo: true).Identifier!;

        Assert.Equal($"{Base}/25S/UN778474481-TEST-VPC8C05S-00009", test);
        Assert.True(scheme.IsTestIdentifier(test));
        Assert.True(IdentifierForms.IsTest("un778474481-test-vpc8c05s-00009")); // read as a test ID however it is typed
        Assert.False(IdentifierForms.IsTest($"{Base}/25S/UN778474481-VPC8C05S-00001"));
    }

    [Theory]
    [InlineData("test-0001")]
    [InlineData("Test-0001")]
    [InlineData("tEST0001")]
    public void ARealSerialNumber_ShouldNotStartWithTest_InAnyCase(string serial)
    {
        // In capitals it would make an ID marked TEST, which says "a test battery".
        var made = Scheme(new DppStore(mongo.NewContext())).ItemFor(serial, demo: false);

        Assert.Null(made.Identifier);
        Assert.Equal(DunsIdentifierScheme.StartsWithTest, made.Problem);
    }

    [Theory]
    [InlineData("UN778474481-VPC8C05S-00001")]
    [InlineData("un778474481-vpc8c05s-00001")]
    [InlineData("Un778474481-Vpc8c05s-00001")]
    [InlineData("UN778474481-VPC8C05s-00001")]
    public void AnItemId_ShouldBeRead_WhicheverWayItsLettersAreTyped(string item)
    {
        Assert.True(IdentifierForms.IsItem(item));
        Assert.True(IdentifierForms.IsAppId(item)); // the S01 guard knows it as the app's own
        Assert.True(IdentifierForms.IsDashed(item));
        Assert.Equal(item, IdentifierForms.Short($"{Base}/25S/{item}"));
        Assert.Equal($"{item}/v2", IdentifierForms.Display($"{Base}/dpp/{item}/v2"));
        Assert.True(IdentifierForms.IsWebAddress($"{Base}/25S/{item}"));

        var redirect = Assert.IsType<RedirectResult>(new ItemIdController().Item(item)); // the QR code's address
        Assert.Equal($"/dpp/{item}", redirect.Url);
    }

    [Theory]
    [InlineData("UN778474481-VPC8C05ß-00001")]
    [InlineData("UN778474481-VPC8C05s--00001")]
    [InlineData("UN77847448-VPC8C05s-00001")] // eight digits
    [InlineData("U778474481-VPC8C05s-00001")]
    public void SomethingThatIsNotAnItemId_ShouldStillNotBeRead(string text)
    {
        Assert.False(IdentifierForms.IsItem(text));
        Assert.IsType<NotFoundResult>(new ItemIdController().Item(text));
    }

    [Fact]
    public void TheTakenRefusal_ShouldSayThatCapitalAndSmallLettersAreTheSame() =>
        Assert.Contains("only in their dashes or in capital and small letters", DunsIdentifierScheme.Taken("UN778474481-VPC8C05S-00001", "VPC8C05s-00001"), StringComparison.Ordinal);
}
