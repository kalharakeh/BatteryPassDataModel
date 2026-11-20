# DPP V3 Step 6d-5: The Battery ID Keeps the Serial as Written, After One Dash — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An owner decision before the cut-over (9 Nov 2026): the battery ID keeps the serial number exactly as written, with one dash after the company.
- **#6:** `UN` + `778474481` + `-` + the serial as released. Serial `VBC-92921` gives `UN778474481-VBC-92921`, as a link `https://www.sib-scania-eubp.eu/25S/UN778474481-VBC-92921`.
- **#5:** `…/dpp/UN778474481-VBC-92921/v1`.
- **Why a single dash:** `UN778474481-` is always the fixed first 12 characters, so it is unambiguous. The owner rejected `.` (looks like a domain or a file extension), `/` (breaks paths), `_` (barcodes) and `--`.

**The rules:**
- **Serials:** capital letters, digits and single dashes only, `^[A-Z0-9]+(-[A-Z0-9]+)*$`. Anything else (lower case, spaces, other characters, a dash at the start or end, two dashes together) is refused at release, with a message naming the rule. Nothing is upper-cased silently.
- **Near-duplicates are still refused:** serials are compared with their dashes removed, so `VBC-92921` and `VBC92921` can't both exist. The released IDs' key stays the dash-free form.
- **Successors:** `-R1`, `-R2`, … (`UN778474481-VBC-92921-R1`).
- **Demo:** `UN778474481-TEST-` + the demo serial (`UN778474481-TEST-DEMO-C7M-0001`). A real serial still may not start with `TEST`.
- **Length:** at most 50 characters (ISO/IEC 15459), with room for `-R99`. **The cap: a real serial has at most 34 characters, dashes included** (50 − 12 for `UN778474481-` − 4 for `-R99`); a demo serial 29 (`-TEST-` takes 5 more). A three-letter agency would make it 33.

**Existing data:** the owner's `battery_pass_v3` holds IDs in the old form (`UN778474481VPC12221`), such as the voided TRIAL IDs. They stay as they are; nothing is migrated. They are still read, shown short and linked, found by the pages, the API, uploads and the QR code. A new passport of a battery whose first ID is in the old form continues that ID (`…R1`, `…R2`), whatever its serial number. The demo reset remakes the demo IDs in the new form.

**Architecture:**
- `web/Dpp/Identifiers/DunsIdentifierScheme.cs`:
  - `IdentifierForms.SerialRule` and `IsSerial` (the rule); `IsDashed` (the new form);
  - the four patterns (`ItemId`, `ItemInAddress`, `VersionInAddress`, `TestItem`) read both forms: `[A-Z]{2,3}[0-9]{9}-?[A-Z0-9]+(?:-[A-Z0-9]+)*`, at most 50 characters. A demo's mark is `-TEST-` or `TEST` right after the company only;
  - `ItemFor` checks the rule on the serial as written, then `TEST`, then the length (`SuccessorRoom` is now 4). The prefix is `UN778474481-`;
  - `NewPackIdentifierAsync`:
    - a successor continues its first battery's ID, `-R<n>` (or `R<n>` for an old-form one);
    - a new battery is refused when another battery's ID is the same but for its dashes (`DppStore.FindPackByItemIgnoringDashesAsync`), the old form included. `Taken`'s words say so.
- `web/Dpp/Identifiers/BatteryIdPool.cs`: a release checks the rule first, whatever the ID scheme, and keeps the dash-free key; a battery's ID is compared by its letters and digits.
- `web/Dpp/Store/DppStore.cs`:
  - `FindPackByItemIgnoringDashesAsync` (new);
  - a successor's suffix is `-?R[0-9]+`.
- The release form's hint names the rule (`web/Views/BatteryIds/Index.cshtml`). A refused release's problems table wraps a long reason (`web/wwwroot/css/bp-battery-ids.css`); **`site.css` is not changed.**
- The `%2F` decoding, the `/25S` route, `Short()`, the pages', API's, uploads' and QR lookups follow the patterns, unchanged.
- **Words:**
  - the Help's Battery IDs part: the example, the rule and the cap, and `-R1, -R2`;
  - the test pack: the demo example, and the refusal check with lower case and a double dash;
  - the per-role script: steps 9, 10, 12 and 15;
  - the accounts page's demo example;
  - the cut-over runbook's examples (the `DPP_ID_BASE_URL` row and check 10).
  - Some code comments.
- **Tests:**
  - `DunsIdentifierTests`: the new form, the rule, the cap, near-duplicates, both forms read, old-form successors, the demo data, and the problems table's wrapping;
  - `TestIdentifierSchemeTests`: the `TEST` mark in both forms;
  - `BatteryIdPoolTests`: the IDs in the new form, the refusal list, the rule under the test scheme too, and an old-form released ID scanned;
  - `BatteryIdsHelpTests`: the new words, and no ID left in the old form in the Help and the documents.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB, xUnit 2.9. Nothing new is downloaded.

**The shots** (no approval round): `step6d5-shots/` in the managing session's notes. They were taken on a scratch app at e9d5a55 plus this plan, with the three ID settings, after "Reset demo data", and a release "PO 4520" with `VBC-92921` and the longest serial allowed, `VBC-92921-ABCDEFGH-0123456789-XYZW` (34 characters, ID 46). A draft battery was made from the long one.
- **All IDs, the release's page and the draft's team page:** at 1280 and 375.
- **The public summary of the demo's longest published ID** (`UN778474481-TEST-DEMO-C13M-0001-R2`): at 1280 and 375.
- **A refused release:** at 1280.

