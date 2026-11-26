# DPP V3 Step 2d-2: The Team's Batteries Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The team manages batteries on new pages under the existing "Batteries" menu entry: the list with its search, creating a battery, and the battery page with its values, live values, cluster, publication checks, publishing, versions and accident reports. The old battery and passport admin pages go, and what they enforced comes back on the new model.

**Architecture:** Builds on 2d-1. Task 1 removes the old admin actions, whose routes would clash with the new ones. `SavePipeline` gains the pack rules (fixed once published, one serial number per battery) and `DppStore` the cluster helpers. `BatteryPages` loads the pages and makes the changes that aren't passport values (the cluster, publishing, removing a draft); values go through `RecordEditor`, now able to show a pack's own values and its live values as two forms. `BatteriesController` and the views in `Views/Batteries` put it on the admin layout with 2d-1's calmer finish, reusing `_RecordEditor` and `_EditorField`.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0. Plain CSS, inline SVG icons; nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 4, 6, 7.1–7.3, 9, 10, 12 and the 2d-2 bullet of 14) and the decisions record (decisions 23–27). Earlier plans: steps 1, 2a, 2b, 2c and 2d-1 in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 6c5bd16 (2d-1, its follow-ups and decision 28) plus the commit that adds this plan. Check with `git branch --show-current`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  1844`. After this plan: 1896. The suite takes about 30 seconds.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../2d2-task1-code.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order.
- **Commits:** end each message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (a second `-m`). Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **`APP_BASE_URL`** stays required to create a battery (each battery's #92 points at its accident page, from 2c). The create page says so when it's missing.

## Decisions taken with the product owner (29 Oct 2026)

1. **The cluster is optional** when a battery is created ("No cluster yet"); it is set later on the battery's page.
2. **"Battery cluster assignments" folds into "Batteries":** the list shows and searches the cluster; the battery page has "Change" beside it. The menu loses that entry.
3. **The battery page checks the uploaded files** its passport uses against the SHA-256 taken at upload (the old Conformance page did a similar check).
4. **Batch and economic operator are chosen once,** at creation. A draft made with the wrong ones is removed and created again.
5. **The look and behaviour** of the prototype (list, draft battery, published battery) were approved as shown.

## Judgement calls in this plan

1. **The new pages replace the old ones in place** (decision 23). "Batteries" opens `/admin/batteries`; `/admin`, `/admin/clusters` without a tab, `?tab=batteries`, `?tab=passports` and `?tab=battery` lead there. The admin actions under `/admin/batteries/...` and `/admin/passports/...` go, with the views only they used (`NewPassport`, `Passports`, `BatteryPassports` and the unused `Index` in `Views/Admin`). The views the cluster-admin pages still render (`EditPassport`, `Conformance`, `Audit`, `Revisions`) stay until steps 3 and 4; their admin branches are no longer reached. The shared battery table (`/registry`, `/cluster-admin/passports`) now links only to the public pages and the cluster-admin actions.
2. **Fixed once published** (parity check): a pack's own values whose rule is "fixed" — #11 serial number, #17 manufacturing date, #19 warranty — are corrected freely while the passport is a draft, and refused once any version exists, removals included: "Fixed once the passport is published." The rule sits in the save pipeline, so uploads (step 5) meet it too. The design's 2d-2 bullet calls #17 and #19 "manufacturer and economic operator"; the numbers are followed. The manufacturer (through the batch's site) and the economic operator can't change on a battery anyway (decision 4).
3. **One serial number per battery:** a serial number another battery has is refused at creation and when a draft is corrected, ignoring case and surrounding spaces, as the old battery pages judged it. Step 4 exempts linked successor packs (design 8.2).
4. **A battery's cluster must be a registered one,** or none. The team moves a battery directly (design 7.8, point 4): no new version, since the cluster isn't passport data; the move is logged as `dpp.pack-cluster-changed`.
5. **Deleting a cluster** counts its batteries with its passports and memberships. Force delete takes its batteries out of the cluster (they show "No cluster yet") and logs how many.
6. **The list search** matches part of a passport ID (#5, of any version), a battery ID (#6), a serial number (#11), or a cluster's ID or name, ignoring case.
7. **The battery page**, top to bottom: the heading (serial number, Draft or Published · version n, battery ID, batch, operator, cluster and, once published, the fixed values); Publication; Passport values; Live values; Accident reports; Versions; Remove (drafts only).
   - **Publication** lists what the passport still needs by February 2027 under the record that holds it — this battery, its live values, its model, batch, site or operator — with a link to that editor; schema shape problems on their own; what is needed only later, folded; and the file check. "Publish passport" appears only when nothing blocks, and asks in a dialog first.
   - **Passport values** is `RecordEditor` for a pack: #11, #13, #17 and #19 while a draft, #13 once published. A change that would publish a new version asks first, as in 2d-1.
   - **Live values** (#63, #83, #87) are their own form with the same stamp, so a page left open never undoes a newer value from the battery system, and an optional "Measured at", read in the server's time zone (the one the audit log shows). They change the passport in place.
   - **Accident reports** are uploaded and listed; the download link comes with step 3's accident page.
   - **Versions** shows each version's passport ID, status, publication and whether its signature still holds.
8. **Removing a battery:** only a draft, with its accident reports, logged as `dpp.record-removed`. A published battery stays with its versions.
9. **Test identifiers** (design 10): a battery whose IDs start with `TEST-` says so in a line under its heading.
10. **The finish** lives in `bp-product-data.css` (decision 27); `site.css` is not changed. Light and dark theme; no page scrolls sideways at 390 px.

## File structure

| File | Responsibility |
|---|---|
| `web/Controllers/AdminController.cs`, `RegistryController.cs`, `web/Services/BatteryTableService.cs`, the admin views and view models (modify, delete) | The old battery and passport admin pages removed; old addresses lead to the new list; deleting a cluster counts its batteries |
| `web/Views/Shared/_AdminTabs.cshtml` (modify) | "Batteries" opens `/admin/batteries`; no "Battery cluster assignments" |
| `web/Dpp/Store/DppStore.cs` (modify) | One serial number per battery; moving packs between clusters |
| `web/Dpp/Editing/SavePipeline.cs`, `PackCreationService.cs` (modify) | Fixed once published; duplicate serial numbers refused |
| `web/Dpp/Pages/RecordEditor.cs`, `ProductDataPages.cs` (modify) | A pack's own values and its live values as editor forms |
| `web/Dpp/Pages/BatteryPages.cs` | The list and search, a new battery, the battery page, the cluster, publishing, the file check, removing a draft |
| `web/Dpp/Publication/PublicationService.cs` (modify) | The publication checks without publishing |
| `web/Dpp/Audit/AuditLines.cs` (modify) | A battery's line links to its page; a cluster move in plain words |
| `web/Controllers/BatteriesController.cs`, `web/Views/Batteries/*.cshtml` | The routes under `/admin/batteries` and the three pages |
| `web/Views/ProductData/_RecordEditor.cshtml`, `web/wwwroot/css/bp-product-data.css` (modify) | The shared editor on a battery page; the batteries pages' finish |


---

### Task 1: The old battery and passport admin pages removed, and "Batteries" pointed at the new list

The admin actions under `/admin/batteries/...` and `/admin/passports/...` go first, because the new pages use the same addresses (design 14). The menu's "Batteries" opens `/admin/batteries` and "Battery cluster assignments" goes (decision 2 above). The old tests lose what checked the removed pages; tests of the views the cluster-admin pages still render keep theirs. `/admin/batteries` itself arrives in Task 4: until then it answers 404.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminEditGuidanceTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/PassportCanonicalDataTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/RegistryController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Models/ViewModels/BatteryViewModels.cs`
- Modify: `web/Services/BatteryTableService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/Views/Shared/_AdminTabs.cshtml`
- Delete: `web/Views/Admin/BatteryPassports.cshtml`
- Delete: `web/Views/Admin/Index.cshtml`
- Delete: `web/Views/Admin/NewPassport.cshtml`
- Delete: `web/Views/Admin/Passports.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `2d2-task1-tests.patch` (`BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/AdminEditGuidanceTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AdminHelpPageTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`, `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`, `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`, `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`, `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`, `BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs`, `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`, `BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`, `BatteryPassWeb.Tests/PassportCanonicalDataTests.cs`, `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`, `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`, `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`, `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`). Save the block as `../2d2-task1-tests.patch`, outside the worktree, and run `git apply ../2d2-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -60,43 +60,9 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.DoesNotContain("<th class=\"px-4 py-3\">Model</th>", passports);
         Assert.DoesNotContain("@row.ModelNumber", passports);
         Assert.Contains("_batteryRepository.ToSummary", service);
-        Assert.Contains("BatterySearchRedirectPath", service);
         Assert.Contains("serial", service, StringComparison.OrdinalIgnoreCase);
     }
 
-    [Fact]
-    public void AdminBatteryClusterAssignments_ShouldUseDenseBatteryIdentityRowsAndAssignmentOnlyControls()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-        var batteryTabStart = clusters.IndexOf("selectedTab == \"battery\"", StringComparison.Ordinal);
-        var clusterTabStart = clusters.IndexOf("selectedTab == \"clusters\"", StringComparison.Ordinal);
-        var batteryTab = clusters.Substring(batteryTabStart, clusterTabStart - batteryTabStart);
-
-        Assert.True(batteryTabStart >= 0);
-        Assert.True(clusterTabStart > batteryTabStart);
-        Assert.Contains("bp-admin-cluster-assignment-table", batteryTab);
-        Assert.Contains("bp-console-table", batteryTab);
-        Assert.Contains("Battery ID", batteryTab);
-        Assert.Contains("Battery Family", batteryTab);
-        Assert.Contains("Battery Model", batteryTab);
-        Assert.Contains("Battery serial number", batteryTab);
-        Assert.Contains("@passport.BatteryId", batteryTab);
-        Assert.Contains("@passport.BatteryFamily", batteryTab);
-        Assert.Contains("bp-battery-id-cell", batteryTab);
-        Assert.Contains("bp-cluster-assignment-form", batteryTab);
-        Assert.Contains("aria-label=\"Save cluster assignment\"", batteryTab);
-        Assert.Contains("title=\"Save cluster assignment\"", batteryTab);
-        Assert.DoesNotContain("<th class=\"bp-action-cell\">Actions</th>", batteryTab);
-        Assert.DoesNotContain("aria-label=\"Summary report\"", batteryTab);
-        Assert.DoesNotContain("aria-label=\"Detailed report\"", batteryTab);
-        Assert.DoesNotContain("aria-label=\"Edit passport snapshot\"", batteryTab);
-        Assert.DoesNotContain("<th class=\"px-3 py-3\">Battery</th>", batteryTab);
-        Assert.DoesNotContain("class=\"bp-secondary-button\">Save</button>", batteryTab);
-        Assert.Contains(".bp-cluster-assignment-form", css);
-        Assert.Contains(".bp-admin-cluster-assignment-table", css);
-    }
-
     [Fact]
     public void AdminUxPolish_ShouldUseExplicitRolesDuplicateClusterValidationAndStandardSelects()
     {
@@ -241,7 +207,7 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.Contains("bp-tab-row", tabs);
         Assert.Contains("Administration tabs", tabs);
         Assert.Contains("bp-tab-active", tabs);
-        Assert.Contains("tab=batteries", tabs);
+        Assert.Contains("href=\"/admin/batteries\"", tabs);
         Assert.Contains("href=\"/admin/clusters?tab=api-token-management\"", tabs);
         Assert.Contains("bp-admin-help-reference-console", help);
         Assert.Contains("bp-admin-help-reference-toolbar", help);
diff --git a/BatteryPassWeb.Tests/AdminEditGuidanceTests.cs b/BatteryPassWeb.Tests/AdminEditGuidanceTests.cs
--- a/BatteryPassWeb.Tests/AdminEditGuidanceTests.cs
+++ b/BatteryPassWeb.Tests/AdminEditGuidanceTests.cs
@@ -11,15 +11,6 @@ public sealed class AdminEditGuidanceTests
         Assert.Contains("FieldRequirementByKey", source);
     }
 
-    [Fact]
-    public void AdminController_ShouldLoadDataRequirementsForEditForms()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-
-        Assert.Contains("DataRequirements = dataRequirements", source);
-        Assert.Contains("BuildFieldRequirementDictionary", source);
-    }
-
     [Fact]
     public void EditPassportView_ShouldRenderRequiredOptionalMarkersAndGuidance()
     {
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -233,7 +233,6 @@ public sealed class AdminFeedbackFollowupTests
     {
         var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
 
         var expectedOrder = new[]
         {
@@ -259,7 +258,6 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("New passport needed", table);
         Assert.DoesNotContain("Latest passport status", table);
         Assert.Contains("bp-battery-table-toolbar", registry);
-        Assert.Contains("bp-battery-table-toolbar", clusters);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -136,17 +136,12 @@ public sealed class AdminFeedbackImplementationTests
     [Fact]
     public void PassportCreation_ShouldBeGuardedWhenNoNewPassportIsNeeded()
     {
-        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
         var historyModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
-        var adminHistoryView = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
         var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
 
-        Assert.Contains("CanCreateBatteryPassport", adminController);
-        Assert.Contains("No new passport is needed", adminController);
         Assert.Contains("CanCreateBatteryPassport", clusterAdminController);
         Assert.Contains("CanCreatePassport", historyModel);
-        Assert.Contains("Model.CanCreatePassport", adminHistoryView);
         Assert.Contains("ClearNewPassportRequiredAsync", externalApi);
     }
 
diff --git a/BatteryPassWeb.Tests/AdminHelpPageTests.cs b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
--- a/BatteryPassWeb.Tests/AdminHelpPageTests.cs
+++ b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
@@ -39,7 +39,7 @@ public sealed class AdminHelpPageTests
         Assert.Contains("_AdminTabs", markup);
         Assert.Contains("Administration tabs", tabs);
         Assert.Contains("bp-tab-active", tabs);
-        Assert.Contains("href=\"@($\"/admin/clusters?tab=batteries{batteryQuerySuffix}\")\"", tabs);
+        Assert.Contains("href=\"/admin/batteries\"", tabs);
         Assert.Contains("href=\"/admin/help\"", tabs);
         Assert.DoesNotContain("Back to main admin page", markup);
         Assert.DoesNotContain("bp-page-return-row", markup);
@@ -173,13 +173,9 @@ public sealed class AdminHelpPageTests
     [Fact]
     public void AdminPages_ShouldLinkToAdminHelp()
     {
-        var index = File.ReadAllText(RepoFile("web", "Views", "Admin", "Index.cshtml"));
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
 
-        Assert.Contains("/admin/help", index);
-        Assert.Contains("/admin/help", clusters);
         Assert.Contains("/admin/help", edit);
         Assert.Contains("/admin/help", conformance);
     }
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -7,8 +7,6 @@ public sealed class AdminTestFeedbackFixesTests
     {
         var program = File.ReadAllText(RepoFile("web", "Program.cs"));
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var adminPassports = File.ReadAllText(RepoFile("web", "Views", "Admin", "Passports.cshtml"));
         var clusterPassports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
 
         Assert.True(File.Exists(RepoFile("web", "Services", "BatteryTableService.cs")));
@@ -16,11 +14,8 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("AddSingleton<BatteryTableService>", program);
         Assert.Contains("BatteryTablePageViewModel", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs")));
         Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs")));
-        Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs")));
         Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs")));
         Assert.Contains("_BatteryTable", registry);
-        Assert.Contains("_BatteryTable", clusters);
-        Assert.Contains("_BatteryTable", adminPassports);
         Assert.Contains("_BatteryTable", clusterPassports);
     }
 
@@ -28,7 +23,6 @@ public sealed class AdminTestFeedbackFixesTests
     public void SharedBatteryTable_ShouldIncludeClusterNewPassportAndRoleAwareActions()
     {
         var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var history = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
 
         Assert.Contains("<th>Cluster</th>", table);
         Assert.Contains("New passport needed", table);
@@ -37,8 +31,6 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("row.CanOpenConformance", table);
         Assert.Contains("row.HistoryUrl", table);
         Assert.Contains("row.ReturnUrl", table);
-        Assert.Contains("returnUrl", history);
-        Assert.Contains("Back to Registry", history);
     }
 
     [Fact]
@@ -69,7 +61,6 @@ public sealed class AdminTestFeedbackFixesTests
     public void BatteryCreateAndEdit_ShouldRenderPolicyLockedFieldsAndClusterSelector()
     {
         var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
         var delta = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));
 
@@ -78,10 +69,6 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("is-locked-field", edit);
         Assert.Contains("Battery mass", edit);
         Assert.DoesNotContain(">Weight kg", edit);
-        Assert.Contains("ApplyBatteryEditableFormValues", adminController);
-        Assert.Contains("IsEditableAtCreation", adminController);
-        Assert.Contains("IsEditableAfterCreation", adminController);
-        Assert.Contains("Rejected locked field", adminController);
         Assert.Contains("\"general.batteryMass\"", policy);
         Assert.Contains("aspects.generalProductInformation.payload.batteryMass", policy);
         Assert.Contains("OperationalFieldKeys", delta);
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -116,11 +116,9 @@ public sealed class ApplicationAuditCoverageTests
     [Fact]
     public void PassportAuditPages_ShouldUseExpandedBatteryTimeline()
     {
-        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
         var auditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
 
-        Assert.Contains("ListPassportTimelineEventsAsync", admin);
         Assert.Contains("ListPassportTimelineEventsAsync", clusterAdmin);
         Assert.Contains("_AuditEventRow", auditView);
     }
@@ -128,13 +126,11 @@ public sealed class ApplicationAuditCoverageTests
     [Fact]
     public void BatteryAndPassportWritePaths_ShouldAppendMeaningfulAuditEvents()
     {
-        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
         var externalApi = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
         var passportsApi = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
         var snapshot = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));
 
-        Assert.Contains("\"battery.updated\"", admin);
         Assert.Contains("\"battery.updated\"", clusterAdmin);
         Assert.Contains("\"battery.telemetry.written\"", externalApi);
         Assert.Contains("\"battery.operations.updated\"", externalApi);
diff --git a/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs b/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
--- a/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
+++ b/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
@@ -2,29 +2,11 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class BatteryAdminWorkflowTests
 {
-    [Fact]
-    public void AdminController_ShouldExposeBatteryCreationAndPassportSnapshotActions()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-
-        Assert.Contains("[HttpGet(\"batteries/new\")]", source);
-        Assert.Contains("[HttpPost(\"batteries/create\")]", source);
-        Assert.Contains("[HttpPost(\"batteries/{batteryId}/passports/create\")]", source);
-        Assert.Contains("CreateBattery", source);
-        Assert.Contains("CreateBatteryPassport", source);
-        Assert.Contains("CreatePassportSnapshotAsync", source);
-        Assert.Contains("Battery Family and serial number are locked", source);
-    }
-
     [Fact]
     public void BatteryCreatePage_ShouldPreviewGeneratedBatteryId()
     {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
 
-        Assert.Contains("[HttpGet(\"batteries/id-preview\")]", controller);
-        Assert.Contains("PreviewBatteryId", controller);
-        Assert.Contains("CreateBatteryId(product.ProductName, serialNumber)", controller);
         Assert.Contains("Battery ID preview", view);
         Assert.Contains("data-battery-id-preview", view);
         Assert.Contains("/admin/batteries/id-preview", view);
@@ -46,14 +28,8 @@ public sealed class BatteryAdminWorkflowTests
     [Fact]
     public void AdminBatteryEdit_ShouldEditBatteryAndReturnToBatteryListWithPendingSnapshot()
     {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
 
-        Assert.Contains("[HttpGet(\"batteries/{batteryId}/edit\")]", controller);
-        Assert.Contains("[HttpPost(\"batteries/{batteryId}/save\")]", controller);
-        Assert.Contains("SaveBattery", controller);
-        Assert.Contains("app.snapshot.newPassportRequired", controller);
-        Assert.Contains("/admin/clusters?tab=batteries", controller);
         Assert.Contains("battery-edit", view);
         Assert.Contains("readonly", view);
         Assert.Contains("New passport", view);
@@ -66,8 +42,6 @@ public sealed class BatteryAdminWorkflowTests
         var creationService = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
 
         Assert.Contains("Battery cluster is required.", creationService);
-        Assert.Contains("UpdateNewPassportRequiredAsync", controller);
-        Assert.Contains("ClearNewPassportRequiredAsync", controller);
         Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
     }
 
@@ -78,13 +52,9 @@ public sealed class BatteryAdminWorkflowTests
         var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
         var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
 
-        Assert.Contains("private static string AdminBatteriesUrl", controller);
-        Assert.Contains("return Redirect(AdminBatteriesUrl(q));", controller);
-        Assert.Contains("TempData[\"StatusMessage\"] = $\"Passport {passportId} saved.\";", controller);
-        Assert.Contains("return Redirect(AdminBatteriesUrl());", controller);
         Assert.Contains("\"passports\" => \"batteries\"", controller);
         Assert.DoesNotContain("href=\"/admin/clusters?tab=passports\"", help);
-        Assert.Contains("href=\"/admin/clusters?tab=batteries\"", help);
+        Assert.Contains("href=\"/admin/batteries\"", help);
         Assert.Contains("Back to battery list", conformance);
     }
 
@@ -112,30 +82,21 @@ public sealed class BatteryAdminWorkflowTests
     public void AdminBatteryActions_ShouldUseUnifiedIconButtons()
     {
         var clusters = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var history = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("bp-action-button", css);
         Assert.Contains("bp-action-icon", css);
-        Assert.Contains("data-action-icon=\"history\"", clusters);
         Assert.Contains("data-action-icon=\"edit\"", clusters);
         Assert.Contains("data-action-icon=\"conformance\"", clusters);
         Assert.Contains("data-action-icon=\"create-passport\"", clusters);
-        Assert.Contains("data-action-icon=\"summary\"", history);
-        Assert.Contains("data-action-icon=\"detail\"", history);
-        Assert.Contains("data-action-icon=\"audit\"", history);
-        Assert.Contains("data-action-icon=\"archive\"", history);
         Assert.DoesNotContain("&#9776;", clusters);
         Assert.DoesNotContain("&#9998;", clusters);
         Assert.DoesNotContain("&#10003;", clusters);
-        Assert.DoesNotContain(">A</a>", history);
-        Assert.DoesNotContain(">X</button>", history);
     }
 
     [Fact]
     public void FollowupBatteryCreate_ShouldRequireClusterAndUniqueSerial()
     {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var creationService = File.ReadAllText(RepoFile("web", "Services", "BatteryCreationService.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
         var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
@@ -143,7 +104,6 @@ public sealed class BatteryAdminWorkflowTests
         Assert.Contains("Battery cluster is required.", creationService);
         Assert.Contains("Battery serial number already exists.", creationService);
         Assert.Contains("GetBySerialNumberAsync", creationService);
-        Assert.Contains("_batteryCreationService.CreateBatteryAsync", controller);
         Assert.Contains("GetBySerialNumberAsync", repository);
         Assert.Contains("name=\"clusterId\" required", view);
         Assert.Contains("is-policy-locked", view);
@@ -158,9 +118,7 @@ public sealed class BatteryAdminWorkflowTests
         var deltaService = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));
         var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
 
-        Assert.Contains("CreatePassportSnapshotAsync", controller);
         Assert.Contains("CreatePassportSnapshotAsync", clusterAdminController);
-        Assert.Contains("Redirect($\"/admin/batteries/{Uri.EscapeDataString", controller);
         Assert.Contains("Redirect($\"/cluster-admin/passports", clusterAdminController);
         Assert.DoesNotContain("return Redirect($\"/admin/passports/{Uri.EscapeDataString(passportId)}/edit\")", controller);
         Assert.Contains("NumericBsonEquals", deltaService);
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -98,12 +98,10 @@ public sealed class BatteryFamilyAccessApiRevisedTests
     [Fact]
     public void ValidateSignPublish_ShouldUseSharedAutoRepublishWorkflow()
     {
-        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var api = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
         var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
 
-        Assert.Contains("PassportTrustWorkflowService", admin);
         Assert.Contains("PassportTrustWorkflowService", api);
         Assert.Contains("ShouldAutoPublishAfterSign", workflow);
         Assert.Contains("AutoPublished", workflow);
@@ -133,9 +131,7 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
         var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
         var adminClusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var batteryPassports = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
 
         Assert.Contains("_BatteryTable", registry);
         Assert.Contains("aria-label=\"Summary report\"", batteryTable);
@@ -146,12 +142,8 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.Contains("title=\"Passport history\"", batteryTable);
         Assert.Contains("title=\"Create passport\"", batteryTable);
         Assert.Contains("title=\"Conformance\"", batteryTable);
-        Assert.Contains("title=\"Archive\"", batteryPassports);
-        Assert.Contains("title=\"Unarchive\"", batteryPassports);
         Assert.Contains("bp-confirm-modal", adminClusters);
-        Assert.Contains("data-confirm-action", batteryPassports);
         Assert.Contains("UnarchivePassportAsync", repository);
-        Assert.Contains("[HttpPost(\"passports/unarchive\")]", controller);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
--- a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
+++ b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
@@ -178,25 +178,6 @@ public sealed class BatteryRouteAndRegistryTests
         Assert.Contains("max-width: none", css);
     }
 
-    [Fact]
-    public void AdminBatteryPassportHistory_ShouldHaveDedicatedRouteAndIconActions()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
-
-        Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
-        Assert.Contains("BatteryPassports", controller);
-        Assert.Contains("Model.ReturnLabel", view);
-        Assert.Contains("/admin/clusters?tab=batteries", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs")));
-        Assert.Contains("returnUrl", view);
-        Assert.Contains("Summary report", view);
-        Assert.Contains("Detailed report", view);
-        Assert.Contains("Conformance", view);
-        Assert.Contains("Audit trail", view);
-        Assert.Contains("Archive", view);
-        Assert.Contains("Unarchive", view);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var roots = new[]
diff --git a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
--- a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
@@ -17,12 +17,7 @@ public sealed class ConformanceLayoutTests
     {
         var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
 
-        Assert.Contains("[HttpGet(\"passports/{passportId}/conformance\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/validate\")]", source);
-        Assert.Contains("ProductTemplateService", source);
-        Assert.Contains("GetPolicyForPassportAsync", source);
         Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
-        Assert.Contains("ConformanceViewModel", source);
     }
 
     [Fact]
@@ -250,25 +245,16 @@ public sealed class ConformanceLayoutTests
     [Fact]
     public void AdminController_ShouldComputePassportReadinessForConformance()
     {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var program = File.ReadAllText(RepoFile("web", "Program.cs"));
 
-        Assert.Contains("PassportReadinessService", source);
-        Assert.Contains("_passportReadinessService.Evaluate", source);
-        Assert.Contains("GroupedBlockingIssues", source);
         Assert.Contains("AddSingleton<PassportReadinessService>", program);
     }
 
     [Fact]
     public void AdminAndApiControllers_ShouldAppendEvidenceValidationSection()
     {
-        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
 
-        Assert.Contains("PassportEvidenceService", admin);
-        Assert.Contains("_passportEvidenceService.Evaluate", admin);
-        Assert.Contains("AppendValidationSection", admin);
-        Assert.Contains("EvidencePack", admin);
 
         Assert.Contains("PassportEvidenceService", api);
         Assert.Contains("_passportEvidenceService.Evaluate", api);
diff --git a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
--- a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
+++ b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
@@ -52,7 +52,6 @@ public sealed class CopyIdButtonLayoutTests
         {
             RepoFile("web", "Views", "Passport", "Battery.cshtml"),
             RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
-            RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"),
             RepoFile("web", "Views", "Admin", "Conformance.cshtml"),
             RepoFile("web", "Views", "Admin", "Audit.cshtml"),
             RepoFile("web", "Views", "Admin", "Revisions.cshtml"),
diff --git a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
--- a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
+++ b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
@@ -15,8 +15,6 @@ public sealed class DataRequirementsAdminLayoutTests
     {
         var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
 
-        Assert.Contains("DataCompletionPolicyService", source);
-        Assert.Contains("GetPolicyForPassportAsync", source);
         Assert.DoesNotContain("[HttpPost(\"data-requirements/save\")]", source);
         Assert.DoesNotContain("SaveDataRequirements", source);
     }
diff --git a/BatteryPassWeb.Tests/DocumentAccessControlTests.cs b/BatteryPassWeb.Tests/DocumentAccessControlTests.cs
--- a/BatteryPassWeb.Tests/DocumentAccessControlTests.cs
+++ b/BatteryPassWeb.Tests/DocumentAccessControlTests.cs
@@ -73,7 +73,6 @@ public sealed class DocumentAccessControlTests
     public void AdminEditPassport_ShouldExposeAndPersistDocumentVisibility()
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
         var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));
 
@@ -82,8 +81,6 @@ public sealed class DocumentAccessControlTests
         Assert.Contains("document_co2StudyReference_visibility", markup);
         Assert.Contains("Public document", markup);
         Assert.Contains("Private / privileged", markup);
-        Assert.Contains("NormalizeDocumentVisibility", controller);
-        Assert.Contains("documentNode[\"visibility\"]", controller);
         Assert.Contains("Visibility", model);
         Assert.Contains("document.GetValue(\"visibility\"", factory);
     }
diff --git a/BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs b/BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs
--- a/BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs
@@ -1,11 +1,12 @@
 namespace BatteryPassWeb.Tests.Dpp;
 
-/// <summary>The admin menu after 2d-1: the new pages under the existing entries, no temporary or duplicate entries (decision 23).</summary>
+/// <summary>The admin menu after 2d-2: the new pages under the existing entries, no temporary or duplicate entries (decision 23).</summary>
 public sealed class AdminMenuTests
 {
     private static readonly string Menu = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
 
     [Theory]
+    [InlineData("/admin/batteries", "Batteries")]
     [InlineData("/admin/families", "Battery families")]
     [InlineData("/admin/sites-and-operators", "Sites and operators")]
     public void NewPages_ShouldBeInBothTheTabsAndThePhoneMenu(string href, string label)
@@ -20,6 +21,9 @@ public sealed class AdminMenuTests
         Assert.DoesNotContain("Editable fields", Menu, StringComparison.Ordinal);
         Assert.DoesNotContain("tab=local-editable-fields", Menu, StringComparison.Ordinal);
         Assert.DoesNotContain("tab=products", Menu, StringComparison.Ordinal);
+        Assert.DoesNotContain("Battery cluster assignments", Menu, StringComparison.Ordinal);
+        Assert.DoesNotContain("tab=batteries", Menu, StringComparison.Ordinal);
+        Assert.DoesNotContain("tab=battery\"", Menu, StringComparison.Ordinal);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs b/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
--- a/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
+++ b/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
@@ -5,15 +5,11 @@ public sealed class ErrorHandlingRolloutTests
     [Fact]
     public void TrustChangingAdminActions_ShouldShowServiceErrorInsteadOfClaimingSuccess()
     {
-        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var api = File.ReadAllText(RepoFile("web", "Controllers", "PassportsApiController.cs"));
         var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
         var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
 
-        Assert.Contains("TrustWorkflowServiceErrorMessage", admin);
-        Assert.Contains("catch (Exception exception) when (IsTrustPersistenceFailure(exception))", admin);
         Assert.Contains("if (!trustUpdated)", workflow);
-        Assert.Contains("if (!published)", admin);
         Assert.Contains("if (!published)", workflow);
         Assert.Contains("StatusCodes.Status503ServiceUnavailable", api);
         Assert.Contains("retrySafe = true", api);
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
@@ -7,12 +7,6 @@ public sealed class ExternalApiBatteryCreateTests
     {
         var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
 
-        Assert.Contains("BatteryCreationService", source);
-        Assert.Contains("_batteryCreationService.CreateBatteryAsync", source);
-        Assert.Contains("CustomizeBatteryBeforeInsert", source);
-        Assert.Contains("ApplyBatteryEditableFormValues(form, editablePolicy, forCreation: true)", source);
-        Assert.Contains("ApplyPassportForm(battery, filteredForm, createNow)", source);
-        Assert.Contains("new BatteryCreationActor(CurrentActor(), \"admin\", \"admin-ui\")", source);
         Assert.DoesNotContain("await _batteryRepository.CreateBatteryAsync(battery, cancellationToken);", source);
     }
 
diff --git a/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs b/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs
--- a/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs
+++ b/BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs
@@ -1,10 +1,8 @@
-using BatteryPassWeb.Controllers;
 using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Http;
 using Microsoft.Extensions.Primitives;
 using MongoDB.Bson;
 using System.Globalization;
-using System.Reflection;
 
 namespace BatteryPassWeb.Tests;
 
@@ -135,35 +133,6 @@ public sealed class ManualFeedbackRound3Tests
         Assert.False(BatteryPassportDeltaService.HasEditableDifferences(battery, latestPassport, policy));
     }
 
-    [Fact]
-    public void BatterySave_ShouldParseDecimalInputsInvariantlyAndPreserveLockedArrayValues()
-    {
-        var previousCulture = CultureInfo.CurrentCulture;
-        var previousUiCulture = CultureInfo.CurrentUICulture;
-        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
-        CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
-        try
-        {
-            var document = BuildEditableSaveDocument();
-            var form = new FormCollection(new Dictionary<string, StringValues>
-            {
-                ["materialNickel"] = "52.74"
-            });
-
-            InvokeApplyPassportForm(document, form);
-
-            Assert.Equal(52.74, ReadMaterialMass(document, "Nickel"), precision: 2);
-            Assert.Equal(42.5, ReadCarbonStage(document, "RawMaterialExtraction"), precision: 2);
-            Assert.Equal(18.25, ReadRecycledShare(document, "Nickel", "preConsumerShare"), precision: 2);
-            Assert.Equal(12.75, ReadRecycledShare(document, "Nickel", "postConsumerShare"), precision: 2);
-        }
-        finally
-        {
-            CultureInfo.CurrentCulture = previousCulture;
-            CultureInfo.CurrentUICulture = previousUiCulture;
-        }
-    }
-
     [Fact]
     public void ConformancePage_ShouldRouteFixActionsToBatteryEditInsteadOfPassportSnapshotEdit()
     {
@@ -261,103 +230,4 @@ public sealed class ManualFeedbackRound3Tests
         };
     }
 
-    private static BsonDocument BuildEditableSaveDocument() =>
-        new()
-        {
-            ["passportId"] = "did:web:acme.battery.pass:test",
-            ["app"] = new BsonDocument
-            {
-                ["display"] = new BsonDocument
-                {
-                    ["serialNumber"] = "SN-SOUTH-002",
-                    ["modelNumber"] = "CP13M-SOUTH-002"
-                },
-                ["product"] = new BsonDocument()
-            },
-            ["aspects"] = new BsonDocument
-            {
-                ["generalProductInformation"] = new BsonDocument
-                {
-                    ["payload"] = new BsonDocument
-                    {
-                        ["manufacturingDate"] = "2026-05-01T00:00:00.000Z"
-                    }
-                },
-                ["materialComposition"] = new BsonDocument
-                {
-                    ["payload"] = new BsonDocument
-                    {
-                        ["batteryMaterials"] = new BsonArray
-                        {
-                            new BsonDocument
-                            {
-                                ["batteryMaterialName"] = "Nickel",
-                                ["batteryMaterialMass"] = 52.74
-                            }
-                        }
-                    }
-                },
-                ["carbonFootprintForBatteries"] = new BsonDocument
-                {
-                    ["payload"] = new BsonDocument
-                    {
-                        ["batteryCarbonFootprint"] = 84.2,
-                        ["carbonFootprintPerformanceClass"] = "B",
-                        ["carbonFootprintPerLifecycleStage"] = new BsonArray
-                        {
-                            new BsonDocument
-                            {
-                                ["lifecycleStage"] = "RawMaterialExtraction",
-                                ["carbonFootprint"] = 42.5
-                            }
-                        }
-                    }
-                },
-                ["circularity"] = new BsonDocument
-                {
-                    ["payload"] = new BsonDocument
-                    {
-                        ["recycledContent"] = new BsonArray
-                        {
-                            new BsonDocument
-                            {
-                                ["recycledMaterial"] = "Nickel",
-                                ["preConsumerShare"] = 18.25,
-                                ["postConsumerShare"] = 12.75
-                            }
-                        }
-                    }
-                }
-            }
-        };
-
-    private static void InvokeApplyPassportForm(BsonDocument document, IFormCollection form)
-    {
-        var method = typeof(AdminController).GetMethod(
-            "ApplyPassportForm",
-            BindingFlags.NonPublic | BindingFlags.Static);
-        Assert.NotNull(method);
-        method.Invoke(null, [document, form, "2026-05-21T12:00:00.000Z"]);
-    }
-
-    private static double ReadMaterialMass(BsonDocument document, string material) =>
-        document["aspects"]["materialComposition"]["payload"]["batteryMaterials"]
-            .AsBsonArray
-            .OfType<BsonDocument>()
-            .Single(row => row["batteryMaterialName"].AsString == material)["batteryMaterialMass"]
-            .ToDouble();
-
-    private static double ReadCarbonStage(BsonDocument document, string stage) =>
-        document["aspects"]["carbonFootprintForBatteries"]["payload"]["carbonFootprintPerLifecycleStage"]
-            .AsBsonArray
-            .OfType<BsonDocument>()
-            .Single(row => row["lifecycleStage"].AsString == stage)["carbonFootprint"]
-            .ToDouble();
-
-    private static double ReadRecycledShare(BsonDocument document, string material, string field) =>
-        document["aspects"]["circularity"]["payload"]["recycledContent"]
-            .AsBsonArray
-            .OfType<BsonDocument>()
-            .Single(row => row["recycledMaterial"].AsString == material)[field]
-            .ToDouble();
 }
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -41,9 +41,7 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains("bp-mobile-identity", layout);
         Assert.Contains("bp-admin-mobile-tab-form", adminTabs);
         Assert.Contains("data-admin-mobile-tab-select", adminTabs);
-        Assert.Contains("bp-admin-cluster-assignment-table bp-mobile-card-table", clusters);
         Assert.Contains("bp-cluster-management-table bp-mobile-card-table", clusters);
-        Assert.Contains("data-label=\"Assign\"", clusters);
         Assert.Contains("data-label=\"Cluster ID\"", clusters);
         Assert.Contains("bp-admin-help-workflow-table bp-mobile-card-table", adminHelp);
         Assert.Contains("bp-admin-help-status-table bp-mobile-card-table", adminHelp);
@@ -92,12 +90,9 @@ public sealed class MobileThemeSessionLayoutTests
     public void GlobalAdminMobileToolbar_ShouldAvoidDuplicateHelpButtonAndAlignCreateAction()
     {
         var adminTabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("<option value=\"/admin/help\"", adminTabs);
-        Assert.Contains("bp-battery-toolbar-actions", clusters);
-        Assert.Contains("bp-battery-toolbar-help", clusters);
         Assert.Contains(".bp-battery-toolbar-actions", css);
         Assert.Contains(".bp-battery-toolbar-help", css);
         Assert.Contains("display: none;", css);
diff --git a/BatteryPassWeb.Tests/PassportCanonicalDataTests.cs b/BatteryPassWeb.Tests/PassportCanonicalDataTests.cs
--- a/BatteryPassWeb.Tests/PassportCanonicalDataTests.cs
+++ b/BatteryPassWeb.Tests/PassportCanonicalDataTests.cs
@@ -172,9 +172,6 @@ public sealed class PassportCanonicalDataTests
     {
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
 
-        Assert.Contains("NormalizeBatteryPassportIdentifier", controller);
-        Assert.Contains("NormalizeBatteryCategory", controller);
-        Assert.Contains("NormalizeManufacturerSerialNumber", controller);
         Assert.DoesNotContain("generalPayload[\"batteryPassportIdentifier\"] = $\"urn:acme:", controller);
         Assert.DoesNotContain("generalPayload[\"batteryCategory\"] = BatteryImageCatalog.CategoryForImageUrl", controller);
     }
diff --git a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
@@ -18,8 +18,6 @@ public sealed class ProductTemplateWorkflowLayoutTests
         var service = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
         var dataCompletionSource = File.ReadAllText(RepoFile("web", "Services", "DataCompletionPolicyService.cs"));
 
-        Assert.Contains("ProductTemplateService", source);
-        Assert.Contains("GetPolicyForPassportAsync", source);
         Assert.Contains("batteryProductTemplateVersions", service);
         Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", service);
         Assert.Contains("productVersion", service);
@@ -84,12 +82,10 @@ public sealed class ProductTemplateWorkflowLayoutTests
     public void AdminAndClusterAdmin_ShouldExposeEditableFieldPolicy()
     {
         var sharedEdit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var program = File.ReadAllText(RepoFile("web", "Program.cs"));
 
         Assert.Contains("FieldEditableByKey", sharedEdit);
         Assert.Contains("FieldVisibleByKey", sharedEdit);
-        Assert.Contains("EditableFieldPolicyService", adminController);
         Assert.Contains("AddSingleton<EditableFieldPolicyService>", program);
     }
 
@@ -112,8 +108,6 @@ public sealed class ProductTemplateWorkflowLayoutTests
         Assert.Contains("data-field-key", edit);
         Assert.Contains("has-missing-fields", edit);
         Assert.Contains("markMissingRequiredFields", edit);
-        Assert.Contains("puttingIntoService", File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs")));
-        Assert.Contains("return Redirect(AdminBatteriesUrl", File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs")));
         Assert.Contains("applyProductTemplateSelection", edit);
         Assert.Contains("updateProductVersionOptions", edit);
         Assert.DoesNotContain("updateSoftwareOptions(productVersion", edit);
diff --git a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
@@ -16,18 +16,11 @@ public sealed class SigningWorkflowLayoutTests
     [Fact]
     public void AdminController_ShouldExposeSignPublishAuditAndRevisionRoutes()
     {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
 
-        Assert.Contains("[HttpPost(\"passports/{passportId}/sign\")]", source);
-        Assert.Contains("[HttpPost(\"passports/{passportId}/publish\")]", source);
-        Assert.Contains("[HttpGet(\"passports/{passportId}/audit\")]", source);
-        Assert.Contains("[HttpGet(\"passports/{passportId}/revisions\")]", source);
-        Assert.Contains("PassportTrustWorkflowService", source);
         Assert.Contains("CreateSignedRevisionAsync", workflow);
         Assert.Contains("UpdateTrustSignatureAsync", workflow);
         Assert.Contains("AppendAuditEventAsync", workflow);
-        Assert.Contains("PublishPassportAsync", source);
     }
 
     [Fact]
@@ -152,7 +145,6 @@ public sealed class SigningWorkflowLayoutTests
         var audit = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
         var auditRow = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AuditEventRow.cshtml"));
         var revisions = File.ReadAllText(RepoFile("web", "Views", "Admin", "Revisions.cshtml"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("Audit trail", audit);
@@ -171,9 +163,6 @@ public sealed class SigningWorkflowLayoutTests
         Assert.Contains("Proof diagnostics", revisions);
         Assert.Contains("Latest trust state", revisions);
         Assert.Contains("passport.TrustIsDirty", revisions);
-        Assert.Contains("passport.updated", controller);
-        Assert.Contains("BuildChangeMetadata", controller);
-        Assert.Contains("changedFields", controller);
         Assert.Contains(".bp-ledger", css);
         Assert.Contains(".bp-ledger-timeline", css);
         Assert.Contains(".bp-revision-card", css);
@@ -184,12 +173,10 @@ public sealed class SigningWorkflowLayoutTests
     {
         var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
         var clusterController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var adminHistory = File.ReadAllText(RepoFile("web", "Views", "Admin", "BatteryPassports.cshtml"));
 
         Assert.Contains("CanViewBatteryHistoryAsync", access);
         Assert.Contains("CanEditLatestBatteryPassportAsync", access);
         Assert.Contains("isLatestForBattery", clusterController);
-        Assert.Contains("readonly history", adminHistory, StringComparison.OrdinalIgnoreCase);
     }
 
     private static string RepoFile(params string[] parts)
diff --git a/BatteryPassWeb.Tests/TrustDirtyStateTests.cs b/BatteryPassWeb.Tests/TrustDirtyStateTests.cs
--- a/BatteryPassWeb.Tests/TrustDirtyStateTests.cs
+++ b/BatteryPassWeb.Tests/TrustDirtyStateTests.cs
@@ -25,14 +25,6 @@ public sealed class TrustDirtyStateTests
         Assert.Contains("CreateBatteryPassport", source);
     }
 
-    [Fact]
-    public void AdminController_ShouldMarkCanonicalSavesDirty()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-
-        Assert.Contains("MarkCanonicalDirtyAsync(passportId", source);
-    }
-
     [Fact]
     public void ExternalApiAcceptance_ShouldOnlyWriteOperationalFieldsWithoutCallingDirtyMarker()
     {
diff --git a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
--- a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
@@ -15,8 +15,6 @@ public sealed class ValidationPolicyLayoutTests
     {
         var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
 
-        Assert.Contains("PassportPublishPolicyService", source);
-        Assert.Contains("SanitizeTrustClaimsForDraftSave", source);
         Assert.DoesNotContain("validation[\"isValid\"] = true", source);
         Assert.DoesNotContain("verification[\"signedAt\"] = now", source);
     }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminEditGuidanceTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAdminWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryRouteAndRegistryTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DocumentAccessControlTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AdminMenuTests|FullyQualifiedName~BatteryPassWeb.Tests.ErrorHandlingRolloutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryCreateTests|FullyQualifiedName~BatteryPassWeb.Tests.ManualFeedbackRound3Tests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportCanonicalDataTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustDirtyStateTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: `Failed!  - Failed:     5, Passed:   196`. Two menu test cases fail, because the menu still links `tab=batteries` and has "Battery cluster assignments". Three updated old tests fail because they now expect the new address: `AdminDenseConsoleLayoutTests.AdminHelp_ShouldUsePersistentAdminTabsAndLighterWorkflowIntro`, `AdminHelpPageTests.AdminHelpView_ShouldRenderDenseNavigationWithoutBackToMainAdminLink` and `BatteryAdminWorkflowTests.AdminBatteryList_ShouldUseBatteriesTabAsCanonicalDestination`.

- [ ] **Step 3: Remove the old pages and point the menu at the new list**

Patch `2d2-task1-code.patch` (`web/Controllers/AdminController.cs`, `web/Controllers/RegistryController.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Models/ViewModels/BatteryViewModels.cs`, `web/Services/BatteryTableService.cs`, `web/Views/Admin/Clusters.cshtml`, `web/Views/Admin/Help.cshtml`, `web/Views/Shared/_AdminTabs.cshtml`). Save the block as `../2d2-task1-code.patch`, outside the worktree, and run `git apply ../2d2-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -18,170 +18,30 @@ namespace BatteryPassWeb.Controllers;
 [Route("admin")]
 public class AdminController : Controller
 {
-    private static readonly string[] DocumentKeys =
-    [
-        "conformityAssessment",
-        "euDeclarationOfConformity",
-        "sustainabilityReport",
-        "dueDiligenceReport",
-        "thirdPartyAudit",
-        "taxonomyReport",
-        "co2StudyReference"
-    ];
-
-    private static readonly IReadOnlyDictionary<string, string[]> BatteryFormFieldsByEditableKey =
-        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
-        {
-            ["general.clusterId"] = ["clusterId"],
-            ["general.product"] = ["productId"],
-            ["general.batteryFamily"] = ["productId"],
-            ["general.productVersion"] = ["productVersion", "batteryModel"],
-            ["general.batteryModel"] = ["productVersion", "batteryModel"],
-            ["general.serialNumber"] = ["serialNumber"],
-            ["general.batteryStatus"] = ["batteryStatus"],
-            ["general.batteryMass"] = ["batteryMass"],
-            ["general.manufacturingDate"] = ["manufacturingDate"],
-            ["general.manufacturedDate"] = ["manufacturingDate"],
-            ["general.facilityId"] = ["facilityId"],
-            ["general.manufacturerName"] = ["manufacturerName"],
-            ["general.manufacturedBy"] = ["manufacturerName"],
-            ["general.softwareVersion"] = ["softwareVersion"],
-            ["software.version"] = ["softwareVersion"],
-            ["material.nickelMass"] = ["materialNickel"],
-            ["material.copperMass"] = ["materialCopper"],
-            ["material.aluminiumMass"] = ["materialAluminium"],
-            ["material.graphiteMass"] = ["materialGraphite"],
-            ["material.manganeseMass"] = ["materialManganese"],
-            ["material.cobaltMass"] = ["materialCobalt"],
-            ["material.lithiumMass"] = ["materialLithium"],
-            ["material.electrolyteMass"] = ["materialElectrolyte"],
-            ["performance.ratedEnergy"] = ["ratedEnergy"],
-            ["performance.ratedCapacity"] = ["ratedCapacity"],
-            ["performance.ratedMaximumPower"] = ["ratedMaximumPower"],
-            ["performance.nominalVoltage"] = ["nominalVoltage"],
-            ["performance.expectedLifetime"] = ["expectedLifetime"],
-            ["performance.expectedNumberOfCycles"] = ["expectedNumberOfCycles"],
-            ["supplyChain.supplyChainIndex"] = ["supplyChainIndex"],
-            ["carbon.amount"] = ["carbonFootprint"],
-            ["carbon.performanceClass"] = ["performanceClass"],
-            ["carbon.rawMaterial"] = ["carbonRawMaterial"],
-            ["carbon.mainProduction"] = ["carbonMainProduction"],
-            ["carbon.distribution"] = ["carbonDistribution"],
-            ["carbon.recycling"] = ["carbonRecycling"],
-            ["circularity.separateCollection"] = ["separateCollection"],
-            ["circularity.wastePrevention"] = ["wastePrevention"],
-            ["circularity.recycledNickelPre"] = ["recycledNickelPre"],
-            ["circularity.recycledNickelPost"] = ["recycledNickelPost"],
-            ["circularity.recycledCobaltPre"] = ["recycledCobaltPre"],
-            ["circularity.recycledCobaltPost"] = ["recycledCobaltPost"],
-            ["circularity.recycledLithiumPre"] = ["recycledLithiumPre"],
-            ["circularity.recycledLithiumPost"] = ["recycledLithiumPost"],
-            ["circularity.recycledLeadPre"] = ["recycledLeadPre"],
-            ["circularity.recycledLeadPost"] = ["recycledLeadPost"]
-        };
-
-    private static readonly string[] AlwaysAllowedBatteryFormFields =
-    [
-        "stateOfCharge",
-        "remainingCapacity",
-        "remainingEnergy",
-        "fullCycles"
-    ];
-
-    private static readonly string[] AlwaysEditableOperationalFieldKeys =
-    [
-        "performance.stateOfCharge",
-        "performance.remainingCapacity",
-        "performance.remainingEnergy",
-        "performance.fullCycles"
-    ];
-
-    private const string SamplePassportId = "did:web:acme.battery.pass:sample-customer-north-001";
-    private const string TrustWorkflowServiceErrorMessage = "The trust workflow could not be completed because MongoDB/service persistence is unavailable. No signed or published trust state was claimed. Please retry after the service is healthy.";
-    private const string BatteryIdentityLockedMessage = "Battery Family and serial number are locked because they define the Battery ID.";
-    private const string BatteryNewPassportRequiredPath = "app.snapshot.newPassportRequired";
-
     private readonly PassportRepository _passportRepository;
-    private readonly BatteryRepository _batteryRepository;
-    private readonly BatteryTableService _batteryTableService;
-    private readonly BatteryIdService _batteryIdService;
-    private readonly BatteryPassportSnapshotService _batteryPassportSnapshotService;
-    private readonly BatteryPassportDeltaService _batteryPassportDeltaService;
-    private readonly BatteryTemplateUpdateService _batteryTemplateUpdateService;
-    private readonly BatteryCreationService _batteryCreationService;
     private readonly ClusterRepository _clusterRepository;
-    private readonly PassportViewModelFactory _viewModelFactory;
     private readonly ExternalApiRepository _externalApiRepository;
-    private readonly PassportValidationService _passportValidationService;
-    private readonly PassportPublishPolicyService _passportPublishPolicyService;
-    private readonly PassportReadinessService _passportReadinessService;
-    private readonly PassportEvidenceService _passportEvidenceService;
-    private readonly DataCompletionPolicyService _dataCompletionPolicyService;
-    private readonly EditableFieldPolicyService _editableFieldPolicyService;
-    private readonly ProductTemplateService _productTemplateService;
-    private readonly PassportTrustService _passportTrustService;
-    private readonly AuditRevisionService _auditRevisionService;
-    private readonly BatteryAuditService _batteryAuditService;
     private readonly ApplicationAuditService _applicationAuditService;
     private readonly ApplicationSettingsService _applicationSettingsService;
-    private readonly PassportTrustWorkflowService _passportTrustWorkflowService;
 
     public AdminController(
         PassportRepository passportRepository,
-        BatteryRepository batteryRepository,
-        BatteryTableService batteryTableService,
-        BatteryIdService batteryIdService,
-        BatteryPassportSnapshotService batteryPassportSnapshotService,
-        BatteryPassportDeltaService batteryPassportDeltaService,
-        BatteryTemplateUpdateService batteryTemplateUpdateService,
-        BatteryCreationService batteryCreationService,
         ClusterRepository clusterRepository,
-        PassportViewModelFactory viewModelFactory,
         ExternalApiRepository externalApiRepository,
-        PassportValidationService passportValidationService,
-        PassportPublishPolicyService passportPublishPolicyService,
-        PassportReadinessService passportReadinessService,
-        PassportEvidenceService passportEvidenceService,
-        DataCompletionPolicyService dataCompletionPolicyService,
-        EditableFieldPolicyService editableFieldPolicyService,
-        ProductTemplateService productTemplateService,
-        PassportTrustService passportTrustService,
-        AuditRevisionService auditRevisionService,
-        BatteryAuditService batteryAuditService,
         ApplicationAuditService applicationAuditService,
-        ApplicationSettingsService applicationSettingsService,
-        PassportTrustWorkflowService passportTrustWorkflowService)
+        ApplicationSettingsService applicationSettingsService)
     {
         _passportRepository = passportRepository;
-        _batteryRepository = batteryRepository;
-        _batteryTableService = batteryTableService;
-        _batteryIdService = batteryIdService;
-        _batteryPassportSnapshotService = batteryPassportSnapshotService;
-        _batteryPassportDeltaService = batteryPassportDeltaService;
-        _batteryTemplateUpdateService = batteryTemplateUpdateService;
-        _batteryCreationService = batteryCreationService;
         _clusterRepository = clusterRepository;
-        _viewModelFactory = viewModelFactory;
         _externalApiRepository = externalApiRepository;
-        _passportValidationService = passportValidationService;
-        _passportPublishPolicyService = passportPublishPolicyService;
-        _passportReadinessService = passportReadinessService;
-        _passportEvidenceService = passportEvidenceService;
-        _dataCompletionPolicyService = dataCompletionPolicyService;
-        _editableFieldPolicyService = editableFieldPolicyService;
-        _productTemplateService = productTemplateService;
-        _passportTrustService = passportTrustService;
-        _auditRevisionService = auditRevisionService;
-        _batteryAuditService = batteryAuditService;
         _applicationAuditService = applicationAuditService;
         _applicationSettingsService = applicationSettingsService;
-        _passportTrustWorkflowService = passportTrustWorkflowService;
     }
 
     [HttpGet("")]
     public IActionResult Index()
     {
-        return Redirect(AdminBatteriesUrl());
+        return Redirect("/admin/batteries");
     }
 
     [HttpGet("help")]
@@ -230,1029 +90,196 @@ public class AdminController : Controller
         });
     }
 
-    [HttpGet("passports")]
-    public async Task<IActionResult> Passports([FromQuery] string? q, CancellationToken cancellationToken)
-    {
-        var batteryTable = await _batteryTableService.BuildAsync(User, BatteryTableScope.AdminPassports, q, cancellationToken);
-        if (!string.IsNullOrWhiteSpace(batteryTable.RedirectPath))
-        {
-            return Redirect(batteryTable.RedirectPath);
-        }
-
-        return View(batteryTable);
-    }
-
-    [HttpGet("passports/new")]
-    public async Task<IActionResult> NewPassport([FromQuery] string? status, [FromQuery] string? error, [FromQuery] string? passportId, CancellationToken cancellationToken)
-    {
-        var draftPassportId = string.IsNullOrWhiteSpace(passportId)
-            ? $"did:web:acme.battery.pass:{Guid.NewGuid():N}"
-            : passportId.Trim();
-        var defaultProduct = BatteryProductTemplateCatalog.DefaultProduct;
-        var defaultProductVersion = defaultProduct.LatestProductVersion;
-        var document = await BuildDraftPassportDocumentAsync(
-            draftPassportId,
-            defaultProduct.ProductId,
-            defaultProductVersion.Version,
-            defaultProductVersion.SoftwareVersion,
-            cancellationToken);
-        var model = await BuildEditPassportModelAsync(
-            document,
-            "new",
-            status == "created" ? "Passport created." : string.Empty,
-            error,
-            cancellationToken);
-
-        return View("EditPassport", model);
-    }
-
-    [HttpGet("batteries/new")]
-    public async Task<IActionResult> NewBattery([FromQuery] string? error, CancellationToken cancellationToken)
-    {
-        var product = BatteryProductTemplateCatalog.DefaultProduct;
-        var productVersion = product.LatestProductVersion;
-        var now = DateTimeOffset.UtcNow.ToString("O");
-        var document = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
-            "new-battery",
-            product,
-            productVersion,
-            new ProductTemplateBatteryIdentity
-            {
-                ModelNumber = string.Empty,
-                SerialNumber = string.Empty,
-                DisplayName = string.Empty,
-                FacilityId = string.Empty,
-                ClusterId = string.Empty,
-                ManufacturingDate = now[..10]
-            },
-            now);
-        var model = await BuildEditPassportModelAsync(
-            document,
-            "new-battery",
-            string.Empty,
-            error,
-            cancellationToken);
-
-        return View("EditPassport", model);
-    }
-
-    [HttpGet("batteries/{batteryId}/edit")]
-    public async Task<IActionResult> EditBattery(string batteryId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
-    {
-        var battery = await _batteryRepository.GetByBatteryIdAsync(Uri.UnescapeDataString(batteryId), cancellationToken);
-        if (battery == null)
-        {
-            return NotFound();
-        }
-
-        var model = await BuildEditPassportModelAsync(
-            battery,
-            "battery-edit",
-            status ?? string.Empty,
-            error,
-            cancellationToken);
-
-        return View("EditPassport", model);
-    }
-
-    [HttpGet("batteries/id-preview")]
-    public async Task<IActionResult> PreviewBatteryId([FromQuery] string? productId, [FromQuery] string? serialNumber, CancellationToken cancellationToken)
-    {
-        if (string.IsNullOrWhiteSpace(serialNumber))
-        {
-            return BadRequest(new { message = "Battery serial number is required." });
-        }
-
-        var product = await _productTemplateService.GetProductAsync(productId ?? string.Empty, cancellationToken)
-            ?? BatteryProductTemplateCatalog.DefaultProduct;
-        var batteryId = _batteryIdService.CreateBatteryId(product.ProductName, serialNumber);
-        return Json(new
-        {
-            batteryId,
-            length = batteryId.Length
-        });
-    }
-
-    [HttpPost("batteries/create")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> CreateBattery(CancellationToken cancellationToken)
-    {
-        var form = Request.Form;
-        var productId = Text(form, "productId", BatteryProductTemplateCatalog.DefaultProductId);
-        var serialNumber = Text(form, "serialNumber");
-        var requestedBatteryModel = FirstNonEmpty(
-            Text(form, "batteryModel"),
-            Text(form, "productVersion"));
-        var now = DateTimeOffset.UtcNow.ToString("O");
-        var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
-        var filteredForm = ApplyBatteryEditableFormValues(form, editablePolicy, forCreation: true);
-
-        var result = await _batteryCreationService.CreateBatteryAsync(
-            new BatteryCreationCommand
-            {
-                ModelNumber = Text(form, "modelNumber"),
-                ProductId = productId,
-                BatteryModel = requestedBatteryModel,
-                ProductVersion = Text(form, "productVersion"),
-                SoftwareVersion = Text(form, "softwareVersion"),
-                SerialNumber = serialNumber,
-                ClusterId = Text(form, "clusterId"),
-                DisplayName = Text(form, "name"),
-                FacilityId = Text(form, "facilityId"),
-                ManufacturingDate = Text(form, "manufacturingDate", now[..10]),
-                CustomizeBatteryBeforeInsert = (battery, createNow) =>
-                {
-                    ApplyPassportForm(battery, filteredForm, createNow);
-                }
-            },
-            new BatteryCreationActor(CurrentActor(), "admin", "admin-ui"),
-            BatteryCreationClusterScope.Unrestricted,
-            cancellationToken);
-
-        if (!result.Success)
-        {
-            return await ReturnNewBatteryFormWithErrorAsync(form, result.Message, cancellationToken);
-        }
-
-        TempData["StatusMessage"] = $"Battery {result.BatteryId} created. Create the first passport when the data is ready.";
-        return Redirect("/admin/clusters?tab=batteries");
-    }
-
-    [HttpPost("batteries/{batteryId}/passports/create")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> CreateBatteryPassport(string batteryId, CancellationToken cancellationToken)
-    {
-        var battery = await _batteryRepository.GetByBatteryIdAsync(Uri.UnescapeDataString(batteryId), cancellationToken);
-        if (battery == null)
-        {
-            return NotFound();
-        }
-
-        if (!await CanCreateBatteryPassport(battery, cancellationToken))
-        {
-            TempData["ErrorMessage"] = "No new passport is needed for this battery because the latest snapshot still matches the battery data.";
-            return Redirect($"/admin/batteries/{Uri.EscapeDataString(BsonHelpers.GetString(battery, "batteryId"))}/passports?returnUrl={Uri.EscapeDataString("/admin/clusters?tab=batteries")}");
-        }
-
-        var passport = await _batteryPassportSnapshotService.CreatePassportSnapshotAsync(
-            battery,
-            CurrentActor(),
-            DateTimeOffset.UtcNow,
-            "admin",
-            "admin-ui",
-            cancellationToken: cancellationToken);
-        var passportId = BsonHelpers.GetString(passport, "passportId");
-        await _batteryPassportDeltaService.ClearNewPassportRequiredAsync(
-            BsonHelpers.GetString(battery, "batteryId"),
-            passportId,
-            cancellationToken);
-        TempData["StatusMessage"] = $"Passport {passportId} created for battery {BsonHelpers.GetString(battery, "batteryId")}.";
-        return Redirect($"/admin/batteries/{Uri.EscapeDataString(BsonHelpers.GetString(battery, "batteryId"))}/passports?returnUrl={Uri.EscapeDataString("/admin/clusters?tab=batteries")}");
-    }
-
-    [HttpPost("batteries/{batteryId}/save")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> SaveBattery(string batteryId, CancellationToken cancellationToken)
+    [HttpGet("clusters")]
+    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
     {
-        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
-        var battery = await _batteryRepository.GetByBatteryIdAsync(decodedBatteryId, cancellationToken);
-        if (battery == null)
-        {
-            return NotFound();
-        }
-
-        var beforeSave = battery.DeepClone().AsBsonDocument;
-        var productId = FirstNonEmpty(
-            BsonHelpers.GetString(battery, "identity", "productId"),
-            BsonHelpers.GetString(battery, "app", "product", "productId"),
-            BatteryProductTemplateCatalog.DefaultProductId);
-        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken)
-            ?? BatteryProductTemplateCatalog.DefaultProduct;
-        var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
-        var filteredForm = ApplyBatteryEditableFormValues(Request.Form, editablePolicy, forCreation: false);
-        var currentBatteryModel = FirstNonEmpty(
-            BsonHelpers.GetString(battery, "identity", "batteryModel"),
-            BsonHelpers.GetString(battery, "app", "product", "productVersion"),
-            product.LatestProductVersion.Version);
-        var currentSoftwareVersion = FirstNonEmpty(
-            BsonHelpers.GetString(battery, "identity", "softwareVersion"),
-            BsonHelpers.GetString(battery, "app", "product", "softwareVersion"));
-        var requestedBatteryModel = FirstNonEmpty(
-            Text(filteredForm, "batteryModel"),
-            Text(filteredForm, "productVersion"),
-            currentBatteryModel,
-            product.LatestProductVersion.Version);
-        var productVersion = product.ProductVersions.FirstOrDefault(version => version.Version.Equals(requestedBatteryModel, StringComparison.OrdinalIgnoreCase))
-            ?? product.LatestProductVersion;
-        var lockedSerialNumber = BsonHelpers.GetString(battery, "identity", "serialNumber");
-        var lockedManufacturerName = BsonHelpers.GetString(battery, "app", "display", "manufacturerName");
-        var now = DateTimeOffset.UtcNow.ToString("O");
-
-        ApplyPassportForm(battery, filteredForm, now);
-        var app = EnsureDocument(battery, "app");
-        var display = EnsureDocument(app, "display");
-        var productNode = EnsureDocument(app, "product");
-        display["serialNumber"] = lockedSerialNumber;
-        display["manufacturerName"] = lockedManufacturerName;
-        productNode["productId"] = product.ProductId;
-        productNode["productName"] = product.ProductName;
-        var compareAllPassportData = false;
-        if (!requestedBatteryModel.Equals(currentBatteryModel, StringComparison.OrdinalIgnoreCase))
-        {
-            var modelResult = await _batteryTemplateUpdateService.ApplyBatteryModelAsync(battery, requestedBatteryModel, cancellationToken);
-            if (!modelResult.Success)
-            {
-                return Redirect($"/admin/batteries/{Uri.EscapeDataString(decodedBatteryId)}/edit?error={Uri.EscapeDataString(modelResult.Message)}");
-            }
-
-            compareAllPassportData = true;
-        }
-
-        var softwareVersion = Text(filteredForm, "softwareVersion");
-        if (!string.IsNullOrWhiteSpace(softwareVersion))
-        {
-            if (!softwareVersion.Equals(currentSoftwareVersion, StringComparison.OrdinalIgnoreCase))
-            {
-                compareAllPassportData = true;
-            }
-
-            var softwareResult = await _batteryTemplateUpdateService.ApplySoftwareVersionAsync(battery, softwareVersion, cancellationToken);
-            if (!softwareResult.Success)
-            {
-                return Redirect($"/admin/batteries/{Uri.EscapeDataString(decodedBatteryId)}/edit?error={Uri.EscapeDataString(softwareResult.Message)}");
-            }
-        }
-
-        var postedClusterId = Text(filteredForm, "clusterId");
-        if (!string.IsNullOrWhiteSpace(postedClusterId))
+        // The batteries and their cluster assignments moved to the batteries pages (plan 2d-2).
+        if (NormalizeTab(tab) is "batteries" or "battery")
         {
-            battery["clusterId"] = ClusterRepository.NormalizeClusterId(postedClusterId);
+            return Redirect(string.IsNullOrWhiteSpace(q) ? "/admin/batteries" : $"/admin/batteries?q={Uri.EscapeDataString(q)}");
         }
-        battery["updatedAt"] = now;
 
-        if (compareAllPassportData)
-        {
-            await _batteryPassportDeltaService.UpdateNewPassportRequiredForPassportDataAsync(battery, cancellationToken);
-        }
-        else
-        {
-            await _batteryPassportDeltaService.UpdateNewPassportRequiredAsync(battery, cancellationToken);
-        }
-        var changeMetadata = AuditRevisionService.BuildChangeMetadata(beforeSave, battery, "batteryAdminSave");
-        if (changeMetadata.GetValue("changedFields", new BsonArray()) is BsonArray { Count: > 0 })
+        // The battery families moved to their own pages, and the attribute catalogue replaced the editable-fields tab.
+        if (tab?.Equals("products", StringComparison.OrdinalIgnoreCase) == true)
         {
-            changeMetadata["newPassportRequired"] = BsonHelpers.GetValue(battery, "app", "snapshot", "newPassportRequired") ?? BsonNull.Value;
-            changeMetadata["compareAllPassportData"] = compareAllPassportData;
-            await _batteryAuditService.AppendBatteryAuditEventAsync(
-                decodedBatteryId,
-                "battery.updated",
-                CurrentActor(),
-                "admin",
-                "admin-ui",
-                "Battery data updated from the admin form.",
-                changeMetadata,
-                cancellationToken: cancellationToken);
+            return Redirect("/admin/families");
         }
-        TempData["StatusMessage"] = "Battery data saved.";
-        return Redirect("/admin/clusters?tab=batteries");
-    }
 
-    [HttpGet("batteries/{batteryId}/passports")]
-    public async Task<IActionResult> BatteryPassports(string batteryId, [FromQuery] string? returnUrl, CancellationToken cancellationToken)
-    {
-        var decodedBatteryId = Uri.UnescapeDataString(batteryId);
-        var battery = await _batteryRepository.GetByBatteryIdAsync(decodedBatteryId, cancellationToken);
-        if (battery == null)
+        if (tab?.Equals("local-editable-fields", StringComparison.OrdinalIgnoreCase) == true)
         {
-            return NotFound();
+            return Redirect("/admin/clusters");
         }
 
-        var clusterDocuments = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = clusterDocuments
-            .Select(cluster => new
+        var selectedTab = NormalizeTab(tab);
+        var selectedCredentialTab = NormalizeCredentialTab(tab);
+        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
+        var clusterViewModels = clusters
+            .Select(cluster => new ClusterViewModel
             {
                 ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name")
+                Name = BsonHelpers.GetString(cluster, "name"),
+                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
+                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
             })
             .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
-
-        var passports = await _passportRepository.ListByBatteryIdAsync(decodedBatteryId, includeArchived: true, cancellationToken);
-        var batterySummary = _batteryRepository.ToSummary(
-            battery,
-            passports.Select(ToAdminHistoryRow).ToList(),
-            ResolveClusterLabel(battery, clusterNamesById));
-        var activePassports = passports
-            .Where(passport => !BsonHelpers.GetString(passport, "registryInfo", "status").Equals("archived", StringComparison.OrdinalIgnoreCase))
+            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
             .ToList();
 
-        var safeReturnUrl = SafeReturnUrl(returnUrl, "/admin/clusters?tab=batteries");
-        return View("BatteryPassports", new BatteryPassportHistoryPageViewModel
+        var clusterNamesById = clusterViewModels.ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
+        var needsUsers = selectedTab is "users" or "clusters";
+        var needsCredentials = selectedTab == "api-token-management";
+        IReadOnlyList<BsonDocument> users = needsUsers
+            ? await _clusterRepository.ListUsersAsync(cancellationToken)
+            : Array.Empty<BsonDocument>();
+        IReadOnlyList<BsonDocument> memberships = needsUsers
+            ? await _clusterRepository.ListClusterMembershipsAsync(cancellationToken)
+            : Array.Empty<BsonDocument>();
+        IReadOnlyList<BsonDocument> apiTokens = needsCredentials
+            ? await _externalApiRepository.ListTokensAsync(cancellationToken)
+            : Array.Empty<BsonDocument>();
+
+        var model = new AdminClusterViewModel
         {
-            Battery = batterySummary,
-            CanCreatePassport = CanCreateBatteryPassport(batterySummary, activePassports),
+            SelectedTab = selectedTab,
+            SelectedCredentialTab = selectedCredentialTab,
+            Clusters = clusterViewModels,
+            Users = users.Select(user => new UserViewModel
+            {
+                Email = BsonHelpers.GetString(user, "email"),
+                Name = BsonHelpers.GetString(user, "name"),
+                Roles = (user.GetValue("roles", new BsonArray()) as BsonArray ?? new BsonArray())
+                    .Select(role => role.ToString() ?? string.Empty)
+                    .Where(role => !string.IsNullOrWhiteSpace(role))
+                    .Select(AccessControlService.DisplayRoleLabel)
+                    .ToList()
+            }).ToList(),
+            Memberships = memberships.Select(membership => new ClusterMembershipViewModel
+            {
+                Email = BsonHelpers.GetString(membership, "email"),
+                ClusterId = BsonHelpers.GetString(membership, "clusterId"),
+                Role = BsonHelpers.GetString(membership, "role"),
+                CreatedAt = BsonHelpers.GetString(membership, "createdAt"),
+                UpdatedAt = BsonHelpers.GetString(membership, "updatedAt")
+            }).ToList(),
+            ApiTokens = apiTokens.Select(token =>
+            {
+                var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
+                var clusterNames = tokenClusterIds
+                    .Select(entry => entry.ToString() ?? string.Empty)
+                    .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
+                    .Select(clusterId => ClusterTokenScopeLabel(clusterId, clusterNamesById))
+                    .ToList();
+                return new ApiTokenViewModel
+                {
+                    TokenId = BsonHelpers.GetString(token, "tokenId"),
+                    Name = BsonHelpers.GetString(token, "name"),
+                    AccessMode = BsonHelpers.GetString(token, "accessMode"),
+                    GlobalAccess = token.GetValue("globalAccess", false).ToBoolean(),
+                    AllowUnassigned = token.GetValue("allowUnassigned", false).ToBoolean(),
+                    IsActive = token.GetValue("isActive", false).ToBoolean(),
+                    IsSample = token.GetValue("isSample", false).ToBoolean(),
+                    ClusterIdsLabel = clusterNames.Count == 0 ? "No clusters" : string.Join(", ", clusterNames),
+                    ClusterScopeNames = clusterNames,
+                    CreatedAt = BsonHelpers.GetString(token, "createdAt"),
+                    UpdatedAt = BsonHelpers.GetString(token, "updatedAt"),
+                    LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
+                };
+            }).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList(),
+            SamplePassportId = ExternalApiInitializer.SamplePassportId,
+            SampleReadToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadTokenId, ExternalApiInitializer.SampleReadTokenValue, cancellationToken),
+            SampleReadWriteToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadWriteTokenId, ExternalApiInitializer.SampleReadWriteTokenValue, cancellationToken),
             StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
             ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
-            ReturnUrl = safeReturnUrl,
-            ReturnLabel = safeReturnUrl.StartsWith("/registry", StringComparison.OrdinalIgnoreCase)
-                ? "Back to Registry"
-                : safeReturnUrl.StartsWith("/admin/passports", StringComparison.OrdinalIgnoreCase)
-                    ? "Back to Passports"
-                    : "Back to Batteries"
-        });
+            GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty,
+            SessionTimeoutMinutes = await _applicationSettingsService.GetSessionTimeoutMinutesAsync(cancellationToken)
+        };
+
+        return View(model);
     }
 
-    [HttpPost("passports/archive")]
+    [HttpPost("clusters/create")]
     [ValidateAntiForgeryToken]
-    public async Task<IActionResult> ArchivePassport(CancellationToken cancellationToken)
+    public async Task<IActionResult> CreateCluster(CancellationToken cancellationToken)
     {
-        var passportId = Text(Request.Form, "passportId");
-        if (!string.IsNullOrWhiteSpace(passportId))
+        var name = Text(Request.Form, "name");
+        var clusterId = Text(Request.Form, "clusterId");
+        if (string.IsNullOrWhiteSpace(name))
         {
-            await _passportRepository.ArchivePassportAsync(passportId, cancellationToken);
-            await _auditRevisionService.AppendAuditEventAsync(
-                passportId,
-                "passport.archived",
-                CurrentActor(),
-                "admin",
-                "admin-ui",
-                "Passport archived from the admin console.",
-                cancellationToken: cancellationToken);
+            return Redirect("/admin/clusters?tab=clusters");
         }
 
-        return Redirect(AdminBatteriesUrl());
-    }
-
-    [HttpPost("passports/unarchive")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> UnarchivePassport(CancellationToken cancellationToken)
-    {
-        var passportId = Text(Request.Form, "passportId");
-        if (!string.IsNullOrWhiteSpace(passportId))
+        if (string.IsNullOrWhiteSpace(clusterId))
         {
-            await _passportRepository.UnarchivePassportAsync(passportId, cancellationToken);
-            await _auditRevisionService.AppendAuditEventAsync(
-                passportId,
-                "passport.unarchived",
-                CurrentActor(),
-                "admin",
-                "admin-ui",
-                "Passport unarchived from the admin console.",
-                cancellationToken: cancellationToken);
+            clusterId = $"cluster-{Guid.NewGuid():N}";
         }
+        clusterId = ClusterRepository.NormalizeClusterId(clusterId);
 
-        return Redirect(AdminBatteriesUrl());
-    }
-
-    [HttpPost("passports/create")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> CreatePassport(CancellationToken cancellationToken)
-    {
-        var form = Request.Form;
-        var passportId = Text(form, "passportId");
-        if (string.IsNullOrWhiteSpace(passportId))
+        await _clusterRepository.EnsureIndexesAsync(cancellationToken);
+        if (await _clusterRepository.GetClusterByIdAsync(clusterId, cancellationToken) != null)
         {
-            return Redirect($"/admin/passports/new?error={Uri.EscapeDataString("Passport ID is required.")}");
+            TempData["ErrorMessage"] = "Cluster ID already exists.";
+            TempData["ClusterCreateName"] = name;
+            TempData["ClusterCreateId"] = clusterId;
+            return Redirect("/admin/clusters?tab=clusters");
         }
 
-        var existing = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (existing != null)
+        try
         {
-            return Redirect($"/admin/passports/new?passportId={Uri.EscapeDataString(passportId)}&error={Uri.EscapeDataString("Passport ID already exists.")}");
+            await _clusterRepository.UpsertClusterAsync(clusterId, name, cancellationToken);
         }
-
-        var document = await BuildDraftPassportDocumentAsync(
-            passportId,
-            Text(form, "productId", BatteryProductTemplateCatalog.DefaultProductId),
-            Text(form, "productVersion", BatteryProductTemplateCatalog.DefaultProduct.LatestProductVersion.Version),
-            Text(form, "softwareVersion", BatteryProductTemplateCatalog.DefaultSoftwareVersion),
-            cancellationToken);
-        var now = DateTime.UtcNow.ToString("O");
-        var requestedStatus = Text(form, "status", "draft");
-        ApplyPassportForm(document, form, now);
-        await ApplySelectedProductTemplateMetadataAsync(document, form, cancellationToken);
-        _passportPublishPolicyService.SanitizeTrustClaimsForDraftSave(document);
-        document["passportId"] = passportId;
-        document.Remove("_id");
-
-        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
-        var validationSummary = _passportValidationService.Validate(document, dataRequirements);
-        if (validationSummary.BlockingErrorCount > 0)
+        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
         {
-            var model = await BuildEditPassportModelAsync(
-                document,
-                "new",
-                string.Empty,
-                BuildCreateBlockedMessage(validationSummary),
-                cancellationToken);
-            return View("EditPassport", model);
+            TempData["ErrorMessage"] = "Cluster ID already exists.";
+            TempData["ClusterCreateName"] = name;
+            TempData["ClusterCreateId"] = clusterId;
+            return Redirect("/admin/clusters?tab=clusters");
         }
-
-        var normalizedStatus = _passportPublishPolicyService.NormalizeRegistryStatus(requestedStatus, document, validationSummary);
-        EnsureDocument(document, "registryInfo")["status"] = normalizedStatus;
-
-        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
-        await _passportRepository.UpdateTrustValidationAsync(passportId, validationSummary, cancellationToken);
-        await _auditRevisionService.AppendAuditEventAsync(
-            passportId,
-            "passport.created",
-            CurrentActor(),
-            "admin",
-            "admin-ui",
-            "Passport created from the admin form.",
+        await AppendAdminAuditAsync(
+            "cluster.created",
+            "Cluster created.",
             new BsonDocument
             {
-                ["batteryId"] = BsonHelpers.GetString(document, "batteryId"),
-                ["registryStatus"] = normalizedStatus,
-                ["blockingErrors"] = validationSummary.BlockingErrorCount,
-                ["warnings"] = validationSummary.WarningCount
+                ["clusterId"] = clusterId,
+                ["name"] = name
             },
+            "cluster",
+            clusterId,
             cancellationToken);
-        var blockedPublishMessage = BuildBlockedPublishMessage(requestedStatus, normalizedStatus);
-        if (!string.IsNullOrWhiteSpace(blockedPublishMessage))
-        {
-            TempData["ErrorMessage"] = blockedPublishMessage;
-        }
-
-        TempData["StatusMessage"] = $"Passport {passportId} created.";
-        return Redirect(AdminBatteriesUrl());
+        return Redirect("/admin/clusters?tab=clusters");
     }
 
-    [HttpGet("passports/{passportId}/edit")]
-    public async Task<IActionResult> EditPassport(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
+    [HttpPost("clusters/update")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> UpdateCluster(CancellationToken cancellationToken)
     {
-        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (document == null)
-        {
-            return NotFound();
-        }
-
-        var model = await BuildEditPassportModelAsync(
-            document,
-            "edit",
-            status switch
+        var clusterId = Text(Request.Form, "clusterId");
+        var name = Text(Request.Form, "name");
+        await _clusterRepository.UpdateClusterNameAsync(clusterId, name, cancellationToken);
+        await AppendAdminAuditAsync(
+            "cluster.updated",
+            "Cluster updated.",
+            new BsonDocument
             {
-                "saved" => "Passport changes saved.",
-                "created" => "Passport created.",
-                _ => string.Empty
+                ["clusterId"] = clusterId,
+                ["name"] = name
             },
-            error,
+            "cluster",
+            clusterId,
             cancellationToken);
-
-        return View(model);
+        return Redirect("/admin/clusters?tab=clusters");
     }
 
-    [HttpPost("passports/save")]
+    [HttpPost("clusters/delete")]
     [ValidateAntiForgeryToken]
-    public async Task<IActionResult> SavePassport(CancellationToken cancellationToken)
+    public async Task<IActionResult> DeleteCluster(CancellationToken cancellationToken)
     {
-        var form = Request.Form;
-        var passportId = Text(form, "passportId");
-        if (string.IsNullOrWhiteSpace(passportId))
+        var clusterId = Text(Request.Form, "clusterId");
+        var passportCount = await _passportRepository.CountPassportsByClusterAsync(clusterId, cancellationToken);
+        var membershipCount = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken);
+        if (passportCount > 0 || membershipCount > 0)
         {
-            return Redirect(AdminBatteriesUrl());
-        }
-
-        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (document == null)
-        {
-            return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/edit?error={Uri.EscapeDataString("Passport not found.")}");
-        }
-
-        var beforeSave = document.DeepClone().AsBsonDocument;
-        var now = DateTime.UtcNow.ToString("O");
-        var requestedStatus = Text(form, "status", BsonHelpers.GetString(document, "registryInfo", "status"));
-        ApplyPassportForm(document, form, now);
-        await ApplySelectedProductTemplateMetadataAsync(document, form, cancellationToken);
-        _passportPublishPolicyService.InvalidateValidationClaimForDraftSave(document);
-        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
-        var validationSummary = _passportValidationService.Validate(document, dataRequirements);
-        var normalizedStatus = _passportPublishPolicyService.NormalizeRegistryStatus(requestedStatus, document, validationSummary);
-        EnsureDocument(document, "registryInfo")["status"] = normalizedStatus;
-
-        await _passportRepository.ReplaceAsync(passportId, document, cancellationToken);
-        await _passportRepository.MarkCanonicalDirtyAsync(passportId, "adminPassportSave", cancellationToken);
-        var changeMetadata = AuditRevisionService.BuildChangeMetadata(beforeSave, document, "adminPassportSave");
-        var changedFields = changeMetadata.GetValue("changedFields", new BsonArray());
-        changeMetadata["requestedStatus"] = requestedStatus;
-        changeMetadata["normalizedStatus"] = normalizedStatus;
-        changeMetadata["blockingErrors"] = validationSummary.BlockingErrorCount;
-        changeMetadata["warnings"] = validationSummary.WarningCount;
-        if (changedFields is BsonArray { Count: > 0 })
-        {
-            await _auditRevisionService.AppendAuditEventAsync(
-                passportId,
-                "passport.updated",
-                CurrentActor(),
-                "admin",
-                "admin-edit-form",
-                "Passport data updated from the admin form.",
-                changeMetadata,
-                cancellationToken);
-        }
-
-        TempData["StatusMessage"] = $"Passport {passportId} saved.";
-        var blockedPublishMessage = BuildBlockedPublishMessage(requestedStatus, normalizedStatus);
-        if (!string.IsNullOrWhiteSpace(blockedPublishMessage))
-        {
-            TempData["ErrorMessage"] = blockedPublishMessage;
-        }
-
-        return Redirect(AdminBatteriesUrl());
-    }
-
-    [HttpGet("passports/{passportId}/conformance")]
-    public async Task<IActionResult> Conformance(string passportId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
-    {
-        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (document == null)
-        {
-            return NotFound();
-        }
-
-        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = BuildClusterDictionary(clusters);
-        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
-        var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
-        var summary = validation.Summary;
-        var evidencePack = validation.EvidencePack;
-        var publishDecision = _passportPublishPolicyService.Evaluate(document, summary);
-        var verificationResult = _passportTrustService.Verify(document);
-        var readiness = _passportReadinessService.Evaluate(document, summary, publishDecision, verificationResult);
-        var groupedBlockingIssues = BuildIssueGroups(summary, TrustValidationSeverity.BlockingError);
-        var groupedWarningIssues = BuildIssueGroups(summary, TrustValidationSeverity.Warning);
-
-        return View(new ConformanceViewModel
-        {
-            Passport = _viewModelFactory.Create(document, clusterNamesById, verificationResult),
-            ValidationSummary = summary,
-            Readiness = readiness,
-            EvidencePack = evidencePack,
-            GroupedBlockingIssues = groupedBlockingIssues,
-            GroupedWarningIssues = groupedWarningIssues,
-            CanSign = publishDecision.CanSign,
-            CanPublish = publishDecision.CanPublish,
-            PublishBlockReason = publishDecision.PublishBlockReason,
-            VerificationResult = verificationResult,
-            StatusMessage = status switch
-            {
-                "validated" => "Passport validation completed.",
-                "signed" => "Passport signed and immutable revision recorded.",
-                "published" => "Passport published from the latest verified revision.",
-                _ => string.Empty
-            },
-            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
-        });
-    }
-
-    [HttpPost("passports/{passportId}/validate")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> ValidatePassport(string passportId, CancellationToken cancellationToken)
-    {
-        var result = await _passportTrustWorkflowService.ValidateAsync(passportId, CurrentActor(), "admin-ui", cancellationToken);
-        if (!result.Success && result.ValidationSummary == null)
-        {
-            return NotFound();
-        }
-
-        return Redirect($"/admin/passports/{Uri.EscapeDataString(passportId)}/conformance?status=validated");
-    }
-
-    [HttpPost("passports/{passportId}/sign")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> SignPassport(string passportId, CancellationToken cancellationToken)
-    {
-        try
-        {
-            var result = await _passportTrustWorkflowService.SignAsync(passportId, CurrentActor(), "admin-ui", cancellationToken);
-            if (!result.Success && result.ValidationSummary == null)
-            {
-                return NotFound();
-            }
-
-            if (!result.Success)
-            {
-                return Redirect(BuildConformanceRedirect(passportId, error: result.Message));
-            }
-
-            return Redirect(BuildConformanceRedirect(passportId, status: result.AutoPublished ? "published" : "signed"));
-        }
-        catch (Exception exception) when (IsTrustPersistenceFailure(exception))
-        {
-            return Redirect(BuildConformanceRedirect(passportId, error: $"{TrustWorkflowServiceErrorMessage} {exception.Message}"));
-        }
-    }
-
-    [HttpPost("passports/{passportId}/publish")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> PublishPassport(string passportId, CancellationToken cancellationToken)
-    {
-        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (document == null)
-        {
-            return NotFound();
-        }
-
-        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
-        var validation = await ValidateWithEvidenceAsync(passportId, document, dataRequirements, cancellationToken);
-        var summary = validation.Summary;
-        if (!_passportPublishPolicyService.CanSign(summary))
-        {
-            await _passportRepository.UpdateTrustValidationAsync(passportId, summary, cancellationToken);
-            return Redirect(BuildConformanceRedirect(passportId, error: "Resolve blocking validation errors before publishing."));
-        }
-
-        var verification = _passportTrustService.Verify(document);
-        if (!verification.IsValid)
-        {
-            await _auditRevisionService.AppendAuditEventAsync(
-                passportId,
-                "passport.publish.blocked",
-                CurrentActor(),
-                "admin",
-                "admin-ui",
-                "Passport publishing blocked by signature verification.",
-                new BsonDocument
-                {
-                    ["state"] = verification.State,
-                    ["message"] = verification.Message,
-                    ["currentHash"] = verification.CurrentHash,
-                    ["expectedHash"] = verification.ExpectedHash
-                },
-                cancellationToken);
-            return Redirect(BuildConformanceRedirect(passportId, error: verification.Message));
-        }
-
-        var revisionId = BsonHelpers.GetString(document, "trust", "latestRevisionId");
-        if (string.IsNullOrWhiteSpace(revisionId))
-        {
-            return Redirect(BuildConformanceRedirect(passportId, error: "Publish requires a signed revision."));
-        }
-
-        var actor = CurrentActor();
-        var publishedAt = DateTimeOffset.UtcNow.ToString("O");
-        var publishedProof = BsonHelpers.GetValue(document, "trust", "latestProof") as BsonDocument ?? new BsonDocument();
-        try
-        {
-            var published = await _passportRepository.PublishPassportAsync(
-                passportId,
-                revisionId,
-                publishedAt,
-                verification.CurrentHash,
-                publishedProof,
-                cancellationToken);
-            if (!published)
-            {
-                throw new InvalidOperationException("Publishing service could not persist the published trust state.");
-            }
-
-            var revisionMarked = await _auditRevisionService.MarkRevisionPublishedAsync(revisionId, publishedAt, cancellationToken);
-            if (!revisionMarked)
-            {
-                throw new InvalidOperationException("Publishing service could not mark the immutable revision as published.");
-            }
-
-            await _auditRevisionService.AppendAuditEventAsync(
-                passportId,
-                "passport.published",
-                actor,
-                "admin",
-                "admin-ui",
-                "Passport published.",
-                new BsonDocument
-                {
-                    ["revisionId"] = revisionId,
-                    ["hash"] = verification.CurrentHash,
-                    ["publishedAt"] = publishedAt
-                },
-                cancellationToken);
-
-            return Redirect(BuildConformanceRedirect(passportId, status: "published"));
-        }
-        catch (Exception exception) when (IsTrustPersistenceFailure(exception))
-        {
-            return Redirect(BuildConformanceRedirect(passportId, error: $"{TrustWorkflowServiceErrorMessage} {exception.Message}"));
-        }
-    }
-
-    [HttpGet("passports/{passportId}/audit")]
-    public async Task<IActionResult> Audit(string passportId, CancellationToken cancellationToken)
-    {
-        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (document == null)
-        {
-            return NotFound();
-        }
-
-        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = BuildClusterDictionary(clusters);
-        var auditEvents = await _applicationAuditService.ListPassportTimelineEventsAsync(
-            passportId,
-            BsonHelpers.GetString(document, "batteryId"),
-            cancellationToken);
-        return View("Audit", new PassportAuditTrailViewModel
-        {
-            Passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document)),
-            AuditEvents = auditEvents
-        });
-    }
-
-    [HttpGet("passports/{passportId}/revisions")]
-    public async Task<IActionResult> Revisions(string passportId, CancellationToken cancellationToken)
-    {
-        var document = await _passportRepository.GetByPassportIdAsync(passportId, cancellationToken);
-        if (document == null)
-        {
-            return NotFound();
-        }
-
-        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = BuildClusterDictionary(clusters);
-        var revisions = await _auditRevisionService.ListRevisionsAsync(passportId, cancellationToken);
-        return View("Revisions", new PassportRevisionHistoryViewModel
-        {
-            Passport = _viewModelFactory.Create(document, clusterNamesById, _passportTrustService.Verify(document)),
-            Revisions = revisions
-        });
-    }
-
-    [HttpGet("clusters")]
-    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
-    {
-        if (tab?.Equals("passports", StringComparison.OrdinalIgnoreCase) == true)
-        {
-            return Redirect(AdminBatteriesUrl(q));
-        }
-
-        // The battery families moved to their own pages, and the attribute catalogue replaced the editable-fields tab.
-        if (tab?.Equals("products", StringComparison.OrdinalIgnoreCase) == true)
-        {
-            return Redirect("/admin/families");
-        }
-
-        if (tab?.Equals("local-editable-fields", StringComparison.OrdinalIgnoreCase) == true)
-        {
-            return Redirect("/admin/clusters");
-        }
-
-        var selectedTab = NormalizeTab(tab);
-        var selectedCredentialTab = NormalizeCredentialTab(tab);
-        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterViewModels = clusters
-            .Select(cluster => new ClusterViewModel
-            {
-                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name"),
-                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
-                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
-            })
-            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
-            .ToList();
-
-        var clusterNamesById = clusterViewModels.ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
-        var needsBatteries = selectedTab == "batteries";
-        var needsPassports = selectedTab is "battery" or "api-token-management";
-        var needsUsers = selectedTab is "users" or "clusters";
-        var needsCredentials = selectedTab == "api-token-management";
-        var batteryTable = needsBatteries
-            ? await _batteryTableService.BuildAsync(User, BatteryTableScope.AdminBatteries, q, cancellationToken)
-            : new BatteryTablePageViewModel();
-        if (needsBatteries && !string.IsNullOrWhiteSpace(batteryTable.RedirectPath))
-        {
-            return Redirect(batteryTable.RedirectPath);
-        }
-
-        IReadOnlyList<PassportSummaryViewModel> passports = needsPassports
-            ? await _passportRepository.SearchAsync(q ?? string.Empty, includeArchived: true, cancellationToken)
-            : Array.Empty<PassportSummaryViewModel>();
-        IReadOnlyList<BsonDocument> users = needsUsers
-            ? await _clusterRepository.ListUsersAsync(cancellationToken)
-            : Array.Empty<BsonDocument>();
-        IReadOnlyList<BsonDocument> memberships = needsUsers
-            ? await _clusterRepository.ListClusterMembershipsAsync(cancellationToken)
-            : Array.Empty<BsonDocument>();
-        IReadOnlyList<BsonDocument> apiTokens = needsCredentials
-            ? await _externalApiRepository.ListTokensAsync(cancellationToken)
-            : Array.Empty<BsonDocument>();
-
-        var model = new AdminClusterViewModel
-        {
-            SelectedTab = selectedTab,
-            SelectedCredentialTab = selectedCredentialTab,
-            PassportsQuery = q ?? string.Empty,
-            Clusters = clusterViewModels,
-            BatteryTable = batteryTable,
-            Batteries = batteryTable.Rows,
-            Passports = passports
-                .Select(passport => new PassportSummaryViewModel
-                {
-                    PassportId = passport.PassportId,
-                    BatteryId = string.IsNullOrWhiteSpace(passport.BatteryId) ? passport.PassportId : passport.BatteryId,
-                    DisplayName = passport.DisplayName,
-                    ModelNumber = passport.ModelNumber,
-                    ManufacturerName = passport.ManufacturerName,
-                    SerialNumber = passport.SerialNumber,
-                    RegistryStatus = passport.RegistryStatus,
-                    BatteryFamily = passport.BatteryFamily,
-                    BatteryVersion = passport.BatteryVersion,
-                    BatterySerialNumber = passport.BatterySerialNumber,
-                    PassportStatus = passport.PassportStatus,
-                    ClusterId = passport.ClusterId,
-                    ClusterLabel = string.IsNullOrWhiteSpace(passport.ClusterId)
-                        ? "No cluster assigned"
-                        : clusterNamesById.TryGetValue(passport.ClusterId, out var clusterName)
-                            ? clusterName
-                            : passport.ClusterId,
-                    BatteryImageUrl = passport.BatteryImageUrl,
-                    UpdatedDate = passport.UpdatedDate
-                })
-                .OrderBy(passport => passport.DisplayName, StringComparer.OrdinalIgnoreCase)
-                .ToList(),
-            Users = users.Select(user => new UserViewModel
-            {
-                Email = BsonHelpers.GetString(user, "email"),
-                Name = BsonHelpers.GetString(user, "name"),
-                Roles = (user.GetValue("roles", new BsonArray()) as BsonArray ?? new BsonArray())
-                    .Select(role => role.ToString() ?? string.Empty)
-                    .Where(role => !string.IsNullOrWhiteSpace(role))
-                    .Select(AccessControlService.DisplayRoleLabel)
-                    .ToList()
-            }).ToList(),
-            Memberships = memberships.Select(membership => new ClusterMembershipViewModel
-            {
-                Email = BsonHelpers.GetString(membership, "email"),
-                ClusterId = BsonHelpers.GetString(membership, "clusterId"),
-                Role = BsonHelpers.GetString(membership, "role"),
-                CreatedAt = BsonHelpers.GetString(membership, "createdAt"),
-                UpdatedAt = BsonHelpers.GetString(membership, "updatedAt")
-            }).ToList(),
-            ApiTokens = apiTokens.Select(token =>
-            {
-                var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
-                var clusterNames = tokenClusterIds
-                    .Select(entry => entry.ToString() ?? string.Empty)
-                    .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
-                    .Select(clusterId => ClusterTokenScopeLabel(clusterId, clusterNamesById))
-                    .ToList();
-                return new ApiTokenViewModel
-                {
-                    TokenId = BsonHelpers.GetString(token, "tokenId"),
-                    Name = BsonHelpers.GetString(token, "name"),
-                    AccessMode = BsonHelpers.GetString(token, "accessMode"),
-                    GlobalAccess = token.GetValue("globalAccess", false).ToBoolean(),
-                    AllowUnassigned = token.GetValue("allowUnassigned", false).ToBoolean(),
-                    IsActive = token.GetValue("isActive", false).ToBoolean(),
-                    IsSample = token.GetValue("isSample", false).ToBoolean(),
-                    ClusterIdsLabel = clusterNames.Count == 0 ? "No clusters" : string.Join(", ", clusterNames),
-                    ClusterScopeNames = clusterNames,
-                    CreatedAt = BsonHelpers.GetString(token, "createdAt"),
-                    UpdatedAt = BsonHelpers.GetString(token, "updatedAt"),
-                    LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
-                };
-            }).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList(),
-            SamplePassportId = ExternalApiInitializer.SamplePassportId,
-            SampleReadToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadTokenId, ExternalApiInitializer.SampleReadTokenValue, cancellationToken),
-            SampleReadWriteToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadWriteTokenId, ExternalApiInitializer.SampleReadWriteTokenValue, cancellationToken),
-            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
-            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
-            GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty,
-            SessionTimeoutMinutes = await _applicationSettingsService.GetSessionTimeoutMinutesAsync(cancellationToken)
-        };
-
-        return View(model);
-    }
-
-    private async Task<IReadOnlyList<BatterySummaryViewModel>> BuildAdminBatteryRowsAsync(
-        IReadOnlyList<BsonDocument> batteryDocuments,
-        IReadOnlyDictionary<string, string> clusterNamesById,
-        CancellationToken cancellationToken)
-    {
-        var rows = new List<BatterySummaryViewModel>();
-        foreach (var battery in batteryDocuments)
-        {
-            var batteryId = BsonHelpers.GetString(battery, "batteryId");
-            var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: true, cancellationToken);
-            var history = passports.Select(ToAdminHistoryRow).ToList();
-            rows.Add(_batteryRepository.ToSummary(battery, history, ResolveClusterLabel(battery, clusterNamesById)));
-        }
-
-        return rows
-            .OrderBy(row => row.BatteryFamily, StringComparer.OrdinalIgnoreCase)
-            .ThenBy(row => row.BatterySerialNumber, StringComparer.OrdinalIgnoreCase)
-            .ToList();
-    }
-
-    private BatteryPassportHistoryRowViewModel ToAdminHistoryRow(BsonDocument passport)
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
-    private static string ResolveClusterLabel(BsonDocument battery, IReadOnlyDictionary<string, string> clusterNamesById)
-    {
-        var clusterId = BsonHelpers.GetString(battery, "clusterId");
-        if (string.IsNullOrWhiteSpace(clusterId))
-        {
-            return "No cluster assigned";
-        }
-
-        return clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
-            ? clusterName
-            : clusterId;
-    }
-
-    [HttpPost("clusters/create")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> CreateCluster(CancellationToken cancellationToken)
-    {
-        var name = Text(Request.Form, "name");
-        var clusterId = Text(Request.Form, "clusterId");
-        if (string.IsNullOrWhiteSpace(name))
-        {
-            return Redirect("/admin/clusters?tab=clusters");
-        }
-
-        if (string.IsNullOrWhiteSpace(clusterId))
-        {
-            clusterId = $"cluster-{Guid.NewGuid():N}";
-        }
-        clusterId = ClusterRepository.NormalizeClusterId(clusterId);
-
-        await _clusterRepository.EnsureIndexesAsync(cancellationToken);
-        if (await _clusterRepository.GetClusterByIdAsync(clusterId, cancellationToken) != null)
-        {
-            TempData["ErrorMessage"] = "Cluster ID already exists.";
-            TempData["ClusterCreateName"] = name;
-            TempData["ClusterCreateId"] = clusterId;
-            return Redirect("/admin/clusters?tab=clusters");
-        }
-
-        try
-        {
-            await _clusterRepository.UpsertClusterAsync(clusterId, name, cancellationToken);
-        }
-        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
-        {
-            TempData["ErrorMessage"] = "Cluster ID already exists.";
-            TempData["ClusterCreateName"] = name;
-            TempData["ClusterCreateId"] = clusterId;
-            return Redirect("/admin/clusters?tab=clusters");
-        }
-        await AppendAdminAuditAsync(
-            "cluster.created",
-            "Cluster created.",
-            new BsonDocument
-            {
-                ["clusterId"] = clusterId,
-                ["name"] = name
-            },
-            "cluster",
-            clusterId,
-            cancellationToken);
-        return Redirect("/admin/clusters?tab=clusters");
-    }
-
-    [HttpPost("clusters/update")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> UpdateCluster(CancellationToken cancellationToken)
-    {
-        var clusterId = Text(Request.Form, "clusterId");
-        var name = Text(Request.Form, "name");
-        await _clusterRepository.UpdateClusterNameAsync(clusterId, name, cancellationToken);
-        await AppendAdminAuditAsync(
-            "cluster.updated",
-            "Cluster updated.",
-            new BsonDocument
-            {
-                ["clusterId"] = clusterId,
-                ["name"] = name
-            },
-            "cluster",
-            clusterId,
-            cancellationToken);
-        return Redirect("/admin/clusters?tab=clusters");
-    }
-
-    [HttpPost("clusters/delete")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> DeleteCluster(CancellationToken cancellationToken)
-    {
-        var clusterId = Text(Request.Form, "clusterId");
-        var passportCount = await _passportRepository.CountPassportsByClusterAsync(clusterId, cancellationToken);
-        var membershipCount = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken);
-        if (passportCount > 0 || membershipCount > 0)
-        {
-            TempData["ErrorMessage"] = $"Cluster {clusterId} has {passportCount} linked passport(s) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
-            return Redirect("/admin/clusters?tab=clusters");
+            TempData["ErrorMessage"] = $"Cluster {clusterId} has {passportCount} linked passport(s) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
+            return Redirect("/admin/clusters?tab=clusters");
         }
 
         await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
@@ -1294,27 +321,6 @@ public class AdminController : Controller
         return Redirect("/admin/clusters?tab=clusters");
     }
 
-    [HttpPost("clusters/assign-passport")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> AssignPassportCluster(CancellationToken cancellationToken)
-    {
-        var passportId = Text(Request.Form, "passportId");
-        var clusterId = Text(Request.Form, "clusterId");
-        await _passportRepository.UpdatePassportClusterAsync(passportId, clusterId, cancellationToken);
-        await AppendAdminAuditAsync(
-            "cluster.passport.assigned",
-            "Passport assigned to cluster.",
-            new BsonDocument
-            {
-                ["passportId"] = passportId,
-                ["clusterId"] = clusterId
-            },
-            "cluster",
-            clusterId,
-            cancellationToken);
-        return Redirect("/admin/clusters?tab=battery");
-    }
-
     [HttpPost("clusters/assign-user")]
     [ValidateAntiForgeryToken]
     public async Task<IActionResult> AssignUserMembership(CancellationToken cancellationToken)
@@ -1784,573 +790,112 @@ public class AdminController : Controller
         IReadOnlyList<BsonDocument> tokens,
         IReadOnlyDictionary<string, string> clusterNamesById)
     {
-        var candidates = tokens
-            .Select(token => ToGeneratedClusterTokenCandidate(token, clusterNamesById))
-            .Where(candidate => candidate != null)
-            .Select(candidate => candidate!)
-            .ToList();
-
-        return candidates
-            .GroupBy(candidate => $"{candidate.ClusterId}\u001f{candidate.AccessMode}", StringComparer.OrdinalIgnoreCase)
-            .SelectMany(group =>
-            {
-                var ordered = group
-                    .OrderByDescending(candidate => candidate.HasUsage)
-                    .ThenByDescending(candidate => candidate.HasAutoClusterId)
-                    .ThenByDescending(candidate => candidate.UpdatedAt, StringComparer.Ordinal)
-                    .ToList();
-                return ordered
-                    .Skip(1)
-                    .Where(candidate => !candidate.HasUsage)
-                    .Select(candidate => candidate.TokenId);
-            })
-            .ToList();
-    }
-
-    private static GeneratedClusterTokenCandidate? ToGeneratedClusterTokenCandidate(
-        BsonDocument token,
-        IReadOnlyDictionary<string, string> clusterNamesById)
-    {
-        if (token.GetValue("isSample", false).ToBoolean()
-            || token.GetValue("globalAccess", false).ToBoolean()
-            || token.GetValue("allowUnassigned", false).ToBoolean())
-        {
-            return null;
-        }
-
-        var accessMode = BsonHelpers.GetString(token, "accessMode");
-        if (!accessMode.Equals("read", StringComparison.OrdinalIgnoreCase)
-            && !accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
-            && !accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase))
-        {
-            return null;
-        }
-
-        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
-        var clusterIds = tokenClusterIds
-            .Select(entry => entry.ToString() ?? string.Empty)
-            .Where(value => !string.IsNullOrWhiteSpace(value))
-            .ToList();
-        if (clusterIds.Count != 1)
-        {
-            return null;
-        }
-
-        var clusterId = clusterIds[0];
-        var autoClusterId = BsonHelpers.GetString(token, "autoClusterId");
-        var clusterName = ClusterTokenScopeLabel(clusterId, clusterNamesById);
-        var tokenName = BsonHelpers.GetString(token, "name");
-        if (!autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase)
-            && !GeneratedClusterTokenNameMatches(tokenName, clusterName, accessMode))
-        {
-            return null;
-        }
-
-        var tokenId = BsonHelpers.GetString(token, "tokenId");
-        if (string.IsNullOrWhiteSpace(tokenId))
-        {
-            return null;
-        }
-
-        return new GeneratedClusterTokenCandidate(
-            tokenId,
-            clusterId,
-            accessMode,
-            HasUsage: !string.IsNullOrWhiteSpace(BsonHelpers.GetString(token, "lastUsedAt")),
-            HasAutoClusterId: autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase),
-            UpdatedAt: BsonHelpers.GetString(token, "updatedAt"));
-    }
-
-    private static bool GeneratedClusterTokenNameMatches(string tokenName, string clusterName, string accessMode)
-    {
-        var expectedSuffix = accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
-            ? "readwritesign"
-            : accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
-                ? "readwrite"
-                : "read";
-        return tokenName.Equals($"{clusterName} - {expectedSuffix}", StringComparison.OrdinalIgnoreCase);
-    }
-
-    private static string AdminBatteriesUrl(string? q = null)
-    {
-        var queryPart = string.IsNullOrWhiteSpace(q) ? string.Empty : $"&q={Uri.EscapeDataString(q)}";
-        return $"/admin/clusters?tab=batteries{queryPart}";
-    }
-
-    private static string SafeReturnUrl(string? returnUrl, string fallback)
-    {
-        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/", StringComparison.Ordinal))
-        {
-            return fallback;
-        }
-
-        if (returnUrl.StartsWith("//", StringComparison.Ordinal)
-            || returnUrl.Contains("://", StringComparison.Ordinal))
-        {
-            return fallback;
-        }
-
-        return returnUrl;
-    }
-
-    private static string NormalizeCredentialTab(string? value)
-    {
-        return value?.ToLowerInvariant() switch
-        {
-            _ => "api-tokens"
-        };
-    }
-
-    private async Task<bool> CanCreateBatteryPassport(BsonDocument battery, CancellationToken cancellationToken)
-    {
-        var batteryId = BsonHelpers.GetString(battery, "batteryId");
-        var passports = await _passportRepository.ListByBatteryIdAsync(batteryId, includeArchived: false, cancellationToken);
-        return CanCreateBatteryPassport(
-            _batteryRepository.ToSummary(battery, passports.Select(ToAdminHistoryRow).ToList(), string.Empty),
-            passports);
-    }
-
-    private static bool CanCreateBatteryPassport(BatterySummaryViewModel battery, IReadOnlyList<BsonDocument> passports)
-    {
-        if (passports.Count == 0 || battery.PassportCount == 0)
-        {
-            return true;
-        }
-
-        var latest = passports.FirstOrDefault(passport => passport.GetValue("isLatestForBattery", false).ToBoolean())
-            ?? passports.FirstOrDefault();
-        return battery.NewPassportRequired && latest != null && !IsDraftPassport(latest);
-    }
-
-    private static bool IsDraftPassport(BsonDocument passport)
-    {
-        var status = BsonHelpers.GetString(passport, "registryInfo", "status");
-        return status.Contains("draft", StringComparison.OrdinalIgnoreCase)
-            || status.Contains("awaiting", StringComparison.OrdinalIgnoreCase);
-    }
-
-    private static string BuildBlockedPublishMessage(string requestedStatus, string normalizedStatus)
-    {
-        return requestedStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
-            && !normalizedStatus.Equals("published", StringComparison.OrdinalIgnoreCase)
-                ? "Draft saved. Publishing is blocked until validation passes and a current signature proof exists."
-                : string.Empty;
-    }
-
-    private static string BuildCreateBlockedMessage(TrustValidationSummary summary)
-    {
-        var blockers = summary.Sections
-            .SelectMany(section => section.Issues)
-            .Where(issue => issue.Severity == TrustValidationSeverity.BlockingError)
-            .Select(issue => string.IsNullOrWhiteSpace(issue.Path)
-                ? issue.Message
-                : $"{issue.Path}: {issue.Message}")
-            .Distinct(StringComparer.OrdinalIgnoreCase)
-            .Take(5)
-            .ToList();
-
-        var details = blockers.Count == 0
-            ? string.Empty
-            : $" Missing or invalid: {string.Join("; ", blockers)}";
-        return $"Create passport is blocked until {summary.BlockingErrorCount} required or invalid field(s) are fixed.{details}";
-    }
-
-    private async Task<(TrustValidationSummary Summary, EvidencePackResult EvidencePack)> ValidateWithEvidenceAsync(
-        string passportId,
-        BsonDocument document,
-        DataCompletionPolicySnapshot dataRequirements,
-        CancellationToken cancellationToken)
-    {
-        var summary = _passportValidationService.Validate(document, dataRequirements);
-        var latestRevision = await _auditRevisionService.GetLatestSignedRevisionAsync(passportId, cancellationToken);
-        var evidencePack = _passportEvidenceService.Evaluate(document, latestRevision, dataRequirements);
-        summary = PassportEvidenceService.AppendValidationSection(summary, evidencePack);
-        return (summary, evidencePack);
-    }
-
-    private static string BuildConformanceRedirect(string passportId, string status = "", string error = "")
-    {
-        var url = $"/admin/passports/{Uri.EscapeDataString(passportId)}/conformance";
-        if (!string.IsNullOrWhiteSpace(status))
-        {
-            return $"{url}?status={Uri.EscapeDataString(status)}";
-        }
-
-        if (!string.IsNullOrWhiteSpace(error))
-        {
-            return $"{url}?error={Uri.EscapeDataString(error)}";
-        }
-
-        return url;
-    }
-
-    private static bool IsTrustPersistenceFailure(Exception exception)
-    {
-        return exception is InvalidOperationException or MongoException or TimeoutException;
-    }
-
-    private static string NormalizeSystemRole(string role)
-    {
-        return role.Trim() switch
-        {
-            AccessControlService.RoleAdmin => AccessControlService.RoleAdmin,
-            AccessControlService.RoleNotifiedBody => AccessControlService.RoleNotifiedBody,
-            AccessControlService.RoleMarketSurveillanceAuthority => AccessControlService.RoleMarketSurveillanceAuthority,
-            AccessControlService.RoleCommission => AccessControlService.RoleCommission,
-            AccessControlService.RoleLegitimateInterest => AccessControlService.RoleLegitimateInterest,
-            _ => AccessControlService.RoleNormalUser
-        };
-    }
-
-    private static IReadOnlyList<ConformanceIssueGroupViewModel> BuildIssueGroups(
-        TrustValidationSummary summary,
-        TrustValidationSeverity severity)
-    {
-        return summary.Sections
-            .Select(section => new ConformanceIssueGroupViewModel
-            {
-                SectionKey = section.SectionKey,
-                SectionLabel = section.SectionLabel,
-                EditAnchor = AdminSectionAnchor(section.SectionKey),
-                Issues = section.Issues
-                    .Where(issue => issue.Severity == severity)
-                    .ToList()
-            })
-            .Where(group => group.Issues.Count > 0)
-            .ToList();
-    }
-
-    private static string AdminSectionAnchor(string sectionKey)
-    {
-        return sectionKey switch
-        {
-            "generalProductInformation" or "identity" or "dataCompletionPolicy" => "admin-general",
-            "materialComposition" => "admin-material-composition",
-            "performanceAndDurability" => "admin-performance",
-            "labeling" => "admin-compliance",
-            "supplyChainDueDiligence" => "admin-supply-chain",
-            "circularity" => "admin-circularity",
-            "carbonFootprintForBatteries" => "admin-carbon-footprint",
-            _ => "admin-general"
-        };
-    }
-
-    private static IReadOnlyDictionary<string, DataRequirementField> BuildFieldRequirementDictionary(DataCompletionPolicySnapshot dataRequirements)
-    {
-        return dataRequirements.Sections
-            .SelectMany(section => section.Fields)
-            .ToDictionary(field => field.FieldKey, field => field, StringComparer.OrdinalIgnoreCase);
-    }
-
-    private async Task<EditPassportViewModel> BuildEditPassportModelAsync(
-        BsonDocument document,
-        string mode,
-        string statusMessage,
-        string? error,
-        CancellationToken cancellationToken)
-    {
-        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
-        var clusterNamesById = BuildClusterDictionary(clusters);
-        var products = await _productTemplateService.ListProductsAsync(cancellationToken);
-        var selectedProductId = FirstNonEmpty(
-            BsonHelpers.GetString(document, "app", "product", "productId"),
-            BatteryProductTemplateCatalog.DefaultProductId);
-        var selectedProduct = products.FirstOrDefault(product => product.ProductId.Equals(selectedProductId, StringComparison.OrdinalIgnoreCase))
-            ?? BatteryProductTemplateCatalog.DefaultProduct;
-        var selectedProductVersion = FirstNonEmpty(
-            BsonHelpers.GetString(document, "app", "product", "productVersion"),
-            selectedProduct.LatestProductVersion.Version);
-        var selectedVersion = selectedProduct.ProductVersions.FirstOrDefault(version => version.Version.Equals(selectedProductVersion, StringComparison.OrdinalIgnoreCase))
-            ?? selectedProduct.LatestProductVersion;
-        var dataRequirements = await _dataCompletionPolicyService.GetPolicyForPassportAsync(document, cancellationToken);
-        var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
-        var fieldEditableByKey = BuildFieldEditableDictionary(editablePolicy, mode);
-
-        return new EditPassportViewModel
-        {
-            Passport = _viewModelFactory.Create(document, clusterNamesById),
-            Mode = mode,
-            DataRequirements = dataRequirements,
-            ProductTemplates = BuildProductTemplateSummaries(products),
-            ProductTemplateCatalog = BuildProductTemplateFormCatalog(products),
-            Clusters = BuildClusterViewModels(clusters),
-            SelectedProductId = selectedProduct.ProductId,
-            SelectedProductVersion = selectedVersion.Version,
-            SelectedClusterId = BsonHelpers.GetString(document, "clusterId"),
-            FieldRequirementByKey = BuildFieldRequirementDictionary(dataRequirements),
-            FieldEditableByKey = fieldEditableByKey,
-            StatusMessage = statusMessage,
-            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error)
-        };
-    }
-
-    private async Task<IActionResult> ReturnNewBatteryFormWithErrorAsync(
-        IFormCollection form,
-        string error,
-        CancellationToken cancellationToken)
-    {
-        var document = await BuildNewBatteryDraftFromFormAsync(form, cancellationToken);
-        var model = await BuildEditPassportModelAsync(
-            document,
-            "new-battery",
-            string.Empty,
-            error,
-            cancellationToken);
-        return View("EditPassport", model);
-    }
-
-    private async Task<BsonDocument> BuildNewBatteryDraftFromFormAsync(IFormCollection form, CancellationToken cancellationToken)
-    {
-        var product = await _productTemplateService.GetProductAsync(Text(form, "productId", BatteryProductTemplateCatalog.DefaultProductId), cancellationToken)
-            ?? BatteryProductTemplateCatalog.DefaultProduct;
-        var productVersion = product.ProductVersions.FirstOrDefault(version =>
-                version.Version.Equals(Text(form, "productVersion"), StringComparison.OrdinalIgnoreCase))
-            ?? product.LatestProductVersion;
-        var now = DateTimeOffset.UtcNow.ToString("O");
-        var document = ProductTemplatePassportBuilder.BuildPassportFromTemplate(
-            "new-battery",
-            product,
-            productVersion,
-            new ProductTemplateBatteryIdentity
-            {
-                ModelNumber = Text(form, "modelNumber"),
-                SerialNumber = Text(form, "serialNumber"),
-                DisplayName = Text(form, "name"),
-                FacilityId = Text(form, "facilityId"),
-                ClusterId = Text(form, "clusterId"),
-                ManufacturingDate = Text(form, "manufacturingDate", now[..10])
-            },
-            now);
-        var editablePolicy = await _editableFieldPolicyService.GetPolicyAsync(cancellationToken);
-        ApplyPassportForm(document, ApplyBatteryEditableFormValues(form, editablePolicy, forCreation: true), now);
-        document["clusterId"] = Text(form, "clusterId");
-        return document;
-    }
+        var candidates = tokens
+            .Select(token => ToGeneratedClusterTokenCandidate(token, clusterNamesById))
+            .Where(candidate => candidate != null)
+            .Select(candidate => candidate!)
+            .ToList();
 
-    private static IReadOnlyList<ClusterViewModel> BuildClusterViewModels(IEnumerable<BsonDocument> clusters) =>
-        clusters
-            .Select(cluster => new ClusterViewModel
+        return candidates
+            .GroupBy(candidate => $"{candidate.ClusterId}\u001f{candidate.AccessMode}", StringComparer.OrdinalIgnoreCase)
+            .SelectMany(group =>
             {
-                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name"),
-                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
-                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
+                var ordered = group
+                    .OrderByDescending(candidate => candidate.HasUsage)
+                    .ThenByDescending(candidate => candidate.HasAutoClusterId)
+                    .ThenByDescending(candidate => candidate.UpdatedAt, StringComparer.Ordinal)
+                    .ToList();
+                return ordered
+                    .Skip(1)
+                    .Where(candidate => !candidate.HasUsage)
+                    .Select(candidate => candidate.TokenId);
             })
-            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
             .ToList();
+    }
 
-    private static IReadOnlyDictionary<string, bool> BuildFieldEditableDictionary(
-        EditableFieldPolicySnapshot policy,
-        string mode)
+    private static GeneratedClusterTokenCandidate? ToGeneratedClusterTokenCandidate(
+        BsonDocument token,
+        IReadOnlyDictionary<string, string> clusterNamesById)
     {
-        var isCreation = mode.Equals("new-battery", StringComparison.OrdinalIgnoreCase);
-        var isBatteryEdit = mode.Equals("battery-edit", StringComparison.OrdinalIgnoreCase);
-        if (!isCreation && !isBatteryEdit)
+        if (token.GetValue("isSample", false).ToBoolean()
+            || token.GetValue("globalAccess", false).ToBoolean()
+            || token.GetValue("allowUnassigned", false).ToBoolean())
         {
-            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
+            return null;
         }
 
-        var editable = policy.PermissionByKey.Values
-            .GroupBy(permission => permission.FieldKey, StringComparer.OrdinalIgnoreCase)
-            .ToDictionary(
-                group => group.Key,
-                group => isCreation ? group.Last().EditableAtCreation : group.Last().EditableAfterCreation,
-                StringComparer.OrdinalIgnoreCase);
-        foreach (var operationalKey in AlwaysEditableOperationalFieldKeys)
+        var accessMode = BsonHelpers.GetString(token, "accessMode");
+        if (!accessMode.Equals("read", StringComparison.OrdinalIgnoreCase)
+            && !accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
+            && !accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase))
         {
-            editable[operationalKey] = true;
+            return null;
         }
 
-        return editable;
-    }
-
-    private static IFormCollection ApplyBatteryEditableFormValues(
-        IFormCollection source,
-        EditableFieldPolicySnapshot policy,
-        bool forCreation)
-    {
-        var allowedFieldNames = new HashSet<string>(AlwaysAllowedBatteryFormFields, StringComparer.OrdinalIgnoreCase);
-        foreach (var permission in policy.PermissionByKey.Values)
+        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
+        var clusterIds = tokenClusterIds
+            .Select(entry => entry.ToString() ?? string.Empty)
+            .Where(value => !string.IsNullOrWhiteSpace(value))
+            .ToList();
+        if (clusterIds.Count != 1)
         {
-            var allowed = forCreation
-                ? policy.IsEditableAtCreation(permission.FieldKey)
-                : policy.IsEditableAfterCreation(permission.FieldKey);
-            if (!allowed || !BatteryFormFieldsByEditableKey.TryGetValue(permission.FieldKey, out var formFields))
-            {
-                continue;
-            }
-
-            foreach (var formField in formFields)
-            {
-                allowedFieldNames.Add(formField);
-            }
+            return null;
         }
 
-        var rejectedLockedFields = source.Keys
-            .Where(key => !allowedFieldNames.Contains(key) && !key.Equals("__RequestVerificationToken", StringComparison.OrdinalIgnoreCase))
-            .ToList();
-        _ = rejectedLockedFields; // Rejected locked field names are intentionally ignored instead of persisted.
-
-        var filtered = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
-        foreach (var fieldName in allowedFieldNames)
+        var clusterId = clusterIds[0];
+        var autoClusterId = BsonHelpers.GetString(token, "autoClusterId");
+        var clusterName = ClusterTokenScopeLabel(clusterId, clusterNamesById);
+        var tokenName = BsonHelpers.GetString(token, "name");
+        if (!autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase)
+            && !GeneratedClusterTokenNameMatches(tokenName, clusterName, accessMode))
         {
-            if (source.TryGetValue(fieldName, out var value))
-            {
-                filtered[fieldName] = value;
-            }
+            return null;
         }
 
-        return new FormCollection(filtered);
-    }
+        var tokenId = BsonHelpers.GetString(token, "tokenId");
+        if (string.IsNullOrWhiteSpace(tokenId))
+        {
+            return null;
+        }
 
-    private static IReadOnlyList<ProductTemplateSummaryViewModel> BuildProductTemplateSummaries(IEnumerable<BatteryProductTemplate> products)
-    {
-        return products
-            .Select(product => new ProductTemplateSummaryViewModel
-            {
-                ProductId = product.ProductId,
-                ProductName = product.ProductName,
-                Description = product.Description,
-                ImageUrl = product.ImageUrl,
-                ModuleCount = product.ModuleCount,
-                BatteryVersionCount = product.ProductVersions.Count == 0 ? 1 : product.ProductVersions.Count,
-                RequiredFieldCount = product.RequiredFieldKeys.Count,
-                DocumentCount = product.TemplateDocuments.Count
-            })
-            .OrderBy(product => product.ProductName, StringComparer.OrdinalIgnoreCase)
-            .ToList();
+        return new GeneratedClusterTokenCandidate(
+            tokenId,
+            clusterId,
+            accessMode,
+            HasUsage: !string.IsNullOrWhiteSpace(BsonHelpers.GetString(token, "lastUsedAt")),
+            HasAutoClusterId: autoClusterId.Equals(clusterId, StringComparison.OrdinalIgnoreCase),
+            UpdatedAt: BsonHelpers.GetString(token, "updatedAt"));
     }
 
-    private static IReadOnlyList<ProductVersionEditViewModel> BuildProductVersionEditModels(IEnumerable<BatteryProductVersion> productVersions)
+    private static bool GeneratedClusterTokenNameMatches(string tokenName, string clusterName, string accessMode)
     {
-        return productVersions
-            .OrderBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
-            .Reverse()
-            .Select(version =>
-            {
-                var highestSoftwareVersion = version.HighestSoftwareVersion;
-                return new ProductVersionEditViewModel
-                {
-                    Version = version.Version,
-                    BatteryMassKg = version.BatteryMassKg,
-                    RatedEnergyKwh = version.RatedEnergyKwh,
-                    RatedCapacityAh = version.RatedCapacityAh,
-                    RatedMaximumPowerKw = version.RatedMaximumPowerKw,
-                    NominalVoltageV = version.NominalVoltageV,
-                    ExpectedLifetimeYears = version.ExpectedLifetimeYears,
-                    ExpectedCycles = version.ExpectedCycles,
-                    SupplyChainIndex = version.SupplyChainIndex,
-                    CarbonFootprint = version.CarbonFootprint,
-                    PerformanceClass = version.PerformanceClass,
-                    MaterialMassesKg = version.MaterialMassesKg,
-                    CarbonStages = version.CarbonStages,
-                    RecycledContent = version.RecycledContent.ToDictionary(
-                        pair => pair.Key,
-                        pair => new ProductTemplateRecycledContentViewModel
-                        {
-                            PreConsumerShare = pair.Value.PreConsumerShare,
-                            PostConsumerShare = pair.Value.PostConsumerShare
-                        },
-                        StringComparer.OrdinalIgnoreCase),
-                    SoftwareVersion = highestSoftwareVersion.SoftwareVersion,
-                    SoftwareReleaseDate = highestSoftwareVersion.SoftwareReleaseDate,
-                    SoftwareLatestUpdate = highestSoftwareVersion.SoftwareLatestUpdate,
-                    SoftwareVersions = version.SoftwareVersions
-                        .Select(softwareVersion => new ProductSoftwareVersionEditViewModel
-                        {
-                            SoftwareVersion = softwareVersion.SoftwareVersion,
-                            SoftwareReleaseDate = softwareVersion.SoftwareReleaseDate,
-                            SoftwareLatestUpdate = softwareVersion.SoftwareLatestUpdate
-                        })
-                        .ToList(),
-                    TemplateDocuments = version.TemplateDocuments
-                        .Select(document => new ProductTemplateDocumentViewModel
-                        {
-                            DocumentKey = document.DocumentKey,
-                            Label = document.Label,
-                            FileName = document.FileName,
-                            Visibility = document.Visibility
-                        })
-                        .ToList(),
-                    RequiredFieldKeys = version.RequiredFieldKeys
-                };
-            })
-            .ToList();
+        var expectedSuffix = accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
+            ? "readwritesign"
+            : accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
+                ? "readwrite"
+                : "read";
+        return tokenName.Equals($"{clusterName} - {expectedSuffix}", StringComparison.OrdinalIgnoreCase);
     }
 
-    private static IReadOnlyList<ProductTemplateFormCatalogItemViewModel> BuildProductTemplateFormCatalog(IEnumerable<BatteryProductTemplate> products)
+    private static string NormalizeCredentialTab(string? value)
     {
-        return products
-            .Select(product => new ProductTemplateFormCatalogItemViewModel
-            {
-                ProductId = product.ProductId,
-                ProductName = product.ProductName,
-                ImageUrl = product.ImageUrl,
-                ModuleCount = product.ModuleCount,
-                BatteryMassKg = product.BatteryMassKg,
-                RatedEnergyKwh = product.RatedEnergyKwh,
-                RatedCapacityAh = product.RatedCapacityAh,
-                RatedMaximumPowerKw = product.RatedMaximumPowerKw,
-                NominalVoltageV = product.NominalVoltageV,
-                ExpectedLifetimeYears = product.ExpectedLifetimeYears,
-                ExpectedCycles = product.ExpectedCycles,
-                SupplyChainIndex = product.SupplyChainIndex,
-                CarbonFootprint = product.CarbonFootprint,
-                PerformanceClass = product.PerformanceClass,
-                MaterialMassesKg = product.MaterialMassesKg,
-                CarbonStages = product.CarbonStages,
-                RecycledContent = product.RecycledContent.ToDictionary(
-                    pair => pair.Key,
-                    pair => new ProductTemplateRecycledContentViewModel
-                    {
-                        PreConsumerShare = pair.Value.PreConsumerShare,
-                        PostConsumerShare = pair.Value.PostConsumerShare
-                    },
-                    StringComparer.OrdinalIgnoreCase),
-                ProductVersions = BuildProductVersionEditModels(product.ProductVersions.Count == 0
-                    ? [product.LatestProductVersion]
-                    : product.ProductVersions),
-                LatestProductVersion = product.LatestProductVersion.Version
-            })
-            .ToList();
+        return value?.ToLowerInvariant() switch
+        {
+            _ => "api-tokens"
+        };
     }
 
-    private sealed class VersionStringComparer : IComparer<string>
+    private static string NormalizeSystemRole(string role)
     {
-        public static readonly VersionStringComparer Descending = new(descending: true);
-
-        private readonly bool _descending;
-
-        private VersionStringComparer(bool descending)
-        {
-            _descending = descending;
-        }
-
-        public int Compare(string? x, string? y)
-        {
-            var result = CompareAscending(x ?? string.Empty, y ?? string.Empty);
-            return _descending ? -result : result;
-        }
-
-        private static int CompareAscending(string left, string right)
+        return role.Trim() switch
         {
-            var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
-            var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
-            var max = Math.Max(leftParts.Length, rightParts.Length);
-            for (var index = 0; index < max; index++)
-            {
-                var leftValue = index < leftParts.Length && int.TryParse(leftParts[index], out var parsedLeft) ? parsedLeft : 0;
-                var rightValue = index < rightParts.Length && int.TryParse(rightParts[index], out var parsedRight) ? parsedRight : 0;
-                var partResult = leftValue.CompareTo(rightValue);
-                if (partResult != 0)
-                {
-                    return partResult;
-                }
-            }
-
-            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
-        }
+            AccessControlService.RoleAdmin => AccessControlService.RoleAdmin,
+            AccessControlService.RoleNotifiedBody => AccessControlService.RoleNotifiedBody,
+            AccessControlService.RoleMarketSurveillanceAuthority => AccessControlService.RoleMarketSurveillanceAuthority,
+            AccessControlService.RoleCommission => AccessControlService.RoleCommission,
+            AccessControlService.RoleLegitimateInterest => AccessControlService.RoleLegitimateInterest,
+            _ => AccessControlService.RoleNormalUser
+        };
     }
 
     private string CurrentActor()
@@ -2379,18 +924,6 @@ public class AdminController : Controller
             cancellationToken: cancellationToken);
     }
 
-    private static Dictionary<string, string> BuildClusterDictionary(IEnumerable<BsonDocument> clusters)
-    {
-        return clusters
-            .Select(cluster => new
-            {
-                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
-                Name = BsonHelpers.GetString(cluster, "name")
-            })
-            .Where(cluster => !string.IsNullOrWhiteSpace(cluster.ClusterId))
-            .ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
-    }
-
     private async Task<string> ResolveTokenValueAsync(string tokenId, string fallback, CancellationToken cancellationToken)
     {
         var tokenDocument = await _externalApiRepository.GetTokenByIdAsync(tokenId, cancellationToken);
@@ -2402,358 +935,6 @@ public class AdminController : Controller
         return fallback;
     }
 
-    private async Task<BsonDocument> BuildDraftPassportDocumentAsync(
-        string passportId,
-        string productId,
-        string productVersion,
-        string softwareVersion,
-        CancellationToken cancellationToken)
-    {
-        var suffix = passportId.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? Guid.NewGuid().ToString("N");
-        var normalizedSuffix = new string(suffix.Where(char.IsLetterOrDigit).Take(12).ToArray());
-        if (string.IsNullOrWhiteSpace(normalizedSuffix))
-        {
-            normalizedSuffix = Guid.NewGuid().ToString("N")[..12];
-        }
-
-        return await _productTemplateService.BuildPassportFromTemplateAsync(
-            passportId,
-            FirstNonEmpty(productId, BatteryProductTemplateCatalog.DefaultProductId),
-            FirstNonEmpty(productVersion, BatteryProductTemplateCatalog.DefaultProduct.LatestProductVersion.Version),
-            FirstNonEmpty(softwareVersion, BatteryProductTemplateCatalog.DefaultSoftwareVersion),
-            new ProductTemplateBatteryIdentity
-            {
-                ModelNumber = string.Empty,
-                SerialNumber = string.Empty,
-                DisplayName = string.Empty,
-                FacilityId = string.Empty,
-                ClusterId = string.Empty,
-                ManufacturingDate = string.Empty
-            },
-            CurrentActor(),
-            cancellationToken);
-    }
-
-    private static void ApplyBatteryIdentityFromDocument(
-        BsonDocument battery,
-        BatteryProductTemplate product,
-        BatteryProductVersion productVersion,
-        string now)
-    {
-        var app = EnsureDocument(battery, "app");
-        var display = EnsureDocument(app, "display");
-        var productNode = EnsureDocument(app, "product");
-        var identity = EnsureDocument(battery, "identity");
-        productNode["batteryModel"] = productVersion.Version;
-        identity["batteryFamily"] = product.ProductName;
-        identity["batteryModel"] = productVersion.Version;
-        identity["modelNumber"] = BsonHelpers.GetString(display, "modelNumber");
-        identity["serialNumber"] = BsonHelpers.GetString(display, "serialNumber");
-        identity["displayName"] = BsonHelpers.GetString(display, "name");
-        identity["facilityId"] = BsonHelpers.GetString(display, "facilityId");
-        identity["productId"] = product.ProductId;
-        identity["productVersion"] = productVersion.Version;
-        identity["softwareVersion"] = BsonHelpers.GetString(productNode, "softwareVersion");
-        battery["updatedAt"] = now;
-    }
-
-    private static void ApplyPassportForm(BsonDocument document, IFormCollection form, string now)
-    {
-        var app = EnsureDocument(document, "app");
-        var display = EnsureDocument(app, "display");
-        var media = EnsureDocument(app, "media");
-        var appDocuments = EnsureDocument(app, "documents");
-        var appNotes = EnsureDocument(app, "notes");
-        var appCircularityNotes = EnsureDocument(appNotes, "circularity");
-        var productNode = EnsureDocument(app, "product");
-        productNode["productId"] = Text(form, "productId", productNode.GetValue("productId", BatteryProductTemplateCatalog.DefaultProductId).ToString());
-        productNode["productVersion"] = Text(form, "productVersion", productNode.GetValue("productVersion", BatteryProductTemplateCatalog.DefaultProduct.LatestProductVersion.Version).ToString());
-        productNode["softwareVersion"] = Text(form, "softwareVersion", productNode.GetValue("softwareVersion", BatteryProductTemplateCatalog.DefaultSoftwareVersion).ToString());
-
-        display["name"] = Text(form, "name", display.GetValue("name", string.Empty).ToString());
-        display["modelNumber"] = Text(form, "modelNumber", display.GetValue("modelNumber", string.Empty).ToString());
-        display["serialNumber"] = BatteryPassCanonicalDataCatalog.NormalizeManufacturerSerialNumber(
-            Text(form, "serialNumber", display.GetValue("serialNumber", string.Empty).ToString()),
-            BsonHelpers.GetString(document, "passportId"));
-        display["facilityId"] = Text(form, "facilityId", display.GetValue("facilityId", string.Empty).ToString());
-        display["manufacturerName"] = Text(form, "manufacturerName", display.GetValue("manufacturerName", string.Empty).ToString());
-
-        var fallbackImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(
-            media.GetValue("batteryImageUrl", BatteryImageCatalog.DefaultImageUrl).ToString(),
-            BsonHelpers.GetString(document, "passportId"));
-        var batteryImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(
-            Text(form, "batteryImageUrl", fallbackImageUrl),
-            BsonHelpers.GetString(document, "passportId"));
-        media["batteryImageUrl"] = batteryImageUrl;
-        media["batteryImageAlt"] = $"Industrial battery pack for passport {display.GetValue("modelNumber", string.Empty)}";
-
-        foreach (var key in DocumentKeys)
-        {
-            var documentNode = EnsureDocument(appDocuments, key);
-            var url = Text(form, $"document_{key}", documentNode.GetValue("url", string.Empty).ToString());
-            if (!string.IsNullOrWhiteSpace(url))
-            {
-                documentNode["url"] = url;
-            }
-
-            documentNode["visibility"] = NormalizeDocumentVisibility(Text(
-                form,
-                $"document_{key}_visibility",
-                documentNode.GetValue("visibility", "private").ToString()));
-        }
-
-        var aspects = EnsureDocument(document, "aspects");
-        var generalAspect = EnsureAspectPayload(aspects, "generalProductInformation", now, "draft");
-        var generalPayload = EnsureDocument(generalAspect, "payload");
-        generalPayload["productIdentifier"] = display.GetValue("modelNumber", string.Empty).ToString();
-        generalPayload["batteryPassportIdentifier"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryPassportIdentifier(
-            BsonHelpers.GetString(generalPayload, "batteryPassportIdentifier"),
-            BsonText(display.GetValue("serialNumber", string.Empty)),
-            BsonHelpers.GetString(document, "passportId"));
-        generalPayload["batteryCategory"] = BatteryPassCanonicalDataCatalog.NormalizeBatteryCategory(
-            BsonHelpers.GetString(generalPayload, "batteryCategory"));
-        generalPayload["batteryStatus"] = Text(form, "batteryStatus", generalPayload.GetValue("batteryStatus", "Original").ToString());
-        generalPayload["batteryMass"] = Number(form, "batteryMass", generalPayload.GetValue("batteryMass", 0).ToDouble());
-        var manufacturingDate = Text(form, "manufacturingDate", DateOnly(generalPayload.GetValue("manufacturingDate", string.Empty).ToString()));
-        generalPayload["manufacturingDate"] = $"{manufacturingDate}T00:00:00.000Z";
-        var existingPuttingIntoService = BsonHelpers.GetString(generalPayload, "puttingIntoService");
-        var puttingIntoServiceDate = string.IsNullOrWhiteSpace(existingPuttingIntoService)
-            ? manufacturingDate
-            : DateOnly(existingPuttingIntoService);
-        generalPayload["puttingIntoService"] = $"{puttingIntoServiceDate}T00:00:00.000Z";
-
-        var materialAspect = EnsureAspectPayload(aspects, "materialComposition", now, "draft");
-        var materialPayload = EnsureDocument(materialAspect, "payload");
-        var existingMaterials = materialPayload.GetValue("batteryMaterials", new BsonArray()) is BsonArray materials
-            ? materials
-            : new BsonArray();
-        var materialRows = new BsonArray();
-        foreach (var materialDefinition in BatteryPassCanonicalDataCatalog.Materials)
-        {
-            var fallbackMass = existingMaterials
-                .OfType<BsonDocument>()
-                .FirstOrDefault(item => BsonText(item.GetValue("batteryMaterialName", string.Empty)).Equals(materialDefinition.Label, StringComparison.OrdinalIgnoreCase))?
-                .GetValue("batteryMaterialMass", 0).ToDouble() ?? 0;
-            var mass = BatteryPassCanonicalDataCatalog.NormalizeMaterialMass(
-                materialDefinition.Label,
-                Number(form, materialDefinition.Field, fallbackMass));
-
-            var existing = existingMaterials.OfType<BsonDocument>()
-                .FirstOrDefault(item => BsonText(item.GetValue("batteryMaterialName", string.Empty)).Equals(materialDefinition.Label, StringComparison.OrdinalIgnoreCase));
-            if (existing != null)
-            {
-                existing["batteryMaterialName"] = materialDefinition.Label;
-                existing["batteryMaterialMass"] = mass;
-                if (existing.GetValue("batteryMaterialLocation", BsonNull.Value) is not BsonDocument)
-                {
-                    existing["batteryMaterialLocation"] = DemoMaterialLocation();
-                }
-                existing["isCriticalRawMaterial"] = materialDefinition.IsCriticalRawMaterial;
-                materialRows.Add(existing);
-            }
-            else
-            {
-                materialRows.Add(new BsonDocument
-                {
-                    ["batteryMaterialName"] = materialDefinition.Label,
-                    ["batteryMaterialMass"] = mass,
-                    ["batteryMaterialLocation"] = DemoMaterialLocation(),
-                    ["isCriticalRawMaterial"] = materialDefinition.IsCriticalRawMaterial
-                });
-            }
-        }
-        materialPayload["batteryMaterials"] = materialRows;
-
-        var performanceAspect = EnsureAspectPayload(aspects, "performanceAndDurability", now, "draft");
-        var performancePayload = EnsureDocument(performanceAspect, "payload");
-        var technical = EnsureDocument(performancePayload, "batteryTechicalProperties");
-        technical["ratedEnergy"] = Number(form, "ratedEnergy", technical.GetValue("ratedEnergy", 0).ToDouble());
-        technical["ratedCapacity"] = Number(form, "ratedCapacity", technical.GetValue("ratedCapacity", 0).ToDouble());
-        technical["ratedMaximumPower"] = Number(form, "ratedMaximumPower", technical.GetValue("ratedMaximumPower", 0).ToDouble());
-        technical["nominalVoltage"] = Number(form, "nominalVoltage", technical.GetValue("nominalVoltage", 0).ToDouble());
-        technical["expectedLifetime"] = Number(form, "expectedLifetime", technical.GetValue("expectedLifetime", 0).ToDouble());
-        technical["expectedNumberOfCycles"] = Number(form, "expectedNumberOfCycles", technical.GetValue("expectedNumberOfCycles", 0).ToDouble());
-        var batteryCondition = EnsureDocument(performancePayload, "batteryCondition");
-        batteryCondition["stateOfCharge"] = new BsonDocument
-        {
-            ["stateOfChargeValue"] = Number(form, "stateOfCharge", NumberAtDocument(batteryCondition, "stateOfCharge", "stateOfChargeValue")),
-            ["lastUpdate"] = now
-        };
-        batteryCondition["remainingCapacity"] = new BsonDocument
-        {
-            ["remainingCapacityValue"] = Number(form, "remainingCapacity", NumberAtDocument(batteryCondition, "remainingCapacity", "remainingCapacityValue")),
-            ["lastUpdate"] = now
-        };
-        batteryCondition["remainingEnergy"] = new BsonDocument
-        {
-            ["remainingEnergyValue"] = Number(form, "remainingEnergy", NumberAtDocument(batteryCondition, "remainingEnergy", "remainingEnergyValue")),
-            ["lastUpdate"] = now
-        };
-        batteryCondition["numberOfFullCycles"] = new BsonDocument
-        {
-            ["numberOfFullCyclesValue"] = Number(form, "fullCycles", NumberAtDocument(batteryCondition, "numberOfFullCycles", "numberOfFullCyclesValue")),
-            ["lastUpdate"] = now
-        };
-
-        var carbonAspect = EnsureAspectPayload(aspects, "carbonFootprintForBatteries", now, "draft");
-        var carbonPayload = EnsureDocument(carbonAspect, "payload");
-        carbonPayload["batteryCarbonFootprint"] = BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(
-            Number(form, "carbonFootprint", carbonPayload.GetValue("batteryCarbonFootprint", 0).ToDouble()));
-        carbonPayload["carbonFootprintPerformanceClass"] = BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(
-            Text(form, "performanceClass", carbonPayload.GetValue("carbonFootprintPerformanceClass", "B").ToString()));
-        var lifecycleRows = new BsonArray();
-        var existingLifecycleRows = carbonPayload.GetValue("carbonFootprintPerLifecycleStage", new BsonArray()) as BsonArray ?? new BsonArray();
-        foreach (var carbonStage in BatteryPassCanonicalDataCatalog.CarbonStages)
-        {
-            var fallbackCarbonStage = existingLifecycleRows
-                .OfType<BsonDocument>()
-                .FirstOrDefault(row => BsonText(row.GetValue("lifecycleStage", string.Empty)).Equals(carbonStage.Stage, StringComparison.OrdinalIgnoreCase))?
-                .GetValue("carbonFootprint", carbonStage.DemoValue)
-                .ToDouble() ?? carbonStage.DemoValue;
-            var value = BatteryPassCanonicalDataCatalog.NormalizeCarbonStageValue(
-                carbonStage.Stage,
-                Number(form, carbonStage.Field, fallbackCarbonStage));
-            lifecycleRows.Add(new BsonDocument
-            {
-                ["lifecycleStage"] = carbonStage.Stage,
-                ["carbonFootprint"] = value
-            });
-        }
-        carbonPayload["carbonFootprintPerLifecycleStage"] = lifecycleRows;
-        carbonPayload["carbonFootprintStudy"] = EnsureDocument(appDocuments, "co2StudyReference").GetValue("url", string.Empty).ToString();
-
-        var supplyAspect = EnsureAspectPayload(aspects, "supplyChainDueDiligence", now, "draft");
-        var supplyPayload = EnsureDocument(supplyAspect, "payload");
-        supplyPayload["supplyChainIndicies"] = BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(
-            Number(form, "supplyChainIndex", supplyPayload.GetValue("supplyChainIndicies", 0).ToDouble()));
-        supplyPayload["supplyChainDueDiligenceReport"] = EnsureDocument(appDocuments, "dueDiligenceReport").GetValue("url", string.Empty).ToString();
-        supplyPayload["thirdPartyAussurances"] = EnsureDocument(appDocuments, "thirdPartyAudit").GetValue("url", string.Empty).ToString();
-        supplyPayload["sustainabilityReport"] = EnsureDocument(appDocuments, "sustainabilityReport").GetValue("url", string.Empty).ToString();
-        supplyPayload["taxonomyReport"] = EnsureDocument(appDocuments, "taxonomyReport").GetValue("url", string.Empty).ToString();
-
-        var labelAspect = EnsureAspectPayload(aspects, "labeling", now, "draft");
-        var labelPayload = EnsureDocument(labelAspect, "payload");
-        labelPayload["resultOfTestReport"] = EnsureDocument(appDocuments, "conformityAssessment").GetValue("url", string.Empty).ToString();
-        labelPayload["declarationOfConformity"] = EnsureDocument(appDocuments, "euDeclarationOfConformity").GetValue("url", string.Empty).ToString();
-
-        var circularityAspect = EnsureAspectPayload(aspects, "circularity", now, "partial");
-        var circularityPayload = EnsureDocument(circularityAspect, "payload");
-        var endOfLifeInformation = EnsureDocument(circularityPayload, "endOfLifeInformation");
-        endOfLifeInformation["separateCollection"] = Text(form, "separateCollection", endOfLifeInformation.GetValue("separateCollection", string.Empty).ToString());
-        endOfLifeInformation["wastePrevention"] = Text(form, "wastePrevention", endOfLifeInformation.GetValue("wastePrevention", string.Empty).ToString());
-        appCircularityNotes["separateCollection"] = endOfLifeInformation.GetValue("separateCollection", string.Empty).ToString();
-        appCircularityNotes["wastePrevention"] = endOfLifeInformation.GetValue("wastePrevention", string.Empty).ToString();
-        appCircularityNotes["recycledContentShareVerification"] = Text(
-            form,
-            "recycledContentShareVerification",
-            appCircularityNotes.GetValue("recycledContentShareVerification", "unverified").ToString());
-
-        var recycledMaterials = new[]
-        {
-            ("recycledNickel", "Nickel"),
-            ("recycledCobalt", "Cobalt"),
-            ("recycledLithium", "Lithium"),
-            ("recycledLead", "Lead")
-        };
-        var existingRecycledRows = circularityPayload.GetValue("recycledContent", new BsonArray()) as BsonArray ?? new BsonArray();
-        var recycledAspectRows = new BsonArray();
-        foreach (var (prefix, material) in recycledMaterials)
-        {
-            var fallbackRecycledRow = existingRecycledRows
-                .OfType<BsonDocument>()
-                .FirstOrDefault(row => BsonText(row.GetValue("recycledMaterial", string.Empty)).Equals(material, StringComparison.OrdinalIgnoreCase));
-            var pre = Number(form, $"{prefix}Pre", fallbackRecycledRow?.GetValue("preConsumerShare", 0).ToDouble() ?? 0);
-            var post = Number(form, $"{prefix}Post", fallbackRecycledRow?.GetValue("postConsumerShare", 0).ToDouble() ?? 0);
-            recycledAspectRows.Add(new BsonDocument
-            {
-                ["recycledMaterial"] = material,
-                ["preConsumerShare"] = pre,
-                ["postConsumerShare"] = post
-            });
-        }
-        circularityPayload["recycledContent"] = recycledAspectRows;
-        RemoveDuplicatedDisplayCharts(app);
-
-        var registryInfo = EnsureDocument(document, "registryInfo");
-        registryInfo["status"] = Text(form, "status", registryInfo.GetValue("status", "draft").ToString());
-        registryInfo["updatedAt"] = now;
-        if (!registryInfo.Contains("createdAt"))
-        {
-            registryInfo["createdAt"] = now;
-        }
-        if (!registryInfo.Contains("registryId") || string.IsNullOrWhiteSpace(registryInfo.GetValue("registryId", string.Empty).ToString()))
-        {
-            registryInfo["registryId"] = Guid.NewGuid().ToString("N");
-        }
-
-        var validation = EnsureDocument(document, "validation");
-        validation["isValid"] = false;
-        validation["signedAt"] = BsonNull.Value;
-    }
-
-    private async Task ApplySelectedProductTemplateMetadataAsync(
-        BsonDocument document,
-        IFormCollection form,
-        CancellationToken cancellationToken)
-    {
-        var app = EnsureDocument(document, "app");
-        var productNode = EnsureDocument(app, "product");
-        var productId = Text(form, "productId", BsonHelpers.GetString(productNode, "productId"));
-        var productVersion = Text(form, "productVersion", BsonHelpers.GetString(productNode, "productVersion"));
-        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken);
-        if (product == null)
-        {
-            return;
-        }
-
-        var selectedProductVersion = product.ProductVersions.FirstOrDefault(version =>
-            version.Version.Equals(productVersion, StringComparison.OrdinalIgnoreCase))
-            ?? product.LatestProductVersion;
-        productNode["productId"] = product.ProductId;
-        productNode["productVersion"] = selectedProductVersion.Version;
-        productNode["productName"] = product.ProductName;
-        productNode["description"] = product.Description;
-        productNode["moduleCount"] = product.ModuleCount;
-        var highestSoftwareVersion = selectedProductVersion.HighestSoftwareVersion;
-        productNode["softwareVersion"] = highestSoftwareVersion.SoftwareVersion;
-        productNode["softwareReleaseDate"] = highestSoftwareVersion.SoftwareReleaseDate;
-        productNode["softwareLatestUpdate"] = highestSoftwareVersion.SoftwareLatestUpdate;
-
-        await _productTemplateService.ApplyProductTemplateReferencesAsync(document, product.ProductId, cancellationToken);
-        productNode["templateHash"] = ProductTemplatePassportBuilder.ComputeTemplateHash(ProductTemplatePassportBuilder.BuildTemplateBaseline(document));
-        EnsureDocument(app, "templateBaseline").Clear();
-        app["templateBaseline"] = ProductTemplatePassportBuilder.BuildTemplateBaseline(document);
-    }
-
-    private static BsonDocument EnsureAspectPayload(BsonDocument aspects, string key, string now, string state)
-    {
-        var aspect = EnsureDocument(aspects, key);
-        var verification = EnsureDocument(aspect, "verification");
-        verification["state"] = state;
-        verification["signedAt"] = BsonNull.Value;
-        if (!verification.Contains("issuer"))
-        {
-            verification["issuer"] = "did:web:acme.battery.pass:issuer";
-        }
-        EnsureDocument(aspect, "payload");
-        if (!aspect.Contains("visibility"))
-        {
-            aspect["visibility"] = "public";
-        }
-        return aspect;
-    }
-
-    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
-    {
-        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
-        {
-            document = new BsonDocument();
-            parent[key] = document;
-        }
-
-        return document;
-    }
-
     private static string Text(IFormCollection form, string key, string? fallback = "")
     {
         var value = form[key].FirstOrDefault()?.Trim();
@@ -2767,84 +948,4 @@ public class AdminController : Controller
             : $"{minutes} minutes";
     }
 
-    private static double Number(IFormCollection form, string key, double fallback)
-    {
-        var text = Text(form, key);
-        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
-            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
-        {
-            return parsed;
-        }
-
-        return fallback;
-    }
-
-    private static string DateOnly(string? value)
-    {
-        if (string.IsNullOrWhiteSpace(value))
-        {
-            return DateTime.UtcNow.ToString("yyyy-MM-dd");
-        }
-
-        return value.Length >= 10 ? value[..10] : value;
-    }
-
-    private static string NormalizeDocumentVisibility(string visibility)
-    {
-        return visibility.Equals("public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
-    }
-
-    private static string BsonText(BsonValue? value)
-    {
-        return value == null || value.IsBsonNull ? string.Empty : value.ToString() ?? string.Empty;
-    }
-
-    private static BsonDocument DemoMaterialLocation()
-    {
-        return new BsonDocument
-        {
-            ["componentName"] = "Cell",
-            ["componentId"] = "DEMO-CELL-01"
-        };
-    }
-
-    private static void RemoveDuplicatedDisplayCharts(BsonDocument app)
-    {
-        if (app.GetValue("charts", BsonNull.Value) is not BsonDocument charts)
-        {
-            return;
-        }
-
-        charts.Remove("materialComposition");
-        charts.Remove("carbonFootprint");
-        charts.Remove("recycledContent");
-        if (!charts.Any())
-        {
-            app.Remove("charts");
-        }
-    }
-
-    private static double NumberAtDocument(BsonDocument parent, string key, string nestedKey)
-    {
-        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
-        {
-            return 0;
-        }
-
-        var nestedValue = document.GetValue(nestedKey, 0);
-        return nestedValue.IsNumeric ? nestedValue.ToDouble() : 0;
-    }
-
-    private static string FirstNonEmpty(params string[] values)
-    {
-        foreach (var value in values)
-        {
-            if (!string.IsNullOrWhiteSpace(value))
-            {
-                return value;
-            }
-        }
-
-        return string.Empty;
-    }
 }
diff --git a/web/Controllers/RegistryController.cs b/web/Controllers/RegistryController.cs
--- a/web/Controllers/RegistryController.cs
+++ b/web/Controllers/RegistryController.cs
@@ -180,12 +180,7 @@ public class RegistryController : Controller
     private static string BatterySearchRedirectPath(string batteryId, bool isAdmin, bool isClusterAdmin)
     {
         var escapedBatteryId = Uri.EscapeDataString(batteryId);
-        if (isAdmin)
-        {
-            return $"/admin/batteries/{escapedBatteryId}/passports";
-        }
-
-        return isClusterAdmin ? $"/{escapedBatteryId}" : $"/{escapedBatteryId}/latest";
+        return isAdmin || isClusterAdmin ? $"/{escapedBatteryId}" : $"/{escapedBatteryId}/latest";
     }
 
     private BatteryPassportHistoryRowViewModel ToHistoryRow(BsonDocument passport)
diff --git a/web/Models/ViewModels/AdminClusterViewModel.cs b/web/Models/ViewModels/AdminClusterViewModel.cs
--- a/web/Models/ViewModels/AdminClusterViewModel.cs
+++ b/web/Models/ViewModels/AdminClusterViewModel.cs
@@ -5,13 +5,9 @@ namespace BatteryPassWeb.Models.ViewModels;
 
 public sealed class AdminClusterViewModel
 {
-    public string SelectedTab { get; init; } = "passports";
+    public string SelectedTab { get; init; } = "clusters";
     public string SelectedCredentialTab { get; init; } = "api-tokens";
-    public string PassportsQuery { get; init; } = string.Empty;
-    public BatteryTablePageViewModel BatteryTable { get; init; } = new();
     public IReadOnlyList<ClusterViewModel> Clusters { get; init; } = [];
-    public IReadOnlyList<BatterySummaryViewModel> Batteries { get; init; } = [];
-    public IReadOnlyList<PassportSummaryViewModel> Passports { get; init; } = [];
     public IReadOnlyList<UserViewModel> Users { get; init; } = [];
     public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
     public IReadOnlyList<ApiTokenViewModel> ApiTokens { get; init; } = [];
diff --git a/web/Models/ViewModels/BatteryViewModels.cs b/web/Models/ViewModels/BatteryViewModels.cs
--- a/web/Models/ViewModels/BatteryViewModels.cs
+++ b/web/Models/ViewModels/BatteryViewModels.cs
@@ -51,7 +51,7 @@ public sealed class BatteryPassportHistoryPageViewModel
     public bool CanCreatePassport { get; init; }
     public string StatusMessage { get; init; } = string.Empty;
     public string ErrorMessage { get; init; } = string.Empty;
-    public string ReturnUrl { get; init; } = "/admin/clusters?tab=batteries";
+    public string ReturnUrl { get; init; } = "/cluster-admin/passports";
     public string ReturnLabel { get; init; } = "Back to Batteries";
 }
 
diff --git a/web/Services/BatteryTableService.cs b/web/Services/BatteryTableService.cs
--- a/web/Services/BatteryTableService.cs
+++ b/web/Services/BatteryTableService.cs
@@ -7,8 +7,6 @@ namespace BatteryPassWeb.Services;
 public enum BatteryTableScope
 {
     Registry,
-    AdminBatteries,
-    AdminPassports,
     ClusterAdminPassports
 }
 
@@ -201,7 +199,7 @@ public sealed class BatteryTableService
             }
 
             var row = _batteryRepository.ToSummary(battery, history, ResolveClusterLabel(battery, clusterNamesById));
-            ApplyActions(row, scope, isGlobalAdmin, returnUrl);
+            ApplyActions(row, scope, returnUrl);
             rows.Add(row);
         }
 
@@ -211,10 +209,13 @@ public sealed class BatteryTableService
             .ToList();
     }
 
-    private static void ApplyActions(BatterySummaryViewModel row, BatteryTableScope scope, bool isGlobalAdmin, string returnUrl)
+    private static void ApplyActions(BatterySummaryViewModel row, BatteryTableScope scope, string returnUrl)
     {
+        // The team's own battery pages live under /admin/batteries (plan 2d-2); these rows only link to the public
+        // pages and, on the cluster-admin page, to the cluster-admin actions.
         var escapedBatteryId = Uri.EscapeDataString(row.BatteryId);
         var escapedReturnUrl = Uri.EscapeDataString(returnUrl);
+        var clusterAdminScope = scope == BatteryTableScope.ClusterAdminPassports;
         row.ReturnUrl = returnUrl;
         row.SummaryUrl = string.IsNullOrWhiteSpace(row.LatestPassportId)
             ? string.Empty
@@ -222,30 +223,22 @@ public sealed class BatteryTableService
         row.DetailUrl = string.IsNullOrWhiteSpace(row.LatestPassportId)
             ? string.Empty
             : $"/{escapedBatteryId}/latest";
-        row.HistoryUrl = isGlobalAdmin
-            ? $"/admin/batteries/{escapedBatteryId}/passports?returnUrl={escapedReturnUrl}"
-            : scope == BatteryTableScope.ClusterAdminPassports
-                ? $"/cluster-admin/batteries/{escapedBatteryId}/passports?returnUrl={escapedReturnUrl}"
-                : $"/{escapedBatteryId}";
-        row.EditUrl = isGlobalAdmin
-            ? $"/admin/batteries/{escapedBatteryId}/edit"
-            : string.IsNullOrWhiteSpace(row.LatestPassportId)
-                ? string.Empty
-                : $"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/edit";
-        row.CreatePassportUrl = isGlobalAdmin
-            ? $"/admin/batteries/{escapedBatteryId}/passports/create"
-            : $"/cluster-admin/batteries/{escapedBatteryId}/passports/create";
-        row.ConformanceUrl = string.IsNullOrWhiteSpace(row.LatestPassportId)
+        row.HistoryUrl = clusterAdminScope
+            ? $"/cluster-admin/batteries/{escapedBatteryId}/passports?returnUrl={escapedReturnUrl}"
+            : $"/{escapedBatteryId}";
+        row.EditUrl = !clusterAdminScope || string.IsNullOrWhiteSpace(row.LatestPassportId)
             ? string.Empty
-            : isGlobalAdmin
-                ? $"/admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance"
-                : $"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance";
+            : $"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/edit";
+        row.CreatePassportUrl = clusterAdminScope
+            ? $"/cluster-admin/batteries/{escapedBatteryId}/passports/create"
+            : string.Empty;
+        row.ConformanceUrl = !clusterAdminScope || string.IsNullOrWhiteSpace(row.LatestPassportId)
+            ? string.Empty
+            : $"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance";
         row.CanViewHistory = true;
-        row.CanEditBattery = isGlobalAdmin || (scope == BatteryTableScope.ClusterAdminPassports && !string.IsNullOrWhiteSpace(row.EditUrl));
-        row.CanCreatePassport = (isGlobalAdmin || scope == BatteryTableScope.ClusterAdminPassports)
-            && CanCreateBatteryPassport(row);
-        row.CanOpenConformance = (isGlobalAdmin || scope == BatteryTableScope.ClusterAdminPassports)
-            && !string.IsNullOrWhiteSpace(row.ConformanceUrl);
+        row.CanEditBattery = !string.IsNullOrWhiteSpace(row.EditUrl);
+        row.CanCreatePassport = clusterAdminScope && CanCreateBatteryPassport(row);
+        row.CanOpenConformance = !string.IsNullOrWhiteSpace(row.ConformanceUrl);
         row.ShowNewPassportRequired = row.NewPassportRequired && !IsDraftStatus(row.LatestPassportStatus);
         row.DisplayStatus = row.ShowNewPassportRequired ? "New passport needed" : row.LatestPassportStatus;
     }
@@ -313,17 +306,6 @@ public sealed class BatteryTableService
         row.PassportCount == 0
         || row.NewPassportRequired && !IsDraftStatus(row.LatestPassportStatus);
 
-    private static string BatterySearchRedirectPath(string batteryId, bool isGlobalAdmin, bool isClusterAdmin)
-    {
-        var escapedBatteryId = Uri.EscapeDataString(batteryId);
-        if (isGlobalAdmin)
-        {
-            return $"/admin/batteries/{escapedBatteryId}/passports";
-        }
-
-        return isClusterAdmin ? $"/{escapedBatteryId}" : $"/{escapedBatteryId}/latest";
-    }
-
     private static string ResolveClusterLabel(BsonDocument battery, IReadOnlyDictionary<string, string> clusterNamesById)
     {
         var clusterId = BsonHelpers.GetString(battery, "clusterId");
@@ -355,28 +337,6 @@ public sealed class BatteryTableService
         var querySuffix = string.IsNullOrWhiteSpace(query) ? string.Empty : $"?q={Uri.EscapeDataString(query)}";
         return scope switch
         {
-            BatteryTableScope.AdminBatteries => new BatteryTablePageViewModel
-            {
-                Query = query,
-                SearchAction = "/admin/clusters",
-                SearchPlaceholder = "Search by battery ID, passport ID, serial, family, or cluster",
-                ScopeLabel = "Search and manage all registered batteries.",
-                EmptyLabel = "No batteries are currently available.",
-                ReturnUrl = $"/admin/clusters?tab=batteries{(string.IsNullOrWhiteSpace(query) ? string.Empty : $"&q={Uri.EscapeDataString(query)}")}",
-                ShowCreateBattery = true,
-                ShowHelp = true
-            },
-            BatteryTableScope.AdminPassports => new BatteryTablePageViewModel
-            {
-                Query = query,
-                SearchAction = "/admin/passports",
-                SearchPlaceholder = "Search by battery ID, passport ID, serial, family, or cluster",
-                ScopeLabel = "Search and manage batteries through their passport history.",
-                EmptyLabel = "No batteries are currently available.",
-                ReturnUrl = $"/admin/passports{querySuffix}",
-                ShowCreateBattery = true,
-                ShowHelp = true
-            },
             BatteryTableScope.ClusterAdminPassports => new BatteryTablePageViewModel
             {
                 Query = query,
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -4,9 +4,6 @@
     var selectedTab = Model.SelectedTab;
     var credentialTab = Model.SelectedCredentialTab;
     var clustersById = Model.Clusters.ToDictionary(cluster => cluster.ClusterId, cluster => cluster, StringComparer.OrdinalIgnoreCase);
-    var query = Model.PassportsQuery ?? string.Empty;
-    var querySuffix = string.IsNullOrWhiteSpace(query) ? string.Empty : $"&q={Uri.EscapeDataString(query)}";
-    ViewData["AdminBatteriesQuerySuffix"] = querySuffix;
     var clusterCreateName = TempData["ClusterCreateName"]?.ToString() ?? string.Empty;
     var clusterCreateId = TempData["ClusterCreateId"]?.ToString() ?? string.Empty;
     var globalAccessOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
@@ -107,109 +104,6 @@
     @* data-generated-token-modal data-generated-token-copy data-generated-token-ok navigator.clipboard.writeText *@
     @await Html.PartialAsync("_GeneratedCredentialModal", Model.GeneratedCredential)
 
-    @if (selectedTab == "batteries")
-    {
-        <section class="bp-card bp-battery-table-toolbar mt-4">
-            <div class="d-flex justify-content-between flex-wrap gap-3 align-items-end">
-                <form action="@Model.BatteryTable.SearchAction" class="bp-search-form mb-0">
-                    <input type="hidden" name="tab" value="batteries" />
-                    <input
-                        name="q"
-                        value="@Model.BatteryTable.Query"
-                        placeholder="@Model.BatteryTable.SearchPlaceholder" />
-                    <button type="submit">Search</button>
-                </form>
-                <div class="bp-battery-toolbar-actions d-flex flex-wrap gap-2">
-                    <a href="/admin/help" class="bp-secondary-button bp-battery-toolbar-help">Help</a>
-                    <a href="/admin/batteries/new" class="bp-primary-button">Create battery</a>
-                </div>
-            </div>
-        </section>
-
-        @await Html.PartialAsync("_BatteryTable", Model.BatteryTable)
-    }
-
-    @if (selectedTab == "battery")
-    {
-        <section class="bp-card bp-table-card mt-4">
-            <div class="bp-table-scroll">
-                <table class="table table-hover align-middle bp-console-table bp-admin-cluster-assignment-table bp-mobile-card-table">
-                    <thead>
-                        <tr>
-                            <th>Battery ID</th>
-                            <th>Battery Family</th>
-                            <th>Battery Model</th>
-                            <th>Battery serial number</th>
-                            <th>Current cluster</th>
-                            <th>Assign</th>
-                            <th>Updated</th>
-                        </tr>
-                    </thead>
-                    <tbody>
-                        @if (Model.Passports.Count == 0)
-                        {
-                            <tr>
-                                <td class="text-secondary" colspan="7">No battery cluster assignments found.</td>
-                            </tr>
-                        }
-                        else
-                        {
-                            @for (var assignmentIndex = 0; assignmentIndex < Model.Passports.Count; assignmentIndex++)
-                            {
-                                var passport = Model.Passports[assignmentIndex];
-                                var updated = string.IsNullOrWhiteSpace(passport.UpdatedDate)
-                                    ? string.Empty
-                                    : passport.UpdatedDate.Length >= 10
-                                        ? passport.UpdatedDate.Substring(0, 10)
-                                        : passport.UpdatedDate;
-                                <tr>
-                                    <td data-label="Battery ID"><span class="bp-battery-id-cell bp-id-copy-row"><span class="bp-id-value">@passport.BatteryId</span>@await Html.PartialAsync("_CopyIdButton", passport.BatteryId)</span></td>
-                                    <td data-label="Battery Family">@passport.BatteryFamily</td>
-                                    <td data-label="Battery Model">@passport.BatteryVersion</td>
-                                    <td data-label="Battery serial number">@passport.BatterySerialNumber</td>
-                                    <td data-label="Current cluster">
-                                        <p class="mb-0">@passport.ClusterLabel</p>
-                                        @if (!string.IsNullOrWhiteSpace(passport.ClusterId))
-                                        {
-                                            <p class="mb-0 small text-secondary">@passport.ClusterId</p>
-                                        }
-                                    </td>
-                                    <td data-label="Assign">
-                                        <form method="post" action="/admin/clusters/assign-passport" class="bp-cluster-assignment-form">
-                                            @Html.AntiForgeryToken()
-                                            <input type="hidden" name="passportId" value="@passport.PassportId" />
-                                            <span class="bp-select-shell">
-                                                <select name="clusterId" class="form-select form-select-sm" aria-label="Cluster for @passport.BatteryId">
-                                                    @foreach (var cluster in Model.Clusters)
-                                                    {
-                                                        if (passport.ClusterId == cluster.ClusterId)
-                                                        {
-                                                            <option value="@cluster.ClusterId" selected>@cluster.Name</option>
-                                                        }
-                                                        else
-                                                        {
-                                                            <option value="@cluster.ClusterId">@cluster.Name</option>
-                                                        }
-                                                    }
-                                                </select>
-                                            </span>
-                                            <button type="submit" class="bp-action-button" aria-label="Save cluster assignment" title="Save cluster assignment" data-tooltip="Save cluster assignment" data-action-icon="save-assignment">
-                                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                    <path d="M20 6 9 17l-5-5" />
-                                                </svg>
-                                            </button>
-                                        </form>
-                                    </td>
-                                    <td data-label="Updated">@updated</td>
-                                </tr>
-                            }
-                        }
-                    </tbody>
-                </table>
-            </div>
-        </section>
-    }
-
     @if (selectedTab == "clusters")
     {
         var clusterRowIndex = 0;
diff --git a/web/Views/Admin/Help.cshtml b/web/Views/Admin/Help.cshtml
--- a/web/Views/Admin/Help.cshtml
+++ b/web/Views/Admin/Help.cshtml
@@ -37,9 +37,9 @@
                 <a href="#ready-checklist">Ready checklist</a>
             </nav>
             <nav class="bp-admin-help-primary-actions" aria-label="Primary admin actions">
-                <a href="/admin/passports/new" class="bp-primary-button">Create battery ID</a>
+                <a href="/admin/batteries/new" class="bp-primary-button">Create battery</a>
                 <a href="/admin/families" class="bp-secondary-button">Open battery families</a>
-                <a href="/admin/clusters?tab=batteries" class="bp-secondary-button">Open batteries</a>
+                <a href="/admin/batteries" class="bp-secondary-button">Open batteries</a>
             </nav>
         </div>
 
@@ -58,14 +58,14 @@
                     <tr>
                         <td><span class="bp-admin-help-workflow-name"><strong>Create battery ID</strong><small>Start a draft passport.</small></span></td>
                         <td>New battery or new demonstrator record.</td>
-                        <td><a href="/admin/passports/new">New battery ID</a></td>
+                        <td><a href="/admin/batteries/new">Create battery</a></td>
                         <td>Draft saved with Battery Family and Battery Model.</td>
                         <td class="bp-action-cell"><button type="button" class="bp-action-button bp-admin-help-detail-toggle" data-admin-help-detail-target="admin-detail-create" aria-expanded="false" aria-controls="admin-detail-create" aria-label="Show create battery ID details" title="Show create battery ID details" data-tooltip="Details"><svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg></button></td>
                     </tr>
                     <tr>
                         <td><span class="bp-admin-help-workflow-name"><strong>Fill battery information</strong><small>Complete required passport data.</small></span></td>
                         <td>Draft exists and needs battery-specific values.</td>
-                        <td><a href="/admin/clusters?tab=batteries">Batteries</a></td>
+                        <td><a href="/admin/batteries">Batteries</a></td>
                         <td>Required fields are complete for the saved product policy.</td>
                         <td class="bp-action-cell"><button type="button" class="bp-action-button bp-admin-help-detail-toggle" data-admin-help-detail-target="admin-detail-fill" aria-expanded="false" aria-controls="admin-detail-fill" aria-label="Show fill battery information details" title="Show fill battery information details" data-tooltip="Details"><svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg></button></td>
                     </tr>
diff --git a/web/Views/Shared/_AdminTabs.cshtml b/web/Views/Shared/_AdminTabs.cshtml
--- a/web/Views/Shared/_AdminTabs.cshtml
+++ b/web/Views/Shared/_AdminTabs.cshtml
@@ -1,7 +1,6 @@
 @model string
 @{
     var activeTab = Model ?? string.Empty;
-    var batteryQuerySuffix = ViewData["AdminBatteriesQuerySuffix"] as string ?? string.Empty;
     string TabClass(string tab) => activeTab.Equals(tab, StringComparison.OrdinalIgnoreCase) ? "bp-tab bp-tab-active" : "bp-tab";
     bool IsSelected(string tab) => activeTab.Equals(tab, StringComparison.OrdinalIgnoreCase);
 }
@@ -9,8 +8,7 @@
 <form class="bp-admin-mobile-tab-form mt-4" aria-label="Administration section">
     <label for="bp-admin-mobile-tab-select">Admin section</label>
     <select id="bp-admin-mobile-tab-select" data-admin-mobile-tab-select>
-        <option value="@($"/admin/clusters?tab=batteries{batteryQuerySuffix}")" selected="@IsSelected("batteries")">Batteries</option>
-        <option value="/admin/clusters?tab=battery" selected="@IsSelected("battery")">Battery cluster assignments</option>
+        <option value="/admin/batteries" selected="@IsSelected("batteries")">Batteries</option>
         <option value="/admin/clusters?tab=clusters" selected="@IsSelected("clusters")">Registered clusters</option>
         <option value="/admin/clusters?tab=users" selected="@IsSelected("users")">Users</option>
         <option value="/admin/clusters?tab=api-token-management" selected="@IsSelected("api-token-management")">API Token Management</option>
@@ -22,8 +20,7 @@
 </form>
 
 <nav class="bp-tab-row mt-4" aria-label="Administration tabs">
-    <a class="@TabClass("batteries")" href="@($"/admin/clusters?tab=batteries{batteryQuerySuffix}")">Batteries</a>
-    <a class="@TabClass("battery")" href="/admin/clusters?tab=battery">Battery cluster assignments</a>
+    <a class="@TabClass("batteries")" href="/admin/batteries">Batteries</a>
     <a class="@TabClass("clusters")" href="/admin/clusters?tab=clusters">Registered clusters</a>
     <a class="@TabClass("users")" href="/admin/clusters?tab=users">Users</a>
     <a class="@TabClass("api-token-management")" href="/admin/clusters?tab=api-token-management">API Token Management</a>
```

- [ ] **Step 4: Delete the files only the old pages used**

```bash
git rm -q web/Views/Admin/BatteryPassports.cshtml web/Views/Admin/Index.cshtml web/Views/Admin/NewPassport.cshtml web/Views/Admin/Passports.cshtml
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminEditGuidanceTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAdminWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryRouteAndRegistryTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DocumentAccessControlTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AdminMenuTests|FullyQualifiedName~BatteryPassWeb.Tests.ErrorHandlingRolloutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryCreateTests|FullyQualifiedName~BatteryPassWeb.Tests.ManualFeedbackRound3Tests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportCanonicalDataTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustDirtyStateTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:   201`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1839`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/AdminEditGuidanceTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AdminHelpPageTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/ConformanceLayoutTests.cs BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs BatteryPassWeb.Tests/DocumentAccessControlTests.cs BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/PassportCanonicalDataTests.cs BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs BatteryPassWeb.Tests/TrustDirtyStateTests.cs BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs web/Controllers/AdminController.cs web/Controllers/RegistryController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Models/ViewModels/BatteryViewModels.cs web/Services/BatteryTableService.cs web/Views/Admin/Clusters.cshtml web/Views/Admin/Help.cshtml web/Views/Shared/_AdminTabs.cshtml
git commit -m "feat(dpp): point Batteries at the new pages and remove the old battery and passport admin pages"
```


---

### Task 2: Fixed once published, one serial number per battery, and a cluster's batteries counted

The rules the old battery pages enforced, now on the new model (design 14, parity check of 29 Oct 2026). Two 2d-1 tests change: a published pack's warranty can no longer trigger a new version, and two packs can't share a serial number.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/VersioningTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PackRulesTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Dpp/Editing/PackCreationService.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

Patch `2d2-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs`, `BatteryPassWeb.Tests/Dpp/VersioningTests.cs`). Save the block as `../2d2-task2-tests.patch`, outside the worktree, and run `git apply ../2d2-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs b/BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs
--- a/BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs
@@ -57,7 +57,7 @@ public sealed class PackCreationServiceTests(MongoFixture mongo)
         var (_, service, seed) = await ServiceAsync();
 
         var first = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0002\"")));
-        var second = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0002\"")));
+        var second = await service.CreateAsync(Team, NewPack(seed, ("batterySerialNumber", "\"VPC9-0012\"")));
 
         Assert.NotEqual(
             first.Pack!.Values["uniqueProductIdentifier"].Value.GetValue<string>(),
diff --git a/BatteryPassWeb.Tests/Dpp/VersioningTests.cs b/BatteryPassWeb.Tests/Dpp/VersioningTests.cs
--- a/BatteryPassWeb.Tests/Dpp/VersioningTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/VersioningTests.cs
@@ -86,7 +86,7 @@ public sealed class VersioningTests(MongoFixture mongo)
         var rig = await PublicationTesting.PublishedRigAsync(mongo);
         await PublicationServiceTests.RemoveStoredValueAsync(rig, "thirdPartyServiceProvider"); // e.g. data damaged outside the app
 
-        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, PackChange(rig, ("warrantyPeriod", "\"P10Y\"")));
+        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(TargetKind.Model, "model-test", Values(("ratedCapacity", "210")), "form"));
 
         Assert.True(result.Saved); // the save itself stands
         var version = Assert.Single(await rig.Store.ListVersionsAsync(rig.PackId));
```

`BatteryPassWeb.Tests/Dpp/PackRulesTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// What the old battery pages enforced and the battery pages restore (design 14, parity check of 29 Oct 2026): a
/// battery's serial number, manufacturing date and warranty (#11, #17, #19) are fixed once its passport is published,
/// no two batteries share a serial number, and a cluster that is deleted counts and releases its batteries.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PackRulesTests(MongoFixture mongo)
{
    private static readonly ChangeSender Team = PublicationTesting.Team;

    [Theory]
    [InlineData("batterySerialNumber", "\"VPC8C05-6790\"")]
    [InlineData("manufacturingDate", "\"2026-08\"")]
    [InlineData("warrantyPeriod", "\"P6Y\"")]
    public async Task ADraft_ShouldLetTheTeamCorrectItsFixedValues(string key, string json)
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, key, json));

        Assert.True(result.Saved);
        Assert.Equal(KeyOutcome.Saved, result.Keys.Single().Outcome);
    }

    [Theory]
    [InlineData("batterySerialNumber", "\"VPC8C05-6790\"")]
    [InlineData("manufacturingDate", "\"2026-08\"")]
    [InlineData("warrantyPeriod", "\"P6Y\"")]
    public async Task OncePublished_FixedValuesShouldBeRefusedAndNoVersionMade(string key, string json)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, key, json));

        Assert.False(result.Saved);
        var refused = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, refused.Outcome);
        Assert.Equal(SavePipeline.FixedOncePublished, refused.Message);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task OncePublished_AFixedValueShouldNotBeRemovedEitherEvenIfConditional()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "form", Removals: ["warrantyPeriod"]));

        Assert.False(result.Saved);
        Assert.Equal(SavePipeline.FixedOncePublished, result.Keys.Single().Message);
        Assert.True((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values.ContainsKey("warrantyPeriod"));
    }

    [Fact]
    public async Task OncePublished_TheServiceProviderMayStillChangeAndPublishesAVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, "thirdPartyServiceProvider", "\"Other Hosting AB\""));

        Assert.True(result.Saved);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task OnlyAPacksOwnFixedValues_ShouldCount()
    {
        var fixedOnes = BatteryPassWeb.Dpp.Catalog.AttributeCatalog.All.Where(SavePipeline.IsFixedPackValue).Select(attribute => attribute.Number);

        Assert.Equal([11, 17, 19], fixedOnes);
    }

    [Theory]
    [InlineData("VPC9-7000")]
    [InlineData("vpc9-7000")]
    [InlineData("  VPC9-7000 ")]
    public async Task ACorrection_ShouldRefuseASerialNumberAnotherBatteryHas(string typed)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(OtherPack(rig, "VPC9-7000"));

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, "batterySerialNumber", JsonValue.Create(typed).ToJsonString()));

        Assert.False(result.Saved);
        Assert.Equal(SavePipeline.SerialNumberTaken, result.Keys.Single().Message);
    }

    [Fact]
    public async Task ACorrection_MayKeepTheBatterysOwnSerialNumberInAnotherCase()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(Team, Change(rig.PackId, "batterySerialNumber", "\"vpc8c05-6788\""));

        Assert.True(result.Saved);
    }

    [Theory]
    [InlineData("VPC8C05-6788")]
    [InlineData("vpc8c05-6788")]
    [InlineData(" VPC8C05-6788  ")]
    public async Task Creation_ShouldRefuseASerialNumberAnotherBatteryHas(string typed)
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var service = new PackCreationService(rig.Store, rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);

        var result = await service.CreateAsync(Team, new NewPack(
            rig.Seed.Sources.Batch.Id,
            rig.Seed.Sources.Operator.Id,
            null,
            new Dictionary<string, JsonNode?> { ["batterySerialNumber"] = JsonValue.Create(typed) }));

        Assert.Null(result.Pack);
        var serial = result.Keys.Single(key => key.Key == "batterySerialNumber");
        Assert.Equal(KeyOutcome.Rejected, serial.Outcome);
        Assert.Equal(SavePipeline.SerialNumberTaken, serial.Message);
        Assert.Single(await rig.Store.ListAsync<BatteryPack>());
    }

    [Fact]
    public async Task APack_ShouldMoveBetweenClustersAndLeaveWhenItsClusterIsCleared()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Store.InsertAsync(OtherPack(rig, "VPC9-7001"));

        Assert.True(await rig.Store.SetPackClusterAsync(rig.PackId, "cluster-north"));
        Assert.Equal(1, await rig.Store.CountPacksInClusterAsync("cluster-north"));
        Assert.Equal(1, await rig.Store.CountPacksInClusterAsync("cluster-test"));
        Assert.Equal(1, await rig.Store.ClearClusterAsync("cluster-test"));
        Assert.Equal(0, await rig.Store.CountPacksInClusterAsync("cluster-test"));
        Assert.True(await rig.Store.SetPackClusterAsync(rig.PackId, null));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.False(await rig.Store.SetPackClusterAsync("no-such-pack", "cluster-north"));
    }

    [Fact]
    public void DeletingACluster_ShouldCountItsBatteriesAndForceDeleteShouldReleaseThem()
    {
        var controller = File.ReadAllText(DppTestFiles.RepoFile("web", "Controllers", "AdminController.cs"));

        Assert.Contains("_dppStore.CountPacksInClusterAsync(clusterId", controller, StringComparison.Ordinal);
        Assert.Contains("batteryCount > 0", controller, StringComparison.Ordinal);
        Assert.Contains("_dppStore.ClearClusterAsync(clusterId", controller, StringComparison.Ordinal);
    }

    private static ChangeSet Change(string packId, string key, string json) =>
        new(TargetKind.Pack, packId, new Dictionary<string, JsonNode?> { [key] = JsonNode.Parse(json) }, "form");

    /// <summary>A second draft pack of the seed's batch, in the seed's cluster, with its own serial number.</summary>
    private static BatteryPack OtherPack(PublicationRig rig, string serialNumber)
    {
        var pack = new BatteryPack
        {
            Id = $"pack-{serialNumber}",
            BatchId = rig.Seed.Sources.Batch.Id,
            OperatorId = rig.Seed.Sources.Operator.Id,
            ClusterId = rig.Seed.Sources.Pack.ClusterId,
        };
        pack.Values["batterySerialNumber"] = DppStoreTesting.Value(JsonValue.Create(serialNumber));
        pack.Values["uniqueProductIdentifier"] = DppStoreTesting.Value(JsonValue.Create($"urn:test:battery:{serialNumber}"));
        return pack;
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackCreationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackRulesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.VersioningTests"`
Expected: the build fails: `SavePipeline` has no `FixedOncePublished`, `SerialNumberTaken` or `IsFixedPackValue` (CS0117), and `DppStore` has no `SetPackClusterAsync`, `CountPacksInClusterAsync` or `ClearClusterAsync` (CS1061).

- [ ] **Step 3: Apply the rules**

Patch `2d2-task2-code.patch` (`web/Controllers/AdminController.cs`, `web/Dpp/Editing/PackCreationService.cs`, `web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../2d2-task2-code.patch`, outside the worktree, and run `git apply ../2d2-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -1,5 +1,6 @@
 using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp.Audit;
+using BatteryPassWeb.Dpp.Store;
 using BatteryPassWeb.Models.Trust;
 using BatteryPassWeb.Models.ViewModels;
 using BatteryPassWeb.Services;
@@ -23,19 +24,22 @@ public class AdminController : Controller
     private readonly ExternalApiRepository _externalApiRepository;
     private readonly ApplicationAuditService _applicationAuditService;
     private readonly ApplicationSettingsService _applicationSettingsService;
+    private readonly DppStore _dppStore;
 
     public AdminController(
         PassportRepository passportRepository,
         ClusterRepository clusterRepository,
         ExternalApiRepository externalApiRepository,
         ApplicationAuditService applicationAuditService,
-        ApplicationSettingsService applicationSettingsService)
+        ApplicationSettingsService applicationSettingsService,
+        DppStore dppStore)
     {
         _passportRepository = passportRepository;
         _clusterRepository = clusterRepository;
         _externalApiRepository = externalApiRepository;
         _applicationAuditService = applicationAuditService;
         _applicationSettingsService = applicationSettingsService;
+        _dppStore = dppStore;
     }
 
     [HttpGet("")]
@@ -275,10 +279,11 @@ public class AdminController : Controller
     {
         var clusterId = Text(Request.Form, "clusterId");
         var passportCount = await _passportRepository.CountPassportsByClusterAsync(clusterId, cancellationToken);
+        var batteryCount = await _dppStore.CountPacksInClusterAsync(clusterId, cancellationToken);
         var membershipCount = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken);
-        if (passportCount > 0 || membershipCount > 0)
+        if (passportCount > 0 || batteryCount > 0 || membershipCount > 0)
         {
-            TempData["ErrorMessage"] = $"Cluster {clusterId} has {passportCount} linked passport(s) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
+            TempData["ErrorMessage"] = $"Cluster {clusterId} has {passportCount} linked passport(s), {batteryCount} linked battery(ies) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
             return Redirect("/admin/clusters?tab=clusters");
         }
 
@@ -306,6 +311,7 @@ public class AdminController : Controller
         }
 
         await _passportRepository.ClearPassportClusterAsync(clusterId, cancellationToken);
+        var batteriesCleared = await _dppStore.ClearClusterAsync(clusterId, cancellationToken);
         await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
         await AppendAdminAuditAsync(
             "cluster.deleted",
@@ -313,7 +319,8 @@ public class AdminController : Controller
             new BsonDocument
             {
                 ["clusterId"] = clusterId,
-                ["forceDelete"] = true
+                ["forceDelete"] = true,
+                ["batteriesCleared"] = batteriesCleared
             },
             "cluster",
             clusterId,
diff --git a/web/Dpp/Editing/PackCreationService.cs b/web/Dpp/Editing/PackCreationService.cs
--- a/web/Dpp/Editing/PackCreationService.cs
+++ b/web/Dpp/Editing/PackCreationService.cs
@@ -68,6 +68,15 @@ public sealed class PackCreationService(
             return new PackCreationResult(null, evaluation.Results, "the battery serial number (#11) is required");
         }
 
+        // Linked successor packs (step 4) will be the one exception (design 8.2).
+        if (await store.SerialNumberTakenAsync(evaluation.Accepted["batterySerialNumber"].GetValue<string>(), cancellationToken: cancellationToken))
+        {
+            var keys = evaluation.Results
+                .Select(result => result.Key == "batterySerialNumber" ? result with { Outcome = KeyOutcome.Rejected, Message = SavePipeline.SerialNumberTaken } : result)
+                .ToArray();
+            return new PackCreationResult(null, keys, "some values were rejected; nothing was saved");
+        }
+
         var now = clock.GetUtcNow().UtcDateTime;
         var pack = new BatteryPack
         {
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -84,6 +84,11 @@ public sealed class SavePipeline(
         }
 
         var evaluation = Evaluate(sender, changes.Target, target.ClusterId, changes.Values);
+        if (changes.Target == TargetKind.Pack)
+        {
+            evaluation = await CheckPackRulesAsync(changes.TargetId, evaluation, cancellationToken);
+        }
+
         var removals = await EvaluateRemovalsAsync(sender, changes, target, cancellationToken);
         var results = evaluation.Results.Concat(removals).ToArray();
         var removed = removals.Where(result => result.Outcome == KeyOutcome.Removed).Select(result => result.Key).ToArray();
@@ -157,6 +162,10 @@ public sealed class SavePipeline(
             {
                 results.Add(new KeyResult(key, KeyOutcome.Ignored, "no value to remove"));
             }
+            else if (changes.Target == TargetKind.Pack && IsFixedPackValue(attribute) && await store.GetLatestVersionNumberAsync(changes.TargetId, cancellationToken) > 0)
+            {
+                results.Add(new KeyResult(key, KeyOutcome.Rejected, FixedOncePublished));
+            }
             else if (attribute.Requirement != Requirement.Conditional && attribute.MandatoryFromFeb2027
                 && (published ??= await UsedByPublishedPassportAsync(changes.Target, changes.TargetId, cancellationToken)))
             {
@@ -171,6 +180,62 @@ public sealed class SavePipeline(
         return results;
     }
 
+    /// <summary>Why a pack's fixed value (#11, #17, #19) is refused after publication.</summary>
+    public const string FixedOncePublished = "fixed once the passport is published";
+
+    /// <summary>Why a serial number (#11) another battery already has is refused.</summary>
+    public const string SerialNumberTaken = "another battery already has this serial number";
+
+    /// <summary>
+    /// A pack's own fixed values (#11, #17, #19): the team corrects them while the passport is a draft; once a version
+    /// is published they stay as they are (parity check, design 14). #6 is the app's and never changes.
+    /// </summary>
+    public static bool IsFixedPackValue(AttributeDefinition attribute)
+    {
+        ArgumentNullException.ThrowIfNull(attribute);
+        return attribute.Storage == StorageLocation.Pack && attribute.Change == ChangeRule.Fixed && attribute.Editors.HasFlag(AttributeEditors.Team);
+    }
+
+    /// <summary>
+    /// The rules for a pack's own values: its fixed values can't change once a version has been published, and no two
+    /// batteries share a serial number (ignoring case). A refused key doesn't block the others (J-12).
+    /// </summary>
+    private async Task<Evaluation> CheckPackRulesAsync(string packId, Evaluation evaluation, CancellationToken cancellationToken)
+    {
+        var results = evaluation.Results.ToList();
+        var accepted = new Dictionary<string, JsonNode>(evaluation.Accepted, StringComparer.Ordinal);
+        bool? published = null;
+        for (var index = 0; index < results.Count; index++)
+        {
+            var result = results[index];
+            if (result.Outcome != KeyOutcome.Saved || AttributeCatalog.FindByProperty(result.Key) is not { } attribute)
+            {
+                continue;
+            }
+
+            string? refusal = null;
+            if (IsFixedPackValue(attribute) && (published ??= await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0))
+            {
+                refusal = FixedOncePublished;
+            }
+            else if (attribute.Number == 11
+                && accepted[result.Key] is JsonValue serial
+                && serial.TryGetValue<string>(out var text)
+                && await store.SerialNumberTakenAsync(text, packId, cancellationToken))
+            {
+                refusal = SerialNumberTaken;
+            }
+
+            if (refusal is not null)
+            {
+                results[index] = result with { Outcome = KeyOutcome.Rejected, Message = refusal };
+                accepted.Remove(result.Key);
+            }
+        }
+
+        return new Evaluation(results, accepted);
+    }
+
     private async Task<bool> UsedByPublishedPassportAsync(TargetKind target, string id, CancellationToken cancellationToken)
     {
         var packIds = target switch
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -1,6 +1,8 @@
+using System.Text.RegularExpressions;
 using BatteryPassWeb.Dpp.Assembly;
 using BatteryPassWeb.Dpp.Values;
 using BatteryPassWeb.Services;
+using MongoDB.Bson;
 using MongoDB.Driver;
 
 namespace BatteryPassWeb.Dpp.Store;
@@ -150,6 +152,59 @@ public sealed class DppStore
         return await Collection<BatteryPack>().Find(Builders<BatteryPack>.Filter.Eq(SerialNumberField, serialNumber)).ToListAsync(cancellationToken);
     }
 
+    /// <summary>
+    /// True when another pack already has this serial number (#11), ignoring case and surrounding spaces, as the old
+    /// battery pages judged it. <paramref name="exceptPackId"/> is the pack being corrected, which may keep its own.
+    /// </summary>
+    public async Task<bool> SerialNumberTakenAsync(string serialNumber, string? exceptPackId = null, CancellationToken cancellationToken = default)
+    {
+        var wanted = serialNumber?.Trim();
+        if (_database is null || string.IsNullOrEmpty(wanted))
+        {
+            return false;
+        }
+
+        var sameSerial = Builders<BatteryPack>.Filter.Regex(SerialNumberField, new BsonRegularExpression($@"^\s*{Regex.Escape(wanted)}\s*$", "i"));
+        var filter = exceptPackId is null ? sameSerial : sameSerial & Builders<BatteryPack>.Filter.Ne("_id", exceptPackId);
+        return await Collection<BatteryPack>().Find(filter).Limit(1).AnyAsync(cancellationToken);
+    }
+
+    /// <summary>Moves a pack to another cluster, or to none; false when no pack has that id. The cluster isn't passport data.</summary>
+    public async Task<bool> SetPackClusterAsync(string packId, string? clusterId, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<BatteryPack>().UpdateOneAsync(
+            pack => pack.Id == packId,
+            Builders<BatteryPack>.Update.Set(pack => pack.ClusterId, clusterId),
+            cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
+    /// <summary>How many packs belong to this cluster.</summary>
+    public async Task<long> CountPacksInClusterAsync(string clusterId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null || string.IsNullOrWhiteSpace(clusterId))
+        {
+            return 0;
+        }
+
+        return await Collection<BatteryPack>().CountDocumentsAsync(pack => pack.ClusterId == clusterId, cancellationToken: cancellationToken);
+    }
+
+    /// <summary>Takes every pack of this cluster out of it (they then have no cluster); returns how many.</summary>
+    public async Task<long> ClearClusterAsync(string clusterId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null || string.IsNullOrWhiteSpace(clusterId))
+        {
+            return 0;
+        }
+
+        var result = await Collection<BatteryPack>().UpdateManyAsync(
+            pack => pack.ClusterId == clusterId,
+            Builders<BatteryPack>.Update.Set(pack => pack.ClusterId, null),
+            cancellationToken: cancellationToken);
+        return result.ModifiedCount;
+    }
+
     /// <summary>The pack with this unique product identifier (#6), if any.</summary>
     public async Task<BatteryPack?> FindPackByUniqueProductIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
     {
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackCreationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PackRulesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.VersioningTests"`
Expected: `Passed!  - Failed:     0, Passed:    37`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1857`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PackCreationServiceTests.cs BatteryPassWeb.Tests/Dpp/VersioningTests.cs BatteryPassWeb.Tests/Dpp/PackRulesTests.cs web/Controllers/AdminController.cs web/Dpp/Editing/PackCreationService.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Store/DppStore.cs
git commit -m "feat(dpp): fix a published battery's serial number, date and warranty, refuse duplicate serial numbers and count a cluster's batteries"
```


---

### Task 3: The batteries pages' services

Loading the list, the new-battery form and the battery page, and the changes that aren't passport values. `RecordEditor` learns a pack's own values and its live values; `PublicationService` runs its checks without publishing; the audit log names the new events.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/BatteryAuditLinesTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/BatteryPagesTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/BatteryTesting.cs`
- Create: `web/Dpp/Pages/BatteryPages.cs`
- Modify: `web/Dpp/Audit/AuditLines.cs`
- Modify: `web/Dpp/Documents/DocumentService.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Dpp/Pages/ProductDataPages.cs`
- Modify: `web/Dpp/Pages/RecordEditor.cs`
- Modify: `web/Dpp/Publication/PublicationService.cs`

- [ ] **Step 1: Write the failing tests**

Patch `2d2-task3-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`). Save the block as `../2d2-task3-tests.patch`, outside the worktree, and run `git apply ../2d2-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
--- a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
@@ -22,6 +22,7 @@ public sealed class DppServiceWiringTests
             .AddSingleton(Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }))
             .AddSingleton<MongoContext>()
             .AddSingleton<ApplicationAuditService>()
+            .AddSingleton<ClusterRepository>()
             .AddSingleton(new DemoSigningKeyService(ECDsa.Create(ECCurve.NamedCurves.nistP256)))
             .AddDpp()
             .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
@@ -36,6 +37,7 @@ public sealed class DppServiceWiringTests
         Assert.Same(provider.GetRequiredService<PublicationService>(), Assert.Single(provider.GetServices<IPackChangeListener>()));
         Assert.NotNull(provider.GetRequiredService<RecordEditor>());
         Assert.Same(provider.GetRequiredService<AttributeForms>(), provider.GetRequiredService<RecordEditor>().Forms);
+        Assert.NotNull(provider.GetRequiredService<BatteryPages>());
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/BatteryAuditLinesTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages' events in the audit log: plain sentences, and a link to the battery's page.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryAuditLinesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    private static async Task<IReadOnlyList<AuditLine>> LinesAsync(PublicationRig rig)
    {
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        return await new AuditLines(rig.Store, new DocumentStore(rig.Context), AttributeFormsTests.Forms, rig.Context).DescribeAsync(events);
    }

    [Fact]
    public async Task AClusterMove_ShouldSayFromWhereToWhereAndLinkToTheBattery()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await BatteryTesting.Pages(rig).SetClusterAsync(Who, rig.PackId, BatteryTesting.South);

        var line = Assert.Single(await LinesAsync(rig), line => line.EventType == BatteryPages.ClusterChangedEvent);

        Assert.Equal($"{Who} moved battery VPC8C05-6788 to another cluster", line.Text);
        Assert.Equal("Customer North → Customer South", line.Change);
        Assert.Contains(line.Facts, fact => fact.Term == "Record" && fact.Href == BatteryPages.BatteryHref(rig.PackId));
    }

    [Fact]
    public async Task ARemovedDraft_ShouldBeNamedByItsSerialNumber()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await BatteryTesting.Pages(rig).RemoveDraftAsync(Who, rig.PackId);

        var line = Assert.Single(await LinesAsync(rig), line => line.EventType == ProductDataPages.RemovedEvent);

        Assert.Equal($"{Who} removed battery VPC8C05-6788", line.Text);
    }
}
```

`BatteryPassWeb.Tests/Dpp/BatteryPagesTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages' services (step 2d-2): the list and its search, a new battery, and the battery page.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteryPagesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    [Fact]
    public void ABattery_ShouldTypeItsOwnValuesAndItsLiveValuesInTwoForms()
    {
        Assert.Equal([11, 13, 17, 19], RecordEditor.AttributesOf(TargetKind.Pack).Select(attribute => attribute.Number));
        Assert.Equal([63, 83, 87], RecordEditor.LiveAttributes.Select(attribute => attribute.Number));
        Assert.Equal([13], BatteryPages.ValueAttributes(published: true).Select(attribute => attribute.Number));
        Assert.Equal([11, 13, 17, 19], BatteryPages.ValueAttributes(published: false).Select(attribute => attribute.Number));
    }

    [Fact]
    public async Task TheList_ShouldNameEachBatterysModelBatchClusterAndPassport()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        var draft = Assert.Single((await pages.ListAsync(null)).Rows);
        Assert.Equal("VPC8C05-6788", draft.SerialNumber);
        Assert.Equal(rig.Seed.Sources.Model.Name, draft.ModelName);
        Assert.Equal($"2026 · {rig.Seed.Sources.Site.Name}", draft.BatchLabel);
        Assert.Equal("Customer North", draft.ClusterLabel);
        Assert.True(draft.IsDraft);

        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        Assert.Equal(1, Assert.Single((await pages.ListAsync(null)).Rows).Active?.Number);
    }

    [Theory]
    [InlineData("VPC8C05-6788", true)] // serial number
    [InlineData("c05-67", true)] // part of one, any case
    [InlineData("urn:test:battery", true)] // battery ID (#6)
    [InlineData("TEST-DPP", true)] // passport ID (#5)
    [InlineData("customer north", true)] // cluster name
    [InlineData("cluster-test", true)] // cluster ID
    [InlineData("Customer South", false)]
    [InlineData("nothing like it", false)]
    public async Task TheSearch_ShouldFindPassportIdsBatteryIdsSerialNumbersAndClusters(string query, bool found)
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = await BatteryTesting.Pages(rig).ListAsync(query);

        Assert.Equal(found ? 1 : 0, page.Rows.Count);
        Assert.Equal(1, page.Total);
        Assert.Equal(query.Trim(), page.Query);
    }

    [Fact]
    public async Task ANewBattery_ShouldBeADraftWithOrWithoutACluster()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        var (without, _) = await pages.CreateAsync(Who, rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, "", Form(("batterySerialNumber", "VPC9-1001")));
        var (with, _) = await pages.CreateAsync(Who, rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, BatteryTesting.South, Form(("batterySerialNumber", "VPC9-1002"), ("warrantyPeriod", "P8Y")));

        Assert.Null(without.Problem);
        Assert.Null(without.Pack!.ClusterId);
        Assert.Equal(BatteryTesting.South, with.Pack!.ClusterId);
        Assert.Equal("P8Y", (await rig.Store.GetAsync<BatteryPack>(with.Pack.Id))!.Values["warrantyPeriod"].Value.GetValue<string>());
        Assert.Equal(0, await rig.Store.GetLatestVersionNumberAsync(with.Pack.Id));
    }

    [Theory]
    [InlineData("", "operator-test", "cluster-test", "choose the batch")]
    [InlineData("batch-test-2026", "", "cluster-test", "choose the economic operator")]
    [InlineData("batch-test-2026", "operator-test", "cluster-gone", "cluster cluster-gone doesn't exist")]
    public async Task ANewBattery_ShouldNeedABatchAnOperatorAndAClusterThatExists(string batch, string economicOperator, string cluster, string problem)
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var (result, post) = await BatteryTesting.Pages(rig).CreateAsync(Who, batch, economicOperator, cluster, Form(("batterySerialNumber", "VPC9-1003")));

        Assert.Null(result.Pack);
        Assert.Contains(problem, result.Problem, StringComparison.Ordinal);
        Assert.Equal("VPC9-1003", post.Texts[BatteryTesting.Field("batterySerialNumber")]); // what was typed stays
        Assert.Single(await rig.Store.ListAsync<BatteryPack>());
    }

    [Fact]
    public async Task ADraftsPage_ShouldSayWhatIsMissingUnderTheRecordThatHoldsIt()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<BatteryPack>(rig.PackId, new Dictionary<string, AttributeValue>(), ["thirdPartyServiceProvider", "temperatureInformation"]);
        await rig.Store.SetValuesAsync<BatteryModel>(rig.Seed.Sources.Model.Id, new Dictionary<string, AttributeValue>(), ["batteryMass"]);

        var page = await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId);

        Assert.NotNull(page);
        Assert.False(page.Publication.IsPublished);
        Assert.False(page.Publication.Ready);
        Assert.Collection(
            page.Publication.Missing,
            group => Assert.Equal(("This battery", "/admin/batteries/pack-VPC8C05-6788#values", 13), (group.Record, group.Href, group.Attributes.Single().Number)),
            group => Assert.Equal(("This battery's live values", "/admin/batteries/pack-VPC8C05-6788#live", 87), (group.Record, group.Href, group.Attributes.Single().Number)),
            group => Assert.Equal(($"Model {rig.Seed.Sources.Model.Name}", $"{ProductDataPages.FamilyHref(rig.Seed.Sources.Model.FamilyId, rig.Seed.Sources.Model.Id)}#editor", 21), (group.Record, group.Href, group.Attributes.Single().Number)));
        Assert.Equal(3, page.Publication.MissingCount);
        Assert.Equal([11, 13, 17, 19], page.Values.Sections.SelectMany(section => section.Fields).Select(field => field.Attribute.Number));
        Assert.Null(page.Removal.Blocked);
        Assert.Empty(page.Versions);
    }

    [Fact]
    public async Task ADraftWithEverythingNeededByFebruary2027_ShouldBeReadyAndTheChecksShouldPublishNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        await rig.Store.SetValuesAsync<ProductionBatch>(rig.Seed.Sources.Batch.Id, new Dictionary<string, AttributeValue>(), ["carbonFootprintPerformanceClass"]);

        var page = await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId);

        Assert.True(page!.Publication.Ready);
        Assert.Equal(35, Assert.Single(page.Publication.NeededLater).Number); // required only after February 2027
        Assert.Empty(await rig.Store.ListVersionsAsync(rig.PackId));
    }

    [Fact]
    public async Task APublishedBatterysPage_ShouldLockItsFixedValuesAndListItsVersions()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var page = await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId);

        Assert.True(page!.Publication.IsPublished);
        Assert.Equal([13], page.Values.Sections.SelectMany(section => section.Fields).Select(field => field.Attribute.Number));
        Assert.Equal(1, page.Values.PublishedPassports);
        var version = Assert.Single(page.Versions);
        Assert.True(version.SignatureHolds);
        Assert.NotNull(page.Removal.Blocked);
        Assert.Equal("Customer North", page.ClusterLabel);
    }

    [Fact]
    public async Task LiveValues_ShouldKeepTheirMeasurementTimeAndMakeNoNewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("stateOfChargeSoC")] = "55";
        var measured = new DateTime(2026, 10, 29, 6, 30, 0, DateTimeKind.Utc);

        var (_, save) = (await pages.SaveAsync(Who, rig.PackId, liveValues: true, PageTesting.Field(form), PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null), measured, confirmed: false))!.Value;

        Assert.Equal(EditorOutcome.Saved, save.Outcome);
        var soc = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["stateOfChargeSoC"];
        Assert.Equal(55, soc.Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(measured, soc.GeneratedAtUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task ALiveFormLeftOpen_ShouldNotUndoANewerValueFromTheBatterySystem()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);
        var shown = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var stamp = PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, shown, null);
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, shown);
        await rig.Pipeline.SaveAsync(
            ChangeSender.Feed("bms@example.com", BatteryTesting.North),
            new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?> { ["stateOfChargeSoC"] = 61 }, "feed"));
        form[BatteryTesting.Field("temperatureInformation")] = "19";

        var (_, save) = (await pages.SaveAsync(Who, rig.PackId, liveValues: true, PageTesting.Field(form), stamp, null, confirmed: false))!.Value;

        Assert.Equal(1, save.SavedCount);
        var values = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        Assert.Equal(61, values["stateOfChargeSoC"].Value["percentageValue"]!.GetValue<int>());
        Assert.Equal(19, values["temperatureInformation"].Value["celsiusValue"]!.GetValue<int>());
    }

    [Fact]
    public async Task TheCluster_ShouldMoveToARegisteredOneOrNoneWithoutANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var pages = BatteryTesting.Pages(rig);

        Assert.Equal("cluster cluster-gone doesn't exist", await pages.SetClusterAsync(Who, rig.PackId, "cluster-gone"));
        Assert.Equal("it is already there", await pages.SetClusterAsync(Who, rig.PackId, BatteryTesting.North));
        Assert.Null(await pages.SetClusterAsync(Who, rig.PackId, BatteryTesting.South));
        Assert.Equal(BatteryTesting.South, (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Null(await pages.SetClusterAsync(Who, rig.PackId, ""));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ClusterId);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Equal(2, events.Count(entry => entry["eventType"].AsString == BatteryPages.ClusterChangedEvent));
    }

    [Fact]
    public async Task ADraft_ShouldBeRemovedWithItsAccidentReports()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var upload = await PageTesting.Documents(rig).AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("report.pdf", DocumentTesting.Pdf()));
        Assert.Null(upload.Problem);

        var (problem, serial) = await BatteryTesting.Pages(rig).RemoveDraftAsync(Who, rig.PackId);

        Assert.Null(problem);
        Assert.Equal("VPC8C05-6788", serial);
        Assert.Null(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, DocumentService.Accidents));
    }

    [Fact]
    public async Task APublishedBattery_ShouldNotBeRemoved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();

        var (problem, _) = await BatteryTesting.Pages(rig).RemoveDraftAsync(Who, rig.PackId);

        Assert.Equal("its passport is published, so it stays with its versions", problem);
        Assert.NotNull(await rig.Store.GetAsync<BatteryPack>(rig.PackId));
    }

    [Fact]
    public async Task UploadedFiles_ShouldBeCheckedAgainstTheirSha256()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var documents = new DocumentStore(rig.Context);
        await PageTesting.Documents(rig).AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("report.pdf", DocumentTesting.Pdf("collision")));
        var tampered = new DppDocument
        {
            Id = "doc-tampered",
            OwnerKind = TargetKind.Pack,
            OwnerId = rig.PackId,
            Property = DocumentService.Accidents,
            FileName = "other.pdf",
            ContentType = "application/pdf",
            Length = 10,
            Sha256 = new string('0', 64),
            FileId = await documents.UploadFileAsync("other.pdf", DocumentTesting.Pdf("changed")),
            Title = "Changed later",
            UploadedAtUtc = DppStoreTesting.Now.UtcDateTime,
            UploadedBy = Who,
        };
        await documents.InsertAsync(tampered);

        var files = (await BatteryTesting.Pages(rig).BatteryAsync(rig.PackId))!.Publication.Files;

        Assert.Equal(2, files.Count);
        Assert.True(files.Single(file => file.Document.FileName == "report.pdf").Unchanged);
        Assert.False(files.Single(file => file.Document.Id == "doc-tampered").Unchanged);
    }

    private static Func<string, string?> Form(params (string Property, string Text)[] fields) =>
        PageTesting.Field(fields.ToDictionary(field => BatteryTesting.Field(field.Property), field => field.Text, StringComparer.Ordinal));
}
```

`BatteryPassWeb.Tests/Dpp/BatteryTesting.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages around a publication rig, with two registered clusters, and their forms as the page shows them.</summary>
internal static class BatteryTesting
{
    public const string North = "cluster-test";
    public const string South = "cluster-south";

