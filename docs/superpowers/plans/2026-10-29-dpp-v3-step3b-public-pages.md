# DPP V3 Step 3b: The Public Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Readers see the new passports. The public summary (no sign-in, opened from the battery's QR code) and the detail page (signed in: one tab per audience the reader may see, each value with when and where it came from) replace today's passport pages in place, together with the QR code and the home page's search. Today's look, with the new data underneath (decision 23).

**Architecture:** Builds on 3a's readers and filter. `PassportPages` finds a published version by the battery ID (#6, the current version), a passport ID (#5, that version) or the serial number, reads it with `PassportReader`, and cuts it with `PassportFilter.Cut` for the reader from `DppReaders.ForAsync` and the battery's cluster from `DppStore.FindPublishedClusterIdsUsingAsync(packId:)`. `ValueWords` puts each value in words by following the attribute's form, the same reading of the official schema as the team's editors. `PassportController` serves `/dpp/{id}` and `/dpp/{id}/detail` in place of today's routes; `QrController` draws the code from `DppLinks.PassportUrl` (APP_BASE_URL); `HomeController.Search` opens the found passport.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0, QRCoder (already in the app). Plain CSS from `site.css` and Bootstrap's grid, as today; nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 4.1, 5, 10, 12 and the 3b bullet of 14) and the decisions record (decisions 20, 23 and 28). Earlier plans: steps 1, 2a–2d-2 and 3a in `docs/superpowers/plans/`. Mock-up: `docs/mockups/dpp-v3-detail-page-audiences.html`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 710687c (3a, and the product owner's change to open files in a new tab) plus the commit that adds this plan. Check with `git branch --show-current`.
- **Close the local app first.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Restart it after the plan.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2019`. After this plan: 2059. The suite takes about a minute.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3b-task1-code.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **No data changes:** this plan changes no stored data.

## Decisions taken with the product owner (29 Oct 2026)

1. **The addresses.** The public summary is `{APP_BASE_URL}/dpp/{id}` and the detail page `{APP_BASE_URL}/dpp/{id}/detail`, where `{id}` is the battery ID (#6, the current version), a passport ID (#5, that version) or the battery's serial number. The QR code carries `{APP_BASE_URL}/dpp/{#6}`, with the battery ID's colons as written (`/dpp/urn:x-battery-pass:TEST-…`). Today's `/{passportId}/summary`, `/{batteryId}/latest` and `/{id}` go, with no redirects (decision 23).
2. **An archived version** opened by its own passport ID is shown with an "Archived" tag and a link to the current version. Live values aren't kept per version, so its Purchaser tab says they are on the current version.
3. **Today's look** for both pages (decision 23); the calmer finish of decision 27 stays on the team pages.
4. **The detail page** follows the mock-up `docs/mockups/dpp-v3-detail-page-audiences.html`, approved with its wording: the audience tabs come first, marked with a lock; under each value a grey line reads "Entered 28 Oct 2026, 10:30 · form" or "Measured 29 Oct 2026, 08:55 · battery system".
5. **The summary report** (original power capability, carbon footprint, recycled content) sits in one row on wide screens and stacks on narrow ones (product owner's change to the prototype). The prototype's look was approved as shown.

## Judgement calls in this plan

1. **Only published versions are shown**, to everyone, the team included; a draft's address says "Not available" (J-6). The team sees drafts on its battery page.
2. **The summary is public data only**, whoever reads it. Its link under the picture says what the detail page holds for the reader: "View more about this passport", "Your account sees the public data only. Sign in with another account to see more." or, signed out, "Sign in to see more about this passport.".
3. **The detail page** opens for any signed-in reader; signed out, the sign-in page comes first and returns to it. Its tabs start with one per audience the reader may see (Purchaser, Shared, Authorities), then the public data by schema section with short tab names. As today, the tab bar scrolls sideways on its own when it is wider than the page.
4. **Values in words** follow the attribute's form: units ("512.5 kg"), months ("September 2026"), durations ("8 years"), grouped values part by part, lists one item per line, documents as their title linking the file (in a new tab, like the team pages). #4 shows in the app's time zone.
5. **The origin line** comes from the stored value's time and source (design 4.1), shown only while the stored value is still the one on the page: an archived version may show a value that has changed since, and then has no line. #1–#5 are set by the app and have none. Who sent a value isn't shown.
6. **The battery card** keeps today's layout: the serial number as title, the model, an "Active" or "Archived" tag, the passport ID and battery ID with copy buttons, the QR code, the family's picture (decision 20) and the fields family, serial number, model, category, passport status, weight, manufacturing date, manufacturer and last update. No cluster, no battery status (#22 is purchaser data), no software version. The detail page adds "Signature: Verified" in place of today's "Trust & conformance" tab.
7. **The test-ID banner** shows when the battery ID or the passport ID starts with the test prefix (design 10).
8. **The summary report** shows #67, #31–#34 with #30, and #49–#52, each only when it has values.
9. **The QR code** is drawn only for a battery with a published passport, and only while APP_BASE_URL is set; its file is named after the serial number. The home page's scanner hands the scanned text to the search.
10. **The search** takes a serial number, a battery ID, a passport ID or a scanned address (it reads the ID out of the address), and opens the summary, or the detail page when signed in. A serial number counts only when exactly one published battery has it. The home page loses the sample battery and the "DID" wording.
11. **Older tests:** 36 source-assertion tests read today's passport pages, QR code and search. 21 now check the same thing in the new files (battery card, QR panel, copy buttons, tab row, links); 15 go with what they checked: the sample passport, the `/latest` routes, the battery-level page, the telemetry charts, the software version, the trust tab and the wrong-cluster notice.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Pages/ValueWords.cs` | A passport value in words, following the attribute's form |
| `web/Dpp/Pages/PassportPages.cs` | Finds a published version by #6, #5 or serial number; the two pages' data, cut per reader |
| `web/Dpp/Documents/DppLinks.cs`, `web/Dpp/DppServiceCollectionExtensions.cs` (modify) | The QR code's address; registers `PassportPages` |
| `web/Controllers/PassportController.cs` (replace) | `/dpp/{id}` and `/dpp/{id}/detail` in place of today's routes |
| `web/Views/Passport/Summary.cshtml`, `Detail.cshtml` (replace), `_PassportHero.cshtml`, `_PassportTabs.cshtml` | The two pages in today's look; `Battery.cshtml` goes |
| `web/Views/Dpp/NotAvailable.cshtml` (modify) | "No published battery passport has this ID" |
| `web/Controllers/QrController.cs`, `web/Services/PassportQrCodeService.cs` (modify) | The QR code of a published battery, from APP_BASE_URL |
| `web/Controllers/HomeController.cs`, `web/Views/Home/Index.cshtml` (modify) | The search on the new passports |


---

### Task 1: Values in words, and the pages' data

The data both pages share; nothing is shown yet. The tests read the seed passport for every kind of reader of design 5.2.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs`
- Create: `web/Dpp/Pages/PassportPages.cs`
- Create: `web/Dpp/Pages/ValueWords.cs`
- Modify: `web/Dpp/Documents/DppLinks.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The public summary and the detail page's data (design 12): found by #6, #5 or serial number, cut per reader.</summary>
[Collection(MongoCollection.Name)]
public sealed class PassportPagesTests(MongoFixture mongo)
{
    private static readonly Reader OwnCluster = Reader.With("user@example.com", ReaderRole.ClusterUser, BatteryTesting.North);
    private static readonly Reader OtherCluster = Reader.With("other@example.com", ReaderRole.ClusterUser, BatteryTesting.South);

    [Fact]
    public async Task APassport_ShouldBeFoundByBatteryIdPassportIdOrSerialNumber()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = Pages(rig);
        var active = await rig.Store.GetActiveVersionAsync(rig.PackId);

        Assert.Equal(active!.Id, (await pages.FindAsync(Identifier(rig)))?.Id);
        Assert.Equal(active.Id, (await pages.FindAsync(active.DigitalProductPassportId))?.Id);
        Assert.Equal(active.Id, (await pages.FindAsync(" VPC8C05-6788 "))?.Id);
        Assert.Null(await pages.FindAsync("VPC8C05-0000"));
        var located = (await pages.LocateAsync(active.DigitalProductPassportId))!;
        Assert.Equal((Identifier(rig), "VPC8C05-6788", Identifier(rig)), (located.Identifier, located.SerialNumber, located.Address));
        Assert.Null(await pages.FindAsync(""));
    }

    [Fact]
    public async Task ADraft_ShouldBeFoundByNobody()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = Pages(rig);

        Assert.Null(await pages.FindAsync(Identifier(rig)));
        Assert.Null(await pages.FindAsync("VPC8C05-6788"));
        Assert.Null(await pages.SummaryAsync(Reader.With("team@example.com", ReaderRole.Team), Identifier(rig)));
        Assert.Null(await pages.DetailAsync(Reader.With("team@example.com", ReaderRole.Team), Identifier(rig)));
    }

    [Fact]
    public async Task TheSummary_ShouldShowEveryPublicValueAndNothingElseWhoeverReads()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var team = Reader.With("team@example.com", ReaderRole.Team);

        var page = (await Pages(rig).SummaryAsync(team, Identifier(rig)))!;

        var shown = page.Sections.SelectMany(section => section.Values).ToArray();
        Assert.All(shown, value => Assert.Equal(Audience.Public, value.Attribute.Audience));
        Assert.All(shown, value => Assert.Null(value.Origin));
        Assert.Equal(AttributeCatalog.All.Count(a => a.Audience == Audience.Public), shown.Length);
        Assert.Empty(page.Audiences);
        Assert.Equal(
            ["Passport", "Identifiers and product data", "Symbols, labels and conformity", "Carbon footprint", "Due diligence", "Materials and composition", "Circularity", "Performance and durability"],
            page.Sections.Select(section => section.Title));
        Assert.Equal(
            ["Passport", "Identifiers", "Symbols and conformity", "Carbon footprint", "Due diligence", "Materials", "Circularity", "Performance"],
            page.Sections.Select(section => section.Tab));
        Assert.Null(page.Value(22));
        Assert.Equal("VPC8C05-6788", page.Text(11));
        Assert.Equal("active", page.Text(2));
    }

    [Fact]
    public async Task TheSummary_ShouldSayWhetherTheDetailPageHasMoreForTheReader()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var pages = Pages(rig);

        Assert.True((await pages.SummaryAsync(OwnCluster, Identifier(rig)))!.ReaderSeesMore);
        Assert.True((await pages.SummaryAsync(Reader.With("nb@example.com", ReaderRole.NotifiedBody), Identifier(rig)))!.ReaderSeesMore);
        Assert.False((await pages.SummaryAsync(OtherCluster, Identifier(rig)))!.ReaderSeesMore);
        Assert.False((await pages.SummaryAsync(Reader.Anonymous, Identifier(rig)))!.ReaderSeesMore);
    }

    [Fact]
    public async Task TheSummary_ShouldCarryTheBannerThePictureAndTheCharts()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetFamilyPictureAsync("family-test", "file-1", "image/png");

        var page = (await Pages(rig).SummaryAsync(Reader.Anonymous, "VPC8C05-6788"))!;

        Assert.True(page.IsTest);
        Assert.True(page.SignatureHolds);
        Assert.True(page.IsCurrent);
        Assert.Null(page.Current);
        Assert.Equal("Test battery family", page.FamilyName);
        Assert.Equal("/dpp/families/family-test/picture", page.PictureHref);
        Assert.Equal(Identifier(rig), page.Address);
        Assert.Equal(["Raw material acquisition", "Main production", "Distribution", "End of life and recycling"], page.Carbon.Select(part => part.Label));
        Assert.Equal([48.1m, 20.3m, 2.2m, 4.8m], page.Carbon.Select(part => part.Value));
        Assert.Equal([("Nickel", 6m), ("Cobalt", 16m), ("Lithium", 6m), ("Lead", 0m)], page.Recycled.Select(part => (part.Label, part.Value)));
    }

    [Theory]
    [InlineData("cluster", new[] { "purchaser", "shared" })]
    [InlineData("other-cluster", new string[0])]
    [InlineData("team", new[] { "purchaser", "shared", "authorities" })]
    [InlineData("market-surveillance", new[] { "purchaser", "shared", "authorities" })]
    [InlineData("notified-body", new[] { "authorities" })]
    [InlineData("commission", new[] { "shared", "authorities" })]
    [InlineData("li-treatment", new[] { "shared" })]
    public async Task TheDetailPage_ShouldHaveOneSectionPerAudienceTheReaderMaySee(string who, string[] expected)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var reader = who switch
        {
            "cluster" => OwnCluster,
            "other-cluster" => OtherCluster,
            "team" => Reader.With(who, ReaderRole.Team),
            "market-surveillance" => Reader.With(who, ReaderRole.MarketSurveillance),
            "notified-body" => Reader.With(who, ReaderRole.NotifiedBody),
            "commission" => Reader.With(who, ReaderRole.Commission),
            _ => Reader.With(who, ReaderRole.LiTreatment),
        };

        var page = (await Pages(rig).DetailAsync(reader, Identifier(rig)))!;

        Assert.Equal(expected, page.Audiences.Select(section => section.Key));
        var clusters = await rig.Store.FindPublishedClusterIdsUsingAsync(packId: rig.PackId);
        Assert.All(
            page.Audiences.SelectMany(section => section.Values),
            value => Assert.True(AudienceRules.MaySee(reader, value.Attribute, clusters), value.Attribute.Label));
        Assert.Equal(AttributeCatalog.All.Count(a => a.Audience == Audience.Public), page.Sections.Sum(section => section.Values.Count));
    }

    [Fact]
    public async Task ATreatmentFacility_ShouldSeeOnlyTheSafetyMeasuresOfTheSharedData()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var page = (await Pages(rig).DetailAsync(Reader.With("weee@example.com", ReaderRole.LiTreatment), Identifier(rig)))!;

        Assert.Equal([48], Assert.Single(page.Audiences).Values.Select(value => value.Attribute.Number));
    }

    [Fact]
    public async Task EveryValueOnTheDetailPage_ShouldSayWhenAndWhereItCameFrom()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var measured = rig.Clock.Now.UtcDateTime.AddMinutes(-5);
        await rig.Pipeline.SaveAsync(
            ChangeSender.Feed("bms-feed", BatteryTesting.North),
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = JsonValue.Create(55) }, "feed", measured));

        var page = (await Pages(rig).DetailAsync(OwnCluster, Identifier(rig)))!;

        var values = page.Sections.Concat(page.Audiences).SelectMany(section => section.Values).ToArray();
        Assert.All(values.Where(value => value.Attribute.Number > 5), value => Assert.NotNull(value.Origin));
        Assert.All(values.Where(value => value.Attribute.Number <= 5), value => Assert.Null(value.Origin));
        var stateOfCharge = values.Single(value => value.Attribute.Number == 63);
        Assert.Equal(new ValueOrigin(measured, "feed", Measured: true), stateOfCharge.Origin);
        Assert.Equal("55 %", Assert.Single(stateOfCharge.Lines).Text);
        Assert.Equal(new ValueOrigin(DppTestSeed.GeneratedAtUtc, "seed", Measured: false), values.Single(value => value.Attribute.Number == 21).Origin);
        Assert.Equal("battery system", PassportPages.SourceWord("feed"));
        var accidents = Assert.Single(values.Single(value => value.Attribute.Number == 92).Lines);
        Assert.Equal("https://passport.example.com/dpp/packs/pack-VPC8C05-6788/accidents", accidents.Href);
    }

    [Fact]
    public async Task AnArchivedVersion_ShouldShowItsOwnDataAndLinkToTheCurrentOne()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var first = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        rig.Clock.Now = rig.Clock.Now.AddHours(1);
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonValue.Create(520) }, "form"));
        var second = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        var team = Reader.With("team@example.com", ReaderRole.Team);

        var page = (await Pages(rig).DetailAsync(team, first.DigitalProductPassportId))!;

        Assert.False(page.IsCurrent);
        Assert.Equal(second.Id, page.Current?.Id);
        Assert.Equal(first.DigitalProductPassportId, page.Address);
        Assert.Equal(first.DigitalProductPassportId, (await Pages(rig).LocateAsync(first.DigitalProductPassportId))!.Address);
        Assert.Equal("archived", page.Text(2));
        Assert.Equal("512.5 kg", page.Text(21));
        Assert.Null(page.Value(21)!.Origin);
        var purchaser = page.Audiences.Single(section => section.Key == "purchaser").Values.Select(value => value.Attribute.Number).ToArray();
        Assert.Contains(22, purchaser);
        Assert.DoesNotContain(63, purchaser);
        Assert.Equal("520 kg", (await Pages(rig).SummaryAsync(team, Identifier(rig)))!.Text(21));
    }

    [Fact]
    public void TheAddresses_ShouldKeepTheBatteryIdReadable()
    {
        Assert.Equal("/dpp/urn:x-battery-pass:TEST-0f3a", PassportPages.SummaryHref("urn:x-battery-pass:TEST-0f3a"));
        Assert.Equal("/dpp/urn:x-battery-pass:TEST-0f3a/detail", PassportPages.DetailHref("urn:x-battery-pass:TEST-0f3a"));
        Assert.Equal("/dpp/VPC%2F1%20A", PassportPages.SummaryHref("VPC/1 A"));
        Assert.Equal("/qr/urn:x-battery-pass:TEST-0f3a/svg", PassportPages.QrHref("urn:x-battery-pass:TEST-0f3a"));
        Assert.Equal("https://passport.example.com/dpp/urn:x-battery-pass:TEST-0f3a", DocumentTesting.Links.PassportUrl("urn:x-battery-pass:TEST-0f3a"));
    }

    internal static PassportPages Pages(PublicationRig rig) =>
        new(rig.Store, new PassportReader(rig.Store), AttributeFormsTests.Forms, new TestIdentifierScheme());

    internal static string Identifier(PublicationRig rig) => rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value.GetValue<string>();
}
```

`BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Passport values in words on the public summary and the detail page (design 12).</summary>
public sealed class ValueWordsTests
{
    [Theory]
    [InlineData(21, """{"gramKilogramUnit":"kg","gramKilogramValue":512.5}""", "512.5 kg")]
    [InlineData(17, "\"2026-09\"", "September 2026")]
    [InlineData(19, "\"P8Y\"", "8 years")]
    [InlineData(19, "\"P1Y6M\"", "1 year 6 months")]
    [InlineData(20, """{"batteryCategoryValue":"industrial/non-stationary battery"}""", "industrial/non-stationary battery")]
    [InlineData(22, """{"batteryStatusValues":"original"}""", "original")]
    [InlineData(81, "12", "12 years")]
    [InlineData(82, "4000", "4,000")]
    [InlineData(63, """{"percentUnit":"%","percentageValue":76}""", "76 %")]
    [InlineData(1001, "3600", "3,600 seconds")]
    [InlineData(11, "\"VPC8C05-6788\"", "VPC8C05-6788")]
    public void ASingleValue_ShouldReadAsOneLine(int number, string json, string expected)
    {
        var line = Assert.Single(Lines(number, json));

        Assert.Null(line.Label);
        Assert.Equal(expected, line.Text);
        Assert.Null(line.Href);
    }

    [Fact]
    public void TheLastUpdate_ShouldReadAsATimeInTheAppsTimeZone()
    {
        var line = Assert.Single(Lines(4, "\"2026-10-28T09:30:00Z\""));

        Assert.Equal(ValueWords.Time(new DateTime(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc)), line.Text);
    }

    [Fact]
    public void AGroupedValue_ShouldGiveEachPartItsLabel()
    {
        var lines = Lines(66, """{"voltValue":650.0,"voltUnit":"V","temperatureRangeLowerBoundaryValue":-20,"temperatureRangeLowerBoundaryUnit":"°C","temperatureRangeUpperBoundaryValue":40,"temperatureRangeUpperBoundaryUnit":"°C"}""");

        Assert.Equal(
            [("Voltage", "650 V"), ("Test temperature from", "-20 °C"), ("Test temperature to", "40 °C")],
            lines.Select(line => (line.Label, line.Text)));
    }

    [Fact]
    public void AList_ShouldReadOneItemPerLine()
    {
        var lines = Lines(42, """["Cobalt","Lithium","Natural graphite"]""");

        Assert.Equal(["Cobalt", "Lithium", "Natural graphite"], lines.Select(line => line.Text));
    }

    [Fact]
    public void AListOfObjects_ShouldMarkWhereEachItemStarts()
    {
        var lines = Lines(44, """[{"chemicalName":"A","identifier":"1-2-3","hazardPhrases":["H301","H314"]},{"chemicalName":"B"}]""");

        Assert.Equal(
            [("Chemical name", "A", true), ("Identifier", "1-2-3", false), ("Hazard phrases", "H301, H314", false), ("Chemical name", "B", true)],
            lines.Select(line => (line.Label!, line.Text, line.StartsRow)));
    }

    [Theory]
    [InlineData(28, "EU declaration", "https://www.example.com/doc.pdf")]
    [InlineData(45, "Dismantling manual", "https://passport.example.com/dpp/documents/doc-1")]
    [InlineData(92, "Accident information", "https://passport.example.com/dpp/packs/pack-1/accidents")]
    public void ADocument_ShouldReadAsItsTitleLinkingItsAddress(int number, string title, string url)
    {
        var line = Assert.Single(Lines(number, $$"""{"contentType":"application/pdf","url":"{{url}}","language":"en-GB","resourceTitle":"{{title}}"}"""));

        Assert.Equal(title, line.Text);
        Assert.Equal(url, line.Href);
    }

