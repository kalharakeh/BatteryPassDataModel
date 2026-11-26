# DPP V3 Step 3e: A Cleaner Look — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The admin and cluster admin pages get a cleaner, friendlier look and layout, with decision 27's finish: the Clusters, Users, API tokens and Audit log tabs, and the cluster admins' Users and API tokens pages. The Help becomes one place that shows each reader only the parts for their role (design section 14, the 3e bullet). Every button and form that works today keeps working.

**Architecture:** Views, one shared script and one stylesheet section. No new pages, routes or data.
- **The finish:** one new section at the end of `web/wwwroot/css/bp-product-data.css`, the approved mock-up's own style block (`site.css` is not changed, decision 27).
- **The pattern on every tab:**
  - an intro card with the tab's one main button, which opens a native `<dialog>` window;
  - a list card whose rows are `<details>` elements that open in place, the risky actions last;
  - rare settings in a small card at the end.
- **Shared pieces:**
  - `wwwroot/js/bp-admin-pages.js`: windows, "Please confirm", password eye, list search, reopening a row after a save. It replaces the pages' inline scripts.
  - `_ConfirmModal.cshtml` and `_PasswordReveal.cshtml`.
  - `_ApiTokenList.cshtml` with `ApiTokenListViewModel`: one token list for the team's and the cluster admins' pages.
  - `_HelpContents.cshtml`: the Help's contents list, filtered by role.
