# DPP V3 Step 6a-2: The Passport Pages on Paper — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A print style for the public summary and the detail page (step 6, go-live readiness, design 14). It follows the product owner's answers of 4 Nov 2026 and the A4 previews they approved ("Approve all"):
- **P1:** both pages print: the public summary and the signed-in detail page.
- **P2:** every tab and section prints open, one after another, each under its own heading (Live values and Modules included).
- **P3:** the live values print as a small table of each value's latest reading and when and from where it came. No charts, and no week's "All readings" list.
- **P4:** the QR code and the page's address head the first page, inside the hero card.
- **P5:** no top bar, menus, tab strip, buttons or footer on paper.
- **Three additions the owner approved:**
  - a "Printed <date, time>" line under the address, because the live values change after printing;
  - a document link prints its web address after its title;
  - the "Test passport" tag stays on paper, without its "?".

On screen nothing changes: the print-only parts are hidden, and `site.css` is not touched.

**Architecture:**
- **`web/wwwroot/css/bp-print.css`** (new) holds every print rule. It is loaded with `media="print"` by `Views/Passport/Summary.cshtml` and `Detail.cshtml` only, so no other page and no screen is affected.
- **`_PassportHero.cshtml`** gets the print-only address line under the QR code: `d-none d-print-block`, only when `APP_BASE_URL` is set. It shows the printed page's address and the print time. `DppLinks` and `TimeProvider` are injected.
- **`_LiveHistory.cshtml`** gets the print-only table of latest readings (`d-none d-print-table`), built from the same tiles as the charts. Its two intro lines, which talk about the charts, are hidden on paper (`d-print-none`).
- **Bootstrap 5.3.3's own print classes** (`d-print-*`, already loaded) switch these parts.
- **One new test class:** `BatteryPassWeb.Tests/Look/PrintStyleTests.cs` (6 tests).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, xUnit 2.9, Bootstrap 5.3.3 (already loaded). Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 12 (pages) and 14 (step 6). The owner's answers P1–P5 and the approved previews are in the managing session's notes: `step6-split-proposal.md` and `step6a2-print/`.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at 879185c (plan 6a-1's last commit, "fix(dpp): in Production the app refuses to start without MONGODB_DB…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas or any database.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
  - If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started, by their process IDs, never by name. Then run it again.
- **Baseline:** the whole suite shows `Passed:  2528` (plan 6a-1's result). After this plan: 2534.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6a2-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**
- **The old app is never touched:**
  - keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`;
  - never touch the running app on Elastic Beanstalk or its database `battery_pass_demo`.

## Found while prototyping

**Setup:**
- a scratch clone with my own app and a throwaway mongod, after a demo reset;
- DEMO-C7M-0001 printed to A4 PDF by Edge from a 1280 px window: as the public, as market surveillance and as the battery's cluster user;
- each PDF checked for text outside the page margins and for its key texts;
- both pages checked on screen at 1280 and 375.

**Findings:**
- **The wide cards printed cut off on the left.**
  - On screen, `site.css` stretches the hero and the wide cards to the window's width: `width: min(1860px, calc(100vw - 96px)); margin-inline: 50%; transform: translateX(-50%)`.
  - On paper, `100vw` is the whole sheet (about 794 px) while the page lays out inside the margins (about 703 px). Every card then started left of the page.
  - `bp-print.css` sets their width, margins and transform back. This is the rule `OnPaper_TheWideCards_ShouldStayInsideThePage` keeps.
- **Width media queries see the whole sheet when printing.** A print layout that relied on the screen breakpoints overflowed, so `bp-print.css` gives the hero its own grid:
  - the battery, the picture and the QR code in one row;
  - then the IDs;
  - then the facts in three columns.
- **The window's width doesn't matter.** A PDF printed from a 703 px window was identical to the one from 1280, so a phone prints the same paper layout.
- **Views are compiled into the build.** A change to a `.cshtml` shows only after a rebuild and restart, while `bp-print.css` is read at once.
- **Results:**
  - public summary, 5 A4 pages; detail page, 5 pages for market surveillance and for the cluster user;
  - no text outside the margins;
  - every tab printed (8 of 8 on the public summary, 13 of 13 for market surveillance, 12 of 12 for the cluster user);
  - no top bar, footer, buttons, tab strip or charts; the latest-readings table and the QR code shown.
- **On screen, nothing changed** at 1280 and 375:
  - the address line and the table stay hidden;
  - one tab shows at a time;
  - `bp-print.css` is linked with `media="print"`.
- **Approved previews and scripts** (the managing session's notes, `step6a2-print/`):
  - `public-summary.pdf`, `detail-msa.pdf`, `detail-north-user.pdf`;
  - `print6a2.mjs` (the prints), `check_print_pdfs.py` (margins and texts), `screen6a2.mjs` (the screen check).
- **Replayed on a clean clone of the branch at 879185c:** 5 of the 6 tests fail first, then all pass. The sixth, `SiteCss_ShouldKeepNoPrintRules`, is a guard that holds before and after.
  - The clean clone shows the two known `ApplicationAuditCoverageTests` failures: a clean checkout gives `site.css` CRLF line endings.
  - They pass in the worktree.

### Task 1: The print style

**Files:**
- Create: `BatteryPassWeb.Tests/Look/PrintStyleTests.cs` (6 tests)
- Create: `web/wwwroot/css/bp-print.css`
- Modify: `web/Views/Passport/Summary.cshtml`, `web/Views/Passport/Detail.cshtml` (the print link)
- Modify: `web/Views/Passport/_PassportHero.cshtml` (the address line)
- Modify: `web/Views/Passport/_LiveHistory.cshtml` (the latest-readings table; the intro hidden on paper)

- [ ] **Step 1: Write the failing tests**

Save as `../6a2-test.patch` and run `git apply ../6a2-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Look/PrintStyleTests.cs b/BatteryPassWeb.Tests/Look/PrintStyleTests.cs
new file mode 100644
index 0000000..b6632c3
--- /dev/null
+++ b/BatteryPassWeb.Tests/Look/PrintStyleTests.cs
@@ -0,0 +1,76 @@
+using BatteryPassWeb.Tests.Dpp;
+
+namespace BatteryPassWeb.Tests.Look;
+
+/// <summary>
+/// Step 6a-2, the passport pages on paper (the owner's answers P1–P5 and the A4 previews approved on 4 Nov 2026): the public
+/// summary and the detail page load bp-print.css for print only; on paper every tab prints open, the live values are a
+/// table of each value's latest reading, and the QR code, the page's address and when it was printed head the first page.
+/// The print-only parts are hidden on screen, and site.css keeps no print rules.
+/// </summary>
+public sealed class PrintStyleTests
+{
+    private const string PrintLink = "<link rel=\"stylesheet\" href=\"~/css/bp-print.css\" media=\"print\" asp-append-version=\"true\" />";
+
+    private static string Read(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(["web", .. path])).Replace("\r\n", "\n");
+
+    private static string Print() => Read("wwwroot", "css", "bp-print.css");
+
+    [Fact]
+    public void OnlyTheTwoPassportPages_ShouldLoadThePrintStyle_ForPrintOnly()
+    {
+        Assert.Contains(PrintLink, Read("Views", "Passport", "Summary.cshtml"), StringComparison.Ordinal);
+        Assert.Contains(PrintLink, Read("Views", "Passport", "Detail.cshtml"), StringComparison.Ordinal);
+
+        var others = Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Shared", "_Layout.cshtml")))!, "*.cshtml", SearchOption.AllDirectories)
+            .Where(path => File.ReadAllText(path).Contains("bp-print.css", StringComparison.Ordinal))
+            .Select(Path.GetFileName)
+            .OrderBy(name => name, StringComparer.Ordinal);
+        Assert.Equal(["Detail.cshtml", "Summary.cshtml"], others);
+    }
+
+    [Fact]
+    public void ThePrintOnlyParts_ShouldBeHiddenOnScreen()
+    {
+        var hero = Read("Views", "Passport", "_PassportHero.cshtml");
+        var live = Read("Views", "Passport", "_LiveHistory.cshtml");
+
+        Assert.Contains("<p class=\"bp-print-address d-none d-print-block\">", hero, StringComparison.Ordinal);
+        Assert.Contains("@if (Links.IsConfigured)", hero, StringComparison.Ordinal);
+        Assert.Contains("<table class=\"bp-print-live d-none d-print-table\">", live, StringComparison.Ordinal);
+        Assert.Contains("<th>Value</th><th>Latest reading</th><th>When and from where</th>", live, StringComparison.Ordinal);
+        Assert.Equal(2, live.Split("<p class=\"bp-help-text d-print-none\">").Length - 1);
+    }
+
+    [Fact]
+    public void OnPaper_TheScreenOnlyPartsGo_AndEveryTabPrints()
+    {
+        var print = Print();
+
+        Assert.Contains("@page {\n  size: A4;", print, StringComparison.Ordinal);
+        foreach (var hidden in new[] { "header.bp-header,", "footer.bp-footer,", ".bp-hero-ctas,", "#passport-tabs,", "[data-mark-invalid],", ".bp-copy-id-button,", ".bp-status-help-button,", ".bp-live-charts,", ".bp-live-all," })
+        {
+            Assert.Contains(hidden, print, StringComparison.Ordinal);
+        }
+
+        Assert.Contains(".tab-content > .tab-pane {\n  display: block !important;\n  opacity: 1 !important;\n}", print, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void OnPaper_TheWideCards_ShouldStayInsideThePage()
+    {
+        // On screen site.css stretches them to 100vw with translateX(-50%); on paper 100vw is the sheet, which cut their left side.
+        Assert.Contains(".bp-report-hero-card,\n.bp-report-wide-section {\n  width: auto !important;\n  margin-inline: 0 !important;\n  transform: none !important;\n}", Print(), StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void OnPaper_ADocumentLink_ShouldShowItsAddress() =>
+        Assert.Contains(".bp-field-grid dd a[target=\"_blank\"]::after {\n  content: \" (\" attr(href) \")\";", Print(), StringComparison.Ordinal);
+
+    [Fact]
+    public void SiteCss_ShouldKeepNoPrintRules()
+    {
+        Assert.DoesNotContain("@media print", Read("wwwroot", "css", "site.css"), StringComparison.Ordinal);
+        Assert.DoesNotContain("bp-print.css", Read("Views", "Shared", "_Layout.cshtml"), StringComparison.Ordinal);
+    }
+}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental` then `dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~PrintStyleTests"`
Expected: `Failed!  - Failed:     5, Passed:     1`.

- [ ] **Step 3: Add the print style and the view changes**

Save as `../6a2-code.patch` and run `git apply ../6a2-code.patch`:

```diff
diff --git a/web/Views/Passport/Detail.cshtml b/web/Views/Passport/Detail.cshtml
index 5dcb39c..a5bec94 100644
--- a/web/Views/Passport/Detail.cshtml
+++ b/web/Views/Passport/Detail.cshtml
@@ -6,6 +6,8 @@
     ViewData["Title"] = $"Battery passport detail · {Model.Text(11)}";
     ViewData["Detail"] = true;
 }
+@* The paper version (step 6a-2). *@
+<link rel="stylesheet" href="~/css/bp-print.css" media="print" asp-append-version="true" />
 <main class="bp-page">
     @if (TempData[BatteryPassWeb.Controllers.PassportController.MarkMessageKey] is string marked)
     {
diff --git a/web/Views/Passport/Summary.cshtml b/web/Views/Passport/Summary.cshtml
index 94b1e45..1d64383 100644
--- a/web/Views/Passport/Summary.cshtml
+++ b/web/Views/Passport/Summary.cshtml
@@ -5,6 +5,8 @@
     ViewData["Title"] = $"Battery passport · {Model.Text(11)}";
     ViewData["Detail"] = false;
 }
+@* The paper version (step 6a-2). *@
+<link rel="stylesheet" href="~/css/bp-print.css" media="print" asp-append-version="true" />
 <main class="bp-page">
     <partial name="_PassportHero" model="Model" view-data="ViewData" />
 
diff --git a/web/Views/Passport/_LiveHistory.cshtml b/web/Views/Passport/_LiveHistory.cshtml
index 8f2bfec..0897533 100644
--- a/web/Views/Passport/_LiveHistory.cshtml
+++ b/web/Views/Passport/_LiveHistory.cshtml
@@ -20,12 +20,22 @@
 <h2 class="h5 mb-2">@LiveTrends.WeekTitle(end, now)</h2>
 @if (older)
 {
-    <p class="bp-help-text">The battery has sent no reading since @ValueWords.Time(end), so the charts show its last week of readings. Every reading of that week, with when it was measured and how it arrived, is under the charts.</p>
+    <p class="bp-help-text d-print-none">The battery has sent no reading since @ValueWords.Time(end), so the charts show its last week of readings. Every reading of that week, with when it was measured and how it arrived, is under the charts.</p>
 }
 else
 {
-    <p class="bp-help-text">Each value's latest reading and when it was taken, and how it went over the last week when the battery sent readings. Every reading of the week, with when it was measured and how it arrived, is under the charts.</p>
+    <p class="bp-help-text d-print-none">Each value's latest reading and when it was taken, and how it went over the last week when the battery sent readings. Every reading of the week, with when it was measured and how it arrived, is under the charts.</p>
 }
+@* On paper, each value's latest reading instead of the charts (step 6a-2, the owner's P3). *@
+<table class="bp-print-live d-none d-print-table">
+    <thead><tr><th>Value</th><th>Latest reading</th><th>When and from where</th></tr></thead>
+    <tbody>
+        @foreach (var chart in charts)
+        {
+            <tr><td>@chart.Title</td><td>@chart.Shown</td><td>@chart.Note</td></tr>
+        }
+    </tbody>
+</table>
 <div class="bp-live-charts">
     @foreach (var chart in charts)
     {
diff --git a/web/Views/Passport/_PassportHero.cshtml b/web/Views/Passport/_PassportHero.cshtml
index a2b68f2..80bd5a7 100644
--- a/web/Views/Passport/_PassportHero.cshtml
+++ b/web/Views/Passport/_PassportHero.cshtml
@@ -1,5 +1,7 @@
 @using BatteryPassWeb.Dpp.Pages
 @model PassportPage
+@inject BatteryPassWeb.Dpp.Documents.DppLinks Links
+@inject TimeProvider Clock
 @{
     // The battery card of both passport pages (today's look, decision 23), with the new data underneath.
     var detail = ViewData["Detail"] is true;
@@ -87,6 +89,11 @@
                 <a class="bp-report-hero-qr" href="@PassportPages.QrDownloadHref(Model.Identifier)" aria-label="Save the QR code of battery @Model.Text(11)">
                     <img src="@PassportPages.QrHref(Model.Identifier)" alt="QR code of battery @Model.Text(11)" />
                 </a>
+                @* On paper only (step 6a-2, the owner's P4): the printed page's address and when it was printed. *@
+                @if (Links.IsConfigured)
+                {
+                    <p class="bp-print-address d-none d-print-block">@(Links.PassportUrl(Model.Address) + (detail ? "/detail" : string.Empty))<br />Printed @ValueWords.Time(Clock.GetUtcNow().UtcDateTime)</p>
+                }
             </section>
 
             @if (Model.PictureHref is not null)
diff --git a/web/wwwroot/css/bp-print.css b/web/wwwroot/css/bp-print.css
new file mode 100644
index 0000000..c12ed38
--- /dev/null
+++ b/web/wwwroot/css/bp-print.css
@@ -0,0 +1,210 @@
+/*
+ * The passport pages on paper (step 6a-2; the owner's answers P1–P5, 4 Nov 2026). Loaded with media="print" by the
+ * public summary and the detail page only. P1: both pages. P2: every tab and section open. P3: the live values as a
+ * table of each value's latest reading, not the charts. P4: the QR code and the page's address at the top of the first
+ * page. P5: no top bar, menus, buttons or footer.
+ */
+
+@page {
+  size: A4;
+  margin: 14mm 12mm;
+}
+
+html,
+body {
+  background: #fff !important;
+  color: #111 !important;
+  font-size: 10.5pt;
+}
+
+/* P5: what only works on screen. */
+header.bp-header,
+footer.bp-footer,
+.bp-hero-ctas,
+#passport-tabs,
+[data-mark-invalid],
+.bp-copy-id-button,
+.bp-status-help-button,
+[popover],
+.bp-live-charts,
+.bp-live-all,
+script {
+  display: none !important;
+}
+
+/* Width media queries see the whole sheet (about 794 px) while the page lays out inside the margins (about 703 px), so
+   the screen layouts for that width overflow; a centred flex column then cuts their left side. On paper the page is a
+   plain column, and the hero has its own grid. */
+.bp-shell,
+main.bp-main,
+main.bp-page {
+  display: block !important;
+  width: auto !important;
+  max-width: none !important;
+  margin: 0 !important;
+  padding: 0 !important;
+}
+
+.bp-report-hero-layout,
+.bp-report-hero-info {
+  display: grid !important;
+  grid-template-columns: minmax(0, 1fr) 24mm 34mm !important;
+  grid-template-areas:
+    "battery picture qr"
+    "ids ids ids"
+    "fields fields fields" !important;
+  gap: 4mm !important;
+  width: auto !important;
+}
+
+.bp-report-hero-layout {
+  grid-template-columns: minmax(0, 1fr) !important;
+  grid-template-areas: none !important;
+}
+
+.bp-report-hero-info > * {
+  min-width: 0 !important;
+  margin: 0 !important;
+}
+
+.bp-hero-picture img {
+  max-width: 24mm !important;
+  max-height: 34mm !important;
+  width: auto !important;
+  height: auto !important;
+}
+
+.bp-summary-qr-panel {
+  display: block !important;
+}
+
+.bp-report-hero-fields {
+  grid-template-columns: repeat(3, minmax(0, 1fr)) !important;
+}
+
+.bp-kf-grid {
+  grid-template-columns: repeat(2, minmax(0, 1fr)) !important;
+}
+
+.bp-field-grid:not(.bp-report-hero-fields) {
+  grid-template-columns: repeat(2, minmax(0, 1fr)) !important;
+}
+
+/* Cards print flat: a thin rule instead of a shadow, and no colour fills to waste ink. */
+.bp-card,
+.bp-report-hero-card,
+.bp-kf-card {
+  box-shadow: none !important;
+  border: 1px solid #bbb !important;
+  background: #fff !important;
+  margin: 0 0 6mm !important;
+  padding: 5mm !important;
+}
+
+/* On screen the hero and the wide cards break out of the column to the window's width (100vw, margin-inline: 50%,
+   translateX(-50%), in site.css); on paper 100vw is the whole sheet, so they would start left of the page. */
+.bp-report-hero-card,
+.bp-report-wide-section {
+  width: auto !important;
+  margin-inline: 0 !important;
+  transform: none !important;
+}
+
+/* P4: the hero (statuses, IDs, QR code and now the page's address) starts the first page. */
+.bp-report-hero-card {
+  break-inside: avoid;
+}
+
+.bp-report-hero-card {
+  border: 0 !important;
+  padding: 0 !important;
+}
+
+.bp-report-hero-qr img {
+  display: block;
+  width: 26mm !important;
+  height: 26mm !important;
+  max-width: 100% !important;
+}
+
+.bp-print-address {
+  margin: 2mm 0 0;
+  font-size: 8pt;
+  overflow-wrap: anywhere;
+}
+
+/* The test tag keeps its words on paper, without its "?" icon. */
+.bp-test-tag {
+  border: 1px solid #b45309 !important;
+  background: none !important;
+}
+
+.bp-test-tag .bp-i {
+  display: none !important;
+}
+
+/* P2: every tab prints, one after another, each under its own heading. */
+.tab-content > .tab-pane {
+  display: block !important;
+  opacity: 1 !important;
+}
+
+.tab-content > .tab-pane + .tab-pane {
+  margin-top: 6mm;
+  padding-top: 4mm;
+  border-top: 1px solid #bbb;
+}
+
+.tab-content {
+  padding-top: 0 !important;
+}
+
+.bp-field-grid > div,
+.bp-kf,
+.bp-print-live tr {
+  break-inside: avoid;
+}
+
+.tab-pane h2,
+.bp-kf-head {
+  break-after: avoid;
+}
+
+/* A paper copy can't follow a link: a document link shows its address after its title. */
+.bp-field-grid dd a[target="_blank"]::after {
+  content: " (" attr(href) ")";
+  font-size: 8pt;
+  color: #444;
+  overflow-wrap: anywhere;
+}
+
+/* P3: the live values' latest readings. */
+.bp-print-live {
+  width: 100%;
+  border-collapse: collapse;
+  font-size: 9.5pt;
+}
+
+.bp-print-live th,
+.bp-print-live td {
+  padding: 1.5mm 2mm;
+  border-bottom: 1px solid #ccc;
+  text-align: left;
+  vertical-align: top;
+}
+
+/* Tables that scroll sideways on screen print whole. */
+.bp-table-scroll {
+  overflow: visible !important;
+  margin: 0 !important;
+}
+
+.bp-table-scroll table {
+  width: 100% !important;
+  margin: 0 !important;
+}
+
+a {
+  color: #111 !important;
+  text-decoration: none !important;
+}
```

- [ ] **Step 4: Run the tests and the classes that read these views**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~AdminFeedbackFollowupTests|FullyQualifiedName~AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPageNameTests|FullyQualifiedName~BatteryRouteAndRegistryTests|FullyQualifiedName~ClusterBatteriesControllerTests|FullyQualifiedName~CopyIdButtonLayoutTests|FullyQualifiedName~HeroButtonIconsTests|FullyQualifiedName~KeyFiguresTests|FullyQualifiedName~LatestWeekTests|FullyQualifiedName~LiveChartsTests|FullyQualifiedName~LiveTabRuleTests|FullyQualifiedName~ModuleDataTests|FullyQualifiedName~PassportControllerTests|FullyQualifiedName~PassportHeroTests|FullyQualifiedName~PassportMarkPagesTests|FullyQualifiedName~PrintStyleTests|FullyQualifiedName~PublicPageNameTests|FullyQualifiedName~QrWorkflowTests|FullyQualifiedName~RegistrySearchWordsTests|FullyQualifiedName~StatusBadgePagesTests|FullyQualifiedName~ApplicationAuditCoverageTests"`
Expected: `Passed!  - Failed:     0, Passed:   127`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/PrintStyleTests.cs web/wwwroot/css/bp-print.css web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml web/Views/Passport/_PassportHero.cshtml web/Views/Passport/_LiveHistory.cshtml
git commit -m "feat(dpp): the passport pages print on A4: every tab open, the latest readings, the QR code and the page's address" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2534, Skipped:     0, Total:  2534`. That is the 2528 from before plus 6 new tests.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 879185c -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner:

1. Restart the app with the usual launcher. Open a published battery's public page, e.g. `/dpp/DEMO-C7M-0001` after "Reset demo data".
2. Print it (Ctrl+P), or save it as PDF, on A4. Check what the previews showed:
   - the battery, its statuses, the IDs, the QR code, the page's address and "Printed …" at the top;
   - the key figures, then every tab;
   - no menus or buttons.
3. Sign in, open "View more about this passport" and print the detail page. Check that the live values print as a table of their latest readings, and that the modules are listed.
4. On screen, both pages look as before.

## Next

6b, the help pages and test pack, rewritten for the new app, per role.