    [Fact]
    public void AWebAddress_ShouldLinkItself()
    {
        var line = Assert.Single(Lines(36, "\"https://www.example.com/study\""));

        Assert.Equal("https://www.example.com/study", line.Href);
    }

    [Fact]
    public void NoValue_ShouldHaveNoLines()
    {
        Assert.Empty(ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(21)), null));
    }

    [Fact]
    public void EveryValueOfTheSeedPassport_ShouldHaveWords()
    {
        var seed = DppTestSeed.Load();
        var passport = PassportAssembler.Assemble(seed.Sources, seed.Header);

        foreach (var attribute in AttributeCatalog.All)
        {
            JsonNode? value = passport;
            foreach (var name in attribute.Path.Split('.'))
            {
                value = value?[name];
            }

            if (value is not null)
            {
                Assert.True(ValueWords.Lines(AttributeFormsTests.Forms.For(attribute), value).Count > 0, attribute.Label);
            }
        }
    }

    private static IReadOnlyList<ValueLine> Lines(int number, string json) =>
        ValueWords.Lines(AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number)), JsonNode.Parse(json));
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ValueWordsTests"`
Expected: the build fails because `PassportPages` and `ValueLine` don't exist yet (CS0246).

- [ ] **Step 3: Write `PassportPages.cs`**

`web/Dpp/Pages/PassportPages.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>When a stored value was generated (design 4.1) and where it came from: "form", "upload", "feed" or "seed".</summary>
public sealed record ValueOrigin(DateTime GeneratedAtUtc, string Source, bool Measured);

/// <summary>One attribute on a passport page: its value in words and, on the detail page, its origin.</summary>
public sealed record ShownValue(AttributeDefinition Attribute, IReadOnlyList<ValueLine> Lines, ValueOrigin? Origin);

/// <summary>
/// A tab of the passport pages: a schema section of public data, or one audience's data (detail page). <paramref name="Tab"/>
/// is the tab's short name, <paramref name="Title"/> the heading inside it.
/// </summary>
public sealed record PassportSection(string Key, string Title, string Tab, string Note, IReadOnlyList<ShownValue> Values);

/// <summary>One part of a chart on the summary report: a carbon footprint stage or a recycled content share.</summary>
public sealed record ChartPart(string Label, decimal Value, string Unit);

/// <summary>
/// A published passport version as one reader sees it (design 12): the public data by schema section and, on the
/// detail page, one section per audience the reader may see. Everything shown went through <see cref="PassportFilter"/>.
/// </summary>
public sealed record PassportPage(
    PassportVersion Version,
    PassportVersion? Current,
    string Identifier,
    string? FamilyName,
    string? PictureHref,
    bool IsTest,
    bool SignatureHolds,
    bool ReaderSeesMore,
    IReadOnlyList<PassportSection> Sections,
    IReadOnlyList<PassportSection> Audiences,
    IReadOnlyList<ChartPart> Carbon,
    IReadOnlyList<ChartPart> Recycled)
{
    /// <summary>True for the active version; an archived one links to <see cref="Current"/>.</summary>
    public bool IsCurrent => Version.DppStatus == DppStatuses.Active;

    /// <summary>The address of this page: the battery ID for the current version, the version's own passport ID otherwise.</summary>
    public string Address => IsCurrent ? Identifier : Version.DigitalProductPassportId;

    /// <summary>A public value shown on the page by its number, e.g. #11 for the hero's title; null when it has none.</summary>
    public ShownValue? Value(int number) =>
        Sections.SelectMany(section => section.Values).FirstOrDefault(value => value.Attribute.Number == number);

    /// <summary>The first line of a public value in words, or "-".</summary>
    public string Text(int number) => Value(number)?.Lines.FirstOrDefault()?.Text ?? "-";
}

/// <summary>Where a published passport is: its version, and the battery's ID (#6) and serial number (#11).</summary>
public sealed record PassportLocation(PassportVersion Version, string Identifier, string SerialNumber)
{
    /// <summary>The pages' address: the battery ID for the current version, the version's own passport ID otherwise.</summary>
    public string Address => Version.DppStatus == DppStatuses.Active ? Identifier : Version.DigitalProductPassportId;
}

/// <summary>
/// The public summary and the detail page (design 12, step 3b). A passport is found by its battery ID (#6, the
/// current version), its passport ID (#5, that version) or the battery's serial number (#11). Only published
/// versions are shown: drafts stay with the team (J-6).
/// </summary>
public sealed class PassportPages(DppStore store, PassportReader reader, AttributeForms forms, IIdentifierScheme identifiers)
{
    /// <summary>The reader-facing title and line of each schema section, in the schema's order.</summary>
    private static readonly (string Key, string Title, string Tab, string Note)[] SectionTexts =
    [
        (AttributeDefinition.HeaderSection, "Passport", "Passport", "This passport and the battery it belongs to."),
        ("identifiersAndProductData", "Identifiers and product data", "Identifiers", "What the battery is and who makes it."),
        ("symbolsLabelsAndDocumentationOfConformity", "Symbols, labels and conformity", "Symbols and conformity", "The battery's symbols and its declaration of conformity."),
        ("batteryCarbonFootprint", "Carbon footprint", "Carbon footprint", "Per functional unit and per life-cycle stage."),
        ("supplyChainDueDiligence", "Due diligence", "Due diligence", "The supply chain due diligence report."),
        ("batteryMaterialsAndComposition", "Materials and composition", "Materials", "Chemistry, critical raw materials and hazardous substances."),
        ("circularityAndResourceEfficiency", "Circularity", "Circularity", "Recycled and renewable content, and the end user's role in collection and waste prevention."),
        ("performanceAndDurability", "Performance and durability", "Performance", "Capacity, voltage, power, efficiency and expected lifetime."),
    ];

    /// <summary>The detail page's audience sections (design 5.1), with who sees each.</summary>
    private static readonly (Audience Audience, string Key, string Title, string Note)[] AudienceTexts =
    [
        (Audience.Purchaser, "purchaser", "Purchaser", "Seen by the battery's cluster and market surveillance."),
        (Audience.Shared, "shared", "Shared", "Seen by the battery's cluster, market surveillance, the Commission and, per value, repairers and recyclers with a legitimate interest."),
        (Audience.Authorities, "authorities", "Authorities", "Seen by notified bodies, market surveillance and the Commission."),
    ];

    private static readonly (int Number, string Label)[] CarbonStages =
    [
        (31, "Raw material acquisition"),
        (32, "Main production"),
        (33, "Distribution"),
        (34, "End of life and recycling"),
    ];

    private static readonly (int Number, string Label)[] RecycledShares = [(49, "Nickel"), (50, "Cobalt"), (51, "Lithium"), (52, "Lead")];

    /// <summary>The public summary's address. Colons stay as they are, so a battery ID reads as written.</summary>
    public static string SummaryHref(string id) => $"/dpp/{Escape(id)}";

    /// <summary>The detail page's address.</summary>
    public static string DetailHref(string id) => $"/dpp/{Escape(id)}/detail";

    /// <summary>The QR code's picture of a battery, and the same picture as a file to save.</summary>
    public static string QrHref(string id) => $"/qr/{Escape(id)}/svg";

    public static string QrDownloadHref(string id) => $"/qr/{Escape(id)}/download";

    /// <summary>What a reader-facing source is called on the detail page.</summary>
    public static string SourceWord(string source) => source switch
    {
        "form" => "form",
        "upload" => "upload",
        "feed" => "battery system",
        "seed" => "test data",
        _ => source,
    };

    /// <summary>
    /// The published version a battery ID (#6), passport ID (#5) or serial number (#11) names; null when there is none.
    /// A serial number counts only when exactly one published battery has it.
    /// </summary>
    public async Task<PassportVersion?> FindAsync(string? id, CancellationToken cancellationToken = default)
    {
        var text = id?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        if (await store.GetVersionByPassportIdAsync(text, cancellationToken) is { } version)
        {
            return version;
        }

        if (await store.FindPackByUniqueProductIdentifierAsync(text, cancellationToken) is { } pack)
        {
            return await store.GetActiveVersionAsync(pack.Id, cancellationToken);
        }

        var published = new List<PassportVersion>();
        foreach (var candidate in await store.FindPacksBySerialNumberAsync(text, cancellationToken))
        {
            if (await store.GetActiveVersionAsync(candidate.Id, cancellationToken) is { } active)
            {
                published.Add(active);
            }
        }

        return published.Count == 1 ? published[0] : null;
    }

    /// <summary>The published passport an ID names, with the battery's ID and serial number; null when there is none.</summary>
    public async Task<PassportLocation?> LocateAsync(string? id, CancellationToken cancellationToken = default)
    {
        var version = await FindAsync(id, cancellationToken);
        var pack = version is null ? null : await store.GetAsync<BatteryPack>(version.PackId, cancellationToken);
        return version is null || pack is null
            ? null
            : new PassportLocation(version, PackText(pack, "uniqueProductIdentifier") ?? string.Empty, PackText(pack, "batterySerialNumber") ?? string.Empty);
    }

    /// <summary>The public summary: public data only, whoever is reading. <see cref="PassportPage.ReaderSeesMore"/> says whether the detail page has more for this reader.</summary>
    public Task<PassportPage?> SummaryAsync(Reader who, string id, CancellationToken cancellationToken = default) =>
        PageAsync(who, id, detail: false, cancellationToken);

    /// <summary>The detail page: the public data and each audience section the reader may see, every value with its origin.</summary>
    public Task<PassportPage?> DetailAsync(Reader who, string id, CancellationToken cancellationToken = default) =>
        PageAsync(who, id, detail: true, cancellationToken);

    private async Task<PassportPage?> PageAsync(Reader who, string id, bool detail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(who);
        var version = await FindAsync(id, cancellationToken);
        var published = version is null ? null : await reader.VersionAsync(version.DigitalProductPassportId, cancellationToken);
        var sources = version is null ? null : await store.LoadSourcesAsync(version.PackId, cancellationToken);
        if (version is null || published is null || sources is null)
        {
            return null;
        }

        var clusters = await store.FindPublishedClusterIdsUsingAsync(packId: version.PackId, cancellationToken: cancellationToken);
        var everyone = PassportFilter.Cut(published.Passport, Reader.Anonymous, clusters);
        var seen = detail ? PassportFilter.Cut(published.Passport, who, clusters) : everyone;
        var family = await store.GetAsync<BatteryFamily>(sources.Model.FamilyId, cancellationToken);
        var identifier = Text(everyone, AttributeCatalog.ByNumber(6)) ?? string.Empty;
        var current = version.DppStatus == DppStatuses.Active ? null : await store.GetActiveVersionAsync(version.PackId, cancellationToken);

        var sections = SectionTexts
            .Select(section => new PassportSection(
                section.Key,
                section.Title,
                section.Tab,
                section.Note,
                Shown(seen, sources, detail, AttributeCatalog.All.Where(a => a.Audience == Audience.Public && a.Section == section.Key))))
            .Where(section => section.Values.Count > 0)
            .ToArray();
        var audiences = detail
            ? AudienceTexts
                .Where(audience => AttributeCatalog.All.Any(a => a.Audience == audience.Audience && AudienceRules.MaySee(who, a, clusters)))
                .Select(audience => new PassportSection(
                    audience.Key,
                    audience.Title,
                    audience.Title,
                    audience.Note,
                    Shown(seen, sources, detail, AttributeCatalog.All.Where(a => a.Audience == audience.Audience))))
                .ToArray()
            : [];

        return new PassportPage(
            version,
            current,
            identifier,
            family?.Name,
            family?.PictureFileId is null ? null : $"/dpp/families/{Uri.EscapeDataString(family.Id)}/picture",
            identifiers.IsTestIdentifier(identifier) || identifiers.IsTestIdentifier(version.DigitalProductPassportId),
            PassportReader.Verify(version),
            AttributeCatalog.All.Any(a => a.Audience != Audience.Public && AudienceRules.MaySee(who, a, clusters)),
            sections,
            audiences,
            Chart(everyone, CarbonStages, "kgCO2EquivalentPerKilowattHourValue", "kgCO2-eq/kWh"),
            Chart(everyone, RecycledShares, "percentageValue", "%"));
    }

    /// <summary>The attributes' values in the cut passport, in catalogue order; the detail page adds each value's origin.</summary>
    private IReadOnlyList<ShownValue> Shown(JsonObject passport, PassportSources sources, bool detail, IEnumerable<AttributeDefinition> attributes)
    {
        var shown = new List<ShownValue>();
        foreach (var attribute in attributes)
        {
            var value = Find(passport, attribute);
            var lines = ValueWords.Lines(forms.For(attribute), value);
            if (lines.Count == 0)
            {
                continue;
            }

            shown.Add(new ShownValue(attribute, lines, detail ? Origin(attribute, sources, value) : null));
        }

        return shown;
    }

    /// <summary>
    /// The stored value's time and source, when the stored value is still the one shown. An archived version may show a
    /// value that has changed since; its origin isn't known any more, so none is given. #1–#5 are set by the app.
    /// </summary>
    private static ValueOrigin? Origin(AttributeDefinition attribute, PassportSources sources, JsonNode? shown)
    {
        var stored = attribute.Storage switch
        {
            StorageLocation.Pack => sources.Pack.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Operator => sources.Operator.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Batch => sources.Batch.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Site => sources.Site.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.Model => sources.Model.Values.GetValueOrDefault(attribute.Property),
            StorageLocation.ModelWithPackOverride => sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property),
            _ => null,
        };
        return stored is null || !FormValues.SameValue(stored.Value, shown)
            ? null
            : new ValueOrigin(stored.GeneratedAtUtc, stored.Source, attribute.Change == ChangeRule.Live);
    }

    private static IReadOnlyList<ChartPart> Chart(JsonObject passport, (int Number, string Label)[] parts, string valueName, string unit) =>
        parts
            .Select(part => (part.Label, Value: ValueWords.Decimal(Find(passport, AttributeCatalog.ByNumber(part.Number))?[valueName])))
            .Where(part => part.Value is not null)
            .Select(part => new ChartPart(part.Label, part.Value!.Value, unit))
            .ToArray();

    private static JsonNode? Find(JsonObject passport, AttributeDefinition attribute)
    {
        JsonNode? node = passport;
        foreach (var name in attribute.Path.Split('.'))
        {
            node = node?[name];
        }

        return node;
    }

    private static string? Text(JsonObject passport, AttributeDefinition attribute) =>
        Find(passport, attribute) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? PackText(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Escape(string id) => Uri.EscapeDataString(id).Replace("%3A", ":", StringComparison.Ordinal);
}
```

- [ ] **Step 4: Write `ValueWords.cs`**

`web/Dpp/Pages/ValueWords.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>
/// One line of a value as passport readers see it: an optional label (a part of a grouped value), the text, and a
/// link where the value is a document or a web address. <paramref name="StartsRow"/> marks the first line of each
/// item of a list of objects (#44).
/// </summary>
public sealed record ValueLine(string? Label, string Text, string? Href = null, bool StartsRow = false);

/// <summary>
/// A passport value in words for the public summary and the detail page (design 12). It follows the attribute's form,
/// the same reading of the official schema as the team's editors, so every shape is covered: "512.5 kg", "8 years",
/// "September 2026", a document's title as a link, a list one item per line.
/// </summary>
public static class ValueWords
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    public static IReadOnlyList<ValueLine> Lines(AttributeForm form, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (value is null)
        {
            return [];
        }

        if (form.Attribute.Number == 4 && Instant(value) is { } updated)
        {
            return [new ValueLine(null, Time(updated))];
        }

        switch (form.Kind)
        {
            case FormKind.Link:
            case FormKind.Upload:
                return Document(value) is { } document ? [document] : [];
            case FormKind.Fixed:
                return value is JsonObject fixedValue && fixedValue.Select(property => Scalar(property.Value)).FirstOrDefault(text => text is not null) is { } text
                    ? [new ValueLine(null, text)]
                    : [];
            case FormKind.Lines:
                return (value as JsonArray ?? []).Select(Scalar).OfType<string>().Select(item => new ValueLine(null, item)).ToArray();
            case FormKind.Rows:
                var rows = new List<ValueLine>();
                foreach (var item in value as JsonArray ?? [])
                {
                    var first = true;
                    foreach (var input in form.Inputs)
                    {
                        if (Part(input, item?[input.Path]) is { } part)
                        {
                            rows.Add(new ValueLine(input.Label, part, StartsRow: first));
                            first = false;
                        }
                    }
                }

                return rows;
            case FormKind.Single:
                var only = form.Inputs[0];
                var single = Part(only, only.Path.Length == 0 ? value : value[only.Path]);
                return single is null ? [] : [new ValueLine(null, single, only.Kind == InputKind.Url ? single : null)];
            default:
                return form.Inputs
                    .Select(input => (input.Label, Text: Part(input, value[input.Path])))
                    .Where(part => part.Text is not null)
                    .Select(part => new ValueLine(part.Label, part.Text!))
                    .ToArray();
        }
    }

    /// <summary>A time on the passport pages, in the app's time zone: "28 Oct 2026, 10:30".</summary>
    public static string Time(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local).ToString("d MMM yyyy, HH:mm", English);

    /// <summary>A JSON number as a decimal, however it was read (the database gives doubles); null for anything else.</summary>
    public static decimal? Decimal(JsonNode? node)
    {
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        return value.TryGetValue<decimal>(out var exact) ? exact
            : value.TryGetValue<double>(out var inexact) ? (decimal)inexact
            : value.TryGetValue<long>(out var whole) ? whole
            : decimal.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>A number as the pages show it: grouped thousands, no trailing zeros ("250,000", "0.5").</summary>
    public static string Number(decimal number) => number.ToString("#,0.######", CultureInfo.InvariantCulture);

    private static string? Part(FormInput input, JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonArray items)
        {
            var texts = items.Select(Scalar).OfType<string>().ToArray();
            return texts.Length == 0 ? null : string.Join(", ", texts);
        }

        if (Decimal(node) is { } number)
        {
            return input.Unit is { } unit ? $"{Number(number)} {unit}" : Number(number);
        }

        var text = Scalar(node);
        if (text is null)
        {
            return null;
        }

        if (input.Kind == InputKind.Month && DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            return month.ToString("MMMM yyyy", English);
        }

        return input.Hint?.StartsWith("A duration", StringComparison.Ordinal) == true ? Duration(text) : text;
    }

    /// <summary>A public link or an uploaded file: its title, opening its address.</summary>
    private static ValueLine? Document(JsonNode value)
    {
        var url = Scalar(value["url"]);
        if (url is null)
        {
            return null;
        }

        return new ValueLine(null, Scalar(value["resourceTitle"]) ?? url, url);
    }

    /// <summary>"P8Y" as "8 years", "P1Y6M" as "1 year 6 months"; anything else as written.</summary>
    private static string Duration(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?$");
        if (!match.Success || text == "P")
        {
            return text;
        }

        var parts = new List<string>();
        foreach (var (group, word) in new[] { (1, "year"), (2, "month"), (3, "day") })
        {
            if (match.Groups[group].Success)
            {
                parts.Add(Words.Count(int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture), word));
            }
        }

        return string.Join(" ", parts);
    }

    private static DateTime? Instant(JsonNode value) =>
        Scalar(value) is { } text && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)
            ? utc
            : null;

    private static string? Scalar(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>() is { Length: > 0 } text ? text : null,
        JsonValue value when Decimal(value) is { } number => Number(number),
        JsonValue value => value.ToJsonString(),
        _ => null,
    };
}
```

- [ ] **Step 5: Add the QR address and register the pages**

Patch `3b-task1-code.patch` (`web/Dpp/Documents/DppLinks.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`). Save the block as `../3b-task1-code.patch`, outside the worktree, and run `git apply ../3b-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Documents/DppLinks.cs b/web/Dpp/Documents/DppLinks.cs
--- a/web/Dpp/Documents/DppLinks.cs
+++ b/web/Dpp/Documents/DppLinks.cs
@@ -29,6 +29,12 @@ public sealed class DppLinks(IOptions<BatteryPassOptions> options)
 
     public string AccidentPageUrl(string packId) => $"{Base}/dpp/packs/{Uri.EscapeDataString(packId)}/accidents";
 
+    /// <summary>
+    /// The public summary's full address, which a battery's QR code carries (design 10): the battery ID (#6) as written,
+    /// so the code opens the battery's current passport.
+    /// </summary>
+    public string PassportUrl(string identifier) => Base + BatteryPassWeb.Dpp.Pages.PassportPages.SummaryHref(identifier);
+
     /// <summary>#92's value: the pack's accident page, which lists its reports or says none were reported (J-14).</summary>
     public JsonObject AccidentPageLink(string packId) => new()
     {
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -45,6 +45,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<BatteryPages>();
         services.AddSingleton<DppReaders>();
         services.AddSingleton<ReaderPages>();
+        services.AddSingleton<PassportPages>();
         services.AddSingleton<AuditLines>();
         return services;
     }
```

