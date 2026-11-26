# DPP V3 Step 6d-3: The Help and Test Documents for Battery IDs — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The last of 6d's parts, documents and their tests only. It brings the Help and the test documents up to date with 6d-2's battery IDs released in advance, as 6d-2b laid out the page: the three tabs, a page per release, and the red notice when the ID settings are missing. The role check was re-run on that page; the two faults its first run found are already fixed in 6d-2b.

1. **The Help (`/help`):**
   - **For everyone**, "Find a passport": a label scanned before its battery's passport is published says "Not published yet".
   - **For the team:**
     - the workflow's new first step after product data, "Release battery IDs", and "Create the battery" now picks the serial number from the released IDs;
     - a new part, **Battery IDs**, in the contents:
       - the page: the real IDs' counts and "Release new IDs" at the top, then the tabs Releases, All IDs and Release new IDs;
       - Release new IDs (all or nothing; the app then opens the release's page);
       - Releases (Open, a release's page with its export at the top, the Demo tag) and All IDs (search, filters, "Show demo IDs");
       - Export to Excel (a release's IDs, or All IDs' filtered ones), the statuses, Void… (in a row's ⋯ menu) and Void unused…;
       - the red notice: what the three settings are, why nothing can be released without them, and that the demo data still works;
       - Change its battery ID… (and a removed draft giving its ID back), and a new passport's R1, R2, ….
2. **The test pack (`docs/qa-test-pack.md`):**
   - the team's row names releasing battery IDs;
   - the demo batteries come from the "Demo" release with four spares, and their IDs are marked TEST after the DUNS with the three settings;
   - the scenario matrix gets a "Battery IDs tab" column (the team only; "Not allowed" for every other reader, sign-in for visitors), and a line on scanning;
   - the battery page actions and the API table name releasing, voiding and creating from a released serial number;
   - seven acceptance checks: release to the release's page and its export; the page's tabs, demo IDs and row menu; a refused release; the picker; the swap and void; the scan pages; and the red notice with a setting missing;
   - the old "test identifiers until plan 6d" limitation becomes "real IDs need the settings", with the red notice.
3. **The per-role script (`docs/end-user-testing-guide.md`):**
   - the team's steps 9–15: release (landing on its page), a refused release (on its tab), Releases and a release's export, create from a picked ID, swap, void from the row menu and "Show demo IDs", and scan;
   - the demo data clear keeps real released IDs;
   - the API's create names a released serial (`DEMO-SPARE-0001`).
4. **The demo accounts' page (`docs/sample-cluster-test-accounts.md`):** the "Demo" release, its spares and the demo IDs' form.

**Architecture:**
- `web/Views/Help/Guide.cshtml`: the "find" and "workflow" parts, and a new team part `id="battery-ids"` between the workflow and "After publishing".
- `web/Views/Shared/_HelpContents.cshtml`: its link under "For the team".
- Three documents: `docs/qa-test-pack.md`, `docs/end-user-testing-guide.md`, `docs/sample-cluster-test-accounts.md`.
- No code changes: the page, its tabs and the notice are 6d-2b's.
- **`site.css` is not changed.**
- **Tests:** new `BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs`. It checks:
  - the team part, under `isTeam` and in the contents, with the tabs, the release's page, "Show demo IDs", the row menu and the red notice's settings;
  - the workflow's order and "pick", and the scan line for everyone;
  - the test pack's column for all eleven readers and its checks (the release's page, the tabs, the red notice), the script's steps (landing on the release's page, the row menu) and the API's released serial;
  - the spares the documents name, which are `DemoDataService.SpareSerials`.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, xUnit 2.9. Nothing new is downloaded.

**Spec:** `step6d-id-release-design.md` in the managing session's notes ("Help and test pack": the team's Help gets the release flow, the test pack and the scenario matrix get the new page, and the role check is re-run), with the page as 6d-2b laid it out (the approved screens in `step6d2b-shots/`).

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at 045648f (plan 6d-2b's commit, "feat(dpp): missing battery ID settings refused…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2611` (plan 6d-2b's result). After this plan: 2615.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6d3-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## The role check, re-run (6d-3)

