# DPP V3 Step 6b-1: The Help per Role — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The in-app Help (`/help`) for every kind of reader of design 5.2 (step 6, go-live readiness). The product owner approved it as shown on 4 Nov 2026 ("Approve help pages"), with their answers Q1, Q2 and Q4 of the step 6b split.

**Every reader, signed in or not:**
- **"Find a passport"** and **"What each reader sees"**, as before.
- **"Statuses"**: both badges with all 11 values. It is the same list as the badges' "What do these mean?" window, from one shared partial (Q2).

**Signed in:**
- **"Print a passport"**: one line.
- **Cluster users:** "Your cluster's batteries" (the Registry, the detail page with live values and modules, who makes changes), and "Need more access?".
- **Cluster admins:**
  - "Your cluster" (people, tokens);
  - "Look after a battery", in the battery page's own words: Live values, Accident reports ("Add a report"), Change status ("What happened?" with its five choices), Add a note, Hand over…, Waiting for you (Accept or Decline), and that a marked or replaced passport is read only;
  - "Upload a file", as before.
- **Market surveillance:** "Market surveillance": every published passport, every section, and "Mark as invalid…".
- **Notified body, Commission and the three legitimate-interest roles:** "What your account adds", one line naming what that role sees beyond the public values.
- **The team:** the workflow, a new "After publishing" (Correct the values, new passports, Mark as invalid or for deletion…, Cluster: Change, Demo data) and the readiness checklist. "Status words" (draft, published and archived only) gives way to "Statuses".
- The API reference card.

**Signed out (Q1, "Yes, small public help"):**
- the three parts for everyone, and nothing else;
- a "Help" link in the top bar, beside "Login". There it shows at every width: on a phone the signed-in links fold into a "Menu" that visitors don't get.

The contents column lists exactly the reader's parts. The look stays decision 33's.

**Architecture:**
- **`Views/Help/Guide.cshtml`:** the parts, each under its reader's condition:
  - `inACluster`: a member who doesn't run a cluster;
  - `isMsa`;
  - `authorityOrInterest`: a list of the notified body, Commission and LI roles the reader has, each with its line;
  - `runsACluster`, `isTeam`, `signedIn`.
- **`Views/Shared/_HelpContents.cshtml`** lists the same parts under the same conditions.
- **`Views/Shared/_StatusMeanings.cshtml`** (new) is the statuses list, moved out of `_StatusHelp.cshtml`, which now includes it. Its two CSS rules are renamed in `bp-status.css` (`.bp-status-help dl` and `dd` become `.bp-status-meanings` and `.bp-status-meanings dd`), so the list looks the same in the window and in Help. `site.css` is not changed.
- **`Views/Shared/_Layout.cshtml`:** the visitor's Help link beside Login.
- **Tests:** a new `BatteryPassWeb.Tests/Look/HelpPerRoleTests.cs` (6 tests). Three tests change for the new wording:
  - `HelpTests.TheTeamsPart_…` (the workflow ends at "After publishing"; "Statuses" in place of "Archived version");
  - `HelpTests.TheClusterAdminsPart_…` (the Registry's icons, not "Manage", open a battery's page, decision 37);
  - `StatusBadgeTests.TheQuestionMark_…` (the count of statuses moves to `_StatusMeanings`).

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, xUnit 2.9. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 5.2, 12 and 14 (step 6). Decisions 33 (one Help place), 34–37 (the lifecycle and battery pages it describes). The step 6b split and the owner's answers: the managing session's notes, `step6b-split-proposal.md`. The approved shots: `step6b1-help-shots/`.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan starts at f27d58f (plan 6b-2's last commit, "docs(dpp): the test pack for the new app…"), plus the commit that adds this plan.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas or any database.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Baseline:** the whole suite shows `Passed:  2528` (plan 6b-2's result). After this plan: 2534.
- **Patches:** save each block to the file it names, outside the worktree (e.g. `../6b1-test.patch`). Then run `git apply ../<name>` from the worktree root. New files are given inside the patches.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## Found while prototyping

**Setup:** a scratch clone with my own app and a throwaway mongod, after a demo reset. The script is the managing session's notes, `step6b1-help-shots/help6b.mjs`.

**Checks, 20 of 20 PASS:**
- the 9 roles and a signed-out visitor, at 1280 and 375;
- each reader sees exactly its parts and none of the others';
- every contents link lands on a part;
- no sideways scrolling or text past the edge;
- every heading, line, term and link, scrolled into view, is fully visible (`elementFromPoint` at its top and bottom);
- the statuses list has 11 rows;
- the visitor's Help link shows at both widths.

**The badges' window still works with the shared list:** it opens with 11 rows on the same grid.

**Where the visitor's Help link goes:** first tried with the signed-in links. At 375 those are hidden (the phone's "Menu" is for signed-in readers), so the link moved beside Login.

**`/help` was already open to visitors** (`[AllowAnonymous]` on `Guide` since 3e). What they lacked was a link to it, and they saw the Print and API parts.

**The labels are the pages' own:** "Change status", "Add a note", "Hand over…", "Waiting for you", "Accept", "Decline", "Add a report", "Mark as invalid…", "Mark as invalid or for deletion…", and the status window's five choices.