- [ ] **Step 6: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ValueWordsTests"`
Expected: `Passed!  - Failed:     0, Passed:    37`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2056`, none failed.

- [ ] **Step 7: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PassportPagesTests.cs BatteryPassWeb.Tests/Dpp/ValueWordsTests.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Pages/ValueWords.cs web/Dpp/Documents/DppLinks.cs web/Dpp/DppServiceCollectionExtensions.cs
git commit -m "feat(dpp): a passport's values in words, found by battery ID, passport ID or serial number" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The public summary and the detail page replace today's

Today's passport controller, its three views, the QR code and the search are replaced in place (decision 23). Razor views compile with the build, so a mistake in a view fails like one in C#. 36 older tests read today's files: Step 1's patch moves 21 of them to the new files and removes 15 (judgement call 11).

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs`
- Test (modify): `BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/QrWorkflowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`
- Create: `web/Views/Passport/_PassportHero.cshtml`
- Create: `web/Views/Passport/_PassportTabs.cshtml`
- Modify: `web/Controllers/HomeController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Controllers/QrController.cs`
- Modify: `web/Services/PassportQrCodeService.cs`
- Modify: `web/Views/Dpp/NotAvailable.cshtml`
- Modify: `web/Views/Home/Index.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Modify: `web/Views/Passport/Summary.cshtml`
- Delete: `web/Views/Passport/Battery.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3b-task2-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`, `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`, `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`, `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`, `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`, `BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs`, `BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs`, `BatteryPassWeb.Tests/QrWorkflowTests.cs`, `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`, `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`). Save the block as `../3b-task2-tests.patch`, outside the worktree, and run `git apply ../3b-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -8,13 +8,10 @@ public sealed class AdminFeedbackFollowupTests
     [Fact]
     public void PassportReportHero_ShouldNotShowPassportStatusAbovePassportId()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
 
-        Assert.DoesNotContain("<span class=\"bp-pill bp-pill-muted\">@passport.PassportStatus</span>", summary);
-        Assert.DoesNotContain("<span class=\"bp-pill bp-pill-muted\">@passport.PassportStatus</span>", detail);
-        Assert.Contains("<div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>", summary);
-        Assert.Contains("<div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>", detail);
+        Assert.DoesNotContain("bp-pill bp-pill-muted", hero);
+        Assert.Contains("<div><dt>Passport status</dt><dd>@Model.Text(2)</dd></div>", hero);
     }
 
     [Fact]
@@ -22,16 +19,15 @@ public sealed class AdminFeedbackFollowupTests
     {
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
         var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        var combined = string.Join(Environment.NewLine, registry, batteryTable, summary, detail);
+        var combined = string.Join(Environment.NewLine, registry, batteryTable, hero);
         Assert.Contains("bp-report-action", combined);
         Assert.Contains("data-tooltip=\"Summary report\"", batteryTable);
         Assert.Contains("data-tooltip=\"Detailed report\"", batteryTable);
-        Assert.Contains("View more about this passport", summary);
-        Assert.Contains("Back to summary", detail);
+        Assert.Contains("View more about this passport", hero);
+        Assert.Contains("Back to summary", hero);
         Assert.Contains("bp-report-action-icon", combined);
         Assert.DoesNotContain("<span aria-hidden=\"true\">S</span>", batteryTable);
         Assert.DoesNotContain("<span aria-hidden=\"true\">D</span>", batteryTable);
@@ -42,30 +38,20 @@ public sealed class AdminFeedbackFollowupTests
     [Fact]
     public void ReportActions_ShouldUseHeroCtaWithoutOldHeaderRailLabels()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("bp-report-header", summary);
-        Assert.Contains("bp-report-header", detail);
-        Assert.Contains("bp-report-title-block", summary);
-        Assert.Contains("bp-report-title-block", detail);
-        Assert.Contains("bp-report-hero-cta", summary);
-        Assert.Contains("bp-report-hero-cta", detail);
-        Assert.Contains("View more about this passport", summary);
-        Assert.Contains("Back to summary", detail);
-        Assert.DoesNotContain("bp-report-header-actions", summary);
-        Assert.DoesNotContain("bp-report-header-actions", detail);
-        Assert.DoesNotContain("bp-report-action-strip", summary);
-        Assert.DoesNotContain("bp-report-action-strip", detail);
-        Assert.DoesNotContain("bp-report-action-label", summary);
-        Assert.DoesNotContain("bp-report-action-label", detail);
-        Assert.DoesNotContain(">Latest</span>", summary);
-        Assert.DoesNotContain(">Latest</span>", detail);
-        Assert.DoesNotContain(">Detailed report</span>", summary);
-        Assert.DoesNotContain(">Summary report</span>", detail);
-        Assert.Contains("bp-latest-passport-action-icon", summary);
-        Assert.Contains("bp-latest-passport-action-icon", detail);
+        Assert.Contains("bp-report-header", hero);
+        Assert.Contains("bp-report-title-block", hero);
+        Assert.Contains("bp-report-hero-cta", hero);
+        Assert.Contains("View more about this passport", hero);
+        Assert.Contains("Back to summary", hero);
+        Assert.DoesNotContain("bp-report-header-actions", hero);
+        Assert.DoesNotContain("bp-report-action-strip", hero);
+        Assert.DoesNotContain("bp-report-action-label", hero);
+        Assert.DoesNotContain(">Latest</span>", hero);
+        Assert.DoesNotContain(">Detailed report</span>", hero);
+        Assert.Contains("bp-latest-passport-action-icon", hero);
         Assert.Contains(".bp-report-title-block", css);
         Assert.Contains(".bp-report-hero-cta", css);
         Assert.Contains("font-weight: 400;", css);
@@ -75,23 +61,19 @@ public sealed class AdminFeedbackFollowupTests
     [Fact]
     public void PassportReportHero_ShouldUseApprovedSplitHeaderDesign()
     {
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
+        var tabs = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
         var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("bp-report-hero-card", summary);
-        Assert.Contains("bp-report-hero-card", detail);
+        Assert.Contains("bp-report-hero-card", hero);
         Assert.Contains("bp-report-wide-section", summary);
-        Assert.Contains("bp-report-wide-section", detail);
-        Assert.Contains("bp-report-hero-layout", summary);
-        Assert.Contains("bp-report-hero-layout", detail);
-        Assert.Contains("bp-report-hero-media", summary);
-        Assert.Contains("bp-report-hero-media", detail);
-        Assert.Contains("View more about this passport", summary);
-        Assert.Contains("Back to summary", detail);
-        Assert.Contains("bp-report-hero-qr", summary);
-        Assert.Contains("bp-report-hero-qr", detail);
-        Assert.DoesNotContain("QR code for battery ID", summary);
+        Assert.Contains("bp-report-wide-section", tabs);
+        Assert.Contains("bp-report-hero-layout", hero);
+        Assert.Contains("bp-report-hero-media", hero);
+        Assert.Contains("View more about this passport", hero);
+        Assert.Contains("Back to summary", hero);
+        Assert.Contains("bp-report-hero-qr", hero);
         Assert.Contains(".bp-report-hero-card", css);
         Assert.Contains(".bp-report-wide-section", css);
         Assert.Contains("width: min(1860px, calc(100vw - 96px));", css);
@@ -187,7 +169,7 @@ public sealed class AdminFeedbackFollowupTests
         var home = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));
         var search = File.ReadAllText(RepoFile("web", "Views", "Home", "Search.cshtml"));
 
-        Assert.Contains("No active public battery passport was found", home);
+        Assert.Contains("No published battery passport was found", home);
         Assert.Contains("No active public battery passport was found", search);
         Assert.DoesNotContain("No published, verified battery passport ID was found", home);
         Assert.DoesNotContain("No published, verified battery passport ID was found", search);
@@ -197,31 +179,24 @@ public sealed class AdminFeedbackFollowupTests
     public void ReportCharts_ShouldUseTableLegendsAndOriginalPowerHeading()
     {
         var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
 
-        Assert.Contains("<h3 class=\"bp-subheading\">Original Power</h3>", summary);
+        Assert.Contains("<h3 class=\"bp-subheading\">Original power capability</h3>", summary);
         Assert.Contains("bp-chart-value-legend", summary);
-        Assert.Contains("bp-chart-value-legend", detail);
-        Assert.Contains("filter(row => Number.isFinite", detail);
     }
 
     [Fact]
     public void FollowupReportHeader_ShouldUseFixedThreeColumnIdentityGrid()
     {
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        foreach (var view in new[] { summary, detail })
-        {
-            Assert.Contains("bp-report-identity-grid", view);
-            Assert.Contains("bp-report-identity-panel--battery", view);
-            Assert.Contains("bp-report-identity-panel--identifiers", view);
-            Assert.Contains("bp-report-identity-panel--qr", view);
-            Assert.DoesNotContain("bp-report-media-code-row", view);
-            Assert.Contains("CultureInfo.InvariantCulture", view);
-        }
-
+        Assert.Contains("bp-report-identity-grid", hero);
+        Assert.Contains("bp-report-identity-panel--battery", hero);
+        Assert.Contains("bp-report-identity-panel--identifiers", hero);
+        Assert.Contains("bp-report-identity-panel--qr", hero);
+        Assert.DoesNotContain("bp-report-media-code-row", hero);
+        Assert.Contains("CultureInfo.InvariantCulture", summary);
         Assert.Contains("grid-template-columns: minmax(220px, 1fr) minmax(280px, 1.2fr) minmax(160px, 220px);", css);
         Assert.Contains(".bp-report-identity-panel--qr", css);
         Assert.Contains("@media (max-width: 1180px)", css);
@@ -282,20 +257,6 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("::-ms-clear", css);
     }
 
-    [Fact]
-    public void FollowupPerformanceCharts_ShouldUseEqualBoundedChartGrid()
-    {
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-telemetry-chart-grid", detail);
-        Assert.Contains("bp-telemetry-chart-card", detail);
-        Assert.Contains("maintainAspectRatio: false", detail);
-        Assert.Contains(".bp-telemetry-chart-grid", css);
-        Assert.Contains(".bp-telemetry-chart-card", css);
-        Assert.Contains("height: clamp(220px, 28vw, 300px);", css);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -108,17 +108,11 @@ public sealed class AdminTestFeedbackFixesTests
     [Fact]
     public void Reports_ShouldUseResponsiveSharedHeaderColorsAndBoundedTelemetryCharts()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-        var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));
-
-        Assert.Contains("bp-summary-identity-grid", summary);
-        Assert.Contains("bp-summary-identity-grid", detail);
-        Assert.Contains("bp-report-identity-panel--qr", summary);
-        Assert.Contains("bp-report-identity-panel--qr", detail);
-        Assert.Contains("CarbonStageColor", factory);
-        Assert.Contains("bp-chart-frame", detail);
+
+        Assert.Contains("bp-summary-identity-grid", hero);
+        Assert.Contains("bp-report-identity-panel--qr", hero);
         Assert.Contains("height: clamp(220px, 28vw, 300px)", css);
         Assert.Contains("overflow: hidden", css);
         Assert.Contains("grid-template-columns: repeat(auto-fit", css);
diff --git a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
--- a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
+++ b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
@@ -6,57 +6,6 @@ public sealed class BatteryRouteAndRegistryTests
 {
     private static readonly string TestSourceFile = CurrentSourceFile();
 
-    [Fact]
-    public void PublicRoutes_ShouldResolveBatteryIdLatestAndPassportId()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
-        var resolver = File.ReadAllText(RepoFile("web", "Services", "BatteryRouteResolutionService.cs"));
-
-        Assert.Contains("[HttpGet(\"{id}\")]", controller);
-        Assert.Contains("[HttpGet(\"{batteryId}/latest\")]", controller);
-        Assert.Contains("ResolveAsync", resolver);
-        Assert.Contains("GetByBatteryIdAsync", resolver);
-        Assert.Contains("GetByPassportIdAsync", resolver);
-        Assert.Contains("BatteryFirst", resolver);
-    }
-
-    [Fact]
-    public void HomeSearch_ShouldSendBatteryIdsToLatestAndAnonymousPassportIdsToSummary()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
-
-        Assert.Contains("BatteryRouteResolutionService", source);
-        Assert.Contains("Battery", source);
-        Assert.Contains("Passport", source);
-        Assert.Contains("var resolvedBatteryId = BsonHelpers.GetString(resolution.Document!, \"batteryId\");", source);
-        Assert.Contains("var routeBatteryId = string.IsNullOrWhiteSpace(resolvedBatteryId) ? query : resolvedBatteryId;", source);
-        Assert.Contains("/{Uri.EscapeDataString(routeBatteryId)}/latest", source);
-        Assert.Contains("{passportPath}/summary", source);
-    }
-
-    [Fact]
-    public void LatestRoute_ShouldReturnToSearchNotFoundInsteadOfRawBrowser404()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
-
-        Assert.Contains("RedirectToSearchNotFound(decodedBatteryId)", controller);
-        Assert.Contains("private IActionResult RedirectToSearchNotFound(string query)", controller);
-        Assert.Contains("RedirectToAction(\"Index\", \"Home\"", controller);
-        Assert.Contains("notFound = \"1\"", controller);
-    }
-
-    [Fact]
-    public void QrService_ShouldBuildStableBatteryLatestPayload()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
-
-        Assert.Contains("BuildPayloadUrl(HttpRequest request, string batteryId)", source);
-        Assert.Contains("/latest", source);
-        Assert.Contains("BatteryRepository", controller);
-        Assert.Contains("batteryId", controller);
-    }
-
     [Fact]
     public void Registry_ShouldUseBatteryRowsAndPassportCounts()
     {
@@ -99,66 +48,40 @@ public sealed class BatteryRouteAndRegistryTests
     [Fact]
     public void PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-        var combined = summary + Environment.NewLine + detail;
-
-        Assert.Contains("Battery ID", combined);
-        Assert.Contains("Passport ID", combined);
-        Assert.Contains("Historical passport", combined);
-        Assert.Contains("Latest passport", combined);
-        Assert.Contains("Battery Model", combined);
-        Assert.DoesNotContain("Battery version", combined);
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
+
+        Assert.Contains("Battery ID", hero);
+        Assert.Contains("Passport ID", hero);
+        Assert.Contains(">Archived<", hero);
+        Assert.Contains(">Active<", hero);
+        Assert.Contains("Battery model", hero);
+        Assert.DoesNotContain("Battery version", hero);
     }
 
     [Fact]
     public void PassportReports_ShouldUseSerialHeaderAndIconLatestAction()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-        var combined = summary + detail;
-
-        Assert.Contains("passport.BatterySerialNumber", summary);
-        Assert.Contains("passport.BatterySerialNumber", detail);
-        Assert.DoesNotContain(">Open latest passport<", combined);
-        Assert.Contains("aria-label=\"Open latest passport\"", combined);
-        Assert.Contains("bp-latest-passport-action-icon", combined);
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
+
+        Assert.Contains("<h1>@Model.Text(11)</h1>", hero);
+        Assert.DoesNotContain(">Open latest passport<", hero);
+        Assert.Contains("aria-label=\"Open the current version\"", hero);
+        Assert.Contains("bp-latest-passport-action-icon", hero);
     }
 
     [Fact]
     public void PassportSummaryLockedDetail_ShouldReplaceHeroCtaAndReturnAfterLogin()
     {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
         var loginController = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
-        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportSummaryPageViewModel.cs"));
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-
-        Assert.Contains("public bool CanOpenDetail", model);
-        Assert.Contains("public string DetailActionUrl", model);
-        Assert.Contains("CanOpenDetail = canOpenDetail", controller);
-        Assert.Contains("DetailActionUrl = BuildDetailActionUrl(", controller);
-        Assert.Contains("switchAccount=1", controller);
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
+
         Assert.Contains("[FromQuery] bool switchAccount", loginController);
         Assert.Contains("&& !switchAccount", loginController);
-        Assert.DoesNotContain("<p class=\"bp-warning mt-3\">@Model.DetailAccessNotice</p>", summary);
-        Assert.Contains("if (Model.CanOpenDetail)", summary);
-        Assert.Contains("href=\"@Model.DetailActionUrl\">View more about this passport", summary);
-        Assert.Contains("bp-report-hero-cta bp-report-hero-cta-warning", summary);
-        Assert.Contains("@Model.DetailAccessNotice", summary);
-    }
-
-    [Fact]
-    public void BatteryLevelView_ShouldShowBatteryIdLabelAndIconActions()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Passport", "Battery.cshtml"));
-
-        Assert.Contains("Battery ID:", view);
-        Assert.Contains("Passport ID", view);
-        Assert.Contains("Latest", view);
-        Assert.Contains("Historical", view);
-        Assert.Contains("aria-label=\"Summary report\"", view);
-        Assert.Contains("aria-label=\"Detailed report\"", view);
-        Assert.Contains("bp-report-action-icon", view);
+        Assert.Contains("Model.ReaderSeesMore", hero);
+        Assert.Contains("href=\"@detailHref\">View more about this passport", hero);
+        Assert.Contains("bp-report-hero-cta bp-report-hero-cta-warning", hero);
+        Assert.Contains("&switchAccount=true", hero);
+        Assert.Contains("/login?next=", hero);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
--- a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
@@ -200,18 +200,6 @@ public sealed class ConformanceLayoutTests
         Assert.Contains("TrustWarningCount", source);
     }
 
-    [Fact]
-    public void PassportSummary_ShouldHideTrustStateWhileDetailConditionallyShowsItInTrustTab()
-    {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-
-        Assert.DoesNotContain("passport.TrustState", summary);
-        Assert.Contains("Model.CanViewTrustConformance", detail);
-        Assert.Contains("tab-trust", detail);
-        Assert.Contains("passport.TrustState", detail);
-    }
-
     [Fact]
     public void AdminPages_ShouldLinkToConformance()
     {
diff --git a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
--- a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
+++ b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
@@ -29,16 +29,12 @@ public sealed class CopyIdButtonLayoutTests
     [Fact]
     public void PassportReports_ShouldRenderCopyButtonsForPassportAndBatteryIds()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-        var combined = summary + Environment.NewLine + detail;
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
 
-        Assert.Contains("Passport ID:", summary);
-        Assert.Contains("Battery ID:", summary);
-        Assert.Contains("Passport ID:", detail);
-        Assert.Contains("Battery ID:", detail);
-        Assert.True(CountOccurrences(combined, "_CopyIdButton") >= 4);
-        Assert.True(CountOccurrences(combined, "bp-id-copy-row") >= 4);
+        Assert.Contains("Passport ID:", hero);
+        Assert.Contains("Battery ID:", hero);
+        Assert.True(CountOccurrences(hero, "_CopyIdButton") >= 2);
+        Assert.True(CountOccurrences(hero, "bp-id-copy-row") >= 2);
     }
 
     [Fact]
@@ -50,7 +46,6 @@ public sealed class CopyIdButtonLayoutTests
 
         var files = new[]
         {
-            RepoFile("web", "Views", "Passport", "Battery.cshtml"),
             RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
             RepoFile("web", "Views", "Admin", "Conformance.cshtml"),
             RepoFile("web", "Views", "Admin", "Audit.cshtml"),
diff --git a/BatteryPassWeb.Tests/DocumentAccessControlTests.cs b/BatteryPassWeb.Tests/DocumentAccessControlTests.cs
--- a/BatteryPassWeb.Tests/DocumentAccessControlTests.cs
+++ b/BatteryPassWeb.Tests/DocumentAccessControlTests.cs
@@ -160,15 +160,6 @@ public sealed class DocumentAccessControlTests
         Assert.Contains("source\"] = \"productTemplate\"", productTemplateService);
     }
 
-    [Fact]
-    public void PassportSummary_ShouldOnlyShowWrongClusterNoticeWhenDetailAccessIsDenied()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
-
-        Assert.Contains("if (!canOpenDetail && string.Equals(access, \"wrong-cluster\"", controller);
-        Assert.DoesNotContain("string.Equals(access, \"wrong-cluster\", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(detailAccessNotice)", controller);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs b/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiInitializerTests.cs
@@ -139,20 +139,6 @@ public sealed class ExternalApiInitializerTests
         Assert.Contains("? ExternalApiInitializer.SampleLifecycleTokenValue", helpController);
     }
 
-    [Fact]
-    public void LandingSearchSample_ShouldUseGeneratedDemoBatteryIdInsteadOfLegacyDid()
-    {
-        var homeController = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
-        var landing = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));
-
-        Assert.Contains("BatteryIdService", homeController);
-        Assert.Contains("ExternalApiInitializer.CreateSampleBatteryId", homeController);
-        Assert.DoesNotContain("sample-customer-north-001", homeController);
-        Assert.Contains("sample battery ID", landing);
-        Assert.Contains("public sample passport", landing, StringComparison.OrdinalIgnoreCase);
-        Assert.Contains("return Redirect($\"/{Uri.EscapeDataString(routeBatteryId)}/latest\")", homeController);
-    }
-
     [Fact]
     public void LegacySampleDid_ShouldResolveToGeneratedDemoBattery()
     {
diff --git a/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs b/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs
--- a/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs
+++ b/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs
@@ -21,16 +21,6 @@ public sealed class ManualFeedbackRound3Tests
         Assert.Contains("ProductVersions", source);
     }
 
-    [Fact]
-    public void AnonymousSampleDetail_ShouldRedirectToSummaryWithAccessMessageInsteadOfNotFound()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
-
-        Assert.Contains("RedirectToPublicSummaryWithAccessNotice", source);
-        Assert.Contains("FallbackSamplePassport(decodedPassportId)", source);
-        Assert.Contains("access=detail-required", source);
-    }
-
     [Fact]
     public void SummaryReport_ShouldStackMainMetricRowsVertically()
     {
diff --git a/BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs b/BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs
--- a/BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs
+++ b/BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs
@@ -45,39 +45,13 @@ public sealed class PassportSoftwarePresentationTests
         Assert.Equal("1.0", passport.ProductVersion);
     }
 