**What the shots showed** (checked by eye and measured):
- Nothing scrolls sideways on any of the nine pages.
- The 46-character ID fits on one line at 1280 and wraps at its own dashes at 375, in the team header, on All IDs and on the release's page. The public summary's `UN778474481-TEST-DEMO-C13M-0001-R2` wraps the same way at 375.
- **One fix came from the shots:** `site.css` keeps table cells on one line, so the serial rule's reason widened the refused release's page by 525 px at 1280. Its problems table now wraps the reason (`bp-battery-ids.css`; `site.css` unchanged).
- At the 46-character maximum, All IDs' table at 1280 scrolls inside its own box, so its last column is partly hidden until scrolled. The page doesn't scroll, and typical IDs fit.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at e9d5a55 ("feat(dpp): voided battery IDs hidden by default…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -3`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas. The tests use their own throwaway MongoDB.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own classes. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2620` (plan 6d-4's result). After this plan: 2653.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6d5-test.patch`). Then run `git apply ../<name>` from the worktree root.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.

**Measured on a clean clone (e9d5a55):**
- the tests first fail: the test build fails with `error CS0117: 'IdentifierForms' does not contain a definition for 'SerialRule'` (22 errors);
- with the changes, Task 1's own classes pass: 116 tests in its six classes;
- the whole suite: `Failed!  - Failed:     2, Passed:  2651, Skipped:     0, Total:  2653` (the clean clone's two known `ApplicationAuditCoverageTests` failures, `site.css` with CRLF line endings, pass in the worktree).

### Task 1: The battery ID with its dash

**Files:**
- Modify (tests): `BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs`, `BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs`, `BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs`, `BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs`
- Modify (app and documents): `docs/aws-v3-cutover.md`, `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md`, `docs/sample-cluster-test-accounts.md`, `web/Controllers/ItemIdController.cs`, `web/Controllers/PassportController.cs`, `web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Identifiers/BatteryIdPool.cs`, `web/Dpp/Identifiers/DunsIdentifierScheme.cs`, `web/Dpp/Identifiers/TestIdentifierScheme.cs`, `web/Dpp/Store/DppStore.cs`, `web/Views/BatteryIds/Index.cshtml`, `web/Views/Help/Guide.cshtml`, `web/wwwroot/css/bp-battery-ids.css`

- [ ] **Step 1: Write the failing tests**

Save as `../6d5-test.patch` and run `git apply ../6d5-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs b/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
index ced59d4..e49f1bf 100644
--- a/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs
@@ -63,7 +63,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         var items = await pool.ListAsync(new ReleasedIdFilter(Reference: result.ReleaseId));
         Assert.Equal(Enumerable.Range(12221, 10).Select(number => $"VPC-{number}"), items.Select(item => item.SerialNumber).Order(StringComparer.Ordinal));
         Assert.All(items, item => Assert.Equal(ReleasedIdStatuses.Available, item.Status));
-        Assert.Equal($"{Base}/25S/UN778474481VPC12221", items.Single(item => item.SerialNumber == "VPC-12221").Identifier);
+        Assert.Equal($"{Base}/25S/UN778474481-VPC-12221", items.Single(item => item.SerialNumber == "VPC-12221").Identifier);
         var release = (await pool.ReleasesAsync()).Single(summary => summary.ReleaseId == result.ReleaseId);
         Assert.Equal(("PO 4512, line 2", model, 10, 10), (release.Reference, release.ModelId, release.Total, release.Count(ReleasedIdStatuses.Available)));
     }
@@ -97,22 +97,36 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "LEGACY-0001") with { FromPool = false });
 
         var result = await pool.ReleaseAsync(Who, new ReleaseRequest("PO 4514", null, [new IdRange("VPC-12228", "VPC-12240", "12")],
-            "VPC-12401\nVPC-12401\nvpc 12301\nTEST-9\n---\nDEMO-C7M-0001\nlegacy 0001\nvpc 12402\nVPC-12402"));
+            "VPC-12401\nVPC-12401\nVPC12301\nTEST-9\n---\nDEMO-C7M-0001\nLEGACY0001\nVPC12402\nVPC-12402\nvpc-12403\nVPC 12404"));
 
         Assert.Equal(0, result.Released);
         Assert.Null(result.ReleaseId);
         var reasons = result.Problems.ToDictionary(problem => problem.What, problem => problem.Reason);
         Assert.Equal("that is 13 IDs, not 12", reasons["VPC-12228 to VPC-12240"]);
         Assert.Equal("it is in this release twice", reasons["VPC-12401"]);
-        Assert.Equal("already released (Available, PO 4513)", reasons["vpc 12301"]);
+        Assert.Equal("already released (Available, PO 4513)", reasons["VPC12301"]); // only the dashes differ (6d-5)
         Assert.Equal(DunsIdentifierScheme.StartsWithTest, reasons["TEST-9"]);
-        Assert.Equal(DunsIdentifierScheme.NoLettersOrDigits, reasons["---"]);
+        Assert.Equal(IdentifierForms.SerialRule, reasons["---"]);
         Assert.Equal("already released (Published, Demo)", reasons["DEMO-C7M-0001"]); // one reason per serial number
-        Assert.Equal("battery LEGACY-0001 already has this serial number or battery ID", reasons["legacy 0001"]);
-        Assert.Equal("it is in this release twice, also as vpc 12402", reasons["VPC-12402"]);
+        Assert.Equal("battery LEGACY-0001 already has this serial number or battery ID", reasons["LEGACY0001"]);
+        Assert.Equal("it is in this release twice, also as VPC12402", reasons["VPC-12402"]);
+        Assert.Equal(IdentifierForms.SerialRule, reasons["vpc-12403"]); // never upper-cased silently
+        Assert.Equal(IdentifierForms.SerialRule, reasons["VPC 12404"]);
         Assert.DoesNotContain(await pool.ReleasesAsync(), release => release.Reference == "PO 4514");
     }
 
+    [Fact]
+    public async Task TheSerialRule_ShouldHoldForEveryRelease_WhateverTheIdScheme()
+    {
+        var pool = new BatteryIdPool(new DppStore(mongo.NewContext()), new TestIdentifierScheme(), new ApplicationAuditService(mongo.NewContext()), TimeProvider.System);
+
+        var result = await pool.ReleaseAsync(Who, List("PO rule", "VBC-92921", "vbc-92922", "VBC-92923-"));
+
+        Assert.False(result.Done);
+        Assert.Equal(["vbc-92922", "VBC-92923-"], result.Problems.Select(problem => problem.What));
+        Assert.All(result.Problems, problem => Assert.Equal(IdentifierForms.SerialRule, problem.Reason));
+    }
+
     [Fact]
     public async Task AReleaseWithoutAReference_ShouldBeRefused() =>
         Assert.Equal("give the release a reference, such as the purchase order", (await new BatteryIdPool(new DppStore(mongo.NewContext()), new TestIdentifierScheme(), new ApplicationAuditService(mongo.NewContext()), TimeProvider.System).ReleaseAsync(Who, List(" ", "VPC-1"))).Problem);
@@ -158,7 +172,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
 
         var pack = made.Pack!;
         Assert.Equal("VPC-12221", pack.Values["batterySerialNumber"].Value!.GetValue<string>()); // the released spelling
-        Assert.Equal($"{Base}/25S/UN778474481VPC12221", pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>());
+        Assert.Equal($"{Base}/25S/UN778474481-VPC-12221", pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>());
         Assert.Equal((ReleasedIdStatuses.Used, pack.Id), ((await pool.GetAsync("VPC-12221"))!.Status, (await pool.GetAsync("VPC-12221"))!.PackId));
         Assert.Null(again.Pack); // taken (the serial number is another battery's)
         Assert.Equal(PackCreationService.NotAvailable, never.Problem);
@@ -191,7 +205,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         Assert.Null(await pool.SwapAsync(Who, draft.Id, "SWAP-0002"));
         var swapped = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!;
         Assert.Equal("SWAP-0002", swapped.Values["batterySerialNumber"].Value!.GetValue<string>());
-        Assert.EndsWith("/25S/UN778474481SWAP0002", swapped.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
+        Assert.EndsWith("/25S/UN778474481-SWAP-0002", swapped.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
         Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("SWAP-0001"))!.Status);
 
         Assert.Null((await rig.Services.GetRequiredService<BatteryPages>().RemoveDraftAsync(Who, draft.Id)).Problem);
@@ -226,16 +240,29 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         using var __ = rig;
         await pool.ReleaseAsync(Who, List("PO scan", "SCAN-0001", "SCAN-0002"));
         await pool.VoidAsync(Who, "SCAN-0002", "Label damaged");
+        await rig.Store.ReleasedIds.InsertOneAsync(new ReleasedBatteryId // released before 6d-5, in the old form
+        {
+            Id = "OLD0001",
+            SerialNumber = "OLD-0001",
+            Identifier = $"{Base}/25S/UN778474481OLD0001",
+            ReleaseId = "old-release",
+            Reference = "PO old",
+            Status = ReleasedIdStatuses.Available,
+            ReleasedBy = Who,
+            ReleasedAtUtc = DateTime.UtcNow,
+        });
         var controller = new PassportController(new DppReaders(rig.Services.GetRequiredService<ClusterRepository>()), rig.Services.GetRequiredService<PassportPages>())
         {
             ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
         };
 
-        var available = Assert.IsType<ViewResult>(await controller.Summary("UN778474481SCAN0001", CancellationToken.None, pool));
-        var voided = Assert.IsType<ViewResult>(await controller.Summary("UN778474481SCAN0002", CancellationToken.None, pool));
-        var published = Assert.IsType<ViewResult>(await controller.Summary("UN778474481TESTDEMOC7M0001", CancellationToken.None, pool));
+        var available = Assert.IsType<ViewResult>(await controller.Summary("UN778474481-SCAN-0001", CancellationToken.None, pool));
+        var voided = Assert.IsType<ViewResult>(await controller.Summary("UN778474481-SCAN-0002", CancellationToken.None, pool));
+        var published = Assert.IsType<ViewResult>(await controller.Summary("UN778474481-TEST-DEMO-C7M-0001", CancellationToken.None, pool));
+        var old = Assert.IsType<ViewResult>(await controller.Summary("UN778474481OLD0001", CancellationToken.None, pool));
 
         Assert.Equal(("~/Views/Dpp/NotPublishedYet.cshtml", 404), (available.ViewName, available.StatusCode));
+        Assert.Equal(("~/Views/Dpp/NotPublishedYet.cshtml", 404), (old.ViewName, old.StatusCode));
         Assert.Equal(("~/Views/Dpp/NotAvailable.cshtml", 404), (voided.ViewName, voided.StatusCode));
         Assert.Equal("Summary", published.ViewName);
         var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", "NotPublishedYet.cshtml"));
@@ -258,7 +285,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
             },
         };
 
-        var svg = Assert.IsType<ContentResult>(await qr.QrCode("UN778474481QR0001", CancellationToken.None));
+        var svg = Assert.IsType<ContentResult>(await qr.QrCode("UN778474481-QR-0001", CancellationToken.None));
 
         Assert.Contains("<svg", svg.Content, StringComparison.Ordinal); // labels can be printed before publication (decision 30)
     }
@@ -276,7 +303,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
 
         Assert.Equal(DemoDataService.SpareSerials.Length, demo.Count(ReleasedIdStatuses.Available));
         Assert.True(demo.Count(ReleasedIdStatuses.Published) > 0);
-        Assert.All(spares, spare => Assert.Matches("/25S/UN778474481TESTDEMOSPARE000[1-4]$", spare.Identifier));
+        Assert.All(spares, spare => Assert.Matches("/25S/UN778474481-TEST-DEMO-SPARE-000[1-4]$", spare.Identifier));
         Assert.Equal(["REAL-0001"], (await pool.ListAsync(new ReleasedIdFilter())).Select(item => item.SerialNumber)); // real IDs stay
     }
 
@@ -291,7 +318,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         var made = await api.CreateAsync(ChangeSender.Team(Who), c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "API-0001" });
         var refused = await api.CreateAsync(ChangeSender.Team(Who), c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "API-0002" });
 
-        Assert.EndsWith("/25S/UN778474481API0001", made.Pack!.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
+        Assert.EndsWith("/25S/UN778474481-API-0001", made.Pack!.Values["uniqueProductIdentifier"].Value!.GetValue<string>(), StringComparison.Ordinal);
         Assert.Equal(PackCreationService.NotAvailable, refused.Problem);
     }
 
@@ -309,7 +336,7 @@ public sealed class BatteryIdPoolTests(MongoFixture mongo)
         var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
         var rows = Regex.Matches(sheet, "<row r=\"(\\d+)\">(.*?)</row>").Select(row => Regex.Matches(row.Groups[2].Value, "<t xml:space=\"preserve\">(.*?)</t>").Select(cell => System.Net.WebUtility.HtmlDecode(cell.Groups[1].Value)).ToArray()).ToArray();
         Assert.Equal(["Serial number", "Battery ID", "QR code address", "Model", "Reference", "Released by", "Released (UTC)", "Status"], rows[0]);
-        Assert.Equal(["000123", "UN778474481000123", $"{Base}/25S/UN778474481000123", "Any model", "=PO Åsa; \"x\""], rows[1][..5]);
+        Assert.Equal(["000123", "UN778474481-000123", $"{Base}/25S/UN778474481-000123", "Any model", "=PO Åsa; \"x\""], rows[1][..5]);
         Assert.Equal([items[0].ReleasedBy, "Available"], rows[1][5..]); // the date cell isn't text
         Assert.DoesNotContain("<f>", sheet, StringComparison.Ordinal); // text cells: no formula, nothing turned into a number or date
         Assert.Contains("t=\"inlineStr\"", sheet, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
index 7490b5a..9c12532 100644
--- a/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs
@@ -19,9 +19,10 @@ namespace BatteryPassWeb.Tests.Dpp;
 
 /// <summary>
 /// Step 6d, the battery ID the product owner chose (O-1 and 6d, 5 Nov 2026): an ISO/IEC 15459 item ID under issuing
-/// agency UN (DUNS 778474481) and the battery's serial number with only its letters and digits, written as a web address
-/// (ISO/IEC 18975, data identifier 25S) under https://www.sib-scania-eubp.eu; made when the battery is created, a
-/// successor adding R1, R2, …; a demo battery's marked TEST. A version's passport ID is /dpp/&lt;item&gt;/v&lt;n&gt;.
+/// agency UN (DUNS 778474481), one dash and the battery's serial number as written (6d-5: capital letters, digits and
+/// single dashes), as a web address (ISO/IEC 18975, data identifier 25S) under https://www.sib-scania-eubp.eu; made when
+/// the battery is created, a successor adding -R1, -R2, …; a demo battery's marked -TEST-. A version's passport ID is
+/// /dpp/&lt;item&gt;/v&lt;n&gt;. IDs made before 6d-5 (UN778474481VPC12221) stay as they are and are still read.
 /// </summary>
 [Collection(MongoCollection.Name)]
 public sealed class DunsIdentifierTests(MongoFixture mongo)
@@ -45,17 +46,18 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
     }
 
     [Fact]
-    public async Task ABatteryId_ShouldBeTheAgencyTheDunsAndTheSerialsLettersAndDigits_AsAWebAddress()
+    public async Task ABatteryId_ShouldBeTheAgencyTheDunsADashAndTheSerialAsWritten_AsAWebAddress()
     {
         var scheme = Scheme(new DppStore(mongo.NewContext()));
 
-        var made = await scheme.NewPackIdentifierAsync(Pack("VPC-12221"));
+        var made = await scheme.NewPackIdentifierAsync(Pack("VBC-92921"));
 
-        Assert.Equal($"{Base}/25S/UN778474481VPC12221", made.Identifier);
+        Assert.Equal($"{Base}/25S/UN778474481-VBC-92921", made.Identifier);
         Assert.Null(made.Problem);
         Assert.False(scheme.IsTestIdentifier(made.Identifier!));
         Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(6), JsonValue.Create(made.Identifier)));
-        Assert.Equal("UN778474481VPC12221", (await scheme.NewPackIdentifierAsync(Pack(" vpc 12221/ä "))).Identifier![^19..]);
+        Assert.Equal($"{Base}/dpp/UN778474481-VBC-92921/v1", scheme.NewPassportIdentifier(made.Identifier!, 1));
+        Assert.Equal($"{Base}/25S/UN778474481-VBC92921", (await scheme.NewPackIdentifierAsync(Pack("VBC92921"))).Identifier); // no other battery has it yet
     }
 
     [Fact]
@@ -65,18 +67,27 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
 
         var demo = (await scheme.NewPackIdentifierAsync(Pack("DEMO-C7M-0001", demo: true))).Identifier!;
 
-        Assert.Equal($"{Base}/25S/UN778474481TESTDEMOC7M0001", demo);
+        Assert.Equal($"{Base}/25S/UN778474481-TEST-DEMO-C7M-0001", demo);
         Assert.True(scheme.IsTestIdentifier(demo));
-        Assert.True(scheme.IsTestIdentifier($"{Base}/dpp/UN778474481TESTDEMOC7M0001R1/v2"));
-        Assert.Matches("^[A-Z0-9]{1,50}$", IdentifierForms.Short(demo)); // ISO/IEC 15459: A–Z, 0–9, at most 50
+        Assert.True(scheme.IsTestIdentifier($"{Base}/dpp/UN778474481-TEST-DEMO-C7M-0001-R1/v2"));
+        Assert.Matches("^[A-Z0-9-]{1,50}$", IdentifierForms.Short(demo)); // ISO/IEC 15459: A–Z, 0–9 and the dash, at most 50
     }
 
     [Theory]
-    [InlineData("---", false, DunsIdentifierScheme.NoLettersOrDigits)]
-    [InlineData("äöü", false, DunsIdentifierScheme.NoLettersOrDigits)]
+    [InlineData("vbc-92921", false, IdentifierForms.SerialRule)] // never upper-cased silently
+    [InlineData("VBC 92921", false, IdentifierForms.SerialRule)]
+    [InlineData("-VBC-92921", false, IdentifierForms.SerialRule)]
+    [InlineData("VBC-92921-", false, IdentifierForms.SerialRule)]
+    [InlineData("VBC--92921", false, IdentifierForms.SerialRule)]
+    [InlineData("VBC_92921", false, IdentifierForms.SerialRule)]
+    [InlineData("VBC.92921", false, IdentifierForms.SerialRule)]
+    [InlineData("VBC/92921", false, IdentifierForms.SerialRule)]
+    [InlineData("ÄBC-92921", false, IdentifierForms.SerialRule)]
+    [InlineData("---", false, IdentifierForms.SerialRule)]
+    [InlineData("", false, IdentifierForms.SerialRule)]
     [InlineData("TEST-0001", false, DunsIdentifierScheme.StartsWithTest)]
-    [InlineData("A23456789012345678901234567890123456X", false, "at most 36 letters and digits")] // 37: no room for R99 within 50
-    [InlineData("A2345678901234567890123456789012X", true, "at most 32 letters and digits")] // a demo's TEST takes 4
+    [InlineData("A234567890-234567890-234567890-2345", false, "at most 34 characters")] // 35: no room for -R99 within 50
+    [InlineData("A234567890-234567890-234567890", true, "at most 29 characters")] // a demo's TEST- takes 5
     public async Task ASerialThatCantMakeAValidId_ShouldBeRefused(string serial, bool demo, string problem)
     {
         var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(serial, demo));
@@ -88,30 +99,35 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
     [Fact]
     public async Task TheLongestSerial_ShouldStillLeaveRoomForASuccessorsR99()
     {
-        var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(new string('A', 36)));
+        var made = await Scheme(new DppStore(mongo.NewContext())).NewPackIdentifierAsync(Pack(new string('A', 34)));
 
-        Assert.Equal(47, IdentifierForms.Short(made.Identifier!).Length);
-        Assert.True(IdentifierForms.IsItem(IdentifierForms.Short(made.Identifier!) + "R99"));
-        Assert.False(IdentifierForms.IsItem("UN778474481" + new string('A', 40)));
+        Assert.Equal(46, IdentifierForms.Short(made.Identifier!).Length);
+        Assert.True(IdentifierForms.IsItem(IdentifierForms.Short(made.Identifier!) + "-R99"));
+        Assert.False(IdentifierForms.IsItem("UN778474481-" + new string('A', 39)));
     }
 
     [Fact]
-    public async Task AnIdAnotherBatteryHas_ShouldBeRefused_NamingThatBattery()
+    public async Task AnIdAnotherBatteryHas_ShouldBeRefused_NamingThatBattery_EvenWhenOnlyTheDashesDiffer()
     {
         var store = new DppStore(mongo.NewContext());
-        await store.InsertAsync(Pack("VPC-12221", identifier: $"{Base}/25S/UN778474481VPC12221"));
+        await store.InsertAsync(Pack("VBC-92921", identifier: $"{Base}/25S/UN778474481-VBC-92921"));
+        await store.InsertAsync(Pack("VPC-12221", identifier: $"{Base}/25S/UN778474481VPC12221")); // an ID made before 6d-5
+        var scheme = Scheme(store);
 
-        var made = await Scheme(store).NewPackIdentifierAsync(Pack("vpc 12221"));
+        var nearDuplicate = await scheme.NewPackIdentifierAsync(Pack("VBC92921"));
+        var oldForm = await scheme.NewPackIdentifierAsync(Pack("VPC-12221"));
 
-        Assert.Null(made.Identifier);
-        Assert.Equal(DunsIdentifierScheme.Taken("UN778474481VPC12221", "VPC-12221"), made.Problem);
+        Assert.Null(nearDuplicate.Identifier);
+        Assert.Equal(DunsIdentifierScheme.Taken("UN778474481-VBC-92921", "VBC-92921"), nearDuplicate.Problem);
+        Assert.Equal(DunsIdentifierScheme.Taken("UN778474481VPC12221", "VPC-12221"), oldForm.Problem);
+        Assert.Equal($"{Base}/25S/UN778474481-BC-92921", (await scheme.NewPackIdentifierAsync(Pack("BC-92921"))).Identifier); // only the whole ID counts
     }
 
     [Fact]
     public async Task ASuccessor_ShouldGetTheNextFreeRNumber()
     {
         var store = new DppStore(mongo.NewContext());
-        var first = Pack("VPC-77001", identifier: $"{Base}/25S/UN778474481VPC77001");
+        var first = Pack("VPC-77001", identifier: $"{Base}/25S/UN778474481-VPC-77001");
         await store.InsertAsync(first);
         var scheme = Scheme(store);
 
@@ -120,8 +136,30 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         await store.InsertAsync(second);
         var r2 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77001", successorOf: second.Id))).Identifier!;
 
-        Assert.Equal($"{Base}/25S/UN778474481VPC77001R1", r1);
-        Assert.Equal($"{Base}/25S/UN778474481VPC77001R2", r2);
+        Assert.Equal($"{Base}/25S/UN778474481-VPC-77001-R1", r1);
+        Assert.Equal($"{Base}/25S/UN778474481-VPC-77001-R2", r2);
+    }
+
+    [Fact]
+    public async Task ASuccessorOfABatteryWithAnIdFromBefore65_ShouldContinueThatId()
+    {
+        // Old records stay as they are (the owner, 6d-5): the new passport continues the first one's ID, whatever its serial.
+        var store = new DppStore(mongo.NewContext());
+        var old = Pack("VPC-77002", identifier: $"{Base}/25S/UN778474481VPC77002");
+        var loose = Pack("vpc 77003", identifier: $"{Base}/25S/UN778474481VPC77003"); // a serial the rule now refuses
+        await store.InsertAsync(old);
+        await store.InsertAsync(loose);
+        var scheme = Scheme(store);
+
+        var r1 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77002", successorOf: old.Id))).Identifier!;
+        var second = Pack("VPC-77002", successorOf: old.Id, identifier: r1);
+        await store.InsertAsync(second);
+        var r2 = (await scheme.NewPackIdentifierAsync(Pack("VPC-77002", successorOf: second.Id))).Identifier!;
+        var looseR1 = await scheme.NewPackIdentifierAsync(Pack("vpc 77003", successorOf: loose.Id));
+
+        Assert.Equal($"{Base}/25S/UN778474481VPC77002R1", r1);
+        Assert.Equal($"{Base}/25S/UN778474481VPC77002R2", r2);
+        Assert.Equal(($"{Base}/25S/UN778474481VPC77003R1", (string?)null), (looseR1.Identifier, looseR1.Problem));
     }
 
     [Fact]
@@ -129,9 +167,9 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
     {
         var scheme = Scheme(new DppStore(mongo.NewContext()));
 
-        var passport = scheme.NewPassportIdentifier($"{Base}/25S/UN778474481VPC12221", 2);
+        var passport = scheme.NewPassportIdentifier($"{Base}/25S/UN778474481-VBC-92921-R1", 2);
 
-        Assert.Equal($"{Base}/dpp/UN778474481VPC12221/v2", passport);
+        Assert.Equal($"{Base}/dpp/UN778474481-VBC-92921-R1/v2", passport);
         Assert.False(scheme.IsTestIdentifier(passport));
         Assert.Empty(new AttributeValueValidator(DppStoreTesting.Schemas).Validate(AttributeCatalog.ByNumber(5), JsonValue.Create(passport)));
     }
@@ -149,6 +187,11 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
     [Fact]
     public void ThePagesAddresses_ShouldUseTheShortItem()
     {
+        Assert.Equal("/dpp/UN778474481-VBC-92921", PassportPages.SummaryHref($"{Base}/25S/UN778474481-VBC-92921"));
+        Assert.Equal("/dpp/UN778474481-VBC-92921-R1/v2/detail", PassportPages.DetailHref($"{Base}/dpp/UN778474481-VBC-92921-R1/v2"));
+        Assert.Equal("/qr/UN778474481-VBC-92921/svg", PassportPages.QrHref($"{Base}/25S/UN778474481-VBC-92921"));
+        Assert.Equal("UN778474481-VBC-92921", BatteryPages.ShortId($"{Base}/25S/UN778474481-VBC-92921"));
+        // IDs made before 6d-5 keep theirs.
         Assert.Equal("/dpp/UN778474481VPC12221", PassportPages.SummaryHref($"{Base}/25S/UN778474481VPC12221"));
         Assert.Equal("/dpp/UN778474481VPC12221/detail", PassportPages.DetailHref($"{Base}/25S/UN778474481VPC12221"));
         Assert.Equal("/dpp/UN778474481VPC12221R1/v2", PassportPages.SummaryHref($"{Base}/dpp/UN778474481VPC12221R1/v2"));
@@ -166,7 +209,8 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         var live = new DppLinks(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = $"{Base}/" })));
         var pc = new DppLinks(Options.Create(DemoRig.WithDunsIds(new BatteryPassOptions { AppBaseUrl = "http://localhost:5186" })));
 
-        Assert.Equal($"{Base}/25S/UN778474481VPC12221", live.PassportUrl($"{Base}/25S/UN778474481VPC12221"));
+        Assert.Equal($"{Base}/25S/UN778474481-VBC-92921", live.PassportUrl($"{Base}/25S/UN778474481-VBC-92921"));
+        Assert.Equal($"{Base}/25S/UN778474481VPC12221", live.PassportUrl($"{Base}/25S/UN778474481VPC12221")); // made before 6d-5
         Assert.Equal("http://localhost:5186/25S/UN778474481VPC12221", pc.PassportUrl($"{Base}/25S/UN778474481VPC12221"));
         Assert.Equal("http://localhost:5186/dpp/urn:x-battery-pass:TEST-0f8c", pc.PassportUrl("urn:x-battery-pass:TEST-0f8c"));
     }
@@ -176,8 +220,10 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
     {
         var item = new ItemIdController();
 
-        Assert.Equal("/dpp/UN778474481VPC12221", Assert.IsType<RedirectResult>(item.Item("UN778474481VPC12221")).Url);
+        Assert.Equal("/dpp/UN778474481-VBC-92921", Assert.IsType<RedirectResult>(item.Item("UN778474481-VBC-92921")).Url);
+        Assert.Equal("/dpp/UN778474481VPC12221", Assert.IsType<RedirectResult>(item.Item("UN778474481VPC12221")).Url); // made before 6d-5
         Assert.IsType<NotFoundResult>(item.Item("not-an-id"));
+        Assert.IsType<NotFoundResult>(item.Item("UN778474481-VBC--92921"));
         Assert.Contains("\"25S\"", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "HomeController.cs")), StringComparison.Ordinal); // a scanned address is searched by its item
     }
 
@@ -188,7 +234,7 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         // slashes encoded (%2F), so the lookups decode it once.
         using var rig = await DemoRig.StartAsync(mongo, dunsIds: true);
         await rig.Demo.SeedAsync(DemoRig.Who);
-        var encoded = Uri.EscapeDataString($"{Base}/25S/UN778474481TESTDEMOC7M0001").Replace("%3A", ":", StringComparison.Ordinal);
+        var encoded = Uri.EscapeDataString($"{Base}/25S/UN778474481-TEST-DEMO-C7M-0001").Replace("%3A", ":", StringComparison.Ordinal);
         var encodedPassport = Uri.EscapeDataString((await rig.Services.GetRequiredService<PassportPages>().FindAsync("DEMO-C7M-0001"))!.DigitalProductPassportId);
 
         var page = await rig.Services.GetRequiredService<PassportPages>().FindAsync(encoded);
@@ -205,6 +251,10 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
     public void ALongIdValue_ShouldWrapOnAPhone_RatherThanScrollThePage() =>
         Assert.Contains(".bp-field-grid dd {\n  min-width: 0;\n  overflow-wrap: anywhere;\n}", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-status.css")).Replace("\r\n", "\n"), StringComparison.Ordinal);
 
+    [Fact]
+    public void ALongRefusal_ShouldWrapInTheProblemsTable_RatherThanWidenThePage() => // the serial rule's words (6d-5)
+        Assert.Contains(".bp-problems-table td:last-child {\n  min-width: 16rem;\n  white-space: normal;\n}", File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-battery-ids.css")).Replace("\r\n", "\n"), StringComparison.Ordinal);
+
     [Fact]
     public async Task TheDemoData_ShouldGetTestIds_AndSuccessorsRNumbers()
     {
@@ -217,17 +267,18 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         Assert.All(packs, pack =>
         {
             var values = pack["Values"].AsBsonDocument;
-            var serial = IdentifierForms.Clean(values["batterySerialNumber"]["Value"].AsString);
-            Assert.Matches($"^{Regex.Escape(Base)}/25S/UN778474481TEST{serial}(R[0-9]+)?$", values["uniqueProductIdentifier"]["Value"].AsString);
+            var serial = values["batterySerialNumber"]["Value"].AsString; // as written: the demo's are in the rule's form
+            Assert.Matches($"^{Regex.Escape(Base)}/25S/UN778474481-TEST-{Regex.Escape(serial)}(-R[0-9]+)?$", values["uniqueProductIdentifier"]["Value"].AsString);
         });
         var identifiers = packs.Select(pack => pack["Values"]["uniqueProductIdentifier"]["Value"].AsString).ToArray();
-        Assert.Contains(identifiers, id => id.EndsWith("TESTDEMOC13M0001R1", StringComparison.Ordinal)); // DEMO-C13M-0001's second passport
-        Assert.Contains(identifiers, id => id.EndsWith("TESTDEMOC13M0001R2", StringComparison.Ordinal)); // and its third
+        Assert.Contains(identifiers, id => id.EndsWith("-TEST-DEMO-C13M-0001-R1", StringComparison.Ordinal)); // DEMO-C13M-0001's second passport
+        Assert.Contains(identifiers, id => id.EndsWith("-TEST-DEMO-C13M-0001-R2", StringComparison.Ordinal)); // and its third
 
         // A battery is found by its short item, its page by its version's address.
         var c7 = (await pages.FindAsync("DEMO-C7M-0001"))!;
-        Assert.Equal(c7.PackId, (await pages.FindAsync("UN778474481TESTDEMOC7M0001"))!.PackId);
-        Assert.Equal($"{Base}/dpp/UN778474481TESTDEMOC7M0001/v{c7.Number}", c7.DigitalProductPassportId);
+        Assert.Equal(c7.PackId, (await pages.FindAsync("UN778474481-TEST-DEMO-C7M-0001"))!.PackId);
+        Assert.Equal(c7.PackId, (await pages.FindAsync($"{Base}/dpp/UN778474481-TEST-DEMO-C7M-0001/v{c7.Number}"))!.PackId);
+        Assert.Equal($"{Base}/dpp/UN778474481-TEST-DEMO-C7M-0001/v{c7.Number}", c7.DigitalProductPassportId);
     }
 
     [Fact]
@@ -240,10 +291,10 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         var before = await rig.Count(DppStore.Packs);
 
         var result = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(team, new NewPack(
-            c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "demo c7m 0001" }, IsDemo: true));
+            c7.BatchId, c7.OperatorId, c7.ClusterId, new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = "DEMOC7M0001" }, IsDemo: true));
 
         Assert.Null(result.Pack);
-        Assert.Equal($"{PackCreationService.CantGetId}: {DunsIdentifierScheme.Taken("UN778474481TESTDEMOC7M0001", "DEMO-C7M-0001")}", result.Problem);
+        Assert.Equal($"{PackCreationService.CantGetId}: {DunsIdentifierScheme.Taken("UN778474481-TEST-DEMO-C7M-0001", "DEMO-C7M-0001")}", result.Problem);
         Assert.Equal(before, await rig.Count(DppStore.Packs));
     }
 
@@ -279,13 +330,15 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         var text = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", folder, view));
 
         Assert.DoesNotContain("scheme is chosen", text, StringComparison.Ordinal); // chosen on 5 Nov 2026
-        Assert.DoesNotContain("start with TEST-", text, StringComparison.Ordinal); // a demo ID is UN778474481TEST…
+        Assert.DoesNotContain("start with TEST-", text, StringComparison.Ordinal); // a demo ID is UN778474481-TEST-…
         Assert.Contains("marked", text, StringComparison.Ordinal);
     }
 
     [Fact]
     public void AnId_ShouldBeShownShort_AndLinkedToItsFullAddress()
     {
+        Assert.Equal("UN778474481-VBC-92921", IdentifierForms.Display($"{Base}/25S/UN778474481-VBC-92921"));
+        Assert.Equal("UN778474481-VBC-92921-R1/v2", IdentifierForms.Display($"{Base}/dpp/UN778474481-VBC-92921-R1/v2"));
         Assert.Equal("UN778474481VPC12221", IdentifierForms.Display($"{Base}/25S/UN778474481VPC12221"));
         Assert.Equal("UN778474481VPC12221R1/v2", IdentifierForms.Display($"{Base}/dpp/UN778474481VPC12221R1/v2"));
         Assert.Equal("urn:x-battery-pass:TEST-0f8c", IdentifierForms.Display("urn:x-battery-pass:TEST-0f8c"));
@@ -294,6 +347,42 @@ public sealed class DunsIdentifierTests(MongoFixture mongo)
         Assert.Contains("d-none d-print-inline\">@Model<", partial, StringComparison.Ordinal); // on paper the full address
     }
 
+    [Theory]
+    [InlineData("UN778474481-VBC-92921", true)]
+    [InlineData("UN778474481-VBC-92921-R1", true)]
+    [InlineData("UN778474481-TEST-DEMO-C7M-0001", true)]
+    [InlineData("UN778474481-000123", true)]
+    [InlineData("UN778474481VPC12221", true)] // made before 6d-5: still read
+    [InlineData("UN778474481VPC12221R1", true)]
+    [InlineData("UN778474481TESTDEMOC7M0001", true)]
+    [InlineData("UN778474481", false)]
+    [InlineData("UN778474481-", false)]
+    [InlineData("UN778474481--VBC", false)]
+    [InlineData("UN778474481-VBC--92921", false)]
+    [InlineData("UN778474481-VBC-92921-", false)]
+    [InlineData("UN778474481-vbc-92921", false)]
+    [InlineData("UN778474481-VBC_92921", false)]
+    public void AnItemId_ShouldBeRead_InTheNewFormAndTheOldOne(string item, bool valid)
+    {
+        Assert.Equal(valid, IdentifierForms.IsItem(item));
+        Assert.Equal(valid ? item : $"{Base}/25S/{item}", IdentifierForms.Short($"{Base}/25S/{item}"));
+        Assert.Equal(valid ? $"{item}/v3" : $"{Base}/dpp/{item}/v3", IdentifierForms.Display($"{Base}/dpp/{item}/v3"));
+        Assert.Equal(valid, IdentifierForms.IsWebAddress($"{Base}/25S/{item}"));
+    }
+
+    [Fact]
+    public void TheSerialRule_ShouldBeCapitalsDigitsAndSingleDashes()
+    {
+        Assert.True(IdentifierForms.IsSerial("VBC-92921"));
+        Assert.True(IdentifierForms.IsSerial("DEMO-C7M-0001"));
+        Assert.True(IdentifierForms.IsSerial("000123"));
+        Assert.False(IdentifierForms.IsSerial("VBC--92921"));
+        Assert.False(IdentifierForms.IsSerial("vbc-92921"));
+        Assert.False(IdentifierForms.IsSerial(null));
+        Assert.Contains("capital letters A–Z, digits 0–9 and single dashes", IdentifierForms.SerialRule, StringComparison.Ordinal);
+        Assert.Equal(34, DunsIdentifierScheme.MaxItemLength - "UN778474481-".Length - "-R99".Length); // the cap: a real serial at most 34 characters
+    }
+
     [Fact]
     public void TheVersionsAddress_ShouldShowThatVersion() =>
         Assert.Contains("return View(nameof(Summary), page);", File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "PassportController.cs")), StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs b/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
index 9967da6..2a6bce4 100644
--- a/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs
@@ -47,6 +47,11 @@ public sealed class TestIdentifierSchemeTests
     [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481TESTDEMOC7M0001", true)]
     [InlineData("https://www.sib-scania-eubp.eu/dpp/UN778474481TESTDEMOC7M0001R1/v2", true)]
     [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221", false)]
+    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-TEST-DEMO-C7M-0001", true)] // 6d-5
+    [InlineData("https://www.sib-scania-eubp.eu/dpp/UN778474481-TEST-DEMO-C7M-0001-R1/v2", true)]
+    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-VBC-92921", false)]
+    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-AB-TEST-1", false)]
+    [InlineData("https://www.sib-scania-eubp.eu/25S/UN778474481-ABUN123456789TEST1", false)] // a TEST mark only right after the company
     public void IsTestIdentifier_ShouldRecogniseOnlyMarkedIdentifiers(string identifier, bool expected)
     {
         Assert.Equal(expected, _scheme.IsTestIdentifier(identifier));
diff --git a/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs b/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
index ddb9e6f..a188aa1 100644
--- a/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
+++ b/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
@@ -27,7 +27,7 @@ public sealed class BatteryIdsHelpTests
 
         Assert.True(Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Guide.IndexOf("id=\"battery-ids\"", StringComparison.Ordinal)); // the team's only
         Assert.Contains("<a href=\"@Part(\"battery-ids\")\">Battery IDs</a>", Read("web", "Views", "Shared", "_HelpContents.cshtml"), StringComparison.Ordinal);
-        foreach (var words in new[] { "href=\"/admin/battery-ids\"", "all or nothing", "the app opens the release's page", "Releases", "All IDs", "Show demo IDs", "A release whose IDs are all voided shows only with \"Show voided\" ticked", "voided IDs only with \"Show voided\" ticked or the status Voided chosen", "⋯ menu", "Export to Excel", "Available, Used", "Voided", "Void unused…", "Change its battery ID…", "Removing a draft gives its ID back", "R1, R2", "The battery ID settings aren't set", "DPP_ID_BASE_URL", "The demo data works without them" })
+        foreach (var words in new[] { "href=\"/admin/battery-ids\"", "all or nothing", "the app opens the release's page", "Releases", "All IDs", "Show demo IDs", "A release whose IDs are all voided shows only with \"Show voided\" ticked", "voided IDs only with \"Show voided\" ticked or the status Voided chosen", "⋯ menu", "Export to Excel", "Available, Used", "Voided", "Void unused…", "Change its battery ID…", "Removing a draft gives its ID back", "-R1, -R2", "The battery ID settings aren't set", "DPP_ID_BASE_URL", "The demo data works without them" })
         {
             Assert.Contains(words, part, StringComparison.Ordinal);
         }
@@ -66,6 +66,30 @@ public sealed class BatteryIdsHelpTests
         Assert.Contains("DEMO-SPARE-0001 to DEMO-SPARE-0004", Read("docs", "sample-cluster-test-accounts.md"), StringComparison.Ordinal);
     }
 
+    [Fact]
+    public void TheHelpAndTheDocs_ShouldWriteTheBatteryIdWithItsDash()
+    {
+        // 6d-5 (the owner, 9 Nov 2026): the serial as written, after one dash; successors -R1, -R2; a demo's -TEST-.
+        var part = Part("battery-ids");
+        var pack = Read("docs", "qa-test-pack.md");
+        var script = Read("docs", "end-user-testing-guide.md");
+        var runbook = Read("docs", "aws-v3-cutover.md");
+        var accounts = Read("docs", "sample-cluster-test-accounts.md");
+
+        Assert.Contains("<code>UN778474481-VPC-12221</code>", part, StringComparison.Ordinal);
+        Assert.Contains("capital letters, digits and single dashes", part, StringComparison.Ordinal);
+        Assert.Contains("adds -R1, -R2, …", part, StringComparison.Ordinal);
+        Assert.Contains("`UN778474481-TEST-DEMO-C7M-0001`", pack, StringComparison.Ordinal);
+        Assert.Contains("`UN778474481-TEST-DEMO-C7M-0001`", accounts, StringComparison.Ordinal);
+        Assert.Contains("(`UN778474481-VPC-12221` …)", script, StringComparison.Ordinal);
+        Assert.Contains("`/25S/UN778474481-VPC-12223`", script, StringComparison.Ordinal);
+        Assert.Contains("A demo ID reads `UN778474481-TEST-…`", runbook, StringComparison.Ordinal);
+        foreach (var text in new[] { Guide, pack, script, runbook, accounts })
+        {
+            Assert.DoesNotMatch("UN778474481[A-Z0-9]", text); // no ID left in the old form
+        }
+    }
+
     [Fact]
     public void TheDemoSpares_ShouldBeTheOnesTheDocsName() =>
         Assert.Equal(["DEMO-SPARE-0001", "DEMO-SPARE-0002", "DEMO-SPARE-0003", "DEMO-SPARE-0004"], BatteryPassWeb.Dpp.Demo.DemoDataService.SpareSerials);
```

- [ ] **Step 2: See them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`
Expected: the test build fails with `error CS0117: 'IdentifierForms' does not contain a definition for 'SerialRule'` (22 errors).

- [ ] **Step 3: Write the change**

Save as `../6d5-app.patch` and run `git apply ../6d5-app.patch`:

```diff
diff --git a/docs/aws-v3-cutover.md b/docs/aws-v3-cutover.md
index e84d4f8..84f8dee 100644
--- a/docs/aws-v3-cutover.md
+++ b/docs/aws-v3-cutover.md
@@ -78,7 +78,7 @@ The new app reads these. "Same" means the live environment's current value stays
 | `ID_GENERATION_SECRET` | same | Only the old app reads it. Keeping it makes a roll-back simpler. |
 | `DPP_ID_AGENCY` | `UN` (new) | The battery IDs' issuing agency: DUNS (decision 38). |
 | `DPP_ID_COMPANY` | `778474481` (new) | Scania Industrial Batteries AB's DUNS. |
-| `DPP_ID_BASE_URL` | `https://www.sib-scania-eubp.eu` (new) | The start of every battery ID and passport ID, e.g. `https://www.sib-scania-eubp.eu/25S/UN778474481…`. Without all three, the Battery IDs page shows a red notice naming the missing ones, and every real release and create is refused (check 9). |
+| `DPP_ID_BASE_URL` | `https://www.sib-scania-eubp.eu` (new) | The start of every battery ID and passport ID, e.g. `https://www.sib-scania-eubp.eu/25S/UN778474481-…`. Without all three, the Battery IDs page shows a red notice naming the missing ones, and every real release and create is refused (check 9). |
 
 ## 4. Why the switch is one command, and the command
 
@@ -141,7 +141,7 @@ Leave out the `DEMO_USERS_PASSWORD` line if no demo accounts are wanted.
 7. **API:** a cluster token reads its batteries at `https://www.sib-scania-eubp.eu/api/external/v1/…` (the API help page's examples).
 8. **Load:** after a day, check the instance's CPU and memory in Beanstalk's Monitoring. If the instance is short of memory, a larger instance type is a cost question for the owner.
 9. **The ID settings** (on the day): signed in as the team, open the Battery IDs tab (`/admin/battery-ids`). The page shows **no red notice**. A red notice names the missing `DPP_ID_*` settings: add them in Beanstalk (Configuration, Environment properties) and check again.
-10. **A demo battery's ID** (after check 6's "Reset demo data"): on Battery IDs, All IDs, tick "Show demo IDs". A demo ID reads `UN778474481TEST…`, and `https://www.sib-scania-eubp.eu/25S/<that ID>` opens its battery's public summary once it is published, or "Not published yet" while it isn't.
+10. **A demo battery's ID** (after check 6's "Reset demo data"): on Battery IDs, All IDs, tick "Show demo IDs". A demo ID reads `UN778474481-TEST-…`, and `https://www.sib-scania-eubp.eu/25S/<that ID>` opens its battery's public summary once it is published, or "Not published yet" while it isn't.
 
 ## 7. Restore points, and rolling back to the old app
 
diff --git a/docs/end-user-testing-guide.md b/docs/end-user-testing-guide.md
index 85617b6..af8d535 100644
--- a/docs/end-user-testing-guide.md
+++ b/docs/end-user-testing-guide.md
@@ -114,20 +114,20 @@ Each step says what to do, then **Expected:** what should happen.
 8. Cluster: Change on a battery.
    **Expected:** it moves at once, and the move is in the audit log.
 9. Battery IDs tab: release VPC-12221 to VPC-12230 with "Release new IDs": quantity 10, reference "PO 4512, line 2", model Compact 7M.
-   **Expected:** the release's page opens with "Released 10 battery IDs. Export them to Excel for the labels.", and its ten IDs Available, each with its battery ID (`UN778474481VPC12221` …).
-10. The same, with VPC-12228 to VPC-12240 and quantity 12, and VPC-12401 twice in the list.
-   **Expected:** nothing is released; the Release new IDs tab lists the reasons ("that is 13 IDs, not 12", "already released", "in this release twice") and keeps what you typed.
+   **Expected:** the release's page opens with "Released 10 battery IDs. Export them to Excel for the labels.", and its ten IDs Available, each with its battery ID (`UN778474481-VPC-12221` …).
+10. The same, with VPC-12228 to VPC-12240 and quantity 12, and VPC-12401 twice and vpc-12402 in the list.
+   **Expected:** nothing is released; the Release new IDs tab lists the reasons ("that is 13 IDs, not 12", "already released", "in this release twice", "only capital letters A–Z, digits 0–9 and single dashes") and keeps what you typed.
 11. The Releases tab, then Open on PO 4512, line 2, and Export to Excel.
    **Expected:**
    - Releases lists PO 4512, line 2 and the demo data's release, tagged Demo; the counts at the top are the real IDs only;
    - an Excel file with that release's ten IDs, one row per ID: serial number, battery ID, QR code address, model, reference, released by and when, status.
 12. Create battery: a Compact 7M batch, pick VPC-12221.
-   **Expected:** the picker offers Compact 7M's IDs first, then any model's, then the demo spares; the draft shows `UN778474481VPC12221` and its QR code; the ID is Used.
+   **Expected:** the picker offers Compact 7M's IDs first, then any model's, then the demo spares; the draft shows `UN778474481-VPC-12221` and its QR code; the ID is Used.
 13. On that draft, More: Change its battery ID…, VPC-12222.
    **Expected:** the draft now has VPC-12222 and its ID; VPC-12221 is Available again.
 14. All IDs: search VPC-12230; in its row's ⋯ menu, Void…, with a reason. Then tick "Show voided", and then "Show demo IDs".
    **Expected:** it leaves the list until "Show voided" is ticked, then shows as Voided with the reason; the create picker no longer offers it; the demo IDs show only once ticked.
-15. Signed out, open `/25S/UN778474481VPC12223`, then `/25S/UN778474481VPC12230`.
+15. Signed out, open `/25S/UN778474481-VPC-12223`, then `/25S/UN778474481-VPC-12230`.
    **Expected:** "Not published yet", then "Not available".
 16. Clusters tab: Demo data, Clear demo data.
    **Expected:**
diff --git a/docs/qa-test-pack.md b/docs/qa-test-pack.md
index 14ef896..0072287 100644
--- a/docs/qa-test-pack.md
+++ b/docs/qa-test-pack.md
@@ -49,7 +49,7 @@ Keep these open while testing:
 | DEMO-CORE-0002 | demo-fleet | A draft |
 | DEMO-CORE-0003 | demo-fleet | Waste, marked for deletion |
 
-The demo batteries come from a release with the reference "Demo", on the Battery IDs tab, with four spare IDs (DEMO-SPARE-0001 to DEMO-SPARE-0004) for trying the create picker and "Change its battery ID…". With the three ID settings set (`DPP_ID_AGENCY`, `DPP_ID_COMPANY`, `DPP_ID_BASE_URL`), their IDs are marked TEST after the DUNS, such as `UN778474481TESTDEMOC7M0001`. Without them they are test IDs starting with `urn:x-battery-pass:TEST-`. Either way the pages show a "Test passport" tag.
+The demo batteries come from a release with the reference "Demo", on the Battery IDs tab, with four spare IDs (DEMO-SPARE-0001 to DEMO-SPARE-0004) for trying the create picker and "Change its battery ID…". With the three ID settings set (`DPP_ID_AGENCY`, `DPP_ID_COMPANY`, `DPP_ID_BASE_URL`), their IDs are marked TEST after the DUNS, such as `UN778474481-TEST-DEMO-C7M-0001`. Without them they are test IDs starting with `urn:x-battery-pass:TEST-`. Either way the pages show a "Test passport" tag.
 
 ## Scenario matrix
 
@@ -102,7 +102,7 @@ Scanning a released ID whose passport isn't published yet shows "Not published y
 - [ ] The two badges show on every passport page with the right status. The Battery badge never shows to the public.
 - [ ] Battery IDs: a range releases and opens the release's page with every ID Available; its "Export to Excel" opens with that release's IDs, one row per ID and its QR address.
 - [ ] The Battery IDs page opens on Releases, with the real IDs' counts at the top; All IDs shows demo IDs only with "Show demo IDs" ticked, and voided IDs only with "Show voided" ticked (or the status Voided chosen); Releases shows a release whose IDs are all voided only with it ticked, and its own page lists every ID; Void… is in a row's ⋯ menu.
-- [ ] A release with a wrong count, a repeated or already released serial number lists every reason and releases nothing.
+- [ ] A release with a wrong count, a repeated or already released serial number, or one with lower case or a double dash lists every reason and releases nothing.
 - [ ] Create battery picks a released serial number (no typing); the draft shows its battery ID and QR code at once; the ID turns Used.
 - [ ] "Change its battery ID…" on a draft takes another available ID and gives the old one back; a voided ID is never offered again.
 - [ ] Scanning a released, unpublished ID shows "Not published yet"; a voided one "Not available".
diff --git a/docs/sample-cluster-test-accounts.md b/docs/sample-cluster-test-accounts.md
index 7dcfaba..d1daee1 100644
--- a/docs/sample-cluster-test-accounts.md
+++ b/docs/sample-cluster-test-accounts.md
@@ -54,7 +54,7 @@ The team's account is not demo data: the project owner gives testers a team acco
 | DEMO-CORE-0002 | Core, Core | demo-fleet | Draft | |
 | DEMO-CORE-0003 | Core, Core | demo-fleet | Waste, marked for deletion | The marked-for-deletion notice |
 
-Drafts are seen by the team only. The batteries come from the "Demo" release on the Battery IDs tab, which also holds four spare IDs, DEMO-SPARE-0001 to DEMO-SPARE-0004. With the three ID settings their IDs are marked TEST after the DUNS (`UN778474481TESTDEMOC7M0001`); without them they start with `urn:x-battery-pass:TEST-`. The pages show a "Test passport" tag either way.
+Drafts are seen by the team only. The batteries come from the "Demo" release on the Battery IDs tab, which also holds four spare IDs, DEMO-SPARE-0001 to DEMO-SPARE-0004. With the three ID settings their IDs are marked TEST after the DUNS (`UN778474481-TEST-DEMO-C7M-0001`); without them they start with `urn:x-battery-pass:TEST-`. The pages show a "Test passport" tag either way.
 
 ## Sample files
 
diff --git a/web/Controllers/ItemIdController.cs b/web/Controllers/ItemIdController.cs
index 3308de4..ee2eaca 100644
--- a/web/Controllers/ItemIdController.cs
+++ b/web/Controllers/ItemIdController.cs
@@ -6,7 +6,7 @@ using Microsoft.AspNetCore.Mvc;
 namespace BatteryPassWeb.Controllers;
 
 /// <summary>
-/// The battery ID's own address (#6, step 6d): <c>/25S/UN778474481VPC12221</c>, the ISO/IEC 18975 web form of an
+/// The battery ID's own address (#6, step 6d): <c>/25S/UN778474481-VBC-92921</c>, the ISO/IEC 18975 web form of an
 /// ISO/IEC 15459 item ID, which the QR code carries. It opens the battery's public summary.
 /// </summary>
 [AllowAnonymous]
diff --git a/web/Controllers/PassportController.cs b/web/Controllers/PassportController.cs
index 535a2f8..a92f9fd 100644
--- a/web/Controllers/PassportController.cs
+++ b/web/Controllers/PassportController.cs
@@ -65,7 +65,7 @@ public sealed class PassportController(DppReaders readers, PassportPages pages)
         return view;
     }
 