**What each other role sees** follows design 5.2, read against the app's detail pages after a demo reset (plan 6b-2's check):
- notified body: the authorities' values (#7, #12, #13, #29);
- Commission: shared and authorities' values;
- repair: #43, #45–#48;
- recycling: #43, #45, #48;
- treatment: #48.

**Measured on a clean clone of f27d58f:**
- the tests fail first: the 6 new ones and the 3 changed ones;
- then they pass with the views: 161 tests across the 23 classes that read these files;
- the clean clone shows them as 159 plus the two known `ApplicationAuditCoverageTests` failures (a clean checkout gives `site.css` CRLF line endings). They pass in the worktree.

### Task 1: The Help per role

**Files:**
- Create: `BatteryPassWeb.Tests/Look/HelpPerRoleTests.cs`, `web/Views/Shared/_StatusMeanings.cshtml`
- Modify: `BatteryPassWeb.Tests/Look/HelpTests.cs`, `BatteryPassWeb.Tests/Look/StatusBadgeTests.cs`
- Modify: `web/Views/Help/Guide.cshtml`, `web/Views/Shared/_HelpContents.cshtml`, `web/Views/Shared/_StatusHelp.cshtml`, `web/Views/Shared/_Layout.cshtml`, `web/wwwroot/css/bp-status.css`

- [ ] **Step 1: Write the failing tests**

