# DPP V3 Step 6d-1: The Battery ID Scheme and Its Look — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The first of 6d's three parts (the owner, 5 and 6 Nov 2026): the real battery and passport IDs, in place of the test URNs, and how pages show them.
- **6d-1 (this plan)** makes the battery ID from the serial number typed when a battery is created.
- **6d-2** moves that to IDs released in advance on a "Battery IDs" page, picked when a battery is created.
- **6d-3** brings the Help, the test pack and the role check up to date.

1. **Battery ID (#6).** An ISO/IEC 15459 item ID: issuing agency `UN` (Dun & Bradstreet), the company's DUNS `778474481`, and the battery's serial number (#11) with only its letters A–Z and digits 0–9, upper-cased. It is written as a web address with the MH10.8.2 data identifier `25S` (ISO/IEC 18975). Serial `VPC-12221` gives `https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221`.
   - **Made when the battery is created** (in 6d-2: when its serial number is released).
   - **A new passport of the same battery** (a successor) adds `R1`, `R2`, …, the next free one, from its own cleaned serial number: `…VPC12221R1`.
   - **Refused, with a clear message, when the ID can't be made**:
     - the ID is already another battery's (`VPC-12221` and `vpc 12221` give the same one), naming that battery;
     - the serial number has no letters or digits;
     - it has more than 36 letters and digits (32 for a demo battery), which leaves room for `R99` within 50 characters (ISO/IEC 15459-4's limit as recalled; not yet checked against the standard's text);
     - a real battery's serial number starts with `TEST`.
   - **Demo batteries** are marked `TEST` after the DUNS: `UN778474481TESTDEMOC7M0001`.
2. **Passport ID (#5)**, per version: `https://www.sib-scania-eubp.eu/dpp/UN778474481VPC12221/v1`. The app serves that address, and `/25S/<item>` opens the battery's public page, as a scanned QR code does.
3. **Shown short:** pages show the short ID (`UN778474481TESTDEMOC7M0001`; `…/v3` for a passport ID). The full address is the link and what the copy button copies. Printouts show the full address. On a phone the battery page header wraps the ID instead of cutting it.
4. **#7 and #12** (Sites and operators) get the hint "The DUNS number in the ISO/IEC 6523 form: iso6523-actorid-upis::0060: and its 9 digits". A value in another form gets a gentle note and is still saved as it is.
   - The demo data uses that form: the Scania operator and both plants have the real DUNS `778474481`; Demo Fleet Services has a made-up `999999999`.
5. **The test-ID notes** no longer say real IDs wait for a scheme. They now say the IDs are "marked TEST": the detail page, the accidents page and the "Test ID" tag's explainer.

**Settings:** the live site and any copy that issues real IDs set all three. Without them the app keeps issuing the test URNs (`urn:x-battery-pass:TEST-…`), as on a developer's PC and in the tests.

| Setting | Value |
|---|---|
| `DPP_ID_AGENCY` | `UN` |
| `DPP_ID_COMPANY` | `778474481` |
| `DPP_ID_BASE_URL` | `https://www.sib-scania-eubp.eu` |

`APP_BASE_URL` stays `https://www.sib-scania-eubp.eu/`, so the QR code carries #6 itself.

**Architecture:**
- **`web/Dpp/Identifiers/DunsIdentifierScheme.cs`** (new):
  - `DunsIdentifierScheme`:
    - `ItemFor(serial, demo)` makes the ID from a serial number and checks it on its own: letters or digits, the length, no `TEST` start for a real battery;
    - `NewPackIdentifierAsync(pack)` adds the taken check (`DppStore.FindPackByUniqueProductIdentifierAsync`), and for a successor the next free `R<n>` (`DppStore.FindPackIdentifiersWithSuffixesAsync`).
  - `IdentifierForms` holds the forms the pages share: `ItemAddress`, `PassportAddress`, `Short`, `Display`, `PagePath`, `IsItem`, `IsWebAddress`, `IsTest`, `Clean`, and the #7/#12 hint and note.
- **`IIdentifierScheme`:**
  - `NewPackIdentifierAsync(BatteryPack)` returns a `NewIdentifier(Identifier, Problem)`;
  - `NewPassportIdentifier(packIdentifier, version)`;
  - `ItemFor(serial, demo)`.
  - `TestIdentifierScheme` keeps its URNs. `AddDpp` picks the DUNS scheme when the three settings are valid (`IdentifierForms.IsConfigured`).
- **Creating a pack:**
  - `PackCreationService` asks the scheme for #6 and refuses with "the battery can't get its battery ID: …" (`PackCreationService.CantGetId`);
  - `NewPack.IsDemo` marks the demo batteries, and a successor keeps its predecessor's (`SuccessionService`).
- **`PublicationService`** works out the version number first, so the passport ID can name it.
- **`DppStore`:** the #6 lookup also takes the short item (escaped), and `FindPackIdentifiersWithSuffixesAsync` lists the R-numbers in use.
- **Addresses:**
  - `/25S/{item}` (new `ItemIdController`) opens the public page; `HomeController` treats `25S` as a route word;
  - `/dpp/{item}/v{n}` and `/dpp/{item}/v{n}/detail` show that version (`PassportController`, with named views);
  - every passport-page and QR link uses `PassportPages.SummaryHref`/`DetailHref`/`QrHref`/`QrDownloadHref`, so the pages' addresses use the short item: the team's battery page, its versions card, the cluster admin's page and the row actions;
  - `DppLinks.PassportUrl` gives `APP_BASE_URL/25S/<item>` for the QR code.
- **Views:**
  - new `Shared/_IdValue.cshtml`: the short ID linked to the full address, the full address copied and printed;
  - it is used by both battery headers, the passport hero, the passport chain and the accidents page;
  - `_EditorField.cshtml` shows the #7/#12 hint and note;
  - `_PassportHero`, `Dpp/Accidents` and `Shared/_TestTag` say test IDs are "marked TEST".
- **CSS:**
  - `bp-status.css`: the hero's IDs wrap rather than end in "…";
  - `bp-product-data.css`: the battery page header's ID wraps on a phone instead of ending in "…".
- **Settings:** `BatteryPassOptions.IdAgency`/`IdCompany`/`IdBaseUrl` from `DPP_ID_AGENCY`/`DPP_ID_COMPANY`/`DPP_ID_BASE_URL` (`Program.cs`).
- **Demo data:** `demo-catalogue.json`: #7 and #12 in the ISO/IEC 6523 form.
- **Docs:** the design's section 10, O-1 and attribute #6.
- **`site.css` is not changed.** No new collection, and nothing new for the reset tool.
- **Tests:**
  - new `BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs`: IDs, refusals, R-numbers, addresses, the QR code's address, the demo data's IDs, a creation refused for a taken ID, the #7/#12 note and the demo values, the test-ID notes and the short display;
  - changed: `TestIdentifierSchemeTests` (the new interface), `PassportControllerTests` (named view), `BatteryPageLayoutTests`, `ReviewFixesTests`, `PassportLinksSameTabTests` and `RowActionsTests` (the new link markup);
  - `DemoTesting` gains `WithDunsIds`.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, section 10 and O-1. The owner's decisions of 5 and 6 Nov 2026. The managing session's notes: `step6-o1-identifier-options.md`, `step6d-id-release-design.md` (the whole of 6d) and `step6d-shots-v3/` (this part's look).

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at db7bd45 (plan 6c's commit, "fix(dpp): the role-matrix check's faults…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas. The tests use a throwaway MongoDB (EphemeralMongo). The settings in the Goal go to the live site at the cutover (plan 6e), not here.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2542` (plan 6c's result). After this plan: 2579.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6d1-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## Found while prototyping

- **The owner's shots** of this part's look (`step6d-shots-v3/`, 1280 and 375) came from the same code with #6 made at first publication; 6d-1 makes it at creation, so a draft has its ID and QR code as before.
- **Checked by eye at 1280 and 375, and fixed before this plan:**
  - at 375 the battery page header cut the ID at the column's edge, because an older rule in `bp-product-data.css` ended it in "…". The telling part of an ID is its end, so it now wraps;
  - the detail page's test-ID note still said real IDs come "once the identifier scheme is chosen".
  - A script checked every ID and toolbar link in the team's headers: all fit, none overlap, at 1280 and 375.
- **The versions card** linked a version with the escaped full ID (`/dpp/https%3A%2F%2F…`). It now uses the version's own address, `/dpp/<item>/v<n>/detail`.
- **ISO/IEC 15459-4:2014**, clause 5.3: the identifier may use the invariant ISO/IEC 646 set, so `-` is allowed; A–Z and 0–9 are the compact choice. Keeping only those, as the owner chose, also keeps the web address clean.

**Measured on a clean clone of db7bd45:**
- the tests first fail to build (`DunsIdentifierScheme` doesn't exist yet);
- with the code, Task 1's own classes pass: 98 tests across the 7 classes this plan adds or changes;
- the whole suite: 2577 of 2579 pass (the clean clone's two known `ApplicationAuditCoverageTests` failures, `site.css` with CRLF line endings, pass in the worktree).

### Task 1: The battery ID scheme and its look

**Files:**
- Create: `BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs`, `web/Dpp/Identifiers/DunsIdentifierScheme.cs`, `web/Controllers/ItemIdController.cs`, `web/Views/Shared/_IdValue.cshtml`
- Modify (tests): `BatteryPassWeb.Tests/Demo/DemoTesting.cs`, `BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs`, `BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs`, `BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs`, `BatteryPassWeb.Tests/Dpp/RowActionsTests.cs`, `BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs`, `BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs`
- Modify (app and docs): `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, `web/Configuration/BatteryPassOptions.cs`, `web/Controllers/HomeController.cs`, `web/Controllers/PassportController.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`, `web/Dpp/Documents/DppLinks.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Editing/SuccessionService.cs`, `web/Dpp/Identifiers/TestIdentifierScheme.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Pages/RowActions.cs`, `web/Dpp/Publication/PublicationService.cs`, `web/Dpp/Store/DppStore.cs`, `web/Program.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/Batteries/_VersionsCard.cshtml`, `web/Views/ClusterAdmin/Battery.cshtml`, `web/Views/Dpp/Accidents.cshtml`, `web/Views/Passport/_PassportHero.cshtml`, `web/Views/ProductData/_EditorField.cshtml`, `web/Views/Shared/_PassportChain.cshtml`, `web/Views/Shared/_TestTag.cshtml`, `web/wwwroot/css/bp-product-data.css`, `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

Save as `../6d1-test.patch` and run `git apply ../6d1-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoTesting.cs b/BatteryPassWeb.Tests/Demo/DemoTesting.cs
index 369c742..5ca4f5a 100644
--- a/BatteryPassWeb.Tests/Demo/DemoTesting.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoTesting.cs
@@ -23,11 +23,20 @@ internal sealed class DemoRig(ServiceProvider services, IMongoDatabase database)
     public DemoDataService Demo => Services.GetRequiredService<DemoDataService>();
     public DppStore Store => Services.GetRequiredService<DppStore>();
 
-    public static async Task<DemoRig> StartAsync(MongoFixture mongo, string? password = Password, string? appBaseUrl = DocumentTesting.BaseUrl)
+    /// <summary>The owner's identifier settings (step 6d), for the rigs that issue DUNS battery IDs.</summary>
+    public static BatteryPassOptions WithDunsIds(BatteryPassOptions options)
+    {
+        options.IdAgency = "UN";
+        options.IdCompany = "778474481";
+        options.IdBaseUrl = "https://www.sib-scania-eubp.eu";
+        return options;
+    }
+
+    public static async Task<DemoRig> StartAsync(MongoFixture mongo, string? password = Password, string? appBaseUrl = DocumentTesting.BaseUrl, bool dunsIds = false)
     {
         var context = mongo.NewContext();
         var services = new ServiceCollection()
-            .AddSingleton(Options.Create(new BatteryPassOptions { AppBaseUrl = appBaseUrl ?? string.Empty, DemoUsersPassword = password }))
+            .AddSingleton(Options.Create(dunsIds ? WithDunsIds(new BatteryPassOptions { AppBaseUrl = appBaseUrl ?? string.Empty, DemoUsersPassword = password }) : new BatteryPassOptions { AppBaseUrl = appBaseUrl ?? string.Empty, DemoUsersPassword = password }))
             .AddSingleton(context)
             .AddSingleton<ApplicationAuditService>()
             .AddSingleton<ClusterRepository>()
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
index af6839a..629a344 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs
@@ -81,7 +81,7 @@ public sealed class BatteryPageLayoutTests
         Assert.Contains("image.dataset.trimmed = 'true';", trim, StringComparison.Ordinal);
         Assert.True(header.IndexOf("bp-bh-picture", StringComparison.Ordinal) < header.IndexOf("<div class=\"bp-bh-id\">", StringComparison.Ordinal)
             && header.IndexOf("<div class=\"bp-bh-id\">", StringComparison.Ordinal) < header.IndexOf("<div class=\"bp-bh-facts-row\">", StringComparison.Ordinal));
-        Assert.Contains("<span class=\"bp-id-value\" title=\"@Model.Identifier\">@BatteryPages.ShortId(Model.Identifier)</span>@await Html.PartialAsync(\"_CopyIdButton\", Model.Identifier)", header, StringComparison.Ordinal);
+        Assert.Contains("<dd>@await Html.PartialAsync(\"_IdValue\", Model.Identifier)</dd>", header, StringComparison.Ordinal); // short, linked, copied whole (6d)
         Assert.Contains("<details class=\"bp-bh-details\" open data-fold-on-phone>", header, StringComparison.Ordinal);
         Assert.Contains("<div class=\"bp-bh-actions\" data-action-bar>", bar, StringComparison.Ordinal);
         Assert.Contains("<nav class=\"bp-bh-open\"", bar, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
new file mode 100644
index 0000000..4627616
--- /dev/null
+++ b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
@@ -0,0 +1,276 @@
+using System.Text.Json.Nodes;
+using System.Text.RegularExpressions;
+using BatteryPassWeb.Configuration;
+using BatteryPassWeb.Controllers;
+using BatteryPassWeb.Dpp.Catalog;
+using BatteryPassWeb.Dpp.Documents;
+using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Pages;
+using BatteryPassWeb.Dpp.Publication;
+using BatteryPassWeb.Dpp.Store;
+using BatteryPassWeb.Dpp.Values;
+using BatteryPassWeb.Tests.Demo;
+using Microsoft.AspNetCore.Mvc;
+using Microsoft.Extensions.DependencyInjection;
+using Microsoft.Extensions.Options;
+
+namespace BatteryPassWeb.Tests.Dpp;
+
+/// <summary>
+/// Step 6d, the battery ID the product owner chose (O-1 and 6d, 5 Nov 2026): an ISO/IEC 15459 item ID under issuing
+/// agency UN (DUNS 778474481) and the battery's serial number with only its letters and digits, written as a web address
+/// (ISO/IEC 18975, data identifier 25S) under https://www.sib-scania-eubp.eu; made when the battery is created, a
+/// successor adding R1, R2, …; a demo battery's marked TEST. A version's passport ID is /dpp/&lt;item&gt;/v&lt;n&gt;.
+/// </summary>
+[Collection(MongoCollection.Name)]
+public sealed class DunsIdentifierTests(MongoFixture mongo)
+{
+    private const string Base = "https://www.sib-scania-eubp.eu";
+
+    private static BatteryPassOptions Settings() => DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl });
+
+    private static DunsIdentifierScheme Scheme(DppStore store) => new(Options.Create(Settings()), store);
+
+    private static BatteryPack Pack(string serial, bool demo = false, string? successorOf = null, string? identifier = null)
+    {
+        var pack = new BatteryPack { Id = $"pack-{Guid.NewGuid():N}", BatchId = "batch-1", OperatorId = "operator-1", IsDemo = demo, SuccessorOf = successorOf };
+        pack.Values["batterySerialNumber"] = new AttributeValue { Value = JsonValue.Create(serial), GeneratedAtUtc = DateTime.UtcNow, Source = "form" };
+        if (identifier is not null)
+        {
+            pack.Values["uniqueProductIdentifier"] = new AttributeValue { Value = JsonValue.Create(identifier), GeneratedAtUtc = DateTime.UtcNow, Source = "system" };
+        }
+
+        return pack;
+    }
+
+    [Fact]
+    public async Task ABatteryId_ShouldBeTheAgencyTheDunsAndTheSerialsLettersAndDigits_AsAWebAddress()
+    {
+        var scheme = Scheme(new DppStore(mongo.NewContext()));
+
+        var made = await scheme.NewPackIdentifierAsync(Pack("VPC-12221"));
+
+        Assert.Equal($"{Base}/25S/UN778474481VPC12221", made.Identifier);
+        Assert.Null(made.Problem);
+        Assert.False(scheme.IsTestIdentifier(made.Identifier!));
+        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(made.Identifier)));
+        Assert.Equal("UN778474481VPC12221", (await scheme.NewPackIdentifierAsync(Pack(" vpc 12221/ä "))).Identifier![^19..]);
+    }
+
+    [Fact]
+    public async Task ADemoBatteryId_ShouldBeMarkedTest()
+    {
+        var scheme = Scheme(new DppStore(mongo.NewContext()));
+
+        var demo = (await scheme.NewPackIdentifierAsync(Pack("DEMO-C7M-0001", demo: true))).Identifier!;
+
+        Assert.Equal($"{Base}/25S/UN778474481TESTDEMOC7M0001", demo);
+        Assert.True(scheme.IsTestIdentifier(demo));
+        Assert.True(scheme.IsTestIdentifier($"{Base}/dpp/UN778474481TESTDEMOC7M0001R1/v2"));
+        Assert.Matches("^[A-Z0-9]{1,50}$", IdentifierForms.Short(demo)); // ISO/IEC 15459: A–Z, 0–9, at most 50
+    }
+
+    [Theory]
+    [InlineData("---", false, DunsIdentifierScheme.NoLettersOrDigits)]
+    [InlineData("äöü", false, DunsIdentifierScheme.NoLettersOrDigits)]
+    [InlineData("TEST-0001", false, DunsIdentifierScheme.StartsWithTest)]
+    [InlineData("A23456789012345678901234567890123456X", false, "at most 36 letters and digits")] // 37: no room for R99 within 50
+    [InlineData("A2345678901234567890123456789012X", true, "at most 32 letters and digits")] // a demo's TEST takes 4
+    public async Task ASerialThatCantMakeAValidId_ShouldBeRefused(string serial, bool demo, string problem)
+    {
+        var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(serial, demo));
+
+        Assert.Null(made.Identifier);
+        Assert.Contains(problem, made.Problem, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public async Task TheLongestSerial_ShouldStillLeaveRoomForASuccessorsR99()
+    {
+        var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(new string('A', 36)));
+
+        Assert.Equal(47, IdentifierForms.Short(made.Identifier!).Length);
+        Assert.True(IdentifierForms.IsItem(IdentifierForms.Short(made.Identifier!) + "R99"));
+        Assert.False(IdentifierForms.IsItem("UN778474481" + new string('A', 40)));
+    }
+
+    [Fact]
+    public async Task AnIdAnotherBatteryHas_ShouldBeRefused_NamingThatBattery()
+    {
+        var store = new DppStore(mongo.NewContext());
+        await store.InsertAsync(Pack("VPC-12221", identifier: $"{Base}/25S/UN778474481VPC12221"));
+
+        var made = await Scheme(store).NewPackIdentifierAsync(Pack("vpc 12221"));
+
+        Assert.Null(made.Identifier);
+        Assert.Equal(DunsIdentifierScheme.Taken("UN778474481VPC12221", "VPC-12221"), made.Problem);
+    }
+
+    [Fact]
+    public async Task ASuccessor_ShouldGetTheNextFreeRNumber()
+    {
+        var store = new DppStore(mongo.NewContext());
+        var first = Pack("VPC-77001", identifier: $"{Base}/25S/UN778474481VPC77001");
+        await store.InsertAsync(first);
+        var scheme = Scheme(store);
+
+        var r1 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77001", successorOf: first.Id))).Identifier!;
+        var second = Pack("VPC-77001", successorOf: first.Id, identifier: r1);
+        await store.InsertAsync(second);
+        var r2 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77001", successorOf: second.Id))).Identifier!;
+
+        Assert.Equal($"{Base}/25S/UN778474481VPC77001R1", r1);
+        Assert.Equal($"{Base}/25S/UN778474481VPC77001R2", r2);
+    }
+
+    [Fact]
+    public void APassportId_ShouldNameTheBatteryAndTheVersion()
+    {
+        var scheme = Scheme(new DppStore(mongo.NewContext()));
+
+        var passport = scheme.NewPassportIdentifier($"{Base}/25S/UN778474481VPC12221", 2);
+
+        Assert.Equal($"{Base}/dpp/UN778474481VPC12221/v2", passport);
+        Assert.False(scheme.IsTestIdentifier(passport));
+        Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(passport)));
+    }
+
+    [Theory]
+    [InlineData("UN", "778474481", Base, true)]
+    [InlineData("", "778474481", Base, false)]
+    [InlineData("UN", "", Base, false)]
+    [InlineData("UN", "778474481", "", false)]
+    [InlineData("UN", "77847448", Base, false)]
+    [InlineData("UN", "778474481", "http://www.sib-scania-eubp.eu", false)]
+    public void TheScheme_ShouldBeDunsOnlyWithAllThreeSettings(string agency, string company, string baseUrl, bool duns) =>
+        Assert.Equal(duns, IdentifierForms.IsConfigured(new BatteryPassOptions { IdAgency = agency, IdCompany = company, IdBaseUrl = baseUrl }));
+
+    [Fact]
+    public void ThePagesAddresses_ShouldUseTheShortItem()
+    {
+        Assert.Equal("/dpp/UN778474481VPC12221", PassportPages.SummaryHref($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal("/dpp/UN778474481VPC12221/detail", PassportPages.DetailHref($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal("/dpp/UN778474481VPC12221R1/v2", PassportPages.SummaryHref($"{Base}/dpp/UN778474481VPC12221R1/v2"));
+        Assert.Equal("/dpp/UN778474481VPC12221/v2/detail", PassportPages.DetailHref($"{Base}/dpp/UN778474481VPC12221/v2"));
+        Assert.Equal("/qr/UN778474481VPC12221/svg", PassportPages.QrHref($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal("UN778474481VPC12221", BatteryPages.ShortId($"{Base}/25S/UN778474481VPC12221"));
+        // Test IDs keep their old addresses.
+        Assert.Equal("/dpp/urn:x-battery-pass:TEST-0f8c", PassportPages.SummaryHref("urn:x-battery-pass:TEST-0f8c"));
+    }
+
+    [Fact]
+    public void TheQrCode_ShouldCarryTheBatteryIdsOwnAddressOnTheAppsSite()
+    {
+        // On the live site APP_BASE_URL is the ID's domain, so the QR carries #6 itself.
+        var live = new DppLinks(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = $"{Base}/" })));
+        var pc = new DppLinks(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = "http://localhost:5186" })));
+
+        Assert.Equal($"{Base}/25S/UN778474481VPC12221", live.PassportUrl($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal("http://localhost:5186/25S/UN778474481VPC12221", pc.PassportUrl($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal("http://localhost:5186/dpp/urn:x-battery-pass:TEST-0f8c", pc.PassportUrl("urn:x-battery-pass:TEST-0f8c"));
+    }
+
+    [Fact]
+    public void TheItemsAddress_ShouldOpenItsPublicPage()
+    {
+        var item = new ItemIdController();
+
+        Assert.Equal("/dpp/UN778474481VPC12221", Assert.IsType<RedirectResult>(item.Item("UN778474481VPC12221")).Url);
+        Assert.IsType<NotFoundResult>(item.Item("not-an-id"));
+        Assert.Contains("\"25S\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs")), StringComparison.Ordinal); // a scanned address is searched by its item
+    }
+
+    [Fact]
+    public async Task TheDemoData_ShouldGetTestIds_AndSuccessorsRNumbers()
+    {
+        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var pages = rig.Services.GetRequiredService<PassportPages>();
+        Assert.IsType<DunsIdentifierScheme>(rig.Services.GetRequiredService<IIdentifierScheme>());
+
+        var packs = await rig.All(DppStore.Packs);
+        Assert.All(packs, pack =>
+        {
+            var values = pack["Values"].AsBsonDocument;
+            var serial = IdentifierForms.Clean(values["batterySerialNumber"]["Value"].AsString);
+            Assert.Matches($"^{Regex.Escape(Base)}/25S/UN778474481TEST{serial}(R[0-9]+)?$", values["uniqueProductIdentifier"]["Value"].AsString);
+        });
+        var identifiers = packs.Select(pack => pack["Values"]["uniqueProductIdentifier"]["Value"].AsString).ToArray();
+        Assert.Contains(identifiers, id => id.EndsWith("TESTDEMOC13M0001R1", StringComparison.Ordinal)); // DEMO-C13M-0001's second passport
+        Assert.Contains(identifiers, id => id.EndsWith("TESTDEMOC13M0001R2", StringComparison.Ordinal)); // and its third
+
+        // A battery is found by its short item, its page by its version's address.
+        var c7 = (await pages.FindAsync("DEMO-C7M-0001"))!;
+        Assert.Equal(c7.PackId, (await pages.FindAsync("UN778474481TESTDEMOC7M0001"))!.PackId);
+        Assert.Equal($"{Base}/dpp/UN778474481TESTDEMOC7M0001/v{c7.Number}", c7.DigitalProductPassportId);
+    }
+
+    [Fact]
+    public async Task ABatteryWhoseIdIsTaken_ShouldNotBeCreated_NamingTheOtherBattery()
+    {
+        using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var c7 = (await rig.Store.FindPacksBySerialNumberAsync("DEMO-C7M-0001")).Single();
+        var team = ChangeSender.Team(DemoRig.Who);
+        var before = await rig.Count(DppStore.Packs);
+
+        var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(team, new NewPack(
+            c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "demo c7m 0001" }, IsDemo: true));
+
+        Assert.Null(result.Pack);
+        Assert.Equal($"{PackCreationService.CantGetId}: {DunsIdentifierScheme.Taken("UN778474481TESTDEMOC7M0001", "DEMO-C7M-0001")}", result.Problem);
+        Assert.Equal(before, await rig.Count(DppStore.Packs));
+    }
+
+    [Theory]
+    [InlineData(7, "iso6523-actorid-upis::0060:778474481", false)]
+    [InlineData(12, " iso6523-actorid-upis::0060:123456789 ", false)]
+    [InlineData(7, "0060:778474481", true)]
+    [InlineData(12, "urn:x-demo:manufacturer:plant-north", true)]
+    [InlineData(12, "", false)]
+    [InlineData(11, "VPC-12221", false)]
+    public void TheOperatorsAndManufacturersIds_ShouldOnlyGetANote_WhenNotInTheIso6523Form(int number, string text, bool note) =>
+        Assert.Equal(note, IdentifierForms.ActorIdNote(number, text) is not null); // never a refusal: the editor saves it as it is
+
+    [Fact]
+    public void TheDemoOperatorsAndSites_ShouldBeInTheIso6523Form_TheScaniaOnesWithTheRealDuns()
+    {
+        var catalogue = JsonNode.Parse(File.ReadAllText(DppTestFiles.RepoFile("web", "Dpp", "Demo", "demo-catalogue.json")))!;
+        var operators = catalogue["operators"]!.AsArray().ToDictionary(item => item!["key"]!.GetValue<string>(), item => item!["values"]!["economicOperatorId"]!.GetValue<string>());
+        var sites = catalogue["sites"]!.AsArray().Select(item => item!["values"]!["uniqueManufacturerIdentifier"]!.GetValue<string>()).ToArray();
+
+        Assert.All(operators.Values, id => Assert.Null(IdentifierForms.ActorIdNote(7, id))); // the note shows only where it should
+        Assert.All(sites, id => Assert.Equal("iso6523-actorid-upis::0060:778474481", id)); // the demo plants are Scania's
+        Assert.Equal("iso6523-actorid-upis::0060:778474481", operators["batteries"]); // Scania Industrial Batteries
+        Assert.DoesNotContain("778474481", operators["fleet"], StringComparison.Ordinal); // another company: a made-up number
+    }
+
+    [Theory]
+    [InlineData("Passport", "_PassportHero.cshtml")]
+    [InlineData("Dpp", "Accidents.cshtml")]
+    [InlineData("Shared", "_TestTag.cshtml")]
+    public void TheTestIdNotes_ShouldNoLongerWaitForAScheme(string folder, string view)
+    {
+        var text = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, view));
+
+        Assert.DoesNotContain("scheme is chosen", text, StringComparison.Ordinal); // chosen on 5 Nov 2026
+        Assert.DoesNotContain("start with TEST-", text, StringComparison.Ordinal); // a demo ID is UN778474481TEST…
+        Assert.Contains("marked", text, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void AnId_ShouldBeShownShort_AndLinkedToItsFullAddress()
+    {
+        Assert.Equal("UN778474481VPC12221", IdentifierForms.Display($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal("UN778474481VPC12221R1/v2", IdentifierForms.Display($"{Base}/dpp/UN778474481VPC12221R1/v2"));
+        Assert.Equal("urn:x-battery-pass:TEST-0f8c", IdentifierForms.Display("urn:x-battery-pass:TEST-0f8c"));
+        var partial = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_IdValue.cshtml"));
+        Assert.Contains("href=\"@Links.PassportUrl(Model)\"", partial, StringComparison.Ordinal);
+        Assert.Contains("d-none d-print-inline\">@Model<", partial, StringComparison.Ordinal); // on paper the full address
+    }
+
+    [Fact]
+    public void TheVersionsAddress_ShouldShowThatVersion() =>
+        Assert.Contains("return page is null ? NotFoundPage() : View(nameof(Summary), page);", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs")), StringComparison.Ordinal);
+}
diff --git a/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs b/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
index fa9a9e5..da8cf4d 100644
--- a/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs
@@ -46,7 +46,7 @@ public sealed class PassportControllerTests(MongoFixture mongo)
         var view = Assert.IsType<ViewResult>(await Pages(rig, null).Summary(Uri.EscapeDataString(PassportPagesTests.Identifier(rig)), CancellationToken.None));
 
         var page = Assert.IsType<PassportPage>(view.Model);
-        Assert.Null(view.ViewName);
+        Assert.Equal("Summary", view.ViewName); // named, as the version addresses reuse it (step 6d)
         Assert.Empty(page.Audiences);
         Assert.False(page.ReaderSeesMore);
         Assert.All(page.Sections.SelectMany(section => section.Values), value => Assert.Equal(Audience.Public, value.Attribute.Audience));
diff --git a/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs b/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
index b5394b7..34d6e77 100644
--- a/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
@@ -59,12 +59,12 @@ public sealed class ReviewFixesTests
     {
         var page = DppTestFiles.BatteryPageSource();
 
-        Assert.Contains("href=\"/dpp/@batteryKey\"", page);
-        Assert.Contains("href=\"/dpp/@batteryKey/detail\"", page);
-        Assert.Contains("href=\"/qr/@batteryKey/svg\"", page);
-        Assert.Contains("href=\"/qr/@batteryKey/download\"", page);
+        Assert.Contains("href=\"@PassportPages.SummaryHref(Model.Identifier)\"", page);
+        Assert.Contains("href=\"@PassportPages.DetailHref(Model.Identifier)\"", page);
+        Assert.Contains("href=\"@PassportPages.QrHref(Model.Identifier)\"", page);
+        Assert.Contains("href=\"@PassportPages.QrDownloadHref(Model.Identifier)\"", page);
         Assert.Contains("href=\"/admin/audit?q=@Uri.EscapeDataString(Model.Pack.Id)\"", page);
-        Assert.Contains("/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail", page); // each version opens
+        Assert.Contains("href=\"@PassportPages.DetailHref(row.Version.DigitalProductPassportId)\"", page); // each version opens its own address (6d)
         Assert.Contains("@row.Version.Proof.Hash", page); // and shows its signature details
     }
 
diff --git a/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs b/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs
index f7877b7..d2de6b8 100644
--- a/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/RowActionsTests.cs
@@ -18,7 +18,7 @@ public sealed class RowActionsTests(MongoFixture mongo)
     {
         var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
         var row = (await BatteryTesting.Pages(rig).ListAsync(null)).Rows.Single(candidate => candidate.PackId == rig.PackId);
-        return (row, Uri.EscapeDataString(row.Identifier));
+        return (row, BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PagePath(row.Identifier)); // the passport pages' own addresses (6d)
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs b/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
index 78cd2e0..9967da6 100644
--- a/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
@@ -1,6 +1,7 @@
 using System.Text.Json.Nodes;
 using BatteryPassWeb.Dpp.Catalog;
 using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Store;
 using BatteryPassWeb.Dpp.Values;
 
 namespace BatteryPassWeb.Tests.Dpp;
@@ -9,10 +10,12 @@ public sealed class TestIdentifierSchemeTests
 {
     private readonly TestIdentifierScheme _scheme = new();
 
+    private static BatteryPack Pack() => new() { Id = "pack-1", BatchId = "batch-1", OperatorId = "operator-1" };
+
     [Fact]
-    public void NewPackIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
+    public async Task NewPackIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
     {
-        var identifier = _scheme.NewPackIdentifier();
+        var identifier = (await _scheme.NewPackIdentifierAsync(Pack())).Identifier!;
 
         Assert.StartsWith("urn:x-battery-pass:TEST-", identifier, StringComparison.Ordinal);
         Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(identifier)));
@@ -21,18 +24,18 @@ public sealed class TestIdentifierSchemeTests
     [Fact]
     public void NewPassportIdentifier_ShouldBeAMarkedTestUrnThatPassesTheValueChecks()
     {
-        var identifier = _scheme.NewPassportIdentifier();
+        var identifier = _scheme.NewPassportIdentifier("urn:x-battery-pass:TEST-0f8c", 1);
 
         Assert.StartsWith("urn:x-battery-pass:TEST-DPP-", identifier, StringComparison.Ordinal);
         Assert.True(_scheme.IsTestIdentifier(identifier));
-        Assert.NotEqual(identifier, _scheme.NewPassportIdentifier());
+        Assert.NotEqual(identifier, _scheme.NewPassportIdentifier("urn:x-battery-pass:TEST-0f8c", 1));
         Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(identifier)));
     }
 
     [Fact]
-    public void NewPackIdentifier_ShouldNeverRepeat()
+    public async Task NewPackIdentifier_ShouldNeverRepeat()
     {
-        var identifiers = Enumerable.Range(0, 200).Select(_ => _scheme.NewPackIdentifier()).ToList();
+        var identifiers = (await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => _scheme.NewPackIdentifierAsync(Pack())))).Select(made => made.Identifier).ToList();
 
         Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
     }
@@ -41,6 +44,9 @@ public sealed class TestIdentifierSchemeTests
     [InlineData("urn:x-battery-pass:TEST-0f8c", true)]
     [InlineData("urn:test:battery:VPC8C05-6788", false)]
     [InlineData("https://id.example.com/01/123", false)]
+    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481TESTDEMOC7M0001", true)]
+    [InlineData("https://www.sib-scania-eubp.eu/dpp/UN778474481TESTDEMOC7M0001R1/v2", true)]
+    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221", false)]
     public void IsTestIdentifier_ShouldRecogniseOnlyMarkedIdentifiers(string identifier, bool expected)
     {
         Assert.Equal(expected, _scheme.IsTestIdentifier(identifier));
diff --git a/BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs b/BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs
index e1e1676..29a54e4 100644
--- a/BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs
+++ b/BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs
@@ -30,7 +30,7 @@ public sealed class PassportLinksSameTabTests
         var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
         var accidents = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "_AccidentsCard.cshtml"));
 