-    [Fact]
-    public void PassportSummary_ShouldIncludeSoftwareVersion()
-    {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-
-        Assert.Contains("Battery Model", summary);
-        Assert.Contains("passport.BatteryModel", summary);
-        Assert.Contains("Software version", summary);
-        Assert.Contains("passport.SoftwareVersion", summary);
-    }
-
-    [Fact]
-    public void PassportDetail_ShouldRenderSoftwareParametersInGeneralTab()
-    {
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-
-        Assert.DoesNotContain("data-bs-target=\"#tab-software\"", detail);
-        Assert.DoesNotContain("id=\"tab-software\"", detail);
-        Assert.Contains("passport.BatteryFamily", detail);
-        Assert.Contains("passport.BatteryModel", detail);
-        Assert.Contains("passport.SoftwareVersion", detail);
-        Assert.Contains("passport.SoftwareReleaseDate", detail);
-        Assert.Contains("passport.SoftwareLatestUpdate", detail);
-        Assert.DoesNotContain("bp-software-panel", detail);
-    }
-
     [Fact]
     public void PassportDetailTabs_ShouldStayOnSingleScrollableRow()
     {
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
+        var tabs = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("class=\"nav nav-tabs bp-tabs\"", detail);
+        Assert.Contains("class=\"nav nav-tabs bp-tabs\"", tabs);
         Assert.Contains("flex-wrap: nowrap;", css);
         Assert.Contains("overflow-x: auto;", css);
         Assert.Contains(".bp-tabs .nav-item", css);
@@ -85,15 +59,6 @@ public sealed class PassportSoftwarePresentationTests
         Assert.Contains("white-space: nowrap;", css);
     }
 
-    [Fact]
-    public void PassportDetailGeneralTab_ShouldRenderCurrentSoftwareVersionAsCompactField()
-    {
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-
-        Assert.Contains("<div><dt>Software version</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareVersion) ? \"-\" : passport.SoftwareVersion)</dd></div>", detail);
-        Assert.DoesNotContain("bp-software-version-card", detail);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/QrWorkflowTests.cs b/BatteryPassWeb.Tests/QrWorkflowTests.cs
--- a/BatteryPassWeb.Tests/QrWorkflowTests.cs
+++ b/BatteryPassWeb.Tests/QrWorkflowTests.cs
@@ -20,15 +20,11 @@ public sealed class QrWorkflowTests
         var source = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
 
         Assert.Contains("[Route(\"qr\")]", source);
-        Assert.Contains("[HttpGet(\"{batteryId}/svg\")]", source);
-        Assert.Contains("[HttpGet(\"{batteryId}/download\")]", source);
+        Assert.Contains("[HttpGet(\"{id}/svg\")]", source);
+        Assert.Contains("[HttpGet(\"{id}/download\")]", source);
         Assert.Contains("PassportQrCodeService", source);
-        Assert.Contains("PassportPublishPolicyService", source);
-        Assert.Contains("BatteryRepository", source);
-        Assert.Contains("CanOpenPassportDetailAsync", source);
+        Assert.Contains("links.PassportUrl(battery.Identifier)", source);
         Assert.Contains("image/svg+xml", source);
-        Assert.Contains("BuildPayloadUrl", source);
-        Assert.Contains("CreateSampleBatteryId", source);
     }
 
     [Fact]
@@ -41,8 +37,8 @@ public sealed class QrWorkflowTests
         Assert.Contains("SvgQRCode", source);
         Assert.Contains("AddBatteryCellMark", source);
         Assert.Contains("bp-qr-battery-mark", source);
-        Assert.Contains("/latest", source);
-        Assert.Contains("BuildPayloadUrl", source);
+        Assert.DoesNotContain("/latest", source);
+        Assert.DoesNotContain("HttpRequest", source);
         Assert.Contains("BuildFileName", source);
     }
 
@@ -60,39 +56,31 @@ public sealed class QrWorkflowTests
     [Fact]
     public void PassportSummary_ShouldShowDownloadableQrBesideBatteryImage()
     {
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
         var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
 
-        Assert.Contains("bp-report-hero-qr", summary);
-        Assert.Contains("bp-report-hero-qr", detail);
-        Assert.Contains("bp-report-hero-media", summary);
-        Assert.Contains("bp-report-hero-media", detail);
-        Assert.Contains("/qr/", summary);
-        Assert.Contains("/qr/", detail);
-        Assert.Contains("qrBatteryId", summary);
-        Assert.Contains("qrBatteryId", detail);
-        Assert.Contains("/svg", summary);
-        Assert.Contains("/svg", detail);
-        Assert.Contains("/download", summary);
-        Assert.Contains("/download", detail);
-        Assert.DoesNotContain("@passport.BatteryImageAlt</p>", summary);
-        Assert.DoesNotContain("Download QR", summary);
-        Assert.DoesNotContain("QR code for battery ID", summary);
+        Assert.Contains("<partial name=\"_PassportHero\"", summary);
+        Assert.Contains("<partial name=\"_PassportHero\"", detail);
+        Assert.Contains("bp-report-hero-qr", hero);
+        Assert.Contains("bp-report-hero-media", hero);
+        Assert.Contains("QrHref", hero);
+        Assert.Contains("QrDownloadHref", hero);
+        Assert.DoesNotContain("Download QR", hero);
         Assert.Contains(".bp-report-hero-qr", File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css")));
-        Assert.DoesNotContain("QR access", detail);
-        Assert.DoesNotContain("Download QR", detail);
+        Assert.DoesNotContain("QR access", hero);
     }
 
     [Fact]
     public void SummaryReport_ShouldUseThreeColumnHeaderAndImageQrDownload()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var qrController = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("bp-summary-identity-grid", summary);
-        Assert.Contains("bp-summary-id-panel", summary);
-        Assert.Contains("bp-summary-qr-panel", summary);
+        Assert.Contains("bp-summary-identity-grid", hero);
+        Assert.Contains("bp-summary-id-panel", hero);
+        Assert.Contains("bp-summary-qr-panel", hero);
         Assert.Contains("image/png", qrController);
         Assert.Contains(".bp-summary-identity-grid", css);
         Assert.Contains("@media (max-width: 900px)", css);
@@ -160,36 +148,19 @@ public sealed class QrWorkflowTests
         Assert.Contains("Apache License", license);
     }
 
-    [Fact]
-    public void QrService_ShouldBuildPublicSummaryPayloadForDemoScenarioPassport()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
-
-        Assert.Contains("/latest/summary", source);
-        Assert.Contains("BuildPayloadUrl", source);
-        Assert.Contains("GetLatestPublicByBatteryIdAsync", controller);
-        Assert.Contains("CanOpenPassportDetailAsync", controller);
-    }
-
     [Fact]
     public void FollowupQrWorkflow_ShouldRenderInsideReportIdentityHeader()
     {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
 
-        foreach (var view in new[] { summary, detail })
-        {
-            Assert.Contains("bp-report-identity-grid", view);
-            Assert.Contains("bp-report-identity-panel--qr", view);
-            Assert.Contains("/qr/@Uri.EscapeDataString(qrBatteryId)/download", view);
-            Assert.Contains("/qr/@Uri.EscapeDataString(qrBatteryId)/svg", view);
-            Assert.DoesNotContain("bp-report-media-code-row", view);
-        }
-
+        Assert.Contains("bp-report-identity-grid", hero);
+        Assert.Contains("bp-report-identity-panel--qr", hero);
+        Assert.Contains("PassportPages.QrDownloadHref(Model.Identifier)", hero);
+        Assert.Contains("PassportPages.QrHref(Model.Identifier)", hero);
+        Assert.DoesNotContain("bp-report-media-code-row", hero);
         Assert.Contains("image/png", controller);
-        Assert.Contains("GetLatestPublicByBatteryIdAsync", controller);
+        Assert.Contains("LocateAsync", controller);
     }
 
     private static string RepoFile(params string[] parts)
diff --git a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
@@ -109,34 +109,18 @@ public sealed class SigningWorkflowLayoutTests
     public void PassportSummaryShouldHideTrustDataAndDetailShouldRenderPrivilegedTrustTab()
     {
         var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
-        var detailModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportDetailViewModel.cs"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
+        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
 
         Assert.DoesNotContain("Public verification", summary);
-        Assert.DoesNotContain("passport.Trust", summary);
-        Assert.DoesNotContain("passport.IsValid", summary);
+        Assert.DoesNotContain("SignatureHolds", summary);
         Assert.DoesNotContain("Conformance", summary);
-        Assert.DoesNotContain("Verified", summary);
-        Assert.DoesNotContain("Unverified", summary);
-        Assert.Contains("CanViewTrustConformance", detailModel);
+        Assert.Contains("@if (detail)", hero);
+        Assert.Contains("<div><dt>Signature</dt><dd>@(Model.SignatureHolds ? \"Verified\" : \"Doesn't match the signed passport\")</dd></div>", hero);
         Assert.Contains("CanViewTrustConformanceAsync", access);
         Assert.Contains("if (IsAdmin(user))", access);
         Assert.Contains("GetAdministeredClusterIdsForUserAsync", access);
         Assert.Contains("managedClusterIds.Contains", access);
-        Assert.Contains("CanViewTrustConformanceAsync(User, passport.ClusterId", controller);
-        Assert.Contains("Model.CanViewTrustConformance", detail);
-        Assert.Contains("Trust & conformance", detail);
-        Assert.Contains("data-bs-target=\"#tab-trust\"", detail);
-        Assert.Contains("id=\"tab-trust\"", detail);
-        Assert.Contains("Verification evidence", detail);
-        Assert.Contains("passport.TrustLatestHash", detail);
-        Assert.Contains("passport.TrustIssuer", detail);
-        Assert.Contains("passport.TrustVerificationMethod", detail);
-        Assert.Contains("passport.TrustLatestRevisionId", detail);
-        Assert.Contains("passport.TrustProofStatus", detail);
-        Assert.Contains("passport.TrustState", detail);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
--- a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
@@ -57,40 +57,23 @@ public sealed class ValidationPolicyLayoutTests
         var home = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
         var registry = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
         var passport = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
+        var pages = File.ReadAllText(RepoFile("web", "Dpp", "Pages", "PassportPages.cs"));
 
-        Assert.Contains("BatteryRouteResolutionService", home);
-        Assert.Contains("ResolveAsync", home);
-        Assert.Contains("BatteryRouteTargetKind.Battery", home);
-        Assert.Contains("BatteryRouteTargetKind.Passport", home);
+        Assert.Contains("pages.LocateAsync", home);
 
         Assert.Contains("PassportPublishPolicyService", registry);
         Assert.Contains("SearchDocumentsAsync", registry);
         Assert.Contains("CanOpenPassportSummaryAsync", registry);
 
-        Assert.Contains("PassportPublishPolicyService", passport);
-        Assert.Contains("CanOpenPassportSummaryAsync", passport);
-        Assert.Contains("CanOpenPassportDetailAsync", passport);
-        Assert.Contains("return NotFound();", passport);
+        Assert.Contains("pages.SummaryAsync", passport);
+        Assert.Contains("pages.DetailAsync", passport);
+        Assert.Contains("PassportFilter.Cut", pages);
+        Assert.Contains("reader.VersionAsync", pages);
 
         var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
         Assert.Contains("IsPubliclyVisible", access);
     }
 