Save as `../6b1-test.patch` and run `git apply ../6b1-test.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/Look/HelpPerRoleTests.cs b/BatteryPassWeb.Tests/Look/HelpPerRoleTests.cs
new file mode 100644
index 0000000..1d6c515
--- /dev/null
+++ b/BatteryPassWeb.Tests/Look/HelpPerRoleTests.cs
@@ -0,0 +1,103 @@
+using BatteryPassWeb.Tests.Dpp;
+
+namespace BatteryPassWeb.Tests.Look;
+
+/// <summary>
+/// Step 6b-1, the Help per role (approved by the product owner on 4 Nov 2026): a part for every kind of reader of design
+/// 5.2, the two badges' statuses in one shared list, printing, and a small Help for visitors who aren't signed in (Q1:
+/// Find a passport, What each reader sees and Statuses only, with a Help link beside Login).
+/// </summary>
+public sealed class HelpPerRoleTests
+{
+    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts)).Replace("\r\n", "\n");
+
+    private static readonly string Guide = Read("web", "Views", "Help", "Guide.cshtml");
+    private static readonly string Contents = Read("web", "Views", "Shared", "_HelpContents.cshtml");
+
+    private static string Part(string id)
+    {
+        var start = Guide.IndexOf($"id=\"{id}\"", StringComparison.Ordinal);
+        Assert.True(start >= 0, $"no part {id}");
+        var end = Guide.IndexOf("</section>", start, StringComparison.Ordinal);
+        return Guide[start..end];
+    }
+
+    [Fact]
+    public void EveryKindOfReader_ShouldHaveTheirPart()
+    {
+        Assert.Contains("var inACluster = !isTeam && !runsACluster && User.IsInRole(AccessControlService.RoleNormalUser);", Guide, StringComparison.Ordinal);
+        Assert.Contains("var isMsa = User.IsInRole(AccessControlService.RoleMarketSurveillanceAuthority);", Guide, StringComparison.Ordinal);
+        foreach (var role in new[] { "RoleNotifiedBody", "RoleCommission", "RoleLiRepair", "RoleLiRecycling", "RoleLiTreatment" })
+        {
+            Assert.Contains($"AccessControlService.{role}", Guide, StringComparison.Ordinal);
+            Assert.Contains($"AccessControlService.{role}", Contents, StringComparison.Ordinal);
+        }
+
+        foreach (var (condition, id) in new[] { ("@if (inACluster)", "your-batteries"), ("@if (isMsa)", "msa"), ("@if (authorityOrInterest.Length > 0)", "your-access"), ("@if (runsACluster)", "look-after"), ("@if (isTeam)", "lifecycle") })
+        {
+            Assert.True(Guide.IndexOf(condition, StringComparison.Ordinal) < Guide.IndexOf($"id=\"{id}\"", StringComparison.Ordinal), id);
+            Assert.Contains($"Part(\"{id}\")", Contents, StringComparison.Ordinal);
+        }
+    }
+
+    [Fact]
+    public void TheOtherRoles_ShouldBeToldExactlyWhatTheyAdd()
+    {
+        Assert.Contains("Sees: \"the authorities' values: the economic operator's and manufacturer's IDs, the service provider and the test reports\"", Guide, StringComparison.Ordinal);
+        Assert.Contains("and the authorities' values; not the purchaser's values\"", Guide, StringComparison.Ordinal);
+        Assert.Contains("Sees: \"the materials, the dismantling manuals, the part numbers, the spare parts sources and the safety measures\"", Guide, StringComparison.Ordinal);
+        Assert.Contains("Sees: \"the materials, the dismantling manuals and the safety measures\"", Guide, StringComparison.Ordinal);
+        Assert.Contains("Sees: \"the safety measures\"", Guide, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void TheStatuses_ShouldBeOneSharedList()
+    {
+        var meanings = Read("web", "Views", "Shared", "_StatusMeanings.cshtml");
+
+        Assert.Contains("@await Html.PartialAsync(\"_StatusMeanings\")", Part("statuses"), StringComparison.Ordinal);
+        Assert.Contains("@await Html.PartialAsync(\"_StatusMeanings\")", Read("web", "Views", "Shared", "_StatusHelp.cshtml"), StringComparison.Ordinal);
+        Assert.Contains("<dl class=\"bp-status-meanings\">", meanings, StringComparison.Ordinal);
+        Assert.Equal(5, meanings.Split("StatusBadge.Battery(").Length - 1);
+        Assert.Equal(6, meanings.Split("StatusBadge.Passport(").Length - 1);
+        Assert.Contains(".bp-status-meanings {\n  display: grid;", Read("web", "wwwroot", "css", "bp-status.css"), StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void AClusterAdmin_ShouldLearnTheBatteryPagesActions()
+    {
+        var part = Part("look-after");
+
+        foreach (var words in new[] { "<dt>Live values</dt>", "<dt>Accident reports</dt>", "\"Add a report\"", "<dt>Change status</dt>", "Repaired, Prepared for re-use and placed on the market again, Remanufactured, Repurposed or Waste", "<dt>Add a note</dt>", "<dt>Hand over…</dt>", "<dt>Waiting for you</dt>", "Accept or Decline", "is read only" })
+        {
+            Assert.Contains(words, part, StringComparison.Ordinal);
+        }
+    }
+
+    [Fact]
+    public void MarketSurveillance_ShouldLearnToMarkAPassportInvalid()
+    {
+        var part = Part("msa");
+
+        Assert.Contains("<strong>Mark as invalid…</strong>", part, StringComparison.Ordinal);
+        Assert.Contains("only the team can undo it", part, StringComparison.Ordinal);
+    }
+
+    [Fact]
+    public void AVisitor_ShouldGetTheSmallPublicHelp()
+    {
+        // Signed out: Find a passport, What each reader sees and Statuses (the owner, Q1); printing and the API stay signed-in.
+        Assert.Contains("[AllowAnonymous]\n    [HttpGet(\"\")]\n    public IActionResult Guide() => View(\"Guide\");", Read("web", "Controllers", "HelpController.cs"), StringComparison.Ordinal);
+        foreach (var page in new[] { Guide, Contents })
+        {
+            var signedInOnly = page.IndexOf("@if (signedIn)", StringComparison.Ordinal);
+            Assert.True(signedInOnly >= 0);
+            Assert.True(signedInOnly < page.IndexOf(page == Guide ? "id=\"print\"" : "Part(\"print\")", StringComparison.Ordinal));
+            Assert.True(page.IndexOf("Statuses", StringComparison.Ordinal) < signedInOnly);
+        }
+
+        Assert.True(Guide.LastIndexOf("@if (signedIn)", StringComparison.Ordinal) < Guide.IndexOf("id=\"api\"", StringComparison.Ordinal));
+        Assert.True(Contents.LastIndexOf("@if (signedIn)", StringComparison.Ordinal) < Contents.IndexOf("<p>For systems</p>", StringComparison.Ordinal));
+        Assert.Contains("<a href=\"/help\">Help</a>\n                            <a href=\"/login\">Login</a>", Read("web", "Views", "Shared", "_Layout.cshtml"), StringComparison.Ordinal);
+    }
+}
diff --git a/BatteryPassWeb.Tests/Look/HelpTests.cs b/BatteryPassWeb.Tests/Look/HelpTests.cs
index 6a22b16..1f04744 100644
--- a/BatteryPassWeb.Tests/Look/HelpTests.cs
+++ b/BatteryPassWeb.Tests/Look/HelpTests.cs
@@ -57,13 +57,13 @@ public sealed class HelpTests(MongoFixture mongo)
     [Fact]
     public void TheTeamsPart_ShouldWalkFromProductDataToAPublishedPassport()
     {
-        var workflow = Guide[Guide.IndexOf("id=\"workflow\"", StringComparison.Ordinal)..Guide.IndexOf("id=\"words\"", StringComparison.Ordinal)];
+        var workflow = Guide[Guide.IndexOf("id=\"workflow\"", StringComparison.Ordinal)..Guide.IndexOf("id=\"lifecycle\"", StringComparison.Ordinal)];
 
         Assert.Contains("<a href=\"/admin/families\">Set up product data</a>", workflow, StringComparison.Ordinal);
         Assert.Contains("<a href=\"/admin/batteries/new\">Create the battery</a>", workflow, StringComparison.Ordinal);
         Assert.Contains("Only \"required by Feb 2027\" blocks publishing", workflow, StringComparison.Ordinal);
         Assert.Contains("checks the passport against the official schema, signs it and publishes it", workflow, StringComparison.Ordinal);
-        Assert.Contains("<dt>Archived version</dt>", Guide, StringComparison.Ordinal);
+        Assert.Contains("<h2>Statuses</h2>", Guide, StringComparison.Ordinal); // step 6b-1: every status, in place of "Status words"
         foreach (var gone in new[] { "Validate passport", "Sign passport", "Dirty", "Evidence readiness", "Trust &amp; conformance" })
         {
             Assert.DoesNotContain(gone, Guide, StringComparison.Ordinal);
@@ -78,7 +78,7 @@ public sealed class HelpTests(MongoFixture mongo)
         Assert.Contains("<a href=\"/cluster-admin/users\">Add your people</a>", part, StringComparison.Ordinal);
         Assert.Contains("<a href=\"/cluster-admin/api-tokens\">Give your systems a token</a>", part, StringComparison.Ordinal);
         Assert.Contains("it is shown once", part, StringComparison.Ordinal);
-        Assert.Contains("\"Manage\" opens a battery's page", part, StringComparison.Ordinal); // step 4a
+        Assert.Contains("a battery's icon opens its page", part, StringComparison.Ordinal); // the Registry's row icons (decision 37)
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/Look/StatusBadgeTests.cs b/BatteryPassWeb.Tests/Look/StatusBadgeTests.cs
index 1186cc5..23ab868 100644
--- a/BatteryPassWeb.Tests/Look/StatusBadgeTests.cs
+++ b/BatteryPassWeb.Tests/Look/StatusBadgeTests.cs
@@ -87,8 +87,11 @@ public sealed partial class StatusBadgeTests
         Assert.Contains("popovertarget=\"status-help\" aria-label=\"What do the two statuses mean?\"", help, StringComparison.Ordinal);
         Assert.Contains("<div id=\"status-help\" class=\"bp-status-help\" popover>", help, StringComparison.Ordinal);
         Assert.Contains("@if (!Context.Items.ContainsKey(\"status-help\"))", help, StringComparison.Ordinal);
-        Assert.Equal(5, help.Split("StatusBadge.Battery(").Length - 1);
-        Assert.Equal(6, help.Split("StatusBadge.Passport(").Length - 1);
+        // The statuses in words live in one shared list, also the Help's "Statuses" part (step 6b-1).
+        var meanings = Read("web", "Views", "Shared", "_StatusMeanings.cshtml");
+        Assert.Contains("@await Html.PartialAsync(\"_StatusMeanings\")", help, StringComparison.Ordinal);
+        Assert.Equal(5, meanings.Split("StatusBadge.Battery(").Length - 1);
+        Assert.Equal(6, meanings.Split("StatusBadge.Passport(").Length - 1);
     }
 
     [Fact]
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental` then `dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~HelpPerRoleTests|FullyQualifiedName~HelpTests|FullyQualifiedName~StatusBadgeTests"`
Expected: `Failed!  - Failed:     9, Passed:    23`: the 6 new tests and the 3 changed ones.