    /// <summary>Registers the seed pack's cluster ("Customer North") and a second one ("Customer South").</summary>
    public static async Task<PublicationRig> WithClustersAsync(this Task<PublicationRig> rigTask)
    {
        var rig = await rigTask;
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterAsync(North, "Customer North");
        await clusters.UpsertClusterAsync(South, "Customer South");
        return rig;
    }

    public static PackCreationService Creation(PublicationRig rig) =>
        new(rig.Store, rig.Pipeline, new TestIdentifierScheme(), DocumentTesting.Links, new ApplicationAuditService(rig.Context), rig.Clock);

    public static BatteryPages Pages(PublicationRig rig) => new(
        rig.Store,
        PageTesting.Editor(rig),
        Creation(rig),
        rig.Publication,
        new DocumentStore(rig.Context),
        DocumentTesting.Links,
        new ClusterRepository(rig.Context),
        new ApplicationAuditService(rig.Context));

    /// <summary>The fields of a form that shows these attributes, as the page shows them.</summary>
    public static Dictionary<string, string> ShownForm(IEnumerable<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            foreach (var (name, text) in FormValues.Show(AttributeFormsTests.Forms.For(attribute), stored.GetValueOrDefault(attribute.Property)?.Value))
            {
                texts[name] = text;
            }
        }