-    [Fact]
-    public void HomeSearch_ShouldLetGeneralAdminsFindDraftAndUnpublishedPassports()
-    {
-        var home = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
-        var searchView = File.ReadAllText(RepoFile("web", "Views", "Home", "Search.cshtml"));
-        var searchModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "SearchPageViewModel.cs"));
-
-        Assert.Contains("ExtractIdFromQrPayload", home);
-        Assert.Contains("BatteryRouteResolutionService", home);
-        Assert.Contains("Redirect($\"/{Uri.EscapeDataString(routeBatteryId)}/latest\")", home);
-        Assert.Contains("IsAdminSearch", searchModel);
-        Assert.Contains("Model.IsAdminSearch", searchView);
-        Assert.Contains("No battery passport ID was found", searchView);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
```

`BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The passport pages, the QR code and the home page's search (step 3b), in place of today's: /dpp/{id} for everyone,
/// /dpp/{id}/detail for signed-in readers.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportControllerTests(MongoFixture mongo)
{
    private const string OwnClusterUser = "user@example.com";

    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", $"{name}.cshtml"));

    [Fact]
    public void ThePages_ShouldBeOpenToEveryoneAndOnlyRead()
    {
        var type = typeof(PassportController);

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("dpp", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("{id}", type.GetMethod(nameof(PassportController.Summary))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("{id}/detail", type.GetMethod(nameof(PassportController.Detail))!.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(typeof(QrController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Passport", "Summary.cshtml"))!, "Battery.cshtml")));
    }

    [Fact]
    public async Task TheSummary_ShouldOpenWithoutSigningInByTheBatteryIdAsTheQrCodeCarriesIt()
    {
        var rig = await RigAsync();

        var view = Assert.IsType<ViewResult>(await Pages(rig, null).Summary(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), CancellationToken.None));

        var page = Assert.IsType<PassportPage>(view.Model);
        Assert.Null(view.ViewName);
        Assert.Empty(page.Audiences);
        Assert.False(page.ReaderSeesMore);
        Assert.All(page.Sections.SelectMany(section => section.Values), value => Assert.Equal(Audience.Public, value.Attribute.Audience));
    }

    [Fact]
    public async Task AnUnknownIdOrADraft_ShouldSayNotAvailable()
    {
        var draft = await PublicationTesting.RigAsync(mongo);

        foreach (var result in new[]
        {
            await Pages(draft, null).Summary(PassportPagesTests.Identifier(draft), CancellationToken.None),
            await Pages(draft, null).Summary("VPC8C05-0000", CancellationToken.None),
            await Pages(draft, "team@example.com", AccessControlService.RoleAdmin).Detail(PassportPagesTests.Identifier(draft), CancellationToken.None),
        })
        {
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("~/Views/Dpp/NotAvailable.cshtml", view.ViewName);
            Assert.Equal(StatusCodes.Status404NotFound, view.StatusCode);
            Assert.Equal("passport", view.ViewData["Subject"]);
        }
    }

    [Fact]
    public async Task TheDetailPage_ShouldAskToSignInFirst()
    {
        var rig = await RigAsync();

        Assert.IsType<ChallengeResult>(await Pages(rig, null).Detail(PassportPagesTests.Identifier(rig), CancellationToken.None));
        Assert.IsType<ChallengeResult>(await Pages(rig, null).Detail("no-such-passport", CancellationToken.None));
    }

    [Fact]
    public async Task TheDetailPage_ShouldShowTheBatterysClusterItsSectionsAndStayOutOfCaches()
    {
        var rig = await RigAsync();
        var controller = Pages(rig, OwnClusterUser);

        var view = Assert.IsType<ViewResult>(await controller.Detail(PassportPagesTests.Identifier(rig), CancellationToken.None));

        var page = Assert.IsType<PassportPage>(view.Model);
        Assert.Equal(["purchaser", "shared"], page.Audiences.Select(section => section.Key));
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task AnotherClustersUser_ShouldGetThePublicDataOnly()
    {
        var rig = await RigAsync();

        var page = Assert.IsType<PassportPage>(Assert.IsType<ViewResult>(await Pages(rig, "other@example.com").Detail(PassportPagesTests.Identifier(rig), CancellationToken.None)).Model);

        Assert.Empty(page.Audiences);
        Assert.False(page.ReaderSeesMore);
    }

    [Fact]
    public async Task TheQrCode_ShouldBeDrawnForAPublishedBatteryOnly()
    {
        var rig = await RigAsync();
        var draft = await PublicationTesting.RigAsync(mongo);

        var svg = Assert.IsType<ContentResult>(await Qr(rig).QrCode("VPC8C05-6788", CancellationToken.None));
        var png = Assert.IsType<FileContentResult>(await Qr(rig).Download(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), CancellationToken.None));

        Assert.StartsWith("image/svg+xml", svg.ContentType, StringComparison.Ordinal);
        Assert.Contains("<svg", svg.Content, StringComparison.Ordinal);
        Assert.Equal("image/png", png.ContentType);
        Assert.Equal("VPC8C05-6788-qr.png", png.FileDownloadName);
        Assert.IsType<NotFoundResult>(await Qr(draft).QrCode(PassportPagesTests.Identifier(draft), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await Qr(rig, DocumentTesting.NoLinks).QrCode("VPC8C05-6788", CancellationToken.None));
    }

    [Theory]
    [InlineData("https://passport.example.com/dpp/urn:x-battery-pass:TEST-0f3a", "urn:x-battery-pass:TEST-0f3a")]
    [InlineData("https://passport.example.com/dpp/urn%3Ax-battery-pass%3ATEST-0f3a/detail", "urn:x-battery-pass:TEST-0f3a")]
    [InlineData("urn:x-battery-pass:TEST-0f3a", "urn:x-battery-pass:TEST-0f3a")]
    [InlineData(" VPC8C05-6788 ", "VPC8C05-6788")]
    [InlineData("https://passport.example.com/?q=VPC8C05-6788", "VPC8C05-6788")]
    [InlineData("https://passport.example.com/BAT-1/latest/summary", "BAT-1")]
    [InlineData("", "")]
    public void TheSearch_ShouldReadTheIdOutOfWhatWasTypedOrScanned(string typed, string expected)
    {
        Assert.Equal(expected, HomeController.IdFromSearch(typed));
    }

    [Fact]
    public async Task TheSearch_ShouldOpenTheSummaryOrWhenSignedInTheDetailPage()
    {
        var rig = await RigAsync();
        var identifier = PassportPagesTests.Identifier(rig);

        var anonymous = Assert.IsType<RedirectResult>(await Home(rig, null).Search("VPC8C05-6788", CancellationToken.None));
        var signedIn = Assert.IsType<RedirectResult>(await Home(rig, OwnClusterUser).Search($"{DocumentTesting.BaseUrl}{PassportPages.SummaryHref(identifier)}", CancellationToken.None));
        var unknown = Assert.IsType<RedirectToActionResult>(await Home(rig, null).Search("VPC8C05-0000", CancellationToken.None));

        Assert.Equal(PassportPages.SummaryHref(identifier), anonymous.Url);
        Assert.Equal(PassportPages.DetailHref(identifier), signedIn.Url);
        Assert.Equal("Index", unknown.ActionName);
        Assert.Equal("1", unknown.RouteValues!["notFound"]);
    }

    [Fact]
    public async Task TheSearch_ShouldOpenAnArchivedVersionAtItsOwnPassportId()
    {
        var rig = await RigAsync();
        var first = (await rig.Store.GetActiveVersionAsync(rig.PackId))!;
        await rig.Pipeline.SaveAsync(
            PublicationTesting.Team,
            new ChangeSet(TargetKind.Model, "model-test", new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonValue.Create(520) }, "form"));

        var result = Assert.IsType<RedirectResult>(await Home(rig, null).Search(first.DigitalProductPassportId, CancellationToken.None));

        Assert.Equal(PassportPages.SummaryHref(first.DigitalProductPassportId), result.Url);
    }

    [Fact]
    public void TheViews_ShouldKeepTodaysLookWithTheNewData()
    {
        var hero = View("_PassportHero");
        var tabs = View("_PassportTabs");
        var summary = View("Summary");
        var detail = View("Detail");

        Assert.DoesNotContain("Cluster", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("Battery status", hero, StringComparison.Ordinal);
        Assert.Contains("Test passport: this battery's IDs start with TEST-.", hero, StringComparison.Ordinal);
        Assert.Contains("Model.PictureHref", hero, StringComparison.Ordinal);
        Assert.Contains("Open the current version", hero, StringComparison.Ordinal);
        Assert.Contains("Model.Audiences", tabs, StringComparison.Ordinal);
        Assert.Contains("@(origin.Measured ? \"Measured\" : \"Entered\") @ValueWords.Time(origin.GeneratedAtUtc) · @PassportPages.SourceWord(origin.Source)", tabs, StringComparison.Ordinal);
        Assert.Contains("are on the <a href=\"@PassportPages.DetailHref(Model.Identifier)\">current version</a>", tabs, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Detail\"] = false;", summary, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"Detail\"] = true;", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("chart.js", summary + detail, StringComparison.OrdinalIgnoreCase);
        var links = Regex.Matches(tabs, "<a href=\"@part.Href\"[^>]*>").Select(match => match.Value).ToArray();
        Assert.All(links, link => Assert.Contains("target=\"_blank\" rel=\"noreferrer\"", link, StringComparison.Ordinal));
        Assert.NotEmpty(links);
    }

    [Fact]
    public void TheHomePage_ShouldSearchTheNewPassportsWithoutTheOldSample()
    {
        var home = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Home", "Index.cshtml"));

        Assert.DoesNotContain("sample", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DID", home, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Serial number, battery ID or passport ID\"", home, StringComparison.Ordinal);
    }

    /// <summary>The seed pack, published, with a member of its cluster and a member of another cluster.</summary>
    private async Task<PublicationRig> RigAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(OwnClusterUser, BatteryTesting.North, "member");
        await clusters.UpsertClusterMembershipAsync("other@example.com", BatteryTesting.South, "member");
        return rig;
    }

    private static HttpContext Http(string? email, string role) => new DefaultHttpContext
    {
        User = email is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
    };

    private static PassportController Pages(PublicationRig rig, string? email, string role = "member") =>
        new(new DppReaders(new ClusterRepository(rig.Context)), PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = Http(email, role) },
        };

    private static QrController Qr(PublicationRig rig, BatteryPassWeb.Dpp.Documents.DppLinks? links = null) =>
        new(PassportPagesTests.Pages(rig), links ?? DocumentTesting.Links, new PassportQrCodeService())
        {
            ControllerContext = new ControllerContext { HttpContext = Http(null, "member") },
        };

    private static HomeController Home(PublicationRig rig, string? email) =>
        new(PassportPagesTests.Pages(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = Http(email, "member") },
        };
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryRouteAndRegistryTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DocumentAccessControlTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiInitializerTests|FullyQualifiedName~BatteryPassWeb.Tests.ManualFeedbackRound3Tests|FullyQualifiedName~BatteryPassWeb.Tests.PassportSoftwarePresentationTests|FullyQualifiedName~BatteryPassWeb.Tests.QrWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: the build fails: `HomeController` has no `IdFromSearch` (CS0117), and today's `PassportController`, `QrController` and `HomeController` still take today's services (CS7036).

- [ ] **Step 3: Write `_PassportHero.cshtml`**

`web/Views/Passport/_PassportHero.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model PassportPage
@{
    // The battery card of both passport pages (today's look, decision 23), with the new data underneath.
    var detail = ViewData["Detail"] is true;
    var manufacturer = Model.Value(15)?.Lines.FirstOrDefault(line => line.Label == "Name")?.Text ?? "-";
    var signedIn = User.Identity?.IsAuthenticated == true;
    var detailHref = PassportPages.DetailHref(Model.Address);
    var signIn = $"/login?next={Uri.EscapeDataString(detailHref)}";
}
@if (Model.IsTest)
{
    <p class="bp-warning">Test passport: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen.</p>
}
<section class="bp-report-hero-card">
    <div class="bp-report-hero-layout">
        <article class="bp-report-hero-info bp-summary-identity-grid bp-report-identity-grid">
            <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--battery bp-report-header bp-report-title-block">
                <span class="bp-summary-title">Battery</span>
                <h1>@Model.Text(11)</h1>
                <p class="text-secondary">Model: @Model.Text(10)</p>
                <div class="d-flex flex-wrap gap-2 align-items-center mt-3">
                    @if (Model.IsCurrent)
                    {
                        <span class="bp-status-pill is-active">Active</span>
                    }
                    else
                    {
                        <span class="bp-status-pill is-inactive">Archived</span>
                        @if (Model.Current is not null)
                        {
                            <a href="@(detail ? PassportPages.DetailHref(Model.Identifier) : PassportPages.SummaryHref(Model.Identifier))" class="bp-report-action" aria-label="Open the current version" title="Open the current version" data-tooltip="Open the current version" data-action-icon="latest">
                                <svg class="bp-report-action-icon bp-latest-passport-action-icon" viewBox="0 0 24 24" aria-hidden="true">
                                    <path d="M12 5a7 7 0 1 1-6.32 4" />
                                    <path d="M5 5v4h4" />
                                    <path d="M12 8v4l3 2" />
                                </svg>
                            </a>
                        }
                    }
                </div>
                @if (!Model.IsCurrent && Model.Current is not null)
                {
                    <p class="text-secondary small mt-2 mb-0">An older version of this passport. <a href="@(detail ? PassportPages.DetailHref(Model.Identifier) : PassportPages.SummaryHref(Model.Identifier))">Open the current version</a></p>
                }
            </section>

            <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--identifiers">
                <span class="bp-summary-title">Identifiers</span>
                <p class="bp-passport-id"><span class="bp-id-label">Passport ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Version.DigitalProductPassportId</span>@await Html.PartialAsync("_CopyIdButton", Model.Version.DigitalProductPassportId)</span></p>
                <p class="bp-passport-id"><span class="bp-id-label">Battery ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></p>
            </section>

            <section class="bp-summary-id-panel bp-summary-qr-panel bp-report-identity-panel bp-report-identity-panel--qr">
                <span class="bp-summary-title">QR code</span>
                <a class="bp-report-hero-qr" href="@PassportPages.QrDownloadHref(Model.Identifier)" aria-label="Save the QR code of battery @Model.Text(11)">
                    <img src="@PassportPages.QrHref(Model.Identifier)" alt="QR code of battery @Model.Text(11)" />
                </a>
            </section>

            <dl class="bp-field-grid bp-report-hero-fields bp-summary-fields-panel">
                <div><dt>Battery family</dt><dd>@(Model.FamilyName ?? "-")</dd></div>
                <div><dt>Battery serial number</dt><dd>@Model.Text(11)</dd></div>
                <div><dt>Battery model</dt><dd>@Model.Text(10)</dd></div>
                <div><dt>Category</dt><dd>@Model.Text(20)</dd></div>
                <div><dt>Passport status</dt><dd>@Model.Text(2)</dd></div>
                <div><dt>Weight</dt><dd>@Model.Text(21)</dd></div>
                <div><dt>Manufactured date</dt><dd>@Model.Text(17)</dd></div>
                <div><dt>Manufactured by</dt><dd>@manufacturer</dd></div>
                <div><dt>Last updated</dt><dd>@Model.Text(4)</dd></div>
                @if (detail)
                {
                    <div><dt>Signature</dt><dd>@(Model.SignatureHolds ? "Verified" : "Doesn't match the signed passport")</dd></div>
                }
            </dl>
        </article>

        <aside class="bp-report-hero-media">
            @if (Model.PictureHref is not null)
            {
                <div class="bp-image-wrap">
                    <img src="@Model.PictureHref" alt="Picture of the @Model.FamilyName battery family" class="bp-battery-image" />
                </div>
            }
            @if (detail)
            {
                <a class="bp-report-hero-cta" href="@PassportPages.SummaryHref(Model.Address)">&lt;- Back to summary</a>
            }
            else if (signedIn && Model.ReaderSeesMore)
            {
                <a class="bp-report-hero-cta" href="@detailHref">View more about this passport -&gt;</a>
            }
            else if (signedIn)
            {
                <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@(signIn)&switchAccount=true">Your account sees the public data only. Sign in with another account to see more.</a>
            }
            else
            {
                <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@signIn">Sign in to see more about this passport.</a>
            }
        </aside>
    </div>
</section>
```

- [ ] **Step 4: Write `_PassportTabs.cshtml`**

`web/Views/Passport/_PassportTabs.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model PassportPage
@{
    // The passport's data as today's tabs: on the detail page, first one tab per audience the reader may see (with a
    // lock), then the public data by schema section. Each value on the detail page says when and where it came from.
    var tabs = Model.Audiences.Select(section => (Section: section, Restricted: true))
        .Concat(Model.Sections.Select(section => (Section: section, Restricted: false)))
        .ToArray();
}
<section class="bp-card bp-report-wide-section">
    <ul class="nav nav-tabs bp-tabs" id="passport-tabs" role="tablist">
        @for (var index = 0; index < tabs.Length; index++)
        {
            var (tab, restricted) = tabs[index];
            <li class="nav-item" role="presentation">
                <button class="nav-link@(index == 0 ? " active" : string.Empty)" data-bs-toggle="tab" data-bs-target="#tab-@tab.Key" type="button" role="tab" aria-selected="@(index == 0 ? "true" : "false")">
                    @if (restricted)
                    {
                        <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true" style="vertical-align:-2px;margin-right:4px"><rect x="3.5" y="7" width="9" height="6.5" rx="1.5" /><path d="M5.5 7V5.5a2.5 2.5 0 0 1 5 0V7" /></svg>
                    }
                    @tab.Tab
                </button>
            </li>
        }
    </ul>

    <div class="tab-content pt-4">
        @for (var index = 0; index < tabs.Length; index++)
        {
            var (tab, _) = tabs[index];
            <div class="tab-pane fade@(index == 0 ? " show active" : string.Empty)" id="tab-@tab.Key" role="tabpanel">
                <h2 class="h5 mb-2">@tab.Title</h2>
                <p class="bp-help-text">@tab.Note</p>
                @if (tab.Key == "purchaser" && !Model.IsCurrent)
                {
                    <p class="bp-warning">The live values (state of charge, cycles, temperature, accidents and the extreme-temperature times) are on the <a href="@PassportPages.DetailHref(Model.Identifier)">current version</a>.</p>
                }
                @if (tab.Values.Count == 0)
                {
                    <p class="text-secondary mb-0">No values in this version.</p>
                }
                else
                {
                    <dl class="bp-field-grid">
                        @foreach (var value in tab.Values)
                        {
                            var single = value.Lines.Count == 1 && value.Lines[0].Label is null;
                            <div>
                                <dt>@(value.Attribute.IsExtension ? $"{value.Attribute.Label} {value.Attribute.Name}" : value.Attribute.Name)</dt>
                                <dd>
                                    @for (var line = 0; line < value.Lines.Count; line++)
                                    {
                                        var part = value.Lines[line];
                                        <span class="@(single ? string.Empty : "d-block")@(part.StartsRow && line > 0 ? " mt-2" : string.Empty)">
                                            @if (part.Label is not null)
                                            {
                                                <span class="text-secondary">@part.Label:</span>
                                            }
                                            @if (part.Href is not null)
                                            {
                                                <a href="@part.Href" target="_blank" rel="noreferrer">@part.Text</a>
                                            }
                                            else
                                            {
                                                @part.Text
                                            }
                                        </span>
                                    }
                                    @if (value.Origin is { } origin)
                                    {
                                        <span class="d-block small text-secondary">@(origin.Measured ? "Measured" : "Entered") @ValueWords.Time(origin.GeneratedAtUtc) · @PassportPages.SourceWord(origin.Source)</span>
                                    }
                                </dd>
                            </div>
                        }
                    </dl>
                }
            </div>
        }
    </div>
</section>
```

- [ ] **Step 5: Replace the controllers, the QR service and the home page**

Patch `3b-task2-code.patch` (`web/Controllers/HomeController.cs`, `web/Controllers/PassportController.cs`, `web/Controllers/QrController.cs`, `web/Services/PassportQrCodeService.cs`, `web/Views/Dpp/NotAvailable.cshtml`, `web/Views/Home/Index.cshtml`, `web/Views/Passport/Detail.cshtml`, `web/Views/Passport/Summary.cshtml`). Save the block as `../3b-task2-code.patch`, outside the worktree, and run `git apply ../3b-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/HomeController.cs b/web/Controllers/HomeController.cs
--- a/web/Controllers/HomeController.cs
+++ b/web/Controllers/HomeController.cs
@@ -1,123 +1,69 @@
-using BatteryPassWeb.Services;
+using BatteryPassWeb.Dpp.Pages;
 using Microsoft.AspNetCore.Mvc;
 
 namespace BatteryPassWeb.Controllers;
 
+/// <summary>
+/// The home page and its search (step 3b): a battery's serial number, battery ID (#6), passport ID (#5) or the address
+/// its QR code carries opens the public summary, or the detail page when signed in.
+/// </summary>
 [Route("")]
-public class HomeController : Controller
+public class HomeController(PassportPages pages) : Controller
 {
-    private readonly BatteryRouteResolutionService _batteryRouteResolutionService;
-    private readonly BatteryIdService _batteryIdService;
-
-    public HomeController(
-        BatteryRouteResolutionService batteryRouteResolutionService,
-        BatteryIdService batteryIdService)
-    {
-        _batteryRouteResolutionService = batteryRouteResolutionService;
-        _batteryIdService = batteryIdService;
-    }
+    /// <summary>Address parts that aren't an ID: the passport pages' prefix and suffixes, and today's old ones.</summary>
+    private static readonly HashSet<string> RouteWords = new(StringComparer.OrdinalIgnoreCase) { "dpp", "detail", "summary", "latest" };
 
     [HttpGet("")]
     public IActionResult Index([FromQuery] string? q, [FromQuery] string? notFound)
     {
-        ViewData["SamplePassportId"] = ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService);
         ViewData["SearchQuery"] = q?.Trim() ?? string.Empty;
         ViewData["SearchNotFound"] = string.Equals(notFound, "1", StringComparison.OrdinalIgnoreCase);
-        ViewData["IsAdminSearch"] = AccessControlService.IsAdmin(User);
         return View();
     }
 
     [HttpGet("search")]
     public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
     {
-        var query = ExtractIdFromQrPayload(q?.Trim() ?? string.Empty);
+        var query = IdFromSearch(q);
         if (query.Length == 0)
         {
             return RedirectToAction(nameof(Index));
         }
 
-        var resolution = await _batteryRouteResolutionService.ResolveAsync(query, cancellationToken);
-        if (resolution.Kind == BatteryRouteTargetKind.Battery)
-        {
-            var resolvedBatteryId = BsonHelpers.GetString(resolution.Document!, "batteryId");
-            var routeBatteryId = string.IsNullOrWhiteSpace(resolvedBatteryId) ? query : resolvedBatteryId;
-            return Redirect($"/{Uri.EscapeDataString(routeBatteryId)}/latest");
-        }
-
-        if (resolution.Kind == BatteryRouteTargetKind.Passport)
-        {
-            var passportPath = $"/{Uri.EscapeDataString(query)}";
-            return User.Identity?.IsAuthenticated == true
-                ? Redirect(passportPath)
-                : Redirect($"{passportPath}/summary");
-        }
-
-        return RedirectToAction(nameof(Index), new
+        var passport = await pages.LocateAsync(query, cancellationToken);
+        if (passport is null)
         {
-            q = query,
-            notFound = "1"
-        });
-    }
-
-    private static string ExtractIdFromQrPayload(string value)
-    {
-        if (string.IsNullOrWhiteSpace(value))
-        {
-            return string.Empty;
+            return RedirectToAction(nameof(Index), new { q = query, notFound = "1" });
         }
 
-        if (value.StartsWith("did:web:", StringComparison.OrdinalIgnoreCase))
-        {
-            return value;
-        }
-
-        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri)
-            || Uri.TryCreate($"https://local.test{(value.StartsWith('/') ? string.Empty : "/")}{value}", UriKind.Absolute, out absoluteUri))
-        {
-            foreach (var key in new[] { "q", "batteryId", "passportId", "id" })
-            {
-                var candidate = ReadQueryValue(absoluteUri.Query, key);
-                if (!string.IsNullOrWhiteSpace(candidate))
-                {
-                    return candidate;
-                }
-            }
-
-            foreach (var segment in absoluteUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
-            {
-                var candidate = Uri.UnescapeDataString(segment);
-                if (!IsRouteSuffix(candidate))
-                {
-                    return candidate;
-                }
-            }
-        }
-
-        return value;
-    }
-
-    private static bool IsRouteSuffix(string value)
-    {
-        return value.Equals("latest", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("summary", StringComparison.OrdinalIgnoreCase);
+        return Redirect(User.Identity?.IsAuthenticated == true
+            ? PassportPages.DetailHref(passport.Address)
+            : PassportPages.SummaryHref(passport.Address));
     }
 
-    private static string ReadQueryValue(string query, string key)
+    /// <summary>
+    /// The ID in what was typed or scanned: from a web address (a QR code's), its first path part that isn't a route
+    /// word, or its "q" or "id"; anything else as typed, so a battery ID such as urn:x-battery-pass:TEST-… keeps its form.
+    /// </summary>
+    public static string IdFromSearch(string? value)
     {
-        if (string.IsNullOrWhiteSpace(query))
+        var text = value?.Trim() ?? string.Empty;
+        if (!Uri.TryCreate(text, UriKind.Absolute, out var address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
         {
-            return string.Empty;
+            return text;
         }
 
-        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
+        foreach (var key in new[] { "q", "id" })
         {
-            var parts = pair.Split('=', 2);
-            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]).Equals(key, StringComparison.OrdinalIgnoreCase))
+            var candidate = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(address.Query).TryGetValue(key, out var found) ? found.ToString().Trim() : string.Empty;
+            if (candidate.Length > 0)
             {
-                return Uri.UnescapeDataString(parts[1].Replace("+", " "));
+                return candidate;
             }
         }
 
-        return string.Empty;
+        return address.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
+            .Select(Uri.UnescapeDataString)
+            .FirstOrDefault(segment => !RouteWords.Contains(segment)) ?? string.Empty;
     }
 }
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -1,373 +1,55 @@
-using BatteryPassWeb.Models.ViewModels;
-using BatteryPassWeb.Services;
+using BatteryPassWeb.Dpp.Access;
+using BatteryPassWeb.Dpp.Pages;
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Http;
 using Microsoft.AspNetCore.Mvc;
-using MongoDB.Bson;
+using Microsoft.Net.Http.Headers;
 
 namespace BatteryPassWeb.Controllers;
 
-public class PassportController : Controller
+/// <summary>
+/// The passport pages (design 12, step 3b), in place of today's: the public summary, which the battery's QR code opens
+/// without signing in, and the detail page for signed-in readers. An address names a passport by its battery ID (#6),
+/// a version's passport ID (#5) or the battery's serial number. Both pages show only what <see cref="PassportFilter"/>
+/// leaves for the reader.
+/// </summary>
+[AllowAnonymous]
+[Route("dpp")]
+public sealed class PassportController(DppReaders readers, PassportPages pages) : Controller
 {
-    private readonly PassportRepository _passportRepository;
-    private readonly BatteryRepository _batteryRepository;
-    private readonly BatteryRouteResolutionService _batteryRouteResolutionService;
-    private readonly ClusterRepository _clusterRepository;
-    private readonly PassportViewModelFactory _viewModelFactory;
-    private readonly AccessControlService _accessControlService;
-    private readonly BatteryTelemetryRepository _batteryTelemetryRepository;
-    private readonly PassportTrustService _passportTrustService;
-    private readonly PassportPublishPolicyService _passportPublishPolicyService;
-    private readonly BatteryIdService _batteryIdService;
-
-    public PassportController(
-        PassportRepository passportRepository,
-        BatteryRepository batteryRepository,
-        BatteryRouteResolutionService batteryRouteResolutionService,
-        ClusterRepository clusterRepository,
-        PassportViewModelFactory viewModelFactory,
-        AccessControlService accessControlService,
-        BatteryTelemetryRepository batteryTelemetryRepository,
-        PassportTrustService passportTrustService,
-        PassportPublishPolicyService passportPublishPolicyService,
-        BatteryIdService batteryIdService)
-    {
-        _passportRepository = passportRepository;
-        _batteryRepository = batteryRepository;
-        _batteryRouteResolutionService = batteryRouteResolutionService;
-        _clusterRepository = clusterRepository;
-        _viewModelFactory = viewModelFactory;
-        _accessControlService = accessControlService;
-        _batteryTelemetryRepository = batteryTelemetryRepository;
-        _passportTrustService = passportTrustService;
-        _passportPublishPolicyService = passportPublishPolicyService;
-        _batteryIdService = batteryIdService;
-    }
-
-    [HttpGet("{passportId}/summary")]
-    public async Task<IActionResult> Summary(string passportId, [FromQuery] string? access, CancellationToken cancellationToken)
-    {
-        if (IsReservedSegment(passportId))
-        {
-            return NotFound();
-        }
-
-        var decodedPassportId = Uri.UnescapeDataString(passportId);
-        var document = await _passportRepository.GetByPassportIdAsync(decodedPassportId, cancellationToken)
-            ?? FallbackSamplePassport(decodedPassportId);
-        if (document == null)
-        {
-            return NotFound();
-        }
-        if (string.Equals(BsonHelpers.GetString(document, "registryInfo", "status"), "archived", StringComparison.OrdinalIgnoreCase))
-        {
-            return NotFound();
-        }
-        if (!await _accessControlService.CanOpenPassportSummaryAsync(User, document, _passportPublishPolicyService, cancellationToken))
-        {
-            return NotFound();
-        }
-
-        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = clusterDocuments
-            .Select(cluster => new
-            {
-                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name")
-            })
-            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
-        var passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document));
-        var canOpenDetail = false;
-
-        if (User.Identity?.IsAuthenticated == true)
-        {
-            canOpenDetail = await _accessControlService.CanOpenPassportDetailAsync(User, document, _passportPublishPolicyService, cancellationToken);
-        }
-
-        var detailAccessNotice = string.Empty;
-        if (!canOpenDetail)
-        {
-            if (string.IsNullOrWhiteSpace(passport.ClusterId))
-            {
-                detailAccessNotice = "This battery is not assigned to a cluster. Sign in as the general admin to open the detailed report.";
-            }
-            else if (User.Identity?.IsAuthenticated == true)
-            {
-                var email = AccessControlService.CurrentEmail(User);
-                detailAccessNotice = $"Current user {email} is not connected to {passport.ClusterLabel}. Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
-            }
-            else
-            {
-                detailAccessNotice = $"Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
-            }
-        }
-
-        if (!canOpenDetail && string.Equals(access, "wrong-cluster", StringComparison.OrdinalIgnoreCase))
-        {
-            if (string.IsNullOrWhiteSpace(detailAccessNotice))
-            {
-                detailAccessNotice = $"Sign in with a user connected to {passport.ClusterLabel} to open the detailed report.";
-            }
-        }
-
-        return View(new PassportSummaryPageViewModel
-        {
-            Passport = passport,
-            CanOpenDetail = canOpenDetail,
-            DetailActionUrl = BuildDetailActionUrl(passport.PassportId, canOpenDetail, User.Identity?.IsAuthenticated == true),
-            DetailAccessNotice = detailAccessNotice
-        });
-    }
-
-    [HttpGet("{batteryId}/latest")]
-    public async Task<IActionResult> Latest(string batteryId, CancellationToken cancellationToken)
-    {
-        if (IsReservedSegment(batteryId))
-        {
-            return NotFound();
-        }
-
-        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
-        var document = User.Identity?.IsAuthenticated == true
-            ? (await _passportRepository.ListByBatteryIdAsync(decodedBatteryId, includeArchived: false, cancellationToken)).FirstOrDefault()
-            : await _passportRepository.GetLatestPublicByBatteryIdAsync(decodedBatteryId, _passportPublishPolicyService, cancellationToken);
-
-        if (document == null)
-        {
-            if (IsGeneratedSampleBatteryId(decodedBatteryId))
-            {
-                return Redirect($"/{Uri.EscapeDataString(ExternalApiInitializer.SamplePassportId)}/summary");
-            }
-
-            return RedirectToSearchNotFound(decodedBatteryId);
-        }
-
-        var passportPath = $"/{Uri.EscapeDataString(BsonHelpers.GetString(document, "passportId"))}";
-        return User.Identity?.IsAuthenticated == true
-            ? Redirect(passportPath)
-            : Redirect($"{passportPath}/summary");
-    }
-
-    [HttpGet("{batteryId}/latest/summary")]
-    public Task<IActionResult> LatestSummary(string batteryId, CancellationToken cancellationToken)
-    {
-        return Latest(batteryId, cancellationToken);
-    }
-
     [HttpGet("{id}")]
-    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
+    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken)
     {
-        if (IsReservedSegment(id))
-        {
-            return NotFound();
-        }
-
-        var decodedPassportId = Uri.UnescapeDataString(id);
-        var resolution = await _batteryRouteResolutionService.ResolveAsync(decodedPassportId, cancellationToken);
-        if (resolution.Kind == BatteryRouteTargetKind.Battery)
-        {
-            return await Battery(decodedPassportId, cancellationToken);
-        }
-
-        if (resolution.Kind != BatteryRouteTargetKind.Passport || resolution.Document == null)
-        {
-            return FallbackSamplePassport(decodedPassportId) != null
-                ? RedirectToPublicSummaryWithAccessNotice(decodedPassportId)
-                : NotFound();
-        }
-
-        if (User.Identity?.IsAuthenticated != true)
-        {
-            return RedirectToPublicSummaryWithAccessNotice(decodedPassportId);
-        }
-
-        var document = resolution.Document;
-        if (string.Equals(BsonHelpers.GetString(document, "registryInfo", "status"), "archived", StringComparison.OrdinalIgnoreCase))
-        {
-            return NotFound();
-        }
-        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = clusterDocuments
-            .Select(cluster => new
-            {
-                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name")
-            })
-            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
-
-        var passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document));
-        var canOpen = await _accessControlService.CanOpenPassportDetailAsync(User, document, _passportPublishPolicyService, cancellationToken);
-        if (!canOpen)
-        {
-            if (!await _accessControlService.CanOpenPassportSummaryAsync(User, document, _passportPublishPolicyService, cancellationToken))
-            {
-                return NotFound();
-            }
-
-            return Redirect($"/{Uri.EscapeDataString(decodedPassportId)}/summary?access=wrong-cluster");
-        }
-        var canViewTrustConformance = await _accessControlService.CanViewTrustConformanceAsync(User, passport.ClusterId, cancellationToken);
-
-        var model = new PassportDetailViewModel
-        {
-            Passport = passport,
-            CanViewTrustConformance = canViewTrustConformance
-        };
-
-        var toUtc = DateTime.UtcNow;
-        var fromUtc = toUtc.AddDays(-7);
-        var telemetryBatteryId = passport.BatteryId;
-        IReadOnlyList<BsonDocument> telemetryHistory = string.IsNullOrWhiteSpace(telemetryBatteryId) || passport.IsHistoricalPassport
-            ? []
-            : await _batteryTelemetryRepository.ReadHistoryAsync(telemetryBatteryId, fromUtc, toUtc, cancellationToken);
-        model = new PassportDetailViewModel
-        {
-            Passport = passport,
-            CanViewTrustConformance = canViewTrustConformance,
-            TelemetryHistory = telemetryHistory.Select(row => new TelemetryHistoryPointViewModel
-            {
-                MeasuredAt = BsonHelpers.GetString(row, "measuredAt"),
-                CurrentConsumptionKwh = ReadNullableNumber(row, "currentConsumptionKwh"),
-                CurrentChargeLevelPct = ReadNullableNumber(row, "currentChargeLevelPct"),
-                CurrentVoltageV = ReadNullableNumber(row, "currentVoltageV"),
-                CurrentCurrentA = ReadNullableNumber(row, "currentCurrentA")
-            }).ToList()
-        };
-
-        return View(model);
+        var reader = await readers.ForAsync(User, cancellationToken);
+        var page = await pages.SummaryAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
+        return page is null ? NotFoundPage() : View(page);
     }
 
-    private async Task<IActionResult> Battery(string batteryId, CancellationToken cancellationToken)
+    [HttpGet("{id}/detail")]
+    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
     {
-        var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
-        if (battery == null)
-        {
-            return NotFound();
-        }
-
-        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = clusterDocuments
-            .Select(cluster => new
-            {
-                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name")
-            })
-            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
-        var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: User.Identity?.IsAuthenticated == true, cancellationToken);
-        var visibleRows = new List<BatteryPassportHistoryRowViewModel>();
-        foreach (var passport in passports)
-        {
-            if (!await _accessControlService.CanOpenPassportSummaryAsync(User, passport, _passportPublishPolicyService, cancellationToken))
-            {
-                continue;
-            }
-
-            visibleRows.Add(ToHistoryRow(passport));
-        }
-
-        if (visibleRows.Count == 0)
-        {
-            return NotFound();
-        }
-
         if (User.Identity?.IsAuthenticated != true)
         {
-            var latestPublic = visibleRows.FirstOrDefault(row => row.IsLatestForBattery && row.IsPubliclyVisible)
-                ?? visibleRows.FirstOrDefault(row => row.IsPubliclyVisible);
-            if (latestPublic != null)
-            {
-                return Redirect($"/{Uri.EscapeDataString(latestPublic.PassportId)}/summary");
-            }
-        }
-
-        var clusterId = BsonHelpers.GetString(battery, "clusterId");
-        var clusterLabel = string.IsNullOrWhiteSpace(clusterId)
-            ? "No cluster assigned"
-            : clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
-                ? clusterName
-                : clusterId;
-
-        return View("Battery", new BatteryDetailViewModel
-        {
-            Battery = _batteryRepository.ToSummary(battery, visibleRows, clusterLabel)
-        });
-    }
-
-    private BsonDocument? FallbackSamplePassport(string passportId)
-    {
-        if (!passportId.Equals(ExternalApiInitializer.SamplePassportId, StringComparison.OrdinalIgnoreCase))
-        {
-            return null;
-        }
-
-        return ExternalApiInitializer.CreateFallbackSampleDocument(ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService));
-    }
-
-    private static IActionResult RedirectToPublicSummaryWithAccessNotice(string passportId)
-    {
-        return new RedirectResult($"/{Uri.EscapeDataString(passportId)}/summary?access=detail-required");
-    }
-
-    private static string BuildDetailActionUrl(string passportId, bool canOpenDetail, bool isAuthenticated)
-    {
-        var detailPath = $"/{Uri.EscapeDataString(passportId)}";
-        if (canOpenDetail)
-        {
-            return detailPath;
+            return Challenge();
         }
 
-        var loginPath = $"/login?next={Uri.EscapeDataString(detailPath)}";
-        return isAuthenticated ? $"{loginPath}&switchAccount=1" : loginPath;
-    }
-
-    private bool IsGeneratedSampleBatteryId(string batteryId)
-    {
-        return batteryId.Equals(ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService), StringComparison.OrdinalIgnoreCase);
-    }
-
-    private IActionResult RedirectToSearchNotFound(string query)
-    {
-        return RedirectToAction("Index", "Home", new
-        {
-            q = query,
-            notFound = "1"
-        });
-    }
-
-    private BatteryPassportHistoryRowViewModel ToHistoryRow(BsonDocument passport)
-    {
-        var status = BsonHelpers.GetString(passport, "registryInfo", "status");
-        return new BatteryPassportHistoryRowViewModel
-        {
-            PassportId = BsonHelpers.GetString(passport, "passportId"),
-            BatteryId = BsonHelpers.GetString(passport, "batteryId"),
-            CreatedAt = BsonHelpers.GetString(passport, "snapshot", "createdAt"),
-            PassportStatus = string.IsNullOrWhiteSpace(status) ? "Draft" : char.ToUpperInvariant(status[0]) + status[1..],
-            IsLatestForBattery = passport.GetValue("isLatestForBattery", false).ToBoolean(),
-            IsPubliclyVisible = _passportPublishPolicyService.IsPubliclyVisible(passport)
-        };
-    }
-
-    private static double? ReadNullableNumber(BsonDocument document, string key)
-    {
-        var value = document.GetValue(key, BsonNull.Value);
-        if (value.IsBsonNull)
+        var reader = await readers.ForAsync(User, cancellationToken);
+        var page = await pages.DetailAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
+        if (page is null)
         {
-            return null;
+            return NotFoundPage();
         }
 
-        return value.IsNumeric ? value.ToDouble() : null;
+        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
+        return View(page);
     }
 
-    private static bool IsReservedSegment(string value)
+    /// <summary>No published passport has this ID; drafts aren't shown to anyone here (J-6).</summary>
+    private ViewResult NotFoundPage()
     {
-        return value.Equals("login", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("registry", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("search", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("help", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("admin", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("cluster-admin", StringComparison.OrdinalIgnoreCase)
-            || value.Equals("api", StringComparison.OrdinalIgnoreCase);
+        ViewData["Subject"] = "passport";
+        var view = View("~/Views/Dpp/NotAvailable.cshtml");
+        view.StatusCode = StatusCodes.Status404NotFound;
+        return view;
     }
 }
diff --git a/web/Controllers/QrController.cs b/web/Controllers/QrController.cs
--- a/web/Controllers/QrController.cs
+++ b/web/Controllers/QrController.cs
@@ -1,100 +1,40 @@
 using System.Text;
+using BatteryPassWeb.Dpp.Documents;
+using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Services;
+using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Mvc;
 
 namespace BatteryPassWeb.Controllers;
 
+/// <summary>
+/// The QR code of a battery with a published passport (design 10). It carries the public summary's address with the
+/// battery ID (#6), built from APP_BASE_URL, so a printed code doesn't depend on where the page was opened. The
+/// address takes the same IDs as the passport pages.
+/// </summary>
+[AllowAnonymous]
 [Route("qr")]
-public sealed class QrController : Controller
+public sealed class QrController(PassportPages pages, DppLinks links, PassportQrCodeService qrCodes) : Controller
 {
-    private readonly BatteryRepository _batteryRepository;
-    private readonly PassportRepository _passportRepository;
-    private readonly PassportPublishPolicyService _passportPublishPolicyService;
-    private readonly AccessControlService _accessControlService;
-    private readonly PassportQrCodeService _passportQrCodeService;
-    private readonly BatteryIdService _batteryIdService;
-
-    public QrController(
-        BatteryRepository batteryRepository,
-        PassportRepository passportRepository,
-        PassportPublishPolicyService passportPublishPolicyService,
-        AccessControlService accessControlService,
-        PassportQrCodeService passportQrCodeService,
-        BatteryIdService batteryIdService)
+    [HttpGet("{id}/svg")]
+    public async Task<IActionResult> QrCode(string id, CancellationToken cancellationToken)
     {
-        _batteryRepository = batteryRepository;
-        _passportRepository = passportRepository;
-        _passportPublishPolicyService = passportPublishPolicyService;
-        _accessControlService = accessControlService;
-        _passportQrCodeService = passportQrCodeService;
-        _batteryIdService = batteryIdService;
+        var battery = await BatteryAsync(id, cancellationToken);
+        return battery is null
+            ? NotFound()
+            : Content(qrCodes.GenerateSvg(links.PassportUrl(battery.Identifier)), "image/svg+xml", Encoding.UTF8);
     }
 
-    [HttpGet("{batteryId}/svg")]
-    public async Task<IActionResult> QrCode(string batteryId, CancellationToken cancellationToken)
+    [HttpGet("{id}/download")]
+    public async Task<IActionResult> Download(string id, CancellationToken cancellationToken)
     {
-        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
-        if (!await CanAccessQrAsync(decodedBatteryId, cancellationToken))
-        {
-            return NotFound();
-        }
-
-        var payload = _passportQrCodeService.BuildPayloadUrl(Request, decodedBatteryId);
-        var svg = _passportQrCodeService.GenerateSvg(payload);
-        return Content(svg, "image/svg+xml", Encoding.UTF8);
+        var battery = await BatteryAsync(id, cancellationToken);
+        return battery is null
+            ? NotFound()
+            : File(qrCodes.GeneratePng(links.PassportUrl(battery.Identifier)), "image/png", Path.ChangeExtension(qrCodes.BuildFileName(battery.SerialNumber), ".png"));
     }
 
-    [HttpGet("{batteryId}/download")]
-    public async Task<IActionResult> Download(string batteryId, CancellationToken cancellationToken)
-    {
-        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
-        if (!await CanAccessQrAsync(decodedBatteryId, cancellationToken))
-        {
-            return NotFound();
-        }
-
-        var payload = _passportQrCodeService.BuildPayloadUrl(Request, decodedBatteryId);
-        var fileName = _passportQrCodeService.BuildFileName(decodedBatteryId);
-        var png = _passportQrCodeService.GeneratePng(payload);
-        return File(png, "image/png", Path.ChangeExtension(fileName, ".png"));
-    }
-
-    private async Task<bool> CanAccessQrAsync(string batteryId, CancellationToken cancellationToken)
-    {
-        if (string.IsNullOrWhiteSpace(batteryId))
-        {
-            return false;
-        }
-
-        var sampleBatteryId = ExternalApiInitializer.CreateSampleBatteryId(_batteryIdService);
-        if (batteryId.Equals(sampleBatteryId, StringComparison.OrdinalIgnoreCase))
-        {
-            return true;
-        }
-
-        var battery = await _batteryRepository.GetByBatteryIdAsync(batteryId, cancellationToken);
-        if (battery == null)
-        {
-            return false;
-        }
-
-        var latestPublicPassport = await _passportRepository.GetLatestPublicByBatteryIdAsync(
-            batteryId,
-            _passportPublishPolicyService,
-            cancellationToken);
-        if (latestPublicPassport != null)
-        {
-            return true;
-        }
-
-        if (User.Identity?.IsAuthenticated != true)
-        {
-            return false;
-        }
-
-        return await _accessControlService.CanOpenPassportDetailAsync(
-            User,
-            BsonHelpers.GetString(battery, "clusterId"),
-            cancellationToken);
-    }
+    /// <summary>The battery of a published passport; null for a draft, an unknown ID, or while APP_BASE_URL isn't set.</summary>
+    private async Task<PassportLocation?> BatteryAsync(string id, CancellationToken cancellationToken) =>
+        links.IsConfigured ? await pages.LocateAsync(Uri.UnescapeDataString(id), cancellationToken) : null;
 }
diff --git a/web/Services/PassportQrCodeService.cs b/web/Services/PassportQrCodeService.cs
--- a/web/Services/PassportQrCodeService.cs
+++ b/web/Services/PassportQrCodeService.cs
@@ -2,14 +2,9 @@ using QRCoder;
 
 namespace BatteryPassWeb.Services;
 
+/// <summary>Draws a battery's QR code; its address comes from <see cref="Dpp.Documents.DppLinks.PassportUrl"/>.</summary>
 public sealed class PassportQrCodeService
 {
-    public string BuildPayloadUrl(HttpRequest request, string batteryId)
-    {
-        var escapedBatteryId = Uri.EscapeDataString(batteryId);
-        return $"{request.Scheme}://{request.Host}/{escapedBatteryId}/latest/summary";
-    }
-
     public string GenerateSvg(string payload)
     {
         using var generator = new QRCodeGenerator();
diff --git a/web/Views/Dpp/NotAvailable.cshtml b/web/Views/Dpp/NotAvailable.cshtml
--- a/web/Views/Dpp/NotAvailable.cshtml
+++ b/web/Views/Dpp/NotAvailable.cshtml
@@ -1,12 +1,17 @@
 @{
     ViewData["Title"] = "Not available";
     var accidents = ViewData["Subject"] as string == "accidents";
+    var passport = ViewData["Subject"] as string == "passport";
 }
 <main class="bp-page">
     <section class="bp-card bp-report-wide-section">
-        <span class="bp-summary-title">@(accidents ? "Accident information" : "Battery passport document")</span>
+        <span class="bp-summary-title">@(accidents ? "Accident information" : passport ? "Battery passport" : "Battery passport document")</span>
         <h1 class="h5 mt-2 mb-1">Not available</h1>
-        @if (accidents)
+        @if (passport)
+        {
+            <p class="text-secondary mb-0">No published battery passport has this ID. Check it, or <a href="/">search</a> by the battery's serial number.</p>
+        }
+        else if (accidents)
         {
             <p class="text-secondary mb-0">This information is shown only to the battery's cluster, market surveillance and the manufacturer's team. If you think you should see it, ask your cluster admin.</p>
         }
diff --git a/web/Views/Home/Index.cshtml b/web/Views/Home/Index.cshtml
--- a/web/Views/Home/Index.cshtml
+++ b/web/Views/Home/Index.cshtml
@@ -1,33 +1,24 @@
 @{
     ViewData["Title"] = "Scania Battery Passport Viewer";
-    var samplePassportId = ViewData["SamplePassportId"] as string ?? string.Empty;
     var searchQuery = ViewData["SearchQuery"] as string ?? string.Empty;
     var searchWasNotFound = ViewData["SearchNotFound"] is true;
-    var isAdminSearch = ViewData["IsAdminSearch"] is true;
 }
 <main class="bp-landing">
     <div class="bp-landing-content">
         <div>
             <h1>Scania Battery Passport Viewer</h1>
-            <p>Enter a battery ID or passport ID to open the public demonstrator view. The placeholder is a sample battery ID for the public sample passport.</p>
+            <p>Enter a battery's serial number, battery ID or passport ID, or scan its QR code, to open its battery passport.</p>
         </div>
         @if (searchWasNotFound && !string.IsNullOrWhiteSpace(searchQuery))
         {
             <p class="bp-warning">
-                @if (isAdminSearch)
-                {
-                    <span>No battery passport ID was found for <span class="fw-semibold">@searchQuery</span>.</span>
-                }
-                else
-                {
-                    <span>No active public battery passport was found for <span class="fw-semibold">@searchQuery</span>. Check the ID, or ask an admin whether the passport is archived, unpublished, or still waiting for signing.</span>
-                }
+                <span>No published battery passport was found for <span class="fw-semibold">@searchQuery</span>. Check the serial number or ID; a battery's passport is found here once it is published.</span>
             </p>
         }
         <form action="/search" class="bp-search-form bp-landing-search" data-search-form>
             <div class="bp-search-main-row">
                 <div class="bp-search-input-shell">
-                    <input name="q" value="@searchQuery" placeholder="@samplePassportId" data-search-query aria-describedby="bpLandingSearchMessage" />
+                    <input name="q" value="@searchQuery" placeholder="Serial number, battery ID or passport ID" data-search-query aria-describedby="bpLandingSearchMessage" />
                     <button type="button" class="bp-qr-trigger" data-qr-trigger aria-label="Scan a battery passport QR code">
                         <span class="bp-qr-trigger-icon" aria-hidden="true">
                             <svg viewBox="0 0 24 24" focusable="false">
@@ -39,12 +30,9 @@
                 <button type="submit">Search</button>
             </div>
             <p id="bpLandingSearchMessage" class="bp-search-inline-message" data-empty-search-message hidden>
-                Enter a battery ID, passport ID, or use the QR icon inside the field before searching.
+                Enter a serial number, battery ID or passport ID, or use the QR icon inside the field before searching.
             </p>
         </form>
-        <div class="bp-landing-links">
-            <a href="/search?q=@Uri.EscapeDataString(samplePassportId)">Search sample battery</a>
-        </div>
     </div>
 
     <div class="bp-qr-modal" data-qr-modal hidden>
@@ -68,7 +56,7 @@
                 Scan an image file
                 <input type="file" accept="image/*,.svg" data-qr-file />
             </label>
-            <p class="bp-qr-status" data-qr-status>QR scan failed? Enter the DID manually in the search field.</p>
+            <p class="bp-qr-status" data-qr-status>QR scan failed? Type the battery's serial number in the search field.</p>
         </section>
     </div>
 </main>
@@ -109,7 +97,7 @@
             const openModal = () => {
                 modal.hidden = false;
                 document.body.classList.add('bp-modal-open');
-                setStatus('Use the camera or scan a saved QR image. Manual DID search stays available behind this dialog.');
+                setStatus('Use the camera or scan a saved QR image. You can also type the battery's serial number behind this dialog.');
             };
 
             const detectQrWithNativeDetector = async (source) => {
@@ -178,36 +166,12 @@
                 });
             };
 
-            const extractDidFromQr = (value) => {
-                const raw = (value || '').trim();
-                if (raw.startsWith('did:web:')) {
-                    return raw;
-                }
-
-                try {
-                    const parsed = new URL(raw, window.location.origin);
-                    const queryValue = parsed.searchParams.get('q') || parsed.searchParams.get('passportId');
-                    if (queryValue && queryValue.startsWith('did:web:')) {
-                        return queryValue;
-                    }
-
-                    for (const segment of parsed.pathname.split('/').filter(Boolean)) {
-                        const decoded = decodeURIComponent(segment);
-                        if (decoded.startsWith('did:web:')) {
-                            return decoded;
-                        }
-                    }
-                } catch {
-                    // Keep the fallback regex below for plain text QR payloads.
-                }
-
-                const match = raw.match(/did:web:[A-Za-z0-9:._-]+/);
-                return match ? match[0] : '';
-            };
+            // The search reads the battery ID out of the QR code's address, so the scanned text goes in as it is.
+            const extractDidFromQr = (value) => (value || '').trim();
 
             const submitDid = (did) => {
                 if (!did) {
-                    setStatus('QR scan failed. The code did not contain a Battery Pass DID.', true);
+                    setStatus('QR scan failed. The code was empty.', true);
                     return;
                 }
 
@@ -307,7 +271,7 @@
                     scanFrame();
                 } catch (error) {
                     stopCamera();
-                    setStatus(`QR scan failed. ${error.message} Use manual DID search instead.`, true);
+                    setStatus(`QR scan failed. ${error.message} Type the battery's serial number instead.`, true);
                 }
             });
 
@@ -343,7 +307,7 @@
                         throw error || lastError;
                     }
                 } catch (error) {
-                    setStatus(`QR scan failed. ${error.message} Use manual DID search instead.`, true);
+                    setStatus(`QR scan failed. ${error.message} Type the battery's serial number instead.`, true);
                 } finally {
                     fileInput.value = '';
                 }