The 6c role check, with 6d's checks (`walk6d3b.mjs`; script and log in the managing session's notes, `step6d3b/`), was run on a scratch app at 6d-2b plus this plan, with the three ID settings, after "Reset demo data". It covers every reader of design 5.2, at 1280 and 375, with every window at 1280, 375, and 150 % in Edge and Chrome, plus the API and the changes that alter data.

- **What it checks for battery IDs:**
  - the team's Battery IDs page at both widths: Releases, All IDs (with and without "Show demo IDs"), Release new IDs, and the demo release's page;
  - "Not allowed" for `/admin/battery-ids`, its export and a release's page, for every other signed-in reader (sign-in for visitors);
  - the scan of a released, unpublished ID ("Not published yet", a 404, no battery data) and of a published one (its page);
  - the windows "Change its battery ID…", "Void…" (opened from a row's ⋯ menu, which then closes), and "Void unused…" (from Releases and from a release's page);
  - the API's create from a released spare (201, its TEST ID) and from an unreleased serial number (400, refused).
- **Before 6d-2b**, its first run found two faults (a web-address ID ran off a phone's screen, and the API missed an escaped ID); 6d-2b fixed both.
- **This run: 1043 PASS, 0 FAIL** (pages 762, windows 246, API 18, data changes 17).

**Measured on a clean clone (045648f):**
- the tests first fail: `Failed!  - Failed:     3, Passed:     1`: the Battery IDs part, the workflow and the test pack don't describe the release flow yet (the spares test already passes);
- with the changes, Task 1's own class passes: 4 tests in the class this plan adds;
- the whole suite: `Failed: 2, Passed: 2613, Total: 2615` (the clean clone's two known `ApplicationAuditCoverageTests` failures, `site.css` with CRLF line endings, pass in the worktree).

### Task 1: The Help and test documents

**Files:**
- Create: `BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs`
- Modify (Help and docs): `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md`, `docs/sample-cluster-test-accounts.md`, `web/Views/Help/Guide.cshtml`, `web/Views/Shared/_HelpContents.cshtml`

- [ ] **Step 1: Write the failing tests**

Save as `../6d3-test.patch` and run `git apply ../6d3-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs b/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
new file mode 100644
index 0000000..3261c52
--- /dev/null
+++ b/BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs
@@ -0,0 +1,71 @@
+using BatteryPassWeb.Tests.Dpp;
+
+namespace BatteryPassWeb.Tests.Look;
+
+/// <summary>
+/// Step 6d-3: the Help and the test documents for battery IDs released in advance (6d-2, in tabs since 6d-2b): the
+/// team's own part (the tabs, a release's page, the red notice), the workflow's new first step, a label scanned too
+/// early, and the test pack, script and accounts page brought up to date.
+/// </summary>
+public sealed class BatteryIdsHelpTests
+{
+    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n");
+
+    private static readonly string Guide = Read("web", "Views", "Help", "Guide.cshtml");
+
+    private static string Part(string id)
+    {
+        var start = Guide.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
+        Assert.True(start >= 0, $"no part {id}");
+        return Guide[start..Guide.IndexOf("</section>", start, StringComparison.Ordinal)];
+    }
+
+    [Fact]
+    public void TheTeam_ShouldHaveAPartOnBatteryIds_InItsContents()
+    {
+        var part = Part("battery-ids");
+
+        Assert.True(Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Guide.IndexOf("id=\"battery-ids\"", StringComparison.Ordinal)); // the team's only
+        Assert.Contains("<a href=\"@Part(\"battery-ids\")\">Battery IDs</a>", Read("web", "Views", "Shared", "_HelpContents.cshtml"), StringComparison.Ordinal);
+        foreach (var words in new[] { "href=\"/admin/battery-ids\"", "all or nothing", "the app opens the release's page", "Releases", "All IDs", "Show demo IDs", "⋯ menu", "Export to Excel", "Available, Used", "Voided", "Void unused…", "Change its battery ID…", "Removing a draft gives its ID back", "R1, R2", "The battery ID settings aren't set", "DPP_ID_BASE_URL", "The demo data works without them" })
+        {
+            Assert.Contains(words, part, StringComparison.Ordinal);
+        }
+    }
+
+    [Fact]
+    public void TheWorkflow_ShouldReleaseFirst_AndPickTheSerialNumber()
+    {
+        var workflow = Part("workflow");
+
+        Assert.True(workflow.IndexOf("Release battery IDs", StringComparison.Ordinal) < workflow.IndexOf("Create the battery", StringComparison.Ordinal));
+        Assert.Contains("pick its serial number from the released IDs", workflow, StringComparison.Ordinal);
+        Assert.DoesNotContain("type its serial number", workflow, StringComparison.Ordinal);
+        Assert.Contains("A label scanned before its battery's passport is published says \"Not published yet\".", Part("find"), StringComparison.Ordinal); // for everyone
+    }
+
+    [Fact]
+    public void TheTestPack_ShouldCoverTheBatteryIdsTab()
+    {
+        var pack = Read("docs", "qa-test-pack.md");
+        var script = Read("docs", "end-user-testing-guide.md");
+
+        Assert.Contains("| Mark as invalid | Battery IDs tab |", pack, StringComparison.Ordinal); // a column for every reader
+        Assert.Equal(11, pack.Split('\n').Count(line => line.StartsWith('|') && (line.EndsWith("| – (Not allowed) |", StringComparison.Ordinal) || line.EndsWith("| – (sign-in) |", StringComparison.Ordinal) || line.EndsWith("| ✓ (and undo) | ✓ |", StringComparison.Ordinal))));
+        foreach (var check in new[] { "Battery IDs: a range releases", "lists every reason and releases nothing", "Create battery picks a released serial number", "opens the release's page", "The Battery IDs page opens on Releases", "\"Show demo IDs\" ticked", "red notice naming it", "\"Change its battery ID…\" on a draft", "\"Not published yet\"" })
+        {
+            Assert.Contains(check, pack, StringComparison.Ordinal);
+        }
+
+        Assert.DoesNotContain("until the identifier scheme is built", pack, StringComparison.Ordinal);
+        Assert.Contains("Battery IDs tab: release VPC-12221 to VPC-12230 with \"Release new IDs\"", script, StringComparison.Ordinal);
+        Assert.Contains("the release's page opens", script, StringComparison.Ordinal);
+        Assert.Contains("in its row's ⋯ menu, Void…", script, StringComparison.Ordinal);
+        Assert.Contains("\"batterySerialNumber\": \"DEMO-SPARE-0001\"", script, StringComparison.Ordinal);
+        Assert.Contains("DEMO-SPARE-0001 to DEMO-SPARE-0004", Read("docs", "sample-cluster-test-accounts.md"), StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void TheDemoSpares_ShouldBeTheOnesTheDocsName() =>
+        Assert.Equal(["DEMO-SPARE-0001", "DEMO-SPARE-0002", "DEMO-SPARE-0003", "DEMO-SPARE-0004"], BatteryPassWeb.Dpp.Demo.DemoDataService.SpareSerials);
+}
```

- [ ] **Step 2: See them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~BatteryIdsHelpTests"`
Expected: `Failed!  - Failed:     3, Passed:     1`: the Battery IDs part, the workflow and the test pack don't describe the release flow yet (the spares test already passes).

- [ ] **Step 3: Write the Help and the documents**

Save as `../6d3-app.patch` and run `git apply ../6d3-app.patch`:

```diff
diff --git a/docs/end-user-testing-guide.md b/docs/end-user-testing-guide.md
index 3378283..bb54782 100644
--- a/docs/end-user-testing-guide.md
+++ b/docs/end-user-testing-guide.md
@@ -113,9 +113,25 @@ Each step says what to do, then **Expected:** what should happen.
    - publishing it makes the old one inactive and links the two.
 8. Cluster: Change on a battery.
    **Expected:** it moves at once, and the move is in the audit log.
-9. Clusters tab: Demo data, Clear demo data.
+9. Battery IDs tab: release VPC-12221 to VPC-12230 with "Release new IDs": quantity 10, reference "PO 4512, line 2", model Compact 7M.
+   **Expected:** the release's page opens with "Released 10 battery IDs. Export them to Excel for the labels.", and its ten IDs Available, each with its battery ID (`UN778474481VPC12221` …).
+10. The same, with VPC-12228 to VPC-12240 and quantity 12, and VPC-12401 twice in the list.
+   **Expected:** nothing is released; the Release new IDs tab lists the reasons ("that is 13 IDs, not 12", "already released", "in this release twice") and keeps what you typed.
+11. The Releases tab, then Open on PO 4512, line 2, and Export to Excel.
    **Expected:**
-   - the demo records go and real data stays;
+   - Releases lists PO 4512, line 2 and the demo data's release, tagged Demo; the counts at the top are the real IDs only;
+   - an Excel file with that release's ten IDs, one row per ID: serial number, battery ID, QR code address, model, reference, released by and when, status.
+12. Create battery: a Compact 7M batch, pick VPC-12221.
+   **Expected:** the picker offers Compact 7M's IDs first, then any model's, then the demo spares; the draft shows `UN778474481VPC12221` and its QR code; the ID is Used.
+13. On that draft, More: Change its battery ID…, VPC-12222.
+   **Expected:** the draft now has VPC-12222 and its ID; VPC-12221 is Available again.
+14. All IDs: search VPC-12230; in its row's ⋯ menu, Void…, with a reason. Then tick "Show demo IDs".
+   **Expected:** it is Voided with the reason, and the create picker no longer offers it; the demo IDs show only once ticked.
+15. Signed out, open `/25S/UN778474481VPC12223`, then `/25S/UN778474481VPC12230`.
+   **Expected:** "Not published yet", then "Not available".
+16. Clusters tab: Demo data, Clear demo data.
+   **Expected:**
+   - the demo records go, the "Demo" release with them, and real data stays (PO 4512's IDs included);
    - Reset demo data brings them back.
 
 ## 5. Market surveillance: `demo.msa@example.test`
@@ -166,8 +182,8 @@ Use the tokens copied after the reset, on `/help/api`'s workbench or with any HT
    **Expected:** saved. The live value history shows it.
 5. The same token, `POST /api/external/v1/uploads?preview=true` with the help's upload file.
    **Expected:** the battery is refused: "status changes are made on the battery's page" (the file carries a repair).
-6. The team's "Full access" token: `GET /api/external/v1/batches`, then `POST /api/external/v1/batteries`.
-   **Expected:** a new draft battery with its battery ID.
+6. The team's "Full access" token: `GET /api/external/v1/batches`, then `POST /api/external/v1/batteries` with `"batterySerialNumber": "DEMO-SPARE-0001"`.
+   **Expected:** a new draft battery with that released ID. The same with a serial number that isn't released is refused.
 7. `GET /api/passports` with a cluster token.
    **Expected:** refused (team only).
 
diff --git a/docs/qa-test-pack.md b/docs/qa-test-pack.md
index 40d172f..605d7cd 100644
--- a/docs/qa-test-pack.md
+++ b/docs/qa-test-pack.md
@@ -23,7 +23,7 @@ Keep these open while testing:
 
 | Who | Account | Cluster | What to check |
 |---|---|---|---|
-| Team | the team's own account (from the project owner) | all | Product data, creating and publishing batteries, every battery page, marking passports, demo data, tokens |
+| Team | the team's own account (from the project owner) | all | Product data, releasing battery IDs, creating and publishing batteries, every battery page, marking passports, demo data, tokens |
 | Cluster admin | `demo.north.admin@example.test` | demo-north | Their batteries' pages: live values, accident reports, status changes and repairs, hand-overs, uploads; their users and tokens. Their Registry also shows DEMO-CORE-0001 waiting to be accepted. |
 | Cluster user | `demo.north.user@example.test` | demo-north | Their batteries in the Registry and on the detail page, read only |
 | Other cluster admin | `demo.south.admin@example.test`, `demo.fleet.admin@example.test` | demo-south, demo-fleet | They see demo-north's batteries as the public does; demo-north's admin's "Waiting for you" from demo-fleet |
@@ -49,25 +49,27 @@ Keep these open while testing:
 | DEMO-CORE-0002 | demo-fleet | A draft |
 | DEMO-CORE-0003 | demo-fleet | Waste, marked for deletion |
 
-All demo IDs start with `TEST-`; the pages show a "Test passport" tag for them.
+The demo batteries come from a release with the reference "Demo", on the Battery IDs tab, with four spare IDs (DEMO-SPARE-0001 to DEMO-SPARE-0004) for trying the create picker and "Change its battery ID…". With the three ID settings set (`DPP_ID_AGENCY`, `DPP_ID_COMPANY`, `DPP_ID_BASE_URL`), their IDs are marked TEST after the DUNS, such as `UN778474481TESTDEMOC7M0001`. Without them they are test IDs starting with `urn:x-battery-pass:TEST-`. Either way the pages show a "Test passport" tag.
 
 ## Scenario matrix
 
 What each reader should get. ✓ = allowed or shown, – = not shown or refused. "Own" means the battery is in the reader's cluster.
 
-| Reader | Public page | Detail page | Purchaser tab | Shared tab | Authorities tab | Live values, Modules tabs | Registry lists | Battery page (manage) | Mark as invalid |
-|---|---|---|---|---|---|---|---|---|---|
-| Anyone, not signed in | ✓ | – (sign-in) | – | – | – | – | – | – | – |
-| Cluster user, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | – | – |
-| Cluster admin, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | ✓ | – |
-| Cluster user or admin, other cluster's battery | ✓ | public only | – | – | – | – | own cluster's | – (refused) | – |
-| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every battery (Batteries tab) | ✓ | ✓ (and undo) |
-| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every published | – | ✓ |
-| Notified body | ✓ | ✓ | – | – | ✓ | – | every published | – | – |
-| Commission | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – |
-| Repair | ✓ | ✓ | – | #43, #45–#48 | – | – | every published | – | – |
-| Recycling | ✓ | ✓ | – | #43, #45, #48 | – | – | every published | – | – |
-| Treatment | ✓ | ✓ | – | #48 | – | – | every published | – | – |
+| Reader | Public page | Detail page | Purchaser tab | Shared tab | Authorities tab | Live values, Modules tabs | Registry lists | Battery page (manage) | Mark as invalid | Battery IDs tab |
+|---|---|---|---|---|---|---|---|---|---|---|
+| Anyone, not signed in | ✓ | – (sign-in) | – | – | – | – | – | – | – | – (sign-in) |
+| Cluster user, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | – | – | – (Not allowed) |
+| Cluster admin, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | ✓ | – | – (Not allowed) |
+| Cluster user or admin, other cluster's battery | ✓ | public only | – | – | – | – | own cluster's | – (refused) | – | – (Not allowed) |
+| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every battery (Batteries tab) | ✓ | ✓ (and undo) | ✓ |
+| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every published | – | ✓ | – (Not allowed) |
+| Notified body | ✓ | ✓ | – | – | ✓ | – | every published | – | – | – (Not allowed) |
+| Commission | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – | – (Not allowed) |
+| Repair | ✓ | ✓ | – | #43, #45–#48 | – | – | every published | – | – | – (Not allowed) |
+| Recycling | ✓ | ✓ | – | #43, #45, #48 | – | – | every published | – | – | – (Not allowed) |
+| Treatment | ✓ | ✓ | – | #48 | – | – | every published | – | – | – (Not allowed) |
+
+Scanning a released ID whose passport isn't published yet shows "Not published yet" to everyone, with no battery data; a voided ID shows "Not available".
 
 **Battery page actions** (team and the battery's cluster admins, on an active passport):
 
@@ -80,6 +82,7 @@ What each reader should get. ✓ = allowed or shown, – = not shown or refused.
 | Hand over… / Accept or Decline on "Waiting for you" | moves directly, or cancels a waiting one | ✓ |
 | Mark as invalid or for deletion…, and Undo | ✓ | – |
 | Edit product data, create and publish batteries | ✓ | – |
+| Release, export and void battery IDs; Change its battery ID… on a draft | ✓ | – |
 
 **API** (`/api/external/v1`, a token in the `Authorization` header):
 
@@ -87,8 +90,8 @@ What each reader should get. ✓ = allowed or shown, – = not shown or refused.
 |---|---|---|
 | A cluster's "Read" token | that cluster's batteries, as its cluster user would | nothing |
 | A cluster's "Read and write live values" token | the same | the live values (#63, #83, #87, E1–E4) and uploads of live values, for that cluster's batteries |
-| The team's "Full access" token | every battery | creating batteries, publishing, live values, uploads |
-| Any token | — | never a status change or repair (made on the battery's page only) |
+| The team's "Full access" token | every battery | creating batteries (from a released, available serial number), publishing, live values, uploads |
+| Any token | — | never a status change or repair (made on the battery's page only), and never releasing battery IDs |
 
 `/api/passports` (the list of all passports) is for the team only.
 
@@ -97,7 +100,14 @@ What each reader should get. ✓ = allowed or shown, – = not shown or refused.
 - [ ] "Reset demo data" finishes and shows the tokens once.
 - [ ] Every row of the scenario matrix holds, for every reader.
 - [ ] The two badges show on every passport page with the right status. The Battery badge never shows to the public.
-- [ ] DEMO-C7M-0002 lists what blocks publication; filling it in and pressing Publish gives a signed version 1 with a QR code.
+- [ ] Battery IDs: a range releases and opens the release's page with every ID Available; its "Export to Excel" opens with that release's IDs, one row per ID and its QR address.
+- [ ] The Battery IDs page opens on Releases, with the real IDs' counts at the top; All IDs shows demo IDs only with "Show demo IDs" ticked; Void… is in a row's ⋯ menu.
+- [ ] A release with a wrong count, a repeated or already released serial number lists every reason and releases nothing.
+- [ ] Create battery picks a released serial number (no typing); the draft shows its battery ID and QR code at once; the ID turns Used.
+- [ ] "Change its battery ID…" on a draft takes another available ID and gives the old one back; a voided ID is never offered again.
+- [ ] Scanning a released, unpublished ID shows "Not published yet"; a voided one "Not available".
+- [ ] With one of the three ID settings missing, the Battery IDs page shows a red notice naming it; nothing can be released, creating a battery from a released real ID is refused, and the demo data still works.
+- [ ] DEMO-C7M-0002 lists what blocks publication; filling it in and pressing Publish gives a signed version 1 with a QR code, and its ID turns Published.
 - [ ] A repair on DEMO-C7M-0001 sets re-used, adds to the repair history and makes a new version.
 - [ ] Hand-over: demo-fleet's DEMO-CORE-0001 waits on demo-north's Registry; Accept moves it, and demo-fleet then sees it as the public does.
 - [ ] Market surveillance marks a passport invalid; the public page says so; only the team undoes it.
@@ -108,7 +118,7 @@ What each reader should get. ✓ = allowed or shown, – = not shown or refused.
 
 ## Known limitations
 
-- **Test identifiers:** all IDs start with `TEST-` until the identifier scheme is built (DUNS, plan 6d).
+- **Real IDs need the settings:** without `DPP_ID_AGENCY`, `DPP_ID_COMPANY` and `DPP_ID_BASE_URL`, the Battery IDs page shows a red notice naming the missing ones, and nothing is released or created from a released ID. The demo data still works, with test IDs (`urn:x-battery-pass:TEST-…`), as on a developer's PC.
 - **Pack temperature:** the battery system's export has no pack temperature yet; each export's notes say so.
 - **Hand-overs:** no e-mail; the receiver sees them on their Registry. A waiting hand-over isn't cancelled by itself when the passport stops being active; accepting is then refused with a reason.
 - **Signatures:** each version is signed when published; live values and accident reports change it in place without a new signature, by design.
diff --git a/docs/sample-cluster-test-accounts.md b/docs/sample-cluster-test-accounts.md
index 0a5fb5c..7dcfaba 100644
--- a/docs/sample-cluster-test-accounts.md
+++ b/docs/sample-cluster-test-accounts.md
@@ -54,7 +54,7 @@ The team's account is not demo data: the project owner gives testers a team acco
 | DEMO-CORE-0002 | Core, Core | demo-fleet | Draft | |
 | DEMO-CORE-0003 | Core, Core | demo-fleet | Waste, marked for deletion | The marked-for-deletion notice |
 
-Drafts are seen by the team only. Every ID starts with `TEST-`, and the pages show a "Test passport" tag.
+Drafts are seen by the team only. The batteries come from the "Demo" release on the Battery IDs tab, which also holds four spare IDs, DEMO-SPARE-0001 to DEMO-SPARE-0004. With the three ID settings their IDs are marked TEST after the DUNS (`UN778474481TESTDEMOC7M0001`); without them they start with `urn:x-battery-pass:TEST-`. The pages show a "Test passport" tag either way.
 
 ## Sample files
 
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index 58faa0a..2d36406 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -30,7 +30,7 @@
         <div class="bp-help-parts">
             <section class="bp-card bp-help-part" id="find">
                 <h2>Find a passport</h2>
-                <p class="mb-0">On the <a href="/">home page</a>, type the battery's serial number, battery ID or passport ID, or scan the QR code on the battery. @(signedIn ? "The Registry lists the batteries you may open." : "Signed in, the Registry also lists the batteries you may open.")</p>
+                <p class="mb-0">On the <a href="/">home page</a>, type the battery's serial number, battery ID or passport ID, or scan the QR code on the battery. A label scanned before its battery's passport is published says "Not published yet". @(signedIn ? "The Registry lists the batteries you may open." : "Signed in, the Registry also lists the batteries you may open.")</p>
             </section>
 
             <section class="bp-card bp-help-part" id="see">
@@ -157,13 +157,39 @@
                     <h2>From product data to a published passport <span class="bp-soft-tag">Team</span></h2>
                     <ol class="bp-steps">
                         <li><strong><a href="/admin/families">Set up product data</a></strong><p>Families, models and batches, and <a href="/admin/sites-and-operators">sites and operators</a>. Most values are entered once and shared by every battery made from them; the V3 attribute list decides which are required, and by when.</p></li>
-                        <li><strong><a href="/admin/batteries/new">Create the battery</a></strong><p>Choose its batch, economic operator and cluster, and type its serial number, manufacturing date and warranty. The app gives it its battery ID. It starts as a draft only the team sees.</p></li>
+                        <li><strong><a href="/admin/battery-ids">Release battery IDs</a></strong><p>Reserve the serial numbers before the batteries are made, so their labels can be printed. Each gets its battery ID at once, fixed for life.</p></li>
+                        <li><strong><a href="/admin/batteries/new">Create the battery</a></strong><p>Choose its batch, economic operator and cluster, and pick its serial number from the released IDs; then its manufacturing date and warranty. It starts as a draft only the team sees, with its battery ID and QR code.</p></li>
                         <li><strong><a href="/admin/batteries">Fill in what is missing</a></strong><p>Every field says whether it is required by Feb 2027, required later or conditional. Only "required by Feb 2027" blocks publishing, the first state of charge and temperature included.</p></li>
                         <li><strong>Publish</strong><p>On the battery page, one step checks the passport against the official schema, signs it and publishes it. Later changes ask first, then publish a new version; live values and accident reports change it in place.</p></li>
                     </ol>
                     <p class="mt-3 mb-0">Other systems get <a href="/admin/clusters?tab=api-token-management">API tokens</a>: a cluster token reads that cluster's batteries and, with "Read + write", sends their live values; only a token for all batteries creates and publishes them.</p>
                 </section>
 
+                <section class="bp-card bp-help-part" id="battery-ids">
+                    <h2>Battery IDs <span class="bp-soft-tag">Team</span></h2>
+                    <p>A battery's ID (#6) is the company's DUNS and its serial number, such as <code>UN778474481VPC12221</code>. The team releases serial numbers in advance on the <a href="/admin/battery-ids">Battery IDs</a> tab, and each battery is created from one. At the top, the real IDs by status (demo IDs aren't counted) and "Release new IDs"; below, three tabs: Releases, All IDs and Release new IDs.</p>
+                    <dl class="bp-word-list">
+                        <dt>Release new IDs</dt>
+                        <dd>Give a reference, such as the purchase order, and optionally the model. Add one or more ranges (start, end and quantity) or paste serial numbers, one per line. It is all or nothing: if one can't be released, none is, and each reason is listed. Once released, the app opens the release's page.</dd>
+                        <dt>Releases</dt>
+                        <dd>The first tab: one row per release, with its reference, model, date and counts. "Open" shows the release's page: its IDs, "Export to Excel" at the top and "Void unused…". The demo data's release is tagged Demo.</dd>
+                        <dt>All IDs</dt>
+                        <dd>Every released ID, searched by serial number and filtered by status, release and model. Demo IDs show only with "Show demo IDs" ticked.</dd>
+                        <dt>Export to Excel</dt>
+                        <dd>One row per ID, with the QR code's full address for the label printer or a mail merge. On a release's page or row, that release's IDs; on All IDs, the filtered ones.</dd>
+                        <dt>Statuses</dt>
+                        <dd>Available, Used (a draft has it), Published (fixed for life) and Voided (never used again).</dd>
+                        <dt>Void…</dt>
+                        <dd>Only an available ID, with a reason such as a damaged label: in its row's ⋯ menu. "Void unused…" voids a release's available IDs at once.</dd>
+                        <dt>A red notice</dt>
+                        <dd>"The battery ID settings aren't set": the app isn't given its DUNS and web address (<code>DPP_ID_AGENCY</code>, <code>DPP_ID_COMPANY</code>, <code>DPP_ID_BASE_URL</code>), so nothing can be released and no battery can be created from a released ID, as they would get test IDs. Whoever runs the app sets them and restarts it. The demo data works without them.</dd>
+                        <dt>Change its battery ID…</dt>
+                        <dd>On a draft's page, under More: it takes another available ID, and its old one is available again. Removing a draft gives its ID back too.</dd>
+                        <dt>A new passport</dt>
+                        <dd>The battery keeps its serial number, and its new passport's battery ID adds R1, R2, …; it isn't released.</dd>
+                    </dl>
+                </section>
+
                 <section class="bp-card bp-help-part" id="lifecycle">
                     <h2>After publishing <span class="bp-soft-tag">Team</span></h2>
                     <dl class="bp-word-list">
diff --git a/web/Views/Shared/_HelpContents.cshtml b/web/Views/Shared/_HelpContents.cshtml
index d1ba079..79f8af7 100644
--- a/web/Views/Shared/_HelpContents.cshtml
+++ b/web/Views/Shared/_HelpContents.cshtml
@@ -52,6 +52,7 @@
     {
         <p>For the team</p>
         <a href="@Part("workflow")">From product data to a published passport</a>
+        <a href="@Part("battery-ids")">Battery IDs</a>
         <a href="@Part("lifecycle")">After publishing</a>
         <a href="@Part("ready")">Before you call a battery ready</a>
     }
```

- [ ] **Step 4: See the tests pass**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~BatteryIdsHelpTests"`
Expected: `Passed!  - Failed:     0, Passed:     4`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/BatteryIdsHelpTests.cs docs/end-user-testing-guide.md docs/qa-test-pack.md docs/sample-cluster-test-accounts.md web/Views/Help/Guide.cshtml web/Views/Shared/_HelpContents.cshtml
git commit -m "docs(dpp): the Help and test documents for battery IDs: the tabs, a release's page and the red notice" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2615, Skipped:     0, Total:  2615`. That is the 2611 from before plus 4 new test cases.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat db7bd45 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner:

1. As the team, `/help`: "Battery IDs" in the contents, describing the tabs, a release's page and the red notice; and the workflow's "Release battery IDs" step.
2. Signed out, `/help`: "Find a passport" mentions "Not published yet"; no team part shows.
3. The test pack's matrix and checklist, and the team's script steps 9–15, ready for the QA round.

## Next

Step 6e, the AWS cutover runbook, with the three ID settings (`DPP_ID_AGENCY=UN`, `DPP_ID_COMPANY=778474481`, `DPP_ID_BASE_URL=https://www.sib-scania-eubp.eu`), and `dpp_battery_ids` among the collections the cutover's reset treats as new test data. Without them the Battery IDs page shows its red notice, so the runbook checks it doesn't.