        return texts;
    }

    public static string Field(string property) => $"{AttributeForm.Prefix}{property}";
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteryAuditLinesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteryPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: the build fails because `BatteryPages` doesn't exist yet (CS0246).

- [ ] **Step 3: Write `BatteryPages.cs`**

`web/Dpp/Pages/BatteryPages.cs`:

```csharp
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>A registered cluster a battery can belong to.</summary>
public sealed record ClusterChoice(string Id, string Name);

/// <summary>One battery in the list: what it is, where it belongs and how far its passport is.</summary>
public sealed record BatteryRow(
    string PackId,
    string SerialNumber,
    string Identifier,
    string FamilyName,
    string ModelName,
    string BatchLabel,
    string? ClusterId,
    string ClusterLabel,
    PassportVersion? Active)
{
    public bool IsDraft => Active is null;
}

public sealed record BatteriesPage(IReadOnlyList<BatteryRow> Rows, string Query, int Total);

/// <summary>A batch a new battery can be made in, named by its family, model, year and site.</summary>
public sealed record BatchChoice(string BatchId, string FamilyName, string ModelName, int Year, string SiteName)
{
    public string Label => $"{ModelName} · {Year} · {SiteName}";
}

/// <summary>The form for a new battery: where it comes from, and its own values as typed.</summary>
public sealed record NewBatteryPage(
    IReadOnlyList<BatchChoice> Batches,
    IReadOnlyList<EconomicOperator> Operators,
    IReadOnlyList<ClusterChoice> Clusters,
    RecordEditorView Values,
    string? BatchId,
    string? OperatorId,
    string? ClusterId,
    bool LinksConfigured);

/// <summary>Values the passport still needs, grouped by the record that holds them, with a link to its editor.</summary>
public sealed record MissingGroup(string Record, string Href, IReadOnlyList<AttributeDefinition> Attributes);

/// <summary>An uploaded file the passport uses, and whether its bytes still match the SHA-256 taken at upload.</summary>
public sealed record FileCheck(DppDocument Document, string Owner, bool Unchanged);

/// <summary>Where the battery's passport stands: published or not, what blocks it, what is needed later, and its files.</summary>
public sealed record PublicationView(
    PassportVersion? Active,
    IReadOnlyList<MissingGroup> Missing,
    IReadOnlyList<string> Problems,
    IReadOnlyList<AttributeDefinition> NeededLater,
    IReadOnlyList<FileCheck> Files)
{
    public bool IsPublished => Active is not null;

    public bool Ready => Missing.Count == 0 && Problems.Count == 0;

    public int MissingCount => Missing.Sum(group => group.Attributes.Count);
}

/// <summary>One published version of the passport and whether its signature still holds.</summary>
public sealed record VersionRow(PassportVersion Version, bool SignatureHolds);

/// <summary>Everything the battery page shows.</summary>
public sealed record BatteryPage(
    BatteryPack Pack,
    PassportSources Sources,
    BatteryFamily Family,
    string SerialNumber,
    string Identifier,
    string ClusterLabel,
    IReadOnlyList<ClusterChoice> Clusters,
    RecordEditorView Values,
    RecordEditorView Live,
    PublicationView Publication,
    IReadOnlyList<VersionRow> Versions,
    IReadOnlyList<DppDocument> AccidentReports,
    RemovalView Removal)
{
    public string BatchLabel => $"{Sources.Batch.Year} · {Sources.Site.Name}";
}

/// <summary>
/// The team's batteries pages (design 12 and 14, step 2d-2): the list with its search, a new battery, and the battery
/// page with its values, live values, cluster, publication checks, versions and accident reports. Values are saved by
/// <see cref="RecordEditor"/> through the save pipeline; this class loads the pages and makes the changes that aren't
/// passport values (the cluster, publishing, removing a draft).
/// </summary>
public sealed class BatteryPages(
    DppStore store,
    RecordEditor editor,
    PackCreationService creation,
    PublicationService publication,
    DocumentStore documents,
    DppLinks links,
    ClusterRepository clusters,
    ApplicationAuditService audit)
{
    public const string ClusterChangedEvent = "dpp.pack-cluster-changed";

    public static string BatteryHref(string packId) => $"/admin/batteries/{Uri.EscapeDataString(packId)}";

    /// <summary>
    /// Every battery, newest model first by name, filtered by <paramref name="query"/>: a passport ID (#5, any version), a
    /// battery ID (#6), a serial number (#11) or a cluster's ID or name, ignoring case; part of one is enough.
    /// </summary>
    public async Task<BatteriesPage> ListAsync(string? query, CancellationToken cancellationToken = default)
    {
        var packs = await store.ListAsync<BatteryPack>(cancellationToken);
        var batches = (await store.ListAsync<ProductionBatch>(cancellationToken)).ToDictionary(batch => batch.Id, StringComparer.Ordinal);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).ToDictionary(model => model.Id, StringComparer.Ordinal);
        var families = (await store.ListAsync<BatteryFamily>(cancellationToken)).ToDictionary(family => family.Id, StringComparer.Ordinal);
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).ToDictionary(site => site.Id, StringComparer.Ordinal);
        var clusterNames = (await ClustersAsync(cancellationToken)).ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        var versions = (await store.ListAsync<PassportVersion>(cancellationToken)).ToLookup(version => version.PackId, StringComparer.Ordinal);

        var rows = new List<BatteryRow>();
        foreach (var pack in packs)
        {
            var batch = batches.GetValueOrDefault(pack.BatchId);
            var model = batch is null ? null : models.GetValueOrDefault(batch.ModelId);
            var family = model is null ? null : families.GetValueOrDefault(model.FamilyId);
            var site = batch is null ? null : sites.GetValueOrDefault(batch.SiteId);
            var active = versions[pack.Id].Where(version => version.DppStatus == DppStatuses.Active).MaxBy(version => version.Number);
            rows.Add(new BatteryRow(
                pack.Id,
                Text(pack, "batterySerialNumber") ?? pack.Id,
                Text(pack, "uniqueProductIdentifier") ?? string.Empty,
                family?.Name ?? "Unknown family",
                model?.Name ?? "Unknown model",
                batch is null ? "Unknown batch" : $"{batch.Year} · {site?.Name ?? batch.SiteId}",
                pack.ClusterId,
                ClusterLabel(pack.ClusterId, clusterNames),
                active));
        }

        var wanted = query?.Trim() ?? string.Empty;
        var shown = rows
            .Where(row => wanted.Length == 0 || Matches(row, wanted, versions[row.PackId]))
            .OrderBy(row => row.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ModelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.SerialNumber, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new BatteriesPage(shown, wanted, rows.Count);
    }

    /// <summary>True when the search text is part of the battery's passport IDs, battery ID, serial number or cluster.</summary>
    public static bool Matches(BatteryRow row, string query, IEnumerable<PassportVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(versions);
        bool Has(string? text) => text is not null && text.Contains(query, StringComparison.OrdinalIgnoreCase);
        return Has(row.SerialNumber)
            || Has(row.Identifier)
            || Has(row.ClusterId)
            || (row.ClusterId is not null && Has(row.ClusterLabel))
            || versions.Any(version => Has(version.DigitalProductPassportId));
    }

    /// <summary>The new-battery form; with <paramref name="post"/>, as typed, with the refused values marked.</summary>
    public async Task<NewBatteryPage> NewAsync(
        string? batchId = null,
        string? operatorId = null,
        string? clusterId = null,
        EditorState? state = null,
        CancellationToken cancellationToken = default)
    {
        var batches = await store.ListAsync<ProductionBatch>(cancellationToken);
        var models = (await store.ListAsync<BatteryModel>(cancellationToken)).ToDictionary(model => model.Id, StringComparer.Ordinal);
        var families = (await store.ListAsync<BatteryFamily>(cancellationToken)).ToDictionary(family => family.Id, StringComparer.Ordinal);
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).ToDictionary(site => site.Id, StringComparer.Ordinal);
        var choices = batches
            .Where(batch => models.ContainsKey(batch.ModelId))
            .Select(batch =>
            {
                var model = models[batch.ModelId];
                return new BatchChoice(
                    batch.Id,
                    families.GetValueOrDefault(model.FamilyId)?.Name ?? "Unknown family",
                    model.Name,
                    batch.Year,
                    sites.GetValueOrDefault(batch.SiteId)?.Name ?? batch.SiteId);
            })
            .OrderBy(choice => choice.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.ModelName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(choice => choice.Year)
            .ThenBy(choice => choice.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var operators = (await store.ListAsync<EconomicOperator>(cancellationToken)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var empty = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        var sections = await editor.SectionsAsync(TargetKind.Pack, string.Empty, empty, state?.Post, state?.Save.Refusals(editor.Forms), cancellationToken);
        var values = new RecordEditorView(TargetKind.Pack, string.Empty, string.Empty, "/admin/batteries", "/admin/batteries", sections, [], 0, editor.Forms.Languages, string.Empty, new RemovalView(string.Empty, null));
        return new NewBatteryPage(choices, operators, await ClustersAsync(cancellationToken), values, batchId, operatorId, clusterId, links.IsConfigured);
    }

    /// <summary>
    /// Creates a draft battery (design 7.1). The cluster is optional (decision of 29 Oct 2026) but must exist; the
    /// values go through the same checks as any save, and a serial number another battery has is refused.
    /// </summary>
    public async Task<(PackCreationResult Result, EditorPost Post)> CreateAsync(
        string who,
        string? batchId,
        string? operatorId,
        string? clusterId,
        Func<string, string?> field,
        CancellationToken cancellationToken = default)
    {
        var post = editor.Read(TargetKind.Pack, new Dictionary<string, AttributeValue>(StringComparer.Ordinal), field);
        if (string.IsNullOrWhiteSpace(batchId))
        {
            return (Problem("choose the batch the battery was made in"), post);
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            return (Problem("choose the economic operator that places it on the market"), post);
        }

        var cluster = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId.Trim();
        if (cluster is not null && await clusters.GetClusterByIdAsync(cluster, cancellationToken) is null)
        {
            return (Problem($"cluster {cluster} doesn't exist; choose a registered cluster or none"), post);
        }

        var values = post.Changed.ToDictionary(entry => entry.Key, entry => (System.Text.Json.Nodes.JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal);
        return (await creation.CreateAsync(ChangeSender.Team(who), new NewPack(batchId, operatorId, cluster, values), cancellationToken), post);

        static PackCreationResult Problem(string problem) => new(null, [], problem);
    }

    /// <summary>The battery page; with <paramref name="values"/> or <paramref name="live"/>, that form as typed after a refused save.</summary>
    public async Task<BatteryPage?> BatteryAsync(string packId, EditorState? values = null, EditorState? live = null, CancellationToken cancellationToken = default)
    {
        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
        var family = sources is null ? null : await store.GetAsync<BatteryFamily>(sources.Model.FamilyId, cancellationToken);
        if (sources is null || family is null)
        {
            return null;
        }

        var pack = sources.Pack;
        var versions = await store.ListVersionsAsync(packId, cancellationToken);
        var active = versions.Where(version => version.DppStatus == DppStatuses.Active).MaxBy(version => version.Number);
        var href = BatteryHref(packId);
        var valueAttributes = ValueAttributes(published: versions.Count > 0);
        var valueSections = await editor.SectionsAsync(TargetKind.Pack, packId, valueAttributes, pack.Values, values?.Post, values?.Save.Refusals(editor.Forms), cancellationToken);
        var valuesView = new RecordEditorView(
            TargetKind.Pack,
            packId,
            Text(pack, "batterySerialNumber") ?? packId,
            href,
            href,
            valueSections,
            values?.Save.Outcome == EditorOutcome.NeedsConfirmation ? values.Save.Affected : [],
            active is null ? 0 : 1,
            editor.Forms.Languages,
            editor.Stamp(valueAttributes, pack.Values, null),
            new RemovalView(string.Empty, null));
        var liveSections = await editor.SectionsAsync(TargetKind.Pack, packId, RecordEditor.LiveAttributes, pack.Values, live?.Post, live?.Save.Refusals(editor.Forms), cancellationToken);
        var liveView = new RecordEditorView(
            TargetKind.Pack,
            packId,
            Text(pack, "batterySerialNumber") ?? packId,
            $"{href}/live",
            $"{href}#live",
            liveSections,
            [],
            0,
            editor.Forms.Languages,
            editor.Stamp(RecordEditor.LiveAttributes, pack.Values, null),
            new RemovalView(string.Empty, null));
        var clusterChoices = await ClustersAsync(cancellationToken);
        return new BatteryPage(
            pack,
            sources,
            family,
            Text(pack, "batterySerialNumber") ?? packId,
            Text(pack, "uniqueProductIdentifier") ?? string.Empty,
            ClusterLabel(pack.ClusterId, clusterChoices.ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)),
            clusterChoices,
            valuesView,
            liveView,
            await PublicationAsync(sources, active, cancellationToken),
            versions.OrderByDescending(version => version.Number).Select(version => new VersionRow(version, PassportReader.Verify(version))).ToArray(),
            await documents.ListAsync(TargetKind.Pack, packId, DocumentService.Accidents, cancellationToken),
            new RemovalView($"{href}/remove", versions.Count > 0 ? "its passport is published, so it stays with its versions." : null));
    }

    /// <summary>
    /// The pack values the battery page edits: all four while the passport is a draft; once published, only those not
    /// fixed (#13), since #11, #17 and #19 can no longer change.
    /// </summary>
    public static IReadOnlyList<AttributeDefinition> ValueAttributes(bool published) =>
        RecordEditor.AttributesOf(TargetKind.Pack).Where(attribute => !published || !SavePipeline.IsFixedPackValue(attribute)).ToArray();

    /// <summary>Saves the battery's own values or its live values, typed on its page.</summary>
    public async Task<(EditorPost Post, EditorSave Save)?> SaveAsync(
        string who,
        string packId,
        bool liveValues,
        Func<string, string?> field,
        string? stamp,
        DateTime? measuredAtUtc,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return null;
        }

        var attributes = liveValues ? RecordEditor.LiveAttributes : ValueAttributes(await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0);
        var post = editor.Read(attributes, pack.Values, field, stamp) with { MeasuredAtUtc = liveValues ? measuredAtUtc : null };
        return (post, await editor.SaveAsync(ChangeSender.Team(who), TargetKind.Pack, packId, post, confirmed, cancellationToken));
    }

    /// <summary>
    /// Moves a battery to another registered cluster, or out of any (design 7.8, step 4: the team moves a pack directly).
    /// The cluster isn't passport data, so no new version is made; the move is logged. Null when it moved.
    /// </summary>
    public async Task<string?> SetClusterAsync(string who, string packId, string? clusterId, CancellationToken cancellationToken = default)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return "the battery was not found";
        }

        var wanted = string.IsNullOrWhiteSpace(clusterId) ? null : clusterId.Trim();
        if (string.Equals(wanted, pack.ClusterId, StringComparison.OrdinalIgnoreCase))
        {
            return "it is already there";
        }

        var names = (await ClustersAsync(cancellationToken)).ToDictionary(cluster => cluster.Id, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);
        if (wanted is not null && !names.ContainsKey(wanted))
        {
            return $"cluster {wanted} doesn't exist";
        }

        await store.SetPackClusterAsync(packId, wanted, cancellationToken);
        var serial = Text(pack, "batterySerialNumber") ?? packId;
        await audit.AppendApplicationAuditEventAsync(
            ClusterChangedEvent,
            who,
            "Team",
            "form",
            $"Battery {serial} moved from {ClusterLabel(pack.ClusterId, names)} to {ClusterLabel(wanted, names)}",
            new MongoDB.Bson.BsonDocument
            {
                ["from"] = pack.ClusterId is null ? MongoDB.Bson.BsonNull.Value : pack.ClusterId,
                ["to"] = wanted is null ? MongoDB.Bson.BsonNull.Value : wanted,
            },
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return null;
    }

    /// <summary>
    /// Removes a draft battery, e.g. one made in the wrong batch, with its accident reports. A battery whose passport has
    /// been published stays (design 14). Returns the problem, or null when it was removed.
    /// </summary>
    public async Task<(string? Problem, string SerialNumber)> RemoveDraftAsync(string who, string packId, CancellationToken cancellationToken = default)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken);
        if (pack is null)
        {
            return ("it was not found", string.Empty);
        }

        var serial = Text(pack, "batterySerialNumber") ?? packId;
        if (await store.GetLatestVersionNumberAsync(packId, cancellationToken) > 0)
        {
            return ("its passport is published, so it stays with its versions", serial);
        }

        await store.DeleteAsync<BatteryPack>(packId, cancellationToken);
        await documents.DeleteOwnedAsync(TargetKind.Pack, packId, cancellationToken);
        await audit.AppendApplicationAuditEventAsync(
            ProductDataPages.RemovedEvent,
            who,
            "Team",
            "form",
            $"Battery {serial} removed",
            entityType: "dpp-pack",
            entityId: packId,
            cancellationToken: cancellationToken);
        return (null, serial);
    }

    /// <summary>Publishes the draft as version 1 (design 7.1); the result says what blocked it, if anything.</summary>
    public Task<PublicationResult> PublishAsync(string who, string packId, CancellationToken cancellationToken = default) =>
        publication.PublishAsync(ChangeSender.Team(who), packId, cancellationToken);

    public async Task<IReadOnlyList<ClusterChoice>> ClustersAsync(CancellationToken cancellationToken = default) =>
        (await clusters.ListClustersAsync(cancellationToken))
            .Select(cluster => new ClusterChoice(BsonHelpers.GetString(cluster, "clusterId"), BsonHelpers.GetString(cluster, "name")))
            .Where(cluster => cluster.Id.Length > 0)
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// The publication checks in the team's terms: each missing value under the record that holds it (this battery, its
    /// model, batch, site or operator) with a link to that editor, then what the schema refuses, then what is needed only
    /// after February 2027, then the uploaded files the passport uses, checked against their SHA-256.
    /// </summary>
    private async Task<PublicationView> PublicationAsync(PassportSources sources, PassportVersion? active, CancellationToken cancellationToken)
    {
        var report = await publication.CheckAsync(sources.Pack.Id, cancellationToken) ?? new PublicationReport([], []);
        var missing = new List<AttributeDefinition>();
        var problems = new List<string>();
        foreach (var item in report.Blocking)
        {
            if (Numbered(item) is { } attribute)
            {
                missing.Add(attribute);
            }
            else
            {
                problems.Add(item);
            }
        }

        var groups = missing
            .GroupBy(Holder)
            .OrderBy(group => group.Key)
            .Select(group => Group(group.Key, sources, group.OrderBy(attribute => attribute.Number).ToArray()))
            .ToArray();
        var later = report.Warnings.Select(Numbered).OfType<AttributeDefinition>().OrderBy(attribute => attribute.Number).ToArray();
        return new PublicationView(active, groups, problems, later, await FilesAsync(sources, cancellationToken));
    }

    /// <summary>The attribute a report line such as "#21 Battery mass is missing" is about.</summary>
    private static AttributeDefinition? Numbered(string item) =>
        Regex.Match(item, @"^#(\d+) ") is { Success: true } match && int.TryParse(match.Groups[1].Value, out var number)
            ? AttributeCatalog.All.FirstOrDefault(attribute => attribute.Number == number)
            : null;

    private enum Record
    {
        Battery,
        Live,
        Model,
        Batch,
        Site,
        Operator,
    }

    private static Record Holder(AttributeDefinition attribute) => attribute.Storage switch
    {
        StorageLocation.Model or StorageLocation.ModelWithPackOverride => Record.Model,
        StorageLocation.Batch => Record.Batch,
        StorageLocation.Site => Record.Site,
        StorageLocation.Operator => Record.Operator,
        _ => RecordEditor.LiveAttributes.Contains(attribute) ? Record.Live : Record.Battery,
    };

    private static MissingGroup Group(Record holder, PassportSources sources, IReadOnlyList<AttributeDefinition> attributes)
    {
        var href = BatteryHref(sources.Pack.Id);
        return holder switch
        {
            Record.Model => new($"Model {sources.Model.Name}", $"{ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)}#editor", attributes),
            Record.Batch => new($"Batch {sources.Batch.Year} · {sources.Site.Name}", $"/admin/batches/{Uri.EscapeDataString(sources.Batch.Id)}#editor", attributes),
            Record.Site => new($"Site {sources.Site.Name}", $"{ProductDataPages.SitesHref("site", sources.Site.Id)}#editor", attributes),
            Record.Operator => new($"Operator {sources.Operator.Name}", $"{ProductDataPages.SitesHref("operator", sources.Operator.Id)}#editor", attributes),
            Record.Live => new("This battery's live values", $"{href}#live", attributes),
            _ => new("This battery", $"{href}#values", attributes),
        };
    }

    /// <summary>
    /// The uploaded files the passport links to: the model's and batch's restricted documents (#45–48, #29) and the
    /// battery's accident reports, each checked against the SHA-256 taken when it was uploaded (the old Conformance page
    /// did the same for its evidence).
    /// </summary>
    private async Task<IReadOnlyList<FileCheck>> FilesAsync(PassportSources sources, CancellationToken cancellationToken)
    {
        var checks = new List<FileCheck>();
        foreach (var (owner, label, values) in new (TargetKind, string, IReadOnlyDictionary<string, AttributeValue>)[]
        {
            (TargetKind.Model, $"Model {sources.Model.Name}", sources.Model.Values),
            (TargetKind.Batch, $"Batch {sources.Batch.Year} · {sources.Site.Name}", sources.Batch.Values),
        })
        {
            foreach (var property in DppLinks.RestrictedDocumentProperties.Where(values.ContainsKey).Order(StringComparer.Ordinal))
            {
                var url = values[property].Value["url"]?.GetValue<string>() ?? string.Empty;
                var id = url.Contains("/dpp/documents/", StringComparison.Ordinal) ? Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..]) : null;
                if (id is not null && await documents.GetAsync(id, cancellationToken) is { } document && document.OwnerKind == owner)
                {
                    checks.Add(new FileCheck(document, label, await UnchangedAsync(document, cancellationToken)));
                }
            }
        }

        foreach (var report in await documents.ListAsync(TargetKind.Pack, sources.Pack.Id, DocumentService.Accidents, cancellationToken))
        {
            checks.Add(new FileCheck(report, "This battery", await UnchangedAsync(report, cancellationToken)));
        }

        return checks;
    }

    /// <summary>True when the stored bytes still have the SHA-256 recorded at upload; a missing file counts as changed.</summary>
    public async Task<bool> UnchangedAsync(DppDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            var bytes = await documents.DownloadAsync(document, cancellationToken);
            return string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), document.Sha256, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is MongoDB.Driver.GridFS.GridFSFileNotFoundException or FormatException)
        {
            return false;
        }
    }

    private static string ClusterLabel(string? clusterId, IReadOnlyDictionary<string, string> names) =>
        clusterId is null ? "No cluster yet" : names.TryGetValue(clusterId, out var name) && name.Length > 0 ? name : clusterId;

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
```