diff --git a/web/Views/Passport/Detail.cshtml b/web/Views/Passport/Detail.cshtml
--- a/web/Views/Passport/Detail.cshtml
+++ b/web/Views/Passport/Detail.cshtml
@@ -1,410 +1,12 @@
-@using System.Globalization
-@model BatteryPassWeb.Models.ViewModels.PassportDetailViewModel
+@using BatteryPassWeb.Dpp.Pages
+@model PassportPage
 @{
-    ViewData["Title"] = "Passport Detail";
-    var passport = Model.Passport;
-    var qrBatteryId = string.IsNullOrWhiteSpace(passport.BatteryId) ? passport.PassportId : passport.BatteryId;
-    var telemetryJson = System.Text.Json.JsonSerializer.Serialize(Model.TelemetryHistory);
-
-    string BuildConicGradient(IEnumerable<BatteryPassWeb.Models.ViewModels.ChartSegmentViewModel> segments)
-    {
-        var start = 0d;
-        var slices = new List<string>();
-        foreach (var segment in segments.Where(segment => segment.Value > 0))
-        {
-            var color = string.IsNullOrWhiteSpace(segment.Color) ? "#4f6f7d" : segment.Color;
-            var end = start + segment.Percentage;
-            slices.Add($"{color} {start.ToString("0.##", CultureInfo.InvariantCulture)}% {end.ToString("0.##", CultureInfo.InvariantCulture)}%");
-            start = end;
-        }
-
-        if (slices.Count == 0)
-        {
-            slices.Add("#d1d5db 0% 100%");
-        }
-
-        return $"conic-gradient({string.Join(", ", slices)})";
-    }
+    // The detail page (design 12): for signed-in readers, one tab per audience they may see, then the public data,
+    // every value with when it was generated and where it came from.
+    ViewData["Title"] = $"Battery passport detail · {Model.Text(11)}";
+    ViewData["Detail"] = true;
 }
 <main class="bp-page">
-    <section class="bp-report-hero-card">
-        <div class="bp-report-hero-layout">
-            <article class="bp-report-hero-info bp-summary-identity-grid bp-report-identity-grid">
-                <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--battery bp-report-header bp-report-title-block">
-                    <span class="bp-summary-title">Battery</span>
-                    <h1>@(string.IsNullOrWhiteSpace(passport.BatterySerialNumber) ? passport.PassportId : passport.BatterySerialNumber)</h1>
-                    <p class="text-secondary">Cluster: @passport.ClusterLabel</p>
-                    <div class="d-flex flex-wrap gap-2 align-items-center mt-3">
-                        @if (passport.IsHistoricalPassport)
-                        {
-                            <span class="bp-status-pill is-draft">Historical passport</span>
-                            <a href="@passport.LatestPassportUrl" class="bp-report-action" aria-label="Open latest passport" title="Open latest passport" data-tooltip="Open latest passport" data-action-icon="latest">
-                                <svg class="bp-report-action-icon bp-latest-passport-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                    <path d="M12 5a7 7 0 1 1-6.32 4" />
-                                    <path d="M5 5v4h4" />
-                                    <path d="M12 8v4l3 2" />
-                                </svg>
-                            </a>
-                        }
-                        else
-                        {
-                            <span class="bp-status-pill is-published">Latest passport</span>
-                        }
-                    </div>
-                </section>
-
-                <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--identifiers">
-                    <span class="bp-summary-title">Identifiers</span>
-                    <p class="bp-passport-id"><span class="bp-id-label">Passport ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@passport.PassportId</span>@await Html.PartialAsync("_CopyIdButton", passport.PassportId)</span></p>
-                    <p class="bp-passport-id"><span class="bp-id-label">Battery ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@(string.IsNullOrWhiteSpace(passport.BatteryId) ? "-" : passport.BatteryId)</span>@await Html.PartialAsync("_CopyIdButton", passport.BatteryId)</span></p>
-                </section>
-
-                <section class="bp-summary-id-panel bp-summary-qr-panel bp-report-identity-panel bp-report-identity-panel--qr">
-                    <span class="bp-summary-title">QR code</span>
-                    <a class="bp-report-hero-qr"
-                       href="/qr/@Uri.EscapeDataString(qrBatteryId)/download"
-                       aria-label="Save battery code for @qrBatteryId">
-                        <img src="/qr/@Uri.EscapeDataString(qrBatteryId)/svg" alt="QR code for @qrBatteryId" />
-                    </a>
-                </section>
-
-                <dl class="bp-field-grid bp-report-hero-fields bp-summary-fields-panel">
-                    <div><dt>Battery Family</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatteryFamily) ? "-" : passport.BatteryFamily)</dd></div>
-                    <div><dt>Battery serial number</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatterySerialNumber) ? "-" : passport.BatterySerialNumber)</dd></div>
-                    <div><dt>Battery Model</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatteryModel) ? "-" : passport.BatteryModel)</dd></div>
-                    <div><dt>Software version</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareVersion) ? "-" : passport.SoftwareVersion)</dd></div>
-                    <div><dt>Category</dt><dd>@passport.Category</dd></div>
-                    <div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
-                    <div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>
-                    <div><dt>Weight</dt><dd>@passport.WeightLabel</dd></div>
-                    <div><dt>Manufactured date</dt><dd>@passport.ManufacturedDate</dd></div>
-                    <div><dt>Manufactured by</dt><dd>@passport.ManufacturerName</dd></div>
-                    <div><dt>Cluster</dt><dd>@passport.ClusterLabel</dd></div>
-                </dl>
-            </article>
-
-            <aside class="bp-report-hero-media">
-                <div class="bp-image-wrap">
-                    <img src="@passport.BatteryImageUrl" alt="@passport.BatteryImageAlt" class="bp-battery-image" />
-                </div>
-                <a class="bp-report-hero-cta" href="/@Uri.EscapeDataString(passport.PassportId)/summary">&lt;- Back to summary</a>
-            </aside>
-        </div>
-    </section>
-
-    <section class="bp-card bp-report-wide-section">
-        <ul class="nav nav-tabs bp-tabs" id="detail-tabs" role="tablist">
-            <li class="nav-item" role="presentation"><button class="nav-link active" data-bs-toggle="tab" data-bs-target="#tab-general" type="button" role="tab">General</button></li>
-            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-material" type="button" role="tab">Material composition</button></li>
-            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-performance" type="button" role="tab">Performance</button></li>
-            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-compliance" type="button" role="tab">Compliance</button></li>
-            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-supply" type="button" role="tab">Supply chain</button></li>
-            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-circularity" type="button" role="tab">Circularity</button></li>
-            <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-carbon" type="button" role="tab">Carbon Footprint</button></li>
-            @if (Model.CanViewTrustConformance)
-            {
-                <li class="nav-item" role="presentation"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#tab-trust" type="button" role="tab">Trust & conformance</button></li>
-            }
-        </ul>
-
-        <div class="tab-content pt-4">
-            <div class="tab-pane fade show active" id="tab-general" role="tabpanel">
-                <h2 class="h5 mb-2">General</h2>
-                <p class="bp-help-text">Generic information about the battery</p>
-                <dl class="bp-field-grid">
-                    <div><dt>Manufactured date</dt><dd>@passport.ManufacturedDate</dd></div>
-                    <div><dt>Facility ID</dt><dd>@passport.FacilityId</dd></div>
-                    <div><dt>Manufactured by</dt><dd>@passport.ManufacturerName</dd></div>
-                    <div><dt>Category</dt><dd>@passport.Category</dd></div>
-                    <div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
-                    <div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>
-                    <div><dt>Software version</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareVersion) ? "-" : passport.SoftwareVersion)</dd></div>
-                    <div><dt>Software release date</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareReleaseDate) ? "-" : passport.SoftwareReleaseDate)</dd></div>
-                    <div><dt>Software latest update</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareLatestUpdate) ? "-" : passport.SoftwareLatestUpdate)</dd></div>
-                    <div><dt>Weight</dt><dd>@passport.WeightLabel</dd></div>
-                </dl>
-            </div>
-
-            <div class="tab-pane fade" id="tab-material" role="tabpanel">
-                <h2 class="h5 mb-2">Material composition</h2>
-                <p class="bp-help-text">Material composition of the battery</p>
-                <div class="bp-two-column mt-4">
-                    <div>
-                        <h3 class="bp-subheading">Battery material composition</h3>
-                        <div class="bp-pie-row">
-                            <div class="bp-pie" style="background:@BuildConicGradient(passport.MaterialCompositionSegments);"></div>
-                            <ul class="bp-legend">
-                                @foreach (var segment in passport.MaterialCompositionSegments)
-                                {
-                                    <li>
-                                        <span class="bp-dot" style="background:@(string.IsNullOrWhiteSpace(segment.Color) ? "#4f6f7d" : segment.Color);"></span>
-                                        <span>@segment.Label: @segment.Value.ToString("0.#") kg</span>
-                                    </li>
-                                }
-                            </ul>
-                        </div>
-                    </div>
-                    <div class="table-responsive">
-                        <table class="table table-sm">
-                            <thead>
-                                <tr>
-                                    <th>Material</th>
-                                    <th>Mass</th>
-                                    <th>Component</th>
-                                    <th>Critical</th>
-                                </tr>
-                            </thead>
-                            <tbody>
-                                @foreach (var material in passport.BatteryMaterials)
-                                {
-                                    <tr>
-                                        <td>@material.MaterialName</td>
-                                        <td>@material.MaterialMass.ToString("0.##") kg</td>
-                                        <td>@material.ComponentName</td>
-                                        <td>@(material.IsCriticalRawMaterial ? "Yes" : "No")</td>
-                                    </tr>
-                                }
-                            </tbody>
-                        </table>
-                    </div>
-                </div>
-            </div>
-
-            <div class="tab-pane fade" id="tab-performance" role="tabpanel">
-                <h2 class="h5 mb-2">Performance</h2>
-                <p class="bp-help-text">Battery performance information</p>
-                <div class="bp-metric-grid mt-3">
-                    <div class="bp-metric"><p>Rated energy</p><strong>@passport.Performance.RatedEnergy.ToString("0.##") kWh</strong></div>
-                    <div class="bp-metric"><p>Rated capacity</p><strong>@passport.Performance.RatedCapacity.ToString("0.##") Ah</strong></div>
-                    <div class="bp-metric"><p>Maximum power</p><strong>@passport.Performance.RatedMaximumPower.ToString("0.##") kW</strong></div>
-                    <div class="bp-metric"><p>Nominal voltage</p><strong>@passport.Performance.NominalVoltage.ToString("0.##") V</strong></div>
-                    <div class="bp-metric"><p>State of charge</p><strong>@passport.Performance.StateOfCharge.ToString("0.##") %</strong></div>
-                    <div class="bp-metric"><p>Remaining capacity</p><strong>@passport.Performance.RemainingCapacity.ToString("0.##") %</strong></div>
-                    <div class="bp-metric"><p>Remaining energy</p><strong>@passport.Performance.RemainingEnergy.ToString("0.##") kWh</strong></div>
-                    <div class="bp-metric"><p>Full cycles</p><strong>@passport.Performance.Cycles.ToString("0.##")</strong></div>
-                </div>
-                @if (passport.IsHistoricalPassport)
-                {
-                    <div class="bp-warning mt-4">
-                        <strong>Telemetry</strong>
-                        <p class="mb-0">Live telemetry is available from the latest passport for this battery.</p>
-                    </div>
-                }
-                else
-                {
-                    <h3 class="bp-subheading mt-4">Operational status</h3>
-                    <div class="bp-metric-grid mt-2">
-                        <div class="bp-metric"><p>Active status</p><strong>@(passport.Operations.IsActive ? "Active" : "Not active")</strong></div>
-                        <div class="bp-metric"><p>Current consumption</p><strong>@(passport.Operations.CurrentConsumptionKwh?.ToString("0.##") ?? "-") kWh</strong></div>
-                        <div class="bp-metric"><p>Current charge level</p><strong>@(passport.Operations.CurrentChargeLevelPct?.ToString("0.##") ?? "-") %</strong></div>
-                        <div class="bp-metric"><p>Current voltage</p><strong>@(passport.Operations.CurrentVoltageV?.ToString("0.##") ?? "-") V</strong></div>
-                        <div class="bp-metric"><p>Current current</p><strong>@(passport.Operations.CurrentCurrentA?.ToString("0.##") ?? "-") A</strong></div>
-                        <div class="bp-metric"><p>Last telemetry timestamp</p><strong>@(string.IsNullOrWhiteSpace(passport.Operations.LatestMeasuredAt) ? "-" : passport.Operations.LatestMeasuredAt)</strong></div>
-                        <div class="bp-metric"><p>Location of use</p><strong>@passport.Operations.LocationSiteName</strong></div>
-                        <div class="bp-metric"><p>Contact person</p><strong>@passport.Operations.ContactName</strong></div>
-                    </div>
-                    <div class="bp-field-grid mt-2">
-                        <div><dt>Address</dt><dd>@passport.Operations.LocationAddress</dd></div>
-                        <div><dt>City</dt><dd>@passport.Operations.LocationCity</dd></div>
-                        <div><dt>Country</dt><dd>@passport.Operations.LocationCountry</dd></div>
-                        <div><dt>Contact email</dt><dd>@passport.Operations.ContactEmail</dd></div>
-                        <div><dt>Contact phone</dt><dd>@passport.Operations.ContactPhone</dd></div>
-                    </div>
-                    <h3 class="bp-subheading mt-4">Telemetry history (last week)</h3>
-                    <div class="bp-telemetry-chart-grid mt-3">
-                        <div class="bp-card bp-telemetry-chart-card">
-                            <p class="bp-help-text mb-2">Current consumption (kWh)</p>
-                            <div class="bp-chart-frame"><canvas id="chart-consumption"></canvas></div>
-                        </div>
-                        <div class="bp-card bp-telemetry-chart-card">
-                            <p class="bp-help-text mb-2">Current charge level (%)</p>
-                            <div class="bp-chart-frame"><canvas id="chart-charge"></canvas></div>
-                        </div>
-                        <div class="bp-card bp-telemetry-chart-card">
-                            <p class="bp-help-text mb-2">Current voltage (V)</p>
-                            <div class="bp-chart-frame"><canvas id="chart-voltage"></canvas></div>
-                        </div>
-                        <div class="bp-card bp-telemetry-chart-card">
-                            <p class="bp-help-text mb-2">Current current (A)</p>
-                            <div class="bp-chart-frame"><canvas id="chart-current"></canvas></div>
-                        </div>
-                    </div>
-                }
-            </div>
-
-            <div class="tab-pane fade" id="tab-compliance" role="tabpanel">
-                <h2 class="h5 mb-2">Compliance</h2>
-                <p class="bp-help-text">Regulatory compliance of the battery</p>
-                <div class="bp-link-grid mt-3">
-                    @if (!string.IsNullOrWhiteSpace(passport.Documents.ConformityAssessment.Url))
-                    {
-                        <a href="@passport.Documents.ConformityAssessment.Url" target="_blank" rel="noreferrer">@passport.Documents.ConformityAssessment.Label</a>
-                    }
-                    @if (!string.IsNullOrWhiteSpace(passport.Documents.EuDeclarationOfConformity.Url))
-                    {
-                        <a href="@passport.Documents.EuDeclarationOfConformity.Url" target="_blank" rel="noreferrer">@passport.Documents.EuDeclarationOfConformity.Label</a>
-                    }
-                </div>
-            </div>
-
-            <div class="tab-pane fade" id="tab-supply" role="tabpanel">
-                <h2 class="h5 mb-2">Supply chain</h2>
-                <p class="bp-help-text">Supply chain information</p>
-                <div class="bp-two-column mt-4">
-                    <div class="bp-metric"><p>Supply chain index</p><strong>@passport.SupplyChainIndex.ToString("0.##")</strong></div>
-                    <div class="bp-link-grid">
-                        @if (!string.IsNullOrWhiteSpace(passport.Documents.SustainabilityReport.Url))
-                        {
-                            <a href="@passport.Documents.SustainabilityReport.Url" target="_blank" rel="noreferrer">@passport.Documents.SustainabilityReport.Label</a>
-                        }
-                        @if (!string.IsNullOrWhiteSpace(passport.Documents.DueDiligenceReport.Url))
-                        {
-                            <a href="@passport.Documents.DueDiligenceReport.Url" target="_blank" rel="noreferrer">@passport.Documents.DueDiligenceReport.Label</a>
-                        }
-                        @if (!string.IsNullOrWhiteSpace(passport.Documents.ThirdPartyAudit.Url))
-                        {
-                            <a href="@passport.Documents.ThirdPartyAudit.Url" target="_blank" rel="noreferrer">@passport.Documents.ThirdPartyAudit.Label</a>
-                        }
-                        @if (!string.IsNullOrWhiteSpace(passport.Documents.TaxonomyReport.Url))
-                        {
-                            <a href="@passport.Documents.TaxonomyReport.Url" target="_blank" rel="noreferrer">@passport.Documents.TaxonomyReport.Label</a>
-                        }
-                    </div>
-                </div>
-            </div>
-
-            <div class="tab-pane fade" id="tab-circularity" role="tabpanel">
-                <h2 class="h5 mb-2">Circularity</h2>
-                <p class="bp-help-text">Circularity of the battery</p>
-                <dl class="bp-field-grid">
-                    <div><dt>Separate collection</dt><dd>@passport.Circularity.SeparateCollection</dd></div>
-                    <div><dt>Waste prevention</dt><dd>@passport.Circularity.WastePrevention</dd></div>
-                    <div><dt>Recycled content share</dt><dd>@passport.Circularity.RecycledContentShareVerification</dd></div>
-                </dl>
-                <div class="bp-donut-grid mt-3">
-                    @foreach (var chart in passport.RecycledContentCharts)
-                    {
-                        var preConsumer = chart.PreConsumerShare.ToString("0.##", CultureInfo.InvariantCulture);
-                        var postStart = chart.PreConsumerShare.ToString("0.##", CultureInfo.InvariantCulture);
-                        var postEnd = (chart.PreConsumerShare + chart.PostConsumerShare).ToString("0.##", CultureInfo.InvariantCulture);
-                        var donutBackground = $"conic-gradient(#08a348 0 {preConsumer}%, #df6b3b {postStart}% {postEnd}%, #d1d5db {postEnd}% 100%)";
-                        <div class="bp-donut-card">
-                            <p class="fw-semibold mb-2">@chart.Material</p>
-                            <div class="bp-donut" style="background:@donutBackground;">
-                                <div class="bp-donut-hole"></div>
-                            </div>
-                            <table class="bp-chart-value-legend mt-2" aria-label="Recycled content values for @chart.Material">
-                                <tbody>
-                                    <tr>
-                                        <td><span class="bp-dot" style="background:#08a348"></span>Pre-consumer</td>
-                                        <td>@chart.PreConsumerShare.ToString("0.#")%</td>
-                                    </tr>
-                                    <tr>
-                                        <td><span class="bp-dot" style="background:#df6b3b"></span>Post-consumer</td>
-                                        <td>@chart.PostConsumerShare.ToString("0.#")%</td>
-                                    </tr>
-                                </tbody>
-                            </table>
-                        </div>
-                    }
-                </div>
-            </div>
-
-            <div class="tab-pane fade" id="tab-carbon" role="tabpanel">
-                <h2 class="h5 mb-2">Carbon Footprint</h2>
-                <p class="bp-help-text">CO2 footprint generated by the battery</p>
-                <div class="bp-two-column mt-4">
-                    <div class="bp-pie-row">
-                        <div class="bp-pie" style="background:@BuildConicGradient(passport.CarbonFootprintSegments);"></div>
-                        <table class="bp-chart-value-legend" aria-label="Carbon footprint values">
-                            <tbody>
-                                @foreach (var segment in passport.CarbonFootprintSegments)
-                                {
-                                    <tr>
-                                        <td><span class="bp-dot" style="background:@(string.IsNullOrWhiteSpace(segment.Color) ? "#4f6f7d" : segment.Color);"></span>@segment.Label</td>
-                                        <td>@segment.Value.ToString("0.#") @(segment.Unit)</td>
-                                    </tr>
-                                }
-                            </tbody>
-                        </table>
-                    </div>
-                    <div>
-                        <dl class="bp-field-grid">
-                            <div><dt>Amount</dt><dd>Verified @passport.CarbonFootprintLabel</dd></div>
-                            <div><dt>Performance class</dt><dd>@passport.PerformanceClass</dd></div>
-                        </dl>
-                        @if (!string.IsNullOrWhiteSpace(passport.Documents.Co2StudyReference.Url))
-                        {
-                            <a href="@passport.Documents.Co2StudyReference.Url" target="_blank" rel="noreferrer" class="bp-secondary-button d-inline-block mt-2">@passport.Documents.Co2StudyReference.Label</a>
-                        }
-                    </div>
-                </div>
-            </div>
-
-            @if (Model.CanViewTrustConformance)
-            {
-                <div class="tab-pane fade" id="tab-trust" role="tabpanel">
-                    <h2 class="h5 mb-2">Trust & conformance</h2>
-                    <p class="bp-help-text">Privileged verification and conformance evidence for the signed passport core.</p>
-                    <div class="bp-trust-panel">
-                        <div>
-                            <span class="bp-summary-title">Verification evidence</span>
-                            <dl class="bp-field-grid">
-                                <div><dt>Conformance state</dt><dd>@passport.TrustState</dd></div>
-                                <div><dt>Proof status</dt><dd>@passport.TrustProofStatus</dd></div>
-                                <div><dt>Dirty state</dt><dd>@(passport.TrustIsDirty ? "Dirty" : "Clean")</dd></div>
-                                <div><dt>Issuer</dt><dd>@(string.IsNullOrWhiteSpace(passport.TrustIssuer) ? "-" : passport.TrustIssuer)</dd></div>
-                                <div><dt>Verification method</dt><dd>@(string.IsNullOrWhiteSpace(passport.TrustVerificationMethod) ? "-" : passport.TrustVerificationMethod)</dd></div>
-                                <div><dt>Signed date</dt><dd>@(string.IsNullOrWhiteSpace(passport.TrustLastSignedAt) ? "-" : passport.TrustLastSignedAt)</dd></div>
-                                <div><dt>Revision</dt><dd>@(string.IsNullOrWhiteSpace(passport.TrustLatestRevisionId) ? "-" : passport.TrustLatestRevisionId)</dd></div>
-                                <div><dt>Hash</dt><dd><code>@(string.IsNullOrWhiteSpace(passport.TrustLatestHash) ? "-" : passport.TrustLatestHash)</code></dd></div>
-                                <div><dt>Diagnostic</dt><dd>@passport.TrustVerificationMessage</dd></div>
-                            </dl>
-                        </div>
-                    </div>
-                </div>
-            }
-        </div>
-    </section>
+    <partial name="_PassportHero" model="Model" view-data="ViewData" />
+    <partial name="_PassportTabs" model="Model" />
 </main>