- [ ] **Step 3: The Help, the shared statuses list and the visitor's link**

Save as `../6b1-code.patch` and run `git apply ../6b1-code.patch`:

```diff
diff --git a/web/Views/Help/Guide.cshtml b/web/Views/Help/Guide.cshtml
index f2d55c7..58faa0a 100644
--- a/web/Views/Help/Guide.cshtml
+++ b/web/Views/Help/Guide.cshtml
@@ -1,11 +1,22 @@
 @using BatteryPassWeb.Services
 @{
     // The one Help place (step 3e): each reader sees only the parts for their role. The team's workflow used to be its
-    // own page under the admin tabs (/help/team and /admin/help now lead here).
+    // own page under the admin tabs (/help/team and /admin/help now lead here). Step 6b-1: a part for every kind of
+    // reader of design 5.2, the statuses in words, and printing.
     ViewData["Title"] = "Help";
     var signedIn = User.Identity?.IsAuthenticated == true;
     var isTeam = User.IsInRole(AccessControlService.RoleAdmin);
     var runsACluster = isTeam || User.IsInRole(AccessControlService.RoleClusterAdmin);
+    var inACluster = !isTeam && !runsACluster && User.IsInRole(AccessControlService.RoleNormalUser);
+    var isMsa = User.IsInRole(AccessControlService.RoleMarketSurveillanceAuthority);
+    var authorityOrInterest = new[]
+    {
+        (Role: AccessControlService.RoleNotifiedBody, Who: "Notified body", Sees: "the authorities' values: the economic operator's and manufacturer's IDs, the service provider and the test reports"),
+        (Role: AccessControlService.RoleCommission, Who: "Commission", Sees: "the shared values (materials, dismantling manuals, part numbers, spare parts, safety measures) and the authorities' values; not the purchaser's values"),
+        (Role: AccessControlService.RoleLiRepair, Who: "Repair, repurposing and remanufacturing", Sees: "the materials, the dismantling manuals, the part numbers, the spare parts sources and the safety measures"),
+        (Role: AccessControlService.RoleLiRecycling, Who: "Recycling and battery treatment", Sees: "the materials, the dismantling manuals and the safety measures"),
+        (Role: AccessControlService.RoleLiTreatment, Who: "Authorised treatment and WEEE facilities", Sees: "the safety measures"),
+    }.Where(role => User.IsInRole(role.Role)).ToArray();
 }
 <main class="bp-page bp-help-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
@@ -37,6 +48,59 @@
                 <p class="mt-3 mb-0">Signed in, the passport's detail page shows one section per group you belong to. Each value says when it was measured or entered, and how.</p>
             </section>
 
+            <section class="bp-card bp-help-part" id="statuses">
+                <h2>Statuses</h2>
+                <p>Every passport page shows two badges. <strong>Battery</strong> (square tag) says what has happened to the battery; only the battery's cluster, market surveillance and the team see it. <strong>Passport</strong> (round pill) says whether this passport is the one that counts; everyone sees it.</p>
+                @await Html.PartialAsync("_StatusMeanings")
+            </section>
+
+            @* Signed out, the Help is the three parts above (the owner, Q1 of step 6b). *@
+            @if (signedIn)
+            {
+                <section class="bp-card bp-help-part" id="print">
+                    <h2>Print a passport</h2>
+                    <p class="mb-0">Print the public page or the detail page from the browser (Ctrl+P), or save it as PDF. The paper copy starts with the QR code, the page's address and when it was printed, and shows every section open. Live values print as their latest readings.</p>
+                </section>
+            }
+
+            @if (inACluster)
+            {
+                <section class="bp-card bp-help-part" id="your-batteries">
+                    <h2>Your cluster's batteries <span class="bp-soft-tag">Cluster users</span></h2>
+                    <ol class="bp-steps">
+                        <li><strong><a href="/registry">The Registry</a></strong><p>Lists your cluster's batteries, each with its two badges. Its icons open the battery's public page and detail page.</p></li>
+                        <li><strong>The detail page</strong><p>The purchaser values, the repair history and the shared values. "Live values" shows the last week's readings as small charts; "Modules" lists the battery's modules.</p></li>
+                        <li><strong>Changes</strong><p>Your cluster admins correct live values, add accident reports, record repairs and hand batteries over. Ask them, or the manufacturer's team.</p></li>
+                    </ol>
+                </section>
+            }
+
+            @if (isMsa)
+            {
+                <section class="bp-card bp-help-part" id="msa">
+                    <h2>Market surveillance <span class="bp-soft-tag">Market surveillance</span></h2>
+                    <ol class="bp-steps">
+                        <li><strong><a href="/registry">Every published passport</a></strong><p>The Registry lists them all, invalid and marked ones included. The detail page shows every section: purchaser, shared and authorities' values, the live values and the modules.</p></li>
+                        <li><strong>Mark as invalid…</strong><p>On an active passport's detail page: give the reason, then confirm. The public page then says the passport is invalid, with the date. The reason is seen only by you and the manufacturer's team, and only the team can undo it. An invalid passport takes no new versions, status changes or live values.</p></li>
+                    </ol>
+                </section>
+            }
+
+            @if (authorityOrInterest.Length > 0)
+            {
+                <section class="bp-card bp-help-part" id="your-access">
+                    <h2>What your account adds</h2>
+                    <dl class="bp-word-list">
+                        @foreach (var role in authorityOrInterest)
+                        {
+                            <dt>@role.Who</dt>
+                            <dd>Besides the public values: @role.Sees.</dd>
+                        }
+                    </dl>
+                    <p class="mt-3 mb-0">The <a href="/registry">Registry</a> lists every published passport; open its detail page to see these values. The manufacturer's team gives this access to named accounts.</p>
+                </section>
+            }
+
             @if (signedIn && !runsACluster)
             {
                 <section class="bp-card bp-help-part" id="access">
@@ -53,9 +117,29 @@
                         <li><strong><a href="/cluster-admin/users">Add your people</a></strong><p>Under Cluster admin, Users: add someone as a user (sees your cluster's batteries) or as a cluster admin (also looks after its users and tokens).</p></li>
                         <li><strong><a href="/cluster-admin/api-tokens">Give your systems a token</a></strong><p>Under Cluster admin, API tokens: "Read" reads your batteries' passports; "Read + write" also sends their live values. Copy the value when it is shown: it is shown once.</p></li>
                         <li><strong><a href="/registry">Check what they see</a></strong><p>Your users find your cluster's batteries in the Registry, with the purchaser values on the detail page.</p></li>
-                        <li><strong><a href="/registry">Look after your batteries</a></strong><p>In the Registry, "Manage" opens a battery's page: correct its live values and add accident reports. Its modules and versions are shown there too; the team keeps the rest.</p></li>
                     </ol>
                 </section>
+
+                <section class="bp-card bp-help-part" id="look-after">
+                    <h2>Look after a battery <span class="bp-soft-tag">Cluster admins</span></h2>
+                    <p>In the <a href="/registry">Registry</a>, a battery's icon opens its page; its side menu has one section at a time.</p>
+                    <dl class="bp-word-list">
+                        <dt>Live values</dt>
+                        <dd>Correct the state of charge, cycles, temperature and the extreme-temperature times. The battery system usually sends them.</dd>
+                        <dt>Accident reports</dt>
+                        <dd>"Add a report" uploads one; the passport's accident page lists them for its readers.</dd>
+                        <dt>Change status</dt>
+                        <dd>"What happened?": Repaired, Prepared for re-use and placed on the market again, Remanufactured, Repurposed or Waste. The window asks for the re-stated fades and lifetime, filled in with the current ones. A repair keeps the passport and adds to its repair history. The three that place the battery on the market again lead to a new linked passport, from Scania or another operator. Waste is final.</dd>
+                        <dt>Add a note</dt>
+                        <dd>A correction to the repair history; nothing in the history is changed or removed.</dd>
+                        <dt>Hand over…</dt>
+                        <dd>Type the receiving cluster's ID; the app shows its name to confirm. Until its admins accept on their Registry's "Waiting for you" card, the battery stays yours. Afterwards your cluster no longer sees its purchaser values.</dd>
+                        <dt>Waiting for you</dt>
+                        <dd>A battery another cluster hands to you: Accept or Decline.</dd>
+                    </dl>
+                    <p class="mt-3 mb-0">A passport that is invalid, marked for deletion or replaced by a new passport is read only.</p>
+                </section>
+
                 <section class="bp-card bp-help-part" id="upload">
                     <h2>Upload a file <span class="bp-soft-tag">Team and cluster admins</span></h2>
                     <p>On a battery's page, "Upload a file" takes one JSON file for that battery: the app's upload format, or the battery system's export as it is. You see what would change before anything is saved. A marked or earlier passport takes no file.</p>
@@ -80,15 +164,19 @@
                     <p class="mt-3 mb-0">Other systems get <a href="/admin/clusters?tab=api-token-management">API tokens</a>: a cluster token reads that cluster's batteries and, with "Read + write", sends their live values; only a token for all batteries creates and publishes them.</p>
                 </section>
 
-                <section class="bp-card bp-help-part" id="words">
-                    <h2>Status words <span class="bp-soft-tag">Team</span></h2>
+                <section class="bp-card bp-help-part" id="lifecycle">
+                    <h2>After publishing <span class="bp-soft-tag">Team</span></h2>
                     <dl class="bp-word-list">
-                        <dt>Draft</dt>
-                        <dd>Not published yet; only the team sees it, on the Batteries tab.</dd>
-                        <dt>Published</dt>
-                        <dd>Its current version is public and signed. Public search and the QR code open only published passports.</dd>
-                        <dt>Archived version</dt>
-                        <dd>An earlier version, kept with its signature when a newer one was published.</dd>
+                        <dt>Everything a cluster admin does</dt>
+                        <dd>Live values, accident reports, status changes, notes and uploads, on any battery. Only the team gets "Correct the values" in the status window: the status stays, and a new version is published.</dd>
+                        <dt>A new passport</dt>
+                        <dd>After "placed on the market again", "Remanufactured" or "Repurposed", the battery page asks who places it on the market. Scania's new passport starts from the old battery as a draft; publishing it makes the old one inactive. Another operator's passport is linked by its ID or web address. Until then, the team can cancel the new passport.</dd>
+                        <dt>More: Mark as invalid or for deletion…</dt>
+                        <dd>Invalid, with a reason, for an active passport; for deletion, for a waste battery after recycling. The header then shows the reason and the one Undo.</dd>
+                        <dt>Cluster: Change</dt>
+                        <dd>Moves a battery to another cluster at once, logged; or cancel a waiting hand-over.</dd>
+                        <dt><a href="/admin/clusters?tab=clusters">Demo data</a></dt>
+                        <dd>At the end of the Clusters tab: "Reset demo data" makes the demo clusters, accounts, tokens and batteries again; "Clear demo data" removes them and keeps real data.</dd>
                     </dl>
                 </section>
 
@@ -103,11 +191,14 @@
                 </section>
             }
 
-            <section class="bp-card bp-help-part" id="api">
-                <h2>API reference and workbench</h2>
-                <p class="mb-2">Every call, with examples for the demo clusters (<code>demo-north</code>, <code>demo-south</code>, <code>demo-fleet</code>), and a workbench to try a call with a token.</p>
-                <a class="bp-secondary-button bp-button-compact" href="/help/api">Open the API reference</a>
-            </section>
+            @if (signedIn)
+            {
+                <section class="bp-card bp-help-part" id="api">
+                    <h2>API reference and workbench</h2>
+                    <p class="mb-2">Every call, with examples for the demo clusters (<code>demo-north</code>, <code>demo-south</code>, <code>demo-fleet</code>), and a workbench to try a call with a token.</p>
+                    <a class="bp-secondary-button bp-button-compact" href="/help/api">Open the API reference</a>
+                </section>
+            }
         </div>
     </div>
 </main>
diff --git a/web/Views/Shared/_HelpContents.cshtml b/web/Views/Shared/_HelpContents.cshtml
index 12341d8..d1ba079 100644
--- a/web/Views/Shared/_HelpContents.cshtml
+++ b/web/Views/Shared/_HelpContents.cshtml
@@ -7,12 +7,36 @@
     var signedIn = User.Identity?.IsAuthenticated == true;
     var isTeam = User.IsInRole(AccessControlService.RoleAdmin);
     var runsACluster = isTeam || User.IsInRole(AccessControlService.RoleClusterAdmin);
+    var inACluster = !isTeam && !runsACluster && User.IsInRole(AccessControlService.RoleNormalUser);
+    var isMsa = User.IsInRole(AccessControlService.RoleMarketSurveillanceAuthority);
+    var addsAccess = new[] { AccessControlService.RoleNotifiedBody, AccessControlService.RoleCommission, AccessControlService.RoleLiRepair, AccessControlService.RoleLiRecycling, AccessControlService.RoleLiTreatment }.Any(User.IsInRole);
     string Part(string id) => onGuide ? $"#{id}" : $"/help#{id}";
 }
 <nav class="bp-help-contents" aria-label="Help contents">
     <p>For everyone</p>
     <a href="@Part("find")">Find a passport</a>
     <a href="@Part("see")">What each reader sees</a>
+    <a href="@Part("statuses")">Statuses</a>
+    @if (signedIn)
+    {
+        <a href="@Part("print")">Print a passport</a>
+    }
+    @if (inACluster || isMsa || addsAccess)
+    {
+        <p>For you</p>
+        @if (inACluster)
+        {
+            <a href="@Part("your-batteries")">Your cluster's batteries</a>
+        }
+        @if (isMsa)
+        {
+            <a href="@Part("msa")">Market surveillance</a>
+        }
+        @if (addsAccess)
+        {
+            <a href="@Part("your-access")">What your account adds</a>
+        }
+    }
     @if (signedIn && !runsACluster)
     {
         <a href="@Part("access")">Need more access?</a>
@@ -21,14 +45,19 @@
     {
         <p>For cluster admins</p>
         <a href="@Part("cluster")">Your cluster</a>
+        <a href="@Part("look-after")">Look after a battery</a>
+        <a href="@Part("upload")">Upload a file</a>
     }
     @if (isTeam)
     {
         <p>For the team</p>
         <a href="@Part("workflow")">From product data to a published passport</a>
-        <a href="@Part("words")">Status words</a>
+        <a href="@Part("lifecycle")">After publishing</a>
         <a href="@Part("ready")">Before you call a battery ready</a>
     }
-    <p>For systems</p>
-    <a href="/help/api" aria-current="@(onGuide ? null : "page")">API reference and workbench</a>
+    @if (signedIn)
+    {
+        <p>For systems</p>
+        <a href="/help/api" aria-current="@(onGuide ? null : "page")">API reference and workbench</a>
+    }
 </nav>
diff --git a/web/Views/Shared/_Layout.cshtml b/web/Views/Shared/_Layout.cshtml
index cf64302..fb07aa3 100644
--- a/web/Views/Shared/_Layout.cshtml
+++ b/web/Views/Shared/_Layout.cshtml
@@ -171,6 +171,8 @@
                         }
                         else
                         {
+                            @* Visitors get the small public Help too, beside Login so it shows on a phone as well (step 6b-1, the owner's Q1). *@
+                            <a href="/help">Help</a>
                             <a href="/login">Login</a>
                         }
                         </div>
diff --git a/web/Views/Shared/_StatusHelp.cshtml b/web/Views/Shared/_StatusHelp.cshtml
index 87b0d8d..2371aaa 100644
--- a/web/Views/Shared/_StatusHelp.cshtml
+++ b/web/Views/Shared/_StatusHelp.cshtml
@@ -10,19 +10,7 @@
     <div id="status-help" class="bp-status-help" popover>
         <h2>Two statuses</h2>
         <p class="mb-0"><strong>Battery</strong> (square tag) says what has happened to the battery. <strong>Passport</strong> (round pill) says whether this passport is the one that counts.</p>
-        <dl>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("original"))</dt><dd>As made; never repaired or reused.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("re-used"))</dt><dd>Back in use for its first purpose: after a repair (same passport), or prepared for re-use and placed on the market again (new passport).</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("repurposed"))</dt><dd>Now used for a different purpose. It gets a new passport.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("remanufactured"))</dt><dd>Rebuilt to work as new. It gets a new passport.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("waste"))</dt><dd>Taken out of use for recycling. Final; the team can then mark the passport for deletion.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Active))</dt><dd>The passport that counts now.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Archived))</dt><dd>An older version of this passport; a newer one counts.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Inactive))</dt><dd>Replaced by the battery's new passport after it was placed on the market again.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Invalid))</dt><dd>Its data shouldn't be relied on, for example after a market surveillance finding. Only the team undoes it.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.MarkedForDeletion))</dt><dd>The battery is waste and gone; the passport will be deleted.</dd>
-            <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(null))</dt><dd>Not published yet; only the team sees it.</dd>
-        </dl>
+        @await Html.PartialAsync("_StatusMeanings")
         <div class="d-flex justify-content-end"><button type="button" class="bp-secondary-button" popovertarget="status-help" popovertargetaction="hide">Close</button></div>
     </div>
 }
diff --git a/web/Views/Shared/_StatusMeanings.cshtml b/web/Views/Shared/_StatusMeanings.cshtml
new file mode 100644
index 0000000..99dccc9
--- /dev/null
+++ b/web/Views/Shared/_StatusMeanings.cshtml
@@ -0,0 +1,16 @@
+@using BatteryPassWeb.Dpp.Pages
+@using BatteryPassWeb.Dpp.Store
+@* Every Battery and Passport status in words, once: the badges' "What do these mean?" window and the Help's "Statuses" part (step 6b-1). *@
+<dl class="bp-status-meanings">
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("original"))</dt><dd>As made; never repaired or reused.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("re-used"))</dt><dd>Back in use for its first purpose: after a repair (same passport), or prepared for re-use and placed on the market again (new passport).</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("repurposed"))</dt><dd>Now used for a different purpose. It gets a new passport.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("remanufactured"))</dt><dd>Rebuilt to work as new. It gets a new passport.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Battery("waste"))</dt><dd>Taken out of use for recycling. Final; the team can then mark the passport for deletion.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Active))</dt><dd>The passport that counts now.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Archived))</dt><dd>An older version of this passport; a newer one counts.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Inactive))</dt><dd>Replaced by the battery's new passport after it was placed on the market again.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.Invalid))</dt><dd>Its data shouldn't be relied on, for example after a market surveillance finding. Only the team undoes it.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(DppStatuses.MarkedForDeletion))</dt><dd>The battery is waste and gone; the passport will be deleted.</dd>
+    <dt>@await Html.PartialAsync("_StatusBadge", StatusBadge.Passport(null))</dt><dd>Not published yet; only the team sees it.</dd>
+</dl>
diff --git a/web/wwwroot/css/bp-status.css b/web/wwwroot/css/bp-status.css
index 8445da6..f8dd5fe 100644
--- a/web/wwwroot/css/bp-status.css
+++ b/web/wwwroot/css/bp-status.css
@@ -163,7 +163,8 @@ th .bp-status-help-words {
   font-size: 1.2rem;
 }
 
-.bp-status-help dl {
+/* The statuses in words: in the badges' explainer window and in the Help's "Statuses" part (step 6b-1). */
+.bp-status-meanings {
   display: grid;
   grid-template-columns: max-content minmax(0, 1fr);
   gap: 8px 14px;
@@ -171,7 +172,7 @@ th .bp-status-help-words {
   margin: 12px 0 16px;
 }
 
-.bp-status-help dd {
+.bp-status-meanings dd {
   margin: 0;
   font-size: 0.9rem;
 }
```