- [ ] **Step 4: Apply the services patch**

Patch `2d2-task3-code.patch` (`web/Dpp/Audit/AuditLines.cs`, `web/Dpp/Documents/DocumentService.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`, `web/Dpp/Pages/ProductDataPages.cs`, `web/Dpp/Pages/RecordEditor.cs`, `web/Dpp/Publication/PublicationService.cs`). Save the block as `../2d2-task3-code.patch`, outside the worktree, and run `git apply ../2d2-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Audit/AuditLines.cs b/web/Dpp/Audit/AuditLines.cs
--- a/web/Dpp/Audit/AuditLines.cs
+++ b/web/Dpp/Audit/AuditLines.cs
@@ -202,6 +202,13 @@ public sealed class AuditLines(DppStore store, DocumentStore documents, Attribut
 
                 record = owner;
                 break;
+            case BatteryPages.ClusterChangedEvent:
+                icon = AuditIcon.Edit;
+                words.Add(new(" moved battery "));
+                words.Add(new(record?.Name ?? entityId, Strong: true));
+                words.Add(new(" to another cluster"));
+                change = After(message, " moved from ") is { } move ? move.Replace(" to ", " → ", StringComparison.Ordinal) : null;
+                break;
             case PackCreationService.AuditEventType:
                 icon = AuditIcon.Add;
                 words.Add(new(" created battery "));
@@ -476,7 +483,7 @@ public sealed class AuditLines(DppStore store, DocumentStore documents, Attribut
     private static string NameFrom(string message, string suffix)
     {
         var text = message.EndsWith(suffix, StringComparison.Ordinal) ? message[..^suffix.Length] : message.Split(suffix, 2)[0];
-        foreach (var prefix in new[] { "Battery family ", "Battery model ", "Batch ", "Manufacturing site ", "Economic operator " })
+        foreach (var prefix in new[] { "Battery family ", "Battery model ", "Batch ", "Manufacturing site ", "Economic operator ", "Battery " })
         {
             if (text.StartsWith(prefix, StringComparison.Ordinal))
             {
@@ -584,7 +591,7 @@ public sealed class AuditLines(DppStore store, DocumentStore documents, Attribut
             foreach (var pack in packs)
             {
                 var name = pack.Values.TryGetValue(serial, out var value) && Plain(value.Value) is { Length: > 0 } text ? text : pack.Id;
-                result.names[("dpp-pack", pack.Id)] = new("battery", name, null, null);
+                result.names[("dpp-pack", pack.Id)] = new("battery", name, BatteryPages.BatteryHref(pack.Id), null);
             }
 
             return result;
diff --git a/web/Dpp/Documents/DocumentService.cs b/web/Dpp/Documents/DocumentService.cs
--- a/web/Dpp/Documents/DocumentService.cs
+++ b/web/Dpp/Documents/DocumentService.cs
@@ -35,7 +35,8 @@ public sealed class DocumentService(
     public const string UploadedEvent = "dpp.document-uploaded";
     public const string DeniedEvent = "dpp.document-denied";
 
-    private const string Accidents = "informationOnAccidents";
+    /// <summary>#92: a pack's accident reports.</summary>
+    public const string Accidents = "informationOnAccidents";
 
     /// <summary>Uploads the file for a restricted document of a model or batch and links the attribute to it.</summary>
     public async Task<DocumentUploadResult> UploadAsync(ChangeSender sender, TargetKind owner, string ownerId, string property, DocumentUpload upload, CancellationToken cancellationToken = default)
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -17,7 +17,7 @@ public static class DppServiceCollectionExtensions
 {
     /// <summary>
     /// Registers the V3 passport services (design section 3). They need MongoContext, ApplicationAuditService,
-    /// DemoSigningKeyService and the BatteryPassOptions (APP_BASE_URL for document links).
+    /// DemoSigningKeyService, ClusterRepository and the BatteryPassOptions (APP_BASE_URL for document links).
     /// </summary>
     public static IServiceCollection AddDpp(this IServiceCollection services)
     {
@@ -41,6 +41,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<AttributeForms>();
         services.AddSingleton<RecordEditor>();
         services.AddSingleton<ProductDataPages>();
+        services.AddSingleton<BatteryPages>();
         services.AddSingleton<AuditLines>();
         return services;
     }
diff --git a/web/Dpp/Pages/ProductDataPages.cs b/web/Dpp/Pages/ProductDataPages.cs
--- a/web/Dpp/Pages/ProductDataPages.cs
+++ b/web/Dpp/Pages/ProductDataPages.cs
@@ -47,6 +47,7 @@ public sealed record RecordEditorView(
         TargetKind.Batch => "Batch",
         TargetKind.Site => "Manufacturing site",
         TargetKind.Operator => "Economic operator",
+        TargetKind.Pack => "Battery",
         _ => "Record",
     };
 
diff --git a/web/Dpp/Pages/RecordEditor.cs b/web/Dpp/Pages/RecordEditor.cs
--- a/web/Dpp/Pages/RecordEditor.cs
+++ b/web/Dpp/Pages/RecordEditor.cs
@@ -28,6 +28,9 @@ public sealed record EditorPost(
     /// <summary>The fields edited on the page; null when the page sent no stamp, so every field counts as edited.</summary>
     public IReadOnlySet<string>? Edited { get; init; }
 
+    /// <summary>When the typed values were measured (live values, design 7.3); the save time when null.</summary>
+    public DateTime? MeasuredAtUtc { get; init; }
+
     public bool WasEdited(string property) => Edited is null || Edited.Contains(property);
 }
 
@@ -181,10 +184,15 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
     {
         (TargetKind.Site, "identifiersAndProductData") => "The manufacturer and the place where the batteries are made.",
         (TargetKind.Operator, "identifiersAndProductData") => "The economic operator that places the batteries on the market.",
+        (TargetKind.Pack, "identifiersAndProductData") => "This battery's own values. The serial number, manufacturing date and warranty are fixed once its passport is published.",
+        (TargetKind.Pack, "performanceAndDurability") => "Measured on the battery. Its battery system sends them; type them here before it does, for example the end-of-line values, or to correct one.",
         _ => SectionTexts[section].Note,
     };
 
-    /// <summary>The attributes the team edits on a model, batch, site or operator, in catalogue order.</summary>
+    /// <summary>
+    /// The attributes the team edits on a model, batch, site or operator, in catalogue order; for a pack, the values the
+    /// team types (#11, #13, #17, #19). A pack's live values have their own form (<see cref="LiveAttributes"/>).
+    /// </summary>
     public static IReadOnlyList<AttributeDefinition> AttributesOf(TargetKind kind) => AttributeCatalog.All
         .Where(attribute => kind switch
         {
@@ -192,10 +200,25 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
             TargetKind.Batch => attribute.Storage == StorageLocation.Batch,
             TargetKind.Site => attribute.Storage == StorageLocation.Site,
             TargetKind.Operator => attribute.Storage == StorageLocation.Operator,
-            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "packs have their own page"),
+            TargetKind.Pack => IsTypedPackValue(attribute),
+            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
         })
         .ToArray();
 
+    /// <summary>
+    /// The live values (#63, #83, #87): the battery system feeds them, and the team types them too, for example the
+    /// end-of-line values a draft needs before publication (design 7.1). They change in place, without a new version.
+    /// </summary>
+    public static IReadOnlyList<AttributeDefinition> LiveAttributes { get; } = AttributeCatalog.All
+        .Where(attribute => attribute.Change == ChangeRule.Live && attribute.Editors.HasFlag(AttributeEditors.BatterySystemFeed))
+        .ToArray();
+
+    // #6 and #92 are the app's, #22 changes only through the status change (step 4), the live values have their own form.
+    private static bool IsTypedPackValue(AttributeDefinition attribute) =>
+        attribute.Storage == StorageLocation.Pack
+        && attribute.Editors.HasFlag(AttributeEditors.Team)
+        && attribute.Change is ChangeRule.Fixed or ChangeRule.IfProviderChanges;
+
     /// <summary>How many of the record's attributes have a value.</summary>
     public static int CountFilled(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored) =>
         AttributesOf(kind).Count(attribute => stored.ContainsKey(attribute.Property));
@@ -220,10 +243,15 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
     /// can tell the fields edited on the page from those it only showed. A page left open then never undoes a newer
     /// value saved by someone else, an upload or a feed.
     /// </summary>
-    public string Stamp(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, string? name)
+    public string Stamp(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, string? name) =>
+        Stamp(AttributesOf(kind), stored, name);
+
+    /// <summary>The stamp of a form that shows only these <paramref name="attributes"/> (e.g. a pack's live values).</summary>
+    public string Stamp(IReadOnlyList<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored, string? name)
     {
+        ArgumentNullException.ThrowIfNull(attributes);
         ArgumentNullException.ThrowIfNull(stored);
-        var parts = AttributesOf(kind)
+        var parts = attributes
             .Select(forms.For)
             .Where(form => form.Kind is not (FormKind.Upload or FormKind.Fixed))
             .Select(form => $"{form.Attribute.Property}:{Fingerprint(FormValues.Show(form, stored.GetValueOrDefault(form.Attribute.Property)?.Value))}");
@@ -241,8 +269,13 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
     /// nothing is removed.
     /// </summary>
     /// <param name="field">Gives the posted text of a field name, or null when it wasn't posted.</param>
-    public EditorPost Read(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, Func<string, string?> field, string? stamp = null)
+    public EditorPost Read(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, Func<string, string?> field, string? stamp = null) =>
+        Read(AttributesOf(kind), stored, field, stamp);
+
+    /// <summary>Reads a form that shows only these <paramref name="attributes"/>, by the rules above.</summary>
+    public EditorPost Read(IReadOnlyList<AttributeDefinition> attributes, IReadOnlyDictionary<string, AttributeValue> stored, Func<string, string?> field, string? stamp = null)
     {
+        ArgumentNullException.ThrowIfNull(attributes);
         ArgumentNullException.ThrowIfNull(stored);
         ArgumentNullException.ThrowIfNull(field);
         var shown = ParseStamp(stamp);
@@ -252,7 +285,7 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
         var edited = new HashSet<string>(StringComparer.Ordinal);
         var removals = new HashSet<string>(StringComparer.Ordinal);
         var conflicts = new HashSet<string>(StringComparer.Ordinal);
-        foreach (var attribute in AttributesOf(kind))
+        foreach (var attribute in attributes)
         {
             var form = forms.For(attribute);
             var count = form.Kind == FormKind.Rows && int.TryParse(field(form.RowCountField), out var posted) ? posted : 0;
@@ -354,17 +387,29 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
     /// The editor's sections. Without <paramref name="post"/> the fields show the stored values; with it, what was typed,
     /// so a refused value can be corrected in place.
     /// </summary>
+    public Task<IReadOnlyList<EditorSection>> SectionsAsync(
+        TargetKind kind,
+        string id,
+        IReadOnlyDictionary<string, AttributeValue> stored,
+        EditorPost? post = null,
+        IReadOnlyDictionary<string, Refusal>? refusals = null,
+        CancellationToken cancellationToken = default) =>
+        SectionsAsync(kind, id, AttributesOf(kind), stored, post, refusals, cancellationToken);
+
+    /// <summary>The sections of a form that shows only these <paramref name="attributes"/> of the record.</summary>
     public async Task<IReadOnlyList<EditorSection>> SectionsAsync(
         TargetKind kind,
         string id,
+        IReadOnlyList<AttributeDefinition> attributes,
         IReadOnlyDictionary<string, AttributeValue> stored,
         EditorPost? post = null,
         IReadOnlyDictionary<string, Refusal>? refusals = null,
         CancellationToken cancellationToken = default)
     {
+        ArgumentNullException.ThrowIfNull(attributes);
         ArgumentNullException.ThrowIfNull(stored);
         var fields = new List<EditorField>();
-        foreach (var attribute in AttributesOf(kind))
+        foreach (var attribute in attributes)
         {
             var form = forms.For(attribute);
             var value = stored.GetValueOrDefault(attribute.Property)?.Value;
@@ -412,7 +457,8 @@ public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, Dp
             id,
             post.Changed.ToDictionary(entry => entry.Key, entry => (JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal),
             "form",
-            Removals: post.Removals.ToArray());
+            post.MeasuredAtUtc,
+            post.Removals.ToArray());
         var preview = await pipeline.SaveAsync(sender, changes, preview: true, cancellationToken);
         if (!preview.Found)
         {
diff --git a/web/Dpp/Publication/PublicationService.cs b/web/Dpp/Publication/PublicationService.cs
--- a/web/Dpp/Publication/PublicationService.cs
+++ b/web/Dpp/Publication/PublicationService.cs
@@ -30,6 +30,22 @@ public sealed class PublicationService(
     public const string PublishedEvent = "dpp.published";
     public const string BlockedEvent = "dpp.publish-blocked";
 
+    /// <summary>
+    /// The checks the pack's passport would meet if it were published now, without publishing anything (the battery
+    /// page shows them); null when the pack, or a record it links to, is missing.
+    /// </summary>
+    public async Task<PublicationReport?> CheckAsync(string packId, CancellationToken cancellationToken = default)
+    {
+        var sources = await store.LoadSourcesAsync(packId, cancellationToken);
+        if (sources is null)
+        {
+            return null;
+        }
+
+        var header = new PassportHeader(identifiers.NewPassportIdentifier(), DppStatuses.Active, clock.GetUtcNow().UtcDateTime);
+        return checks.Check(PassportAssembler.Assemble(sources, header));
+    }
+
     public Task<PublicationResult> PublishAsync(ChangeSender sender, string packId, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(sender);
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteryAuditLinesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteryPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    27`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1882`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs BatteryPassWeb.Tests/Dpp/BatteryAuditLinesTests.cs BatteryPassWeb.Tests/Dpp/BatteryPagesTests.cs BatteryPassWeb.Tests/Dpp/BatteryTesting.cs web/Dpp/Pages/BatteryPages.cs web/Dpp/Audit/AuditLines.cs web/Dpp/Documents/DocumentService.cs web/Dpp/DppServiceCollectionExtensions.cs web/Dpp/Pages/ProductDataPages.cs web/Dpp/Pages/RecordEditor.cs web/Dpp/Publication/PublicationService.cs
git commit -m "feat(dpp): load the batteries pages: list, search, new battery, values, live values, cluster, publication, files and removal"
```