-
-@section Scripts {
-    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.3/dist/chart.umd.min.js"></script>
-    <script>
-        (() => {
-            const telemetryRows = @Html.Raw(telemetryJson);
-            const rows = (Array.isArray(telemetryRows) ? telemetryRows : [])
-                .filter(row => Number.isFinite(Date.parse(row?.MeasuredAt ?? '')))
-                .sort((left, right) => Date.parse(left.MeasuredAt) - Date.parse(right.MeasuredAt));
-            if (!Array.isArray(rows) || rows.length === 0 || !window.Chart) {
-                return;
-            }
-
-            const labels = rows.map(row => row.MeasuredAt ? row.MeasuredAt.substring(5, 16).replace('T', ' ') : '');
-            const values = (field) => rows.map(row => Number.isFinite(Number(row[field])) ? Number(row[field]) : null);
-            const commonOptions = {
-                responsive: true,
-                maintainAspectRatio: false,
-                scales: { y: { beginAtZero: false } }
-            };
-
-            const renderLine = (canvasId, label, data, color) => {
-                const canvas = document.getElementById(canvasId);
-                if (!canvas) return;
-                new Chart(canvas, {
-                    type: 'line',
-                    data: {
-                        labels,
-                        datasets: [{ label, data, borderColor: color, backgroundColor: color + "33", tension: 0.25 }]
-                    },
-                    options: commonOptions
-                });
-            };
-
-            renderLine('chart-consumption', 'kWh', values('CurrentConsumptionKwh'), '#0f766e');
-            renderLine('chart-charge', '%', values('CurrentChargeLevelPct'), '#1d4ed8');
-            renderLine('chart-voltage', 'V', values('CurrentVoltageV'), '#b45309');
-            renderLine('chart-current', 'A', values('CurrentCurrentA'), '#7c3aed');
-        })();
-    </script>
-}
diff --git a/web/Views/Passport/Summary.cshtml b/web/Views/Passport/Summary.cshtml
--- a/web/Views/Passport/Summary.cshtml
+++ b/web/Views/Passport/Summary.cshtml
@@ -1,157 +1,99 @@
 @using System.Globalization