- **Headings:** `_AdminTabs` and `_ClusterAdminTabs` carry their pages' heading, so it is written once.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, section 14, the 3e bullet. Decisions record: decisions 23, 27, 30 and 32. Mock-up approved by the product owner on 30 Oct 2026: `docs/mockups/dpp-v3-step3e-cleaner-look.html` (committed with this plan).

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at da214a2 (the end of plan 3d-3's run), plus the commit that adds this plan and its mock-up. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo).
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2078`. After this plan: 2098. The suite takes about two minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3e-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (30 Oct 2026, through the managing session)

1. **The mock-up is approved**, with the screens as drawn.
2. **Help leaves the admin tabs.** It stays in the top bar for everyone and opens the one Help page. `/help/team` and `/admin/help` lead to the team's part of it.
3. **The Demo data card goes to the end of the Clusters tab**, so real clusters come first.
4. **The two bulk token buttons stay**, under "More: tokens for every cluster at once".
5. **The API reference stays open to everyone**, signed in or not. Its example battery ID now comes only from a published demo battery, and there is none without demo data. The help is public, so it never shows a real battery's ID.
6. **The demo tokens' names** lose the doubled "Demo" ("Demo North Operations (read)", not "Demo Demo North Operations (read)"). There is a test for it.
7. **The tabs** are renamed "Clusters" (was "Registered clusters") and "API tokens" (was "API Token Management"), in the order of the work: Batteries, Battery families, Sites and operators, Clusters, Users, API tokens, Audit log.
8. **A cluster admin's "delete" stays a switch-off**, with honest words: "Deactivate token". Only the team deletes a token or switches it back on.
9. **The demo data survives an app restart** (folded into Task 4, both parts, with tests):
   - The seed's cluster tokens are the cluster's own (`autoClusterId`), so the app's start adds no others.
   - Clear and Reset also remove a demo cluster's own tokens with it, and don't count them as real use. This repairs databases seeded and restarted before this plan.

## Found while prototyping, and what this plan does

- **The cluster admins' "Edit profile" and "Change password" saved nothing.** Those two forms sent no cluster, so the save action redirected without a word. Every cluster admin save also re-saves a membership with the role it is sent, so adding just the cluster would have made cluster admins into users. Task 5's forms send the person's existing membership in the admin's clusters, cluster and role. No controller change.
- **A cluster admin's "Delete token" only switches the token off.** Their action deactivates it, keeps it, and then said "API token deleted."; only the team can switch it on again. Task 5 keeps the behaviour and makes the words true: "Deactivate token", and the message "API token deactivated."
- **The token form's small print was wrong.** It said "Passport Lifecycle" creates batteries. It can't: Read + write creates them (with Global access) and Passport Lifecycle publishes them. The new window says what each permission does.
- **Already done before this plan:** the "North telemetry integration" placeholder was gone. "Demo API Cluster" was not in the help: its API examples named `demo-cluster`, which the reset deleted. Task 6 uses `demo-north`.
- **Clearing the demo data after an app restart kept the demo clusters (3d-3).** At its start the app gives every cluster without them a "- read" and a "- readwrite" token of its own (`autoClusterId`), unmarked. The seed's cluster tokens weren't those, so after a restart each demo cluster got two more, and "Clear demo data" kept the three clusters as used by real tokens. Task 4 fixes it (decision 9 above). One case it can't undo: a Clear or Reset pressed after a restart *before* this plan already took the demo mark off the three clusters, so they count as real from then on. "After this plan" says how to tidy that by hand.

## What happens to the old tests

The layout tests of the pages replaced here read the views as text, so they pinned the old markup. Each task updates the assertions that describe what changed and keeps the rest. Where a test only described the replaced markup, it goes and the task's new tests in `BatteryPassWeb.Tests/Look/` cover the new one:
- Task 2: `AdminRegisteredClusters_ShouldUseDenseProductionTableWithoutMockupTabStyles` and `RegisteredClusters_ShouldExposeUserOverviewDropdownAndCreateUserCopy` (the old clusters table).
- Task 3: `AdminUsers_ShouldUseDenseMoreMenuTableForMultiClusterAssignments` (the old users table).
- Task 4: `AdminApiTokenManagement_ShouldUseDenseTokenConsoleLayout` (the old token table).
- Task 6: `AdminHelpPageTests.cs`, nine tests of the old team help page, which goes, and `AdminHelp_ShouldUsePersistentAdminTabsAndLighterWorkflowIntro`. `HelpTests` carries over what still matters: publishing checks and signs in one step, "required by Feb 2027", archived versions and the links.

So the whole suite's count goes up in Tasks 1 to 5 and down in Task 6.


---

### Task 1: One heading, the tabs in the order of the work, and the calmer finish

The admin pages' heading moves into `_AdminTabs`, with one line that stays true; the five pages that repeated it (with lines naming passport snapshots and other things that are gone) lose their copy. The tabs are renamed and put in the order of the work, and Help leaves them. This step's styles go at the end of `bp-product-data.css`, and the Clusters page loads that stylesheet. Until Task 6, the old team help page still shows the admin tabs, without one marked.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Look/AdminHeadingTests.cs`
- Replace: `web/Views/Shared/_AdminTabs.cshtml`
- Modify: `web/Views/Admin/AuditLog.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/Views/Batteries/Batteries.cshtml`
- Modify: `web/Views/ProductData/Families.cshtml`
- Modify: `web/Views/ProductData/SitesAndOperators.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `3e-task1-tests.patch` (`BatteryPassWeb.Tests/AdminHelpPageTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`). Save the block as `../3e-task1-tests.patch`, outside the worktree, and run `git apply ../3e-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminHelpPageTests.cs b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
--- a/BatteryPassWeb.Tests/AdminHelpPageTests.cs
+++ b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
@@ -38,12 +38,11 @@ public sealed class AdminHelpPageTests
         var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
         var tabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
 
-        Assert.Contains("bp-console-header", markup);
         Assert.Contains("_AdminTabs", markup);
         Assert.Contains("Administration tabs", tabs);
         Assert.Contains("bp-tab-active", tabs);
         Assert.Contains("href=\"/admin/batteries\"", tabs);
-        Assert.Contains("href=\"/help/team\"", tabs);
+        Assert.DoesNotContain("/help", tabs); // Help is in the top bar (step 3e)
         Assert.DoesNotContain("Back to main admin page", markup);
         Assert.DoesNotContain("bp-page-return-row", markup);
         Assert.DoesNotContain("bp-page-return-link", markup);
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -74,7 +74,7 @@ public sealed class ApplicationAuditCoverageTests
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.DoesNotContain("Back to batteries", globalAuditView);
-        Assert.Contains("<h1 class=\"mb-1\">Administration</h1>", globalAuditView);
+        Assert.DoesNotContain("<h1", globalAuditView); // the heading comes with _AdminTabs (step 3e)
         Assert.Contains("_AdminTabs", globalAuditView);
         Assert.Contains("_AuditLine", globalAuditView);
         Assert.DoesNotContain("bp-ledger-event ", globalAuditView);
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -85,7 +85,7 @@ public sealed class MobileThemeSessionLayoutTests
         var adminTabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("<option value=\"/help/team\"", adminTabs);
+        Assert.DoesNotContain("/help", adminTabs); // Help is in the top bar (step 3e)
         Assert.Contains(".bp-battery-toolbar-actions", css);
         Assert.Contains(".bp-battery-toolbar-help", css);
         Assert.Contains("display: none;", css);
```

`BatteryPassWeb.Tests/Look/AdminHeadingTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: one admin heading that stays true, the tabs in the order of the work, Help in the top bar only, and
/// the calmer finish in the team pages' stylesheet (decision 27; site.css is not changed).</summary>
public sealed class AdminHeadingTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static readonly string Tabs = Read("web", "Views", "Shared", "_AdminTabs.cshtml");

    private static readonly string[] AdminPages =
    [
        "Admin/AuditLog.cshtml",
        "Admin/Clusters.cshtml",
        "Batteries/Batteries.cshtml",
        "ProductData/Families.cshtml",
        "ProductData/SitesAndOperators.cshtml",
    ];

    [Fact]
    public void Heading_ShouldBeSharedByEveryAdminTab()
    {
        Assert.Contains("<h1>Administration</h1>", Tabs, StringComparison.Ordinal);
        Assert.Contains("<p>Batteries, product data, clusters, users and API tokens.</p>", Tabs, StringComparison.Ordinal);

        foreach (var page in AdminPages)
        {
            var markup = Read(["web", "Views", .. page.Split('/')]);
            Assert.Contains("_AdminTabs", markup, StringComparison.Ordinal);
            Assert.DoesNotContain(">Administration</h1>", markup, StringComparison.Ordinal);
            Assert.DoesNotContain("passport snapshots", markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Manage batteries", markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Manage battery families", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Tabs_ShouldFollowTheWork()
    {
        var order = new[]
        {
            "href=\"/admin/batteries\">Batteries</a>",
            "href=\"/admin/families\">Battery families</a>",
            "href=\"/admin/sites-and-operators\">Sites and operators</a>",
            "href=\"/admin/clusters?tab=clusters\">Clusters</a>",
            "href=\"/admin/clusters?tab=users\">Users</a>",
            "href=\"/admin/clusters?tab=api-token-management\">API tokens</a>",
            "href=\"/admin/audit\">Audit log</a>",
        };

        var positions = order.Select(tab => Tabs.IndexOf(tab, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order(), positions);
        Assert.DoesNotContain("Registered clusters", Tabs, StringComparison.Ordinal);
        Assert.DoesNotContain("API Token Management", Tabs, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_ShouldBeInTheTopBarOnly()
    {
        Assert.DoesNotContain("/help", Tabs, StringComparison.Ordinal);
        Assert.DoesNotContain(">Help<", Tabs, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/help\">Help</a>", Read("web", "Views", "Shared", "_Layout.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Finish_ShouldLiveInTheTeamStylesheet()
    {
        var finish = Read("web", "wwwroot", "css", "bp-product-data.css");
        var site = Read("web", "wwwroot", "css", "site.css");

        Assert.Contains("/* ==== Admin, cluster admin and help pages (step 3e)", finish, StringComparison.Ordinal);
        foreach (var rule in new[] { ".bp-admin-heading h1", ".bp-tab-intro", ".bp-soft-tag", ".bp-row > summary", ".bp-row-panel", ".bp-danger-zone", ".bp-help-layout" })
        {
            Assert.Contains(rule, finish, StringComparison.Ordinal);
            Assert.DoesNotContain(rule, site, StringComparison.Ordinal);
        }

        Assert.Contains("~/css/bp-product-data.css", Read("web", "Views", "Admin", "Clusters.cshtml"), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.AdminHeadingTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Failed:     7, Passed:    23`: the four new `AdminHeadingTests`, and three updated tests that no longer expect Help among the admin tabs or the audit page's own heading.

- [ ] **Step 3: Replace `_AdminTabs.cshtml`**

`web/Views/Shared/_AdminTabs.cshtml`:

```cshtml
@model string
@{
    // The admin pages' heading and tabs, in the order of the work: batteries and their product data, then clusters,
    // people and tokens, then the log. Help is in the top bar for everyone, so it has no tab here.
    var activeTab = Model ?? string.Empty;
    string TabClass(string tab) => activeTab.Equals(tab, StringComparison.OrdinalIgnoreCase) ? "bp-tab bp-tab-active" : "bp-tab";
    bool IsSelected(string tab) => activeTab.Equals(tab, StringComparison.OrdinalIgnoreCase);
}

<div class="bp-admin-heading">
    <h1>Administration</h1>
    <p>Batteries, product data, clusters, users and API tokens.</p>
</div>

<form class="bp-admin-mobile-tab-form mt-4" aria-label="Administration section">
    <label for="bp-admin-mobile-tab-select">Admin section</label>
    <select id="bp-admin-mobile-tab-select" data-admin-mobile-tab-select>
        <option value="/admin/batteries" selected="@IsSelected("batteries")">Batteries</option>
        <option value="/admin/families" selected="@IsSelected("products")">Battery families</option>
        <option value="/admin/sites-and-operators" selected="@IsSelected("sites-and-operators")">Sites and operators</option>
        <option value="/admin/clusters?tab=clusters" selected="@IsSelected("clusters")">Clusters</option>
        <option value="/admin/clusters?tab=users" selected="@IsSelected("users")">Users</option>
        <option value="/admin/clusters?tab=api-token-management" selected="@IsSelected("api-token-management")">API tokens</option>
        <option value="/admin/audit" selected="@IsSelected("audit")">Audit log</option>
    </select>
</form>

<nav class="bp-tab-row mt-4" aria-label="Administration tabs">
    <a class="@TabClass("batteries")" href="/admin/batteries">Batteries</a>
    <a class="@TabClass("products")" href="/admin/families">Battery families</a>
    <a class="@TabClass("sites-and-operators")" href="/admin/sites-and-operators">Sites and operators</a>
    <a class="@TabClass("clusters")" href="/admin/clusters?tab=clusters">Clusters</a>
    <a class="@TabClass("users")" href="/admin/clusters?tab=users">Users</a>
    <a class="@TabClass("api-token-management")" href="/admin/clusters?tab=api-token-management">API tokens</a>
    <a class="@TabClass("audit")" href="/admin/audit">Audit log</a>
</nav>
```

- [ ] **Step 4: Move the heading into the tabs, reorder them and add the finish**

Patch `3e-task1-code.patch` (`web/Views/Admin/AuditLog.cshtml`, `web/Views/Admin/Clusters.cshtml`, `web/Views/Admin/Help.cshtml`, `web/Views/Batteries/Batteries.cshtml`, `web/Views/ProductData/Families.cshtml`, `web/Views/ProductData/SitesAndOperators.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../3e-task1-code.patch`, outside the worktree, and run `git apply ../3e-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Admin/AuditLog.cshtml b/web/Views/Admin/AuditLog.cshtml
--- a/web/Views/Admin/AuditLog.cshtml
+++ b/web/Views/Admin/AuditLog.cshtml
@@ -17,11 +17,6 @@
 
 <main class="bp-page bp-admin-page bp-audit-log">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
-    <div>
-        <h1 class="mb-1">Administration</h1>
-        <p class="text-secondary mb-0">Manage battery families, sites and operators, batteries, cluster assignments, registered clusters, user memberships, credentials, and audit history.</p>
-    </div>
-
     @await Html.PartialAsync("_AdminTabs", "audit")
 
     <svg width="0" height="0" class="position-absolute" aria-hidden="true" focusable="false">
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -92,11 +92,7 @@
     }
 }
 <main class="bp-page bp-admin-page">
-    <div>
-        <h1 class="mb-1">Administration</h1>
-        <p class="text-secondary mb-0">Manage batteries, passport snapshots, battery families, cluster assignments, registered clusters, user memberships, credentials, and audit history from one page.</p>
-    </div>
-
+    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
     @await Html.PartialAsync("_AdminTabs", selectedTab)
 
     @if (!string.IsNullOrWhiteSpace(Model.StatusMessage))
diff --git a/web/Views/Admin/Help.cshtml b/web/Views/Admin/Help.cshtml
--- a/web/Views/Admin/Help.cshtml
+++ b/web/Views/Admin/Help.cshtml
@@ -5,12 +5,6 @@
 }
 
 <main class="bp-page bp-admin-page bp-admin-help-page">
-    <header class="bp-console-header">
-        <div>
-            <h1>Administration</h1>
-            <p class="text-secondary mb-0">Set up product data, create batteries and publish their passports.</p>
-        </div>
-    </header>
 
     @await Html.PartialAsync("_AdminTabs", "help")
     @await Html.PartialAsync("_HelpTabs", "team")
diff --git a/web/Views/Batteries/Batteries.cshtml b/web/Views/Batteries/Batteries.cshtml
--- a/web/Views/Batteries/Batteries.cshtml
+++ b/web/Views/Batteries/Batteries.cshtml
@@ -6,11 +6,6 @@
 
 <main class="bp-page bp-admin-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
-    <div>
-        <h1 class="mb-1">Administration</h1>
-        <p class="text-secondary mb-0">Manage batteries, battery families, sites and operators, registered clusters, user memberships, credentials, and audit history.</p>
-    </div>
-
     @await Html.PartialAsync("_AdminTabs", "batteries")
     @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
 
diff --git a/web/Views/ProductData/Families.cshtml b/web/Views/ProductData/Families.cshtml
--- a/web/Views/ProductData/Families.cshtml
+++ b/web/Views/ProductData/Families.cshtml
@@ -6,11 +6,6 @@
 
 <main class="bp-page bp-admin-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
-    <div>
-        <h1 class="mb-1">Administration</h1>
-        <p class="text-secondary mb-0">Manage battery families, sites and operators, batteries, cluster assignments, registered clusters, user memberships, credentials, and audit history.</p>
-    </div>
-
     @await Html.PartialAsync("_AdminTabs", "products")
     @await Html.PartialAsync("_Messages")
 
diff --git a/web/Views/ProductData/SitesAndOperators.cshtml b/web/Views/ProductData/SitesAndOperators.cshtml
--- a/web/Views/ProductData/SitesAndOperators.cshtml
+++ b/web/Views/ProductData/SitesAndOperators.cshtml
@@ -8,11 +8,6 @@
 
 <main class="bp-page bp-admin-page">
     <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
-    <div>
-        <h1 class="mb-1">Administration</h1>
-        <p class="text-secondary mb-0">Manage battery families, sites and operators, batteries, cluster assignments, registered clusters, user memberships, credentials, and audit history.</p>
-    </div>
-
     @await Html.PartialAsync("_AdminTabs", "sites-and-operators")
     @if (back is not null)
     {
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2035,3 +2035,606 @@ main .bp-field-marker.is-published {
 [data-theme="dark"] :is(.bp-module-edit summary, .bp-passport-modules small) {
   color: var(--bp-muted);
 }
+
+/* ==== Admin, cluster admin and help pages (step 3e) ================================================================
+   Decision 27's finish on the remaining admin tabs, the cluster admin pages and the help: one card per thing, a short
+   intro with the tab's one main button, lists whose rows open in place, outlined sentence-case tags, dialogs for
+   adding things, and the rare or risky actions at the end of a row's panel. */
+
+/* The page heading: one title, one line. */
+.bp-admin-heading h1 {
+  margin: 0 0 4px;
+  font-size: 2rem;
+  font-weight: 650;
+}
+
+.bp-admin-heading p {
+  margin: 0;
+  color: var(--bp-muted);
+}
+
+/* A tab's intro card: what it is for, its numbers, and its one main button. */
+.bp-tab-intro {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 14px 28px;
+}
+
+.bp-tab-intro h2 {
+  margin: 0 0 4px;
+  font-size: 1.25rem;
+  font-weight: 650;
+}
+
+.bp-tab-intro p {
+  max-width: 760px;
+  margin: 0;
+  color: #52637a;
+}
+
+.bp-tab-intro-actions {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 10px 16px;
+}
+
+.bp-counts {
+  color: var(--bp-muted);
+  font-size: 0.9rem;
+  white-space: nowrap;
+}
+
+/* Quiet outlined tags in sentence case. */
+.bp-soft-tag {
+  display: inline-block;
+  padding: 1px 8px;
+  border: 1px solid #d6dee8;
+  border-radius: 999px;
+  color: #405367;
+  font-size: 0.78rem;
+  font-weight: 600;
+  line-height: 1.5;
+  white-space: nowrap;
+}
+
+.bp-soft-tag.is-off {
+  border-style: dashed;
+  color: var(--bp-muted);
+}
+
+.bp-tags {
+  display: flex;
+  flex-wrap: wrap;
+  gap: 4px 6px;
+}
+
+/* A list card: a toolbar, then one row per thing. A row opens in place to show its panel. */
+.bp-list-card {
+  padding: 0;
+  overflow: hidden;
+}
+
+.bp-list-tools {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 10px 20px;
+  padding: 14px 20px;
+}
+
+.bp-list-tools h2 {
+  margin: 0;
+  font-size: 1.125rem;
+  font-weight: 650;
+}
+
+.bp-list-search {
+  flex: 0 1 320px;
+}
+
+.bp-list-search input {
+  width: 100%;
+}
+
+.bp-list-head,
+.bp-row > summary {
+  display: grid;
+  grid-template-columns: var(--bp-row-columns, minmax(0, 2fr) minmax(0, 1.4fr) minmax(0, 1.6fr) 96px);
+  align-items: center;
+  gap: 6px 20px;
+  padding: 12px 20px;
+}
+
+.bp-list-head {
+  border-top: 1px solid var(--bp-border);
+  color: var(--bp-muted);
+  font-size: 0.8rem;
+  font-weight: 600;
+}
+
+.bp-row {
+  border-top: 1px solid var(--bp-border);
+}
+
+.bp-row > summary {
+  list-style: none;
+  cursor: pointer;
+  transition: background-color 0.15s ease;
+}
+
+.bp-row > summary::-webkit-details-marker {
+  display: none;
+}
+
+.bp-row > summary:hover,
+.bp-row[open] > summary {
+  background: rgba(42, 110, 207, 0.05);
+}
+
+.bp-row-title {
+  min-width: 0;
+}
+
+.bp-row-title strong {
+  display: block;
+  overflow-wrap: anywhere;
+}
+
+.bp-row-title small,
+.bp-row-title code {
+  display: block;
+  color: var(--bp-muted);
+  font-size: 0.82rem;
+  overflow-wrap: anywhere;
+}
+
+.bp-row-open {
+  justify-self: end;
+  color: var(--bp-primary);
+  font-size: 0.9rem;
+  font-weight: 600;
+  white-space: nowrap;
+}
+
+.bp-row-open::after {
+  content: "";
+  display: inline-block;
+  width: 7px;
+  height: 7px;
+  margin-left: 8px;
+  border-right: 2px solid currentColor;
+  border-bottom: 2px solid currentColor;
+  transform: translateY(-3px) rotate(45deg);
+  transition: transform 0.15s ease;
+}
+
+.bp-row[open] .bp-row-open::after {
+  transform: translateY(1px) rotate(225deg);
+}
+
+.bp-row-open .is-close,
+.bp-row[open] .bp-row-open .is-open {
+  display: none;
+}
+
+.bp-row[open] .bp-row-open .is-close {
+  display: inline;
+}
+
+.bp-row-label {
+  display: none;
+}
+
+.bp-list-empty {
+  margin: 0;
+  padding: 18px 20px;
+  border-top: 1px solid var(--bp-border);
+  color: var(--bp-muted);
+}
+
+/* A row's panel: small cards side by side, the risky actions last. */
+.bp-row-panel {
+  display: grid;
+  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
+  gap: 14px;
+  padding: 4px 20px 20px;
+}
+
+.bp-subcard {
+  padding: 16px;
+  border: 1px solid var(--bp-border);
+  border-radius: 12px;
+  background: var(--bp-surface);
+}
+
+.bp-subcard h3 {
+  margin: 0 0 10px;
+  font-size: 1rem;
+  font-weight: 650;
+}
+
+.bp-subcard form {
+  display: grid;
+  gap: 10px;
+}
+
+.bp-subcard label {
+  display: grid;
+  gap: 4px;
+  font-size: 0.88rem;
+  font-weight: 600;
+}
+
+.bp-subcard .bp-inline-form {
+  grid-template-columns: minmax(0, 1fr) auto;
+  align-items: end;
+}
+
+.bp-subcard .bp-add-membership {
+  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
+  align-items: end;
+}
+
+.bp-add-membership button {
+  justify-self: start;
+}
+
+.bp-member-list {
+  display: grid;
+  gap: 6px;
+  margin: 0 0 12px;
+  padding: 0;
+  list-style: none;
+}
+
+.bp-member-list li {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 4px 10px;
+}
+
+.bp-danger-zone {
+  grid-column: 1 / -1;
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 10px 20px;
+  padding-top: 14px;
+  border-top: 1px dashed var(--bp-border);
+}
+
+.bp-danger-zone p {
+  max-width: 720px;
+  margin: 0;
+  color: #52637a;
+  font-size: 0.9rem;
+}
+
+.bp-danger-zone .bp-danger-actions {
+  display: flex;
+  flex-wrap: wrap;
+  gap: 10px;
+}
+
+.bp-quiet-button {
+  padding: 0;
+  border: 0;
+  background: none;
+  color: var(--bp-danger);
+  font-weight: 600;
+}
+
+.bp-quiet-button:hover,
+.bp-quiet-button:focus-visible {
+  text-decoration: underline;
+}
+
+/* A small card for a rare setting, at the end of a tab. */
+.bp-setting-card {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 12px 24px;
+}
+
+.bp-setting-card h2 {
+  margin: 0 0 2px;
+  font-size: 1.125rem;
+  font-weight: 650;
+}
+
+.bp-setting-card p {
+  margin: 0;
+  color: #52637a;
+}
+
+.bp-setting-card form {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 10px;
+}
+
+/* Choices with a line of explanation each (a token's permission, a user's access). */
+.bp-choice-list {
+  display: grid;
+  gap: 8px;
+  margin: 0;
+  padding: 0;
+  border: 0;
+}
+
+.bp-choice-list legend {
+  margin-bottom: 4px;
+  font-size: 0.88rem;
+  font-weight: 600;
+}
+
+.bp-choice {
+  display: grid;
+  grid-template-columns: auto minmax(0, 1fr);
+  gap: 2px 10px;
+  padding: 10px 12px;
+  border: 1px solid var(--bp-border);
+  border-radius: 10px;
+  cursor: pointer;
+}
+
+.bp-choice:has(input:checked) {
+  border-color: var(--bp-primary);
+  background: rgba(42, 110, 207, 0.05);
+}
+
+.bp-console-form-card .bp-choice input[type="radio"] {
+  grid-row: span 2;
+  width: auto;
+  min-height: 0;
+  margin-top: 4px;
+  padding: 0;
+  box-shadow: none;
+}
+
+.bp-choice small {
+  color: #52637a;
+}
+
+.bp-dialog-form {
+  display: grid;
+  gap: 14px;
+}
+
+.bp-dialog-form label {
+  display: grid;
+  gap: 4px;
+  font-size: 0.88rem;
+  font-weight: 600;
+}
+
+.bp-dialog-form :is(input, select) {
+  width: 100%;
+  min-width: 0;
+}
+
+.bp-field-pairs {
+  display: grid;
+  grid-template-columns: repeat(auto-fit, minmax(min(100%, 220px), 1fr));
+  gap: 14px;
+}
+
+.bp-dialog-wide {
+  width: min(100% - 32px, 620px);
+  min-width: 0;
+}
+
+/* The Help: one place, a contents list on the left with only the reader's parts. */
+.bp-help-layout {
+  display: grid;
+  grid-template-columns: 220px minmax(0, 1fr);
+  align-items: start;
+  gap: 28px;
+}
+
+.bp-help-contents {
+  position: sticky;
+  top: 16px;
+  display: grid;
+  gap: 2px;
+}
+
+.bp-help-contents p {
+  margin: 10px 0 4px;
+  color: var(--bp-muted);
+  font-size: 0.8rem;
+  font-weight: 600;
+}
+
+.bp-help-contents a {
+  padding: 6px 10px;
+  border-radius: 8px;
+  color: var(--bp-text);
+  text-decoration: none;
+}
+
+.bp-help-contents a:hover,
+.bp-help-contents a:focus-visible {
+  background: rgba(42, 110, 207, 0.06);
+}
+
+.bp-help-contents a[aria-current] {
+  background: #e8f1ff;
+  color: var(--bp-scania-blue);
+  font-weight: 600;
+}
+
+.bp-help-parts {
+  display: grid;
+  gap: 20px;
+}
+
+.bp-help-part h2 {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 6px 10px;
+  margin: 0 0 8px;
+  font-size: 1.25rem;
+  font-weight: 650;
+}
+
+.bp-help-part > p {
+  max-width: 820px;
+}
+
+.bp-steps {
+  display: grid;
+  gap: 12px;
+  margin: 12px 0 0;
+  padding: 0;
+  list-style: none;
+  counter-reset: bp-step;
+}
+
+.bp-steps li {
+  display: grid;
+  grid-template-columns: 30px minmax(0, 1fr);
+  gap: 2px 12px;
+  counter-increment: bp-step;
+}
+
+.bp-steps li::before {
+  content: counter(bp-step);
+  grid-row: span 2;
+  display: grid;
+  place-items: center;
+  width: 28px;
+  height: 28px;
+  border: 1px solid #bacde8;
+  border-radius: 50%;
+  color: var(--bp-scania-blue);
+  font-size: 0.85rem;
+  font-weight: 700;
+}
+
+.bp-steps strong a {
+  font-weight: 600;
+}
+
+.bp-steps p {
+  margin: 0;
+  color: #52637a;
+}
+
+.bp-word-list {
+  display: grid;
+  grid-template-columns: max-content minmax(0, 1fr);
+  gap: 6px 18px;
+  margin: 0;
+}
+
+.bp-word-list dt {
+  font-weight: 600;
+}
+
+.bp-word-list dd {
+  margin: 0;
+  color: #52637a;
+}
+
+@media (max-width: 767.98px) {
+  .bp-list-head {
+    display: none;
+  }
+
+  .bp-row > summary {
+    grid-template-columns: minmax(0, 1fr) auto;
+    padding: 14px 16px;
+  }
+
+  .bp-row > summary > :not(.bp-row-title):not(.bp-row-open) {
+    grid-column: 1 / -1;
+  }
+
+  .bp-row > summary > .bp-row-open {
+    grid-column: 2;
+    grid-row: 1;
+  }
+
+  .bp-row-label {
+    display: inline;
+    margin-right: 6px;
+    color: var(--bp-muted);
+    font-size: 0.8rem;
+  }
+
+  .bp-row-panel {
+    padding: 4px 12px 16px;
+  }
+
+  .bp-list-tools {
+    padding: 14px 16px;
+  }
+
+  .bp-list-search {
+    flex-basis: 100%;
+  }
+
+  .bp-help-layout {
+    grid-template-columns: minmax(0, 1fr);
+    gap: 16px;
+  }
+
+  .bp-help-contents {
+    position: static;
+    display: flex;
+    flex-wrap: wrap;
+    gap: 6px;
+  }
+
+  .bp-help-contents p {
+    display: none;
+  }
+
+  .bp-help-contents a {
+    border: 1px solid var(--bp-border);
+    font-size: 0.9rem;
+  }
+
+  .bp-word-list {
+    grid-template-columns: minmax(0, 1fr);
+  }
+
+  .bp-word-list dd {
+    margin-bottom: 6px;
+  }
+}
+
+[data-theme="dark"] :is(.bp-tab-intro p, .bp-danger-zone p, .bp-setting-card p, .bp-choice small, .bp-steps p, .bp-word-list dd) {
+  color: var(--bp-muted);
+}
+
+[data-theme="dark"] .bp-soft-tag {
+  border-color: var(--bp-border);
+  color: var(--bp-text);
+}
+
+[data-theme="dark"] .bp-soft-tag.is-off {
+  color: var(--bp-muted);
+}
+
+[data-theme="dark"] .bp-help-contents a[aria-current] {
+  background: rgba(122, 182, 255, 0.14);
+  color: var(--bp-text);
+}
+
+[data-theme="dark"] .bp-steps li::before {
+  border-color: var(--bp-border);
+  color: var(--bp-text);
+}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.AdminHeadingTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    30`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2082`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/AdminHelpPageTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Look/AdminHeadingTests.cs web/Views/Shared/_AdminTabs.cshtml web/Views/Admin/AuditLog.cshtml web/Views/Admin/Clusters.cshtml web/Views/Admin/Help.cshtml web/Views/Batteries/Batteries.cshtml web/Views/ProductData/Families.cshtml web/Views/ProductData/SitesAndOperators.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): one admin heading, the tabs in the order of the work, the calmer finish for the admin pages" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The Clusters tab

"Add cluster" opens a window (a refused ID opens it again with what was typed). Each cluster is a row that opens in place: rename, its users, and deleting last; force delete keeps its typed sentence, in its own window. The row counts each cluster's users and tokens, so this tab now loads the tokens and each token knows its cluster IDs. The Demo data card moves to the end. The shared confirm window and the new shared script replace the page's own copies.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Look/ClustersTabTests.cs`
- Create: `web/Views/Shared/_ConfirmModal.cshtml`
- Create: `web/wwwroot/js/bp-admin-pages.js`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3e-task2-tests.patch` (`BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`, `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`). Save the block as `../3e-task2-tests.patch`, outside the worktree, and run `git apply ../3e-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
@@ -7,7 +7,7 @@ public sealed class AdminCredentialLayoutTests
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var tokenTabStart = markup.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
-        var confirmModalStart = markup.IndexOf("<div class=\"bp-confirm-modal\" data-confirm-modal hidden>", StringComparison.Ordinal);
+        var confirmModalStart = markup.IndexOf("@await Html.PartialAsync(\"_ConfirmModal\")", StringComparison.Ordinal);
 
         Assert.True(tokenTabStart >= 0);
         Assert.True(confirmModalStart > tokenTabStart);
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -38,7 +38,7 @@ public sealed class AdminDenseConsoleLayoutTests
         var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("Create Cluster", clusters);
+        Assert.Contains("Add cluster", clusters);
         Assert.Contains("Cluster ID already exists.", admin);
         Assert.Contains("Access", clusters);
         Assert.Contains("Global Admin", clusters);
@@ -51,37 +51,6 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.DoesNotContain("bp-console-split", clusterUsers);
     }
 
-    [Fact]
-    public void AdminRegisteredClusters_ShouldUseDenseProductionTableWithoutMockupTabStyles()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-        var clusterTabStart = clusters.IndexOf("selectedTab == \"clusters\"", StringComparison.Ordinal);
-        var usersTabStart = clusters.IndexOf("selectedTab == \"users\"", StringComparison.Ordinal);
-        var clusterTab = clusters.Substring(clusterTabStart, usersTabStart - clusterTabStart);
-
-        Assert.True(clusterTabStart >= 0);
-        Assert.True(usersTabStart > clusterTabStart);
-        Assert.Contains("bp-cluster-create-strip", clusterTab);
-        Assert.Contains("bp-cluster-management-table", clusterTab);
-        Assert.Contains("bp-console-table", clusterTab);
-        Assert.Contains("<th>Cluster</th>", clusterTab);
-        Assert.Contains("<th>Cluster ID</th>", clusterTab);
-        Assert.Contains("<th>Rename</th>", clusterTab);
-        Assert.Contains("<th>Delete</th>", clusterTab);
-        Assert.Contains("<th>Force delete</th>", clusterTab);
-        Assert.Contains("aria-label=\"Save cluster name\"", clusterTab);
-        Assert.Contains("aria-label=\"Delete cluster\"", clusterTab);
-        Assert.Contains("aria-label=\"Force delete cluster\"", clusterTab);
-        Assert.DoesNotContain("bp-management-card", clusterTab);
-        Assert.DoesNotContain("bp-management-danger-zone", clusterTab);
-        Assert.DoesNotContain("option-button", clusterTab);
-        Assert.DoesNotContain("mockup-shell", clusterTab);
-        Assert.Contains(".bp-cluster-create-strip", css);
-        Assert.Contains(".bp-cluster-management-table", css);
-        Assert.Contains(".bp-cluster-danger-form", css);
-    }
-
     [Fact]
     public void AdminUsers_ShouldUseDenseMoreMenuTableForMultiClusterAssignments()
     {
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -89,11 +89,10 @@ public sealed class AdminFeedbackFollowupTests
 
         Assert.DoesNotContain("confirm(", clusters);
         Assert.DoesNotContain("confirm(", clusterTokens);
-        Assert.Contains("bp-confirm-modal", clusters);
+        Assert.Contains("_ConfirmModal", clusters);
         Assert.Contains("bp-confirm-modal", clusterTokens);
         Assert.Contains("data-confirm-action", clusters);
         Assert.Contains("data-confirm-action", clusterTokens);
-        Assert.Contains("bp-cluster-management-table", clusters);
         Assert.Contains("bp-user-management-table", clusters);
         Assert.Contains("bp-user-management-table", clusterUsers);
         Assert.DoesNotContain("class=\"bp-tag\"", clusters);
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -3,18 +3,6 @@ namespace BatteryPassWeb.Tests;
 public sealed class AdminFeedbackImplementationTests
 {
 
-    [Fact]
-    public void RegisteredClusters_ShouldExposeUserOverviewDropdownAndCreateUserCopy()
-    {
-        var clustersView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-
-        Assert.Contains("data-cluster-user-toggle", clustersView);
-        Assert.Contains("Users in cluster", clustersView);
-        Assert.Contains("data-cluster-user-drawer-row", clustersView);
-        Assert.Contains(">Create User<", clustersView);
-        Assert.DoesNotContain(">Save user<", clustersView);
-    }
-
     [Fact]
     public void CreateUserDropdown_ShouldIncludeGlobalReportRoles()
     {
diff --git a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
--- a/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoCardTests.cs
@@ -22,7 +22,7 @@ namespace BatteryPassWeb.Tests.Demo;
 [Collection(MongoCollection.Name)]
 public sealed class DemoCardTests(MongoFixture mongo)
 {
-    private static AdminController Controller(DemoRig rig)
+    internal static AdminController Controller(DemoRig rig)
     {
         var http = new DefaultHttpContext
         {
@@ -116,7 +116,7 @@ public sealed class DemoCardTests(MongoFixture mongo)
     public void TheCard_ShouldAskFirstAndTheWindowShouldShowTheTokensOnce()
     {
         var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("bp-cluster-create-strip", StringComparison.Ordinal)];
+        var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal)];
         var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_GeneratedCredentialsModal.cshtml"));
 
         Assert.Contains("action=\"/admin/demo-data/reset\"", card, StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -37,8 +37,7 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains("bp-mobile-identity", layout);
         Assert.Contains("bp-admin-mobile-tab-form", adminTabs);
         Assert.Contains("data-admin-mobile-tab-select", adminTabs);
-        Assert.Contains("bp-cluster-management-table bp-mobile-card-table", clusters);
-        Assert.Contains("data-label=\"Cluster ID\"", clusters);
+        Assert.Contains("<details class=\"bp-row\" data-cluster-row", clusters);
         Assert.Contains("bp-admin-help-workflow-table bp-mobile-card-table", adminHelp);
         Assert.Contains("bp-admin-help-status-table bp-mobile-card-table", adminHelp);
         Assert.Contains("bp-api-help-endpoint-table bp-mobile-card-table", apiHelp);
```

`BatteryPassWeb.Tests/Look/ClustersTabTests.cs`:

```csharp
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the Clusters tab. "Add cluster" opens a window, each cluster is a row that opens in place (rename,
/// its users, delete; force delete in its own window), and the Demo data card comes after the real clusters.</summary>
[Collection(MongoCollection.Name)]
public sealed class ClustersTabTests(MongoFixture mongo)
{
    private static string Page => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

    private static string Tab
    {
        get
        {
            var page = Page;
            var start = page.IndexOf("@if (selectedTab == \"clusters\")", StringComparison.Ordinal);
            var end = page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return page[start..end];
        }
    }

    [Fact]
    public void AddCluster_ShouldOpenAWindowThatKeepsARefusedName()
    {
        var tab = Tab;

        Assert.Contains("<button type=\"button\" class=\"bp-primary-button\" data-open-dialog=\"add-cluster-dialog\">Add cluster</button>", tab, StringComparison.Ordinal);
        Assert.Contains("<dialog id=\"add-cluster-dialog\"", tab, StringComparison.Ordinal);
        Assert.Contains("data-open-on-load=\"@(clusterCreateName.Length > 0 ? \"true\" : \"false\")\"", tab, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/create\"", tab, StringComparison.Ordinal);
        Assert.Contains("name=\"name\" value=\"@clusterCreateName\"", tab, StringComparison.Ordinal);
        Assert.Contains("name=\"clusterId\" value=\"@clusterCreateId\"", tab, StringComparison.Ordinal);
        Assert.Contains("formmethod=\"dialog\" formnovalidate", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void EachCluster_ShouldBeARowThatOpensToRenameSeeUsersAndDelete()
    {
        var tab = Tab;
        var row = tab[tab.IndexOf("<details class=\"bp-row\" data-cluster-row", StringComparison.Ordinal)..tab.IndexOf("</details>", StringComparison.Ordinal)];

        Assert.Contains("<span class=\"bp-row-title\"><strong>@cluster.Name</strong><code>@cluster.ClusterId</code></span>", row, StringComparison.Ordinal);
        Assert.Contains("@Count(clusterMemberships.Count, \"user\", \"users\")", row, StringComparison.Ordinal);
        Assert.Contains("@Count(clusterTokenCount, \"token\", \"tokens\")", row, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/update\"", row, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag\">@MembershipRoleLabel(membership.Role)</span>", row, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/delete\"", row, StringComparison.Ordinal);
        Assert.Contains("data-confirm-action data-confirm-title=\"Delete cluster\"", row, StringComparison.Ordinal);
        Assert.Contains("<div class=\"bp-danger-zone\">", row, StringComparison.Ordinal);
        Assert.Contains("data-open-dialog=\"@forceDialogId\">Force delete…</button>", row, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void ForceDelete_ShouldAskForTheTypedSentenceInItsOwnWindow()
    {
        var tab = Tab;
        var window = tab[tab.IndexOf("<dialog id=\"@forceDialogId\"", StringComparison.Ordinal)..];

        Assert.Contains("action=\"/admin/clusters/force-delete\"", window, StringComparison.Ordinal);
        Assert.Contains("<em>I understand this deletes linked cluster data</em>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\" autocomplete=\"off\" required", window, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoData_ShouldComeAfterTheClusters()
    {
        var tab = Tab;

        Assert.True(tab.IndexOf("data-demo-data-card", StringComparison.Ordinal) > tab.IndexOf("data-cluster-row", StringComparison.Ordinal));
        Assert.Contains("data-demo-data-state>@(Model.HasDemoData ? \"Loaded\" : \"Not loaded\")</span>", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePage_ShouldUseTheSharedScriptAndConfirmWindow()
    {
        var page = Page;
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));

        Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
        Assert.Contains("<script src=\"~/js/bp-admin-pages.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confirm-modal hidden", page, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('[data-open-dialog]')", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('dialog[data-open-on-load=\"true\"]')", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('[data-confirm-action]')", script, StringComparison.Ordinal);
        Assert.Contains("data-confirm-modal hidden", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ConfirmModal.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTab_ShouldKnowEachTokensClusters()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var model = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await DemoCardTests.Controller(rig).Clusters("clusters", null, CancellationToken.None)).Model);

        Assert.Equal(2, model.ApiTokens.Count(token => !token.GlobalAccess && token.ClusterIds.Contains("demo-north")));
        Assert.Single(model.ApiTokens, token => token.GlobalAccess);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoCardTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ClustersTabTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: the build fails: `ApiTokenViewModel` has no `ClusterIds` yet (CS1061).

- [ ] **Step 3: Write `_ConfirmModal.cshtml`**

`web/Views/Shared/_ConfirmModal.cshtml`:

```cshtml
@* The "Please confirm" window that bp-admin-pages.js opens for any button with data-confirm-action. *@
<div class="bp-confirm-modal" data-confirm-modal hidden>
    <div class="bp-confirm-modal-backdrop" data-confirm-cancel></div>
    <section class="bp-confirm-modal-panel" role="dialog" aria-modal="true" aria-labelledby="bp-confirm-title">
        <p class="bp-summary-title">Please confirm</p>
        <h2 id="bp-confirm-title">Confirm action</h2>
        <p class="bp-confirm-message" data-confirm-message>Are you sure?</p>
        <div class="bp-confirm-actions">
            <button type="button" class="bp-secondary-button" data-confirm-cancel>Cancel</button>
            <button type="button" class="bp-danger-button" data-confirm-submit>Confirm</button>
        </div>
    </section>
</div>
```

- [ ] **Step 4: Write `bp-admin-pages.js`**

`web/wwwroot/js/bp-admin-pages.js`:

```javascript
// The admin and cluster admin pages (step 3e): the windows for adding things, "Please confirm" before a risky action,
// showing a typed password, searching a list, and opening a row named in the address. Without JavaScript the rows
// still open and close, and every form in them still works.
(function () {
    // A button with data-open-dialog opens that native <dialog>: focus, Escape and the dimmed page come with it. A
    // dialog marked data-open-on-load="true" opens at once, e.g. after a refused "Add cluster", with the typed values
    // kept. (Razor writes data- attributes even when false, so the value says it.)
    document.querySelectorAll('[data-open-dialog]').forEach((button) => {
        const dialog = document.getElementById(button.dataset.openDialog);
        button.addEventListener('click', () => dialog?.showModal());
    });
    document.querySelectorAll('dialog[data-open-on-load="true"]').forEach((dialog) => dialog.showModal());

    // The eye button beside a password shows or hides what was typed.
    document.querySelectorAll('[data-password-reveal]').forEach((button) => {
        button.addEventListener('click', () => {
            const input = button.closest('.bp-password-field')?.querySelector('input');
            if (!input) {
                return;
            }

            const reveal = input.type === 'password';
            input.type = reveal ? 'text' : 'password';
            button.setAttribute('aria-label', reveal ? 'Hide password' : 'Show password');
            button.setAttribute('title', reveal ? 'Hide password' : 'Show password');
        });
    });

    // A list's search box hides the rows that don't match; "No ... match" shows when none are left.
    document.querySelectorAll('[data-list-search]').forEach((input) => {
        const list = input.closest('[data-list]');
        const rows = Array.from(list?.querySelectorAll('[data-search-text]') ?? []);
        const none = list?.querySelector('[data-list-no-results]');
        input.addEventListener('input', () => {
            const query = input.value.trim().toLowerCase();
            let shown = 0;
            rows.forEach((row) => {
                const match = query.length === 0 || (row.dataset.searchText || '').toLowerCase().includes(query);
                row.hidden = !match;
                shown += match ? 1 : 0;
            });
            if (none) {
                none.hidden = shown > 0;
            }
        });
    });

    // After a save the page comes back with ?openUser=...: that user's row opens again.
    const openUser = new URLSearchParams(window.location.search).get('openUser');
    if (openUser) {
        const row = Array.from(document.querySelectorAll('details[data-open-user]'))
            .find((candidate) => (candidate.dataset.openUser || '').toLowerCase() === openUser.toLowerCase());
        if (row) {
            row.open = true;
            row.scrollIntoView({ block: 'center' });
        }
    }

    // A button with data-confirm-action asks first, in the shared "Please confirm" window (_ConfirmModal).
    const confirmModal = document.querySelector('[data-confirm-modal]');
    const confirmTitle = document.getElementById('bp-confirm-title');
    const confirmMessage = confirmModal?.querySelector('[data-confirm-message]');
    const confirmSubmit = confirmModal?.querySelector('[data-confirm-submit]');
    let pendingForm = null;
    document.querySelectorAll('[data-confirm-action]').forEach((button) => {
        button.addEventListener('click', (event) => {
            const form = button.closest('form');
            if (!form || !confirmModal || !confirmTitle || !confirmMessage || !confirmSubmit) {
                return;
            }

            event.preventDefault();
            pendingForm = form;
            confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
            confirmMessage.textContent = button.dataset.confirmMessage || 'Continue with this action?';
            confirmSubmit.textContent = button.dataset.confirmSubmit || 'Confirm';
            confirmModal.hidden = false;
            confirmSubmit.focus();
        });
    });

    confirmModal?.querySelectorAll('[data-confirm-cancel]').forEach((button) => {
        button.addEventListener('click', () => {
            confirmModal.hidden = true;
            pendingForm = null;
        });
    });

    confirmSubmit?.addEventListener('click', () => {
        if (pendingForm) {
            pendingForm.submit();
        }
    });
})();
```

- [ ] **Step 5: Rebuild the tab and add the shared pieces**

Patch `3e-task2-code.patch` (`web/Controllers/AdminController.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Views/Admin/Clusters.cshtml`). Save the block as `../3e-task2-code.patch`, outside the worktree, and run `git apply ../3e-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -127,7 +127,7 @@ public class AdminController : Controller
 
         var clusterNamesById = clusterViewModels.ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
         var needsUsers = selectedTab is "users" or "clusters";
-        var needsCredentials = selectedTab == "api-token-management";
+        var needsCredentials = selectedTab is "api-token-management" or "clusters"; // the clusters tab counts each cluster's tokens
         IReadOnlyList<BsonDocument> users = needsUsers
             ? await _clusterRepository.ListUsersAsync(cancellationToken)
             : Array.Empty<BsonDocument>();
@@ -179,6 +179,7 @@ public class AdminController : Controller
                     IsActive = token.GetValue("isActive", false).ToBoolean(),
                     IsSample = token.GetValue("isSample", false).ToBoolean(),
                     ClusterIdsLabel = clusterNames.Count == 0 ? "No clusters" : string.Join(", ", clusterNames),
+                    ClusterIds = tokenClusterIds.Select(entry => entry.ToString() ?? string.Empty).Where(clusterId => !string.IsNullOrWhiteSpace(clusterId)).ToList(),
                     ClusterScopeNames = clusterNames,
                     CreatedAt = BsonHelpers.GetString(token, "createdAt"),
                     UpdatedAt = BsonHelpers.GetString(token, "updatedAt"),
diff --git a/web/Models/ViewModels/AdminClusterViewModel.cs b/web/Models/ViewModels/AdminClusterViewModel.cs
--- a/web/Models/ViewModels/AdminClusterViewModel.cs
+++ b/web/Models/ViewModels/AdminClusterViewModel.cs
@@ -54,6 +54,7 @@ public sealed class ApiTokenViewModel
     public bool IsActive { get; init; } = true;
     public bool IsSample { get; init; }
     public string ClusterIdsLabel { get; init; } = string.Empty;
+    public IReadOnlyList<string> ClusterIds { get; init; } = [];
     public IReadOnlyList<string> ClusterScopeNames { get; init; } = [];
     public string CreatedAt { get; init; } = string.Empty;
     public string UpdatedAt { get; init; } = string.Empty;
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -41,6 +41,7 @@
         "liTreatment" => "Legitimate interest: authorised or WEEE treatment",
         _ => clusterMembershipRoleOptions.TryGetValue(role, out var label) ? label : "Normal User"
     };
+    string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
     string MembershipCountLabel(int count) => $"{count} {(count == 1 ? "membership" : "memberships")}";
     string TokenClusterPickerName(string name) => name.StartsWith("Default ", StringComparison.OrdinalIgnoreCase) ? name["Default ".Length..] : name;
     string TokenDisplayName(BatteryPassWeb.Models.ViewModels.ApiTokenViewModel token)
@@ -109,172 +110,153 @@
 
     @if (selectedTab == "clusters")
     {
-        var clusterRowIndex = 0;
-        <div class="bp-management-shell bp-cluster-management-shell mt-4">
-            <section class="bp-card bp-console-form-card bp-demo-data-card" data-demo-data-card>
+        <section class="bp-card bp-console-form-card bp-tab-intro mt-4">
+            <div>
+                <h2>Clusters</h2>
+                <p>Each battery and each cluster user belongs to a cluster. Cluster admins look after their own cluster's users and API tokens.</p>
+            </div>
+            <div class="bp-tab-intro-actions">
+                <span class="bp-counts">@Count(Model.Clusters.Count, "cluster", "clusters")</span>
+                <button type="button" class="bp-primary-button" data-open-dialog="add-cluster-dialog">Add cluster</button>
+            </div>
+        </section>
+
+        <dialog id="add-cluster-dialog" class="bp-modal-panel bp-dialog bp-console-form-card" aria-labelledby="add-cluster-title" data-open-on-load="@(clusterCreateName.Length > 0 ? "true" : "false")">
+            <form method="post" action="/admin/clusters/create" class="bp-dialog-form">
+                @Html.AntiForgeryToken()
                 <div>
-                    <span class="bp-summary-title">Demo data</span>
-                    <h2 class="h5 mb-2">Demo data</h2>
-                    <p class="text-secondary mb-1">The Compact and Core families with six batteries in several states, three demo clusters, an account for every role but the team admin, and API tokens. Real data is never changed. Sign in with your own team account; the demo accounts cover the other roles.</p>
-                    <p class="mb-0" data-demo-data-state>@(Model.HasDemoData ? "The demo data is there." : "There is no demo data.")</p>
-                    @if (!Model.DemoAccountsPossible)
-                    {
-                        <p class="text-secondary mb-0" data-demo-data-no-accounts>@BatteryPassWeb.Dpp.Demo.DemoDataService.NoPassword</p>
-                    }
-                </div>
-                <div class="d-flex flex-wrap gap-2 align-items-end">
-                    <form method="post" action="/admin/demo-data/reset">
-                        @Html.AntiForgeryToken()
-                        <button type="submit" class="bp-primary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data">Reset demo data</button>
-                    </form>
-                    <form method="post" action="/admin/demo-data/clear">
-                        @Html.AntiForgeryToken()
-                        <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data">Clear demo data</button>
-                    </form>
+                    <h2 id="add-cluster-title">Add a cluster</h2>
+                    <p class="bp-help-text mb-0">Batteries and users are added to it afterwards.</p>
                 </div>
-            </section>
-            <section class="bp-card bp-cluster-create-strip bp-console-form-card">
-                <div>
-                    <span class="bp-summary-title">Cluster setup</span>
-                    <h2 class="h5 mb-2">Create Cluster</h2>
-                    <p class="text-secondary mb-0">Create a cluster first, then assign batteries and users from the nearby tabs.</p>
+                <label>
+                    Cluster name
+                    <input type="text" name="name" value="@clusterCreateName" placeholder="e.g. North operations" required />
+                </label>
+                <label>
+                    <span>Cluster ID <span class="text-secondary fw-normal">(optional; one is made for you if empty)</span></span>
+                    <input type="text" name="clusterId" value="@clusterCreateId" placeholder="e.g. north-operations" />
+                </label>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                    <button type="submit" class="bp-primary-button">Add cluster</button>
                 </div>
-                <form method="post" action="/admin/clusters/create" class="bp-cluster-create-form">
-                    @Html.AntiForgeryToken()
-                    <label>
-                        Cluster name
-                        <input type="text" name="name" value="@clusterCreateName" placeholder="North Operations Cluster" required />
-                    </label>
-                    <label>
-                        Cluster ID
-                        <input type="text" name="clusterId" value="@clusterCreateId" placeholder="cluster-north-operations (optional)" />
-                    </label>
-                    <button type="submit" class="bp-primary-button">Create Cluster</button>
-                </form>
-            </section>
-            <section class="bp-card bp-table-card">
-                <div class="bp-table-scroll">
-                    <table class="table table-hover align-middle bp-console-table bp-cluster-management-table bp-mobile-card-table">
-                        <thead>
-                            <tr>
-                                <th>Cluster</th>
-                                <th>Cluster ID</th>
-                                <th>Users</th>
-                                <th>Rename</th>
-                                <th>Delete</th>
-                                <th>Force delete</th>
-                            </tr>
-                        </thead>
-                        <tbody>
-                            @if (Model.Clusters.Count == 0)
+            </form>
+        </dialog>
+
+        <section class="bp-card bp-list-card mt-4" style="--bp-row-columns: minmax(0, 2fr) minmax(0, 1.2fr) minmax(0, 1.2fr) 96px" aria-label="Clusters">
+            @if (Model.Clusters.Count == 0)
+            {
+                <p class="bp-list-empty">No clusters yet.</p>
+            }
+            else
+            {
+                <div class="bp-list-head" aria-hidden="true"><span>Cluster</span><span>Users</span><span>API tokens</span><span></span></div>
+            }
+            @foreach (var cluster in Model.Clusters)
+            {
+                var clusterMemberships = Model.Memberships
+                    .Where(membership => membership.ClusterId.Equals(cluster.ClusterId, StringComparison.OrdinalIgnoreCase))
+                    .OrderBy(membership => membership.Email, StringComparer.OrdinalIgnoreCase)
+                    .ToList();
+                var clusterTokenCount = Model.ApiTokens.Count(token => !token.GlobalAccess && token.ClusterIds.Contains(cluster.ClusterId, StringComparer.OrdinalIgnoreCase));
+                var forceDialogId = $"force-delete-{cluster.ClusterId}";
+                <details class="bp-row" data-cluster-row="@cluster.ClusterId">
+                    <summary>
+                        <span class="bp-row-title"><strong>@cluster.Name</strong><code>@cluster.ClusterId</code></span>
+                        <span><span class="bp-row-label">Users</span>@Count(clusterMemberships.Count, "user", "users")</span>
+                        <span><span class="bp-row-label">API tokens</span>@Count(clusterTokenCount, "token", "tokens")</span>
+                        <span class="bp-row-open"><span class="is-open">Open</span><span class="is-close">Close</span></span>
+                    </summary>
+                    <div class="bp-row-panel bp-console-form-card">
+                        <section class="bp-subcard">
+                            <h3>Name</h3>
+                            <form method="post" action="/admin/clusters/update" class="bp-inline-form">
+                                @Html.AntiForgeryToken()
+                                <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
+                                <label>
+                                    Cluster name
+                                    <input type="text" name="name" value="@cluster.Name" required />
+                                </label>
+                                <button type="submit" class="bp-secondary-button">Save name</button>
+                            </form>
+                            <p class="text-secondary small mt-2 mb-0">The ID <code>@cluster.ClusterId</code> stays as it is.</p>
+                        </section>
+                        <section class="bp-subcard">
+                            <h3>Users</h3>
+                            @if (clusterMemberships.Count == 0)
                             {
-                                <tr>
-                                    <td class="text-secondary" colspan="6">No clusters have been registered.</td>
-                                </tr>
+                                <p class="text-secondary">No users in this cluster yet.</p>
                             }
                             else
                             {
-                                @foreach (var cluster in Model.Clusters)
-                                {
-                                    var clusterMemberships = Model.Memberships
-                                        .Where(membership => membership.ClusterId.Equals(cluster.ClusterId, StringComparison.OrdinalIgnoreCase))
-                                        .OrderBy(membership => membership.Email, StringComparer.OrdinalIgnoreCase)
-                                        .ToList();
-                                    var drawerId = $"admin-cluster-user-drawer-{clusterRowIndex++}";
-                                    <tr>
-                                        <td data-label="Cluster">
-                                            <p class="bp-cluster-name">@cluster.Name</p>
-                                            <p class="mb-0 small text-secondary">Registered cluster</p>
-                                        </td>
-                                        <td data-label="Cluster ID"><code class="bp-code-wrap">@cluster.ClusterId</code></td>
-                                        <td data-label="Users">
-                                            <button type="button" class="bp-action-button bp-cluster-user-toggle" data-cluster-user-toggle aria-expanded="false" aria-controls="@drawerId" aria-label="Users in cluster @cluster.Name" title="Users in cluster" data-tooltip="Users in cluster">
-                                                <span class="bp-user-count-pill">@clusterMemberships.Count users</span>
-                                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                    <path d="m6 9 6 6 6-6" />
-                                                </svg>
-                                            </button>
-                                        </td>
-                                        <td data-label="Rename">
-                                            <form method="post" action="/admin/clusters/update" class="bp-cluster-rename-form">
-                                                @Html.AntiForgeryToken()
-                                                <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
-                                                <input class="form-control form-control-sm" name="name" value="@cluster.Name" aria-label="Display name for @cluster.Name" required />
-                                                <button type="submit" class="bp-action-button" aria-label="Save cluster name" title="Save cluster name" data-tooltip="Save cluster name" data-action-icon="save-cluster-name">
-                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                        <path d="M20 6 9 17l-5-5" />
-                                                    </svg>
-                                                </button>
-                                            </form>
-                                        </td>
-                                        <td data-label="Delete">
-                                            <form method="post" action="/admin/clusters/delete" class="bp-cluster-delete-form">
-                                                @Html.AntiForgeryToken()
-                                                <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
-                                                <button type="submit" class="bp-action-button is-danger" aria-label="Delete cluster" title="Delete cluster" data-tooltip="Delete cluster" data-action-icon="delete-cluster" data-confirm-action data-confirm-title="Delete cluster" data-confirm-message="Delete @cluster.ClusterId? This only succeeds when no batteries or users are linked." data-confirm-submit="Delete">
-                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                        <path d="M3 6h18" />
-                                                        <path d="M8 6V4h8v2" />
-                                                        <path d="M6 6l1 15h10l1-15" />
-                                                        <path d="M10 11v6" />
-                                                        <path d="M14 11v6" />
-                                                    </svg>
-                                                </button>
-                                            </form>
-                                        </td>
-                                        <td data-label="Force delete">
-                                            <form method="post" action="/admin/clusters/force-delete" class="bp-cluster-danger-form">
-                                                @Html.AntiForgeryToken()
-                                                <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
-                                                <input class="form-control form-control-sm" name="confirmation" placeholder="I understand this deletes linked cluster data" aria-label="Force delete confirmation for @cluster.Name" />
-                                                <button type="submit" class="bp-action-button is-danger" aria-label="Force delete cluster" title="Force delete cluster" data-tooltip="Force delete cluster" data-action-icon="force-delete-cluster" data-confirm-action data-confirm-title="Force delete cluster" data-confirm-message="Force delete @cluster.ClusterId and remove linked cluster data from passports and memberships?" data-confirm-submit="Force delete">
-                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                        <path d="M12 9v4" />
-                                                        <path d="M12 17h.01" />
-                                                        <path d="M10.3 4.3 2.8 17.2A2 2 0 0 0 4.5 20h15a2 2 0 0 0 1.7-2.8L13.7 4.3a2 2 0 0 0-3.4 0Z" />
-                                                    </svg>
-                                                </button>
-                                            </form>
-                                        </td>
-                                    </tr>
-                                    <tr id="@drawerId" class="bp-cluster-user-drawer-row" data-cluster-user-drawer-row hidden>
-                                        <td colspan="6">
-                                            <div class="bp-cluster-user-drawer">
-                                                <div>
-                                                    <h3>Users in cluster</h3>
-                                                    <p class="mb-0 text-secondary">@cluster.Name</p>
-                                                </div>
-                                                @if (clusterMemberships.Count == 0)
-                                                {
-                                                    <p class="mb-0 text-secondary">No users are assigned to this cluster.</p>
-                                                }
-                                                else
-                                                {
-                                                    <div class="bp-cluster-user-list">
-                                                        @foreach (var membership in clusterMemberships)
-                                                        {
-                                                            var user = Model.Users.FirstOrDefault(candidate => candidate.Email.Equals(membership.Email, StringComparison.OrdinalIgnoreCase));
-                                                            var displayName = string.IsNullOrWhiteSpace(user?.Name) ? membership.Email : user.Name;
-                                                            <div class="bp-cluster-user-row">
-                                                                <span>
-                                                                    <strong>@displayName</strong>
-                                                                    <code>@membership.Email</code>
-                                                                </span>
-                                                                <span class="bp-user-membership-role">@MembershipRoleLabel(membership.Role)</span>
-                                                            </div>
-                                                        }
-                                                    </div>
-                                                }
-                                            </div>
-                                        </td>
-                                    </tr>
-                                }
+                                <ul class="bp-member-list">
+                                    @foreach (var membership in clusterMemberships)
+                                    {
+                                        var user = Model.Users.FirstOrDefault(candidate => candidate.Email.Equals(membership.Email, StringComparison.OrdinalIgnoreCase));
+                                        var displayName = string.IsNullOrWhiteSpace(user?.Name) ? membership.Email : user.Name;
+                                        <li>
+                                            <span><strong>@displayName</strong> <span class="text-secondary small">@membership.Email</span></span>
+                                            <span class="bp-soft-tag">@MembershipRoleLabel(membership.Role)</span>
+                                        </li>
+                                    }
+                                </ul>
                             }
-                        </tbody>
-                    </table>
-                </div>
-            </section>
-        </div>
+                            <a href="/admin/clusters?tab=users">Change in Users</a>
+                        </section>
+                        <div class="bp-danger-zone">
+                            <p>A cluster can be deleted when no batteries or users belong to it. Force delete also removes it from its batteries and memberships.</p>
+                            <div class="bp-danger-actions">
+                                <form method="post" action="/admin/clusters/delete">
+                                    @Html.AntiForgeryToken()
+                                    <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
+                                    <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Delete cluster" data-confirm-message="Delete @cluster.Name? This only works when no batteries or users belong to it." data-confirm-submit="Delete">Delete cluster</button>
+                                </form>
+                                <button type="button" class="bp-quiet-button" data-open-dialog="@forceDialogId">Force delete…</button>
+                            </div>
+                        </div>
+                    </div>
+                </details>
+                <dialog id="@forceDialogId" class="bp-modal-panel bp-dialog bp-console-form-card" aria-labelledby="@(forceDialogId)-title">
+                    <form method="post" action="/admin/clusters/force-delete" class="bp-dialog-form">
+                        @Html.AntiForgeryToken()
+                        <input type="hidden" name="clusterId" value="@cluster.ClusterId" />
+                        <div>
+                            <h2 id="@(forceDialogId)-title">Force delete @cluster.Name?</h2>
+                            <p class="bp-help-text mb-0">This removes the cluster from its batteries and memberships. It can't be undone.</p>
+                        </div>
+                        <label>
+                            <span>Type <em>I understand this deletes linked cluster data</em></span>
+                            <input type="text" name="confirmation" autocomplete="off" required />
+                        </label>
+                        <div class="bp-dialog-actions">
+                            <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                            <button type="submit" class="bp-danger-button">Force delete</button>
+                        </div>
+                    </form>
+                </dialog>
+            }
+        </section>
+
+        <section class="bp-card bp-console-form-card bp-setting-card mt-4" data-demo-data-card>
+            <div>
+                <h2>Demo data <span class="bp-soft-tag ms-1" data-demo-data-state>@(Model.HasDemoData ? "Loaded" : "Not loaded")</span></h2>
+                <p>The Compact and Core families with six batteries in several states, three demo clusters, an account for every role but the team admin, and API tokens. Real data is never changed. Sign in with your own team account; the demo accounts cover the other roles.</p>
+                @if (!Model.DemoAccountsPossible)
+                {
+                    <p class="mt-1" data-demo-data-no-accounts>@BatteryPassWeb.Dpp.Demo.DemoDataService.NoPassword</p>
+                }
+            </div>
+            <div class="d-flex flex-wrap gap-2">
+                <form method="post" action="/admin/demo-data/reset">
+                    @Html.AntiForgeryToken()
+                    <button type="submit" class="bp-secondary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data">Reset demo data</button>
+                </form>
+                <form method="post" action="/admin/demo-data/clear">
+                    @Html.AntiForgeryToken()
+                    <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data">Clear demo data</button>
+                </form>
+            </div>
+        </section>
     }
 
     @if (selectedTab == "users")
@@ -782,37 +764,13 @@
 
     }
 
-    <div class="bp-confirm-modal" data-confirm-modal hidden>
-        <div class="bp-confirm-modal-backdrop" data-confirm-cancel></div>
-        <section class="bp-confirm-modal-panel" role="dialog" aria-modal="true" aria-labelledby="bp-confirm-title">
-            <p class="bp-summary-title">Please confirm</p>
-            <h2 id="bp-confirm-title">Confirm action</h2>
-            <p class="bp-confirm-message" data-confirm-message>Are you sure?</p>
-            <div class="bp-confirm-actions">
-                <button type="button" class="bp-secondary-button" data-confirm-cancel>Cancel</button>
-                <button type="button" class="bp-danger-button" data-confirm-submit>Confirm</button>
-            </div>
-        </section>
-    </div>
+    @await Html.PartialAsync("_ConfirmModal")
 </main>
 
 @section Scripts {
+    <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
     <script>
         (function () {
-            document.querySelectorAll('[data-password-reveal]').forEach((button) => {
-                button.addEventListener('click', () => {
-                    const input = button.closest('.bp-password-field')?.querySelector('input');
-                    if (!input) {
-                        return;
-                    }
-
-                    const reveal = input.type === 'password';
-                    input.type = reveal ? 'text' : 'password';
-                    button.setAttribute('aria-label', reveal ? 'Hide password' : 'Show password');
-                    button.setAttribute('title', reveal ? 'Hide password' : 'Show password');
-                });
-            });
-
             document.querySelectorAll('[data-user-drawer-toggle]').forEach((button) => {
                 button.addEventListener('click', () => {
                     const drawerId = button.getAttribute('aria-controls');
@@ -827,20 +785,6 @@
                 });
             });
 
-            document.querySelectorAll('[data-cluster-user-toggle]').forEach((button) => {
-                button.addEventListener('click', () => {
-                    const drawerId = button.getAttribute('aria-controls');
-                    const drawer = drawerId ? document.getElementById(drawerId) : null;
-                    if (!drawer) {
-                        return;
-                    }
-
-                    const expanded = button.getAttribute('aria-expanded') === 'true';
-                    button.setAttribute('aria-expanded', expanded ? 'false' : 'true');
-                    drawer.hidden = expanded;
-                });
-            });
-
             const openUser = new URLSearchParams(window.location.search).get('openUser');
             if (openUser) {
                 const drawer = Array.from(document.querySelectorAll('.bp-user-drawer-row[data-open-user]'))
@@ -888,41 +832,6 @@
                     history.replaceState(null, '', `/admin/clusters?tab=${selected}`);
                 });
             });
-
-            const confirmModal = document.querySelector('[data-confirm-modal]');
-            const confirmTitle = document.getElementById('bp-confirm-title');
-            const confirmMessage = confirmModal?.querySelector('[data-confirm-message]');
-            const confirmSubmit = confirmModal?.querySelector('[data-confirm-submit]');
-            let pendingForm = null;
-            document.querySelectorAll('[data-confirm-action]').forEach((button) => {
-                button.addEventListener('click', (event) => {
-                    const form = button.closest('form');
-                    if (!form || !confirmModal || !confirmTitle || !confirmMessage || !confirmSubmit) {
-                        return;
-                    }
-
-                    event.preventDefault();
-                    pendingForm = form;
-                    confirmTitle.textContent = button.dataset.confirmTitle || 'Confirm action';
-                    confirmMessage.textContent = button.dataset.confirmMessage || 'Continue with this action?';
-                    confirmSubmit.textContent = button.dataset.confirmSubmit || 'Confirm';
-                    confirmModal.hidden = false;
-                    confirmSubmit.focus();
-                });
-            });
-
-            confirmModal?.querySelectorAll('[data-confirm-cancel]').forEach((button) => {
-                button.addEventListener('click', () => {
-                    confirmModal.hidden = true;
-                    pendingForm = null;
-                });
-            });
-
-            confirmSubmit?.addEventListener('click', () => {
-                if (pendingForm) {
-                    pendingForm.submit();
-                }
-            });
         })();
     </script>
 }
```

- [ ] **Step 6: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoCardTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ClustersTabTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    47`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2086`, none failed.

- [ ] **Step 7: Commit**

```bash
git add BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/Demo/DemoCardTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Look/ClustersTabTests.cs web/Views/Shared/_ConfirmModal.cshtml web/wwwroot/js/bp-admin-pages.js web/Controllers/AdminController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Views/Admin/Clusters.cshtml
git commit -m "feat(dpp): the Clusters tab, one row per cluster that opens in place, demo data last" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: The Users tab

"Add user" opens a window with today's fields. Each row shows the person's access and clusters, and the list has a search. A row opens to name and access, clusters, a new password and, last, deleting the account. The sign-out time becomes a small card at the end. Role labels move to sentence case; the three legitimate-interest labels stay as they are.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/AppRolesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Look/UsersTabTests.cs`
- Create: `web/Views/Shared/_PasswordReveal.cshtml`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3e-task3-tests.patch` (`BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`, `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/Dpp/AppRolesTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`). Save the block as `../3e-task3-tests.patch`, outside the worktree, and run `git apply ../3e-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
@@ -71,7 +71,7 @@ public sealed class AdminCredentialLayoutTests
         Assert.DoesNotContain(">API tokens</a>", clusters);
         Assert.DoesNotContain(">Battery secrets</a>", clusters);
         Assert.DoesNotContain("Battery secrets", clusters);
-        Assert.Contains("data-password-reveal", clusters);
+        Assert.Contains("_PasswordReveal", clusters);
         Assert.Contains("data-password-reveal", clusterUsers);
         Assert.Contains(".bp-tab-row", css);
         Assert.Contains("flex-wrap: nowrap", css);
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -41,8 +41,8 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.Contains("Add cluster", clusters);
         Assert.Contains("Cluster ID already exists.", admin);
         Assert.Contains("Access", clusters);
-        Assert.Contains("Global Admin", clusters);
-        Assert.Contains("Cluster Member", clusters);
+        Assert.Contains("Global admin (the team)", clusters);
+        Assert.Contains("Cluster member (sees only", clusters);
         Assert.DoesNotContain("No global admin", clusters);
         Assert.Contains("bp-select-shell", clusters);
         Assert.Contains("bp-select-shell", clusterUsers);
@@ -51,42 +51,6 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.DoesNotContain("bp-console-split", clusterUsers);
     }
 
-    [Fact]
-    public void AdminUsers_ShouldUseDenseMoreMenuTableForMultiClusterAssignments()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-        var usersTabStart = clusters.IndexOf("selectedTab == \"users\"", StringComparison.Ordinal);
-        var nextTabStart = clusters.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
-        var usersTab = clusters.Substring(usersTabStart, nextTabStart - usersTabStart);
-
-        Assert.True(usersTabStart >= 0);
-        Assert.True(nextTabStart > usersTabStart);
-        Assert.Contains("bp-user-create-strip", usersTab);
-        Assert.Contains("bp-user-management-table", usersTab);
-        Assert.Contains("bp-console-table", usersTab);
-        Assert.Contains("<th>Username</th>", usersTab);
-        Assert.Contains("<th>Email</th>", usersTab);
-        Assert.Contains("<th>Cluster memberships</th>", usersTab);
-        Assert.Contains("<th class=\"bp-action-cell\">Actions</th>", usersTab);
-        Assert.Contains("memberships.Count", usersTab);
-        Assert.Contains("data-user-drawer-toggle", usersTab);
-        Assert.Contains("bp-user-drawer-row", usersTab);
-        Assert.Contains("bp-user-drawer-grid", usersTab);
-        Assert.Contains("Account email (read-only)", usersTab);
-        Assert.Contains("readonly", usersTab);
-        Assert.Contains("bp-user-membership-add-row", usersTab);
-        Assert.Contains("Access is app-wide", usersTab);
-        Assert.DoesNotContain("<th>System role</th>", usersTab);
-        Assert.DoesNotContain("Quick add membership", usersTab);
-        Assert.DoesNotContain("bp-user-management-list", usersTab);
-        Assert.DoesNotContain("bp-user-card", usersTab);
-        Assert.Contains(".bp-user-create-strip", css);
-        Assert.Contains(".bp-user-management-table", css);
-        Assert.Contains(".bp-user-drawer-grid", css);
-        Assert.Contains(".bp-user-membership-add-row", css);
-    }
-
     [Fact]
     public void UserPasswordChanges_ShouldRequireConfirmationAndUseIconRevealButtons()
     {
@@ -96,8 +60,8 @@ public sealed class AdminDenseConsoleLayoutTests
         var accountController = File.ReadAllText(RepoFile("web", "Controllers", "AccountController.cs"));
 
         Assert.Contains("name=\"passwordConfirmation\"", clusters);
-        Assert.Contains("Confirm password", clusters);
-        Assert.Contains("data-password-reveal", clusters);
+        Assert.Contains("Type it again", clusters);
+        Assert.Contains("_PasswordReveal", clusters);
         Assert.Contains("<svg class=\"bp-action-icon\"", clusters);
         Assert.Contains("name=\"passwordConfirmation\"", account);
         Assert.Contains("Confirm password", account);
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -93,7 +93,6 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("bp-confirm-modal", clusterTokens);
         Assert.Contains("data-confirm-action", clusters);
         Assert.Contains("data-confirm-action", clusterTokens);
-        Assert.Contains("bp-user-management-table", clusters);
         Assert.Contains("bp-user-management-table", clusterUsers);
         Assert.DoesNotContain("class=\"bp-tag\"", clusters);
         Assert.DoesNotContain("class=\"bp-tag\"", clusterUsers);
@@ -153,7 +152,7 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("TempData[\"ClusterCreateName\"]", adminController);
         Assert.Contains("TempData[\"ClusterCreateId\"]", adminController);
         Assert.Contains("Cluster ID already exists.", adminController);
-        Assert.Contains("[\"member\"] = \"Cluster Member\"", clusters);
+        Assert.Contains("[\"member\"] = \"Cluster member (sees only their clusters' batteries)\"", clusters);
         Assert.Contains("selected=\"@(option.Key.Equals(\"member\"", clusters);
         Assert.Contains("data-open-user", clusters);
         Assert.Contains("data-open-user", clusterUsers);
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -7,7 +7,7 @@ public sealed class AdminFeedbackImplementationTests
     public void CreateUserDropdown_ShouldIncludeGlobalReportRoles()
     {
         var clustersView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var createFormStart = clustersView.IndexOf("action=\"/admin/clusters/save-user\" class=\"bp-user-create-strip\"", StringComparison.Ordinal);
+        var createFormStart = clustersView.IndexOf("<dialog id=\"add-user-dialog\"", StringComparison.Ordinal);
         Assert.True(createFormStart >= 0);
         var createFormEnd = clustersView.IndexOf("</form>", createFormStart, StringComparison.Ordinal);
         Assert.True(createFormEnd > createFormStart);
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -12,12 +12,12 @@ public sealed class AdminTestFeedbackFixesTests
         var accessControl = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
 
         Assert.Contains(">Access", adminUsers);
-        Assert.Contains("Global Admin", adminUsers);
-        Assert.Contains("Cluster Member", adminUsers);
-        Assert.Contains("Cluster Admin", adminUsers);
-        Assert.Contains("Normal User", adminUsers);
-        Assert.Contains("Notified Body", adminUsers);
-        Assert.Contains("Market Surveillance Authorities", adminUsers);
+        Assert.Contains("Global admin", adminUsers);
+        Assert.Contains("Cluster member", adminUsers);
+        Assert.Contains("Cluster admin", adminUsers);
+        Assert.Contains("[\"member\"] = \"User\"", adminUsers);
+        Assert.Contains("Notified body", adminUsers);
+        Assert.Contains("Market surveillance authority", adminUsers);
         Assert.Contains("Commission", adminUsers);
         Assert.Contains("Legitimate interest: repair, repurposing, remanufacturing", adminUsers);
         Assert.Contains("Legitimate interest: recycling and battery treatment", adminUsers);
diff --git a/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs b/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs
--- a/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs
@@ -26,9 +26,9 @@ public sealed class AppRolesTests
         var users = View("Admin", "Clusters.cshtml");
 
         Assert.Equal(
-            ["admin", "notifiedBody", "marketSurveillanceAuthority", "commission", "liRepair", "liRecycling", "liTreatment", "member"],
+            ["member", "admin", "notifiedBody", "marketSurveillanceAuthority", "commission", "liRepair", "liRecycling", "liTreatment"],
             Keys(users, "globalAccessOptions"));
-        Assert.Equal(["clusterAdmin", "member"], Keys(users, "clusterMembershipRoleOptions"));
+        Assert.Equal(["member", "clusterAdmin"], Keys(users, "clusterMembershipRoleOptions"));
         Assert.DoesNotContain("legitimateInterest", users, StringComparison.Ordinal);
         Assert.DoesNotContain("Person with Legitimate Interest", users, StringComparison.Ordinal);
     }
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -184,7 +184,7 @@ public sealed class MobileThemeSessionLayoutTests
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var program = File.ReadAllText(RepoFile("web", "Program.cs"));
 
-        Assert.Contains("Global session timeout", view);
+        Assert.Contains("<h2>Signing out</h2>", view);
         Assert.Contains("name=\"sessionTimeoutMinutes\"", view);
         Assert.Contains("/admin/clusters/session-timeout", view);
         Assert.Contains("[HttpPost(\"clusters/session-timeout\")]", controller);
```

`BatteryPassWeb.Tests/Look/UsersTabTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the Users tab. "Add user" opens a window, the list shows each person's access and clusters and can be
/// searched, a row opens to name and access, clusters, a new password and deleting the account, and the sign-out time is a
/// small card at the end.</summary>
public sealed class UsersTabTests
{
    private static string Page => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));

    private static string Tab
    {
        get
        {
            var page = Page;
            var start = page.IndexOf("@if (selectedTab == \"users\")", StringComparison.Ordinal);
            var end = page.IndexOf("@if (selectedTab == \"api-token-management\")", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return page[start..end];
        }
    }

    [Fact]
    public void AddUser_ShouldOpenAWindowWithTodaysFields()
    {
        var tab = Tab;
        var window = tab[tab.IndexOf("<dialog id=\"add-user-dialog\"", StringComparison.Ordinal)..tab.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"add-user-dialog\">Add user</button>", tab, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/save-user\"", window, StringComparison.Ordinal);
        foreach (var field in new[] { "name=\"name\"", "name=\"email\"", "name=\"password\"", "name=\"passwordConfirmation\"", "name=\"systemRole\"" })
        {
            Assert.Contains(field, window, StringComparison.Ordinal);
        }

        Assert.Contains("selected=\"@(option.Key.Equals(\"member\", StringComparison.OrdinalIgnoreCase))\"", window, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_PasswordReveal\")", window, StringComparison.Ordinal);
    }

    [Fact]
    public void EachUser_ShouldShowAccessAndClustersAndOpenInPlace()
    {
        var tab = Tab;
        var row = tab[tab.IndexOf("<details class=\"bp-row\" data-open-user=\"@user.Email\"", StringComparison.Ordinal)..tab.IndexOf("</details>", StringComparison.Ordinal)];

        Assert.Contains("data-search-text=\"@searchText\"", row, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag\">@AccessTag(selectedGlobalAccess)</span>", row, StringComparison.Ordinal);
        Assert.Contains("@ClusterName(membership.ClusterId)@(membership.Role == \"clusterAdmin\" ? \" · admin\" : \"\")", row, StringComparison.Ordinal);
        Assert.Contains("<h3>Name and access</h3>", row, StringComparison.Ordinal);
        Assert.Contains("<h3>Clusters</h3>", row, StringComparison.Ordinal);
        Assert.Contains("<h3>New password</h3>", row, StringComparison.Ordinal);
        Assert.Equal(2, row.Split("action=\"/admin/clusters/save-user\"").Length - 1);
        Assert.Contains("action=\"/admin/clusters/delete-user\"", row, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/clusters/assign-user\"", row, StringComparison.Ordinal);
        Assert.True(row.IndexOf("<div class=\"bp-danger-zone\">", StringComparison.Ordinal) > row.IndexOf("<h3>New password</h3>", StringComparison.Ordinal));
        Assert.Contains("action=\"/admin/clusters/delete-account\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void TheList_ShouldBeSearchable()
    {
        var tab = Tab;
        var script = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "js", "bp-admin-pages.js"));

        Assert.Contains("<section class=\"bp-card bp-list-card mt-4\" data-list aria-label=\"Users\">", tab, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Search name, email, access or cluster\" data-list-search", tab, StringComparison.Ordinal);
        Assert.Contains("data-list-no-results hidden>No users match the search.</p>", tab, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('[data-list-search]')", script, StringComparison.Ordinal);
        Assert.Contains("document.querySelectorAll('details[data-open-user]')", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-user-drawer-toggle", Page, StringComparison.Ordinal);
    }

    [Fact]
    public void SigningOut_ShouldBeASmallCardAtTheEnd()
    {
        var tab = Tab;

        Assert.True(tab.IndexOf("<h2>Signing out</h2>", StringComparison.Ordinal) > tab.IndexOf("</details>", StringComparison.Ordinal));
        Assert.Contains("action=\"/admin/clusters/session-timeout\"", tab, StringComparison.Ordinal);
        Assert.Contains("name=\"sessionTimeoutMinutes\" aria-label=\"Sign out after\"", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_ShouldBeInSentenceCase()
    {
        var page = Page;

        Assert.Contains("[\"member\"] = \"Cluster member (sees only their clusters' batteries)\"", page, StringComparison.Ordinal);
        Assert.Contains("[\"admin\"] = \"Global admin (the team)\"", page, StringComparison.Ordinal);
        Assert.Contains("[\"clusterAdmin\"] = \"Cluster admin\"", page, StringComparison.Ordinal);
        Assert.Contains("[\"member\"] = \"User\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Normal User", page, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Global Admin\";", page, StringComparison.Ordinal);
        Assert.Contains("data-password-reveal", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_PasswordReveal.cshtml")), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AppRolesTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.UsersTabTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Failed:    13, Passed:    36`: the five new `UsersTabTests`, and eight updated tests that expect the new Users tab and its sentence-case labels.

- [ ] **Step 3: Write `_PasswordReveal.cshtml`**

`web/Views/Shared/_PasswordReveal.cshtml`:

```cshtml
@* The eye button beside a password field; bp-admin-pages.js shows or hides what was typed. *@
<button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password" title="Show password">
    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
        <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z" />
    </svg>
</button>
```

- [ ] **Step 4: Rebuild the tab**

Patch `3e-task3-code.patch` (`web/Views/Admin/Clusters.cshtml`). Save the block as `../3e-task3-code.patch`, outside the worktree, and run `git apply ../3e-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -6,21 +6,22 @@
     var clustersById = Model.Clusters.ToDictionary(cluster => cluster.ClusterId, cluster => cluster, StringComparer.OrdinalIgnoreCase);
     var clusterCreateName = TempData["ClusterCreateName"]?.ToString() ?? string.Empty;
     var clusterCreateId = TempData["ClusterCreateId"]?.ToString() ?? string.Empty;
+    // Access across the app, in sentence case; the first is the default for a new user.
     var globalAccessOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
     {
-        ["admin"] = "Global Admin",
-        ["notifiedBody"] = "Notified Body",
-        ["marketSurveillanceAuthority"] = "Market Surveillance Authorities",
+        ["member"] = "Cluster member (sees only their clusters' batteries)",
+        ["admin"] = "Global admin (the team)",
+        ["notifiedBody"] = "Notified body",
+        ["marketSurveillanceAuthority"] = "Market surveillance authority",
         ["commission"] = "Commission",
         ["liRepair"] = "Legitimate interest: repair, repurposing, remanufacturing",
         ["liRecycling"] = "Legitimate interest: recycling and battery treatment",
-        ["liTreatment"] = "Legitimate interest: authorised or WEEE treatment",
-        ["member"] = "Cluster Member"
+        ["liTreatment"] = "Legitimate interest: authorised or WEEE treatment"
     };
     var clusterMembershipRoleOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
     {
-        ["clusterAdmin"] = "Cluster Admin",
-        ["member"] = "Normal User"
+        ["member"] = "User",
+        ["clusterAdmin"] = "Cluster admin"
     };
     var sessionTimeoutOptions = new Dictionary<int, string>
     {
@@ -33,16 +34,28 @@
     };
     string MembershipRoleLabel(string role) => role switch
     {
-        "notifiedBody" => "Notified Body",
-        "marketSurveillanceAuthority" => "Market Surveillance Authorities",
+        "notifiedBody" => "Notified body",
+        "marketSurveillanceAuthority" => "Market surveillance authority",
         "commission" => "Commission",
         "liRepair" => "Legitimate interest: repair, repurposing, remanufacturing",
         "liRecycling" => "Legitimate interest: recycling and battery treatment",
         "liTreatment" => "Legitimate interest: authorised or WEEE treatment",
-        _ => clusterMembershipRoleOptions.TryGetValue(role, out var label) ? label : "Normal User"
+        _ => clusterMembershipRoleOptions.TryGetValue(role, out var label) ? label : "User"
     };
+    // The short access tag on a user's row.
+    string AccessTag(string access) => access switch
+    {
+        "admin" => "Global admin",
+        "notifiedBody" => "Notified body",
+        "marketSurveillanceAuthority" => "Market surveillance",
+        "commission" => "Commission",
+        "liRepair" => "Legitimate interest: repair",
+        "liRecycling" => "Legitimate interest: recycling",
+        "liTreatment" => "Legitimate interest: treatment",
+        _ => "Cluster member"
+    };
+    string ClusterName(string clusterId) => clustersById.TryGetValue(clusterId, out var cluster) ? cluster.Name : clusterId;
     string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
-    string MembershipCountLabel(int count) => $"{count} {(count == 1 ? "membership" : "memberships")}";
     string TokenClusterPickerName(string name) => name.StartsWith("Default ", StringComparison.OrdinalIgnoreCase) ? name["Default ".Length..] : name;
     string TokenDisplayName(BatteryPassWeb.Models.ViewModels.ApiTokenViewModel token)
     {
@@ -261,41 +274,27 @@
 
     @if (selectedTab == "users")
     {
-        var userRowIndex = 0;
-        <div class="bp-management-shell mt-4">
-            <section class="bp-card bp-session-timeout-card bp-console-form-card">
-                <div>
-                    <span class="bp-summary-title">Security</span>
-                    <h2 class="h5 mb-2">Global session timeout</h2>
-                    <p class="text-secondary mb-0">Signed-in users are logged out after this period of inactivity.</p>
-                </div>
-                <form method="post" action="/admin/clusters/session-timeout" class="bp-session-timeout-form">
-                    @Html.AntiForgeryToken()
-                    <label>
-                        Inactivity timeout
-                        <span class="bp-select-shell">
-                            <select name="sessionTimeoutMinutes" aria-label="Global session timeout">
-                                @foreach (var option in sessionTimeoutOptions)
-                                {
-                                    <option value="@option.Key" selected="@(option.Key == Model.SessionTimeoutMinutes)">@option.Value</option>
-                                }
-                            </select>
-                        </span>
-                    </label>
-                    <button type="submit" class="bp-primary-button">Save timeout</button>
-                </form>
-            </section>
+        <section class="bp-card bp-console-form-card bp-tab-intro mt-4">
+            <div>
+                <h2>Users</h2>
+                <p>Everyone who can sign in: their access across the app, and the clusters they belong to.</p>
+            </div>
+            <div class="bp-tab-intro-actions">
+                <span class="bp-counts">@Count(Model.Users.Count, "user", "users")</span>
+                <button type="button" class="bp-primary-button" data-open-dialog="add-user-dialog">Add user</button>
+            </div>
+        </section>
 
-            <section class="bp-card bp-management-create-card bp-console-form-card">
+        <dialog id="add-user-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide bp-console-form-card" aria-labelledby="add-user-title">
+            <form method="post" action="/admin/clusters/save-user" class="bp-dialog-form">
+                @Html.AntiForgeryToken()
                 <div>
-                    <span class="bp-summary-title">User setup</span>
-                    <h2 class="h5 mb-2">Add or update user</h2>
-                    <p class="text-secondary mb-0">Create the account first, then manage cluster memberships from each user's action menu.</p>
+                    <h2 id="add-user-title">Add a user</h2>
+                    <p class="bp-help-text mb-0">Their clusters are added afterwards, from their row. Typing the email of an existing user updates that user.</p>
                 </div>
-                <form method="post" action="/admin/clusters/save-user" class="bp-user-create-strip">
-                    @Html.AntiForgeryToken()
+                <div class="bp-field-pairs">
                     <label>
-                        Username
+                        Name
                         <input type="text" name="name" placeholder="Display name" />
                     </label>
                     <label>
@@ -305,233 +304,222 @@
                     <label>
                         Password
                         <span class="bp-password-field">
-                            <input type="password" name="password" placeholder="Required for new users" />
-                            <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password" title="Show password">
-                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                    <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
-                                    <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z" />
-                                </svg>
-                            </button>
+                            <input type="password" name="password" placeholder="Password" autocomplete="new-password" />
+                            @await Html.PartialAsync("_PasswordReveal")
                         </span>
                     </label>
                     <label>
-                        Confirm password
+                        Type it again
                         <span class="bp-password-field">
-                            <input type="password" name="passwordConfirmation" placeholder="Type password again" />
-                            <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password" title="Show password">
-                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                    <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
-                                    <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z" />
-                                </svg>
-                            </button>
+                            <input type="password" name="passwordConfirmation" placeholder="Type it again" autocomplete="new-password" />
+                            @await Html.PartialAsync("_PasswordReveal")
                         </span>
                     </label>
-                    <label>
-                        Access
-                        <span class="bp-select-shell">
-                            <select name="systemRole">
-                                @foreach (var option in globalAccessOptions)
+                </div>
+                <label>
+                    Access across the app
+                    <span class="bp-select-shell">
+                        <select name="systemRole">
+                            @foreach (var option in globalAccessOptions)
+                            {
+                                <option value="@option.Key" selected="@(option.Key.Equals("member", StringComparison.OrdinalIgnoreCase))">@option.Value</option>
+                            }
+                        </select>
+                    </span>
+                </label>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                    <button type="submit" class="bp-primary-button">Add user</button>
+                </div>
+            </form>
+        </dialog>
+
+        <section class="bp-card bp-list-card mt-4" data-list aria-label="Users">
+            <div class="bp-list-tools">
+                <h2>All users</h2>
+                <label class="bp-list-search">
+                    <span class="visually-hidden">Search users</span>
+                    <input type="search" placeholder="Search name, email, access or cluster" data-list-search />
+                </label>
+            </div>
+            @if (Model.Users.Count == 0)
+            {
+                <p class="bp-list-empty">No users yet.</p>
+            }
+            else
+            {
+                <div class="bp-list-head" aria-hidden="true"><span>User</span><span>Access</span><span>Clusters</span><span></span></div>
+                <p class="bp-list-empty" data-list-no-results hidden>No users match the search.</p>
+            }
+            @foreach (var user in Model.Users.OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase))
+            {
+                var memberships = Model.Memberships
+                    .Where(membership => membership.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
+                    .OrderBy(membership => membership.ClusterId, StringComparer.OrdinalIgnoreCase)
+                    .ToList();
+                var displayName = string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name;
+                var selectedGlobalAccess = GlobalAccessValue(user.Roles);
+                var searchText = $"{displayName} {user.Email} {AccessTag(selectedGlobalAccess)} {string.Join(" ", memberships.Select(membership => ClusterName(membership.ClusterId)))}";
+                <details class="bp-row" data-open-user="@user.Email" data-search-text="@searchText">
+                    <summary>
+                        <span class="bp-row-title"><strong>@displayName</strong><small>@user.Email</small></span>
+                        <span><span class="bp-row-label">Access</span><span class="bp-soft-tag">@AccessTag(selectedGlobalAccess)</span></span>
+                        @if (memberships.Count == 0)
+                        {
+                            <span class="text-secondary"><span class="bp-row-label">Clusters</span>None</span>
+                        }
+                        else
+                        {
+                            <span class="bp-tags">
+                                <span class="bp-row-label">Clusters</span>
+                                @foreach (var membership in memberships)
                                 {
-                                    <option value="@option.Key" selected="@(option.Key.Equals("member", StringComparison.OrdinalIgnoreCase))">@option.Value</option>
+                                    <span class="bp-soft-tag">@ClusterName(membership.ClusterId)@(membership.Role == "clusterAdmin" ? " · admin" : "")</span>
                                 }
-                            </select>
-                        </span>
-                    </label>
-                    <button type="submit" class="bp-primary-button">Create User</button>
-                </form>
-            </section>
-
-            <section class="bp-card bp-table-card p-0">
-                <div class="bp-table-scroll">
-                    <table class="table align-middle mb-0 bp-console-table bp-user-management-table bp-mobile-card-table">
-                        <thead>
-                            <tr>
-                                <th>Username</th>
-                                <th>Email</th>
-                                <th>Cluster memberships</th>
-                                <th class="bp-action-cell">Actions</th>
-                            </tr>
-                        </thead>
-                        <tbody>
-                            @if (Model.Users.Count == 0)
+                            </span>
+                        }
+                        <span class="bp-row-open"><span class="is-open">Open</span><span class="is-close">Close</span></span>
+                    </summary>
+                    <div class="bp-row-panel bp-console-form-card">
+                        <section class="bp-subcard">
+                            <h3>Name and access</h3>
+                            <form method="post" action="/admin/clusters/save-user">
+                                @Html.AntiForgeryToken()
+                                <input type="hidden" name="email" value="@user.Email" />
+                                <label>
+                                    Name
+                                    <input type="text" name="name" value="@displayName" />
+                                </label>
+                                <label>
+                                    Access across the app
+                                    <span class="bp-select-shell">
+                                        <select name="systemRole">
+                                            @foreach (var option in globalAccessOptions)
+                                            {
+                                                <option value="@option.Key" selected="@(option.Key.Equals(selectedGlobalAccess, StringComparison.OrdinalIgnoreCase))">@option.Value</option>
+                                            }
+                                        </select>
+                                    </span>
+                                </label>
+                                <p class="text-secondary small mb-0">What they see in a cluster depends on their role there.</p>
+                                <div><button type="submit" class="bp-primary-button bp-button-compact">Save</button></div>
+                            </form>
+                        </section>
+                        <section class="bp-subcard">
+                            <h3>Clusters</h3>
+                            @if (memberships.Count == 0)
                             {
-                                <tr>
-                                    <td colspan="4" class="text-secondary">No users have been created yet.</td>
-                                </tr>
+                                <p class="text-secondary">Not in any cluster yet.</p>
                             }
-                            @foreach (var user in Model.Users.OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase))
+                            else
                             {
-                                var memberships = Model.Memberships
-                                    .Where(membership => membership.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
-                                    .OrderBy(membership => membership.ClusterId, StringComparer.OrdinalIgnoreCase)
-                                    .ToList();
-                                var displayName = string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name;
-                                var selectedGlobalAccess = GlobalAccessValue(user.Roles);
-                                var drawerId = $"admin-user-drawer-{userRowIndex++}";
-
-                                <tr>
-                                    <td class="bp-user-name-cell" data-label="Username">
-                                        <strong>@displayName</strong>
-                                    </td>
-                                    <td data-label="Email">
-                                        <code>@user.Email</code>
-                                    </td>
-                                    <td data-label="Cluster memberships">
-                                        <span class="bp-user-count-pill">@MembershipCountLabel(memberships.Count)</span>
-                                    </td>
-                                    <td class="bp-action-cell" data-label="Actions">
-                                        <button type="button" class="bp-action-button bp-user-drawer-toggle" data-user-drawer-toggle data-open-user="@user.Email" aria-expanded="false" aria-controls="@drawerId" aria-label="Open user actions" title="Open user actions" data-tooltip="Open user actions">
-                                            <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                <path d="m6 9 6 6 6-6" />
-                                            </svg>
-                                        </button>
-                                    </td>
-                                </tr>
-                                <tr id="@drawerId" class="bp-user-drawer-row" data-open-user="@user.Email" hidden>
-                                    <td colspan="4">
-                                        <div class="bp-user-drawer-grid bp-console-form-card">
-                                            <section class="bp-user-drawer-panel">
-                                                <h3>Edit name and access</h3>
-                                                <form method="post" action="/admin/clusters/save-user" class="bp-user-profile-form">
-                                                    @Html.AntiForgeryToken()
-                                                    <input type="hidden" name="email" value="@user.Email" />
-                                                    <label>
-                                                        Username
-                                                        <input type="text" name="name" value="@displayName" />
-                                                    </label>
-                                                    <label>
-                                                        Account email (read-only)
-                                                        <input type="email" value="@user.Email" readonly />
-                                                    </label>
-                                                    <label>
-                                                        Access
-                                                        <span class="bp-select-shell">
-                                                            <select name="systemRole">
-                                                                @foreach (var option in globalAccessOptions)
-                                                                {
-                                                                    <option value="@option.Key" selected="@(option.Key.Equals(selectedGlobalAccess, StringComparison.OrdinalIgnoreCase))">@option.Value</option>
-                                                                }
-                                                            </select>
-                                                        </span>
-                                                    </label>
-                                                    <p class="bp-user-access-note">Access is app-wide. Cluster roles can differ from one cluster membership to another.</p>
-                                                    <div class="bp-user-drawer-actions">
-                                                        <button type="submit" class="bp-primary-button">Save profile</button>
-                                                    </div>
-                                                </form>
-                                            </section>
-
-                                            <section class="bp-user-drawer-panel">
-                                                <h3>Cluster memberships</h3>
-                                                @if (memberships.Count == 0)
+                                <ul class="bp-member-list">
+                                    @foreach (var membership in memberships)
+                                    {
+                                        <li>
+                                            <span>@ClusterName(membership.ClusterId)</span>
+                                            <form method="post" action="/admin/clusters/delete-user" class="d-flex align-items-center gap-2">
+                                                @Html.AntiForgeryToken()
+                                                <input type="hidden" name="email" value="@membership.Email" />
+                                                <input type="hidden" name="clusterId" value="@membership.ClusterId" />
+                                                <span class="bp-soft-tag">@MembershipRoleLabel(membership.Role)</span>
+                                                <button type="submit" class="bp-icon-button is-danger" aria-label="Remove from @ClusterName(membership.ClusterId)" title="Remove from this cluster">
+                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
+                                                        <path d="M18 6 6 18" />
+                                                        <path d="m6 6 12 12" />
+                                                    </svg>
+                                                </button>
+                                            </form>
+                                        </li>
+                                    }
+                                </ul>
+                            }
+                            @if (Model.Clusters.Count > 0)
+                            {
+                                <form method="post" action="/admin/clusters/assign-user" class="bp-add-membership">
+                                    @Html.AntiForgeryToken()
+                                    <input type="hidden" name="email" value="@user.Email" />
+                                    <label>
+                                        Add to
+                                        <span class="bp-select-shell">
+                                            <select name="clusterId">
+                                                @foreach (var cluster in Model.Clusters)
                                                 {
-                                                    <p class="text-secondary mb-0">No cluster membership</p>
+                                                    <option value="@cluster.ClusterId">@cluster.Name</option>
                                                 }
-                                                else
+                                            </select>
+                                        </span>
+                                    </label>
+                                    <label>
+                                        As
+                                        <span class="bp-select-shell">
+                                            <select name="role">
+                                                @foreach (var option in clusterMembershipRoleOptions)
                                                 {
-                                                    <div class="bp-user-membership-list">
-                                                        @foreach (var membership in memberships)
-                                                        {
-                                                            var clusterName = clustersById.TryGetValue(membership.ClusterId, out var cluster) ? cluster.Name : membership.ClusterId;
-                                                            <form method="post" action="/admin/clusters/delete-user" class="bp-user-membership-row">
-                                                                @Html.AntiForgeryToken()
-                                                                <input type="hidden" name="email" value="@membership.Email" />
-                                                                <input type="hidden" name="clusterId" value="@membership.ClusterId" />
-                                                                <span class="bp-user-membership-name">@clusterName</span>
-                                                                <span class="bp-user-membership-role">@MembershipRoleLabel(membership.Role)</span>
-                                                                <button type="submit" class="bp-icon-button is-danger" aria-label="Remove membership">
-                                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                                        <path d="M18 6 6 18" />
-                                                                        <path d="m6 6 12 12" />
-                                                                    </svg>
-                                                                </button>
-                                                            </form>
-                                                        }
-                                                    </div>
+                                                    <option value="@option.Key">@option.Value</option>
                                                 }
-                                                <form method="post" action="/admin/clusters/assign-user" class="bp-user-membership-add-row">
-                                                    @Html.AntiForgeryToken()
-                                                    <input type="hidden" name="email" value="@user.Email" />
-                                                    <label>
-                                                        Add cluster
-                                                        <span class="bp-select-shell">
-                                                            <select name="clusterId" class="form-select form-select-sm">
-                                                                @foreach (var cluster in Model.Clusters)
-                                                                {
-                                                                    <option value="@cluster.ClusterId">@cluster.Name</option>
-                                                                }
-                                                            </select>
-                                                        </span>
-                                                    </label>
-                                                    <label>
-                                                        Membership role
-                                                        <span class="bp-select-shell">
-                                                            <select name="role" class="form-select form-select-sm">
-                                                                @foreach (var option in clusterMembershipRoleOptions)
-                                                                {
-                                                                    <option value="@option.Key">@option.Value</option>
-                                                                }
-                                                            </select>
-                                                        </span>
-                                                    </label>
-                                                    <button type="submit" class="bp-primary-button bp-button-compact" aria-label="Save membership">Save</button>
-                                                </form>
-                                            </section>
-
-                                            <section class="bp-user-drawer-panel">
-                                                <h3>Change password</h3>
-                                                <form method="post" action="/admin/clusters/save-user" class="bp-user-password-form">
-                                                    @Html.AntiForgeryToken()
-                                                    <input type="hidden" name="email" value="@user.Email" />
-                                                    <input type="hidden" name="name" value="@displayName" />
-                                                    <input type="hidden" name="systemRole" value="@selectedGlobalAccess" />
-                                                    <label>
-                                                        New password
-                                                        <span class="bp-password-field">
-                                                            <input type="password" name="password" placeholder="New password" />
-                                                            <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password" title="Show password">
-                                                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                                    <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
-                                                                    <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z" />
-                                                                </svg>
-                                                            </button>
-                                                        </span>
-                                                    </label>
-                                                    <label>
-                                                        Confirm password
-                                                        <span class="bp-password-field">
-                                                            <input type="password" name="passwordConfirmation" placeholder="Type password again" />
-                                                            <button type="button" class="bp-password-reveal" data-password-reveal aria-label="Show password" title="Show password">
-                                                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                                    <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
-                                                                    <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6Z" />
-                                                                </svg>
-                                                            </button>
-                                                        </span>
-                                                    </label>
-                                                    <div class="bp-user-drawer-actions">
-                                                        <button type="submit" class="bp-primary-button">Save password</button>
-                                                    </div>
-                                                </form>
-                                            </section>
-                                            <section class="bp-user-drawer-panel">
-                                                <h3>Delete account</h3>
-                                                <p class="text-secondary">Removes the account and its cluster memberships. It can't be undone; the audit log keeps what the account did.</p>
-                                                <form method="post" action="/admin/clusters/delete-account">
-                                                    @Html.AntiForgeryToken()
-                                                    <input type="hidden" name="email" value="@user.Email" />
-                                                    <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Delete account" data-confirm-message="Delete the account @user.Email and its cluster memberships? It can't be undone." data-confirm-submit="Delete account">Delete account</button>
-                                                </form>
-                                            </section>
-                                        </div>
-                                    </td>
-                                </tr>
+                                            </select>
+                                        </span>
+                                    </label>
+                                    <button type="submit" class="bp-secondary-button">Add</button>
+                                </form>
                             }
-                        </tbody>
-                    </table>
-                </div>
-            </section>
-        </div>
+                        </section>
+                        <section class="bp-subcard">
+                            <h3>New password</h3>
+                            <form method="post" action="/admin/clusters/save-user">
+                                @Html.AntiForgeryToken()
+                                <input type="hidden" name="email" value="@user.Email" />
+                                <input type="hidden" name="name" value="@displayName" />
+                                <input type="hidden" name="systemRole" value="@selectedGlobalAccess" />
+                                <label>
+                                    New password
+                                    <span class="bp-password-field">
+                                        <input type="password" name="password" placeholder="New password" autocomplete="new-password" />
+                                        @await Html.PartialAsync("_PasswordReveal")
+                                    </span>
+                                </label>
+                                <label>
+                                    Type it again
+                                    <span class="bp-password-field">
+                                        <input type="password" name="passwordConfirmation" placeholder="Type it again" autocomplete="new-password" />
+                                        @await Html.PartialAsync("_PasswordReveal")
+                                    </span>
+                                </label>
+                                <div><button type="submit" class="bp-secondary-button bp-button-compact">Save password</button></div>
+                            </form>
+                        </section>
+                        <div class="bp-danger-zone">
+                            <p>Deleting the account also removes its cluster memberships. It can't be undone; the audit log keeps what it did.</p>
+                            <form method="post" action="/admin/clusters/delete-account">
+                                @Html.AntiForgeryToken()
+                                <input type="hidden" name="email" value="@user.Email" />
+                                <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Delete account" data-confirm-message="Delete the account @user.Email and its cluster memberships? It can't be undone." data-confirm-submit="Delete account">Delete account</button>
+                            </form>
+                        </div>
+                    </div>
+                </details>
+            }
+        </section>
+
+        <section class="bp-card bp-console-form-card bp-setting-card mt-4">
+            <div>
+                <h2>Signing out</h2>
+                <p>Everyone is signed out after this long without activity.</p>
+            </div>
+            <form method="post" action="/admin/clusters/session-timeout">
+                @Html.AntiForgeryToken()
+                <span class="bp-select-shell">
+                    <select name="sessionTimeoutMinutes" aria-label="Sign out after">
+                        @foreach (var option in sessionTimeoutOptions)
+                        {
+                            <option value="@option.Key" selected="@(option.Key == Model.SessionTimeoutMinutes)">@option.Value</option>
+                        }
+                    </select>
+                </span>
+                <button type="submit" class="bp-secondary-button">Save</button>
+            </form>
+        </section>
     }
 
     @if (selectedTab == "api-token-management")
@@ -771,34 +759,6 @@
     <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
     <script>
         (function () {
-            document.querySelectorAll('[data-user-drawer-toggle]').forEach((button) => {
-                button.addEventListener('click', () => {
-                    const drawerId = button.getAttribute('aria-controls');
-                    const drawer = drawerId ? document.getElementById(drawerId) : null;
-                    if (!drawer) {
-                        return;
-                    }
-
-                    const expanded = button.getAttribute('aria-expanded') === 'true';
-                    button.setAttribute('aria-expanded', expanded ? 'false' : 'true');
-                    drawer.hidden = expanded;
-                });
-            });
-
-            const openUser = new URLSearchParams(window.location.search).get('openUser');
-            if (openUser) {
-                const drawer = Array.from(document.querySelectorAll('.bp-user-drawer-row[data-open-user]'))
-                    .find((row) => String(row.dataset.openUser || '').toLowerCase() === openUser.toLowerCase());
-                const toggle = drawer?.id
-                    ? document.querySelector(`[data-user-drawer-toggle][aria-controls="${CSS.escape(drawer.id)}"]`)
-                    : null;
-                if (drawer) {
-                    drawer.hidden = false;
-                    toggle?.setAttribute('aria-expanded', 'true');
-                    drawer.scrollIntoView({ block: 'center' });
-                }
-            }
-
             const tokenSearch = document.querySelector('[data-token-search]');
             const tokenRows = Array.from(document.querySelectorAll('[data-token-row]'));
             const tokenNoResults = document.querySelector('[data-token-no-results]');
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AppRolesTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.UsersTabTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    49`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2090`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/Dpp/AppRolesTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Look/UsersTabTests.cs web/Views/Shared/_PasswordReveal.cshtml web/Views/Admin/Clusters.cshtml
git commit -m "feat(dpp): the Users tab, with access and clusters on each row, a search and the sign-out time last" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 4: The API tokens tab and the demo tokens' names

"Create token" opens a window that says what each permission does. The list becomes the shared `_ApiTokenList` (Task 5 uses it too): names wrap, the ID sits under the name, and a row opens to copy the ID, regenerate (now asked first), switch off or on, and delete. The two bulk buttons wait under "More". The demo seed names its tokens after the cluster once, and makes its cluster tokens the cluster's own (`autoClusterId`), so an app restart adds none; clearing a demo cluster removes its own tokens with it and doesn't count them as real use.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Demo/DemoRestartTests.cs`
- Test: `BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs`
- Create: `web/Models/ViewModels/ApiTokenListViewModel.cs`
- Create: `web/Views/Shared/_ApiTokenList.cshtml`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3e-task4-tests.patch` (`BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`). Save the block as `../3e-task4-tests.patch`, outside the worktree, and run `git apply ../3e-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
@@ -2,64 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class AdminCredentialLayoutTests
 {
-    [Fact]
-    public void AdminApiTokenManagement_ShouldUseDenseTokenConsoleLayout()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var tokenTabStart = markup.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
-        var confirmModalStart = markup.IndexOf("@await Html.PartialAsync(\"_ConfirmModal\")", StringComparison.Ordinal);
-
-        Assert.True(tokenTabStart >= 0);
-        Assert.True(confirmModalStart > tokenTabStart);
-        var tokenTab = markup.Substring(tokenTabStart, confirmModalStart - tokenTabStart);
-
-        Assert.Contains("bp-token-console", tokenTab);
-        Assert.Contains("bp-token-console-summary", tokenTab);
-        Assert.Contains("bp-token-create-strip", tokenTab);
-        Assert.Contains("bp-token-create-topline", tokenTab);
-        Assert.Contains("bp-token-cluster-section-header", tokenTab);
-        Assert.Contains("bp-token-cluster-chip-picker", tokenTab);
-        Assert.Contains("bp-token-scope-select", tokenTab);
-        Assert.Contains("bp-token-filter-toolbar", tokenTab);
-        Assert.Contains("bp-token-management-table", tokenTab);
-        Assert.Contains("bp-token-name-column", tokenTab);
-        Assert.Contains("bp-token-id-column", tokenTab);
-        Assert.Contains("bp-token-access-column", tokenTab);
-        Assert.Contains("bp-token-scope-column", tokenTab);
-        Assert.Contains("bp-token-status-column", tokenTab);
-        Assert.Contains("bp-token-last-used-column", tokenTab);
-        Assert.Contains("bp-token-actions-column", tokenTab);
-        Assert.Contains("<th>Token ID</th>", tokenTab);
-        Assert.Contains("bp-token-id-cell", tokenTab);
-        Assert.Contains("Html.PartialAsync(\"_CopyIdButton\", token.TokenId)", tokenTab);
-        Assert.Contains("bp-token-scope-cell", tokenTab);
-        Assert.Contains("bp-token-scope-details", tokenTab);
-        Assert.Contains("bp-token-scope-summary", tokenTab);
-        Assert.Contains("bp-token-scope-list", tokenTab);
-        Assert.Contains("ScopeSummary(token)", tokenTab);
-        Assert.Contains("token.ClusterScopeNames", tokenTab);
-        Assert.Contains("bp-console-table", tokenTab);
-        Assert.Contains("bp-status-pill", tokenTab);
-        Assert.Contains("bp-icon-action-row", tokenTab);
-        Assert.Contains("/admin/api/tokens/cleanup-generated", tokenTab);
-        Assert.Contains("Clean up unused generated tokens", tokenTab);
-        Assert.Contains("aria-label=\"Regenerate token", tokenTab);
-        Assert.Contains("aria-label=\"Deactivate token", tokenTab);
-        Assert.Contains("aria-label=\"Activate token", tokenTab);
-        Assert.Contains("aria-label=\"Delete token", tokenTab);
-        Assert.Contains("@TokenClusterPickerName(cluster.Name)", tokenTab);
-        Assert.Contains("var tokenDisplayName = TokenDisplayName(token);", tokenTab);
-        Assert.Contains("<p class=\"bp-credential-name\">@tokenDisplayName</p>", tokenTab);
-        Assert.Contains("token.ClusterIdsLabel", tokenTab);
-        Assert.DoesNotContain("<p class=\"mb-0\">@token.ClusterIdsLabel</p>", tokenTab);
-        Assert.DoesNotContain("<p class=\"mb-0 small text-secondary\"><code class=\"bp-code-wrap\">@token.TokenId</code>", tokenTab);
-        Assert.DoesNotContain("<p class=\"bp-credential-name\">@token.Name</p>", tokenTab);
-        Assert.DoesNotContain("<strong>@cluster.Name</strong>", tokenTab);
-        Assert.DoesNotContain("<small>@cluster.ClusterId</small>", tokenTab);
-        Assert.DoesNotContain("bp-token-create-grid", tokenTab);
-        Assert.DoesNotContain("bp-action-row", tokenTab);
-    }
-
     [Fact]
     public void AdminCredentials_ShouldUseSingleManagementTabAndPasswordRevealControls()
     {
@@ -67,8 +9,7 @@ public sealed class AdminCredentialLayoutTests
         var clusterUsers = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("API Token Management", clusters);
-        Assert.DoesNotContain(">API tokens</a>", clusters);
+        Assert.Contains("<h2>API tokens</h2>", clusters);
         Assert.DoesNotContain(">Battery secrets</a>", clusters);
         Assert.DoesNotContain("Battery secrets", clusters);
         Assert.Contains("_PasswordReveal", clusters);
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -96,8 +96,6 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("bp-user-management-table", clusterUsers);
         Assert.DoesNotContain("class=\"bp-tag\"", clusters);
         Assert.DoesNotContain("class=\"bp-tag\"", clusterUsers);
-        Assert.Contains("bp-token-console", clusters);
-        Assert.Contains("bp-token-cluster-chip-picker", clusters);
         Assert.Contains("bp-token-cluster-chip-picker", clusterTokens);
         Assert.Contains(".bp-confirm-modal", css);
         Assert.Contains(".bp-token-cluster-chip-picker", css);
diff --git a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
--- a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
+++ b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
@@ -42,7 +42,7 @@ public sealed class CopyIdButtonLayoutTests
     {
         var files = new[]
         {
-            RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
+            RepoFile("web", "Views", "Shared", "_ApiTokenList.cshtml"),
             RepoFile("web", "Views", "Help", "Index.cshtml")
         };
 
```

`BatteryPassWeb.Tests/Demo/DemoRestartTests.cs`:

```csharp
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3e: an app restart between seeding and clearing. At its start the app gives every cluster without them a
/// read and a read-write token of its own (autoClusterId). The demo cluster tokens are those, so the start adds none; and a
/// demo cluster's own tokens go with it when it is cleared, which also repairs databases seeded before this fix.</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoRestartTests(MongoFixture mongo)
{
    private static Task RestartAsync(DemoRig rig) =>
        new ExternalApiInitializer(rig.Services.GetRequiredService<ExternalApiRepository>(), rig.Services.GetRequiredService<ClusterRepository>()).InitializeAsync();

    [Fact]
    public async Task ARestart_ShouldAddNoTokensAndClearingShouldStillRemoveEverything()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        await RestartAsync(rig);
        Assert.Equal(7, await rig.Count("apiTokens"));

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(0, await rig.Count("clusters"));
        Assert.Equal(0, await rig.Count("apiTokens"));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public async Task ClearingADatabaseThatAlreadyHasTheExtraTokens_ShouldRemoveThemWithTheirClusters()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        // As the seed made them before this fix: the demo cluster tokens weren't the cluster's own, so a restart added six.
        await rig.Database.GetCollection<BsonDocument>("apiTokens").UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Unset("autoClusterId"));
        await RestartAsync(rig);
        Assert.Equal(13, await rig.Count("apiTokens"));

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(0, await rig.Count("clusters"));
        Assert.Equal(0, await rig.Count("apiTokens"));
        Assert.Empty(result.Notes);
        Assert.Contains("13 API tokens removed", result.Done);
    }

    [Fact]
    public async Task ARealTokenOnADemoCluster_ShouldStillKeepIt()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        await RestartAsync(rig);
        await rig.Services.GetRequiredService<ExternalApiRepository>().CreateTokenAsync("North integration", ExternalTokenAccessMode.Read, ["demo-north"], false, false, "team@example.test");

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(1, await rig.Count("clusters"));
        Assert.Contains(result.Notes, note => note.StartsWith("Kept cluster demo-north: 1 real API tokens", StringComparison.Ordinal));
    }
}
```

`BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs`:

```csharp
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the API tokens tab. "Create token" opens a window that says what each permission does, the list is
/// shared with the cluster admins' page, names wrap with the ID under them, a row opens for regenerate, deactivate and
/// delete, and the two bulk buttons sit under "More". The demo tokens are named after their cluster once.</summary>
[Collection(MongoCollection.Name)]
public sealed class ApiTokensTabTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static string Tab
    {
        get
        {
            var page = Read("web", "Views", "Admin", "Clusters.cshtml");
            var start = page.IndexOf("@if (selectedTab == \"api-token-management\")", StringComparison.Ordinal);
            var end = page.IndexOf("@await Html.PartialAsync(\"_ConfirmModal\")", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            return page[start..end];
        }
    }

    private static string List => Read("web", "Views", "Shared", "_ApiTokenList.cshtml");

    [Fact]
    public void CreateToken_ShouldSayWhatEachPermissionDoes()
    {
        var tab = Tab;
        var window = tab[tab.IndexOf("<dialog id=\"create-token-dialog\"", StringComparison.Ordinal)..tab.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("data-open-dialog=\"create-token-dialog\">Create token</button>", tab, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/api/tokens/create\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"read\" checked /><strong>Read</strong><small>Reads the passports of its batteries.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"readWrite\" /><strong>Read + write</strong><small>Also sends live values. For all batteries, it also creates batteries.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"sign\" /><strong>Passport lifecycle</strong><small>Reads, and for all batteries publishes passports.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"accessMode\" value=\"readWriteSign\" /><strong>Full access</strong><small>All of the above.</small>", window, StringComparison.Ordinal);
        Assert.Contains("name=\"globalAccess\" value=\"off\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"globalAccess\" value=\"on\"", window, StringComparison.Ordinal);
        Assert.Contains("name=\"clusterIds\" value=\"@cluster.ClusterId\"", window, StringComparison.Ordinal);
        Assert.DoesNotContain("Full access and Passport Lifecycle create and publish batteries", tab, StringComparison.Ordinal);
    }

    [Fact]
    public void TheList_ShouldBeSharedWithTheClusterAdminsPage()
    {
        Assert.Contains("@await Html.PartialAsync(\"_ApiTokenList\", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, \"/admin/api/tokens\", true, \"No API tokens yet.\"))", Tab, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", Tab, StringComparison.Ordinal);
    }

    [Fact]
    public void EachToken_ShouldWrapItsNameAndOpenForItsActions()
    {
        var list = List;
        var row = list[list.IndexOf("<details class=\"bp-row\" data-token-row", StringComparison.Ordinal)..];

        Assert.Contains("<strong>@name</strong>\n                    <code>@token.TokenId</code>", row.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag\">@access</span>", row, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-soft-tag is-off\">Inactive</span>", row, StringComparison.Ordinal);
        Assert.Contains("\"Never\"", row, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_CopyIdButton\", token.TokenId)", row, StringComparison.Ordinal);
        Assert.Contains("action=\"@Model.ActionBase/regenerate\"", row, StringComparison.Ordinal);
        Assert.Contains("data-confirm-title=\"Regenerate token\"", row, StringComparison.Ordinal);
        Assert.Contains("@if (Model.CanSetActive)", row, StringComparison.Ordinal);
        Assert.Contains("action=\"@Model.ActionBase/set-active\"", row, StringComparison.Ordinal);
        Assert.True(row.IndexOf("<div class=\"bp-danger-zone\">", StringComparison.Ordinal) > row.IndexOf("set-active", StringComparison.Ordinal));
        Assert.Contains("action=\"@Model.ActionBase/delete\"", row, StringComparison.Ordinal);
        Assert.Contains("data-confirm-title=\"Delete API token\"", row, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Search name, ID, permission or cluster\" data-list-search", list, StringComparison.Ordinal);
        Assert.Contains("\"sign\" => \"Passport lifecycle\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain("Passport Lifecycle", list, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBulkButtons_ShouldWaitUnderMore()
    {
        var tab = Tab;
        var more = tab[tab.IndexOf("<details class=\"bp-card bp-console-form-card mt-4 p-3\" data-token-bulk-actions>", StringComparison.Ordinal)..];

        Assert.Contains("<summary class=\"fw-semibold\">More: tokens for every cluster at once</summary>", more, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/api/tokens/generate-per-cluster\"", more, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/api/tokens/cleanup-generated\"", more, StringComparison.Ordinal);
        Assert.Contains("data-confirm-title=\"Remove unused generated tokens\"", more, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDemoTokens_ShouldNameTheirClusterOnce()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var names = (await rig.Services.GetRequiredService<ExternalApiRepository>().ListTokensAsync()).Select(token => token["name"].AsString).ToList();

        Assert.Contains("Demo North Operations (read)", names);
        Assert.Contains("Demo North Operations (read and write live values)", names);
        Assert.Contains("Demo team (full access)", names);
        Assert.DoesNotContain(names, name => name.StartsWith("Demo Demo", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoRestartTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ApiTokensTabTests"`
Expected: `Failed:    10, Passed:    14`: five of the new `ApiTokensTabTests`, the three `DemoRestartTests` (a restart still adds six tokens, so clearing keeps the clusters), and two updated tests.

- [ ] **Step 3: Write `ApiTokenListViewModel.cs`**

`web/Models/ViewModels/ApiTokenListViewModel.cs`:

```csharp
namespace BatteryPassWeb.Models.ViewModels;

/// <summary>The token list shared by the team's and the cluster admins' API token pages (step 3e): the tokens, the address
/// their actions post to, whether a token can be switched off and on (the team's page only), and what an empty list says.</summary>
public sealed record ApiTokenListViewModel(IReadOnlyList<ApiTokenViewModel> Tokens, string ActionBase, bool CanSetActive, string EmptyText);
```

- [ ] **Step 4: Write `_ApiTokenList.cshtml`**

`web/Views/Shared/_ApiTokenList.cshtml`:

```cshtml
@using BatteryPassWeb.Models.ViewModels
@model ApiTokenListViewModel
@{
    // One row per token: its name, permission, batteries and last use. A row opens to its ID, regenerating it,
    // switching it off and on (the team's page only) and, last, deleting it.
    string DisplayName(ApiTokenViewModel token)
    {
        // Tokens made per cluster end in " - read" or " - readwrite"; the permission column says that already.
        var suffixes = token.AccessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
            ? new[] { " - readwritesign", "-readwritesign" }
            : token.AccessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
                ? new[] { " - readwrite", "-readwrite" }
                : token.AccessMode.Equals("read", StringComparison.OrdinalIgnoreCase)
                    ? new[] { " - read", "-read" }
                    : [];
        var suffix = suffixes.FirstOrDefault(candidate => token.Name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(suffix) ? token.Name : token.Name[..^suffix.Length];
    }
    string AccessLabel(ApiTokenViewModel token) => token.AccessMode.ToLowerInvariant() switch
    {
        "readwritesign" => "Full access",
        "sign" => "Passport lifecycle",
        "readwrite" => "Read + write",
        _ => "Read"
    };
    string Batteries(ApiTokenViewModel token) => token.GlobalAccess
        ? "All batteries"
        : token.ClusterScopeNames.Count == 0 ? "No clusters" : string.Join(", ", token.ClusterScopeNames);
}

<section class="bp-card bp-list-card mt-4" data-list aria-label="API tokens" style="--bp-row-columns: minmax(0, 2.2fr) minmax(0, 1fr) minmax(0, 1.4fr) minmax(0, 1fr) 96px">
    <div class="bp-list-tools">
        <h2>Tokens</h2>
        <label class="bp-list-search">
            <span class="visually-hidden">Search tokens</span>
            <input type="search" placeholder="Search name, ID, permission or cluster" data-list-search />
        </label>
    </div>
    @if (Model.Tokens.Count == 0)
    {
        <p class="bp-list-empty">@Model.EmptyText</p>
    }
    else
    {
        <div class="bp-list-head" aria-hidden="true"><span>Token</span><span>Permission</span><span>Batteries</span><span>Last used</span><span></span></div>
        <p class="bp-list-empty" data-list-no-results hidden>No tokens match the search.</p>
    }
    @foreach (var token in Model.Tokens)
    {
        var name = DisplayName(token);
        var access = AccessLabel(token);
        var searchText = $"{token.Name} {token.TokenId} {access} {Batteries(token)} {(token.IsActive ? "Active" : "Inactive")}";
        <details class="bp-row" data-token-row="@token.TokenId" data-search-text="@searchText">
            <summary>
                <span class="bp-row-title">
                    <strong>@name</strong>
                    <code>@token.TokenId</code>
                    @if (token.IsSample)
                    {
                        <small>Sample token</small>
                    }
                </span>
                <span class="bp-tags">
                    <span class="bp-row-label">Permission</span>
                    <span class="bp-soft-tag">@access</span>
                    @if (!token.IsActive)
                    {
                        <span class="bp-soft-tag is-off">Inactive</span>
                    }
                </span>
                <span>
                    <span class="bp-row-label">Batteries</span>@Batteries(token)
                    @if (token.AllowUnassigned && !token.GlobalAccess)
                    {
                        <small class="d-block text-secondary">Also batteries in no cluster</small>
                    }
                </span>
                <span class="text-secondary"><span class="bp-row-label">Last used</span>@(string.IsNullOrWhiteSpace(token.LastUsedAt) ? "Never" : token.LastUsedAt)</span>
                <span class="bp-row-open"><span class="is-open">Open</span><span class="is-close">Close</span></span>
            </summary>
            <div class="bp-row-panel bp-console-form-card">
                <section class="bp-subcard">
                    <h3>Token ID</h3>
                    <span class="bp-id-copy-row">
                        <code class="bp-id-value">@token.TokenId</code>
                        @await Html.PartialAsync("_CopyIdButton", token.TokenId)
                    </span>
                    <p class="text-secondary small mt-2 mb-0">The ID names the token in the audit log. The value a system sends is shown only when the token is made or regenerated.</p>
                </section>
                <section class="bp-subcard">
                    <h3>Regenerate</h3>
                    <p class="text-secondary small">Makes a new value and shows it once. Systems using the old value stop working until they get the new one.</p>
                    <form method="post" action="@Model.ActionBase/regenerate">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="tokenId" value="@token.TokenId" />
                        <button type="submit" class="bp-secondary-button bp-button-compact" data-confirm-action data-confirm-title="Regenerate token" data-confirm-message="Make a new value for @name? Systems using the old value stop working until they get the new one." data-confirm-submit="Regenerate">Regenerate value</button>
                    </form>
                </section>
                @if (Model.CanSetActive)
                {
                    <section class="bp-subcard">
                        <h3>Active <span class="bp-soft-tag ms-1@(token.IsActive ? "" : " is-off")">@(token.IsActive ? "Active" : "Inactive")</span></h3>
                        <p class="text-secondary small">@(token.IsActive ? "Deactivating stops the token until it is activated again." : "Activating lets systems use the token again.")</p>
                        <form method="post" action="@Model.ActionBase/set-active">
                            @Html.AntiForgeryToken()
                            <input type="hidden" name="tokenId" value="@token.TokenId" />
                            <input type="hidden" name="isActive" value="@(token.IsActive ? "false" : "true")" />
                            <button type="submit" class="bp-secondary-button bp-button-compact">@(token.IsActive ? "Deactivate" : "Activate")</button>
                        </form>
                    </section>
                }
                <div class="bp-danger-zone">
                    <p>Deleting the token stops every system that uses it. It can't be undone.</p>
                    <form method="post" action="@Model.ActionBase/delete">
                        @Html.AntiForgeryToken()
                        <input type="hidden" name="tokenId" value="@token.TokenId" />
                        <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Delete API token" data-confirm-message="Delete @name? Systems using this token will stop working." data-confirm-submit="Delete">Delete token</button>
                    </form>
                </div>
            </div>
        </details>
    }
</section>
```

- [ ] **Step 5: Rebuild the tab, add the shared list and fix the demo tokens**

Patch `3e-task4-code.patch` (`web/Dpp/Demo/DemoDataService.cs`, `web/Views/Admin/Clusters.cshtml`). Save the block as `../3e-task4-code.patch`, outside the worktree, and run `git apply ../3e-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -277,8 +277,10 @@ public sealed class DemoDataService(
             var uses = new List<string>();
             var packs = await Collection(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ClusterId", clusterId), cancellationToken: cancellationToken);
             var members = await Collection("clusterMemberships").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clusterId", clusterId), cancellationToken: cancellationToken);
+            // The cluster's own tokens, which the app's start makes for every cluster, go with it and aren't real use.
+            var ownTokens = Builders<BsonDocument>.Filter.Eq("autoClusterId", clusterId);
             var realTokens = await Collection("apiTokens").CountDocumentsAsync(
-                Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("autoClusterId", clusterId), Builders<BsonDocument>.Filter.AnyEq("clusterIds", clusterId)),
+                Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.AnyEq("clusterIds", clusterId), Builders<BsonDocument>.Filter.Not(ownTokens)),
                 cancellationToken: cancellationToken);
             if (packs > 0) uses.Add($"{packs} real batteries");
             if (members > 0) uses.Add($"{members} real accounts");
@@ -290,6 +292,7 @@ public sealed class DemoDataService(
                 continue;
             }
 
+            removed["API tokens"] += (int)(await Collection("apiTokens").DeleteManyAsync(ownTokens, cancellationToken)).DeletedCount;
             await clusters.DeleteClusterAsync(clusterId, cancellationToken);
             Count("clusters");
         }
@@ -342,7 +345,8 @@ public sealed class DemoDataService(
         return made;
     }
 
-    /// <summary>Each demo cluster's read and read-write tokens, and one team token with full access; values shown once.</summary>
+    /// <summary>Each demo cluster's read and read-write tokens, and one team token with full access; values shown once.
+    /// The cluster tokens are the cluster's own (autoClusterId), so the app's start adds no others for a demo cluster.</summary>
     private async Task<List<DemoToken>> SeedTokensAsync(string who, DemoCatalogue catalogue, CancellationToken cancellationToken)
     {
         var made = new List<DemoToken>();
@@ -350,7 +354,7 @@ public sealed class DemoDataService(
         {
             foreach (var (mode, access) in new[] { (ExternalTokenAccessMode.Read, "Read"), (ExternalTokenAccessMode.ReadWrite, "Read and write live values") })
             {
-                var (document, value) = await tokens.CreateTokenAsync($"Demo {cluster.Name} ({access.ToLowerInvariant()})", mode, [cluster.Id], false, false, who, cancellationToken: cancellationToken);
+                var (document, value) = await tokens.CreateTokenAsync($"{cluster.Name} ({access.ToLowerInvariant()})", mode, [cluster.Id], false, false, who, autoClusterId: cluster.Id, cancellationToken: cancellationToken);
                 await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), cancellationToken);
                 made.Add(new DemoToken($"{cluster.Name}", access, value));
             }
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -2,7 +2,6 @@
 @{
     ViewData["Title"] = "Administration";
     var selectedTab = Model.SelectedTab;
-    var credentialTab = Model.SelectedCredentialTab;
     var clustersById = Model.Clusters.ToDictionary(cluster => cluster.ClusterId, cluster => cluster, StringComparer.OrdinalIgnoreCase);
     var clusterCreateName = TempData["ClusterCreateName"]?.ToString() ?? string.Empty;
     var clusterCreateId = TempData["ClusterCreateId"]?.ToString() ?? string.Empty;
@@ -56,39 +55,6 @@
     };
     string ClusterName(string clusterId) => clustersById.TryGetValue(clusterId, out var cluster) ? cluster.Name : clusterId;
     string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
-    string TokenClusterPickerName(string name) => name.StartsWith("Default ", StringComparison.OrdinalIgnoreCase) ? name["Default ".Length..] : name;
-    string TokenDisplayName(BatteryPassWeb.Models.ViewModels.ApiTokenViewModel token)
-    {
-        var suffixes = token.AccessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
-            ? new[] { " - readwritesign", "-readwritesign" }
-            : token.AccessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
-                ? new[] { " - readwrite", "-readwrite" }
-                : token.AccessMode.Equals("read", StringComparison.OrdinalIgnoreCase)
-                    ? new[] { " - read", "-read" }
-                    : [];
-        var suffix = suffixes.FirstOrDefault(candidate => token.Name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase));
-        return string.IsNullOrWhiteSpace(suffix) ? token.Name : token.Name[..^suffix.Length];
-    }
-    string AccessLabel(BatteryPassWeb.Models.ViewModels.ApiTokenViewModel token)
-    {
-        return token.AccessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
-            ? "Full access"
-            : token.AccessMode.Equals("sign", StringComparison.OrdinalIgnoreCase)
-                ? "Passport Lifecycle"
-                : token.AccessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
-                    ? "Read + write"
-                    : "Read";
-    }
-    string ScopeSummary(BatteryPassWeb.Models.ViewModels.ApiTokenViewModel token)
-    {
-        if (token.GlobalAccess)
-        {
-            return "Global";
-        }
-
-        var count = token.ClusterScopeNames.Count;
-        return $"{count} {(count == 1 ? "cluster" : "clusters")}";
-    }
     string GlobalAccessValue(IReadOnlyList<string> roles)
     {
         var role = roles.FirstOrDefault() ?? string.Empty;
@@ -527,229 +493,68 @@
         var activeTokenCount = Model.ApiTokens.Count(token => token.IsActive);
         var globalTokenCount = Model.ApiTokens.Count(token => token.GlobalAccess);
 
-        <section class="bp-card mt-4 bp-credential-management bp-token-console" data-credential-management>
-            <div class="bp-token-console-header">
-                <div>
-                    <span class="bp-summary-title">External integrations</span>
-                    <h2 class="h5 mb-1">API Token Management</h2>
-                    <p class="text-secondary mb-0">Create scoped API tokens for external integrations and keep active, inactive, and global tokens easy to scan.</p>
-                </div>
-                <div class="bp-token-console-summary" aria-label="API token summary">
-                    <span><strong>@Model.ApiTokens.Count</strong> tokens</span>
-                    <span><strong>@activeTokenCount</strong> active</span>
-                    <span><strong>@globalTokenCount</strong> global</span>
-                </div>
-                <form method="post" action="/admin/api/tokens/generate-per-cluster">
-                    @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-secondary-button bp-button-compact">Generate per-cluster tokens</button>
-                </form>
-                <form method="post" action="/admin/api/tokens/cleanup-generated">
-                    @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-secondary-button bp-button-compact" data-confirm-action data-confirm-title="Clean up unused generated tokens" data-confirm-message="Delete unused duplicate tokens created by per-cluster generation? Custom tokens and used tokens are kept." data-confirm-submit="Clean up">Clean up unused generated tokens</button>
-                </form>
+        <section class="bp-card bp-console-form-card bp-tab-intro mt-4">
+            <div>
+                <h2>API tokens</h2>
+                <p>Tokens let other systems read passports and send live values. A token's value is shown once, when it is made or regenerated.</p>
+            </div>
+            <div class="bp-tab-intro-actions">
+                <span class="bp-counts">@Count(Model.ApiTokens.Count, "token", "tokens") · @activeTokenCount active · @globalTokenCount for all batteries</span>
+                <button type="button" class="bp-primary-button" data-open-dialog="create-token-dialog">Create token</button>
             </div>
+        </section>
 
-            <form method="post" action="/admin/api/tokens/create" class="bp-token-create-strip bp-console-form-card">
+        <dialog id="create-token-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide bp-console-form-card" aria-labelledby="create-token-title">
+            <form method="post" action="/admin/api/tokens/create" class="bp-dialog-form">
                 @Html.AntiForgeryToken()
-                <div class="bp-token-create-topline">
-                    <label>
-                        Label
-                        <input type="text" name="name" placeholder="e.g. North battery system" required />
-                    </label>
-                    <label>
-                        Permission
-                        <span class="bp-select-shell">
-                            <select name="accessMode">
-                                <option value="read">Read</option>
-                                <option value="readWrite">Read + write</option>
-                                <option value="readWriteSign">Full access</option>
-                                <option value="sign">Passport Lifecycle</option>
-                            </select>
+                <h2 id="create-token-title">Create a token</h2>
+                <label>
+                    Name
+                    <input type="text" name="name" placeholder="e.g. North battery system" required />
+                </label>
+                <fieldset class="bp-choice-list">
+                    <legend>What it may do</legend>
+                    <label class="bp-choice"><input type="radio" name="accessMode" value="read" checked /><strong>Read</strong><small>Reads the passports of its batteries.</small></label>
+                    <label class="bp-choice"><input type="radio" name="accessMode" value="readWrite" /><strong>Read + write</strong><small>Also sends live values. For all batteries, it also creates batteries.</small></label>
+                    <label class="bp-choice"><input type="radio" name="accessMode" value="sign" /><strong>Passport lifecycle</strong><small>Reads, and for all batteries publishes passports.</small></label>
+                    <label class="bp-choice"><input type="radio" name="accessMode" value="readWriteSign" /><strong>Full access</strong><small>All of the above.</small></label>
+                </fieldset>
+                <fieldset class="bp-choice-list">
+                    <legend>Which batteries</legend>
+                    <div class="bp-choice">
+                        <input type="radio" name="globalAccess" value="off" id="token-scope-clusters" checked />
+                        <label for="token-scope-clusters"><strong>The batteries of these clusters</strong></label>
+                        <span class="bp-tags mt-1">
+                            @foreach (var cluster in Model.Clusters)
+                            {
+                                <label class="bp-checkbox-option"><input type="checkbox" name="clusterIds" value="@cluster.ClusterId" /> <span>@cluster.Name</span></label>
+                            }
                         </span>
-                        <small class="text-secondary">Full access and Passport Lifecycle create and publish batteries only with Global access, as the team.</small>
-                    </label>
-                    <fieldset class="bp-token-scope-select">
-                        <legend>Scope options</legend>
-                        <label class="bp-checkbox-option">
-                            <input type="checkbox" name="globalAccess" />
-                            <span>Global access</span>
-                        </label>
-                    </fieldset>
-                    <button type="submit" class="bp-primary-button">Create token</button>
-                </div>
-                <fieldset class="bp-token-cluster-chip-picker">
-                    <div class="bp-token-cluster-section-header">
-                        <legend>Linked clusters</legend>
-                        <span class="bp-token-cluster-count">@Model.Clusters.Count available</span>
-                    </div>
-                    <div class="bp-token-cluster-chip-grid">
-                        @foreach (var cluster in Model.Clusters)
-                        {
-                            <label>
-                                <input type="checkbox" name="clusterIds" value="@cluster.ClusterId" />
-                                <span>@TokenClusterPickerName(cluster.Name)</span>
-                            </label>
-                        }
                     </div>
+                    <label class="bp-choice"><input type="radio" name="globalAccess" value="on" /><strong>All batteries</strong><small>Acts as the team. Only a token for all batteries can create or publish batteries.</small></label>
                 </fieldset>
+                <div class="bp-dialog-actions">
+                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
+                    <button type="submit" class="bp-primary-button">Create token</button>
+                </div>
             </form>
-        </section>
+        </dialog>
 
-        <section class="bp-card bp-table-card mt-4" data-credential-panel="api-tokens">
-            <div class="bp-token-filter-toolbar">
-                <div>
-                    <span class="bp-summary-title">Issued tokens</span>
-                    <p class="mb-0 text-secondary">Search and manage integration tokens from one dense list.</p>
-                </div>
-                <label class="bp-token-search-field">
-                    <span>Search</span>
-                    <input type="search" placeholder="Token, scope, or access" data-token-search />
-                </label>
-            </div>
-                <div class="bp-table-scroll">
-                    <table class="table align-middle bp-console-table bp-credential-table bp-token-management-table bp-mobile-card-table">
-                        <colgroup>
-                            <col class="bp-token-name-column" />
-                            <col class="bp-token-id-column" />
-                            <col class="bp-token-access-column" />
-                            <col class="bp-token-scope-column" />
-                            <col class="bp-token-status-column" />
-                            <col class="bp-token-last-used-column" />
-                            <col class="bp-token-actions-column" />
-                        </colgroup>
-                        <thead>
-                            <tr>
-                                <th>Token</th>
-                                <th>Token ID</th>
-                                <th>Access</th>
-                                <th>Scope</th>
-                                <th>Status</th>
-                                <th>Last used</th>
-                                <th>Actions</th>
-                            </tr>
-                        </thead>
-                        <tbody>
-                            @if (Model.ApiTokens.Count == 0)
-                            {
-                                <tr>
-                                    <td colspan="7" class="bp-table-empty">No API tokens have been created.</td>
-                                </tr>
-                            }
-                            else
-                            {
-                                <tr data-token-no-results hidden>
-                                    <td colspan="7" class="bp-table-empty">No tokens match the current search.</td>
-                                </tr>
-                            }
-                            @foreach (var token in Model.ApiTokens)
-                            {
-                                var accessLabel = AccessLabel(token);
-                                var tokenDisplayName = TokenDisplayName(token);
-                                var statusClass = token.IsActive ? "bp-status-pill is-active" : "bp-status-pill is-inactive";
-                                var searchText = $"{token.Name} {token.TokenId} {accessLabel} {token.ClusterIdsLabel} {(token.GlobalAccess ? "Global scope" : string.Empty)} {(token.IsActive ? "Active" : "Inactive")}";
-                                <tr data-token-row data-token-search-value="@searchText">
-                                    <td data-label="Token">
-                                        <p class="bp-credential-name">@tokenDisplayName</p>
-                                        @if (token.IsSample)
-                                        {
-                                            <p class="mb-0 small text-secondary">Sample token</p>
-                                        }
-                                    </td>
-                                    <td data-label="Token ID">
-                                        <span class="bp-token-id-cell bp-id-copy-row">
-                                            <code class="bp-id-value">@token.TokenId</code>
-                                            @await Html.PartialAsync("_CopyIdButton", token.TokenId)
-                                        </span>
-                                    </td>
-                                    <td data-label="Access"><span class="bp-access-pill">@accessLabel</span></td>
-                                    <td class="bp-token-scope-cell" data-label="Scope">
-                                        <details class="bp-token-scope-details">
-                                            <summary class="bp-token-scope-summary">@ScopeSummary(token)</summary>
-                                            <div class="bp-token-scope-list">
-                                                @if (token.GlobalAccess)
-                                                {
-                                                    <p>Global scope</p>
-                                                }
-                                                else if (token.ClusterScopeNames.Count == 0)
-                                                {
-                                                    <p>No clusters</p>
-                                                }
-                                                else
-                                                {
-                                                    <ul>
-                                                        @foreach (var clusterName in token.ClusterScopeNames)
-                                                        {
-                                                            <li>@clusterName</li>
-                                                        }
-                                                    </ul>
-                                                }
-                                                @if (token.AllowUnassigned && !token.GlobalAccess)
-                                                {
-                                                    <p class="bp-token-scope-note">Includes unassigned batteries</p>
-                                                }
-                                            </div>
-                                        </details>
-                                    </td>
-                                    <td data-label="Status"><span class="@statusClass">@(token.IsActive ? "Active" : "Inactive")</span></td>
-                                    <td data-label="Last used">@(string.IsNullOrWhiteSpace(token.LastUsedAt) ? "-" : token.LastUsedAt)</td>
-                                    <td class="bp-action-cell" data-label="Actions">
-                                        <div class="bp-icon-action-row">
-                                            <form method="post" action="/admin/api/tokens/regenerate">
-                                                @Html.AntiForgeryToken()
-                                                <input type="hidden" name="tokenId" value="@token.TokenId" />
-                                                <button type="submit" class="bp-action-button" aria-label="Regenerate token @token.Name" title="Regenerate token" data-tooltip="Regenerate">
-                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                        <path d="M21 12a9 9 0 0 1-15.4 6.4" />
-                                                        <path d="M3 12a9 9 0 0 1 15.4-6.4" />
-                                                        <path d="M7 18H3v4" />
-                                                        <path d="M17 6h4V2" />
-                                                    </svg>
-                                                </button>
-                                            </form>
-                                            <form method="post" action="/admin/api/tokens/set-active">
-                                                @Html.AntiForgeryToken()
-                                                <input type="hidden" name="tokenId" value="@token.TokenId" />
-                                                <input type="hidden" name="isActive" value="@(token.IsActive ? "false" : "true")" />
-                                                @if (token.IsActive)
-                                                {
-                                                    <button type="submit" class="bp-action-button is-danger" aria-label="Deactivate token @token.Name" title="Deactivate token" data-tooltip="Deactivate">
-                                                        <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                            <path d="M18 6 6 18" />
-                                                            <path d="m6 6 12 12" />
-                                                        </svg>
-                                                    </button>
-                                                }
-                                                else
-                                                {
-                                                    <button type="submit" class="bp-action-button" aria-label="Activate token @token.Name" title="Activate token" data-tooltip="Activate">
-                                                        <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                            <path d="M20 6 9 17l-5-5" />
-                                                        </svg>
-                                                    </button>
-                                                }
-                                            </form>
-                                            <form method="post" action="/admin/api/tokens/delete">
-                                                @Html.AntiForgeryToken()
-                                                <input type="hidden" name="tokenId" value="@token.TokenId" />
-                                                <button type="submit" class="bp-action-button is-danger" aria-label="Delete token @token.Name" title="Delete token" data-tooltip="Delete" data-confirm-action data-confirm-title="Delete API token" data-confirm-message="Delete @token.Name? Existing integrations using this token will stop working." data-confirm-submit="Delete">
-                                                    <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                        <path d="M3 6h18" />
-                                                        <path d="M8 6V4h8v2" />
-                                                        <path d="M6 6l1 15h10l1-15" />
-                                                        <path d="M10 11v6" />
-                                                        <path d="M14 11v6" />
-                                                    </svg>
-                                                </button>
-                                            </form>
-                                        </div>
-                                    </td>
-                                </tr>
-                            }
-                        </tbody>
-                    </table>
-                </div>
-        </section>
+        @await Html.PartialAsync("_ApiTokenList", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, "/admin/api/tokens", true, "No API tokens yet."))
 
+        <details class="bp-card bp-console-form-card mt-4 p-3" data-token-bulk-actions>
+            <summary class="fw-semibold">More: tokens for every cluster at once</summary>
+            <div class="d-flex flex-wrap gap-2 mt-3">
+                <form method="post" action="/admin/api/tokens/generate-per-cluster">
+                    @Html.AntiForgeryToken()
+                    <button type="submit" class="bp-secondary-button bp-button-compact">Make a read and a read + write token for every cluster</button>
+                </form>
+                <form method="post" action="/admin/api/tokens/cleanup-generated">
+                    @Html.AntiForgeryToken()
+                    <button type="submit" class="bp-secondary-button bp-button-compact" data-confirm-action data-confirm-title="Remove unused generated tokens" data-confirm-message="Delete the unused duplicate tokens made per cluster? Your own tokens and tokens in use are kept." data-confirm-submit="Remove">Remove the unused ones made this way</button>
+                </form>
+            </div>
+        </details>
     }
 
     @await Html.PartialAsync("_ConfirmModal")
@@ -757,41 +562,4 @@
 
 @section Scripts {
     <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
-    <script>
-        (function () {
-            const tokenSearch = document.querySelector('[data-token-search]');
-            const tokenRows = Array.from(document.querySelectorAll('[data-token-row]'));
-            const tokenNoResults = document.querySelector('[data-token-no-results]');
-            tokenSearch?.addEventListener('input', () => {
-                const query = tokenSearch.value.trim().toLowerCase();
-                let visibleCount = 0;
-                tokenRows.forEach((row) => {
-                    const value = (row.dataset.tokenSearchValue || '').toLowerCase();
-                    const visible = query.length === 0 || value.includes(query);
-                    row.hidden = !visible;
-                    visibleCount += visible ? 1 : 0;
-                });
-                if (tokenNoResults) {
-                    tokenNoResults.hidden = query.length === 0 || visibleCount > 0;
-                }
-            });
-
-            const credentialTabs = Array.from(document.querySelectorAll('[data-credential-tab]'));
-            const credentialPanels = Array.from(document.querySelectorAll('[data-credential-panel]'));
-            credentialTabs.forEach((tab) => {
-                tab.addEventListener('click', () => {
-                    const selected = tab.dataset.credentialTab || 'api-tokens';
-                    credentialTabs.forEach((candidate) => {
-                        const active = candidate.dataset.credentialTab === selected;
-                        candidate.classList.toggle('bp-tab-active', active);
-                        candidate.setAttribute('aria-selected', active ? 'true' : 'false');
-                    });
-                    credentialPanels.forEach((panel) => {
-                        panel.hidden = panel.dataset.credentialPanel !== selected;
-                    });
-                    history.replaceState(null, '', `/admin/clusters?tab=${selected}`);
-                });
-            });
-        })();
-    </script>
 }
```

- [ ] **Step 6: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoRestartTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ApiTokensTabTests"`
Expected: `Passed!  - Failed:     0, Passed:    24`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2097`, none failed.

- [ ] **Step 7: Commit**

```bash
git add BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs BatteryPassWeb.Tests/Demo/DemoRestartTests.cs BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs web/Models/ViewModels/ApiTokenListViewModel.cs web/Views/Shared/_ApiTokenList.cshtml web/Dpp/Demo/DemoDataService.cs web/Views/Admin/Clusters.cshtml
git commit -m "feat(dpp): the API tokens tab, one list for both token pages, demo tokens named once and kept through a restart" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 5: The cluster admins' pages

Users and API tokens get the same layout, limited to the admin's clusters, with the heading in `_ClusterAdminTabs`. Saving a name or a password sends the person's existing cluster and role, so these saves work again and nobody loses their role. A cluster admin's "delete" only switches a token off, as before, and now says so.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs`
- Replace: `web/Views/ClusterAdmin/ApiTokens.cshtml`
- Replace: `web/Views/ClusterAdmin/Users.cshtml`
- Replace: `web/Views/Shared/_ClusterAdminTabs.cshtml`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Models/ViewModels/ApiTokenListViewModel.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Shared/_ApiTokenList.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3e-task5-tests.patch` (`BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`, `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`). Save the block as `../3e-task5-tests.patch`, outside the worktree, and run `git apply ../3e-task5-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
@@ -13,7 +13,7 @@ public sealed class AdminCredentialLayoutTests
         Assert.DoesNotContain(">Battery secrets</a>", clusters);
         Assert.DoesNotContain("Battery secrets", clusters);
         Assert.Contains("_PasswordReveal", clusters);
-        Assert.Contains("data-password-reveal", clusterUsers);
+        Assert.Contains("_PasswordReveal", clusterUsers);
         Assert.Contains(".bp-tab-row", css);
         Assert.Contains("flex-wrap: nowrap", css);
     }
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -47,7 +47,7 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.Contains("bp-select-shell", clusters);
         Assert.Contains("bp-select-shell", clusterUsers);
         Assert.Contains(".bp-select-shell::after", css);
-        Assert.Contains("bp-user-management-table", clusterUsers);
+        Assert.Contains("<details class=\"bp-row\" data-open-user", clusterUsers);
         Assert.DoesNotContain("bp-console-split", clusterUsers);
     }
 
@@ -84,9 +84,9 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.Contains("_ClusterAdminTabs", users);
         Assert.Contains("_ClusterAdminTabs", tokens);
         Assert.Contains("bp-local-admin-nav", localTabs);
-        Assert.Contains("bp-user-management-table", users);
+        Assert.Contains("<details class=\"bp-row\" data-open-user", users);
         Assert.DoesNotContain("bp-console-split", users);
-        Assert.Contains("bp-console-table", tokens);
+        Assert.Contains("_ApiTokenList", tokens);
         Assert.Contains(".bp-local-admin-nav", css);
         Assert.Contains(".bp-console-split", css);
         Assert.Contains(".bp-edit-grid", css);
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -90,13 +90,11 @@ public sealed class AdminFeedbackFollowupTests
         Assert.DoesNotContain("confirm(", clusters);
         Assert.DoesNotContain("confirm(", clusterTokens);
         Assert.Contains("_ConfirmModal", clusters);
-        Assert.Contains("bp-confirm-modal", clusterTokens);
+        Assert.Contains("_ConfirmModal", clusterTokens);
         Assert.Contains("data-confirm-action", clusters);
-        Assert.Contains("data-confirm-action", clusterTokens);
-        Assert.Contains("bp-user-management-table", clusterUsers);
+        Assert.Contains("<details class=\"bp-row\" data-open-user", clusterUsers);
         Assert.DoesNotContain("class=\"bp-tag\"", clusters);
         Assert.DoesNotContain("class=\"bp-tag\"", clusterUsers);
-        Assert.Contains("bp-token-cluster-chip-picker", clusterTokens);
         Assert.Contains(".bp-confirm-modal", css);
         Assert.Contains(".bp-token-cluster-chip-picker", css);
     }
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -22,7 +22,7 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("Legitimate interest: repair, repurposing, remanufacturing", adminUsers);
         Assert.Contains("Legitimate interest: recycling and battery treatment", adminUsers);
         Assert.Contains("Legitimate interest: authorised or WEEE treatment", adminUsers);
-        Assert.Contains("Notified Body", clusterUsers);
+        Assert.Contains("Notified body", clusterUsers);
         Assert.Contains("NormalizeClusterMembershipRole", clusterRepository);
         Assert.Contains("CreateIndexOptions { Unique = true", clusterRepository);
         Assert.Contains("RoleNotifiedBody", accessControl);
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -92,11 +92,12 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.Contains("ValidateClusterTokenScopeAsync", controller);
         Assert.Contains("CanAdministerClusterAsync", controller);
         Assert.Contains("/cluster-admin/api-tokens/create", view);
-        Assert.Contains("Full access", view);
-        Assert.DoesNotContain("Read + write + Passport Lifecycle", view);
-        Assert.Contains("Passport Lifecycle", view);
+        Assert.Contains("name=\"accessMode\" value=\"read\"", view); // a cluster token reads, and with write sends live values (step 3e)
+        Assert.Contains("name=\"accessMode\" value=\"readWrite\"", view);
+        Assert.DoesNotContain("value=\"readWriteSign\"", view);
+        Assert.DoesNotContain("value=\"sign\"", view);
         Assert.DoesNotContain("Create, validate, sign and publish passport", view);
-        Assert.Contains("/cluster-admin/api-tokens/delete", view);
+        Assert.Contains("\"/cluster-admin/api-tokens\", false, false", view);
     }
 
     private static string RepoFile(params string[] parts)
diff --git a/BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs b/BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs
--- a/BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs
+++ b/BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs
@@ -49,7 +49,7 @@ public sealed class ApiTokensTabTests(MongoFixture mongo)
     [Fact]
     public void TheList_ShouldBeSharedWithTheClusterAdminsPage()
     {
-        Assert.Contains("@await Html.PartialAsync(\"_ApiTokenList\", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, \"/admin/api/tokens\", true, \"No API tokens yet.\"))", Tab, StringComparison.Ordinal);
+        Assert.Contains("@await Html.PartialAsync(\"_ApiTokenList\", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, \"/admin/api/tokens\", true, true, \"No API tokens yet.\"))", Tab, StringComparison.Ordinal);
         Assert.DoesNotContain("<table", Tab, StringComparison.Ordinal);
     }
 
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -69,7 +69,7 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains("value=\"/cluster-admin/api-tokens\"", clusterTabs);
         Assert.Contains("aria-label=\"Local admin tabs\"", clusterTabs);
         Assert.Contains("bp-tab-row", clusterTabs);
-        Assert.Contains("API token management", clusterTabs);
+        Assert.Contains(">API tokens</a>", clusterTabs);
         Assert.True(mobileFormIndex >= 0);
         Assert.True(localNavIndex > mobileFormIndex);
         Assert.Contains("@await Html.PartialAsync(\"_ClusterAdminTabs\", \"users\")", users);
```

`BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs`:

```csharp
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the cluster admins' Users and API tokens pages, on the same layout as the team's tabs and limited to
/// the clusters they run. Saving a name or a password names one of the person's memberships and keeps its role (before,
/// those two forms sent no cluster, so the save was refused without a word).</summary>
public sealed class ClusterAdminPagesTests
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static readonly string Tabs = Read("web", "Views", "Shared", "_ClusterAdminTabs.cshtml");
    private static readonly string Users = Read("web", "Views", "ClusterAdmin", "Users.cshtml");
    private static readonly string Tokens = Read("web", "Views", "ClusterAdmin", "ApiTokens.cshtml");

    [Fact]
    public void Heading_ShouldNameTheClustersAndTheTabs()
    {
        Assert.Contains("<h1>Cluster admin</h1>", Tabs, StringComparison.Ordinal);
        Assert.Contains("<p>Users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? \"the clusters you run\" : scope).</p>", Tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/users\">Users</a>", Tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/api-tokens\">API tokens</a>", Tabs, StringComparison.Ordinal);
        foreach (var page in new[] { Users, Tokens })
        {
            Assert.Contains("ViewData[\"ClusterAdminScope\"] = Model.Clusters.Count == 1 ? Model.Clusters[0].Name : null;", page, StringComparison.Ordinal);
            Assert.DoesNotContain("<h1>", page, StringComparison.Ordinal);
            Assert.Contains("@await Html.PartialAsync(\"_ConfirmModal\")", page, StringComparison.Ordinal);
            Assert.Contains("<script src=\"~/js/bp-admin-pages.js\" asp-append-version=\"true\"></script>", page, StringComparison.Ordinal);
            Assert.DoesNotContain("<table", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AddUser_ShouldPutThemStraightIntoOneOfYourClusters()
    {
        var window = Users[Users.IndexOf("<dialog id=\"add-user-dialog\"", StringComparison.Ordinal)..Users.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("action=\"/cluster-admin/users/save\"", window, StringComparison.Ordinal);
        Assert.Contains("<select name=\"clusterId\" required>", window, StringComparison.Ordinal);
        Assert.Contains("<select name=\"role\">", window, StringComparison.Ordinal);
        Assert.Contains("[\"member\"] = \"User\"", Users, StringComparison.Ordinal);
        Assert.Contains("[\"clusterAdmin\"] = \"Cluster admin\"", Users, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingANameOrAPassword_ShouldKeepTheirClusterAndRole()
    {
        var row = Users[Users.IndexOf("<details class=\"bp-row\" data-open-user=\"@user.Email\"", StringComparison.Ordinal)..Users.IndexOf("</details>", StringComparison.Ordinal)];
        var keepFields = "<input type=\"hidden\" name=\"clusterId\" value=\"@keep?.ClusterId\" />";

        Assert.Contains("var keep = memberships.FirstOrDefault();", Users, StringComparison.Ordinal);
        Assert.Equal(2, row.Split(keepFields).Length - 1);
        Assert.Equal(2, row.Split("<input type=\"hidden\" name=\"role\" value=\"@keep?.Role\" />").Length - 1);
        Assert.Contains("action=\"/cluster-admin/users/delete-membership\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("delete-account", Users, StringComparison.Ordinal);
        Assert.Contains("only the team changes their account", row, StringComparison.Ordinal);
    }

    [Fact]
    public void Tokens_ShouldOfferOnlyWhatAClusterTokenDoes()
    {
        var window = Tokens[Tokens.IndexOf("<dialog id=\"create-token-dialog\"", StringComparison.Ordinal)..Tokens.IndexOf("</dialog>", StringComparison.Ordinal)];

        Assert.Contains("action=\"/cluster-admin/api-tokens/create\"", window, StringComparison.Ordinal);
        Assert.Contains("<strong>Read</strong><small>Reads the passports of your cluster's batteries.</small>", window, StringComparison.Ordinal);
        Assert.Contains("<strong>Read + write</strong><small>Also sends their live values.</small>", window, StringComparison.Ordinal);
        Assert.DoesNotContain("globalAccess", window, StringComparison.Ordinal);
        Assert.Contains("checked=\"@(Model.Clusters.Count == 1)\"", window, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_ApiTokenList\", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, \"/cluster-admin/api-tokens\", false, false, \"No API tokens for your clusters yet.\"))", Tokens, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovingATokenHere_ShouldSayItOnlySwitchesItOff()
    {
        var list = Read("web", "Views", "Shared", "_ApiTokenList.cshtml");
        var controller = Read("web", "Controllers", "ClusterAdminController.cs");

        Assert.Contains("@if (Model.CanDelete)", list, StringComparison.Ordinal);
        Assert.Contains(">Deactivate token</button>", list, StringComparison.Ordinal);
        Assert.Contains("Only the team can switch it on again or delete it.", list, StringComparison.Ordinal);
        Assert.Contains("TempData[\"StatusMessage\"] = \"API token deactivated.\";", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("TempData[\"StatusMessage\"] = \"API token deleted.\";", controller, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ApiTokensTabTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ClusterAdminPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Failed:    13, Passed:    35`: the five new `ClusterAdminPagesTests`, `ApiTokensTabTests`' check of the shared list's new `CanDelete` argument, and seven updated tests of the cluster admin pages.

- [ ] **Step 3: Replace `ApiTokens.cshtml`**

`web/Views/ClusterAdmin/ApiTokens.cshtml`:

```cshtml
@model BatteryPassWeb.Models.ViewModels.AdminClusterViewModel
@{
    ViewData["Title"] = "Cluster API tokens";
    ViewData["ClusterAdminScope"] = Model.Clusters.Count == 1 ? Model.Clusters[0].Name : null;
    var activeTokenCount = Model.ApiTokens.Count(token => token.IsActive);
    string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    @await Html.PartialAsync("_ClusterAdminTabs", "api-tokens")

    @if (!string.IsNullOrWhiteSpace(Model.StatusMessage))
    {
        <div class="alert alert-success mt-3 mb-0">@Model.StatusMessage</div>
    }
    @if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
    {
        <div class="alert alert-danger mt-3 mb-0">@Model.ErrorMessage</div>
    }
    @* data-generated-token-modal data-generated-token-copy data-generated-token-ok navigator.clipboard.writeText *@
    @await Html.PartialAsync("_GeneratedCredentialModal", Model.GeneratedCredential)

    <section class="bp-card bp-console-form-card bp-tab-intro mt-4">
        <div>
            <h2>API tokens</h2>
            <p>Tokens let your systems read your batteries' passports and send their live values. A token's value is shown once, when it is made or regenerated.</p>
        </div>
        <div class="bp-tab-intro-actions">
            <span class="bp-counts">@Count(Model.ApiTokens.Count, "token", "tokens") · @activeTokenCount active</span>
            <button type="button" class="bp-primary-button" data-open-dialog="create-token-dialog">Create token</button>
        </div>
    </section>

    <dialog id="create-token-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide bp-console-form-card" aria-labelledby="create-token-title">
        <form method="post" action="/cluster-admin/api-tokens/create" class="bp-dialog-form">
            @Html.AntiForgeryToken()
            <h2 id="create-token-title">Create a token</h2>
            <label>
                Name
                <input type="text" name="name" placeholder="e.g. North battery system" required />
            </label>
            <fieldset class="bp-choice-list">
                <legend>What it may do</legend>
                <label class="bp-choice"><input type="radio" name="accessMode" value="read" checked /><strong>Read</strong><small>Reads the passports of your cluster's batteries.</small></label>
                <label class="bp-choice"><input type="radio" name="accessMode" value="readWrite" /><strong>Read + write</strong><small>Also sends their live values.</small></label>
            </fieldset>
            <fieldset class="bp-choice-list">
                <legend>Which clusters</legend>
                <span class="bp-tags">
                    @foreach (var cluster in Model.Clusters)
                    {
                        <label class="bp-checkbox-option"><input type="checkbox" name="clusterIds" value="@cluster.ClusterId" checked="@(Model.Clusters.Count == 1)" /> <span>@cluster.Name</span></label>
                    }
                </span>
            </fieldset>
            <div class="bp-dialog-actions">
                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                <button type="submit" class="bp-primary-button">Create token</button>
            </div>
        </form>
    </dialog>

    @await Html.PartialAsync("_ApiTokenList", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, "/cluster-admin/api-tokens", false, false, "No API tokens for your clusters yet."))

    @await Html.PartialAsync("_ConfirmModal")
</main>

@section Scripts {
    <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
}
```

- [ ] **Step 4: Replace `Users.cshtml`**

`web/Views/ClusterAdmin/Users.cshtml`:

```cshtml
@model BatteryPassWeb.Models.ViewModels.AdminClusterViewModel
@{
    ViewData["Title"] = "Cluster users";
    ViewData["ClusterAdminScope"] = Model.Clusters.Count == 1 ? Model.Clusters[0].Name : null;
    var errorMessage = ViewData["ErrorMessage"] as string ?? string.Empty;
    var clustersById = Model.Clusters.ToDictionary(cluster => cluster.ClusterId, cluster => cluster, StringComparer.OrdinalIgnoreCase);
    var clusterMembershipRoleOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["member"] = "User",
        ["clusterAdmin"] = "Cluster admin"
    };
    string MembershipRoleLabel(string role) => role switch
    {
        "notifiedBody" => "Notified body",
        "marketSurveillanceAuthority" => "Market surveillance authority",
        "commission" => "Commission",
        "liRepair" => "Legitimate interest: repair, repurposing, remanufacturing",
        "liRecycling" => "Legitimate interest: recycling and battery treatment",
        "liTreatment" => "Legitimate interest: authorised or WEEE treatment",
        _ => clusterMembershipRoleOptions.TryGetValue(role, out var label) ? label : "User"
    };
    string ClusterName(string clusterId) => clustersById.TryGetValue(clusterId, out var cluster) ? cluster.Name : clusterId;
    string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    @await Html.PartialAsync("_ClusterAdminTabs", "users")

    @if (!string.IsNullOrWhiteSpace(errorMessage))
    {
        <div class="alert alert-danger mt-3 mb-0">@errorMessage</div>
    }

    <section class="bp-card bp-console-form-card bp-tab-intro mt-4">
        <div>
            <h2>Users</h2>
            <p>The people in your clusters. Users see your cluster's batteries; cluster admins also look after its users and tokens.</p>
        </div>
        <div class="bp-tab-intro-actions">
            <span class="bp-counts">@Count(Model.Users.Count, "user", "users")</span>
            <button type="button" class="bp-primary-button" data-open-dialog="add-user-dialog">Add user</button>
        </div>
    </section>

    <dialog id="add-user-dialog" class="bp-modal-panel bp-dialog bp-dialog-wide bp-console-form-card" aria-labelledby="add-user-title">
        <form method="post" action="/cluster-admin/users/save" class="bp-dialog-form">
            @Html.AntiForgeryToken()
            <div>
                <h2 id="add-user-title">Add a user</h2>
                <p class="bp-help-text mb-0">They join one of your clusters straight away. Typing the email of an existing user adds them to it.</p>
            </div>
            <div class="bp-field-pairs">
                <label>
                    Name
                    <input type="text" name="name" placeholder="Display name" />
                </label>
                <label>
                    Email
                    <input type="email" name="email" placeholder="user@example.test" required />
                </label>
                <label>
                    Password
                    <span class="bp-password-field">
                        <input type="password" name="password" placeholder="Password (new users)" autocomplete="new-password" />
                        @await Html.PartialAsync("_PasswordReveal")
                    </span>
                </label>
                <label>
                    Type it again
                    <span class="bp-password-field">
                        <input type="password" name="passwordConfirmation" placeholder="Type it again" autocomplete="new-password" />
                        @await Html.PartialAsync("_PasswordReveal")
                    </span>
                </label>
                <label>
                    Cluster
                    <span class="bp-select-shell">
                        <select name="clusterId" required>
                            @foreach (var cluster in Model.Clusters)
                            {
                                <option value="@cluster.ClusterId">@cluster.Name</option>
                            }
                        </select>
                    </span>
                </label>
                <label>
                    As
                    <span class="bp-select-shell">
                        <select name="role">
                            @foreach (var option in clusterMembershipRoleOptions)
                            {
                                <option value="@option.Key">@option.Value</option>
                            }
                        </select>
                    </span>
                </label>
            </div>
            <div class="bp-dialog-actions">
                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                <button type="submit" class="bp-primary-button">Add user</button>
            </div>
        </form>
    </dialog>

    <section class="bp-card bp-list-card mt-4" data-list aria-label="Users">
        <div class="bp-list-tools">
            <h2>Your users</h2>
            <label class="bp-list-search">
                <span class="visually-hidden">Search users</span>
                <input type="search" placeholder="Search name, email or cluster" data-list-search />
            </label>
        </div>
        @if (Model.Users.Count == 0)
        {
            <p class="bp-list-empty">No users in your clusters yet.</p>
        }
        else
        {
            <div class="bp-list-head" aria-hidden="true"><span>User</span><span>Role in your clusters</span><span>Clusters</span><span></span></div>
            <p class="bp-list-empty" data-list-no-results hidden>No users match the search.</p>
        }
        @foreach (var user in Model.Users.OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase))
        {
            var memberships = Model.Memberships
                .Where(membership => membership.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
                .OrderBy(membership => membership.ClusterId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var displayName = string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name;
            var isGlobalAdmin = user.Roles.Contains("Global Admin", StringComparer.OrdinalIgnoreCase);
            // A save here always names one of the user's memberships in your clusters, and keeps its role.
            var keep = memberships.FirstOrDefault();
            var searchText = $"{displayName} {user.Email} {string.Join(" ", memberships.Select(membership => ClusterName(membership.ClusterId)))}";
            <details class="bp-row" data-open-user="@user.Email" data-search-text="@searchText">
                <summary>
                    <span class="bp-row-title"><strong>@displayName</strong><small>@user.Email</small></span>
                    <span class="bp-tags">
                        <span class="bp-row-label">Role</span>
                        @foreach (var role in memberships.Select(membership => membership.Role).Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            <span class="bp-soft-tag">@MembershipRoleLabel(role)</span>
                        }
                        @if (isGlobalAdmin)
                        {
                            <span class="bp-soft-tag">Global admin</span>
                        }
                    </span>
                    <span><span class="bp-row-label">Clusters</span>@string.Join(", ", memberships.Select(membership => ClusterName(membership.ClusterId)))</span>
                    <span class="bp-row-open"><span class="is-open">Open</span><span class="is-close">Close</span></span>
                </summary>
                <div class="bp-row-panel bp-console-form-card">
                    @if (isGlobalAdmin)
                    {
                        <p class="text-secondary mb-0">This is one of the team's global admins: only the team changes their account.</p>
                    }
                    else
                    {
                        <section class="bp-subcard">
                            <h3>Name</h3>
                            <form method="post" action="/cluster-admin/users/save">
                                @Html.AntiForgeryToken()
                                <input type="hidden" name="email" value="@user.Email" />
                                <input type="hidden" name="clusterId" value="@keep?.ClusterId" />
                                <input type="hidden" name="role" value="@keep?.Role" />
                                <label>
                                    Name
                                    <input type="text" name="name" value="@displayName" />
                                </label>
                                <div><button type="submit" class="bp-primary-button bp-button-compact">Save</button></div>
                            </form>
                        </section>
                        <section class="bp-subcard">
                            <h3>Clusters</h3>
                            <ul class="bp-member-list">
                                @foreach (var membership in memberships)
                                {
                                    <li>
                                        <span>@ClusterName(membership.ClusterId)</span>
                                        <form method="post" action="/cluster-admin/users/delete-membership" class="d-flex align-items-center gap-2">
                                            @Html.AntiForgeryToken()
                                            <input type="hidden" name="email" value="@membership.Email" />
                                            <input type="hidden" name="clusterId" value="@membership.ClusterId" />
                                            <span class="bp-soft-tag">@MembershipRoleLabel(membership.Role)</span>
                                            <button type="submit" class="bp-icon-button is-danger" aria-label="Remove from @ClusterName(membership.ClusterId)" title="Remove from this cluster">
                                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
                                                    <path d="M18 6 6 18" />
                                                    <path d="m6 6 12 12" />
                                                </svg>
                                            </button>
                                        </form>
                                    </li>
                                }
                            </ul>
                            <form method="post" action="/cluster-admin/users/save" class="bp-add-membership">
                                @Html.AntiForgeryToken()
                                <input type="hidden" name="email" value="@user.Email" />
                                <input type="hidden" name="name" value="@displayName" />
                                <label>
                                    Add to or change
                                    <span class="bp-select-shell">
                                        <select name="clusterId">
                                            @foreach (var cluster in Model.Clusters)
                                            {
                                                <option value="@cluster.ClusterId">@cluster.Name</option>
                                            }
                                        </select>
                                    </span>
                                </label>
                                <label>
                                    As
                                    <span class="bp-select-shell">
                                        <select name="role">
                                            @foreach (var option in clusterMembershipRoleOptions)
                                            {
                                                <option value="@option.Key">@option.Value</option>
                                            }
                                        </select>
                                    </span>
                                </label>
                                <button type="submit" class="bp-secondary-button">Save</button>
                            </form>
                        </section>
                        <section class="bp-subcard">
                            <h3>New password</h3>
                            <form method="post" action="/cluster-admin/users/save">
                                @Html.AntiForgeryToken()
                                <input type="hidden" name="email" value="@user.Email" />
                                <input type="hidden" name="name" value="@displayName" />
                                <input type="hidden" name="clusterId" value="@keep?.ClusterId" />
                                <input type="hidden" name="role" value="@keep?.Role" />
                                <label>
                                    New password
                                    <span class="bp-password-field">
                                        <input type="password" name="password" placeholder="New password" autocomplete="new-password" />
                                        @await Html.PartialAsync("_PasswordReveal")
                                    </span>
                                </label>
                                <label>
                                    Type it again
                                    <span class="bp-password-field">
                                        <input type="password" name="passwordConfirmation" placeholder="Type it again" autocomplete="new-password" />
                                        @await Html.PartialAsync("_PasswordReveal")
                                    </span>
                                </label>
                                <div><button type="submit" class="bp-secondary-button bp-button-compact">Save password</button></div>
                            </form>
                        </section>
                    }
                </div>
            </details>
        }
    </section>

    @await Html.PartialAsync("_ConfirmModal")
</main>

@section Scripts {
    <script src="~/js/bp-admin-pages.js" asp-append-version="true"></script>
}
```

- [ ] **Step 5: Replace `_ClusterAdminTabs.cshtml`**

`web/Views/Shared/_ClusterAdminTabs.cshtml`:

```cshtml
@model string
@{
    // The cluster admin pages' heading and tabs. The page names the clusters it is about in ViewData["ClusterAdminScope"].
    var activeTab = Model ?? string.Empty;
    string TabClass(string tab) => activeTab.Equals(tab, StringComparison.OrdinalIgnoreCase) ? "bp-tab bp-tab-active" : "bp-tab";
    bool IsSelected(string tab) => activeTab.Equals(tab, StringComparison.OrdinalIgnoreCase);
    var scope = ViewData["ClusterAdminScope"] as string;
}

<div class="bp-admin-heading">
    <h1>Cluster admin</h1>
    <p>Users and API tokens for @(string.IsNullOrWhiteSpace(scope) ? "the clusters you run" : scope).</p>
</div>

<form class="bp-admin-mobile-tab-form mt-4" aria-label="Cluster administration section">
    <label for="bp-cluster-admin-mobile-tab-select">Cluster admin section</label>
    <select id="bp-cluster-admin-mobile-tab-select" data-admin-mobile-tab-select>
        <option value="/cluster-admin/users" selected="@IsSelected("users")">Users</option>
        <option value="/cluster-admin/api-tokens" selected="@IsSelected("api-tokens")">API tokens</option>
    </select>
</form>

<nav class="bp-local-admin-nav" aria-label="Local admin tabs">
    <div class="bp-tab-row">
        <a class="@TabClass("users")" href="/cluster-admin/users">Users</a>
        <a class="@TabClass("api-tokens")" href="/cluster-admin/api-tokens">API tokens</a>
    </div>
</nav>
```

- [ ] **Step 6: Rebuild both pages on the shared pieces**

Patch `3e-task5-code.patch` (`web/Controllers/ClusterAdminController.cs`, `web/Models/ViewModels/ApiTokenListViewModel.cs`, `web/Views/Admin/Clusters.cshtml`, `web/Views/Shared/_ApiTokenList.cshtml`). Save the block as `../3e-task5-code.patch`, outside the worktree, and run `git apply ../3e-task5-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/ClusterAdminController.cs b/web/Controllers/ClusterAdminController.cs
--- a/web/Controllers/ClusterAdminController.cs
+++ b/web/Controllers/ClusterAdminController.cs
@@ -205,7 +205,7 @@ public class ClusterAdminController : Controller
             "apiToken",
             tokenId,
             cancellationToken);
-        TempData["StatusMessage"] = "API token deleted.";
+        TempData["StatusMessage"] = "API token deactivated.";
         return Redirect("/cluster-admin/api-tokens");
     }
 
diff --git a/web/Models/ViewModels/ApiTokenListViewModel.cs b/web/Models/ViewModels/ApiTokenListViewModel.cs
--- a/web/Models/ViewModels/ApiTokenListViewModel.cs
+++ b/web/Models/ViewModels/ApiTokenListViewModel.cs
@@ -1,5 +1,6 @@
 namespace BatteryPassWeb.Models.ViewModels;
 
 /// <summary>The token list shared by the team's and the cluster admins' API token pages (step 3e): the tokens, the address
-/// their actions post to, whether a token can be switched off and on (the team's page only), and what an empty list says.</summary>
-public sealed record ApiTokenListViewModel(IReadOnlyList<ApiTokenViewModel> Tokens, string ActionBase, bool CanSetActive, string EmptyText);
+/// their actions post to, whether a token can be switched off and on and deleted (the team's page only: a cluster admin's
+/// "delete" only switches a token off), and what an empty list says.</summary>
+public sealed record ApiTokenListViewModel(IReadOnlyList<ApiTokenViewModel> Tokens, string ActionBase, bool CanSetActive, bool CanDelete, string EmptyText);
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -540,7 +540,7 @@
             </form>
         </dialog>
 
-        @await Html.PartialAsync("_ApiTokenList", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, "/admin/api/tokens", true, "No API tokens yet."))
+        @await Html.PartialAsync("_ApiTokenList", new BatteryPassWeb.Models.ViewModels.ApiTokenListViewModel(Model.ApiTokens, "/admin/api/tokens", true, true, "No API tokens yet."))
 
         <details class="bp-card bp-console-form-card mt-4 p-3" data-token-bulk-actions>
             <summary class="fw-semibold">More: tokens for every cluster at once</summary>
diff --git a/web/Views/Shared/_ApiTokenList.cshtml b/web/Views/Shared/_ApiTokenList.cshtml
--- a/web/Views/Shared/_ApiTokenList.cshtml
+++ b/web/Views/Shared/_ApiTokenList.cshtml
@@ -2,7 +2,7 @@
 @model ApiTokenListViewModel
 @{
     // One row per token: its name, permission, batteries and last use. A row opens to its ID, regenerating it,
-    // switching it off and on (the team's page only) and, last, deleting it.
+    // switching it off and on (the team's page only) and, last, deleting it (on a cluster admin's page: switching it off).
     string DisplayName(ApiTokenViewModel token)
     {
         // Tokens made per cluster end in " - read" or " - readwrite"; the permission column says that already.
@@ -110,12 +110,28 @@
                     </section>
                 }
                 <div class="bp-danger-zone">
-                    <p>Deleting the token stops every system that uses it. It can't be undone.</p>
-                    <form method="post" action="@Model.ActionBase/delete">
-                        @Html.AntiForgeryToken()
-                        <input type="hidden" name="tokenId" value="@token.TokenId" />
-                        <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Delete API token" data-confirm-message="Delete @name? Systems using this token will stop working." data-confirm-submit="Delete">Delete token</button>
-                    </form>
+                    @if (Model.CanDelete)
+                    {
+                        <p>Deleting the token stops every system that uses it. It can't be undone.</p>
+                        <form method="post" action="@Model.ActionBase/delete">
+                            @Html.AntiForgeryToken()
+                            <input type="hidden" name="tokenId" value="@token.TokenId" />
+                            <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Delete API token" data-confirm-message="Delete @name? Systems using this token will stop working." data-confirm-submit="Delete">Delete token</button>
+                        </form>
+                    }
+                    else if (token.IsActive)
+                    {
+                        <p>Deactivating the token stops every system that uses it. Only the team can switch it on again or delete it.</p>
+                        <form method="post" action="@Model.ActionBase/delete">
+                            @Html.AntiForgeryToken()
+                            <input type="hidden" name="tokenId" value="@token.TokenId" />
+                            <button type="submit" class="bp-danger-button bp-button-compact" data-confirm-action data-confirm-title="Deactivate API token" data-confirm-message="Deactivate @name? Systems using it will stop working, and only the team can switch it on again." data-confirm-submit="Deactivate">Deactivate token</button>
+                        </form>
+                    }
+                    else
+                    {
+                        <p>This token is switched off. Only the team can switch it on again or delete it.</p>
+                    }
                 </div>
             </div>
         </details>
```

- [ ] **Step 7: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ApiTokensTabTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.ClusterAdminPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:    48`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2102`, none failed.

- [ ] **Step 8: Commit**

```bash
git add BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs BatteryPassWeb.Tests/Look/ApiTokensTabTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Look/ClusterAdminPagesTests.cs web/Views/ClusterAdmin/ApiTokens.cshtml web/Views/ClusterAdmin/Users.cshtml web/Views/Shared/_ClusterAdminTabs.cshtml web/Controllers/ClusterAdminController.cs web/Models/ViewModels/ApiTokenListViewModel.cs web/Views/Admin/Clusters.cshtml web/Views/Shared/_ApiTokenList.cshtml
git commit -m "feat(dpp): the cluster admins' Users and API tokens on the same layout, their saves keeping cluster and role" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 6: One Help place

`/help` shows each reader only their parts, through the `_HelpContents` list: everyone, signed-in readers, cluster admins ("Your cluster", new) and the team (the workflow in four steps, the status words, the ready checklist). The API reference keeps its page inside the same Help. `/help/team` leads to the team's part, the old team page and the Help tabs go, the API examples name `demo-north`, and the example battery is a published demo battery or none.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
- Test: `BatteryPassWeb.Tests/Look/HelpTests.cs`
- Test (delete): `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Replace: `web/Views/Help/Guide.cshtml`
- Create: `web/Views/Shared/_HelpContents.cshtml`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Views/Help/Index.cshtml`
- Delete: `web/Views/Admin/Help.cshtml`
- Delete: `web/Views/Shared/_HelpTabs.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3e-task6-tests.patch` (`BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`, `BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs`, `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`, `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`). Save the block as `../3e-task6-tests.patch`, outside the worktree, and run `git apply ../3e-task6-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -92,37 +92,6 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.Contains(".bp-edit-grid", css);
     }
 
-    [Fact]
-    public void AdminHelp_ShouldUsePersistentAdminTabsAndLighterWorkflowIntro()
-    {
-        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
-        var tabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("_AdminTabs", help);
-        Assert.Contains("bp-tab-row", tabs);
-        Assert.Contains("Administration tabs", tabs);
-        Assert.Contains("bp-tab-active", tabs);
-        Assert.Contains("href=\"/admin/batteries\"", tabs);
-        Assert.Contains("href=\"/admin/clusters?tab=api-token-management\"", tabs);
-        Assert.Contains("bp-admin-help-reference-console", help);
-        Assert.Contains("bp-admin-help-reference-toolbar", help);
-        Assert.Contains("bp-admin-help-workflow-table", help);
-        Assert.Contains("bp-admin-help-detail-row", help);
-        Assert.Contains("bp-admin-help-detail-panel", help);
-        Assert.Contains("data-admin-help-detail-target", help);
-        Assert.Contains("parentNode.insertBefore", help);
-        Assert.DoesNotContain("scrollIntoView", help);
-        Assert.DoesNotContain("bp-admin-help-hero-panel", help);
-        Assert.DoesNotContain("bp-admin-help-workflow-card", help);
-        Assert.DoesNotContain("bp-admin-help-workflow-chip-row", help);
-        Assert.Contains(".bp-admin-help-reference-console", css);
-        Assert.Contains(".bp-admin-help-reference-toolbar", css);
-        Assert.Contains(".bp-admin-help-workflow-table", css);
-        Assert.Contains(".bp-admin-help-detail-row", css);
-        Assert.Contains(".bp-admin-help-detail-panel", css);
-    }
-
     [Fact]
     public void AdminForms_ShouldUseModernConsoleFieldControls()
     {
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -51,7 +51,6 @@ public sealed class ApplicationAuditCoverageTests
     public void AdminWorkspace_ShouldLinkToApplicationAuditLog()
     {
         var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
         var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
         var adminTabsPath = RepoFileOrEmpty("web", "Views", "Shared", "_AdminTabs.cshtml");
 
@@ -64,7 +63,6 @@ public sealed class ApplicationAuditCoverageTests
         Assert.Contains(">Audit log<", tabs);
         Assert.Contains("bp-tab-active", tabs);
         Assert.Contains("_AdminTabs", clusters);
-        Assert.Contains("_AdminTabs", help);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
--- a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
+++ b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
@@ -15,7 +15,7 @@ public sealed class DataRequirementsAdminLayoutTests
     [Fact]
     public void ProductTemplateEditor_ShouldRemainRequirementSourceOfTruth()
     {
-        Assert.Contains("Battery families tab", File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml")));
+        Assert.Contains("<a href=\"/admin/families\">Set up product data</a>", File.ReadAllText(RepoFile("web", "Views", "Help", "Guide.cshtml")));
     }
 
     private static string RepoFile(params string[] parts)
diff --git a/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs b/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
--- a/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs
@@ -37,20 +37,18 @@ public sealed class ReviewFixesTests
     }
 
     [Fact]
-    public void Help_ShouldBeOneAreaWithAGuideATeamTabAndTheApi()
+    public void Help_ShouldBeOnePlaceWithTheTeamsPartsAndTheApi()
     {
-        var tabs = Read("web", "Views", "Shared", "_HelpTabs.cshtml");
+        var contents = Read("web", "Views", "Shared", "_HelpContents.cshtml");
         var help = Read("web", "Controllers", "HelpController.cs");
 
-        Assert.Contains("href=\"/help\">Guide</a>", tabs);
-        Assert.Contains("href=\"/help/team\">Team</a>", tabs);
-        Assert.Contains("href=\"/help/api\">API</a>", tabs);
-        Assert.Contains("User.IsInRole(AccessControlService.RoleAdmin)", tabs); // the team tab only for the team
+        Assert.Contains("<a href=\"/help/api\"", contents);
+        Assert.Contains("User.IsInRole(AccessControlService.RoleAdmin)", contents); // the team's parts only for the team (step 3e)
         Assert.Contains("[HttpGet(\"api\")]", help);
         Assert.Contains("public IActionResult Guide() => View(\"Guide\");", help);
-        foreach (var page in new[] { Read("web", "Views", "Help", "Guide.cshtml"), Read("web", "Views", "Help", "Index.cshtml"), Read("web", "Views", "Admin", "Help.cshtml") })
+        foreach (var page in new[] { Read("web", "Views", "Help", "Guide.cshtml"), Read("web", "Views", "Help", "Index.cshtml") })
         {
-            Assert.Contains("_HelpTabs", page);
+            Assert.Contains("_HelpContents", page);
         }
 
         Assert.Contains("href=\"/help/api\">Developer help</a>", Read("web", "Views", "Shared", "_Layout.cshtml"));
diff --git a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
--- a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
+++ b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
@@ -22,7 +22,7 @@ public sealed class HelpWorkbenchLayoutTests
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("bp-api-help-page", markup);
-        Assert.Contains("bp-console-header", markup);
+        Assert.Contains("_HelpContents", markup);
         Assert.Contains("bp-console-toolbar", markup);
         Assert.Contains("bp-api-help-meta-strip", markup);
         Assert.Contains("bp-api-help-section-card", markup);
@@ -138,11 +138,11 @@ public sealed class HelpWorkbenchLayoutTests
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
 
         Assert.Contains("Example battery ID", markup);
-        Assert.Contains("No battery passport is published yet.", markup);
+        Assert.Contains("No published demo battery to show.", markup);
         Assert.Contains("id=\"tester-battery-id\" class=\"bp-id-value\" value=\"@Model.ExampleBatteryId\"", markup);
         Assert.DoesNotContain("SampleBatteryId", markup);
         Assert.DoesNotContain("SampleBatteryId", controller);
-        Assert.Contains("Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier", controller);
+        Assert.Contains("ExampleBatteryIdAsync(cancellationToken)", controller);
         Assert.Contains("BatteryPassWeb.Dpp.Api.PassportApi.Sections", markup);
         Assert.DoesNotContain("<code>materialComposition</code>", markup);
     }
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -30,7 +30,6 @@ public sealed class MobileThemeSessionLayoutTests
         var layout = File.ReadAllText(RepoFile("web", "Views", "Shared", "_Layout.cshtml"));
         var adminTabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
         var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var adminHelp = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
         var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
 
         Assert.Contains("bp-header-mobile-menu", layout);
@@ -38,8 +37,6 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains("bp-admin-mobile-tab-form", adminTabs);
         Assert.Contains("data-admin-mobile-tab-select", adminTabs);
         Assert.Contains("<details class=\"bp-row\" data-cluster-row", clusters);
-        Assert.Contains("bp-admin-help-workflow-table bp-mobile-card-table", adminHelp);
-        Assert.Contains("bp-admin-help-status-table bp-mobile-card-table", adminHelp);
         Assert.Contains("bp-api-help-endpoint-table bp-mobile-card-table", apiHelp);
         Assert.Contains("data-label=\"Endpoint\"", apiHelp);
         Assert.Contains(".bp-mobile-card-table td[data-label=\"Battery ID\"]", css);
diff --git a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
--- a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
+++ b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
@@ -38,7 +38,7 @@ public sealed class Phase6ADocumentationTests
     public void Documentation_ShouldExplainPhase6BEvidenceWorkflow()
     {
         var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
-        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
+        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Guide.cshtml"));
 
         Assert.Contains("Phase 6B", guide);
         Assert.Contains("Evidence readiness", guide);
```

`BatteryPassWeb.Tests/Look/HelpTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Demo;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Look;

/// <summary>Step 3e: the Help is one place. Its contents list shows only the reader's parts, the team's workflow is a part
/// of it rather than a page under the admin tabs, the API reference keeps its page inside it, and the API examples use the
/// demo clusters and a demo battery, never a real one.</summary>
[Collection(MongoCollection.Name)]
public sealed class HelpTests(MongoFixture mongo)
{
    private static string Read(params string[] parts) => File.ReadAllText(DppTestFiles.RepoFile(parts));

    private static readonly string Contents = Read("web", "Views", "Shared", "_HelpContents.cshtml");
    private static readonly string Guide = Read("web", "Views", "Help", "Guide.cshtml");
    private static readonly string Api = Read("web", "Views", "Help", "Index.cshtml");

    [Fact]
    public void TheContents_ShouldShowOnlyTheReadersParts()
    {
        Assert.Contains("var isTeam = User.IsInRole(AccessControlService.RoleAdmin);", Contents, StringComparison.Ordinal);
        Assert.Contains("var runsACluster = isTeam || User.IsInRole(AccessControlService.RoleClusterAdmin);", Contents, StringComparison.Ordinal);
        Assert.True(Contents.IndexOf("@if (runsACluster)", StringComparison.Ordinal) < Contents.IndexOf("Part(\"cluster\")", StringComparison.Ordinal));
        Assert.True(Contents.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Contents.IndexOf("Part(\"workflow\")", StringComparison.Ordinal));
        Assert.Contains("@if (signedIn && !runsACluster)", Contents, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/help/api\" aria-current=\"@(onGuide ? null : \"page\")\">API reference and workbench</a>", Contents, StringComparison.Ordinal);

        // The page itself shows the same parts under the same conditions.
        Assert.True(Guide.IndexOf("@if (runsACluster)", StringComparison.Ordinal) < Guide.IndexOf("id=\"cluster\"", StringComparison.Ordinal));
        Assert.True(Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal) < Guide.IndexOf("id=\"workflow\"", StringComparison.Ordinal));
        Assert.True(Guide.IndexOf("@if (signedIn && !runsACluster)", StringComparison.Ordinal) < Guide.IndexOf("id=\"access\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Help_ShouldNoLongerSitUnderTheAdminTabs()
    {
        foreach (var page in new[] { Guide, Api })
        {
            Assert.Contains("@await Html.PartialAsync(\"_HelpContents\"", page, StringComparison.Ordinal);
            Assert.Contains("<div class=\"bp-help-layout mt-4\">", page, StringComparison.Ordinal);
            Assert.Contains("<h1>Help</h1>", page, StringComparison.Ordinal);
            Assert.DoesNotContain("_AdminTabs", page, StringComparison.Ordinal);
            Assert.DoesNotContain("_HelpTabs", page, StringComparison.Ordinal);
        }

        Assert.Throws<FileNotFoundException>(() => DppTestFiles.RepoFile("web", "Views", "Admin", "Help.cshtml"));
        Assert.Throws<FileNotFoundException>(() => DppTestFiles.RepoFile("web", "Views", "Shared", "_HelpTabs.cshtml"));
        Assert.Contains("public IActionResult Team() => Redirect(\"/help#workflow\");", Read("web", "Controllers", "HelpController.cs"), StringComparison.Ordinal);
        Assert.Contains("public IActionResult Help() => Redirect(\"/help/team\");", Read("web", "Controllers", "AdminController.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheTeamsPart_ShouldWalkFromProductDataToAPublishedPassport()
    {
        var workflow = Guide[Guide.IndexOf("id=\"workflow\"", StringComparison.Ordinal)..Guide.IndexOf("id=\"words\"", StringComparison.Ordinal)];

        Assert.Contains("<a href=\"/admin/families\">Set up product data</a>", workflow, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/admin/batteries/new\">Create the battery</a>", workflow, StringComparison.Ordinal);
        Assert.Contains("Only \"required by Feb 2027\" blocks publishing", workflow, StringComparison.Ordinal);
        Assert.Contains("checks the passport against the official schema, signs it and publishes it", workflow, StringComparison.Ordinal);
        Assert.Contains("<dt>Archived version</dt>", Guide, StringComparison.Ordinal);
        foreach (var gone in new[] { "Validate passport", "Sign passport", "Dirty", "Evidence readiness", "Trust &amp; conformance" })
        {
            Assert.DoesNotContain(gone, Guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheClusterAdminsPart_ShouldSayHowToAddPeopleAndTokens()
    {
        var part = Guide[Guide.IndexOf("id=\"cluster\"", StringComparison.Ordinal)..Guide.IndexOf("@if (isTeam)", StringComparison.Ordinal)];

        Assert.Contains("<a href=\"/cluster-admin/users\">Add your people</a>", part, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/cluster-admin/api-tokens\">Give your systems a token</a>", part, StringComparison.Ordinal);
        Assert.Contains("it is shown once", part, StringComparison.Ordinal);
    }

    [Fact]
    public void TheApiExamples_ShouldUseTheDemoClusters()
    {
        Assert.DoesNotContain("demo-cluster", Api, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo API Cluster", Api, StringComparison.Ordinal);
        Assert.Contains("GET @Model.BasePath/clusters/demo-north/batteries", Api, StringComparison.Ordinal);
        Assert.Contains("\"clusterId\": \"demo-north\",", Api, StringComparison.Ordinal);
        Assert.Contains("No published demo battery to show. Use one of your batteries' IDs.", Api, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExampleBattery_ShouldBeAPublishedDemoBatteryOrNone()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var pages = rig.Services.GetRequiredService<BatteryPages>();

        Assert.Equal(string.Empty, await pages.ExampleBatteryIdAsync());

        await rig.Demo.SeedAsync(DemoRig.Who);
        var example = await pages.ExampleBatteryIdAsync();
        var published = (await pages.ListAsync(null)).Rows.Where(row => !row.IsDraft).Select(row => row.Identifier).ToList();
        Assert.Contains(example, published);

        // Batteries without the demo mark (real ones) are never shown.
        await rig.Database.GetCollection<BsonDocument>(DppStore.Packs).UpdateManyAsync(FilterDefinition<BsonDocument>.Empty, Builders<BsonDocument>.Update.Set("IsDemo", false));
        Assert.Equal(string.Empty, await pages.ExampleBatteryIdAsync());
    }
}
```

```bash
git rm -q BatteryPassWeb.Tests/AdminHelpPageTests.cs
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ReviewFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.HelpTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests"`
Expected: the build fails: `BatteryPages` has no `ExampleBatteryIdAsync` yet (CS1061).

- [ ] **Step 3: Replace `Guide.cshtml`**

`web/Views/Help/Guide.cshtml`:

```cshtml
@using BatteryPassWeb.Services
@{
    // The one Help place (step 3e): each reader sees only the parts for their role. The team's workflow used to be its
    // own page under the admin tabs (/help/team and /admin/help now lead here).
    ViewData["Title"] = "Help";
    var signedIn = User.Identity?.IsAuthenticated == true;
    var isTeam = User.IsInRole(AccessControlService.RoleAdmin);
    var runsACluster = isTeam || User.IsInRole(AccessControlService.RoleClusterAdmin);
}
<main class="bp-page bp-help-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <div class="bp-admin-heading">
        <h1>Help</h1>
        <p>How the battery passport works, for you.</p>
    </div>

    <div class="bp-help-layout mt-4">
        @await Html.PartialAsync("_HelpContents", "guide")
        <div class="bp-help-parts">
            <section class="bp-card bp-help-part" id="find">
                <h2>Find a passport</h2>
                <p class="mb-0">On the <a href="/">home page</a>, type the battery's serial number, battery ID or passport ID, or scan the QR code on the battery. @(signedIn ? "The Registry lists the batteries you may open." : "Signed in, the Registry also lists the batteries you may open.")</p>
            </section>

            <section class="bp-card bp-help-part" id="see">
                <h2>What each reader sees</h2>
                <dl class="bp-word-list">
                    <dt>Everyone</dt>
                    <dd>The public summary: the public values, the document links and the family's picture.</dd>
                    <dt>The battery's cluster, market surveillance</dt>
                    <dd>Also the purchaser values, such as state of charge, temperature, battery status and accident reports.</dd>
                    <dt>Repair, recycling, treatment, Commission</dt>
                    <dd>Also the shared values meant for them.</dd>
                    <dt>Notified bodies, market surveillance, Commission</dt>
                    <dd>Also the authorities' values, such as test reports.</dd>
                </dl>
                <p class="mt-3 mb-0">Signed in, the passport's detail page shows one section per group you belong to. Each value says when it was measured or entered, and how.</p>
            </section>

            @if (signedIn && !runsACluster)
            {
                <section class="bp-card bp-help-part" id="access">
                    <h2>Need more access?</h2>
                    <p class="mb-0">If you should see more than you do, ask your cluster admin or the manufacturer's team.</p>
                </section>
            }

            @if (runsACluster)
            {
                <section class="bp-card bp-help-part" id="cluster">
                    <h2>Your cluster <span class="bp-soft-tag">Cluster admins</span></h2>
                    <ol class="bp-steps">
                        <li><strong><a href="/cluster-admin/users">Add your people</a></strong><p>Under Cluster admin, Users: add someone as a user (sees your cluster's batteries) or as a cluster admin (also looks after its users and tokens).</p></li>
                        <li><strong><a href="/cluster-admin/api-tokens">Give your systems a token</a></strong><p>Under Cluster admin, API tokens: "Read" reads your batteries' passports; "Read + write" also sends their live values. Copy the value when it is shown: it is shown once.</p></li>
                        <li><strong><a href="/registry">Check what they see</a></strong><p>Your users find your cluster's batteries in the Registry, with the purchaser values on the detail page.</p></li>
                    </ol>
                </section>
            }

            @if (isTeam)
            {
                <section class="bp-card bp-help-part" id="workflow">
                    <h2>From product data to a published passport <span class="bp-soft-tag">Team</span></h2>
                    <ol class="bp-steps">
                        <li><strong><a href="/admin/families">Set up product data</a></strong><p>Families, models and batches, and <a href="/admin/sites-and-operators">sites and operators</a>. Most values are entered once and shared by every battery made from them; the V3 attribute list decides which are required, and by when.</p></li>
                        <li><strong><a href="/admin/batteries/new">Create the battery</a></strong><p>Choose its batch, economic operator and cluster, and type its serial number, manufacturing date and warranty. The app gives it its battery ID. It starts as a draft only the team sees.</p></li>
                        <li><strong><a href="/admin/batteries">Fill in what is missing</a></strong><p>Every field says whether it is required by Feb 2027, required later or conditional. Only "required by Feb 2027" blocks publishing, the first state of charge and temperature included.</p></li>
                        <li><strong>Publish</strong><p>On the battery page, one step checks the passport against the official schema, signs it and publishes it. Later changes ask first, then publish a new version; live values and accident reports change it in place.</p></li>
                    </ol>
                    <p class="mt-3 mb-0">Other systems get <a href="/admin/clusters?tab=api-token-management">API tokens</a>: a cluster token reads that cluster's batteries and, with "Read + write", sends their live values; only a token for all batteries creates and publishes them.</p>
                </section>

                <section class="bp-card bp-help-part" id="words">
                    <h2>Status words <span class="bp-soft-tag">Team</span></h2>
                    <dl class="bp-word-list">
                        <dt>Draft</dt>
                        <dd>Not published yet; only the team sees it, on the Batteries tab.</dd>
                        <dt>Published</dt>
                        <dd>Its current version is public and signed. Public search and the QR code open only published passports.</dd>
                        <dt>Archived version</dt>
                        <dd>An earlier version, kept with its signature when a newer one was published.</dd>
                    </dl>
                </section>

                <section class="bp-card bp-help-part" id="ready">
                    <h2>Before you call a battery ready <span class="bp-soft-tag">Team</span></h2>
                    <ul class="mb-0">
                        <li>The model, batch, site and operator are complete.</li>
                        <li>The battery's own values and first live values are in.</li>
                        <li>Nothing blocks publication, and the battery says "Published" with the latest version.</li>
                        <li>The QR code opens the public summary.</li>
                    </ul>
                </section>
            }

            <section class="bp-card bp-help-part" id="api">
                <h2>API reference and workbench</h2>
                <p class="mb-2">Every call, with examples for the demo clusters (<code>demo-north</code>, <code>demo-south</code>, <code>demo-fleet</code>), and a workbench to try a call with a token.</p>
                <a class="bp-secondary-button bp-button-compact" href="/help/api">Open the API reference</a>
            </section>
        </div>
    </div>
</main>
```

- [ ] **Step 4: Write `_HelpContents.cshtml`**

`web/Views/Shared/_HelpContents.cshtml`:

```cshtml
@using BatteryPassWeb.Services
@model string
@{
    // The one Help place (step 3e): its contents list shows only the parts for the reader's role. The model names the
    // page it is on, "guide" (/help) or "api" (/help/api), so the links work from both.
    var onGuide = !string.Equals(Model, "api", StringComparison.OrdinalIgnoreCase);
    var signedIn = User.Identity?.IsAuthenticated == true;
    var isTeam = User.IsInRole(AccessControlService.RoleAdmin);
    var runsACluster = isTeam || User.IsInRole(AccessControlService.RoleClusterAdmin);
    string Part(string id) => onGuide ? $"#{id}" : $"/help#{id}";
}
<nav class="bp-help-contents" aria-label="Help contents">
    <p>For everyone</p>
    <a href="@Part("find")">Find a passport</a>
    <a href="@Part("see")">What each reader sees</a>
    @if (signedIn && !runsACluster)
    {
        <a href="@Part("access")">Need more access?</a>
    }
    @if (runsACluster)
    {
        <p>For cluster admins</p>
        <a href="@Part("cluster")">Your cluster</a>
    }
    @if (isTeam)
    {
        <p>For the team</p>
        <a href="@Part("workflow")">From product data to a published passport</a>
        <a href="@Part("words")">Status words</a>
        <a href="@Part("ready")">Before you call a battery ready</a>
    }
    <p>For systems</p>
    <a href="/help/api" aria-current="@(onGuide ? null : "page")">API reference and workbench</a>
</nav>
```

- [ ] **Step 5: Make the Help one place**

Patch `3e-task6-code.patch` (`web/Controllers/HelpController.cs`, `web/Dpp/Pages/BatteryPages.cs`, `web/Views/Help/Index.cshtml`). Save the block as `../3e-task6-code.patch`, outside the worktree, and run `git apply ../3e-task6-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/HelpController.cs b/web/Controllers/HelpController.cs
--- a/web/Controllers/HelpController.cs
+++ b/web/Controllers/HelpController.cs
@@ -16,20 +16,15 @@ public class HelpController : Controller
         _batteryPages = batteryPages;
     }
 
-    /// <summary>The guide for everyone: finding a passport and what each role sees.</summary>
+    /// <summary>The one Help place (step 3e): each reader sees only the parts for their role.</summary>
     [AllowAnonymous]
     [HttpGet("")]
     public IActionResult Guide() => View("Guide");
 
-    /// <summary>The team's workflow help (it used to be /admin/help, which now forwards here).</summary>
+    /// <summary>The team's workflow is a part of the Help page now (this address, and /admin/help before it, lead there).</summary>
     [Authorize(Policy = "AdminOnly")]
     [HttpGet("team")]
-    public IActionResult Team([FromQuery] string? status, [FromQuery] string? error)
-    {
-        ViewData["StatusMessage"] = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status);
-        ViewData["ErrorMessage"] = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error);
-        return View("~/Views/Admin/Help.cshtml");
-    }
+    public IActionResult Team() => Redirect("/help#workflow");
 
     /// <summary>The API reference and request workbench.</summary>
     [AllowAnonymous]
@@ -40,7 +35,7 @@ public class HelpController : Controller
         {
             BasePath = "/api/external/v1",
             PublicBaseUrl = RequestBaseUrl(),
-            ExampleBatteryId = (await _batteryPages.ListAsync(null, cancellationToken)).Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier ?? string.Empty,
+            ExampleBatteryId = await _batteryPages.ExampleBatteryIdAsync(cancellationToken),
         };
 
         return View(model);
diff --git a/web/Dpp/Pages/BatteryPages.cs b/web/Dpp/Pages/BatteryPages.cs
--- a/web/Dpp/Pages/BatteryPages.cs
+++ b/web/Dpp/Pages/BatteryPages.cs
@@ -156,6 +156,22 @@ public sealed class BatteryPages(
         return new BatteriesPage(shown, wanted, rows.Count);
     }
 
+    /// <summary>The API help's example battery: a published demo battery's ID, or none. The help is open to everyone, so it
+    /// never shows a real battery's ID (product owner, 30 Oct 2026).</summary>
+    public async Task<string> ExampleBatteryIdAsync(CancellationToken cancellationToken = default)
+    {
+        var published = (await store.ListAsync<PassportVersion>(cancellationToken))
+            .Where(version => version.DppStatus == DppStatuses.Active)
+            .Select(version => version.PackId)
+            .ToHashSet(StringComparer.Ordinal);
+        return (await store.ListAsync<BatteryPack>(cancellationToken))
+            .Where(pack => pack.IsDemo && published.Contains(pack.Id))
+            .Select(pack => Text(pack, "uniqueProductIdentifier") ?? string.Empty)
+            .Where(identifier => identifier.Length > 0)
+            .Order(StringComparer.Ordinal)
+            .FirstOrDefault() ?? string.Empty;
+    }
+
     /// <summary>True when the search text is part of the battery's passport IDs, battery ID, serial number or cluster.</summary>
     public static bool Matches(BatteryRow row, string query, IEnumerable<PassportVersion> versions)
     {
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -4,14 +4,15 @@
 }
 <link rel="stylesheet" href="~/css/bp-api-help.css" asp-append-version="true" />
 <main class="bp-page bp-admin-page bp-help-page bp-api-help-page">
-    <header class="bp-console-header">
-        <div>
-            <h1>API help</h1>
-            <p class="text-secondary mb-0">Read passports, send a battery's live values, and create and publish batteries with a team token.</p>
-        </div>
-    </header>
+    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
+    <div class="bp-admin-heading">
+        <h1>Help</h1>
+        <p>The API: read passports, send a battery's live values, and create and publish batteries with a team token.</p>
+    </div>
 
-    @await Html.PartialAsync("_HelpTabs", "api")
+    <div class="bp-help-layout mt-4">
+    @await Html.PartialAsync("_HelpContents", "api")
+    <div class="bp-help-parts">
 
     <section class="bp-console-toolbar bp-api-help-meta-strip" aria-label="API connection values">
         <div class="bp-help-kv-grid">
@@ -31,7 +32,7 @@
                 }
                 else
                 {
-                    <span class="text-secondary small">No battery passport is published yet.</span>
+                    <span class="text-secondary small">No published demo battery to show. Use one of your batteries' IDs.</span>
                 }
             </div>
         </div>
@@ -246,7 +247,7 @@
 
                 <article id="api-detail-cluster-batteries" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
                     <div><span class="bp-summary-title">Cluster batteries</span><h3>List the batteries of one cluster.</h3><p>Each battery with its battery ID, serial number, family, model and current passport (passport ID, version and status). A cluster token lists its own clusters' batteries that have a published passport; a global token lists every battery, drafts included.</p></div>
-                    <pre class="bp-help-pre"><code>GET @Model.BasePath/clusters/demo-cluster/batteries</code></pre>
+                    <pre class="bp-help-pre"><code>GET @Model.BasePath/clusters/demo-north/batteries</code></pre>
                 </article>
 
                 <article id="api-detail-batches" class="bp-help-detail-panel" data-api-help-detail-panel hidden>
@@ -265,7 +266,7 @@ GET @Model.BasePath/operators</code></pre>
 {
   "batchId": "BATCH-ID",
   "operatorId": "OPERATOR-ID",
-  "clusterId": "demo-cluster",
+  "clusterId": "demo-north",
   "values": {
     "batterySerialNumber": "SN-API-REPLACE-ME",
     "manufacturingDate": "2026-09"
@@ -510,6 +511,8 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             </details>
         </section>
     </div>
+    </div>
+    </div>
 </main>
 
 @section Scripts {
@@ -618,7 +621,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 },
                 listClusterBatteries: {
                     method: 'GET',
-                    pathTemplate: `${basePath}/clusters/demo-cluster/batteries`,
+                    pathTemplate: `${basePath}/clusters/demo-north/batteries`,
                     body: ''
                 },
                 readSection: {
@@ -682,7 +685,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                     body: JSON.stringify({
                         batchId: 'BATCH-ID',
                         operatorId: 'OPERATOR-ID',
-                        clusterId: 'demo-cluster',
+                        clusterId: 'demo-north',
                         values: {
                             batterySerialNumber: 'SN-API-REPLACE-ME',
                             manufacturingDate: new Date().toISOString().slice(0, 7)
```

- [ ] **Step 6: Delete the old files**

```bash
git rm -q web/Views/Admin/Help.cshtml web/Views/Shared/_HelpTabs.cshtml
```

- [ ] **Step 7: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ReviewFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Look.HelpTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests"`
Expected: `Passed!  - Failed:     0, Passed:    60`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2098`, none failed.

- [ ] **Step 8: Commit**

```bash
git add BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs BatteryPassWeb.Tests/Dpp/ReviewFixesTests.cs BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Phase6ADocumentationTests.cs BatteryPassWeb.Tests/Look/HelpTests.cs web/Views/Help/Guide.cshtml web/Views/Shared/_HelpContents.cshtml web/Controllers/HelpController.cs web/Dpp/Pages/BatteryPages.cs web/Views/Help/Index.cshtml
git commit -m "feat(dpp): one Help place showing each reader their parts, the API examples on the demo data" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 7: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2098, Skipped:     0, Total:  2098`. From the 2078 before: 2082 after Task 1, 2086 after Task 2, 2090 after Task 3, 2097 after Task 4, 2102 after Task 5, 2098 after Task 6 (Task 6 retires the old team help page's tests).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat da214a2 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

Not steps of this plan. The managing session brings them to the product owner:

1. **Restart the app with the usual launcher**, and sign in with a team account.
2. **On the phone and on the computer**, open each admin tab:
   - **Clusters:** open a row, then close it; Demo data at the end.
   - **Users:** search for a name and open its row.
   - **API tokens:** "Create token" (Cancel is fine); open a row; "More".
   - **Audit log.**
3. **Help** in the top bar: the team's parts. Signed in as a demo cluster admin (if the demo data and `DEMO_USERS_PASSWORD` are there): "Your cluster", and the Cluster admin pages.
4. **Light and dark:** the moon button in the top bar switches.
5. **Demo data and restarts:** "Reset demo data", restart the app, then "Clear demo data": no demo cluster or token stays. "Reset demo data" again to keep the demo data.
6. **Only if a Clear or Reset was pressed after a restart before this plan:** the three demo clusters stay as real ones ("already there as real data" on a reset). Tidy by hand, once:
   - on API tokens, delete the six tokens named just "Demo North Operations", "Demo South Operations" or "Demo Fleet Operations";
   - "Clear demo data", then on Clusters delete demo-north, demo-south and demo-fleet;
   - "Reset demo data" if you want the demo data back.