---

### Task 4: The pages: controller, views and finish

Razor views compile with the build, so a mistake in a view fails Step 2's build like one in C#. The shared editor partial (2d-1) learns that a battery has no name field and no remove box inside the editor, and names `_EditorField` by its full path, since the battery views live in another folder.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`
- Create: `web/Controllers/BatteriesController.cs`
- Create: `web/Views/Batteries/Batteries.cshtml`
- Create: `web/Views/Batteries/Battery.cshtml`
- Create: `web/Views/Batteries/NewBattery.cshtml`
- Modify: `web/Views/ProductData/_RecordEditor.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The batteries pages' routes (step 2d-2), as a signed-in team member reaches them, and their views.</summary>
[Collection(MongoCollection.Name)]
public sealed class BatteriesControllerTests(MongoFixture mongo)
{
    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", $"{name}.cshtml"));

    [Fact]
    public void TheBatteriesPages_ShouldBeTheTeamsOnlyUnderTheMenusAddress()
    {
        var type = typeof(BatteriesController);

        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("admin/batteries", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.All(
            type.GetMethods().Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null),
            method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
    }

    [Fact]
    public async Task TheList_ShouldShowEveryBatteryAndFilterBySearch()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var all = Assert.IsType<ViewResult>(await Controller(rig).Batteries(null, CancellationToken.None));
        var none = Assert.IsType<ViewResult>(await Controller(rig).Batteries("no such battery", CancellationToken.None));

        Assert.Single(Assert.IsType<BatteriesPage>(all.Model).Rows);
        Assert.Empty(Assert.IsType<BatteriesPage>(none.Model).Rows);
    }

    [Fact]
    public async Task CreatingABattery_ShouldOpenItsPageOrShowTheFormAgainAsTyped()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var serial = BatteryTesting.Field("batterySerialNumber");

        var created = await Controller(rig, (serial, "VPC9-2001")).CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, BatteryTesting.South, CancellationToken.None);
        var refused = await Controller(rig, (serial, " vpc9-2001 ")).CreateBattery(rig.Seed.Sources.Batch.Id, rig.Seed.Sources.Operator.Id, null, CancellationToken.None);

        Assert.StartsWith("/admin/batteries/pack-", Assert.IsType<RedirectResult>(created).Url, StringComparison.Ordinal);
        var again = Assert.IsType<ViewResult>(refused);
        Assert.Equal("NewBattery", again.ViewName);
        var page = Assert.IsType<NewBatteryPage>(again.Model);
        var field = page.Values.Sections.SelectMany(section => section.Fields).Single(item => item.Attribute.Number == 11);
        Assert.Equal(" vpc9-2001 ", field.Text(serial));
        Assert.Equal("Another battery already has this serial number.", field.Refusal);
        Assert.Equal(2, (await rig.Store.ListAsync<BatteryPack>()).Count);
    }

    [Fact]
    public async Task AValueChangeOnAPublishedBattery_ShouldAskFirstThenPublishAVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(BatteryPages.ValueAttributes(published: true), stored);
        form[BatteryTesting.Field("thirdPartyServiceProvider")] = "Other Hosting AB";
        form[ProductDataController.StampField] = PageTesting.Editor(rig).Stamp(BatteryPages.ValueAttributes(published: true), stored, null);

        var asked = Assert.IsType<ViewResult>(await Controller(rig, [.. form]).SaveValues(rig.PackId, confirmed: false, CancellationToken.None));
        Assert.Single(Assert.IsType<BatteryPage>(asked.Model).Values.Confirm);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));

        var saved = await Controller(rig, [.. form]).SaveValues(rig.PackId, confirmed: true, CancellationToken.None);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#values", Assert.IsType<RedirectResult>(saved).Url);
        Assert.Equal(2, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task LiveValues_ShouldSaveInPlaceAtTheTimeTyped()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var stored = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values;
        var form = BatteryTesting.ShownForm(RecordEditor.LiveAttributes, stored);
        form[BatteryTesting.Field("temperatureInformation")] = "18";
        form[ProductDataController.StampField] = PageTesting.Editor(rig).Stamp(RecordEditor.LiveAttributes, stored, null);
        form[BatteriesController.MeasuredAtField] = "2026-10-29T09:15";

        var result = await Controller(rig, [.. form]).SaveLiveValues(rig.PackId, CancellationToken.None);

        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#live", Assert.IsType<RedirectResult>(result).Url);
        var temperature = (await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.Values["temperatureInformation"];
        Assert.Equal(BatteriesController.MeasuredAt("2026-10-29T09:15"), temperature.GeneratedAtUtc);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a time")]
    public void AMeasurementTimeThatIsBlankOrUnreadable_ShouldMeanNow(string typed) =>
        Assert.Null(BatteriesController.MeasuredAt(typed));

    [Fact]
    public void AMeasurementTime_ShouldBeReadInThePagesTimeZone() =>
        Assert.Equal(
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, 29, 9, 15, 0, DateTimeKind.Unspecified), TimeZoneInfo.Local),
            BatteriesController.MeasuredAt("2026-10-29T09:15"));

    [Fact]
    public async Task PublishingAndMovingTheCluster_ShouldReportInTheirOwnCard()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var moved = Controller(rig);
        await moved.SetCluster(rig.PackId, "cluster-gone", CancellationToken.None);
        var published = Controller(rig);
        var result = await published.Publish(rig.PackId, CancellationToken.None);

        Assert.Equal("cluster", moved.TempData[BatteriesController.CardKey]);
        Assert.Equal("The cluster wasn't changed: cluster cluster-gone doesn't exist.", moved.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal($"{BatteryPages.BatteryHref(rig.PackId)}#publication", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Version 1 published.", published.TempData[BatteriesController.CardStatusKey]);
        Assert.Equal(1, await rig.Store.GetLatestVersionNumberAsync(rig.PackId));
    }

    [Fact]
    public async Task AnAccidentReport_ShouldBeAddedOrSayWhatIsMissing()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();
        var bytes = DocumentTesting.Pdf("collision");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "collision.pdf");

        var empty = Controller(rig);
        await empty.AddAccidentReport(rig.PackId, null, null, null, CancellationToken.None);
        var added = Controller(rig);
        await added.AddAccidentReport(rig.PackId, file, "Collision", "en", CancellationToken.None);

        Assert.Equal("Choose a file first.", empty.TempData[BatteriesController.CardErrorKey]);
        Assert.Equal("collision.pdf added.", added.TempData[BatteriesController.CardStatusKey]);
        Assert.Single(await new DocumentStore(rig.Context).ListAsync(TargetKind.Pack, rig.PackId, DocumentService.Accidents));
    }

    [Fact]
    public async Task RemovingABattery_ShouldWorkOnlyForADraft()
    {
        var published = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        var draft = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        var kept = await Controller(published).Remove(published.PackId, CancellationToken.None);
        var removed = await Controller(draft).Remove(draft.PackId, CancellationToken.None);

        Assert.Equal($"{BatteryPages.BatteryHref(published.PackId)}#remove", Assert.IsType<RedirectResult>(kept).Url);
        Assert.Equal("/admin/batteries", Assert.IsType<RedirectResult>(removed).Url);
        Assert.Null(await draft.Store.GetAsync<BatteryPack>(draft.PackId));
    }

    [Fact]
    public async Task AMissingBattery_ShouldBeNotFound()
    {
        var rig = await PublicationTesting.RigAsync(mongo).WithClustersAsync();

        Assert.IsType<NotFoundResult>(await Controller(rig).Battery("pack-missing", CancellationToken.None));
    }

    [Fact]
    public void TheViews_ShouldUseTheTeamPagesFinishAndTheirParts()
    {
        var list = View("Batteries");
        var battery = View("Battery");
        var create = View("NewBattery");
        var editor = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_RecordEditor.cshtml"));

        Assert.All(new[] { list, battery, create }, view => Assert.Contains("~/css/bp-product-data.css", view, StringComparison.Ordinal));
        Assert.Contains("PartialAsync(\"_AdminTabs\", \"batteries\")", list, StringComparison.Ordinal);
        Assert.Contains("Passport ID, battery ID, serial or cluster", list, StringComparison.Ordinal);
        foreach (var card in new[] { "publication", "values", "live", "accidents", "versions", "remove" })
        {
            Assert.Contains($"id=\"{card}\"", battery, StringComparison.Ordinal);
        }

        Assert.Contains("~/Views/ProductData/_RecordEditor.cshtml", battery, StringComparison.Ordinal);
        Assert.Contains("No cluster yet", create, StringComparison.Ordinal);
        Assert.Contains("Model.Kind is TargetKind.Model or TargetKind.Site or TargetKind.Operator", editor, StringComparison.Ordinal); // a battery has no name field
        Assert.Contains("~/Views/ProductData/_EditorField.cshtml", editor, StringComparison.Ordinal);
    }

    private static BatteriesController Controller(PublicationRig rig, params KeyValuePair<string, string>[] form)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)],
                "test")),
        };
        http.Request.Form = new FormCollection(form.ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new BatteriesController(BatteryTesting.Pages(rig), PageTesting.Documents(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private static BatteriesController Controller(PublicationRig rig, (string Name, string Value) field) =>
        Controller(rig, new KeyValuePair<string, string>(field.Name, field.Value));

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteriesControllerTests"`
Expected: the build fails because `BatteriesController` doesn't exist yet (CS0246).