- [ ] **Step 4: Run the tests and the classes that read these files**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then
`dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~AdminDenseConsoleLayoutTests|FullyQualifiedName~AdminHeadingTests|FullyQualifiedName~ApplicationAuditCoverageTests|FullyQualifiedName~BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPageLayoutTests|FullyQualifiedName~CopyIdButtonLayoutTests|FullyQualifiedName~DataRequirementsAdminLayoutTests|FullyQualifiedName~ExternalApiHashOnlyTokenTests|FullyQualifiedName~HelpPerRoleTests|FullyQualifiedName~HelpTests|FullyQualifiedName~HelpWorkbenchLayoutTests|FullyQualifiedName~HeroButtonIconsTests|FullyQualifiedName~MobileThemeSessionLayoutTests|FullyQualifiedName~PassportHeroTests|FullyQualifiedName~PrintStyleTests|FullyQualifiedName~RegistryBadgesTests|FullyQualifiedName~ReviewFixesTests|FullyQualifiedName~SiteShellLayoutTests|FullyQualifiedName~StatusBadgePagesTests|FullyQualifiedName~StatusBadgeTests|FullyQualifiedName~StatusHelpLinkTests|FullyQualifiedName~StatusUploadTests|FullyQualifiedName~TestPackTests"`
Expected: `Passed!  - Failed:     0, Passed:   161`.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Look/HelpPerRoleTests.cs BatteryPassWeb.Tests/Look/HelpTests.cs BatteryPassWeb.Tests/Look/StatusBadgeTests.cs web/Views/Help/Guide.cshtml web/Views/Shared/_HelpContents.cshtml web/Views/Shared/_StatusHelp.cshtml web/Views/Shared/_StatusMeanings.cshtml web/Views/Shared/_Layout.cshtml web/wwwroot/css/bp-status.css
git commit -m "feat(dpp): the Help for every reader: a part per role, the statuses in words, printing, and a small Help for visitors" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2534, Skipped:     0, Total:  2534`. That is the 2528 from before plus 6 new tests.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat f27d58f -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings them to the product owner:

1. Restart the app with the usual launcher and press "Reset demo data".
2. Signed out, the top bar has "Help" beside "Login". `/help` shows "Find a passport", "What each reader sees" and "Statuses".
3. Signed in as each demo account in turn (`docs/sample-cluster-test-accounts.md`), `/help` shows that reader's parts, as in the approved shots.
4. On a battery's page, "What do these mean?" beside the badges shows the same list as Help's "Statuses".

## Next

6c, the role-matrix check, with `docs/end-user-testing-guide.md` as its script.