-@model BatteryPassWeb.Models.ViewModels.PassportSummaryPageViewModel
+@using BatteryPassWeb.Dpp.Pages
+@model PassportPage
 @{
-    ViewData["Title"] = "Passport Summary";
-    var passport = Model.Passport;
-    var qrBatteryId = string.IsNullOrWhiteSpace(passport.BatteryId) ? passport.PassportId : passport.BatteryId;
+    // The public summary (design 12): no sign-in, opened from the battery's QR code. Public data only.
+    ViewData["Title"] = $"Battery passport · {Model.Text(11)}";
+    ViewData["Detail"] = false;
+    var colours = new[] { "#22577a", "#38a3a5", "#f2a541", "#57cc99" };
+    var carbonTotal = Model.Carbon.Sum(part => part.Value);
+    var power = Model.Value(67);
 
-    string BuildConicGradient(IEnumerable<BatteryPassWeb.Models.ViewModels.ChartSegmentViewModel> segments)
+    string Pie()
     {
-        var start = 0d;
+        var start = 0m;
         var slices = new List<string>();
-        foreach (var segment in segments.Where(segment => segment.Value > 0))
+        for (var index = 0; index < Model.Carbon.Count; index++)
         {
-            var color = string.IsNullOrWhiteSpace(segment.Color) ? "#4f6f7d" : segment.Color;
-            var end = start + segment.Percentage;
-            slices.Add($"{color} {start.ToString("0.##", CultureInfo.InvariantCulture)}% {end.ToString("0.##", CultureInfo.InvariantCulture)}%");
+            var end = carbonTotal == 0 ? 0 : start + Model.Carbon[index].Value / carbonTotal * 100;
+            slices.Add($"{colours[index]} {start.ToString("0.##", CultureInfo.InvariantCulture)}% {end.ToString("0.##", CultureInfo.InvariantCulture)}%");
             start = end;
         }
 
-        if (slices.Count == 0)
-        {
-            slices.Add("#d1d5db 0% 100%");
-        }
-
-        return $"conic-gradient({string.Join(", ", slices)})";
+        return carbonTotal == 0 ? "conic-gradient(#d1d5db 0% 100%)" : $"conic-gradient({string.Join(", ", slices)})";
     }
 }
 <main class="bp-page">
-    <section class="bp-report-hero-card">
-        <div class="bp-report-hero-layout">
-            <article class="bp-report-hero-info bp-summary-identity-grid bp-report-identity-grid">
-                <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--battery bp-report-header bp-report-title-block">
-                    <span class="bp-summary-title">Battery</span>
-                    <h1>@(string.IsNullOrWhiteSpace(passport.BatterySerialNumber) ? passport.PassportId : passport.BatterySerialNumber)</h1>
-                    <p class="text-secondary">Cluster: @passport.ClusterLabel</p>
-                    <div class="d-flex flex-wrap gap-2 align-items-center mt-3">
-                        @if (passport.IsHistoricalPassport)
-                        {
-                            <span class="bp-status-pill is-draft">Historical passport</span>
-                            <a href="@passport.LatestPassportUrl" class="bp-report-action" aria-label="Open latest passport" title="Open latest passport" data-tooltip="Open latest passport" data-action-icon="latest">
-                                <svg class="bp-report-action-icon bp-latest-passport-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                    <path d="M12 5a7 7 0 1 1-6.32 4" />
-                                    <path d="M5 5v4h4" />
-                                    <path d="M12 8v4l3 2" />
-                                </svg>
-                            </a>
-                        }
-                        else
-                        {
-                            <span class="bp-status-pill is-published">Latest passport</span>
-                        }
-                    </div>
-                </section>
-
-                <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--identifiers">
-                    <span class="bp-summary-title">Identifiers</span>
-                    <p class="bp-passport-id"><span class="bp-id-label">Passport ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@passport.PassportId</span>@await Html.PartialAsync("_CopyIdButton", passport.PassportId)</span></p>
-                    <p class="bp-passport-id"><span class="bp-id-label">Battery ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@(string.IsNullOrWhiteSpace(passport.BatteryId) ? "-" : passport.BatteryId)</span>@await Html.PartialAsync("_CopyIdButton", passport.BatteryId)</span></p>
-                </section>
+    <partial name="_PassportHero" model="Model" view-data="ViewData" />
 
-                <section class="bp-summary-id-panel bp-summary-qr-panel bp-report-identity-panel bp-report-identity-panel--qr">
-                    <span class="bp-summary-title">QR code</span>
-                    <a class="bp-report-hero-qr"
-                       href="/qr/@Uri.EscapeDataString(qrBatteryId)/download"
-                       aria-label="Save battery code for @qrBatteryId">
-                        <img src="/qr/@Uri.EscapeDataString(qrBatteryId)/svg" alt="QR code for @qrBatteryId" />
-                    </a>
-                </section>
-
-                <dl class="bp-field-grid bp-report-hero-fields bp-summary-fields-panel">
-                    <div><dt>Battery Family</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatteryFamily) ? "-" : passport.BatteryFamily)</dd></div>
-                    <div><dt>Battery serial number</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatterySerialNumber) ? "-" : passport.BatterySerialNumber)</dd></div>
-                    <div><dt>Battery Model</dt><dd>@(string.IsNullOrWhiteSpace(passport.BatteryModel) ? "-" : passport.BatteryModel)</dd></div>
-                    <div><dt>Software version</dt><dd>@(string.IsNullOrWhiteSpace(passport.SoftwareVersion) ? "-" : passport.SoftwareVersion)</dd></div>
-                    <div><dt>Category</dt><dd>@passport.Category</dd></div>
-                    <div><dt>Battery status</dt><dd>@passport.BatteryStatus</dd></div>
-                    <div><dt>Passport status</dt><dd>@passport.PassportStatus</dd></div>
-                    <div><dt>Weight</dt><dd>@passport.WeightLabel</dd></div>
-                    <div><dt>Manufactured date</dt><dd>@passport.ManufacturedDate</dd></div>
-                    <div><dt>Manufactured by</dt><dd>@passport.ManufacturerName</dd></div>
-                    <div><dt>Cluster</dt><dd>@passport.ClusterLabel</dd></div>
-                </dl>
-            </article>
-
-            <aside class="bp-report-hero-media">
-                <div class="bp-image-wrap">
-                    <img src="@passport.BatteryImageUrl" alt="@passport.BatteryImageAlt" class="bp-battery-image" />
-                </div>
-                @if (Model.CanOpenDetail)
-                {
-                    <a class="bp-report-hero-cta" href="@Model.DetailActionUrl">View more about this passport -&gt;</a>
-                }
-                else
+    @if (power is not null || Model.Carbon.Count > 0 || Model.Recycled.Count > 0)
+    {
+        // One row on wide screens: power, carbon footprint and recycled content side by side; stacked on narrow ones.
+        <section class="bp-card bp-report-wide-section">
+            <span class="bp-summary-title">Summary report</span>
+            <div class="row g-4 mt-1">
+                @if (power is not null)
                 {
-                    <a class="bp-report-hero-cta bp-report-hero-cta-warning" href="@Model.DetailActionUrl">@Model.DetailAccessNotice</a>
-                }
-            </aside>
-        </div>
-    </section>
-
-    <section class="bp-card bp-report-wide-section">
-        <span class="bp-summary-title">Summary report</span>
-        <div class="bp-summary-section">
-            <div class="bp-summary-report-row bp-summary-report-row--power">
-                <h3 class="bp-subheading">Original Power</h3>
-                <p class="bp-number">@(passport.Performance.RatedMaximumPower > 0 ? passport.Performance.RatedMaximumPower : 380) kW</p>
-            </div>
-            <div class="bp-summary-report-row bp-summary-report-row--carbon">
-                <h3 class="bp-subheading">Carbon footprint</h3>
-                <div class="bp-pie-row">
-                    <div class="bp-pie" style="background:@BuildConicGradient(passport.CarbonFootprintSegments);"></div>
-                    <table class="bp-chart-value-legend" aria-label="Carbon footprint values">
-                        <tbody>
-                            @foreach (var segment in passport.CarbonFootprintSegments)
+                    <div class="col-12 col-md-6 col-xl-3 bp-summary-report-row bp-summary-report-row--power">
+                        <h3 class="bp-subheading">Original power capability</h3>
+                        <div class="bp-metric-grid mt-2" style="grid-template-columns: 1fr;">
+                            @foreach (var line in power.Lines)
                             {
-                                <tr>
-                                    <td><span class="bp-dot" style="background:@(string.IsNullOrWhiteSpace(segment.Color) ? "#4f6f7d" : segment.Color);"></span>@segment.Label</td>
-                                    <td>@segment.Value.ToString("0.#") @(segment.Unit)</td>
-                                </tr>
+                                <div class="bp-metric"><p>@(line.Label ?? "Power")</p><strong>@line.Text</strong></div>
                             }
-                        </tbody>
-                    </table>
-                </div>
-            </div>
-            <div class="bp-summary-report-row bp-summary-report-row--recycled">
-                <h3 class="bp-subheading">Recycled content share</h3>
-                <div class="bp-donut-grid">
-                    @foreach (var chart in passport.RecycledContentCharts)
-                    {
-                        var preConsumer = chart.PreConsumerShare.ToString("0.##", CultureInfo.InvariantCulture);
-                        var postStart = chart.PreConsumerShare.ToString("0.##", CultureInfo.InvariantCulture);
-                        var postEnd = (chart.PreConsumerShare + chart.PostConsumerShare).ToString("0.##", CultureInfo.InvariantCulture);
-                        var donutBackground = $"conic-gradient(#08a348 0 {preConsumer}%, #df6b3b {postStart}% {postEnd}%, #d1d5db {postEnd}% 100%)";
-                        <div class="bp-donut-card">
-                            <p class="fw-semibold mb-2">@chart.Material</p>
-                            <div class="bp-donut" style="background:@donutBackground;">
-                                <div class="bp-donut-hole"></div>
-                            </div>
-                            <table class="bp-chart-value-legend mt-2" aria-label="Recycled content values for @chart.Material">
+                        </div>
+                    </div>
+                }
+                @if (Model.Carbon.Count > 0)
+                {
+                    <div class="col-12 col-md-6 col-xl-5 bp-summary-report-row bp-summary-report-row--carbon">
+                        <h3 class="bp-subheading">Carbon footprint</h3>
+                        @if (Model.Value(30) is { } perUnit)
+                        {
+                            <p class="text-secondary small mb-2">@perUnit.Lines[0].Text per functional unit</p>
+                        }
+                        <div class="bp-pie-row">
+                            <div class="bp-pie" style="width:140px;height:140px;background:@Pie();"></div>
+                            <table class="bp-chart-value-legend" style="flex:1 1 200px;width:auto;min-width:0;" aria-label="Carbon footprint per life-cycle stage">
                                 <tbody>
-                                    <tr>
-                                        <td><span class="bp-dot" style="background:#08a348"></span>Pre-consumer</td>
-                                        <td>@chart.PreConsumerShare.ToString("0.#")%</td>
-                                    </tr>
-                                    <tr>
-                                        <td><span class="bp-dot" style="background:#df6b3b"></span>Post-consumer</td>
-                                        <td>@chart.PostConsumerShare.ToString("0.#")%</td>
-                                    </tr>
+                                    @for (var index = 0; index < Model.Carbon.Count; index++)
+                                    {
+                                        <tr>
+                                            <td><span class="bp-dot" style="background:@colours[index]"></span>@Model.Carbon[index].Label</td>
+                                            <td>@ValueWords.Number(Model.Carbon[index].Value) @Model.Carbon[index].Unit</td>
+                                        </tr>
+                                    }
                                 </tbody>
                             </table>
                         </div>
-                    }
-                </div>
+                    </div>
+                }
+                @if (Model.Recycled.Count > 0)
+                {
+                    <div class="col-12 col-xl-4 bp-summary-report-row bp-summary-report-row--recycled">
+                        <h3 class="bp-subheading">Recycled content share</h3>
+                        <div class="row g-2 mt-1">
+                            @foreach (var share in Model.Recycled)
+                            {
+                                var percent = Math.Clamp(share.Value, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
+                                <div class="col-6 col-sm-3 col-xl-6">
+                                    <div class="bp-donut-card d-flex align-items-center gap-2">
+                                        <div class="bp-donut flex-shrink-0 m-0" style="width:48px;height:48px;background:conic-gradient(#08a348 0 @(percent)%, #d1d5db @(percent)% 100%);" aria-hidden="true">
+                                            <div class="bp-donut-hole" style="width:26px;height:26px;"></div>
+                                        </div>
+                                        <div class="lh-sm">
+                                            <span class="d-block small text-secondary">@share.Label</span>
+                                            <strong>@ValueWords.Number(share.Value) %</strong>
+                                        </div>
+                                    </div>
+                                </div>
+                            }
+                        </div>
+                    </div>
+                }
             </div>
-        </div>
-    </section>
+        </section>
+    }
+
+    <partial name="_PassportTabs" model="Model" />
 </main>
```

- [ ] **Step 6: Delete the files**

```bash
git rm -q web/Views/Passport/Battery.cshtml
```

- [ ] **Step 7: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryRouteAndRegistryTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DocumentAccessControlTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiInitializerTests|FullyQualifiedName~BatteryPassWeb.Tests.ManualFeedbackRound3Tests|FullyQualifiedName~BatteryPassWeb.Tests.PassportSoftwarePresentationTests|FullyQualifiedName~BatteryPassWeb.Tests.QrWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:   118`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2059`, none failed.

- [ ] **Step 8: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/ConformanceLayoutTests.cs BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs BatteryPassWeb.Tests/DocumentAccessControlTests.cs BatteryPassWeb.Tests/ExternalApiInitializerTests.cs BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs BatteryPassWeb.Tests/QrWorkflowTests.cs BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs web/Views/Passport/_PassportHero.cshtml web/Views/Passport/_PassportTabs.cshtml web/Controllers/HomeController.cs web/Controllers/PassportController.cs web/Controllers/QrController.cs web/Services/PassportQrCodeService.cs web/Views/Dpp/NotAvailable.cshtml web/Views/Home/Index.cshtml web/Views/Passport/Detail.cshtml web/Views/Passport/Summary.cshtml
git commit -m "feat(dpp): the public summary and detail page, the QR code and the search replace today's passport pages" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2059, Skipped:     0, Total:  2059`: the 2019 from before, 37 new in Task 1 and a net 3 in Task 2 (18 new, 15 older ones removed).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Look at the pages**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too. Never point it at `battery_pass_demo`. The sample data from 3a's check is enough. Check, in light and dark theme:

1. **Signed out, the home page:** search `SAMPLE-3A-0001`. Its public summary opens at `/dpp/urn:x-battery-pass:TEST-…`: the test banner, the battery card with the QR code and, if its family has one, the family's picture, the summary report in one row, and the public tabs. No cluster, no battery status, no state of charge.
2. **The QR code:** click it to save it, then on the home page use the QR icon, "Scan an image file", and pick the saved file: the same summary opens.
3. **Sign in as a team member** from the link under the picture: you come back to the detail page. Its tabs start with Purchaser, Shared and Authorities (with a lock); each value has its grey "Entered … · form" line, live values "Measured …".
4. **Purchaser tab, "Information on accidents":** opens the battery's accident page with its two reports.
5. **A draft:** search `SAMPLE-3A-0003`: "No published battery passport was found".
6. **An older version, if a battery has one** (the battery page's Versions list shows more than one): open `/dpp/<its passport ID>`: "Archived", "Open the current version", and the Purchaser tab's note about live values.
7. **A narrow window** (about 390 px): neither page scrolls sideways.

## After this plan

- **3c** rebuilds the external API on the new model; `/api/passports` becomes team-only. Its passport reads use `PassportPages`' finding rules and `PassportFilter`.
- **3d** removes the old model and pages and resets `battery_pass_v3`. Until then, two old pages still link today's passport addresses, which now say "not found": the admin's Conformance page and the cluster admins' "Managed passports" (and the registry lists old passports). They go in 3d.
- **Step 4** adds inactive, invalid and marked-for-deletion passports: the battery card then names those statuses, an inactive passport links its successor (design 12), and the Purchaser tab gains the repair history (7.9).
- **Known small gaps:**
  - The team's battery page doesn't link the public pages yet; the search finds them.
  - The detail page no longer shows today's telemetry charts; the live-value history comes back through the API in 3c.
  - The QR code carries APP_BASE_URL, so a code saved from the local app opens only on the same PC.