- [ ] **Step 3: Write `BatteriesController.cs`**

`web/Controllers/BatteriesController.cs`:

```csharp
using System.Globalization;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The team's batteries pages (design 12 and 14, step 2d-2): the list, a new battery and the battery page. They replace
/// the old "Batteries" admin pages under the same menu entry. Passport values go through the save pipeline.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin/batteries")]
public sealed class BatteriesController(BatteryPages pages, DocumentService documents) : Controller
{
    /// <summary>Which card of the battery page a message belongs to, so it shows where the action was taken.</summary>
    public const string CardKey = "BatteryCard";

    public const string CardStatusKey = "BatteryCardStatus";

    public const string CardErrorKey = "BatteryCardError";

    /// <summary>Neutral information in a card ("Nothing changed."): neither a success nor a problem.</summary>
    public const string CardInfoKey = "BatteryCardInfo";

    /// <summary>The live-values form's field with the time the values were measured, in the page's time zone.</summary>
    public const string MeasuredAtField = "measuredAt";

    [HttpGet("")]
    public async Task<IActionResult> Batteries([FromQuery] string? q, CancellationToken cancellationToken) =>
        View(await pages.ListAsync(q, cancellationToken));

    [HttpGet("new")]
    public async Task<IActionResult> NewBattery([FromQuery] string? batch, CancellationToken cancellationToken) =>
        View(await pages.NewAsync(batchId: batch, cancellationToken: cancellationToken));

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBattery([FromForm] string? batchId, [FromForm] string? operatorId, [FromForm] string? clusterId, CancellationToken cancellationToken)
    {
        var form = Request.Form;
        var (result, post) = await pages.CreateAsync(Who, batchId, operatorId, clusterId, name => form.TryGetValue(name, out var value) ? value.ToString() : null, cancellationToken);
        if (result.Pack is { } pack)
        {
            TempData[ProductDataController.StatusKey] = "Draft battery created. Fill in what its passport still needs, then publish it.";
            return Redirect(BatteryPages.BatteryHref(pack.Id));
        }

        var refused = result.Keys.Count(key => key.Outcome == KeyOutcome.Rejected);
        ViewData[ProductDataController.ErrorKey] = refused > 0
            ? $"The battery wasn't created: {Words.Count(refused, "value")} {(refused == 1 ? "was" : "were")} refused; {(refused == 1 ? "it's" : "they're")} marked below."
            : $"The battery wasn't created: {result.Problem}.";
        var state = new EditorState(post, new EditorSave(EditorOutcome.Refused, result.Keys, []));
        return View("NewBattery", await pages.NewAsync(batchId, operatorId, clusterId, state, cancellationToken));
    }

    [HttpGet("{packId}")]
    public async Task<IActionResult> Battery(string packId, CancellationToken cancellationToken)
    {
        var page = await pages.BatteryAsync(packId, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost("{packId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveValues(string packId, [FromForm] bool confirmed, CancellationToken cancellationToken) =>
        SaveAsync(packId, liveValues: false, confirmed, cancellationToken);

    [HttpPost("{packId}/live")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveLiveValues(string packId, CancellationToken cancellationToken) =>
        SaveAsync(packId, liveValues: true, confirmed: true, cancellationToken);

    [HttpPost("{packId}/cluster")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCluster(string packId, [FromForm] string? clusterId, CancellationToken cancellationToken)
    {
        var problem = await pages.SetClusterAsync(Who, packId, clusterId, cancellationToken);
        Card("cluster", problem is null ? "Cluster changed. The passport didn't change: the cluster isn't passport data." : null, problem is null ? null : $"The cluster wasn't changed: {problem}.");
        return Redirect(BatteryPages.BatteryHref(packId));
    }

    [HttpPost("{packId}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(string packId, CancellationToken cancellationToken)
    {
        var result = await pages.PublishAsync(Who, packId, cancellationToken);
        if (result.Version is { } version && result.Problem is null)
        {
            Card("publication", result.Unchanged ? "Nothing new to publish: the passport is already up to date." : $"Version {version.Number} published.", null);
        }
        else
        {
            Card("publication", null, $"The passport wasn't published: {result.Problem}.");
        }

        return Redirect($"{BatteryPages.BatteryHref(packId)}#publication");
    }

    [HttpPost("{packId}/accidents")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccidentReport(string packId, IFormFile? file, [FromForm] string? title, [FromForm] string? language, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            Card("accidents", null, "Choose a file first.");
            return Redirect($"{BatteryPages.BatteryHref(packId)}#accidents");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var result = await documents.AddAccidentReportAsync(ChangeSender.Team(Who), packId, new DocumentUpload(file.FileName, buffer.ToArray(), title, language), cancellationToken);
        Card("accidents", result.Problem is null ? $"{result.Document!.FileName} added." : null, result.Problem is null ? null : $"The report wasn't added: {result.Problem}.");
        return Redirect($"{BatteryPages.BatteryHref(packId)}#accidents");
    }

    [HttpPost("{packId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string packId, CancellationToken cancellationToken)
    {
        var (problem, serial) = await pages.RemoveDraftAsync(Who, packId, cancellationToken);
        if (problem is not null)
        {
            Card("remove", null, $"The battery wasn't removed: {problem}.");
            return Redirect($"{BatteryPages.BatteryHref(packId)}#remove");
        }

        TempData[ProductDataController.StatusKey] = $"Draft battery {serial} removed.";
        return Redirect("/admin/batteries");
    }

    private string Who => AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";

    private void Card(string card, string? status, string? error)
    {
        TempData[CardKey] = card;
        TempData[CardStatusKey] = status;
        TempData[CardErrorKey] = error;
    }

    /// <summary>
    /// Saves the battery's own values (the values card, which asks before publishing a new version) or its live values
    /// (in place, with the time they were measured). Refused values show the page again as typed.
    /// </summary>
    private async Task<IActionResult> SaveAsync(string packId, bool liveValues, bool confirmed, CancellationToken cancellationToken)
    {
        var form = Request.Form;
        var measured = liveValues ? MeasuredAt(form[MeasuredAtField].ToString()) : null;
        var saved = await pages.SaveAsync(
            Who,
            packId,
            liveValues,
            name => form.TryGetValue(name, out var value) ? value.ToString() : null,
            form[ProductDataController.StampField].ToString(),
            measured,
            confirmed,
            cancellationToken);
        if (saved is not { } result)
        {
            return NotFound();
        }

        var (post, save) = result;
        var anchor = liveValues ? "#live" : "#values";
        var done = (save.SavedCount, save.RemovedCount) switch
        {
            (> 0, > 0) => $"{Words.Count(save.SavedCount, "value")} saved and {save.RemovedCount} removed.",
            (> 0, _) => $"{Words.Count(save.SavedCount, "value")} saved.",
            (_, > 0) => $"{Words.Count(save.RemovedCount, "value")} removed.",
            _ => string.Empty,
        };
        var version = !liveValues && save.Affected.Count > 0 && save.Outcome is EditorOutcome.Saved or EditorOutcome.Refused ? " The passport got a new version." : string.Empty;
        EditorState state = new(post, save);
        switch (save.Outcome)
        {
            case EditorOutcome.NotFound:
                return NotFound();
            case EditorOutcome.NeedsConfirmation:
                return View("Battery", await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken));
            case EditorOutcome.Refused:
                var refused = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.{version}".TrimStart();
                if (liveValues)
                {
                    ViewData[CardKey] = "live";
                    ViewData[CardErrorKey] = refused;
                    return View("Battery", await pages.BatteryAsync(packId, live: state, cancellationToken: cancellationToken));
                }

                ViewData[ProductDataController.EditorErrorKey] = refused;
                return View("Battery", await pages.BatteryAsync(packId, values: state, cancellationToken: cancellationToken));
            case EditorOutcome.NothingChanged:
                if (liveValues)
                {
                    TempData[CardKey] = "live";
                    TempData[CardInfoKey] = "Nothing changed.";
                }
                else
                {
                    TempData[ProductDataController.EditorInfoKey] = "Nothing changed.";
                }

                return Redirect(BatteryPages.BatteryHref(packId) + anchor);
            default:
                if (liveValues)
                {
                    Card("live", $"{done} Live values change the passport in place, without a new version.", null);
                }
                else
                {
                    TempData[ProductDataController.EditorStatusKey] = $"{done}{version}";
                }

                return Redirect(BatteryPages.BatteryHref(packId) + anchor);
        }
    }

    /// <summary>A measurement time typed in the page's time zone (a datetime-local input), as UTC; null when blank or unreadable.</summary>
    public static DateTime? MeasuredAt(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)
            || !DateTime.TryParse(typed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var local))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);
    }
}
```