-        Assert.Contains("href=\"/qr/@batteryKey/svg\" target=\"_blank\" rel=\"noreferrer\"", battery, StringComparison.Ordinal);
+        Assert.Contains("href=\"@PassportPages.QrHref(Model.Identifier)\" target=\"_blank\" rel=\"noreferrer\"", battery, StringComparison.Ordinal);
         Assert.Contains("<a href=\"@ReaderPages.DocumentHref(report.Id)\" target=\"_blank\" rel=\"noreferrer\">", accidents, StringComparison.Ordinal);
     }
 }
```

- [ ] **Step 2: See them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`
Expected: the build fails with `CS0103: The name 'DunsIdentifierScheme' does not exist in the current context` (and `CS0246` for the same name).

- [ ] **Step 3: Write the code**

Save as `../6d1-app.patch` and run `git apply ../6d1-app.patch`:

```diff
diff --git a/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md b/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
index 42e99ae..2a8ce36 100644
--- a/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
+++ b/docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md
@@ -349,7 +349,7 @@ Each attribute holds one document per owner (model, batch or pack).
 ## 10. Identifiers
 
 - #6 (pack), #7 (operator) and #12 (manufacturer) are EN 18219 identifiers; #5 identifies a passport version. The schema stores all four as URIs.
-- The scheme (EN 18219 with an ISO/IEC 15459 issuing agency) is chosen later (decision 5, open item O-1). Until then the identifier service issues test IDs marked `TEST-`, and every page of a passport with a test ID shows a banner.
+- The scheme (decision 5, O-1, chosen 5 Nov 2026, step 6d): #6 is an ISO/IEC 15459 item ID under issuing agency `UN` (DUNS), the company's DUNS `778474481` and the serial number's letters and digits, written as a web address with data identifier `25S` (ISO/IEC 18975): `https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221`. A battery gets it when it is created; a successor adds `R1`, `R2`, …; a demo battery's is marked `TEST` after the DUNS. #5 is `…/dpp/<item>/v<n>`. #7 and #12 are written `iso6523-actorid-upis::0060:<DUNS>`, with only a note for another form. The settings `DPP_ID_AGENCY`, `DPP_ID_COMPANY` and `DPP_ID_BASE_URL` turn it on; without them the identifier service issues test IDs marked `TEST-`, and every page of a passport with a test ID shows a banner.
 - The QR code on the battery carries #6 and opens the public summary.
 - A successor passport gets a new #6 (J-2).
 
@@ -481,7 +481,7 @@ Choices this design makes where the file and the 21 decisions don't decide. Plea
 
 | Ref | Item | Proposal |
 |---|---|---|
-| O-1 | Identifier scheme (decision 5) | Choose the issuing agency and register before go-live. Test IDs until then. |
+| O-1 | Identifier scheme (decision 5) | Decided 5 Nov 2026: DUNS under issuing agency UN, #6 from the serial number (step 6d, section 10). |
 
 Decided on 28 Oct 2026 (decisions 17–21 in the decision record):
 
diff --git a/web/Configuration/BatteryPassOptions.cs b/web/Configuration/BatteryPassOptions.cs
index 19a3fc8..722f4a4 100644
--- a/web/Configuration/BatteryPassOptions.cs
+++ b/web/Configuration/BatteryPassOptions.cs
@@ -30,6 +30,15 @@ public sealed class BatteryPassOptions
     public string PowerAutomateResetWebhookSecret { get; set; } = string.Empty;
     public string PasswordResetAppName { get; set; } = "Battery Pass";
     public string AppBaseUrl { get; set; } = string.Empty;
+
+    /// <summary>The identifier scheme (O-1, step 6d): the ISO/IEC 15459 issuing agency code, e.g. UN (DUNS).</summary>
+    public string IdAgency { get; set; } = string.Empty;
+
+    /// <summary>The company's code at that agency: Scania Industrial Batteries AB's DUNS, 778474481.</summary>
+    public string IdCompany { get; set; } = string.Empty;
+
+    /// <summary>The domain the IDs are written under for good, e.g. https://www.sib-scania-eubp.eu; not where the app runs.</summary>
+    public string IdBaseUrl { get; set; } = string.Empty;
     public bool RequireHttpsRedirection { get; set; } = true;
     public bool EnableDemoAdminFallback { get; set; }
     public bool EnableRateLimiting { get; set; } = DefaultEnableRateLimiting;
diff --git a/web/Controllers/HomeController.cs b/web/Controllers/HomeController.cs
index 1da96ab..7fd9007 100644
--- a/web/Controllers/HomeController.cs
+++ b/web/Controllers/HomeController.cs
@@ -11,7 +11,7 @@ namespace BatteryPassWeb.Controllers;
 public class HomeController(PassportPages pages) : Controller
 {
     /// <summary>Address parts that aren't an ID: the passport pages' prefix and suffixes, and today's old ones.</summary>
-    private static readonly HashSet<string> RouteWords = new(StringComparer.OrdinalIgnoreCase) { "dpp", "detail", "summary", "latest" };
+    private static readonly HashSet<string> RouteWords = new(StringComparer.OrdinalIgnoreCase) { "dpp", "detail", "summary", "latest", "25S" };
 
     /// <summary>Where the home page finds the sample battery's ID: a published demo battery, or none (redesign R6).</summary>
     public const string SampleKey = "SampleBatteryId";
diff --git a/web/Controllers/ItemIdController.cs b/web/Controllers/ItemIdController.cs
new file mode 100644
index 0000000..3308de4
--- /dev/null
+++ b/web/Controllers/ItemIdController.cs
@@ -0,0 +1,18 @@
+using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Pages;
+using Microsoft.AspNetCore.Authorization;
+using Microsoft.AspNetCore.Mvc;
+
+namespace BatteryPassWeb.Controllers;
+
+/// <summary>
+/// The battery ID's own address (#6, step 6d): <c>/25S/UN778474481VPC12221</c>, the ISO/IEC 18975 web form of an
+/// ISO/IEC 15459 item ID, which the QR code carries. It opens the battery's public summary.
+/// </summary>
+[AllowAnonymous]
+public sealed class ItemIdController : Controller
+{
+    [HttpGet("25S/{item}")]
+    public IActionResult Item(string item) =>
+        IdentifierForms.IsItem(item) ? Redirect(PassportPages.SummaryHref(item)) : NotFound();
+}
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index 8e9a579..875ea3f 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -46,9 +46,24 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
     {
         var reader = await readers.ForAsync(User, cancellationToken);
         var page = await pages.SummaryAsync(reader, Uri.UnescapeDataString(id), cancellationToken);
-        return page is null ? NotFoundPage() : View(page);
+        return page is null ? NotFoundPage() : View(nameof(Summary), page);
     }
 
+    /// <summary>A version's own address (#5, step 6d): <c>/dpp/UN778474481VPC12221/v2</c>.</summary>
+    [HttpGet("{id}/v{version:int}")]
+    public Task<IActionResult> Version(string id, int version, [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options, CancellationToken cancellationToken) =>
+        Summary(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken);
+
+    [HttpGet("{id}/v{version:int}/detail")]
+    public Task<IActionResult> VersionDetail(
+        string id,
+        int version,
+        [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options,
+        CancellationToken cancellationToken,
+        [FromServices] BatteryApi? liveHistory = null,
+        [FromServices] ClusterBatteryPages? manage = null) =>
+        Detail(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken, liveHistory, manage);
+
     [HttpGet("{id}/detail")]
     public async Task<IActionResult> Detail(
         string id,
@@ -89,7 +104,7 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
         }
 
         Response.Headers[HeaderNames.CacheControl] = "private, no-store";
-        return View(page);
+        return View(nameof(Detail), page);
     }
 
     /// <summary>No published passport has this ID; drafts aren't shown to anyone here (J-6).</summary>
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index 735b460..290c19f 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -402,7 +402,7 @@ public sealed class DemoDataService(
     {
         var published = 0;
         var successors = 0;
-        var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form"), cancellationToken);
+        var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form", IsDemo: true), cancellationToken);
         if (created.Pack is null)
         {
             var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index c581429..ca85640 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -614,7 +614,7 @@
       "key": "plant-north",
       "name": "Demo plant North",
       "values": {
-        "uniqueManufacturerIdentifier": "urn:x-demo:manufacturer:plant-north",
+        "uniqueManufacturerIdentifier": "iso6523-actorid-upis::0060:778474481",
         "manufacturerInformation": {
           "name": "Scania Industrial Batteries (demo)",
           "registeredTradeNameOrRegisteredTrademark": "Scania Industrial Batteries",
@@ -629,7 +629,7 @@
       "key": "plant-south",
       "name": "Demo plant South",
       "values": {
-        "uniqueManufacturerIdentifier": "urn:x-demo:manufacturer:plant-south",
+        "uniqueManufacturerIdentifier": "iso6523-actorid-upis::0060:778474481",
         "manufacturerInformation": {
           "name": "Scania Industrial Batteries (demo)",
           "registeredTradeNameOrRegisteredTrademark": "Scania Industrial Batteries",
@@ -646,7 +646,7 @@
       "key": "batteries",
       "name": "Scania Industrial Batteries (demo)",
       "values": {
-        "economicOperatorId": "urn:x-demo:operator:industrial-batteries",
+        "economicOperatorId": "iso6523-actorid-upis::0060:778474481",
         "economicOperatorInformation": {
           "name": "Scania Industrial Batteries (demo)",
           "registeredTradeNameOrRegisteredTrademark": "Scania Industrial Batteries",
@@ -660,7 +660,7 @@
       "key": "fleet",
       "name": "Demo Fleet Services AB",
       "values": {
-        "economicOperatorId": "urn:x-demo:operator:fleet-services",
+        "economicOperatorId": "iso6523-actorid-upis::0060:999999999",
         "economicOperatorInformation": {
           "name": "Demo Fleet Services AB",
           "registeredTradeNameOrRegisteredTrademark": "Demo Fleet Services",
diff --git a/web/Dpp/Documents/DppLinks.cs b/web/Dpp/Documents/DppLinks.cs
index 5cfd447..ceeaa95 100644
--- a/web/Dpp/Documents/DppLinks.cs
+++ b/web/Dpp/Documents/DppLinks.cs
@@ -33,7 +33,10 @@ public sealed class DppLinks(IOptions<BatteryPassOptions> options)
     /// The public summary's full address, which a battery's QR code carries (design 10): the battery ID (#6) as written,
     /// so the code opens the battery's current passport.
     /// </summary>
-    public string PassportUrl(string identifier) => Base + BatteryPassWeb.Dpp.Pages.PassportPages.SummaryHref(identifier);
+    public string PassportUrl(string identifier) =>
+        Identifiers.IdentifierForms.Short(identifier) is { } item && Identifiers.IdentifierForms.IsItem(item)
+            ? $"{Base}/{Identifiers.IdentifierForms.DataIdentifier}/{item}"
+            : Base + BatteryPassWeb.Dpp.Pages.PassportPages.SummaryHref(identifier);
 
     /// <summary>#92's value: the pack's accident page, which lists its reports or says none were reported (J-14).</summary>
     public JsonObject AccidentPageLink(string packId) => new()
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
index cad16b7..33ad050 100644
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -30,7 +30,13 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<AttributeValueValidator>();
         services.AddSingleton<AttributeValueNormalizer>();
         services.AddSingleton<DppStore>();
-        services.AddSingleton<IIdentifierScheme, TestIdentifierScheme>();
+        services.AddSingleton<IIdentifierScheme>(provider =>
+        {
+            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions>>();
+            return IdentifierForms.IsConfigured(options.Value)
+                ? new DunsIdentifierScheme(options, provider.GetRequiredService<DppStore>())
+                : new TestIdentifierScheme();
+        });
         services.AddSingleton<SavePipeline>();
         services.AddSingleton<PackCreationService>();
         services.AddSingleton<StatusChangeService>();
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
index adcfeaa..65d1231 100644
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -14,7 +14,8 @@ public sealed record NewPack(
     string? ClusterId,
     IReadOnlyDictionary<string, JsonNode?> Values,
     string Source = "form",
-    string? PredecessorPackId = null);
+    string? PredecessorPackId = null,
+    bool IsDemo = false);
 
 /// <summary>The created pack, or the problem that stopped it; <see cref="Keys"/> says what happened to each value.</summary>
 public sealed record PackCreationResult(BatteryPack? Pack, IReadOnlyList<KeyResult> Keys, string? Problem);
@@ -33,6 +34,9 @@ public sealed class PackCreationService(
 {
     public const string AuditEventType = "dpp.pack-created";
 
+    /// <summary>The start of the refusal when the serial number can't make a battery ID (6d).</summary>
+    public const string CantGetId = "the battery can't get its battery ID";
+
     public async Task<PackCreationResult> CreateAsync(ChangeSender sender, NewPack request, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(sender);
@@ -90,8 +94,8 @@ public sealed class PackCreationService(
             OperatorId = request.OperatorId,
             ClusterId = request.ClusterId,
             SuccessorOf = request.PredecessorPackId,
+            IsDemo = request.IsDemo, // a demo battery's ID is marked TEST
         };
-        pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifiers.NewPackIdentifier()), now, "system", sender.Who);
         pack.Values["batteryStatus"] = Stored(new JsonObject { ["batteryStatusValues"] = "original" }, now, "system", sender.Who);
         pack.Values["informationOnAccidents"] = Stored(links.AccidentPageLink(pack.Id), now, "system", sender.Who);
         foreach (var (key, value) in evaluation.Accepted)
@@ -99,7 +103,17 @@ public sealed class PackCreationService(
             pack.Values[key] = Stored(value, now, request.Source, sender.Who);
         }
 
+        // #6 (step 6d): made from the serial number (a battery's new passport adds R1, R2, …), and refused with the reason
+        // when the serial number can't make a valid one or another battery has it.
+        var made = await identifiers.NewPackIdentifierAsync(pack, cancellationToken);
+        if (made.Identifier is not { } identifier)
+        {
+            return Failed($"{CantGetId}: {made.Problem}");
+        }
+
+        pack.Values["uniqueProductIdentifier"] = Stored(JsonValue.Create(identifier), now, "system", sender.Who);
         await store.InsertAsync(pack, cancellationToken);
+
         await audit.AppendApplicationAuditEventAsync(
             AuditEventType,
             sender.Who,
diff --git a/web/Dpp/Editing/SuccessionService.cs b/web/Dpp/Editing/SuccessionService.cs
index 72bcad9..a28dbed 100644
--- a/web/Dpp/Editing/SuccessionService.cs
+++ b/web/Dpp/Editing/SuccessionService.cs
@@ -79,7 +79,7 @@ public sealed class SuccessionService(
         var values = CarriedValues
             .Where(old.Values.ContainsKey)
             .ToDictionary(key => key, key => old.Values[key].Value?.DeepClone(), StringComparer.Ordinal);
-        var created = await creation.CreateAsync(sender, new NewPack(old.BatchId, operatorId, old.ClusterId, values, "form", PredecessorPackId: old.Id), cancellationToken);
+        var created = await creation.CreateAsync(sender, new NewPack(old.BatchId, operatorId, old.ClusterId, values, "form", PredecessorPackId: old.Id, IsDemo: old.IsDemo), cancellationToken);
         if (created.Pack is not { } pack)
         {
             return (null, created.Problem ?? "the new passport couldn't be made");
diff --git a/web/Dpp/Identifiers/DunsIdentifierScheme.cs b/web/Dpp/Identifiers/DunsIdentifierScheme.cs
new file mode 100644
index 0000000..602e765
--- /dev/null
+++ b/web/Dpp/Identifiers/DunsIdentifierScheme.cs
@@ -0,0 +1,199 @@
+using System.Text.RegularExpressions;
+using BatteryPassWeb.Configuration;
+using BatteryPassWeb.Dpp.Store;
+using Microsoft.Extensions.Options;
+
+namespace BatteryPassWeb.Dpp.Identifiers;
+
+/// <summary>
+/// The battery ID chosen by the product owner (O-1 and 6d, 5 Nov 2026): an ISO/IEC 15459 unique item ID under issuing
+/// agency code <c>UN</c> (Dun &amp; Bradstreet), the company's DUNS, and the battery's serial number (#11) with only its
+/// letters and digits, upper-cased, written as a web address per ISO/IEC 18975 with the MH10.8.2 data identifier
+/// <c>25S</c>: serial <c>VPC-12221</c> gives <c>https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221</c>. A battery
+/// gets it when it is created; a new passport of the same battery (a successor) adds <c>R1</c>, <c>R2</c>, …, the
+/// next free one. A demo battery's is marked <c>TEST</c> after the DUNS. A version's passport ID (#5) is
+/// <c>…/dpp/UN778474481VPC12221/v1</c>.
+/// </summary>
+public sealed class DunsIdentifierScheme(IOptions<BatteryPassOptions> options, DppStore store) : IIdentifierScheme
+{
+    /// <summary>ISO/IEC 15459: an item ID has at most 50 characters, agency code included.</summary>
+    public const int MaxItemLength = 50;
+
+    /// <summary>Room kept for a successor's <c>R99</c>, so a battery that gets a first ID can always get its next ones.</summary>
+    public const int SuccessorRoom = 3;
+
+    public const int MaxSuccessors = 99;
+
+    public const string NoLettersOrDigits = "the serial number has no letters or digits, so it can't make a battery ID";
+
+    public const string StartsWithTest = "a real battery's serial number can't start with TEST, which marks demo batteries' IDs";
+
+    public const string TooManySuccessors = "this battery already has 99 new passports, so it can't get another battery ID";
+
+    public static string TooLong(int letters) => $"the serial number is too long to make a battery ID: at most {letters} letters and digits count";
+
+    public static string Taken(string item, string serial) =>
+        $"the battery ID {item} is already used by battery {serial}. Serial numbers that differ only in dashes, spaces or capitals give the same ID";
+
+    private string Prefix => options.Value.IdAgency + options.Value.IdCompany;
+
+    public async Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(pack);
+        var made = ItemFor(SerialNumber(pack), pack.IsDemo);
+        if (made.Identifier is not { } address)
+        {
+            return made;
+        }
+
+        var item = IdentifierForms.Short(address);
+        if (pack.SuccessorOf is null)
+        {
+            return await store.FindPackByUniqueProductIdentifierAsync(address, cancellationToken) is { } other && other.Id != pack.Id
+                ? NewIdentifier.Refused(Taken(item, SerialNumber(other)))
+                : new NewIdentifier(address, null);
+        }
+
+        var used = await store.FindPackIdentifiersWithSuffixesAsync(address, cancellationToken);
+        for (var number = 1; number <= MaxSuccessors; number++)
+        {
+            var next = $"{address}R{number}";
+            if (!used.Contains(next))
+            {
+                return new NewIdentifier(next, null);
+            }
+        }
+
+        return NewIdentifier.Refused(TooManySuccessors);
+    }
+
+    public NewIdentifier ItemFor(string serialNumber, bool demo)
+    {
+        var serial = IdentifierForms.Clean(serialNumber);
+        if (serial.Length == 0)
+        {
+            return NewIdentifier.Refused(NoLettersOrDigits);
+        }
+
+        if (!demo && serial.StartsWith(IdentifierForms.TestMarker, StringComparison.Ordinal))
+        {
+            return NewIdentifier.Refused(StartsWithTest);
+        }
+
+        var head = demo ? Prefix + IdentifierForms.TestMarker : Prefix;
+        return head.Length + serial.Length > MaxItemLength - SuccessorRoom
+            ? NewIdentifier.Refused(TooLong(MaxItemLength - SuccessorRoom - head.Length))
+            : new NewIdentifier(IdentifierForms.ItemAddress(options.Value.IdBaseUrl, head + serial), null);
+    }
+
+    public string NewPassportIdentifier(string packIdentifier, int version) =>
+        IdentifierForms.PassportAddress(options.Value.IdBaseUrl, IdentifierForms.Short(packIdentifier), version);
+
+    public bool IsTestIdentifier(string identifier) => IdentifierForms.IsTest(identifier);
+
+    private static string SerialNumber(BatteryPack pack) =>
+        pack.Values.GetValueOrDefault("batterySerialNumber")?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
+}
+
+/// <summary>The identifiers' forms, shared by every scheme and by the pages' addresses.</summary>
+public static partial class IdentifierForms
+{
+    /// <summary>The MH10.8.2 data identifier for an issuing agency's unique item ID (agency + company + serial).</summary>
+    public const string DataIdentifier = "25S";
+
+    public const string TestMarker = "TEST";
+
+    /// <summary>True when the settings give a full scheme; otherwise the app issues test IDs.</summary>
+    public static bool IsConfigured(BatteryPassOptions options) =>
+        AgencyCode().IsMatch(options.IdAgency) && CompanyCode().IsMatch(options.IdCompany) && Uri.TryCreate(options.IdBaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
+
+    /// <summary><c>https://…/25S/UN778474481VPC12221</c>: the battery ID (#6) as a web address.</summary>
+    public static string ItemAddress(string baseUrl, string item) => $"{baseUrl.Trim().TrimEnd('/')}/{DataIdentifier}/{item}";
+
+    /// <summary><c>https://…/dpp/UN778474481VPC12221/v1</c>: a version's passport ID (#5).</summary>
+    public static string PassportAddress(string baseUrl, string item, int version) => $"{baseUrl.Trim().TrimEnd('/')}/dpp/{item}/v{version}";
+
+    /// <summary>The item ID inside a web-form battery ID ("UN778474481VPC12221"); anything else as it is.</summary>
+    public static string Short(string identifier)
+    {
+        var match = ItemInAddress().Match(identifier ?? string.Empty);
+        return match.Success ? match.Groups["item"].Value : identifier ?? string.Empty;
+    }
+
+    /// <summary>
+    /// What a page shows of an ID (the owner, 6d): the short item for a battery ID ("UN778474481VPC12221"), the item and
+    /// version for a passport ID ("UN778474481VPC12221/v2"); anything else as it is. The full address is the link.
+    /// </summary>
+    public static string Display(string id) => IsWebAddress(id) ? PagePath(id) : id;
+
+    /// <summary>True for a battery or passport ID in the web form (shown short, linked to its full address).</summary>
+    public static bool IsWebAddress(string? id) => id is not null && (ItemInAddress().IsMatch(id) || VersionInAddress().IsMatch(id));
+
+    /// <summary>
+    /// The path of a passport page for an ID: "UN…" for a web-form battery ID, "UN…/v2" for a web-form passport ID, else
+    /// the ID escaped as before.
+    /// </summary>
+    public static string PagePath(string id)
+    {
+        if (ItemInAddress().Match(id) is { Success: true } item)
+        {
+            return item.Groups["item"].Value;
+        }
+
+        if (VersionInAddress().Match(id) is { Success: true } version)
+        {
+            return $"{version.Groups["item"].Value}/v{version.Groups["version"].Value}";
+        }
+
+        return Uri.EscapeDataString(id).Replace("%3A", ":", StringComparison.Ordinal);
+    }
+
+    /// <summary>True for a short item ID a reader may type or scan ("UN778474481VPC12221").</summary>
+    public static bool IsItem(string text) => text is { Length: <= DunsIdentifierScheme.MaxItemLength } && ItemId().IsMatch(text);
+
+    /// <summary>A serial number's part of a battery ID: only its letters A–Z and digits 0–9, upper-cased ("VPC-12221" → "VPC12221").</summary>
+    public static string Clean(string? serialNumber) =>
+        new((serialNumber ?? string.Empty).ToUpperInvariant().Where(letter => letter is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
+
+    /// <summary>Test IDs: the old TEST- URNs, and items marked TEST after the company (demo batteries and their passports).</summary>
+    public static bool IsTest(string? identifier) =>
+        identifier?.StartsWith(TestIdentifierScheme.Prefix, StringComparison.Ordinal) == true
+        || TestItem().IsMatch(identifier ?? string.Empty);
+
+    /// <summary>The economic operator's (#7) and the manufacturer's (#12) ID in the owner's form (6d): ISO/IEC 6523 with DUNS.</summary>
+    public const string ActorIdPrefix = "iso6523-actorid-upis::0060:";
+
+    /// <summary>The hint under #7 and #12 on Sites and operators.</summary>
+    public const string ActorIdHint = "The DUNS number in the ISO/IEC 6523 form: iso6523-actorid-upis::0060: and its 9 digits.";
+
+    /// <summary>
+    /// A gentle note when #7 or #12 isn't in that form; null otherwise. Only a note: the value is saved as it is, since
+    /// another form may be right for another company (the owner).
+    /// </summary>
+    public static string? ActorIdNote(int number, string? text) =>
+        number is 7 or 12 && !string.IsNullOrWhiteSpace(text) && !ActorId().IsMatch(text.Trim())
+            ? $"Not in the usual form {ActorIdPrefix}<DUNS>, for example {ActorIdPrefix}778474481. It is kept as it is."
+            : null;
+
+    [GeneratedRegex("^iso6523-actorid-upis::0060:[0-9]{9}$")]
+    private static partial Regex ActorId();
+
+    [GeneratedRegex("^[A-Z]{2,3}$")]
+    private static partial Regex AgencyCode();
+
+    [GeneratedRegex("^[0-9]{9}$")]
+    private static partial Regex CompanyCode();
+
+    // ISO/IEC 15459: upper-case letters and digits; agency + company + serial (at most 50 characters, checked in IsItem).
+    [GeneratedRegex("^[A-Z]{2,3}[0-9]{9}[A-Z0-9]{1,39}$")]
+    private static partial Regex ItemId();
+
+    [GeneratedRegex("^https://[^/]+/25S/(?<item>[A-Z]{2,3}[0-9]{9}[A-Z0-9]{1,39})$")]
+    private static partial Regex ItemInAddress();
+
+    [GeneratedRegex("^https://[^/]+/dpp/(?<item>[A-Z]{2,3}[0-9]{9}[A-Z0-9]{1,39})/v(?<version>[0-9]+)$")]
+    private static partial Regex VersionInAddress();
+
+    [GeneratedRegex("[A-Z]{2,3}[0-9]{9}TEST[A-Z0-9]*(/v[0-9]+)?$")]
+    private static partial Regex TestItem();
+}
diff --git a/web/Dpp/Identifiers/TestIdentifierScheme.cs b/web/Dpp/Identifiers/TestIdentifierScheme.cs
index 8901fa3..5ff7869 100644
--- a/web/Dpp/Identifiers/TestIdentifierScheme.cs
+++ b/web/Dpp/Identifiers/TestIdentifierScheme.cs
@@ -1,29 +1,52 @@
+using BatteryPassWeb.Dpp.Store;
+
 namespace BatteryPassWeb.Dpp.Identifiers;
 
-/// <summary>Issues unique product identifiers (#6). The real EN 18219 scheme is chosen before go-live (decision 5, O-1).</summary>
+/// <summary>The battery ID (#6) a battery gets when it is created, or why it can't get one.</summary>
+public sealed record NewIdentifier(string? Identifier, string? Problem)
+{
+    public static NewIdentifier Refused(string problem) => new(null, problem);
+}
+
+/// <summary>
+/// Issues the battery ID (#6) and the passport IDs (#5). The scheme is chosen by settings (O-1, step 6d): an ISO/IEC 15459
+/// item ID under issuing agency UN (DUNS) made from the battery's serial number, as a web address, or, without those
+/// settings, test IDs. A battery gets its #6 when it is created (the owner, 5 Nov 2026).
+/// </summary>
 public interface IIdentifierScheme
 {
-    string NewPackIdentifier();
+    /// <summary>The battery ID (#6) <paramref name="pack"/> gets, or why it can't get one.</summary>
+    Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default);
 
-    /// <summary>A new passport ID (#5) for one passport version.</summary>
-    string NewPassportIdentifier();
+    /// <summary>
+    /// The battery ID a serial number makes, checked on its own (its letters and digits, its length, TEST). Whether another
+    /// battery has it is the caller's check.
+    /// </summary>
+    NewIdentifier ItemFor(string serialNumber, bool demo);
 
-    /// <summary>True for identifiers issued for testing; pages show a banner for these (design 10).</summary>
+    /// <summary>A new passport ID (#5) for version <paramref name="version"/> of the battery <paramref name="packIdentifier"/>.</summary>
+    string NewPassportIdentifier(string packIdentifier, int version);
+
+    /// <summary>True for identifiers issued for testing; pages show a "Test passport" tag for these (design 10).</summary>
     bool IsTestIdentifier(string identifier);
 }
 
 /// <summary>
 /// Test identifiers: a URN in a private namespace, marked TEST-, so they pass the absolute-URI check, can never be
-/// mistaken for a registered identifier, and can be recognised later (plan judgement call).
+/// mistaken for a registered identifier, and can be recognised later (plan judgement call). Used while the identifier
+/// settings aren't given.
 /// </summary>
 public sealed class TestIdentifierScheme : IIdentifierScheme
 {
     public const string Prefix = "urn:x-battery-pass:TEST-";
 
-    public string NewPackIdentifier() => $"{Prefix}{Guid.NewGuid():N}";
+    public Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default) =>
+        Task.FromResult(new NewIdentifier($"{Prefix}{Guid.NewGuid():N}", null));
+
+    public NewIdentifier ItemFor(string serialNumber, bool demo) =>
+        IdentifierForms.Clean(serialNumber).Length == 0 ? NewIdentifier.Refused(DunsIdentifierScheme.NoLettersOrDigits) : new NewIdentifier($"{Prefix}{Guid.NewGuid():N}", null);
 
-    public string NewPassportIdentifier() => $"{Prefix}DPP-{Guid.NewGuid():N}";
+    public string NewPassportIdentifier(string packIdentifier, int version) => $"{Prefix}DPP-{Guid.NewGuid():N}";
 
-    public bool IsTestIdentifier(string identifier) =>
-        identifier?.StartsWith(Prefix, StringComparison.Ordinal) == true;
+    public bool IsTestIdentifier(string identifier) => IdentifierForms.IsTest(identifier);
 }
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
index 7257f4c..62cb5d0 100644
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -229,7 +229,7 @@ public sealed class BatteryPages(
     public static string ShortId(string id)
     {
         ArgumentNullException.ThrowIfNull(id);
-        return id.Length <= 36 ? id : $"{id[..24]}…{id[^8..]}";
+        return Identifiers.IdentifierForms.IsWebAddress(id) ? Identifiers.IdentifierForms.Display(id) : id.Length <= 36 ? id : $"{id[..24]}…{id[^8..]}";
     }
 
     public static string BatteryHref(string packId) => $"/admin/batteries/{Uri.EscapeDataString(packId)}";
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index b0487c8..4a42605 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -158,15 +158,15 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
     private static readonly (int Number, string Label)[] RecycledShares = [(49, "Nickel"), (50, "Cobalt"), (51, "Lithium"), (52, "Lead")];
 
     /// <summary>The public summary's address. Colons stay as they are, so a battery ID reads as written.</summary>
-    public static string SummaryHref(string id) => $"/dpp/{Escape(id)}";
+    public static string SummaryHref(string id) => $"/dpp/{Identifiers.IdentifierForms.PagePath(id)}";
 
     /// <summary>The detail page's address.</summary>
-    public static string DetailHref(string id) => $"/dpp/{Escape(id)}/detail";
+    public static string DetailHref(string id) => $"/dpp/{Identifiers.IdentifierForms.PagePath(id)}/detail";
 
     /// <summary>The QR code's picture of a battery, and the same picture as a file to save.</summary>
-    public static string QrHref(string id) => $"/qr/{Escape(id)}/svg";
+    public static string QrHref(string id) => $"/qr/{Identifiers.IdentifierForms.PagePath(id)}/svg";
 
-    public static string QrDownloadHref(string id) => $"/qr/{Escape(id)}/download";
+    public static string QrDownloadHref(string id) => $"/qr/{Identifiers.IdentifierForms.PagePath(id)}/download";
 
     /// <summary>What a reader-facing source is called on the detail page.</summary>
     public static string SourceWord(string source) => source switch
diff --git a/web/Dpp/Pages/RowActions.cs b/web/Dpp/Pages/RowActions.cs
index adf97db..29446a0 100644
--- a/web/Dpp/Pages/RowActions.cs
+++ b/web/Dpp/Pages/RowActions.cs
@@ -12,13 +12,12 @@ public sealed record RowActions(string Serial, string? Manage, string? Public, s
     public static RowActions For(BatteryRow row, string? manage, bool team)
     {
         ArgumentNullException.ThrowIfNull(row);
-        var key = Uri.EscapeDataString(row.Identifier);
         var published = !row.IsDraft && row.Identifier.Length > 0;
         return new RowActions(
             row.SerialNumber,
             manage,
-            published ? $"/dpp/{key}" : null,
-            published ? $"/dpp/{key}/detail" : null,
+            published ? PassportPages.SummaryHref(row.Identifier) : null,
+            published ? PassportPages.DetailHref(row.Identifier) : null,
             team ? $"/admin/audit?q={Uri.EscapeDataString(row.PackId)}" : null);
     }
 }
diff --git a/web/Dpp/Publication/PublicationService.cs b/web/Dpp/Publication/PublicationService.cs
index 266ea38..8d97f38 100644
--- a/web/Dpp/Publication/PublicationService.cs
+++ b/web/Dpp/Publication/PublicationService.cs
@@ -46,10 +46,15 @@ public sealed class PublicationService(
             return null;
         }
 
-        var header = new PassportHeader(identifiers.NewPassportIdentifier(), DppStatuses.Active, clock.GetUtcNow().UtcDateTime);
+        var number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1;
+        var header = new PassportHeader(identifiers.NewPassportIdentifier(PackIdentifier(sources.Pack), number), DppStatuses.Active, clock.GetUtcNow().UtcDateTime);
         return checks.Check(PassportAssembler.Assemble(sources, header));
     }
 
+    /// <summary>The battery ID (#6) a version's passport ID names (6d); the pack's own ID for a pack without one.</summary>
+    private static string PackIdentifier(BatteryPack pack) =>
+        pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? pack.Id;
+
     public async Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(sender);
@@ -157,7 +162,8 @@ public sealed class PublicationService(
         }
 
         var now = clock.GetUtcNow().UtcDateTime;
-        var passportId = identifiers.NewPassportIdentifier();
+        var number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1;
+        var passportId = identifiers.NewPassportIdentifier(PackIdentifier(sources.Pack), number);
         var passport = PassportAssembler.Assemble(sources, new PassportHeader(passportId, DppStatuses.Active, now));
         var report = checks.Check(passport);
         if (!report.CanPublish)
@@ -176,7 +182,7 @@ public sealed class PublicationService(
         {
             Id = $"version-{Guid.NewGuid():N}",
             PackId = packId,
-            Number = await store.GetLatestVersionNumberAsync(packId, cancellationToken) + 1,
+            Number = number,
             DigitalProductPassportId = passportId,
             DppStatus = DppStatuses.Active,
             PublishedAtUtc = now,
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 13bbc59..f11e7a6 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -342,7 +342,10 @@ public sealed class DppStore
         return result.ModifiedCount;
     }
 
-    /// <summary>The pack with this unique product identifier (#6), if any.</summary>
+    /// <summary>
+    /// The pack with this unique product identifier (#6), if any: as stored, or by its short item ID
+    /// ("UN778474481VPC12221") for a battery ID written as a web address (step 6d).
+    /// </summary>
     public async Task<BatteryPack?> FindPackByUniqueProductIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
     {
         if (_database is null || string.IsNullOrWhiteSpace(identifier))
@@ -350,7 +353,24 @@ public sealed class DppStore
             return null;
         }
 
-        return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Eq(UniqueProductIdentifierField, identifier)).FirstOrDefaultAsync(cancellationToken);
+        var filter = Builders<BatteryPack>.Filter.Eq(UniqueProductIdentifierField, identifier);
+        if (Identifiers.IdentifierForms.IsItem(identifier))
+        {
+            filter |= Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression($"/{Identifiers.IdentifierForms.DataIdentifier}/{Regex.Escape(identifier)}$", string.Empty));
+        }
+
+        return await Collection<BatteryPack>().Find(filter).FirstOrDefaultAsync(cancellationToken);
+    }
+
+    /// <summary>
+    /// The battery IDs (#6) that are <paramref name="address"/> or <paramref name="address"/> with a successor's R-number
+    /// (<c>…R1</c>, <c>…R2</c>), for picking the next free one (step 6d).
+    /// </summary>
+    public async Task<IReadOnlySet<string>> FindPackIdentifiersWithSuffixesAsync(string address, CancellationToken cancellationToken = default)
+    {
+        var filter = Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression($"^{Regex.Escape(address)}(R[0-9]+)?$", string.Empty));
+        var packs = await Collection<BatteryPack>().Find(filter).ToListAsync(cancellationToken);
+        return packs.Select(pack => pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
     }
 
     /// <summary>Everything one pack's passport is built from; null if the pack or a record it links to is missing.</summary>
diff --git a/web/Program.cs b/web/Program.cs
index 0561efb..0e8f71a 100644
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -92,6 +92,9 @@ builder.Services.Configure<BatteryPassOptions>(options =>
     options.PasswordResetAppName = Environment.GetEnvironmentVariable("PASSWORD_RESET_APP_NAME")
         ?? builder.Configuration["BatteryPass:PasswordResetAppName"]
         ?? "Battery Pass";
+    options.IdAgency = (Environment.GetEnvironmentVariable("DPP_ID_AGENCY") ?? builder.Configuration["BatteryPass:IdAgency"] ?? string.Empty).Trim();
+    options.IdCompany = (Environment.GetEnvironmentVariable("DPP_ID_COMPANY") ?? builder.Configuration["BatteryPass:IdCompany"] ?? string.Empty).Trim();
+    options.IdBaseUrl = (Environment.GetEnvironmentVariable("DPP_ID_BASE_URL") ?? builder.Configuration["BatteryPass:IdBaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
     options.AppBaseUrl = Environment.GetEnvironmentVariable("APP_BASE_URL")
         ?? Environment.GetEnvironmentVariable("APP_URL")
         ?? builder.Configuration["BatteryPass:AppBaseUrl"]
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
index 2d8da89..8f19646 100644
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -34,7 +34,6 @@
     var card = ViewData[BatteriesController.CardKey] as string;
     var testIds = Identifiers.IsTestIdentifier(Model.Identifier) || Model.Versions.Any(row => Identifiers.IsTestIdentifier(row.Version.DigitalProductPassportId));
     var filesChanged = publication.Files.Count(file => !file.Unchanged);
-    var batteryKey = Uri.EscapeDataString(Model.Identifier);
 
     // The side menu (redesign R4): the open section comes from ?section=, the card a form just sent, or a values save.
     var valuesMessage = TempData.Peek(ProductDataController.EditorStatusKey) is not null || TempData.Peek(ProductDataController.EditorErrorKey) is not null
@@ -110,7 +109,7 @@
                 <dl class="bp-bh-facts">
                     <div class="bp-bh-fact-id">
                         <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
-                        <dd><span class="bp-id-copy-row"><span class="bp-id-value" title="@Model.Identifier">@BatteryPages.ShortId(Model.Identifier)</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
+                        <dd>@await Html.PartialAsync("_IdValue", Model.Identifier)</dd>
                     </div>
                 </dl>
                 <details class="bp-bh-details" open data-fold-on-phone>
@@ -196,11 +195,11 @@
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
                 @if (publication.IsPublished)
                 {
-                    <a class="bp-ilink" href="/dpp/@batteryKey" title="The public page, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
-                    <a class="bp-ilink" href="/dpp/@batteryKey/detail" title="Everything its readers see, by who they are"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
+                    <a class="bp-ilink" href="@PassportPages.SummaryHref(Model.Identifier)" title="The public page, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                    <a class="bp-ilink" href="@PassportPages.DetailHref(Model.Identifier)" title="Everything its readers see, by who they are"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
                 }
-                <a class="bp-ilink" href="/qr/@batteryKey/svg" target="_blank" rel="noreferrer" title="@(publication.IsPublished ? "Opens the public page" : "Opens the public page once the passport is published")"><svg class="bp-i" aria-hidden="true"><use href="#i-qr" /></svg>QR code</a>
-                <a class="bp-ilink" href="/qr/@batteryKey/download" title="Save the QR code as a picture"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Save QR</a>
+                <a class="bp-ilink" href="@PassportPages.QrHref(Model.Identifier)" target="_blank" rel="noreferrer" title="@(publication.IsPublished ? "Opens the public page" : "Opens the public page once the passport is published")"><svg class="bp-i" aria-hidden="true"><use href="#i-qr" /></svg>QR code</a>
+                <a class="bp-ilink" href="@PassportPages.QrDownloadHref(Model.Identifier)" title="Save the QR code as a picture"><svg class="bp-i" aria-hidden="true"><use href="#i-download" /></svg>Save QR</a>
                 <a class="bp-ilink" href="/admin/audit?q=@Uri.EscapeDataString(Model.Pack.Id)" title="Every change to this battery, in the audit log"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>History</a>
             </nav>
         </div>
diff --git a/web/Views/Batteries/_VersionsCard.cshtml b/web/Views/Batteries/_VersionsCard.cshtml
index 64c4e62..8186302 100644
--- a/web/Views/Batteries/_VersionsCard.cshtml
+++ b/web/Views/Batteries/_VersionsCard.cshtml
@@ -28,7 +28,7 @@
                     {
                         <tr>
                             <td data-label="Version">@row.Version.Number</td>
-                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="/dpp/@Uri.EscapeDataString(row.Version.DigitalProductPassportId)/detail" title="@row.Version.DigitalProductPassportId">@BatteryPages.ShortId(row.Version.DigitalProductPassportId)</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
+                            <td data-label="Passport ID"><span class="bp-id-copy-row"><a class="bp-id-value" href="@PassportPages.DetailHref(row.Version.DigitalProductPassportId)" title="@row.Version.DigitalProductPassportId">@BatteryPages.ShortId(row.Version.DigitalProductPassportId)</a>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
                             <td data-label="Status"><div class="bp-badges is-compact">@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(row.Version.DppStatus))</div></td>
                             <td data-label="Published">@ValueWords.Time(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
                             <td data-label="Signature">
diff --git a/web/Views/ClusterAdmin/Battery.cshtml b/web/Views/ClusterAdmin/Battery.cshtml
index 33ef4d1..863a45b 100644
--- a/web/Views/ClusterAdmin/Battery.cshtml
+++ b/web/Views/ClusterAdmin/Battery.cshtml
@@ -10,7 +10,6 @@
     var active = Model.Versions.Select(row => row.Version).FirstOrDefault(version => version.DppStatus == BatteryPassWeb.Dpp.Store.DppStatuses.Active);
     // A marked passport's newest version (redesign R6b): the page is read-only then.
     var shown = active ?? (Model.ReadOnly ? Model.Versions.Select(row => row.Version).FirstOrDefault() : null);
-    var batteryKey = Uri.EscapeDataString(Model.Identifier);
     var card = ViewData[BatteriesController.CardKey] as string;
     var sections = Model.Sections;
     var chosen = BatterySections.Chosen(sections, Context.Request.Query["section"], card, valuesMessage: false);
@@ -50,14 +49,14 @@
                 </div>
                 @if (Model.Status?.Predecessor is { } replaced)
                 {
-                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber. Its <a href="/dpp/@batteryKey/detail">detail page</a> links the earlier one.</p>
+                    <p class="bp-lineage"><svg class="bp-i" aria-hidden="true"><use href="#i-history" /></svg>New passport of @replaced.SerialNumber. Its <a href="@PassportPages.DetailHref(Model.Identifier)">detail page</a> links the earlier one.</p>
                 }
             </div>
             <div class="bp-bh-facts-row">
                 <dl class="bp-bh-facts">
                     <div class="bp-bh-fact-id">
                         <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
-                        <dd><span class="bp-id-copy-row"><span class="bp-id-value" title="@Model.Identifier">@BatteryPages.ShortId(Model.Identifier)</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
+                        <dd>@await Html.PartialAsync("_IdValue", Model.Identifier)</dd>
                     </div>
                 </dl>
                 <details class="bp-bh-details" open data-fold-on-phone>
@@ -112,8 +111,8 @@
                 <button type="button" class="bp-secondary-button bp-with-icon" data-open-dialog="handover-dialog"><svg class="bp-i" aria-hidden="true"><use href="#i-handover" /></svg>Hand over…</button>
             }
             <nav class="bp-bh-open" aria-label="This battery elsewhere">
-                <a class="bp-ilink" href="/dpp/@batteryKey" title="The public page, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
-                <a class="bp-ilink" href="/dpp/@batteryKey/detail" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
+                <a class="bp-ilink" href="@PassportPages.SummaryHref(Model.Identifier)" title="The public page, as anyone sees it"><svg class="bp-i" aria-hidden="true"><use href="#i-globe" /></svg>Public page</a>
+                <a class="bp-ilink" href="@PassportPages.DetailHref(Model.Identifier)" title="Everything your cluster sees"><svg class="bp-i" aria-hidden="true"><use href="#i-doc" /></svg>Detail page</a>
                 <a class="bp-ilink" href="@ReaderPages.AccidentsHref(Model.Pack.Id)" title="The battery's accident reports, as readers see them"><svg class="bp-i" aria-hidden="true"><use href="#i-accident" /></svg>Accident page</a>
             </nav>
         </div>
diff --git a/web/Views/Dpp/Accidents.cshtml b/web/Views/Dpp/Accidents.cshtml
index c548f60..650112e 100644
--- a/web/Views/Dpp/Accidents.cshtml
+++ b/web/Views/Dpp/Accidents.cshtml
@@ -16,11 +16,11 @@
         <span class="bp-summary-title">Accident information</span>
         <h1 class="h3 mt-2 mb-2" title="Battery serial number">@Model.SerialNumber</h1>
         <dl class="bp-field-grid mb-0">
-            <div><dt>Battery ID</dt><dd class="bp-id-value">@Model.Identifier</dd></div>
+            <div><dt>Battery ID</dt><dd>@await Html.PartialAsync("_IdValue", Model.Identifier)</dd></div>
         </dl>
         @if (Identifiers.IsTestIdentifier(Model.Identifier))
         {
-            <p class="text-secondary small mt-2 mb-0">Test identifiers: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen.</p>
+            <p class="text-secondary small mt-2 mb-0">Test identifiers: this battery's IDs are marked TEST, so it isn't a real passport.</p>
         }
     </section>
 
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index 80bd5a7..dd9cacb 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -1,3 +1,4 @@
+@using BatteryPassWeb.Dpp.Identifiers
 @using BatteryPassWeb.Dpp.Pages
 @model PassportPage
 @inject BatteryPassWeb.Dpp.Documents.DppLinks Links
@@ -76,11 +77,11 @@
 
             <section class="bp-summary-id-panel bp-report-identity-panel bp-report-identity-panel--identifiers">
                 <span class="bp-summary-title">Identifiers</span>
-                <p class="bp-passport-id"><span class="bp-id-label">Passport ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Version.DigitalProductPassportId</span>@await Html.PartialAsync("_CopyIdButton", Model.Version.DigitalProductPassportId)</span></p>
-                <p class="bp-passport-id"><span class="bp-id-label">Battery ID:</span><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></p>
+                <p class="bp-passport-id"><span class="bp-id-label">Passport ID:</span>@if (IdentifierForms.IsWebAddress(Model.Version.DigitalProductPassportId)) { @await Html.PartialAsync("_IdValue", Model.Version.DigitalProductPassportId) } else { <span class="bp-id-copy-row"><span class="bp-id-value">@Model.Version.DigitalProductPassportId</span>@await Html.PartialAsync("_CopyIdButton", Model.Version.DigitalProductPassportId)</span> }</p>
+                <p class="bp-passport-id"><span class="bp-id-label">Battery ID:</span>@if (IdentifierForms.IsWebAddress(Model.Identifier)) { @await Html.PartialAsync("_IdValue", Model.Identifier) } else { <span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span> }</p>
                 @if (Model.IsTest && detail)
                 {
-                    <p class="bp-test-note">Test identifiers: these IDs start with TEST-, so this isn't a real passport yet. Real ones come once the identifier scheme is chosen.</p>
+                    <p class="bp-test-note">Test identifiers: these IDs are marked TEST, so this isn't a real passport. Demo batteries have them.</p>
                 }
             </section>
 
diff --git a/web/Views/ProductData/_EditorField.cshtml b/web/Views/ProductData/_EditorField.cshtml
index 8edb94a..6906dcb 100644
--- a/web/Views/ProductData/_EditorField.cshtml
+++ b/web/Views/ProductData/_EditorField.cshtml
@@ -2,6 +2,7 @@
 @using BatteryPassWeb.Configuration
 @using BatteryPassWeb.Dpp.Catalog
 @using BatteryPassWeb.Dpp.Forms
+@using BatteryPassWeb.Dpp.Identifiers
 @using BatteryPassWeb.Dpp.Pages
 @using Microsoft.Extensions.Options
 @inject IOptions<BatteryPassOptions> Options
@@ -20,7 +21,7 @@
             : $"{label} ({unit})";
 
     var required = definition.Requirement != Requirement.Conditional;
-    var hint = form.Kind switch
+    var hint = definition.Number is 7 or 12 ? IdentifierForms.ActorIdHint : form.Kind switch
     {
         FormKind.Link => "A link to the document on your website.",
         FormKind.Rows => "One row per substance. Phrases go one per line.",
@@ -133,6 +134,10 @@
         {
             <small class="bp-field-help">@hint</small>
         }
+        @if (IdentifierForms.ActorIdNote(definition.Number, field.Texts.Values.FirstOrDefault()) is { } note)
+        {
+            <small class="bp-field-help text-warning-emphasis" data-actor-id-note>@note</small>
+        }
     </div>
 
     <div class="bp-attr-inputs">
diff --git a/web/Views/Shared/_IdValue.cshtml b/web/Views/Shared/_IdValue.cshtml
new file mode 100644
index 0000000..420660a
--- /dev/null
+++ b/web/Views/Shared/_IdValue.cshtml
@@ -0,0 +1,14 @@
+@using BatteryPassWeb.Dpp.Identifiers
+@using BatteryPassWeb.Dpp.Pages
+@inject BatteryPassWeb.Dpp.Documents.DppLinks Links
+@model string
+@* A battery or passport ID (step 6d, the owner): the short ID to read, its full web address as the link and in the copy
+   button, and the full address on paper. *@
+@if (IdentifierForms.IsWebAddress(Model) && Links.IsConfigured)
+{
+    <span class="bp-id-copy-row"><a class="bp-id-value d-print-none" href="@Links.PassportUrl(Model)" title="@Model">@IdentifierForms.Display(Model)</a><span class="bp-id-value d-none d-print-inline">@Model</span>@await Html.PartialAsync("_CopyIdButton", Model)</span>
+}
+else
+{
+    <span class="bp-id-copy-row"><span class="bp-id-value" title="@Model">@BatteryPages.ShortId(Model)</span>@await Html.PartialAsync("_CopyIdButton", Model)</span>
+}
diff --git a/web/Views/Shared/_PassportChain.cshtml b/web/Views/Shared/_PassportChain.cshtml
index aeb3b86..a22dd94 100644
--- a/web/Views/Shared/_PassportChain.cshtml
+++ b/web/Views/Shared/_PassportChain.cshtml
@@ -15,7 +15,7 @@
                 <li class="@(link.IsThis ? "is-this" : null)">
                     <span class="bp-passport-chain-number">@link.Number</span>
                     <span class="bp-passport-chain-main">
-                        <span class="bp-id-copy-row"><span class="bp-id-value" title="@link.Identifier">@BatteryPages.ShortId(link.Identifier)</span>@await Html.PartialAsync("_CopyIdButton", link.Identifier)</span>
+                        @await Html.PartialAsync("_IdValue", link.Identifier)
                         <span class="bp-badges is-compact">
                             @await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(link.Published ? link.Status : null))
                             @if (link.IsNewest)
diff --git a/web/Views/Shared/_TestTag.cshtml b/web/Views/Shared/_TestTag.cshtml
index 718f8d5..05b82c7 100644
--- a/web/Views/Shared/_TestTag.cshtml
+++ b/web/Views/Shared/_TestTag.cshtml
@@ -1,13 +1,13 @@
 @model string
 @* A test passport's or battery's tag (redesign R3 and R4; owner's point 7): small, amber, and explained on a tap or a
    hover, instead of a banner across the page. The explanation is written once per page, however many tags point at it. *@
-<button type="button" class="bp-test-tag" popovertarget="test-help" title="IDs that start with TEST- are test identifiers">@Model<svg class="bp-i" aria-hidden="true"><use href="#i-help" /></svg></button>
+<button type="button" class="bp-test-tag" popovertarget="test-help" title="IDs marked TEST are test identifiers">@Model<svg class="bp-i" aria-hidden="true"><use href="#i-help" /></svg></button>
 @if (!Context.Items.ContainsKey("test-help"))
 {
     Context.Items["test-help"] = true;
     <div id="test-help" class="bp-status-help bp-test-help" popover>
         <h2>A test passport</h2>
-        <p>This battery's IDs start with <strong>TEST-</strong>: it isn't a real passport yet. Real IDs come once the identifier scheme is chosen.</p>
+        <p>This battery's IDs are marked <strong>TEST</strong>: it isn't a real passport. Demo batteries have test IDs, and so does a copy of the app without the battery ID settings.</p>
         <div class="d-flex justify-content-end"><button type="button" class="bp-secondary-button" popovertarget="test-help" popovertargetaction="hide">Close</button></div>
     </div>
 }
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index d10a4f0..bb94b77 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -3113,10 +3113,12 @@ main .bp-field-marker.is-published {
   max-width: 100%;
 }
 
+/* Step 6d: a battery ID's telling part is its end (…TESTDEMOC13M0001R2), so it wraps on a phone instead of ending in "…". */
 .bp-bh-fact-id .bp-id-value {
-  overflow: hidden;
-  text-overflow: ellipsis;
-  white-space: nowrap;
+  min-width: 0;
+  overflow: visible;
+  white-space: normal;
+  overflow-wrap: anywhere;
 }
 
 .bp-bh-details > summary {
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index ee2ec5b..f41f5a1 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -553,3 +553,12 @@ th .bp-status-help-words {
     margin-bottom: 8px;
   }
 }
+
+/* Step 6d: a battery or passport ID is a web address whose telling part is its end (UN778474481VPC12221/v2), so on the
+   passport pages it wraps instead of being cut with "…" (site.css cuts it on wide screens). */
+.bp-report-hero-info .bp-report-identity-panel--identifiers .bp-id-value {
+  overflow: visible;
+  text-overflow: clip;
+  white-space: normal;
+  overflow-wrap: anywhere;
+}
```