-    /// <summary>A version's own address (#5, step 6d): <c>/dpp/UN778474481VPC12221/v2</c>.</summary>
+    /// <summary>A version's own address (#5, step 6d): <c>/dpp/UN778474481-VBC-92921/v2</c>.</summary>
     [HttpGet("{id}/v{version:int}")]
     public Task<IActionResult> Version(string id, int version, [FromServices] Microsoft.Extensions.Options.IOptions<BatteryPassWeb.Configuration.BatteryPassOptions> options, CancellationToken cancellationToken) =>
         Summary(BatteryPassWeb.Dpp.Identifiers.IdentifierForms.PassportAddress(options.Value.IdBaseUrl, Uri.UnescapeDataString(id), version), cancellationToken);
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
index a0b0cb1..e33a8e1 100644
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -109,7 +109,7 @@ public sealed class PackCreationService(
         }
 
         // #6 (step 6d): a battery takes a released ID, picked in one step so that no one else can; a battery's new passport
-        // makes its own from its serial number (R1, R2, …), as does a battery made without the pool (tests, scripts).
+        // continues its first one's (-R1, -R2, …), and a battery made without the pool (tests, scripts) makes its own.
         if (request.FromPool && request.PredecessorPackId is null && pool is not null)
         {
             // Never a real released ID while the ID settings are missing: the battery would keep a test ID (6d-2b).
diff --git a/web/Dpp/Identifiers/BatteryIdPool.cs b/web/Dpp/Identifiers/BatteryIdPool.cs
index e594708..30f019f 100644
--- a/web/Dpp/Identifiers/BatteryIdPool.cs
+++ b/web/Dpp/Identifiers/BatteryIdPool.cs
@@ -126,6 +126,12 @@ public sealed partial class BatteryIdPool(
         var seen = new Dictionary<string, string>(StringComparer.Ordinal);
         foreach (var serial in serials)
         {
+            if (!IdentifierForms.IsSerial(serial)) // whatever the ID scheme (6d-5)
+            {
+                problems.Add(new IdProblem(serial, IdentifierForms.SerialRule));
+                continue;
+            }
+
             var made = identifiers.ItemFor(serial, request.IsDemo);
             if (made.Identifier is not { } identifier)
             {
@@ -133,7 +139,7 @@ public sealed partial class BatteryIdPool(
                 continue;
             }
 
-            var key = IdentifierForms.Clean(serial);
+            var key = IdentifierForms.Clean(serial); // without its dashes: VBC-92921 and VBC92921 are one (6d-5)
             if (seen.TryGetValue(key, out var first))
             {
                 problems.Add(new IdProblem(serial, first == serial ? "it is in this release twice" : $"it is in this release twice, also as {first}"));
@@ -561,7 +567,7 @@ public sealed partial class BatteryIdPool(
 
             if (Text(pack, "uniqueProductIdentifier") is { } identifier && IdentifierForms.Short(identifier) is var item && item != identifier)
             {
-                byKey.TryAdd(item, name);
+                byKey.TryAdd(IdentifierForms.Clean(item), name); // by its letters and digits too, as the keys are
             }
         }
 
diff --git a/web/Dpp/Identifiers/DunsIdentifierScheme.cs b/web/Dpp/Identifiers/DunsIdentifierScheme.cs
index c2cce40..029e208 100644
--- a/web/Dpp/Identifiers/DunsIdentifierScheme.cs
+++ b/web/Dpp/Identifiers/DunsIdentifierScheme.cs
@@ -6,73 +6,77 @@ using Microsoft.Extensions.Options;
 namespace BatteryPassWeb.Dpp.Identifiers;
 
 /// <summary>
-/// The battery ID chosen by the product owner (O-1 and 6d, 5 Nov 2026): an ISO/IEC 15459 unique item ID under issuing
-/// agency code <c>UN</c> (Dun &amp; Bradstreet), the company's DUNS, and the battery's serial number (#11) with only its
-/// letters and digits, upper-cased, written as a web address per ISO/IEC 18975 with the MH10.8.2 data identifier
-/// <c>25S</c>: serial <c>VPC-12221</c> gives <c>https://www.sib-scania-eubp.eu/25S/UN778474481VPC12221</c>. A battery
-/// gets it when it is created; a new passport of the same battery (a successor) adds <c>R1</c>, <c>R2</c>, …, the
-/// next free one. A demo battery's is marked <c>TEST</c> after the DUNS. A version's passport ID (#5) is
-/// <c>…/dpp/UN778474481VPC12221/v1</c>.
+/// The battery ID chosen by the product owner (O-1 and 6d, 5 Nov 2026; the dash, 6d-5, 9 Nov 2026): an ISO/IEC 15459
+/// unique item ID under issuing agency code <c>UN</c> (Dun &amp; Bradstreet), the company's DUNS, one dash and the
+/// battery's serial number (#11) exactly as written (capital letters, digits and single dashes), as a web address per
+/// ISO/IEC 18975 with the MH10.8.2 data identifier <c>25S</c>: serial <c>VBC-92921</c> gives
+/// <c>https://www.sib-scania-eubp.eu/25S/UN778474481-VBC-92921</c>. The dash is unambiguous: <c>UN778474481-</c> is always
+/// the first 12 characters. A battery gets it when it is created; a new passport of the same battery (a successor) adds
+/// <c>-R1</c>, <c>-R2</c>, …, the next free one. A demo battery's is marked <c>-TEST-</c> after the DUNS. A version's
+/// passport ID (#5) is <c>…/dpp/UN778474481-VBC-92921/v1</c>. IDs made before 6d-5 (<c>UN778474481VPC12221</c>) stay as
+/// they are and are still read; their successors continue them (<c>…R1</c>).
 /// </summary>
 public sealed class DunsIdentifierScheme(IOptions<BatteryPassOptions> options, DppStore store) : IIdentifierScheme
 {
     /// <summary>ISO/IEC 15459: an item ID has at most 50 characters, agency code included.</summary>
     public const int MaxItemLength = 50;
 
-    /// <summary>Room kept for a successor's <c>R99</c>, so a battery that gets a first ID can always get its next ones.</summary>
-    public const int SuccessorRoom = 3;
+    /// <summary>
+    /// Room kept for a successor's <c>-R99</c>, so a battery that gets a first ID can always get its next ones: a real
+    /// serial number has at most 50 − 12 (<c>UN778474481-</c>) − 4 = 34 characters, a demo one 29 (6d-5).
+    /// </summary>
+    public const int SuccessorRoom = 4;
 
     public const int MaxSuccessors = 99;
 
+    /// <summary>The test scheme's only refusal: a serial number with no letters or digits.</summary>
     public const string NoLettersOrDigits = "the serial number has no letters or digits, so it can't make a battery ID";
 
     public const string StartsWithTest = "a real battery's serial number can't start with TEST, which marks demo batteries' IDs";
 
     public const string TooManySuccessors = "this battery already has 99 new passports, so it can't get another battery ID";
 
-    public static string TooLong(int letters) => $"the serial number is too long to make a battery ID: at most {letters} letters and digits count";
+    public static string TooLong(int characters) => $"the serial number is too long to make a battery ID: at most {characters} characters, dashes included";
 
     public static string Taken(string item, string serial) =>
-        $"the battery ID {item} is already used by battery {serial}. Serial numbers that differ only in dashes, spaces or capitals give the same ID";
+        $"the battery ID {item} is already used by battery {serial}. Serial numbers that differ only in their dashes can't both have one";
 
-    private string Prefix => options.Value.IdAgency + options.Value.IdCompany;
+    /// <summary>The fixed start of every battery ID: agency, company and one dash (<c>UN778474481-</c>).</summary>
+    private string Prefix => $"{options.Value.IdAgency}{options.Value.IdCompany}-";
 
     public async Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(pack);
+
+        // A new passport continues its battery's first ID as it is (6d-5): an old-form one too, whatever its serial number.
+        if (pack.SuccessorOf is not null && await FirstIdentifierAsync(pack, cancellationToken) is { } first)
+        {
+            return await NextSuccessorAsync(first, IdentifierForms.IsDashed(IdentifierForms.Short(first)) ? "-" : string.Empty, cancellationToken);
+        }
+
         var made = ItemFor(SerialNumber(pack), pack.IsDemo);
         if (made.Identifier is not { } address)
         {
             return made;
         }
 
-        var item = IdentifierForms.Short(address);
-        if (pack.SuccessorOf is null)
+        if (pack.SuccessorOf is not null)
         {
-            return await store.FindPackByUniqueProductIdentifierAsync(address, cancellationToken) is { } other && other.Id != pack.Id
-                ? NewIdentifier.Refused(Taken(item, SerialNumber(other)))
-                : new NewIdentifier(address, null);
+            return await NextSuccessorAsync(address, "-", cancellationToken); // its first battery has no web-form ID (a test ID)
         }
 
-        var used = await store.FindPackIdentifiersWithSuffixesAsync(address, cancellationToken);
-        for (var number = 1; number <= MaxSuccessors; number++)
-        {
-            var next = $"{address}R{number}";
-            if (!used.Contains(next))
-            {
-                return new NewIdentifier(next, null);
-            }
-        }
-
-        return NewIdentifier.Refused(TooManySuccessors);
+        // Serial numbers that differ only in their dashes can't both have an ID (6d-5), nor an ID made before 6d-5.
+        return await store.FindPackByItemIgnoringDashesAsync(IdentifierForms.Short(address), cancellationToken) is { } other && other.Id != pack.Id
+            ? NewIdentifier.Refused(Taken(IdentifierForms.Short(Identifier(other) ?? address), SerialNumber(other)))
+            : new NewIdentifier(address, null);
     }
 
     public NewIdentifier ItemFor(string serialNumber, bool demo)
     {
-        var serial = IdentifierForms.Clean(serialNumber);
-        if (serial.Length == 0)
+        var serial = serialNumber ?? string.Empty; // as written: never upper-cased or cleaned (6d-5)
+        if (!IdentifierForms.IsSerial(serial))
         {
-            return NewIdentifier.Refused(NoLettersOrDigits);
+            return NewIdentifier.Refused(IdentifierForms.SerialRule);
         }
 
         if (!demo && serial.StartsWith(IdentifierForms.TestMarker, StringComparison.Ordinal))
@@ -80,7 +84,7 @@ public sealed class DunsIdentifierScheme(IOptions<BatteryPassOptions> options, D
             return NewIdentifier.Refused(StartsWithTest);
         }
 
-        var head = demo ? Prefix + IdentifierForms.TestMarker : Prefix;
+        var head = demo ? $"{Prefix}{IdentifierForms.TestMarker}-" : Prefix;
         return head.Length + serial.Length > MaxItemLength - SuccessorRoom
             ? NewIdentifier.Refused(TooLong(MaxItemLength - SuccessorRoom - head.Length))
             : new NewIdentifier(IdentifierForms.ItemAddress(options.Value.IdBaseUrl, head + serial), null);
@@ -91,8 +95,45 @@ public sealed class DunsIdentifierScheme(IOptions<BatteryPassOptions> options, D
 
     public bool IsTestIdentifier(string identifier) => IdentifierForms.IsTest(identifier);
 
-    private static string SerialNumber(BatteryPack pack) =>
-        pack.Values.GetValueOrDefault("batterySerialNumber")?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
+    private static string SerialNumber(BatteryPack pack) => Text(pack, "batterySerialNumber") ?? string.Empty;
+
+    private static string? Identifier(BatteryPack pack) => Text(pack, "uniqueProductIdentifier");
+
+    private static string? Text(BatteryPack pack, string key) =>
+        pack.Values.GetValueOrDefault(key)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
+
+    /// <summary>The web-form battery ID of the first battery in <paramref name="pack"/>'s chain of passports; null when it has none.</summary>
+    private async Task<string?> FirstIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken)
+    {
+        var current = pack;
+        for (var step = 0; step <= MaxSuccessors && current.SuccessorOf is { } previous; step++)
+        {
+            if (await store.GetAsync<BatteryPack>(previous, cancellationToken) is not { } before)
+            {
+                return null;
+            }
+
+            current = before;
+        }
+
+        return current.SuccessorOf is null && Identifier(current) is { } first && IdentifierForms.Short(first) != first ? first : null;
+    }
+
+    /// <summary>The first ID with a free R-number after <paramref name="first"/>: <c>…-R1</c>, <c>…-R2</c>, … (<c>…R1</c> before 6d-5).</summary>
+    private async Task<NewIdentifier> NextSuccessorAsync(string first, string dash, CancellationToken cancellationToken)
+    {
+        var used = await store.FindPackIdentifiersWithSuffixesAsync(first, cancellationToken);
+        for (var number = 1; number <= MaxSuccessors; number++)
+        {
+            var next = $"{first}{dash}R{number}";
+            if (!used.Contains(next))
+            {
+                return new NewIdentifier(next, null);
+            }
+        }
+
+        return NewIdentifier.Refused(TooManySuccessors);
+    }
 }
 
 /// <summary>The identifiers' forms, shared by every scheme and by the pages' addresses.</summary>
@@ -103,6 +144,13 @@ public static partial class IdentifierForms
 
     public const string TestMarker = "TEST";
 
+    /// <summary>
+    /// What a serial number may hold to make a battery ID (the owner, 6d-5): <c>^[A-Z0-9]+(-[A-Z0-9]+)*$</c>. Anything else
+    /// is refused at release, never upper-cased or cleaned silently.
+    /// </summary>
+    public const string SerialRule =
+        "a serial number may hold only capital letters A–Z, digits 0–9 and single dashes between them, such as VBC-92921: no lower case, spaces or other characters, and no dash at the start, at the end or next to another";
+
     /// <summary>
     /// The ID settings that aren't set or aren't valid (6d-2b): DPP_ID_AGENCY (two or three capital letters),
     /// DPP_ID_COMPANY (nine digits) and DPP_ID_BASE_URL (an https address). Empty when the scheme is complete.
@@ -137,13 +185,13 @@ public static partial class IdentifierForms
     public static bool IsConfigured(BatteryPassOptions options) =>
         AgencyCode().IsMatch(options.IdAgency) && CompanyCode().IsMatch(options.IdCompany) && Uri.TryCreate(options.IdBaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
 
-    /// <summary><c>https://…/25S/UN778474481VPC12221</c>: the battery ID (#6) as a web address.</summary>
+    /// <summary><c>https://…/25S/UN778474481-VBC-92921</c>: the battery ID (#6) as a web address.</summary>
     public static string ItemAddress(string baseUrl, string item) => $"{baseUrl.Trim().TrimEnd('/')}/{DataIdentifier}/{item}";
 
-    /// <summary><c>https://…/dpp/UN778474481VPC12221/v1</c>: a version's passport ID (#5).</summary>
+    /// <summary><c>https://…/dpp/UN778474481-VBC-92921/v1</c>: a version's passport ID (#5).</summary>
     public static string PassportAddress(string baseUrl, string item, int version) => $"{baseUrl.Trim().TrimEnd('/')}/dpp/{item}/v{version}";
 
-    /// <summary>The item ID inside a web-form battery ID ("UN778474481VPC12221"); anything else as it is.</summary>
+    /// <summary>The item ID inside a web-form battery ID ("UN778474481-VBC-92921"); anything else as it is.</summary>
     public static string Short(string identifier)
     {
         var match = ItemInAddress().Match(identifier ?? string.Empty);
@@ -151,8 +199,8 @@ public static partial class IdentifierForms
     }
 
     /// <summary>
-    /// What a page shows of an ID (the owner, 6d): the short item for a battery ID ("UN778474481VPC12221"), the item and
-    /// version for a passport ID ("UN778474481VPC12221/v2"); anything else as it is. The full address is the link.
+    /// What a page shows of an ID (the owner, 6d): the short item for a battery ID ("UN778474481-VBC-92921"), the item and
+    /// version for a passport ID ("UN778474481-VBC-92921/v2"); anything else as it is. The full address is the link.
     /// </summary>
     public static string Display(string id) => IsWebAddress(id) ? PagePath(id) : id;
 
@@ -178,10 +226,22 @@ public static partial class IdentifierForms
         return Uri.EscapeDataString(id).Replace("%3A", ":", StringComparison.Ordinal);
     }
 
-    /// <summary>True for a short item ID a reader may type or scan ("UN778474481VPC12221").</summary>
+    /// <summary>
+    /// True for a short item ID a reader may type or scan: "UN778474481-VBC-92921", or one made before 6d-5
+    /// ("UN778474481VPC12221"). At most 50 characters (ISO/IEC 15459).
+    /// </summary>
     public static bool IsItem(string text) => text is { Length: <= DunsIdentifierScheme.MaxItemLength } && ItemId().IsMatch(text);
 
-    /// <summary>A serial number's part of a battery ID: only its letters A–Z and digits 0–9, upper-cased ("VPC-12221" → "VPC12221").</summary>
+    /// <summary>True for a serial number that may make a battery ID: capital letters, digits and single dashes (<see cref="SerialRule"/>).</summary>
+    public static bool IsSerial(string? serialNumber) => serialNumber is not null && Serial().IsMatch(serialNumber);
+
+    /// <summary>True for an item ID in the 6d-5 form, with a dash after the company; false for one made before.</summary>
+    public static bool IsDashed(string item) => Dashed().IsMatch(item ?? string.Empty);
+
+    /// <summary>
+    /// A serial number's key, only its letters A–Z and digits 0–9, upper-cased ("VBC-92921" → "VBC92921"): the released
+    /// IDs' key, so that serial numbers which differ only in their dashes are one (6d-5), and what searches compare.
+    /// </summary>
     public static string Clean(string? serialNumber) =>
         new((serialNumber ?? string.Empty).ToUpperInvariant().Where(letter => letter is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
 
@@ -214,16 +274,26 @@ public static partial class IdentifierForms
     [GeneratedRegex("^[0-9]{9}$")]
     private static partial Regex CompanyCode();
 
-    // ISO/IEC 15459: upper-case letters and digits; agency + company + serial (at most 50 characters, checked in IsItem).
-    [GeneratedRegex("^[A-Z]{2,3}[0-9]{9}[A-Z0-9]{1,39}$")]
+    // ISO/IEC 15459: agency + company, a dash (6d-5; none before) and the serial: capital letters and digits in groups
+    // joined by single dashes. At most 50 characters (checked in IsItem, and by the look-aheads in an address).
+    private const string Item = "[A-Z]{2,3}[0-9]{9}-?[A-Z0-9]+(?:-[A-Z0-9]+)*";
+
+    [GeneratedRegex("^" + Item + "$")]
     private static partial Regex ItemId();
 
-    [GeneratedRegex("^https://[^/]+/25S/(?<item>[A-Z]{2,3}[0-9]{9}[A-Z0-9]{1,39})$")]
+    [GeneratedRegex("^https://[^/]+/25S/(?<item>(?=[A-Z0-9-]{1,50}$)" + Item + ")$")]
     private static partial Regex ItemInAddress();
 
-    [GeneratedRegex("^https://[^/]+/dpp/(?<item>[A-Z]{2,3}[0-9]{9}[A-Z0-9]{1,39})/v(?<version>[0-9]+)$")]
+    [GeneratedRegex("^https://[^/]+/dpp/(?<item>(?=[A-Z0-9-]{1,50}/)" + Item + ")/v(?<version>[0-9]+)$")]
     private static partial Regex VersionInAddress();
 
-    [GeneratedRegex("[A-Z]{2,3}[0-9]{9}TEST[A-Z0-9]*(/v[0-9]+)?$")]
+    // A demo ID's mark, right after the company: -TEST- (6d-5) or TEST (before).
+    [GeneratedRegex("(?:^|/)[A-Z]{2,3}[0-9]{9}-?TEST[A-Z0-9-]*(?:/v[0-9]+)?$")]
     private static partial Regex TestItem();
+
+    [GeneratedRegex("^[A-Z0-9]+(?:-[A-Z0-9]+)*$")]
+    private static partial Regex Serial();
+
+    [GeneratedRegex("^[A-Z]{2,3}[0-9]{9}-")]
+    private static partial Regex Dashed();
 }
diff --git a/web/Dpp/Identifiers/TestIdentifierScheme.cs b/web/Dpp/Identifiers/TestIdentifierScheme.cs
index 5ff7869..be7a26c 100644
--- a/web/Dpp/Identifiers/TestIdentifierScheme.cs
+++ b/web/Dpp/Identifiers/TestIdentifierScheme.cs
@@ -19,7 +19,7 @@ public interface IIdentifierScheme
     Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default);
 
     /// <summary>
-    /// The battery ID a serial number makes, checked on its own (its letters and digits, its length, TEST). Whether another
+    /// The battery ID a serial number makes, checked on its own (the serial rule, its length, TEST). Whether another
     /// battery has it is the caller's check.
     /// </summary>
     NewIdentifier ItemFor(string serialNumber, bool demo);
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index 557359a..673b6b4 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -407,7 +407,7 @@ public sealed class DppStore
 
     /// <summary>
     /// The pack with this unique product identifier (#6), if any: as stored, or by its short item ID
-    /// ("UN778474481VPC12221") for a battery ID written as a web address (step 6d).
+    /// ("UN778474481-VBC-92921", or "UN778474481VPC12221" before 6d-5) for a battery ID written as a web address (step 6d).
     /// </summary>
     public async Task<BatteryPack?> FindPackByUniqueProductIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
     {
@@ -425,13 +425,29 @@ public sealed class DppStore
         return await Collection<BatteryPack>().Find(filter).FirstOrDefaultAsync(cancellationToken);
     }
 
+    /// <summary>
+    /// The pack whose web-form battery ID is <paramref name="item"/> but for its dashes, if any (6d-5): serial numbers
+    /// that differ only in their dashes can't both have one, and an ID made before 6d-5 (no dashes) counts too.
+    /// </summary>
+    public async Task<BatteryPack?> FindPackByItemIgnoringDashesAsync(string item, CancellationToken cancellationToken = default)
+    {
+        var key = Identifiers.IdentifierForms.Clean(item); // only A–Z and 0–9: nothing to escape
+        if (_database is null || key.Length == 0)
+        {
+            return null;
+        }
+
+        var pattern = $"/{Identifiers.IdentifierForms.DataIdentifier}/{string.Join("-?", key.Select(letter => letter.ToString()))}$";
+        return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression(pattern, string.Empty))).FirstOrDefaultAsync(cancellationToken);
+    }
+
     /// <summary>
     /// The battery IDs (#6) that are <paramref name="address"/> or <paramref name="address"/> with a successor's R-number
-    /// (<c>…R1</c>, <c>…R2</c>), for picking the next free one (step 6d).
+    /// (<c>…-R1</c>, <c>…-R2</c>; <c>…R1</c> before 6d-5), for picking the next free one (step 6d).
     /// </summary>
     public async Task<IReadOnlySet<string>> FindPackIdentifiersWithSuffixesAsync(string address, CancellationToken cancellationToken = default)
     {
-        var filter = Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression($"^{Regex.Escape(address)}(R[0-9]+)?$", string.Empty));
+        var filter = Builders<BatteryPack>.Filter.Regex(UniqueProductIdentifierField, new BsonRegularExpression($"^{Regex.Escape(address)}(-?R[0-9]+)?$", string.Empty));
         var packs = await Collection<BatteryPack>().Find(filter).ToListAsync(cancellationToken);
         return packs.Select(pack => pack.Values["uniqueProductIdentifier"].Value!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
     }
diff --git a/web/Views/BatteryIds/Index.cshtml b/web/Views/BatteryIds/Index.cshtml
index 21ccfa8..9dfdbf3 100644
--- a/web/Views/BatteryIds/Index.cshtml
+++ b/web/Views/BatteryIds/Index.cshtml
@@ -238,7 +238,7 @@
                     <label class="bp-release-list">
                         <span>Individual serial numbers <span class="bp-attr-optional">(optional)</span></span>
                         <textarea name="list" rows="4" placeholder="One per line, e.g.&#10;VPC-12301&#10;VPC-12305">@Model.Form.List</textarea>
-                        <small class="bp-field-help">Pasted from a list, one per line. Use them with ranges or on their own.</small>
+                        <small class="bp-field-help">Pasted from a list, one per line. Use them with ranges or on their own. Capital letters, digits and single dashes only.</small>
                     </label>
 
                     <div class="bp-release-actions">
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index c77af2d..49be7f7 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -167,10 +167,10 @@
 
                 <section class="bp-card bp-help-part" id="battery-ids">
                     <h2>Battery IDs <span class="bp-soft-tag">Team</span></h2>
-                    <p>A battery's ID (#6) is the company's DUNS and its serial number, such as <code>UN778474481VPC12221</code>. The team releases serial numbers in advance on the <a href="/admin/battery-ids">Battery IDs</a> tab, and each battery is created from one. At the top, the real IDs by status (demo IDs aren't counted) and "Release new IDs"; below, three tabs: Releases, All IDs and Release new IDs.</p>
+                    <p>A battery's ID (#6) is the company's DUNS, a dash and its serial number as written, such as <code>UN778474481-VPC-12221</code>. The team releases serial numbers in advance on the <a href="/admin/battery-ids">Battery IDs</a> tab, and each battery is created from one. At the top, the real IDs by status (demo IDs aren't counted) and "Release new IDs"; below, three tabs: Releases, All IDs and Release new IDs.</p>
                     <dl class="bp-word-list">
                         <dt>Release new IDs</dt>
-                        <dd>Give a reference, such as the purchase order, and optionally the model. Add one or more ranges (start, end and quantity) or paste serial numbers, one per line. It is all or nothing: if one can't be released, none is, and each reason is listed. Once released, the app opens the release's page.</dd>
+                        <dd>Give a reference, such as the purchase order, and optionally the model. Add one or more ranges (start, end and quantity) or paste serial numbers, one per line. A serial number holds capital letters, digits and single dashes only, at most 34 characters; one that differs from another only in its dashes is refused. It is all or nothing: if one can't be released, none is, and each reason is listed. Once released, the app opens the release's page.</dd>
                         <dt>Releases</dt>
                         <dd>The first tab: one row per release, with its reference, model, date and counts. "Open" shows the release's page: its IDs, "Export to Excel" at the top and "Void unused…". The demo data's release is tagged Demo. A release whose IDs are all voided shows only with "Show voided" ticked; its page still lists every ID.</dd>
                         <dt>All IDs</dt>
@@ -186,7 +186,7 @@
                         <dt>Change its battery ID…</dt>
                         <dd>On a draft's page, under More: it takes another available ID, and its old one is available again. Removing a draft gives its ID back too.</dd>
                         <dt>A new passport</dt>
-                        <dd>The battery keeps its serial number, and its new passport's battery ID adds R1, R2, …; it isn't released.</dd>
+                        <dd>The battery keeps its serial number, and its new passport's battery ID adds -R1, -R2, …; it isn't released.</dd>
                     </dl>
                 </section>
 
diff --git a/web/wwwroot/css/bp-battery-ids.css b/web/wwwroot/css/bp-battery-ids.css
index 22e9135..d618b38 100644
--- a/web/wwwroot/css/bp-battery-ids.css
+++ b/web/wwwroot/css/bp-battery-ids.css
@@ -107,6 +107,12 @@
   background: transparent;
 }
 
+/* A reason may be long (the serial rule, 6d-5): it wraps rather than widening the page. */
+.bp-problems-table td:last-child {
+  min-width: 16rem;
+  white-space: normal;
+}
+
 .bp-release-filter {
   display: grid;
   grid-template-columns: repeat(3, minmax(0, 10rem)) minmax(0, 1fr) auto;
```

- [ ] **Step 4: See the tests pass**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~DunsIdentifierTests|FullyQualifiedName~TestIdentifierSchemeTests|FullyQualifiedName~BatteryIdPoolTests|FullyQualifiedName~BatteryIdsHelpTests|FullyQualifiedName~BatteryIdSettingsAndPagesTests|FullyQualifiedName~BatteryIdsVoidedTests"`
Expected: `Passed!  - Failed:     0, Passed:   116`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteryIdPoolTests.cs BatteryPassWeb.Tests/Dpp/DunsIdentifierTests.cs BatteryPassWeb.Tests/Dpp/TestIdentifierSchemeTests.cs BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs docs/aws-v3-cutover.md docs/end-user-testing-guide.md docs/qa-test-pack.md docs/sample-cluster-test-accounts.md web/Controllers/ItemIdController.cs web/Controllers/PassportController.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Identifiers/BatteryIdPool.cs web/Dpp/Identifiers/DunsIdentifierScheme.cs web/Dpp/Identifiers/TestIdentifierScheme.cs web/Dpp/Store/DppStore.cs web/Views/BatteryIds/Index.cshtml web/Views/Help/Guide.cshtml web/wwwroot/css/bp-battery-ids.css
git commit -m "feat(dpp): the battery ID keeps the serial as written after one dash (UN778474481-VBC-92921); serial rule, near-duplicates, old IDs still read" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2653, Skipped:     0, Total:  2653`. That is the 2620 from before plus 33 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat e9d5a55 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.