- [ ] **Step 4: Write `Batteries.cshtml`**

`web/Views/Batteries/Batteries.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model BatteriesPage
@{
    ViewData["Title"] = "Batteries";
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <div>
        <h1 class="mb-1">Administration</h1>
        <p class="text-secondary mb-0">Manage batteries, battery families, sites and operators, registered clusters, user memberships, credentials, and audit history.</p>
    </div>

    @await Html.PartialAsync("_AdminTabs", "batteries")
    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")

    <section class="bp-card bp-product-template-hero bp-console-form-card bp-batteries-hero mt-4">
        <div>
            <span class="bp-summary-title">Batteries</span>
            <h2>Batteries and their passports</h2>
            <p class="mb-0">
                One passport per battery. A new battery is a draft, seen only by your team, until its passport is published.
            </p>
        </div>
        <div class="bp-batteries-tools mt-3">
            <form method="get" action="/admin/batteries" class="bp-batteries-search" role="search">
                <label class="visually-hidden" for="battery-search">Search batteries</label>
                <input id="battery-search" type="search" name="q" value="@Model.Query" placeholder="Passport ID, battery ID, serial or cluster" />
                <button type="submit" class="bp-secondary-button">Search</button>
                @if (Model.Query.Length > 0)
                {
                    <a href="/admin/batteries" class="bp-batteries-clear">Show all</a>
                }
            </form>
            <a href="/admin/batteries/new" class="bp-primary-button text-nowrap">Create battery</a>
        </div>
    </section>

    @if (Model.Total == 0)
    {
        <div class="bp-card mt-4">No batteries yet. Use Create battery to add the first; it starts as a draft.</div>
    }
    else if (Model.Rows.Count == 0)
    {
        <div class="bp-card mt-4">No battery matches “@Model.Query”. Search by passport ID, battery ID, serial number or cluster, or <a href="/admin/batteries">show all</a>.</div>
    }
    else
    {
        <section class="bp-card bp-table-card bp-batteries-card mt-4">
            <p class="bp-batteries-count">
                @(Model.Query.Length > 0 ? $"{Words.Count(Model.Rows.Count, "battery", "batteries")} of {Model.Total} match “{Model.Query}”." : $"{Words.Count(Model.Total, "battery", "batteries")}.")
            </p>
            <div class="bp-table-scroll">
                <table class="table align-middle bp-console-table bp-batteries-table bp-mobile-card-table">
                    <thead>
                        <tr>
                            <th>Serial number</th>
                            <th>Model</th>
                            <th>Batch</th>
                            <th>Cluster</th>
                            <th>Passport</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var row in Model.Rows)
                        {
                            <tr>
                                <td data-label="Serial number">
                                    <a href="@BatteryPages.BatteryHref(row.PackId)" class="bp-battery-link">@row.SerialNumber</a>
                                    <small class="bp-battery-id" title="Battery ID (#6)">@row.Identifier</small>
                                </td>
                                <td data-label="Model">@row.ModelName <small class="bp-batteries-muted">@row.FamilyName</small></td>
                                <td data-label="Batch">@row.BatchLabel</td>
                                <td data-label="Cluster">
                                    @if (row.ClusterId is null)
                                    {
                                        <span class="bp-batteries-muted">No cluster yet</span>
                                    }
                                    else
                                    {
                                        @row.ClusterLabel
                                    }
                                </td>
                                <td data-label="Passport">
                                    @if (row.Active is { } active)
                                    {
                                        <span class="bp-field-marker is-published">Published · version @active.Number</span>
                                    }
                                    else
                                    {
                                        <span class="bp-field-marker">Draft</span>
                                    }
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </section>
    }
</main>
```