- [ ] **Step 4: See the tests pass**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~DunsIdentifierTests|FullyQualifiedName~TestIdentifierSchemeTests|FullyQualifiedName~PassportControllerTests|FullyQualifiedName~BatteryPageLayoutTests|FullyQualifiedName~ReviewFixesTests|FullyQualifiedName~PassportLinksSameTabTests|FullyQualifiedName~RowActionsTests"`
Expected: `Passed!  - Failed:     0, Passed:   98`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs BatteryPassWeb.Tests/Demo/DemoTesting.cs BatteryPassWeb.Tests/Dpp/BatteryPageLayoutTests.cs BatteryPassWeb.Tests/Dpp/PassportControllerTests.cs BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs BatteryPassWeb.Tests/Dpp/RowActionsTests.cs BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs BatteryPassWeb.Tests/Look/PassportLinksSameTabTests.cs web/Controllers/ItemIdController.cs web/Dpp/Identifiers/DunsIdentifierScheme.cs web/Views/Shared/_IdValue.cshtml docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md web/Configuration/BatteryPassOptions.cs web/Controllers/HomeController.cs web/Controllers/PassportController.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/demo-catalogue.json web/Dpp/Documents/DppLinks.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Editing/SuccessionService.cs web/Dpp/Identifiers/TestIdentifierScheme.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Pages/RowActions.cs web/Dpp/Publication/PublicationService.cs web/Dpp/Store/DppStore.cs web/Program.cs web/Views/Batteries/Battery.cshtml web/Views/Batteries/_VersionsCard.cshtml web/Views/ClusterAdmin/Battery.cshtml web/Views/Dpp/Accidents.cshtml web/Views/Passport/_PassportHero.cshtml web/Views/ProductData/_EditorField.cshtml web/Views/Shared/_PassportChain.cshtml web/Views/Shared/_TestTag.cshtml web/wwwroot/css/bp-product-data.css web/wwwroot/css/bp-status.css
git commit -m "feat(dpp): the battery ID scheme: DUNS under UN from the serial number, version passport IDs, shown short" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2579, Skipped:     0, Total:  2579`. That is the 2542 from before plus 37 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat db7bd45 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner, on the v3 app started with the three settings and after "Reset demo data":

1. DEMO-C7M-0001's public page shows `UN778474481TESTDEMOC7M0001` and `…/v3`. The copy button copies the full address; a printout shows it whole.
2. DEMO-C13M-0001's newest passport is `…TESTDEMOC13M0001R2`.
3. A new battery made on the Batteries page with serial `VPC-12221` gets `UN778474481VPC12221` and its QR code at once. A second one typed `vpc 12221` is refused, naming the first.
4. Sites and operators: the Scania operator's #7 is `iso6523-actorid-upis::0060:778474481`, with the hint and no note.

Without the three settings the app keeps the test IDs, as before.

## Next

Plan 6d-2: the "Battery IDs" page (release, export, void), creating a battery by picking a released ID on the pages and in the API, the swap while a draft, "Not published yet", and the demo release.
