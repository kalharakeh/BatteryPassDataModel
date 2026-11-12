using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Tests.Demo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Step 6d, the battery ID the product owner chose (O-1 and 6d, 5 Nov 2026): an ISO/IEC 15459 item ID under issuing
/// agency UN (DUNS 778474481), one dash and the battery's serial number as written (6d-5: capital letters, digits and
/// single dashes), as a web address (ISO/IEC 18975, data identifier 25S) under https://www.sib-scania-eubp.eu; made when
/// the battery is created, a successor adding -R1, -R2, …; a demo battery's marked -TEST-. A version's passport ID is
/// /dpp/&lt;item&gt;/v&lt;n&gt;. IDs made before 6d-5 (UN778474481VPC12221) stay as they are and are still read.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class DunsIdentifierTests(MongoFixture mongo)
{
    private const string Base = "https://www.sib-scania-eubp.eu";

    private static BatteryPassOptions Settings() => DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl });

    private static DunsIdentifierScheme Scheme(DppStore store) => new(Options.Create(Settings()), store);

    private static BatteryPack Pack(string serial, bool demo = false, string? successorOf = null, string? identifier = null)
    {
        var pack = new BatteryPack { Id = $"pack-{Guid.NewGuid():N}", BatchId = "batch-1", OperatorId = "operator-1", IsDemo = demo, SuccessorOf = successorOf };
        pack.Values["batterySerialNumber"] = new AttributeValue { Value = JsonValue.Create(serial), GeneratedAtUtc = DateTime.UtcNow, Source = "form" };
        if (identifier is not null)
        {
            pack.Values["uniqueProductIdentifier"] = new AttributeValue { Value = JsonValue.Create(identifier), GeneratedAtUtc = DateTime.UtcNow, Source = "system" };
        }

        return pack;
    }

    [Fact]
    public async Task ABatteryId_ShouldBeTheAgencyTheDunsADashAndTheSerialAsWritten_AsAWebAddress()
    {
        var scheme = Scheme(new DppStore(mongo.NewContext()));

        var made = await scheme.NewPackIdentifierAsync(Pack("VBC-92921"));

        Assert.Equal($"{Base}/25S/UN778474481-VBC-92921", made.Identifier);
        Assert.Null(made.Problem);
        Assert.False(scheme.IsTestIdentifier(made.Identifier!));
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(made.Identifier)));
        Assert.Equal($"{Base}/dpp/UN778474481-VBC-92921/v1", scheme.NewPassportIdentifier(made.Identifier!, 1));
        Assert.Equal($"{Base}/25S/UN778474481-VBC92921", (await scheme.NewPackIdentifierAsync(Pack("VBC92921"))).Identifier); // no other battery has it yet
    }

    [Fact]
    public async Task ADemoBatteryId_ShouldBeMarkedTest()
    {
        var scheme = Scheme(new DppStore(mongo.NewContext()));

        var demo = (await scheme.NewPackIdentifierAsync(Pack("DEMO-C7M-0001", demo: true))).Identifier!;

        Assert.Equal($"{Base}/25S/UN778474481-TEST-DEMO-C7M-0001", demo);
        Assert.True(scheme.IsTestIdentifier(demo));
        Assert.True(scheme.IsTestIdentifier($"{Base}/dpp/UN778474481-TEST-DEMO-C7M-0001-R1/v2"));
        Assert.Matches("^[A-Z0-9-]{1,50}$", IdentifierForms.Short(demo)); // ISO/IEC 15459: A–Z, 0–9 and the dash, at most 50
    }

    [Theory]
    [InlineData("vbc-92921", false, IdentifierForms.SerialRule)] // never upper-cased silently
    [InlineData("VBC 92921", false, IdentifierForms.SerialRule)]
    [InlineData("-VBC-92921", false, IdentifierForms.SerialRule)]
    [InlineData("VBC-92921-", false, IdentifierForms.SerialRule)]
    [InlineData("VBC--92921", false, IdentifierForms.SerialRule)]
    [InlineData("VBC_92921", false, IdentifierForms.SerialRule)]
    [InlineData("VBC.92921", false, IdentifierForms.SerialRule)]
    [InlineData("VBC/92921", false, IdentifierForms.SerialRule)]
    [InlineData("ÄBC-92921", false, IdentifierForms.SerialRule)]
    [InlineData("---", false, IdentifierForms.SerialRule)]
    [InlineData("", false, IdentifierForms.SerialRule)]
    [InlineData("TEST-0001", false, DunsIdentifierScheme.StartsWithTest)]
    [InlineData("A234567890-234567890-234567890-2345", false, "at most 34 characters")] // 35: no room for -R99 within 50
    [InlineData("A234567890-234567890-234567890", true, "at most 29 characters")] // a demo's TEST- takes 5
    public async Task ASerialThatCantMakeAValidId_ShouldBeRefused(string serial, bool demo, string problem)
    {
        var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(serial, demo));

        Assert.Null(made.Identifier);
        Assert.Contains(problem, made.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLongestSerial_ShouldStillLeaveRoomForASuccessorsR99()
    {
        var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(new string('A', 34)));

        Assert.Equal(46, IdentifierForms.Short(made.Identifier!).Length);
        Assert.True(IdentifierForms.IsItem(IdentifierForms.Short(made.Identifier!) + "-R99"));
        Assert.False(IdentifierForms.IsItem("UN778474481-" + new string('A', 39)));
    }

    [Fact]
    public async Task AnIdAnotherBatteryHas_ShouldBeRefused_NamingThatBattery_EvenWhenOnlyTheDashesDiffer()
    {
        var store = new DppStore(mongo.NewContext());
        await store.InsertAsync(Pack("VBC-92921", identifier: $"{Base}/25S/UN778474481-VBC-92921"));
        await store.InsertAsync(Pack("VPC-12221", identifier: $"{Base}/25S/UN778474481VPC12221")); // an ID made before 6d-5
        var scheme = Scheme(store);

        var nearDuplicate = await scheme.NewPackIdentifierAsync(Pack("VBC92921"));
        var oldForm = await scheme.NewPackIdentifierAsync(Pack("VPC-12221"));

        Assert.Null(nearDuplicate.Identifier);
        Assert.Equal(DunsIdentifierScheme.Taken("UN778474481-VBC-92921", "VBC-92921"), nearDuplicate.Problem);
        Assert.Equal(DunsIdentifierScheme.Taken("UN778474481VPC12221", "VPC-12221"), oldForm.Problem);
        Assert.Equal($"{Base}/25S/UN778474481-BC-92921", (await scheme.NewPackIdentifierAsync(Pack("BC-92921"))).Identifier); // only the whole ID counts
    }

    [Fact]
    public async Task ASuccessor_ShouldGetTheNextFreeRNumber()
    {
        var store = new DppStore(mongo.NewContext());
        var first = Pack("VPC-77001", identifier: $"{Base}/25S/UN778474481-VPC-77001");
        await store.InsertAsync(first);
        var scheme = Scheme(store);

        var r1 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77001", successorOf: first.Id))).Identifier!;
        var second = Pack("VPC-77001", successorOf: first.Id, identifier: r1);
        await store.InsertAsync(second);
        var r2 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77001", successorOf: second.Id))).Identifier!;

        Assert.Equal($"{Base}/25S/UN778474481-VPC-77001-R1", r1);
        Assert.Equal($"{Base}/25S/UN778474481-VPC-77001-R2", r2);
    }

    [Fact]
    public async Task ASuccessorOfABatteryWithAnIdFromBefore65_ShouldContinueThatId()
    {
        // Old records stay as they are (the owner, 6d-5): the new passport continues the first one's ID, whatever its serial.
        var store = new DppStore(mongo.NewContext());
        var old = Pack("VPC-77002", identifier: $"{Base}/25S/UN778474481VPC77002");
        var loose = Pack("vpc 77003", identifier: $"{Base}/25S/UN778474481VPC77003"); // a serial the rule now refuses
        await store.InsertAsync(old);
        await store.InsertAsync(loose);
        var scheme = Scheme(store);

        var r1 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77002", successorOf: old.Id))).Identifier!;
        var second = Pack("VPC-77002", successorOf: old.Id, identifier: r1);
        await store.InsertAsync(second);
        var r2 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77002", successorOf: second.Id))).Identifier!;
        var looseR1 = await scheme.NewPackIdentifierAsync(Pack("vpc 77003", successorOf: loose.Id));

        Assert.Equal($"{Base}/25S/UN778474481VPC77002R1", r1);
        Assert.Equal($"{Base}/25S/UN778474481VPC77002R2", r2);
        Assert.Equal(($"{Base}/25S/UN778474481VPC77003R1", (string?)null), (looseR1.Identifier, looseR1.Problem));
    }

    [Fact]
    public void APassportId_ShouldNameTheBatteryAndTheVersion()
    {
        var scheme = Scheme(new DppStore(mongo.NewContext()));

        var passport = scheme.NewPassportIdentifier($"{Base}/25S/UN778474481-VBC-92921-R1", 2);

        Assert.Equal($"{Base}/dpp/UN778474481-VBC-92921-R1/v2", passport);
        Assert.False(scheme.IsTestIdentifier(passport));
        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(passport)));
    }

    [Theory]
    [InlineData("UN", "778474481", Base, true)]
    [InlineData("", "778474481", Base, false)]
    [InlineData("UN", "", Base, false)]
    [InlineData("UN", "778474481", "", false)]
    [InlineData("UN", "77847448", Base, false)]
    [InlineData("UN", "778474481", "http://www.sib-scania-eubp.eu", false)]
    public void TheScheme_ShouldBeDunsOnlyWithAllThreeSettings(string agency, string company, string baseUrl, bool duns) =>
        Assert.Equal(duns, IdentifierForms.IsConfigured(new BatteryPassOptions { IdAgency = agency, IdCompany = company, IdBaseUrl = baseUrl }));

    [Fact]
    public void ThePagesAddresses_ShouldUseTheShortItem()
    {
        Assert.Equal("/dpp/UN778474481-VBC-92921", PassportPages.SummaryHref($"{Base}/25S/UN778474481-VBC-92921"));
        Assert.Equal("/dpp/UN778474481-VBC-92921-R1/v2/detail", PassportPages.DetailHref($"{Base}/dpp/UN778474481-VBC-92921-R1/v2"));
        Assert.Equal("/qr/UN778474481-VBC-92921/svg", PassportPages.QrHref($"{Base}/25S/UN778474481-VBC-92921"));
        Assert.Equal("UN778474481-VBC-92921", BatteryPages.ShortId($"{Base}/25S/UN778474481-VBC-92921"));
        // IDs made before 6d-5 keep theirs.
        Assert.Equal("/dpp/UN778474481VPC12221", PassportPages.SummaryHref($"{Base}/25S/UN778474481VPC12221"));
        Assert.Equal("/dpp/UN778474481VPC12221/detail", PassportPages.DetailHref($"{Base}/25S/UN778474481VPC12221"));
        Assert.Equal("/dpp/UN778474481VPC12221R1/v2", PassportPages.SummaryHref($"{Base}/dpp/UN778474481VPC12221R1/v2"));
        Assert.Equal("/dpp/UN778474481VPC12221/v2/detail", PassportPages.DetailHref($"{Base}/dpp/UN778474481VPC12221/v2"));
        Assert.Equal("/qr/UN778474481VPC12221/svg", PassportPages.QrHref($"{Base}/25S/UN778474481VPC12221"));
        Assert.Equal("UN778474481VPC12221", BatteryPages.ShortId($"{Base}/25S/UN778474481VPC12221"));
        // Test IDs keep their old addresses.
        Assert.Equal("/dpp/urn:x-battery-pass:TEST-0f8c", PassportPages.SummaryHref("urn:x-battery-pass:TEST-0f8c"));
    }

    [Fact]
    public void TheQrCode_ShouldCarryTheBatteryIdsOwnAddressOnTheAppsSite()
    {
        // On the live site APP_BASE_URL is the ID's domain, so the QR carries #6 itself.
        var live = new DppLinks(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = $"{Base}/" })));
        var pc = new DppLinks(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = "http://localhost:5186" })));

        Assert.Equal($"{Base}/25S/UN778474481-VBC-92921", live.PassportUrl($"{Base}/25S/UN778474481-VBC-92921"));
        Assert.Equal($"{Base}/25S/UN778474481VPC12221", live.PassportUrl($"{Base}/25S/UN778474481VPC12221")); // made before 6d-5
        Assert.Equal("http://localhost:5186/25S/UN778474481VPC12221", pc.PassportUrl($"{Base}/25S/UN778474481VPC12221"));
        Assert.Equal("http://localhost:5186/dpp/urn:x-battery-pass:TEST-0f8c", pc.PassportUrl("urn:x-battery-pass:TEST-0f8c"));
    }

    [Fact]
    public void TheItemsAddress_ShouldOpenItsPublicPage()
    {
        var item = new ItemIdController();

        Assert.Equal("/dpp/UN778474481-VBC-92921", Assert.IsType<RedirectResult>(item.Item("UN778474481-VBC-92921")).Url);
        Assert.Equal("/dpp/UN778474481VPC12221", Assert.IsType<RedirectResult>(item.Item("UN778474481VPC12221")).Url); // made before 6d-5
        Assert.IsType<NotFoundResult>(item.Item("not-an-id"));
        Assert.IsType<NotFoundResult>(item.Item("UN778474481-VBC--92921"));
        Assert.Contains("\"25S\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs")), StringComparison.Ordinal); // a scanned address is searched by its item
    }

    [Fact]
    public async Task AWebAddressIdInAnAddress_ShouldFindItsBattery_OnThePagesAndInTheApi()
    {
        // Found by the role check (6d-3): an API client puts the battery ID in the address escaped, and the route keeps its
        // slashes encoded (%2F), so the lookups decode it once.
        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var encoded = Uri.EscapeDataString($"{Base}/25S/UN778474481-TEST-DEMO-C7M-0001").Replace("%3A", ":", StringComparison.Ordinal);
        var encodedPassport = Uri.EscapeDataString((await rig.Services.GetRequiredService<PassportPages>().FindAsync("DEMO-C7M-0001"))!.DigitalProductPassportId);

        var page = await rig.Services.GetRequiredService<PassportPages>().FindAsync(encoded);
        var byPassport = await rig.Services.GetRequiredService<PassportPages>().FindAsync(encodedPassport);
        var write = await rig.Services.GetRequiredService<BatteryPassWeb.Dpp.Api.BatteryApi>().FindAsync(ChangeSender.Team(DemoRig.Who), encoded);

        Assert.Contains("%2F", encoded, StringComparison.Ordinal);
        Assert.NotNull(page);
        Assert.Equal(page.Id, byPassport!.Id);
        Assert.Equal(page.PackId, write.Pack!.Id);
    }

    [Fact]
    public void ALongIdValue_ShouldWrapOnAPhone_RatherThanScrollThePage() =>
        Assert.Contains(".bp-field-grid dd {\n  min-width: 0;\n  overflow-wrap: anywhere;\n}", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-status.css")).Replace("\r\n", "\n"), StringComparison.Ordinal);

    [Fact]
    public void ALongRefusal_ShouldWrapInTheProblemsTable_RatherThanWidenThePage() => // the serial rule's words (6d-5)
        Assert.Contains(".bp-problems-table td:last-child {\n  min-width: 16rem;\n  white-space: normal;\n}", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-battery-ids.css")).Replace("\r\n", "\n"), StringComparison.Ordinal);

    [Fact]
    public async Task TheDemoData_ShouldGetTestIds_AndSuccessorsRNumbers()
    {
        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var pages = rig.Services.GetRequiredService<PassportPages>();
        Assert.IsType<DunsIdentifierScheme>(rig.Services.GetRequiredService<IIdentifierScheme>());

        var packs = await rig.All(DppStore.Packs);
        Assert.All(packs, pack =>
        {
            var values = pack["Values"].AsBsonDocument;
            var serial = values["batterySerialNumber"]["Value"].AsString; // as written: the demo's are in the rule's form
            Assert.Matches($"^{Regex.Escape(Base)}/25S/UN778474481-TEST-{Regex.Escape(serial)}(-R[0-9]+)?$", values["uniqueProductIdentifier"]["Value"].AsString);
        });
        var identifiers = packs.Select(pack => pack["Values"]["uniqueProductIdentifier"]["Value"].AsString).ToArray();
        Assert.Contains(identifiers, id => id.EndsWith("-TEST-DEMO-C13M-0001-R1", StringComparison.Ordinal)); // DEMO-C13M-0001's second passport
        Assert.Contains(identifiers, id => id.EndsWith("-TEST-DEMO-C13M-0001-R2", StringComparison.Ordinal)); // and its third

        // A battery is found by its short item, its page by its version's address.
        var c7 = (await pages.FindAsync("DEMO-C7M-0001"))!;
        Assert.Equal(c7.PackId, (await pages.FindAsync("UN778474481-TEST-DEMO-C7M-0001"))!.PackId);
        Assert.Equal(c7.PackId, (await pages.FindAsync($"{Base}/dpp/UN778474481-TEST-DEMO-C7M-0001/v{c7.Number}"))!.PackId);
        Assert.Equal($"{Base}/dpp/UN778474481-TEST-DEMO-C7M-0001/v{c7.Number}", c7.DigitalProductPassportId);
    }

    [Fact]
    public async Task ABatteryWhoseIdIsTaken_ShouldNotBeCreated_NamingTheOtherBattery()
    {
        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
        var team = ChangeSender.Team(DemoRig.Who);
        var before = await rig.Count(DppStore.Packs);

        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(team, new NewPack(
            c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "DEMOC7M0001" }, IsDemo: true));

        Assert.Null(result.Pack);
        Assert.Equal($"{PackCreationService.CantGetId}: {DunsIdentifierScheme.Taken("UN778474481-TEST-DEMO-C7M-0001", "DEMO-C7M-0001")}", result.Problem);
        Assert.Equal(before, await rig.Count(DppStore.Packs));
    }

    [Theory]
    [InlineData(7, "iso6523-actorid-upis::0060:778474481", false)]
    [InlineData(12, " iso6523-actorid-upis::0060:123456789 ", false)]
    [InlineData(7, "0060:778474481", true)]
    [InlineData(12, "urn:x-demo:manufacturer:plant-north", true)]
    [InlineData(12, "", false)]
    [InlineData(11, "VPC-12221", false)]
    public void TheOperatorsAndManufacturersIds_ShouldOnlyGetANote_WhenNotInTheIso6523Form(int number, string text, bool note) =>
        Assert.Equal(note, IdentifierForms.ActorIdNote(number, text) is not null); // never a refusal: the editor saves it as it is

    [Fact]
    public void TheDemoOperatorsAndSites_ShouldBeInTheIso6523Form_TheScaniaOnesWithTheRealDuns()
    {
        var catalogue = JsonNode.Parse(File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Demo", "demo-catalogue.json")))!;
        var operators = catalogue["operators"]!.AsArray().ToDictionary(item => item!["key"]!.GetValue<string>(), item => item!["values"]!["economicOperatorId"]!.GetValue<string>());
        var sites = catalogue["sites"]!.AsArray().Select(item => item!["values"]!["uniqueManufacturerIdentifier"]!.GetValue<string>()).ToArray();

        Assert.All(operators.Values, id => Assert.Null(IdentifierForms.ActorIdNote(7, id))); // the note shows only where it should
        Assert.All(sites, id => Assert.Equal("iso6523-actorid-upis::0060:778474481", id)); // the demo plants are Scania's
        Assert.Equal("iso6523-actorid-upis::0060:778474481", operators["batteries"]); // Scania Industrial Batteries
        Assert.DoesNotContain("778474481", operators["fleet"], StringComparison.Ordinal); // another company: a made-up number
    }

    [Theory]
    [InlineData("Passport", "_PassportHero.cshtml")]
    [InlineData("Dpp", "Accidents.cshtml")]
    [InlineData("Shared", "_TestTag.cshtml")]
    public void TheTestIdNotes_ShouldNoLongerWaitForAScheme(string folder, string view)
    {
        var text = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, view));

        Assert.DoesNotContain("scheme is chosen", text, StringComparison.Ordinal); // chosen on 5 Nov 2026
        Assert.DoesNotContain("start with TEST-", text, StringComparison.Ordinal); // a demo ID is UN778474481-TEST-…
        Assert.Contains("marked", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnId_ShouldBeShownShort_AndLinkedToItsFullAddress()
    {
        Assert.Equal("UN778474481-VBC-92921", IdentifierForms.Display($"{Base}/25S/UN778474481-VBC-92921"));
        Assert.Equal("UN778474481-VBC-92921-R1/v2", IdentifierForms.Display($"{Base}/dpp/UN778474481-VBC-92921-R1/v2"));
        Assert.Equal("UN778474481VPC12221", IdentifierForms.Display($"{Base}/25S/UN778474481VPC12221"));
        Assert.Equal("UN778474481VPC12221R1/v2", IdentifierForms.Display($"{Base}/dpp/UN778474481VPC12221R1/v2"));
        Assert.Equal("urn:x-battery-pass:TEST-0f8c", IdentifierForms.Display("urn:x-battery-pass:TEST-0f8c"));
        var partial = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_IdValue.cshtml"));
        Assert.Contains("href=\"@Links.PassportUrl(Model)\"", partial, StringComparison.Ordinal);
        Assert.Contains("d-none d-print-inline\">@Model<", partial, StringComparison.Ordinal); // on paper the full address
    }

    [Theory]
    [InlineData("UN778474481-VBC-92921", true)]
    [InlineData("UN778474481-VBC-92921-R1", true)]
    [InlineData("UN778474481-TEST-DEMO-C7M-0001", true)]
    [InlineData("UN778474481-000123", true)]
    [InlineData("UN778474481VPC12221", true)] // made before 6d-5: still read
    [InlineData("UN778474481VPC12221R1", true)]
    [InlineData("UN778474481TESTDEMOC7M0001", true)]
    [InlineData("UN778474481", false)]
    [InlineData("UN778474481-", false)]
    [InlineData("UN778474481--VBC", false)]
    [InlineData("UN778474481-VBC--92921", false)]
    [InlineData("UN778474481-VBC-92921-", false)]
    [InlineData("UN778474481-vbc-92921", false)]
    [InlineData("UN778474481-VBC_92921", false)]
    public void AnItemId_ShouldBeRead_InTheNewFormAndTheOldOne(string item, bool valid)
    {
        Assert.Equal(valid, IdentifierForms.IsItem(item));
        Assert.Equal(valid ? item : $"{Base}/25S/{item}", IdentifierForms.Short($"{Base}/25S/{item}"));
        Assert.Equal(valid ? $"{item}/v3" : $"{Base}/dpp/{item}/v3", IdentifierForms.Display($"{Base}/dpp/{item}/v3"));
        Assert.Equal(valid, IdentifierForms.IsWebAddress($"{Base}/25S/{item}"));
    }

    [Fact]
    public void TheSerialRule_ShouldBeCapitalsDigitsAndSingleDashes()
    {
        Assert.True(IdentifierForms.IsSerial("VBC-92921"));
        Assert.True(IdentifierForms.IsSerial("DEMO-C7M-0001"));
        Assert.True(IdentifierForms.IsSerial("000123"));
        Assert.False(IdentifierForms.IsSerial("VBC--92921"));
        Assert.False(IdentifierForms.IsSerial("vbc-92921"));
        Assert.False(IdentifierForms.IsSerial(null));
        Assert.Contains("capital letters A–Z, digits 0–9 and single dashes", IdentifierForms.SerialRule, StringComparison.Ordinal);
        Assert.Equal(34, DunsIdentifierScheme.MaxItemLength - "UN778474481-".Length - "-R99".Length); // the cap: a real serial at most 34 characters
    }

    [Fact]
    public void TheVersionsAddress_ShouldShowThatVersion() =>
        Assert.Contains("return View(nameof(Summary), page);", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs")), StringComparison.Ordinal);
}