- [ ] **Step 5: Write `Battery.cshtml`**

`web/Views/Batteries/Battery.cshtml`:

```cshtml
@using System.Globalization
@using BatteryPassWeb.Controllers
@using BatteryPassWeb.Dpp.Identifiers
@using BatteryPassWeb.Dpp.Pages
@using BatteryPassWeb.Dpp.Store
@inject IIdentifierScheme Identifiers
@model BatteryPage
@{
    ViewData["Title"] = $"Battery {Model.SerialNumber}";
    var href = BatteryPages.BatteryHref(Model.Pack.Id);
    var sources = Model.Sources;
    var publication = Model.Publication;
    var english = CultureInfo.GetCultureInfo("en-GB");
    string When(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("d MMM yyyy, HH:mm", english);
    string Value(string property) => Model.Pack.Values.GetValueOrDefault(property)?.Value?.ToString() ?? "—";

    // "2026-09" as "September 2026", and an ISO 8601 period such as "P3Y6M" as "3 years 6 months".
    string Month(string text) => DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
        ? month.ToString("MMMM yyyy", english)
        : text;
    string Period(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)D)?$");
        if (!match.Success || text == "P")
        {
            return text;
        }

        var parts = new[] { (match.Groups[1].Value, "year"), (match.Groups[2].Value, "month"), (match.Groups[3].Value, "day") }
            .Where(part => part.Item1.Length > 0)
            .Select(part => Words.Count(int.Parse(part.Item1, CultureInfo.InvariantCulture), part.Item2));
        return string.Join(" ", parts);
    }
    var card = (TempData[BatteriesController.CardKey] ?? ViewData[BatteriesController.CardKey]) as string;
    var cardStatus = (TempData[BatteriesController.CardStatusKey] ?? ViewData[BatteriesController.CardStatusKey]) as string;
    var cardError = (TempData[BatteriesController.CardErrorKey] ?? ViewData[BatteriesController.CardErrorKey]) as string;
    var cardInfo = (TempData[BatteriesController.CardInfoKey] ?? ViewData[BatteriesController.CardInfoKey]) as string;
    var testIds = Identifiers.IsTestIdentifier(Model.Identifier) || Model.Versions.Any(row => Identifiers.IsTestIdentifier(row.Version.DigitalProductPassportId));
    var liveFields = Model.Live.Sections.SelectMany(section => section.Fields).ToArray();
    var filesChanged = publication.Files.Count(file => !file.Unchanged);

    async Task CardMessagesAsync(string name)
    {
        if (card != name)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(cardStatus))
        {
            <p class="alert alert-success bp-status-line is-done" role="status">
                <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 8.5l3 3 6-7" /></svg>
                <span>@cardStatus</span>
            </p>
        }
        if (!string.IsNullOrWhiteSpace(cardInfo))
        {
            <p class="alert alert-secondary bp-status-line is-info" role="status"><span>@cardInfo</span></p>
        }
        if (!string.IsNullOrWhiteSpace(cardError))
        {
            <p class="alert alert-danger bp-status-line is-problem" role="alert">
                <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 4.5v4.5M8 11.5v.5" /></svg>
                <span>@cardError</span>
            </p>
        }
        await Task.CompletedTask;
    }

    static string Size(long bytes) => bytes < 1024
        ? $"{bytes} bytes"
        : bytes < 1048576 ? $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB" : $"{(bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB";

    static string Source(string? source) => source switch
    {
        "form" => "typed on this page",
        "upload" => "from an upload",
        "feed" => "from the battery system",
        "system" => "set by the app",
        "seed" => "test data",
        _ => source ?? "not recorded",
    };
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <nav class="bp-breadcrumb" aria-label="Breadcrumb">
        <a href="/admin/batteries">Batteries</a>
        <span aria-current="page">@Model.SerialNumber</span>
    </nav>

    <header class="bp-page-heading bp-battery-heading">
        <span class="bp-summary-title">Battery</span>
        <div class="bp-battery-title">
            <h1>@Model.SerialNumber</h1>
            @if (publication.Active is { } current)
            {
                <span class="bp-field-marker is-published">Published · version @current.Number</span>
            }
            else
            {
                <span class="bp-field-marker">Draft · only your team sees it</span>
            }
        </div>
        <p class="mb-0">
            Model <a href="@ProductDataPages.FamilyHref(sources.Model.FamilyId, sources.Model.Id)">@sources.Model.Name</a> of @Model.Family.Name,
            made at @sources.Site.Name in @sources.Batch.Year.
        </p>
        <dl class="bp-battery-facts">
            <div class="bp-battery-fact-wide">
                <dt>Battery ID <span class="bp-attr-number">#6</span></dt>
                <dd><span class="bp-id-copy-row"><span class="bp-id-value">@Model.Identifier</span>@await Html.PartialAsync("_CopyIdButton", Model.Identifier)</span></dd>
            </div>
            <div>
                <dt>Batch</dt>
                <dd><a href="/admin/batches/@Uri.EscapeDataString(sources.Batch.Id)">@Model.BatchLabel</a></dd>
            </div>
            <div>
                <dt>Economic operator</dt>
                <dd><a href="@ProductDataPages.SitesHref("operator", sources.Operator.Id)">@sources.Operator.Name</a></dd>
            </div>
            <div>
                <dt>Cluster</dt>
                <dd>
                    <span class="@(Model.Pack.ClusterId is null ? "bp-batteries-muted" : null)">@Model.ClusterLabel</span>
                    <button type="button" class="bp-text-button" data-open-dialog="cluster-dialog">Change</button>
                </dd>
            </div>
            @if (publication.IsPublished)
            {
                <div>
                    <dt>Serial number <span class="bp-attr-number">#11</span></dt>
                    <dd>@Value("batterySerialNumber")</dd>
                </div>
                <div>
                    <dt>Manufacturing date <span class="bp-attr-number">#17</span></dt>
                    <dd>@Month(Value("manufacturingDate"))</dd>
                </div>
                <div>
                    <dt>Warranty <span class="bp-attr-number">#19</span></dt>
                    <dd>@Period(Value("warrantyPeriod"))</dd>
                </div>
            }
        </dl>
        @{ await CardMessagesAsync("cluster"); }
    </header>

    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
    @if (testIds)
    {
        <p class="alert alert-secondary bp-status-line is-info mt-3" role="note">
            <span>Test identifiers: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen (design 10).</span>
        </p>
    }

    <dialog id="cluster-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="cluster-dialog-title">
        <form method="post" action="@href/cluster">
            @Html.AntiForgeryToken()
            <h2 id="cluster-dialog-title">Change the cluster</h2>
            <p class="bp-help-text">The customer the battery belongs to. Its members see its purchaser data. The passport doesn't change.</p>
            <label class="bp-product-version-selected-field">
                <span>Cluster</span>
                <select name="clusterId">
                    <option value="" selected="@(Model.Pack.ClusterId is null)">No cluster yet</option>
                    @foreach (var cluster in Model.Clusters)
                    {
                        <option value="@cluster.Id" selected="@(string.Equals(cluster.Id, Model.Pack.ClusterId, StringComparison.OrdinalIgnoreCase))">@cluster.Name</option>
                    }
                </select>
            </label>
            <div class="bp-dialog-actions">
                <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                <button type="submit" class="bp-primary-button">Save cluster</button>
            </div>
        </form>
    </dialog>

    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="publication">
        <div class="bp-battery-card-head">
            <div>
                <span class="bp-summary-title">Publication</span>
                @if (publication.Active is { } active)
                {
                    <h2>Published since @When(active.PublishedAtUtc)</h2>
                    <p class="mb-0">Version @active.Number is live. A change to its values publishes a new version, and you're asked first; live values and accident reports update it in place.</p>
                }
                else if (publication.Ready)
                {
                    <h2>Ready to publish</h2>
                    <p class="mb-0">Every value the passport needs by February 2027 is there. Publishing makes version 1, signs it and shows it to its readers.</p>
                }
                else
                {
                    <h2>Not ready to publish yet</h2>
                    <p class="mb-0"><span class="bp-missing-note">@Words.Count(publication.MissingCount, "value") missing</span> that the passport needs by February 2027.</p>
                }
            </div>
            @if (!publication.IsPublished && publication.Ready)
            {
                <button type="button" class="bp-primary-button text-nowrap" data-open-dialog="publish-dialog">Publish passport</button>
            }
        </div>
        @{ await CardMessagesAsync("publication"); }

        @if (publication.Missing.Count > 0)
        {
            <ul class="bp-missing-groups">
                @foreach (var group in publication.Missing)
                {
                    <li>
                        <div class="bp-missing-group-head">
                            <strong>@group.Record</strong>
                            <a href="@group.Href">Fill in</a>
                        </div>
                        <span>@string.Join(", ", group.Attributes.Select(attribute => $"#{attribute.Number} {attribute.Name}"))</span>
                    </li>
                }
            </ul>
        }
        @if (publication.Problems.Count > 0)
        {
            <ul class="bp-missing-groups">
                @foreach (var problem in publication.Problems)
                {
                    <li><span class="text-danger">@problem</span></li>
                }
            </ul>
        }

        <div class="bp-battery-checks">
            @if (publication.NeededLater.Count > 0)
            {
                <details>
                    <summary>@Words.Count(publication.NeededLater.Count, "value") needed only after February 2027</summary>
                    <p class="mb-0">@string.Join(", ", publication.NeededLater.Select(attribute => $"#{attribute.Number} {attribute.Name}"))</p>
                </details>
            }
            @if (publication.Files.Count == 0)
            {
                <p class="mb-0 bp-batteries-muted">No uploaded files yet.</p>
            }
            else
            {
                <details open="@(filesChanged > 0)">
                    <summary>
                        @if (filesChanged == 0)
                        {
                            <span class="bp-done-note">Uploaded files: @publication.Files.Count of @publication.Files.Count unchanged</span>
                        }
                        else
                        {
                            <span class="bp-missing-note">Uploaded files: @filesChanged changed since upload</span>
                        }
                    </summary>
                    <ul class="bp-file-checks">
                        @foreach (var file in publication.Files)
                        {
                            <li>
                                <span>@file.Document.FileName · @file.Owner</span>
                                @if (file.Unchanged)
                                {
                                    <span class="bp-done-note">Unchanged</span>
                                }
                                else
                                {
                                    <span class="bp-missing-note">Changed or missing: its SHA-256 no longer matches</span>
                                }
                            </li>
                        }
                    </ul>
                    <small class="bp-field-help">Each file is checked against the SHA-256 fingerprint taken when it was uploaded.</small>
                </details>
            }
        </div>
    </section>

    @if (!publication.IsPublished && publication.Ready)
    {
        <dialog id="publish-dialog" class="bp-modal-panel bp-dialog" aria-labelledby="publish-dialog-title">
            <form method="post" action="@href/publish">
                @Html.AntiForgeryToken()
                <h2 id="publish-dialog-title">Publish this passport?</h2>
                <p class="bp-help-text">
                    Version 1 gets its passport ID and signature, and its readers see it: everyone the public values,
                    the cluster and authorities theirs. The serial number, manufacturing date and warranty can't change after this.
                </p>
                <div class="bp-dialog-actions">
                    <button type="submit" formmethod="dialog" formnovalidate class="bp-secondary-button">Cancel</button>
                    <button type="submit" class="bp-primary-button">Publish version 1</button>
                </div>
            </form>
        </dialog>
    }

    <div id="values">
        @await Html.PartialAsync("~/Views/ProductData/_RecordEditor.cshtml", Model.Values)
    </div>
    @if (publication.IsPublished)
    {
        <p class="bp-battery-fixed-note">The serial number, manufacturing date and warranty are fixed now that the passport is published; they're shown at the top.</p>
    }

    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="live">
        <span class="bp-summary-title">Live values</span>
        <h2>Measured on the battery</h2>
        <p class="mb-0">
            Its battery system sends these; type them here before it does, for example the end-of-line values, or to
            correct one. They change the passport in place, without a new version.
        </p>
        @{ await CardMessagesAsync("live"); }
        <form method="post" action="@Model.Live.SaveAction" class="bp-live-form">
            @Html.AntiForgeryToken()
            <input type="hidden" name="@ProductDataController.StampField" value="@Model.Live.Stamp" />
            <div class="bp-attr-list">
                @foreach (var field in liveFields)
                {
                    @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Live, field))
                    @if (Model.Pack.Values.GetValueOrDefault(field.Attribute.Property) is { } stored)
                    {
                        <p class="bp-live-meta">Last value @When(stored.GeneratedAtUtc), @Source(stored.Source)@(string.IsNullOrEmpty(stored.Who) ? string.Empty : $" by {stored.Who}").</p>
                    }
                }
            </div>
            <div class="bp-live-save">
                <label>
                    <span>Measured at <span class="bp-attr-optional">(optional; now if empty)</span></span>
                    <input type="datetime-local" name="@BatteriesController.MeasuredAtField" />
                </label>
                <button type="submit" class="bp-primary-button">Save live values</button>
            </div>
        </form>
    </section>

    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="accidents">
        <span class="bp-summary-title">Accident reports <span class="bp-attr-number">#92</span></span>
        <h2>@(Model.AccidentReports.Count == 0 ? "No accidents reported" : Words.Count(Model.AccidentReports.Count, "accident report"))</h2>
        <p class="mb-0">The passport links to the battery's accident page, which lists these reports or says none were reported. Seen only by the battery's cluster, market surveillance and your team.</p>
        @{ await CardMessagesAsync("accidents"); }
        @if (Model.AccidentReports.Count > 0)
        {
            <ul class="bp-file-checks">
                @foreach (var report in Model.AccidentReports)
                {
                    <li>
                        <span><strong>@report.Title</strong> · @report.FileName · @Size(report.Length)</span>
                        <span class="bp-batteries-muted">@When(report.UploadedAtUtc) by @report.UploadedBy</span>
                    </li>
                }
            </ul>
        }
        <form method="post" action="@href/accidents" enctype="multipart/form-data" class="bp-attr-upload bp-accident-upload">
            @Html.AntiForgeryToken()
            <div class="bp-attr-file">
                <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                    Choose file
                    <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" data-upload-file />
                </label>
                <span class="bp-help-text" data-upload-chosen>No file chosen</span>
            </div>
            <div class="bp-attr-parts">
                <label>
                    <span>Title (optional)</span>
                    <input type="text" name="title" placeholder="e.g. Collision on 12 Oct 2026" />
                </label>
                <label>
                    <span>Language (optional)</span>
                    <select name="language">
                        <option value=""></option>
                        @foreach (var language in Model.Values.Languages)
                        {
                            <option value="@language">@language</option>
                        }
                    </select>
                </label>
            </div>
            <div class="bp-live-save">
                <small class="bp-field-help">PDF, PNG or JPEG, checked by its content.</small>
                <button type="submit" class="bp-secondary-button" data-upload-button>Add report</button>
            </div>
        </form>
    </section>

    <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="versions">
        <span class="bp-summary-title">Versions</span>
        @if (Model.Versions.Count == 0)
        {
            <h2>No versions yet</h2>
            <p class="mb-0">Publishing makes version 1. Each later change to its values makes a new version; the older ones stay readable by their passport ID.</p>
        }
        else
        {
            <h2>@Words.Count(Model.Versions.Count, "version")</h2>
            <div class="bp-table-scroll">
                <table class="table align-middle bp-console-table bp-mobile-card-table bp-versions-table">
                    <thead>
                        <tr>
                            <th>Version</th>
                            <th>Passport ID <span class="bp-attr-number">#5</span></th>
                            <th>Status</th>
                            <th>Published</th>
                            <th>Signature</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var row in Model.Versions)
                        {
                            <tr>
                                <td data-label="Version">@row.Version.Number</td>
                                <td data-label="Passport ID"><span class="bp-id-copy-row"><span class="bp-id-value">@row.Version.DigitalProductPassportId</span>@await Html.PartialAsync("_CopyIdButton", row.Version.DigitalProductPassportId)</span></td>
                                <td data-label="Status">@(row.Version.DppStatus == DppStatuses.Active ? "Active" : "Archived")</td>
                                <td data-label="Published">@When(row.Version.PublishedAtUtc) <small class="bp-batteries-muted d-block">by @row.Version.PublishedBy</small></td>
                                <td data-label="Signature">
                                    @if (row.SignatureHolds)
                                    {
                                        <span class="bp-done-note">Verified</span>
                                    }
                                    else
                                    {
                                        <span class="bp-missing-note">Doesn't match</span>
                                    }
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </section>

    @if (!publication.IsPublished)
    {
    <details class="bp-card bp-remove-record mt-4" id="remove" open="@(card == "remove")">
        <summary>Remove this battery</summary>
        @{ await CardMessagesAsync("remove"); }
        @if (Model.Removal.Blocked is { } blocked)
        {
            <p class="mb-0 mt-2">It can't be removed: @blocked</p>
        }
        else
        {
            <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
                @Html.AntiForgeryToken()
                <span>It's a draft, so nothing has been published. Removing it deletes it with its accident reports; this can't be undone.</span>
                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove draft battery</button>
            </form>
        }
    </details>
    }
</main>

@section Scripts {
    <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 6: Write `NewBattery.cshtml`**

`web/Views/Batteries/NewBattery.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model NewBatteryPage
@{
    ViewData["Title"] = "Create battery";
    var fields = Model.Values.Sections.SelectMany(section => section.Fields).ToArray();
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <nav class="bp-breadcrumb" aria-label="Breadcrumb">
        <a href="/admin/batteries">Batteries</a>
        <span aria-current="page">Create battery</span>
    </nav>
    <header class="bp-page-heading">
        <span class="bp-summary-title">New battery</span>
        <h1>Create battery</h1>
        <p class="mb-0">
            The battery starts as a draft, seen only by your team. Its batch gives it the values of its model, batch and
            manufacturing site; its economic operator gives the operator's.
        </p>
    </header>

    @await Html.PartialAsync("~/Views/ProductData/_Messages.cshtml")
    @if (!Model.LinksConfigured)
    {
        <p class="alert alert-danger bp-status-line is-problem mt-3" role="alert">
            <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 4.5v4.5M8 11.5v.5" /></svg>
            <span>Batteries can't be created yet: the app's web address (APP_BASE_URL) isn't set, and each battery's accident page needs it. Set it where the app is started, then reload this page.</span>
        </p>
    }

    <form method="post" action="/admin/batteries" class="bp-card bp-console-form-card bp-new-battery mt-4" id="record-editor" data-record-editor data-cancel-href="/admin/batteries">
        @Html.AntiForgeryToken()
        <section class="bp-new-battery-part">
            <h2>Where it comes from</h2>
            <p class="bp-template-sync-note">Chosen once. A draft made with the wrong batch or operator is removed and created again.</p>
            <div class="bp-new-battery-choices">
                <label>
                    <span>Batch</span>
                    <select name="batchId" required>
                        <option value="">Choose the model, year and site</option>
                        @foreach (var family in Model.Batches.GroupBy(choice => choice.FamilyName))
                        {
                            <optgroup label="@family.Key">
                                @foreach (var choice in family)
                                {
                                    <option value="@choice.BatchId" selected="@(choice.BatchId == Model.BatchId)">@choice.Label</option>
                                }
                            </optgroup>
                        }
                    </select>
                    @if (Model.Batches.Count == 0)
                    {
                        <small class="bp-field-help">No batches yet: add one to a model on its <a href="/admin/families">battery family's page</a>.</small>
                    }
                </label>
                <label>
                    <span>Economic operator</span>
                    <select name="operatorId" required>
                        <option value="">Choose who places it on the market</option>
                        @foreach (var item in Model.Operators)
                        {
                            <option value="@item.Id" selected="@(item.Id == Model.OperatorId)">@item.Name</option>
                        }
                    </select>
                    @if (Model.Operators.Count == 0)
                    {
                        <small class="bp-field-help">No operators yet: add one under <a href="/admin/sites-and-operators">Sites and operators</a>.</small>
                    }
                </label>
                <label>
                    <span>Cluster <span class="bp-attr-optional">(optional)</span></span>
                    <select name="clusterId">
                        <option value="">No cluster yet</option>
                        @foreach (var cluster in Model.Clusters)
                        {
                            <option value="@cluster.Id" selected="@(string.Equals(cluster.Id, Model.ClusterId, StringComparison.OrdinalIgnoreCase))">@cluster.Name</option>
                        }
                    </select>
                    <small class="bp-field-help">The customer it belongs to. Not passport data; it can be set later.</small>
                </label>
            </div>
        </section>

        <section class="bp-new-battery-part">
            <h2>Its own values</h2>
            <p class="bp-template-sync-note">The battery ID (#6) is issued by the app, and its status starts as “original”.</p>
            <div class="bp-attr-list">
                @foreach (var field in fields)
                {
                    @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model.Values, field))
                }
            </div>
        </section>

        <div class="bp-product-editor-savebar">
            <span class="bp-savebar-text" data-save-status>The serial number is required; the rest can follow on the battery's page.</span>
            <div class="bp-savebar-actions">
                <button type="button" class="bp-secondary-button" data-cancel-edits data-cancel-href="/admin/batteries">Cancel</button>
                <button type="submit" class="bp-primary-button" disabled="@(!Model.LinksConfigured)">Create draft battery</button>
            </div>
        </div>
    </form>

    <div class="bp-modal" data-cancel-modal hidden>
        <div class="bp-modal-backdrop" data-cancel-dismiss></div>
        <div class="bp-modal-panel" role="dialog" aria-modal="true" aria-labelledby="cancel-edits-title" aria-describedby="cancel-edits-description">
            <h2 id="cancel-edits-title" class="h5 mb-2">Discard this battery?</h2>
            <p id="cancel-edits-description" class="mb-3">You have typed values that aren't saved. Leaving this page discards them.</p>
            <div class="d-flex justify-content-end flex-wrap gap-2">
                <button type="button" class="bp-secondary-button" data-cancel-stay>Keep editing</button>
                <button type="button" class="bp-danger-button" data-cancel-discard>Discard</button>
            </div>
        </div>
    </div>
</main>

@section Scripts {
    <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 7: Let the shared editor show a battery, and add the batteries finish**

Patch `2d2-task4-code.patch` (`web/Views/ProductData/_RecordEditor.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../2d2-task4-code.patch`, outside the worktree, and run `git apply ../2d2-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Views/ProductData/_RecordEditor.cshtml b/web/Views/ProductData/_RecordEditor.cshtml
--- a/web/Views/ProductData/_RecordEditor.cshtml
+++ b/web/Views/ProductData/_RecordEditor.cshtml
@@ -14,9 +14,10 @@
     var editorError = (TempData[ProductDataController.EditorErrorKey] ?? ViewData[ProductDataController.EditorErrorKey]) as string;
     var editorInfo = TempData[ProductDataController.EditorInfoKey] as string;
 
-    // A batch's page is already titled with its year and site; its editor is simply its passport values.
-    var eyebrow = Model.Kind == TargetKind.Batch ? Model.KindLabel : $"{Model.KindLabel} · passport values";
-    var title = Model.Kind == TargetKind.Batch ? "Passport values" : Model.Name;
+    // A batch's and a battery's pages are already titled; their editor is simply their passport values.
+    var titled = Model.Kind is TargetKind.Batch or TargetKind.Pack;
+    var eyebrow = titled ? Model.KindLabel : $"{Model.KindLabel} · passport values";
+    var title = titled ? "Passport values" : Model.Name;
 
     // Whose values these are: the batch page says it in its heading.
     var scope = Model.Kind switch
@@ -24,8 +25,16 @@
         TargetKind.Model => "Every battery of this model shares these values.",
         TargetKind.Site => "Every battery made at this site shares these values.",
         TargetKind.Operator => "Every battery this operator places on the market shares these values.",
+        TargetKind.Pack => "Only this battery has these values; the others come from its model, batch, site and operator.",
         _ => null,
     };
+    var publishedNote = Model.Kind == TargetKind.Pack
+        ? Model.PublishedPassports > 0
+            ? "Its passport is published: saving a change publishes a new version, and you're asked first."
+            : "Its passport is a draft: saving changes the draft only."
+        : Model.PublishedPassports > 0
+            ? $"Used by {Words.Count(Model.PublishedPassports, "published passport")}: saving a change publishes new versions, and you're asked first."
+            : "No published passport uses it yet.";
 }
 
 <section class="bp-card bp-console-form-card bp-editor-card mt-4" id="editor">
@@ -48,9 +57,7 @@
                     <span class="bp-done-mark" aria-hidden="true">✓</span>
                     <span>Every value the passport needs by Feb 2027 is filled in.</span>
                 }
-                <span>@(Model.PublishedPassports > 0
-                    ? $"Used by {Words.Count(Model.PublishedPassports, "published passport")}: saving a change publishes new versions, and you're asked first."
-                    : "No published passport uses it yet.")</span>
+                <span>@publishedNote</span>
             </p>
         </div>
         <div class="bp-editor-progress" data-completeness>
@@ -79,7 +86,7 @@
     }
 
     <div class="bp-editor-tools">
-        @if (Model.Kind != TargetKind.Batch)
+        @if (Model.Kind is TargetKind.Model or TargetKind.Site or TargetKind.Operator)
         {
             <label class="bp-editor-name">
                 <span>Name <em class="bp-field-marker">Not passport data</em></span>
@@ -133,7 +140,7 @@
                     <div class="bp-attr-list">
                         @foreach (var field in group.Fields)
                         {
-                            @await Html.PartialAsync("_EditorField", new EditorFieldView(Model, field))
+                            @await Html.PartialAsync("~/Views/ProductData/_EditorField.cshtml", new EditorFieldView(Model, field))
                         }
                     </div>
                 </section>
@@ -185,6 +192,8 @@
     </form>
 }
 
+@if (Model.Removal.Action.Length > 0)
+{
 <details class="bp-card bp-remove-record mt-4">
     <summary>Remove this @kindWord</summary>
     @if (Model.Removal.Blocked is { } blocked)
@@ -200,6 +209,7 @@
         </form>
     }
 </details>
+}
 
 <div class="bp-modal" data-upload-guard-modal hidden>
     <div class="bp-modal-backdrop" data-upload-dismiss></div>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -1634,3 +1634,318 @@ dialog.bp-dialog::backdrop {
 [data-theme="dark"] .bp-auditlog-more summary {
   color: #7ab6ff;
 }
+
+/* ==== Batteries (step 2d-2) ======================================================================================== */
+
+/* The list: search and "Create battery" beside the heading's text, then one quiet table. */
+.bp-batteries-tools {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  justify-content: space-between;
+  gap: 12px;
+}
+
+.bp-batteries-search {
+  display: flex;
+  flex: 1 1 420px;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 10px;
+  max-width: 640px;
+}
+
+.bp-batteries-search input {
+  flex: 1 1 300px;
+  min-width: 0;
+}
+
+.bp-batteries-clear {
+  font-weight: 600;
+}
+
+.bp-batteries-count {
+  margin: 0;
+  padding: 14px 16px 10px;
+  color: #52637a;
+}
+
+.bp-batteries-muted,
+.bp-battery-id {
+  color: #6b7a90;
+}
+
+.bp-batteries-table td small,
+.bp-battery-id {
+  display: block;
+  font-size: 0.82rem;
+}
+
+.bp-battery-id {
+  overflow-wrap: anywhere;
+}
+
+.bp-battery-link {
+  font-weight: 650;
+  text-decoration: none;
+}
+
+.bp-battery-link:hover,
+.bp-battery-link:focus-visible {
+  text-decoration: underline;
+}
+
+/* "Published · version 2" is done, so it is the only green tag; "Draft" stays a plain outlined tag. */
+main .bp-field-marker.is-published {
+  border-color: #b7dfc4;
+  color: #166534;
+}
+
+/* The battery page's heading: its serial number, status and the records its values come from. */
+.bp-battery-title {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: center;
+  gap: 6px 14px;
+}
+
+.bp-battery-title h1 {
+  margin-bottom: 4px;
+}
+
+.bp-battery-facts {
+  display: grid;
+  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
+  gap: 12px 28px;
+  margin: 18px 0 0;
+}
+
+.bp-battery-facts dt {
+  margin-bottom: 2px;
+  color: #6b7a90;
+  font-size: 0.82rem;
+  font-weight: 600;
+}
+
+.bp-battery-facts dd {
+  margin: 0;
+  overflow-wrap: anywhere;
+}
+
+/* The battery ID is long: it gets two columns where there is room. */
+@media (min-width: 721px) {
+  .bp-battery-facts .bp-battery-fact-wide {
+    grid-column: span 2;
+  }
+}
+
+.bp-battery-facts .bp-id-copy-row {
+  display: inline-flex;
+  align-items: center;
+  gap: 6px;
+}
+
+.bp-text-button {
+  margin-left: 8px;
+  padding: 0;
+  border: 0;
+  background: none;
+  color: var(--bp-primary);
+  font-weight: 600;
+}
+
+.bp-text-button:hover,
+.bp-text-button:focus-visible {
+  text-decoration: underline;
+}
+
+/* One card per part of the battery: publication, live values, accident reports, versions. */
+.bp-battery-card h2 {
+  margin: 2px 0 4px;
+  font-size: 1.25rem;
+  font-weight: 650;
+}
+
+.bp-battery-card > p,
+.bp-battery-card-head p {
+  max-width: 900px;
+  color: #52637a;
+}
+
+.bp-battery-card .bp-status-line {
+  margin: 14px 0 0;
+}
+
+.bp-battery-card-head {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: flex-start;
+  justify-content: space-between;
+  gap: 12px 24px;
+}
+
+/* What the passport still needs, under the record that holds it. */
+.bp-missing-groups {
+  display: grid;
+  gap: 10px;
+  margin: 16px 0 0;
+  padding: 0;
+  list-style: none;
+}
+
+.bp-missing-groups li {
+  display: grid;
+  gap: 2px;
+  padding: 10px 14px;
+  border: 1px solid #e6ebf1;
+  border-radius: 12px;
+}
+
+.bp-missing-group-head {
+  display: flex;
+  flex-wrap: wrap;
+  justify-content: space-between;
+  gap: 4px 16px;
+}
+
+.bp-missing-group-head a {
+  font-weight: 600;
+}
+
+.bp-missing-groups li > span {
+  color: #52637a;
+}
+
+.bp-battery-checks {
+  display: grid;
+  gap: 10px;
+  margin-top: 16px;
+  padding-top: 14px;
+  border-top: 1px solid #e6ebf1;
+}
+
+.bp-battery-checks summary {
+  width: fit-content;
+  color: #52637a;
+  font-weight: 600;
+  cursor: pointer;
+}
+
+.bp-battery-checks details p,
+.bp-battery-checks details ul {
+  margin-top: 8px;
+}
+
+.bp-file-checks {
+  display: grid;
+  gap: 6px;
+  margin: 14px 0 0;
+  padding: 0;
+  list-style: none;
+}
+
+.bp-file-checks li {
+  display: flex;
+  flex-wrap: wrap;
+  justify-content: space-between;
+  gap: 4px 16px;
+  padding: 6px 0;
+  border-bottom: 1px solid #eef2f6;
+}
+
+.bp-file-checks li:last-child {
+  border-bottom: 0;
+}
+
+.bp-battery-fixed-note {
+  margin: 8px 4px 0;
+  color: #6b7a90;
+  font-size: 0.9rem;
+}
+
+/* The live values: the editor's fields, each with when its value was measured and where it came from. */
+.bp-live-form {
+  margin-top: 14px;
+}
+
+.bp-live-meta {
+  margin: -6px 0 0;
+  color: #6b7a90;
+  font-size: 0.85rem;
+  text-align: right;
+}
+
+.bp-live-save {
+  display: flex;
+  flex-wrap: wrap;
+  align-items: flex-end;
+  justify-content: space-between;
+  gap: 12px 20px;
+  margin-top: 16px;
+  padding-top: 14px;
+  border-top: 1px solid #e6ebf1;
+}
+
+.bp-live-save label {
+  display: grid;
+  gap: 6px;
+}
+
+.bp-accident-upload {
+  margin-top: 16px;
+}
+
+/* A new battery: where it comes from, then its own values. */
+.bp-new-battery-part + .bp-new-battery-part {
+  margin-top: 22px;
+  padding-top: 18px;
+  border-top: 1px solid #e6ebf1;
+}
+
+.bp-new-battery-part h2 {
+  margin: 0 0 4px;
+  font-size: 1.25rem;
+  font-weight: 650;
+}
+
+.bp-new-battery-choices {
+  display: grid;
+  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
+  gap: 16px 24px;
+  margin-top: 12px;
+}
+
+.bp-new-battery-choices label {
+  display: grid;
+  align-content: start;
+  gap: 6px;
+}
+
+.bp-new-battery-choices label > span {
+  font-weight: 600;
+}
+
+[data-theme="dark"] :is(.bp-batteries-count, .bp-batteries-muted, .bp-battery-id, .bp-battery-facts dt, .bp-battery-card > p,
+  .bp-battery-card-head p, .bp-missing-groups li > span, .bp-battery-checks summary, .bp-battery-fixed-note, .bp-live-meta) {
+  color: var(--bp-muted);
+}
+
+[data-theme="dark"] :is(.bp-missing-groups li, .bp-battery-checks, .bp-file-checks li, .bp-live-save, .bp-new-battery-part + .bp-new-battery-part) {
+  border-color: rgba(148, 163, 184, 0.18);
+}
+
+[data-theme="dark"] main .bp-field-marker.is-published {
+  border-color: rgba(134, 239, 172, 0.35);
+  color: #86efac;
+}
+
+@media (max-width: 720px) {
+  .bp-batteries-tools,
+  .bp-batteries-search {
+    align-items: stretch;
+  }
+
+  .bp-live-meta {
+    text-align: left;
+  }
+}
```

- [ ] **Step 8: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.BatteriesControllerTests"`
Expected: `Passed!  - Failed:     0, Passed:    14`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1896`, none failed.

- [ ] **Step 9: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/BatteriesControllerTests.cs web/Controllers/BatteriesController.cs web/Views/Batteries/Batteries.cshtml web/Views/Batteries/Battery.cshtml web/Views/Batteries/NewBattery.cshtml web/Views/ProductData/_RecordEditor.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): the team's batteries pages on the admin layout"
```


---

### Task 5: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  1896, Skipped:     0, Total:  1896`: the 1844 from before, 5 fewer after Task 1 (six old tests go, one menu case comes), then 18, 25 and 14 new in Tasks 2, 3 and 4.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Look at the pages**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too, and sign in as a team member. Never point it at `battery_pass_demo`. Check, in light and dark theme:

1. **Batteries** (menu): the list shows each battery's serial number, battery ID, model, batch, cluster and passport (Draft, or Published · version n). Search by part of a serial number, a cluster name and a passport ID. The menu has no "Battery cluster assignments".
2. **Create battery:** choose a batch and an operator, leave the cluster as "No cluster yet", and type a serial number another battery has, in other letters: it is refused and marked, with what you typed kept. Change it and create: the draft's page opens.
3. **The draft's page:**
   - Publication lists what is missing under the record that holds it; each "Fill in" opens that editor.
   - Type the live values with a measurement time and save: they show when they were measured.
   - Once nothing is missing, "Publish passport" appears; publish: version 1, and the serial number, manufacturing date and warranty move to the heading.
4. **The published battery's page:**
   - Change the third-party service provider: it asks first, then publishes version 2; Versions lists both, signatures verified.
   - Change the cluster: the heading shows the new one; no new version.
   - Add an accident report (a PDF): it is listed, and Publication says the uploaded files are unchanged.
   - There is no Remove box.
5. **A draft you don't need:** remove it from its page; the list says it was removed.
6. **Registered clusters:** deleting a cluster that has batteries says how many.
7. **Audit log:** the cluster move and the removal read as plain sentences, and a battery's record links to its page.
8. **A narrow window** (about 390 px): no page scrolls sideways.

Remove the batteries you created; drafts offer Remove.

## After this plan

- **Step 3** adds the access-checked download route and the accident page that #92 already points to, so accident reports and restricted documents get download links; it also removes the old views the cluster-admin pages still render, with their unreachable admin branches, and rewrites the help pages.
- **E1–E4 (decision 28)** get their own small plan after this one and before step 5's BMS adapter: catalogue entries beyond the file, a form and checks without a schema entry, "E1"–"E4" labels, and the compliance test keeping the 73 file attributes exact. The battery page needs no change for them: its live-values form is built from the catalogue's live values, so they appear there once they are in it.
- **Step 4** adds the status change, successor passports (which may share a serial number with their predecessor), transfers between clusters and the cluster admins' editing.
- **Known small gaps:**
  - The live-values form has no "unsaved edits" guard of its own: the editor script guards one form per page, the battery's passport values. Leaving the page after typing live values doesn't ask.
  - A "Measured at" time is read in the server's time zone, which on your PC is your own; on a server in another zone it would be that server's.

