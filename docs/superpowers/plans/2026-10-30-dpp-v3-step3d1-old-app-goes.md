# DPP V3 Step 3d-1: The Old App's Code Goes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The old v1.2 app's code goes, now that the new pages and API replace it. It covers:
- the cluster admins' old passport pages;
- the old services, models and views;
- the old files API;
- the startup's sample battery, sample tokens and data repairs;
- the demo settings and the old battery pictures;
- the old Next.js app;
- the tests of all this.

The app keeps working on the new data; nothing in the database changes. The reset follows in 3d-2.

**Architecture:** Mostly deletions. What stays is trimmed to what the new app uses:
- `ClusterAdminController` keeps the cluster admins' users and API token pages;
- `AccessControlService` keeps the role names, labels and cluster-admin checks;
- `ExternalApiInitializer` keeps the token indexes and each cluster's default read and read-write tokens;
- the API help page's workbench takes the reader's own token;
- `Program.cs` registers only the services the new app uses;
- the audit log page's model moves to its own file.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`: section 13, Appendix C and the 3d bullet of section 14, with the 3d-1 and 3d-2 split recorded there on 30 Oct 2026. Decisions record: decisions 1, 2, 23, 29 and 30. Earlier plans: steps 1, 2a–2d-2, 3a, 3b, 3c-1 and 3c-2 in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 903894d (the review fixes of decisions 30–31, the 3d split and part 3e) plus the commit that adds this plan. Check with `git branch --show-current`.
- **Close the local app first.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Restart it after the plan.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2158`. After this plan: 1980. The suite takes about a minute.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3d1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order. Rewritten files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`; everything removed here stays there. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **No data changes:** this plan changes no stored data. The old data and the four sample tokens stay in `battery_pass_v3` until 3d-2's reset; the app no longer uses them.

## Decisions taken with the product owner (30 Oct 2026)

1. **3d in two parts:** this plan removes the old app's code; 3d-2 resets `battery_pass_v3` after the product owner has reviewed the accounts.
2. **The `BatteryPass` folder stays** in the repository as reference, with its README. It holds the v1.2 aspect models the repository was copied from. The app no longer reads it (amending design section 13).
3. **The old Next.js app (`web-next-legacy`) goes.**
4. **The audit log stays** through 3d-2's reset.
5. **The prototype was approved as shown.** The product owner also asked for a cleaner, friendlier look for the admin, cluster admin and help pages; that is part 3e, after 3d, with mock-ups first. 3e also makes the help one place that shows each reader only the parts for their role.

## Judgement calls in this plan

1. **What goes:**
   - the cluster admins' old passport pages (list, a battery's passports, edit, conformance, audit, revisions) with their actions;
   - the old services: templates, passports, batteries, trust and signing workflow, validation, readiness, evidence, editable fields, data completion, demo completion, snapshots, the battery ID service and the battery table;
   - the old models and view models, and the views only they used;
   - `FilesApiController`;
   - the admin's old passport counts when a cluster is deleted;
   - the four old battery pictures.

   **What stays:** every new page and the API; users, clusters, roles, sign-in, API tokens, the audit log, QR codes, the signing key, the settings service and the demo admin sign-in fallback (a local testing aid, off unless set).
2. **The cluster admins' workspace** opens on Users. The old "Managed passports" address leads to the Registry, which replaces it (decision 30), and the old secrets address leads to API tokens. The tab row is Users and API token management. The old passport addresses answer 404, with no redirects (decision 23).
3. **At startup** the app prepares the token and cluster indexes and gives each registered cluster its default read and read-write tokens. It no longer creates a default "Full access" token for a cluster, because a cluster token neither creates nor publishes (3c-2); existing ones stay. It no longer creates the sample battery or the sample tokens, sets old battery pictures or repairs old passports.
4. **The API help page's workbench** takes the reader's own token, pasted from API Token Management. Its value is shown once, when the token is created. The sample token presets and the sample tokens in the toolbar go; the "readable values" link uses the token typed in the workbench.
5. **Settings:** `EnableDemoData`, `EnablePublicDemoReadToken`, `EnableDemoWriteSignTesting` and `IdGenerationSecret` (with their environment variables) go: nothing reads them any more.
6. **Older tests:** 30 test files go with what they tested (the removed services, pages and the old Next.js app's checks); 26 keep their checks of what stays and lose only their lines about removed files. One new test file, `OldAppRemovedTests`, checks what went and what stays.
7. **Small tidy-ups** noted after 3c-2: the token-name placeholder no longer says "telemetry"; the unreferenced `ExternalApiRepository.ValidateSignPublish` goes; `.ebignore` no longer names the removed folder.

## File structure

| File | Responsibility |
|---|---|
| `web/Controllers/ClusterAdminController.cs` (replace) | The cluster admins' users and API token pages; old addresses lead to Users, the Registry and API tokens |
| `web/Services/AccessControlService.cs` (replace) | Role names, labels and the cluster-admin checks the new app uses |
| `web/Services/ExternalApiInitializer.cs` (replace) | Token and cluster indexes, and each cluster's default tokens |
| `web/Controllers/AdminController.cs`, `HelpController.cs`, `web/Program.cs`, `web/Configuration/BatteryPassOptions.cs`, the settings files (modify) | The old services, sample tokens and demo settings removed |
| `web/Views/Help/Index.cshtml`, `web/Models/ViewModels/ExternalApiHelpViewModel.cs` (modify) | The workbench takes the reader's own token |
| `web/Models/ViewModels/ApplicationAuditLogViewModel.cs` | The audit log page's model, in its own file |
| `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Views/Shared/_ClusterAdminTabs.cshtml`, the token views (modify) | The old template models, "Managed passports" and the telemetry wording removed |
| The old services, models, views, pictures and `web-next-legacy` (delete) | The v1.2 app |
| `BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs` | What went and what stays |


---

### Task 1: The old app's pages, services, sample data and tests go

The old services, models and views go with the tests that read them; what stays is trimmed to what the new app uses. Thirty test files go and twenty-six lose only their lines about removed files, so Step 1 is long: deletions, one patch for the tests that stay, and the new `OldAppRemovedTests`.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/AppRolesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs`
- Test (modify): `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/QrWorkflowTests.cs`
- Test (modify): `BatteryPassWeb.Tests/SecurityLimitsTests.cs`
- Test (modify): `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs`
- Test (delete): `BatteryPassWeb.Tests/AdminEditGuidanceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs`
- Test (delete): `BatteryPassWeb.Tests/AuditRevisionServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/BatteryAuditServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/BatteryIdServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs`
- Test (delete): `BatteryPassWeb.Tests/CanonicalPassportSnapshotServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/DataCompletionPolicyServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/DemoRequiredDataCompletionServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/DocumentAccessControlTests.cs`
- Test (delete): `BatteryPassWeb.Tests/ExternalApiInitializerTests.cs`
- Test (delete): `BatteryPassWeb.Tests/JsonSchemaValidationServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/LegacyNextExposureTests.cs`
- Test (delete): `BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs`
- Test (delete): `BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportCanonicalDataTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportEvidenceServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportReadinessServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportStatusLabelTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportTrustServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PassportValidationServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/ProductTemplateServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs`
- Test (delete): `BatteryPassWeb.Tests/SchemaRegistryServiceTests.cs`
- Test (delete): `BatteryPassWeb.Tests/SearchRegexHardeningTests.cs`
- Test (delete): `BatteryPassWeb.Tests/TrustDirtyStateTests.cs`
- Test (delete): `BatteryPassWeb.Tests/TrustModelsTests.cs`
- Test (delete): `BatteryPassWeb.Tests/TrustWorkflowAcceptanceTests.cs`
- Replace: `web/Controllers/ClusterAdminController.cs`
- Create: `web/Models/ViewModels/ApplicationAuditLogViewModel.cs`
- Replace: `web/Services/AccessControlService.cs`
- Replace: `web/Services/ExternalApiInitializer.cs`
- Modify: `web/Configuration/BatteryPassOptions.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/HelpController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Models/ViewModels/ExternalApiHelpViewModel.cs`
- Modify: `web/Program.cs`
- Modify: `web/Services/ExternalApiRepository.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/ApiTokens.cshtml`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: `web/Views/Shared/_ClusterAdminTabs.cshtml`
- Modify: `web/appsettings.Development.json`
- Modify: `web/appsettings.json`
- Delete: `web/Controllers/FilesApiController.cs`
- Delete: `web/Models/Trust/DataCompletionPolicyModels.cs`
- Delete: `web/Models/Trust/PassportEvidenceModels.cs`
- Delete: `web/Models/Trust/PassportReadinessModels.cs`
- Delete: `web/Models/Trust/TrustModels.cs`
- Delete: `web/Models/ViewModels/BatteryViewModels.cs`
- Delete: `web/Models/ViewModels/ConformanceViewModel.cs`
- Delete: `web/Models/ViewModels/EditPassportViewModel.cs`
- Delete: `web/Models/ViewModels/PassportDetailViewModel.cs`
- Delete: `web/Models/ViewModels/PassportSummaryPageViewModel.cs`
- Delete: `web/Models/ViewModels/PassportSummaryViewModel.cs`
- Delete: `web/Models/ViewModels/PassportViewModel.cs`
- Delete: `web/Models/ViewModels/SearchPageViewModel.cs`
- Delete: `web/Services/AuditRevisionService.cs`
- Delete: `web/Services/BatteryAuditService.cs`
- Delete: `web/Services/BatteryIdService.cs`
- Delete: `web/Services/BatteryImageCatalog.cs`
- Delete: `web/Services/BatteryPassCanonicalDataCatalog.cs`
- Delete: `web/Services/BatteryPassportDeltaService.cs`
- Delete: `web/Services/BatteryPassportSnapshotService.cs`
- Delete: `web/Services/BatteryRepository.cs`
- Delete: `web/Services/BatteryRouteResolutionService.cs`
- Delete: `web/Services/BatteryTableService.cs`
- Delete: `web/Services/BatteryTemplateUpdateService.cs`
- Delete: `web/Services/CanonicalPassportSnapshotService.cs`
- Delete: `web/Services/DataCompletionPolicyService.cs`
- Delete: `web/Services/DemoRequiredDataCompletionService.cs`
- Delete: `web/Services/EditableFieldPolicyService.cs`
- Delete: `web/Services/JsonSchemaValidationService.cs`
- Delete: `web/Services/PassportDataNormalizationService.cs`
- Delete: `web/Services/PassportEvidenceService.cs`
- Delete: `web/Services/PassportPublishPolicyService.cs`
- Delete: `web/Services/PassportReadinessService.cs`
- Delete: `web/Services/PassportRepository.cs`
- Delete: `web/Services/PassportTrustService.cs`
- Delete: `web/Services/PassportTrustWorkflowService.cs`
- Delete: `web/Services/PassportValidationService.cs`
- Delete: `web/Services/PassportViewModelFactory.cs`
- Delete: `web/Services/ProductTemplateModels.cs`
- Delete: `web/Services/ProductTemplateService.cs`
- Delete: `web/Services/SchemaRegistryService.cs`
- Delete: `web/Services/SearchRegexBuilder.cs`
- Delete: `web/Views/Admin/Audit.cshtml`
- Delete: `web/Views/Admin/Conformance.cshtml`
- Delete: `web/Views/Admin/EditPassport.cshtml`
- Delete: `web/Views/Admin/Revisions.cshtml`
- Delete: `web/Views/ClusterAdmin/BatteryPassports.cshtml`
- Delete: `web/Views/ClusterAdmin/Index.cshtml`
- Delete: `web/Views/ClusterAdmin/Passports.cshtml`
- Delete: `web/Views/Home/Search.cshtml`
- Delete: `web/Views/Shared/_AuditEventRow.cshtml`
- Delete: `web/Views/Shared/_BatteryTable.cshtml`
- Delete: `web/wwwroot/images/compact13.png`
- Delete: `web/wwwroot/images/compact7.png`
- Delete: `web/wwwroot/images/core.png`
- Delete: `web/wwwroot/images/sample-battery.png`

- [ ] **Step 1: Write the failing tests**

Patch `3d1-task1-tests.patch` (`BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AdminHelpPageTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs`, `BatteryPassWeb.Tests/ConformanceLayoutTests.cs`, `BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs`, `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`, `BatteryPassWeb.Tests/Dpp/AppRolesTests.cs`, `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`, `BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs`, `BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`, `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`, `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`, `BatteryPassWeb.Tests/QrWorkflowTests.cs`, `BatteryPassWeb.Tests/SecurityLimitsTests.cs`, `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`, `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`). Save the block as `../3d1-task1-tests.patch`, outside the worktree, and run `git apply ../3d1-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -20,49 +20,16 @@ public sealed class AdminDenseConsoleLayoutTests
     public void Registry_ShouldUseDenseConsoleTableAndToolbar()
     {
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("bp-registry-page", registry);
         Assert.Contains("bp-console-toolbar", registry);
         Assert.Contains("bp-console-table", registry); // the Registry's own table on the new model (decision 30)
-        Assert.Contains("bp-table-card", batteryTable);
-        Assert.Contains("bp-console-table", batteryTable);
-        Assert.Contains("bp-battery-id-cell", batteryTable);
-        Assert.Contains("bp-status-pill", batteryTable);
-        Assert.Contains("bp-action-cell", batteryTable);
         Assert.Contains(".bp-console-table", css);
         Assert.Contains(".bp-battery-id-cell", css);
         Assert.Contains("white-space: nowrap", css);
     }
 
-    [Fact]
-    public void LocalAdminPassports_ShouldUseBatteryIdentityColumnsAndIconActions()
-    {
-        var passports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var service = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
-
-        Assert.Contains("_BatteryTable", passports);
-        Assert.Contains("Battery ID", batteryTable);
-        Assert.Contains("<th>Passports</th>", batteryTable);
-        Assert.Contains("<th>Status</th>", batteryTable);
-        Assert.Contains("<th>Family</th>", batteryTable);
-        Assert.Contains("<th>Model</th>", batteryTable);
-        Assert.Contains("Battery serial number", batteryTable);
-        Assert.Contains("LatestPassportStatus", batteryTable);
-        Assert.Contains("@row.BatteryFamily", batteryTable);
-        Assert.Contains("@row.BatteryModel", batteryTable);
-        Assert.Contains("@row.BatterySerialNumber", batteryTable);
-        Assert.Contains("bp-console-table", batteryTable);
-        Assert.Contains("bp-battery-id-cell", batteryTable);
-        Assert.Contains("aria-label=\"Edit battery\"", batteryTable);
-        Assert.DoesNotContain("<th class=\"px-4 py-3\">Model</th>", passports);
-        Assert.DoesNotContain("@row.ModelNumber", passports);
-        Assert.Contains("_batteryRepository.ToSummary", service);
-        Assert.Contains("serial", service, StringComparison.OrdinalIgnoreCase);
-    }
-
     [Fact]
     public void AdminUxPolish_ShouldUseExplicitRolesDuplicateClusterValidationAndStandardSelects()
     {
@@ -176,20 +143,16 @@ public sealed class AdminDenseConsoleLayoutTests
     [Fact]
     public void LocalAdminEditAndUsers_ShouldShareDenseConsoleStructure()
     {
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var users = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
         var tokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
         var localTabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.Contains("_ClusterAdminTabs", users);
-        Assert.DoesNotContain("bp-local-admin-nav", edit);
         Assert.Contains("_ClusterAdminTabs", tokens);
         Assert.Contains("bp-local-admin-nav", localTabs);
         Assert.Contains("bp-user-management-table", users);
         Assert.DoesNotContain("bp-console-split", users);
-        Assert.Contains("bp-edit-grid", edit);
-        Assert.Contains("bp-field-shell", edit);
         Assert.Contains("bp-console-table", tokens);
         Assert.Contains(".bp-local-admin-nav", css);
         Assert.Contains(".bp-console-split", css);
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -18,19 +18,14 @@ public sealed class AdminFeedbackFollowupTests
     public void ReportActions_ShouldUseConsistentIconButtonsWithDesignedTooltips()
     {
         var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
         var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        var combined = string.Join(Environment.NewLine, registry, batteryTable, hero);
+        var combined = string.Join(Environment.NewLine, registry, hero);
         Assert.Contains("bp-report-action", combined);
-        Assert.Contains("data-tooltip=\"Summary report\"", batteryTable);
-        Assert.Contains("data-tooltip=\"Detailed report\"", batteryTable);
         Assert.Contains("View more about this passport", hero);
         Assert.Contains("Back to summary", hero);
         Assert.Contains("bp-report-action-icon", combined);
-        Assert.DoesNotContain("<span aria-hidden=\"true\">S</span>", batteryTable);
-        Assert.DoesNotContain("<span aria-hidden=\"true\">D</span>", batteryTable);
         Assert.Contains(".bp-report-action::after", css);
         Assert.Contains("top: calc(-100% -", css);
     }
@@ -110,71 +105,13 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains(".bp-token-cluster-chip-picker", css);
     }
 
-    [Fact]
-    public void RegistryController_ShouldPreserveBatteryIdentityFieldsForNormalUsers()
-    {
-        // The Registry lists the new model's batteries (decision 30); the old table stays only for the cluster admins'
-        // pages until 3d.
-        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var service = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
-
-        Assert.Contains("@row.SerialNumber", registry);
-        Assert.Contains("@row.Identifier", registry);
-        Assert.Contains("_batteryRepository.ToSummary", service);
-        Assert.Contains("LatestPassportStatus", batteryTable);
-    }
-
-    [Fact]
-    public void RemovedNameAndModelNumber_ShouldNotBlockPassportValidationOrCreation()
-    {
-        var policy = DataCompletionPolicyService.CreateDefaultPolicy();
-        var fields = policy.Sections.SelectMany(section => section.Fields).ToList();
-        var document = new BsonDocument
-        {
-            ["passportId"] = "did:web:acme.battery.pass:identity-without-model",
-            ["registryInfo"] = new BsonDocument
-            {
-                ["registryId"] = "registry-001",
-                ["status"] = "draft"
-            },
-            ["app"] = new BsonDocument
-            {
-                ["display"] = new BsonDocument
-                {
-                    ["serialNumber"] = "SERIAL-1",
-                    ["manufacturerName"] = "ACME Batteries",
-                    ["facilityId"] = "FAC-1"
-                },
-                ["product"] = new BsonDocument
-                {
-                    ["productId"] = "compact-7m",
-                    ["productVersion"] = "1.0",
-                    ["softwareVersion"] = "2.0"
-                }
-            }
-        };
-
-        var summary = new PassportValidationService(new SchemaRegistryService(), new JsonSchemaValidationService())
-            .Validate(document, policy);
-        var issuePaths = summary.Sections.SelectMany(section => section.Issues).Select(issue => issue.Path).ToList();
-
-        Assert.DoesNotContain(fields, field => field.FieldKey == "general.name");
-        Assert.DoesNotContain(fields, field => field.FieldKey == "general.modelNumber");
-        Assert.DoesNotContain("app.display.name", issuePaths);
-        Assert.DoesNotContain("app.display.modelNumber", issuePaths);
-    }
-
     [Fact]
     public void PublicSearchEmptyCopy_ShouldSayActivePublicPassport()
     {
         var home = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));
-        var search = File.ReadAllText(RepoFile("web", "Views", "Home", "Search.cshtml"));
 
         Assert.Contains("No published battery passport was found", home);
-        Assert.Contains("No active public battery passport was found", search);
         Assert.DoesNotContain("No published, verified battery passport ID was found", home);
-        Assert.DoesNotContain("No published, verified battery passport ID was found", search);
     }
 
     [Fact]
@@ -205,38 +142,6 @@ public sealed class AdminFeedbackFollowupTests
         Assert.Contains("@media (max-width: 760px)", css);
     }
 
-    [Fact]
-    public void FollowupSharedBatteryTable_ShouldUseCompactColumnsAndStatusCell()
-    {
-        var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-
-        var expectedOrder = new[]
-        {
-            "<th>Battery serial number</th>",
-            "<th>Battery ID</th>",
-            "<th>Family</th>",
-            "<th>Model</th>",
-            "<th>Cluster</th>",
-            "<th>Passports</th>",
-            "<th>Status</th>",
-            "<th>Updated</th>",
-            "<th class=\"bp-action-cell\">Actions</th>"
-        };
-        var previousIndex = -1;
-        foreach (var heading in expectedOrder)
-        {
-            var index = table.IndexOf(heading, StringComparison.Ordinal);
-            Assert.True(index > previousIndex, $"{heading} should appear after the previous compact table column.");
-            previousIndex = index;
-        }
-
-        Assert.Contains("row.DisplayStatus", table);
-        Assert.Contains("New passport needed", table);
-        Assert.DoesNotContain("Latest passport status", table);
-        Assert.Contains("bp-battery-table-toolbar", registry);
-    }
-
     [Fact]
     public void FollowupUserAndClusterUx_ShouldPreserveInputAndDrawerState()
     {
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -2,19 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class AdminFeedbackImplementationTests
 {
-    [Fact]
-    public void GlobalReportRoles_ShouldHaveAllClusterReadScopeAndDraftRules()
-    {
-        var accessControl = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
-
-        Assert.Contains("HasAllClusterReadScope", accessControl);
-        Assert.Contains("CanSeeUnpublishedAcrossClusters", accessControl);
-        Assert.Contains("RoleMarketSurveillanceAuthority", accessControl);
-        Assert.Contains("RoleCommission", accessControl);
-        Assert.Contains("HasAllClusterReadScope(user)", batteryTable);
-        Assert.DoesNotContain("Cluster search requires global admin access.", batteryTable);
-    }
 
     [Fact]
     public void RegisteredClusters_ShouldExposeUserOverviewDropdownAndCreateUserCopy()
@@ -47,104 +34,6 @@ public sealed class AdminFeedbackImplementationTests
         Assert.Contains("[\"liTreatment\"]", clustersView);
     }
 
-    [Fact]
-    public void EditableFields_ShouldSeparateClusterAdminVisibilityFromEditability()
-    {
-        var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
-        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var editModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));
-
-        Assert.Contains("VisibleToClusterAdmin", policy);
-        Assert.Contains("VisibleToClusterAdminCount", policy);
-        Assert.Contains("EditableFieldPolicyService", clusterAdminController);
-        Assert.Contains("FieldVisibleByKey", editModel);
-        Assert.Contains("data-visible-field-policy", sharedEditView);
-        Assert.Contains("applyVisibleFieldPolicy", sharedEditView);
-    }
-
-    [Fact]
-    public void ClusterAdminPassportLifecycle_ShouldUseSharedHistoryAndConformanceUx()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var table = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
-        var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var conformanceView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var auditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
-        var revisionsView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Revisions.cshtml"));
-        var clusterHistoryView = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "BatteryPassports.cshtml"));
-
-        Assert.Contains("[HttpGet(\"batteries/{batteryId}/passports\")]", controller);
-        Assert.Contains("[HttpGet(\"passports/{passportId}/conformance\")]", controller);
-        Assert.Contains("View(\"~/Views/Admin/EditPassport.cshtml\", model)", controller);
-        Assert.Contains("View(\"~/Views/Admin/Conformance.cshtml\"", controller);
-        Assert.True(File.Exists(RepoPath("web", "Views", "ClusterAdmin", "BatteryPassports.cshtml")));
-        Assert.Contains("/cluster-admin/batteries/{escapedBatteryId}/passports", table);
-        Assert.Contains("$\"/cluster-admin/passports/{Uri.EscapeDataString(row.LatestPassportId)}/conformance\"", table);
-        Assert.Contains("isClusterEdit", sharedEditView);
-        Assert.Contains("clusterEditBasePath", sharedEditView);
-        Assert.DoesNotContain("bp-local-admin-nav", sharedEditView);
-        Assert.Contains("workflowBasePath", conformanceView);
-        Assert.Contains("Validate passport", conformanceView);
-        Assert.Contains("Sign passport", conformanceView);
-        Assert.Contains("Publish passport", conformanceView);
-        Assert.Contains("workflowBasePath", auditView);
-        Assert.Contains("workflowBasePath", revisionsView);
-        Assert.Contains("/cluster-admin/passports/@Uri.EscapeDataString(passport.PassportId)/conformance", clusterHistoryView);
-    }
-
-    [Fact]
-    public void ClusterAdminBatteryEdits_ShouldUpdateBatteryAndReuseNewPassportLogic()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("_batteryRepository.GetByBatteryIdAsync", controller);
-        Assert.Contains("ApplyLocalBatteryForm", controller);
-        Assert.Contains("_batteryPassportDeltaService.UpdateNewPassportRequired", controller);
-        Assert.DoesNotContain("ApplyLocalPassportForm(document", controller);
-        Assert.DoesNotContain("_passportRepository.ReplaceAsync(passportId, document", controller);
-        Assert.Contains("Mode = \"cluster-edit\"", controller);
-        Assert.Contains("FieldEditableByKey = BuildEditableFieldDictionary(editablePolicy)", controller);
-        Assert.Contains("FieldVisibleByKey = BuildVisibleFieldDictionary(editablePolicy)", controller);
-        Assert.Contains("return Redirect($\"/cluster-admin/passports?q={Uri.EscapeDataString(batteryId)}\")", controller);
-        Assert.Contains("data-edit-passport-form", sharedEditView);
-        Assert.Contains("name=\"productVersion\"", sharedEditView);
-        Assert.Contains("name=\"softwareVersion\"", sharedEditView);
-        Assert.Contains("Save battery", sharedEditView);
-    }
-
-    [Fact]
-    public void ClusterAdminProductTemplateEnumFields_ShouldRenderAsDropdowns()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var editView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("ProductTemplateService", controller);
-        Assert.Contains("_productTemplateService.ListProductsAsync", controller);
-        Assert.Contains("ProductTemplates = BuildProductTemplateSummaries(products)", controller);
-        Assert.Contains("ProductTemplateCatalog = BuildProductTemplateFormCatalog(products)", controller);
-        Assert.Contains("SelectedProductId = selectedProduct.ProductId", controller);
-        Assert.Contains("SelectedProductVersion = selectedVersion.Version", controller);
-        Assert.Contains("data-product-template-catalog", editView);
-        Assert.Contains("name=\"productVersion\" data-product-version-select", editView);
-        Assert.Contains("name=\"softwareVersion\" data-product-software-version-select", editView);
-        Assert.Contains("function updateProductVersionOptions", editView);
-        Assert.Contains("function updateSoftwareParameters", editView);
-        Assert.DoesNotContain("<input type=\"text\" name=\"productVersion\"", editView);
-        Assert.DoesNotContain("<input type=\"text\" name=\"softwareVersion\"", editView);
-    }
-
-    [Fact]
-    public void PassportCreation_ShouldBeGuardedWhenNoNewPassportIsNeeded()
-    {
-        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var historyModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
-
-        Assert.Contains("CanCreateBatteryPassport", clusterAdminController);
-        Assert.Contains("CanCreatePassport", historyModel);
-    }
-
     [Fact]
     public void ExternalApi_ShouldListAccessibleClustersAndUseLifecycleTokens()
     {
@@ -167,21 +56,6 @@ public sealed class AdminFeedbackImplementationTests
         Assert.Contains("@Model.BasePath/clusters", helpView);
     }
 
-    [Fact]
-    public void ResetSeed_ShouldIncludeGlobalReadRoleUsersAndLifecycleToken()
-    {
-        var service = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));
-
-        Assert.Contains("notified.body@example.test", service);
-        Assert.Contains("msa@example.test", service);
-        Assert.Contains("commission@example.test", service);
-        Assert.Contains("legitimate.interest@example.test", service);
-        Assert.Contains("SampleLifecycleTokenId", initializer);
-        Assert.Contains("SampleLifecycleTokenValue", initializer);
-        Assert.Contains("ExternalTokenAccessMode.Lifecycle", service);
-    }
-
     [Fact]
     public void PowerAutomateSetup_ShouldBeDocumentedWithEnvironmentKeys()
     {
diff --git a/BatteryPassWeb.Tests/AdminHelpPageTests.cs b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
--- a/BatteryPassWeb.Tests/AdminHelpPageTests.cs
+++ b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
@@ -166,16 +166,6 @@ public sealed class AdminHelpPageTests
         Assert.Contains(".bp-admin-help-status-table", css);
     }
 
-    [Fact]
-    public void AdminPages_ShouldLinkToAdminHelp()
-    {
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-
-        Assert.Contains("/admin/help", edit);
-        Assert.Contains("/admin/help", conformance);
-    }
-
     [Fact]
     public void AdminHelpCss_ShouldDefineWorkflowLayout()
     {
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -2,36 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class AdminTestFeedbackFixesTests
 {
-    [Fact]
-    public void BatteryPages_ShouldUseSharedBatteryTableServiceAndPartial()
-    {
-        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
-        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var clusterPassports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
-
-        Assert.True(File.Exists(RepoFile("web", "Services", "BatteryTableService.cs")));
-        Assert.True(File.Exists(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml")));
-        Assert.Contains("AddSingleton<BatteryTableService>", program);
-        Assert.Contains("BatteryTablePageViewModel", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs")));
-        Assert.Contains("RegistryList.Build", File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"))); // decision 30
-        Assert.Contains("BatteryTableService", File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs")));
-        Assert.DoesNotContain("_BatteryTable", registry);
-        Assert.Contains("_BatteryTable", clusterPassports);
-    }
-
-    [Fact]
-    public void SharedBatteryTable_ShouldIncludeClusterNewPassportAndRoleAwareActions()
-    {
-        var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-
-        Assert.Contains("<th>Cluster</th>", table);
-        Assert.Contains("New passport needed", table);
-        Assert.Contains("row.CanEditBattery", table);
-        Assert.Contains("row.CanCreatePassport", table);
-        Assert.Contains("row.CanOpenConformance", table);
-        Assert.Contains("row.HistoryUrl", table);
-        Assert.Contains("row.ReturnUrl", table);
-    }
 
     [Fact]
     public void UsersAndMemberships_ShouldUseAccessDropdownAndApprovedClusterRoles()
@@ -59,37 +29,6 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("Cluster Member", accessControl);
     }
 
-    [Fact]
-    public void BatteryCreateAndEdit_ShouldRenderPolicyLockedFieldsAndClusterSelector()
-    {
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
-        var delta = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));
-
-        Assert.Contains("name=\"clusterId\"", edit);
-        Assert.Contains("data-editable-field-state", edit);
-        Assert.Contains("is-locked-field", edit);
-        Assert.Contains("Battery mass", edit);
-        Assert.DoesNotContain(">Weight kg", edit);
-        Assert.Contains("\"general.batteryMass\"", policy);
-        Assert.Contains("aspects.generalProductInformation.payload.batteryMass", policy);
-        Assert.Contains("OperationalFieldKeys", delta);
-    }
-
-    [Fact]
-    public void ProductEditor_ShouldManagePerModelSoftwareVersions()
-    {
-        var models = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var updateService = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));
-
-        Assert.Contains("BatteryProductSoftwareVersion", models);
-        Assert.Contains("IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions", models);
-        Assert.Contains("HighestSoftwareVersion", models);
-        Assert.Contains("data-product-software-version-select", edit);
-        Assert.Contains("selectedVersion.SoftwareVersions", updateService);
-    }
-
     [Fact]
     public void TokenPages_ShouldShowGeneratedCredentialModal()
     {
@@ -118,21 +57,6 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("grid-template-columns: repeat(auto-fit", css);
     }
 
-    [Fact]
-    public void FullReset_ShouldClearAndReseedCanonicalDemoState()
-    {
-        var productTemplateService = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-
-        Assert.Contains("ClearDemoCollectionsAsync", productTemplateService);
-        Assert.Contains("\"users\"", productTemplateService);
-        Assert.Contains("\"clusters\"", productTemplateService);
-        Assert.Contains("\"clusterMemberships\"", productTemplateService);
-        Assert.Contains("\"externalApiTokens\"", productTemplateService);
-        Assert.Contains("\"editableFieldPolicies\"", productTemplateService);
-        Assert.Contains("\"batteryTelemetry\"", productTemplateService);
-        Assert.DoesNotContain("string.Empty, \"CP7M-DEMO-001\"", productTemplateService);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs b/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
--- a/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
+++ b/BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs
@@ -5,10 +5,8 @@ public sealed class AntiForgeryProtectionTests
     [Fact]
     public void CookieAuthenticatedJsonMutationApis_ShouldRequireAntiForgeryTokens()
     {
-        var filesApi = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
         var authApi = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));
 
-        AssertMethodHasAttribute(filesApi, "Upload", "ValidateAntiForgeryToken");
         AssertMethodHasAttribute(authApi, "Login", "ValidateAntiForgeryToken");
         AssertMethodHasAttribute(authApi, "Logout", "ValidateAntiForgeryToken");
     }
@@ -23,16 +21,6 @@ public sealed class AntiForgeryProtectionTests
         Assert.Contains("ValidateTokenAsync", externalApi);
     }
 
-    [Fact]
-    public void ExistingFileUploadJavascript_ShouldSendAntiForgeryHeader()
-    {
-        var editPassport = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("@Html.AntiForgeryToken()", editPassport);
-        Assert.Contains("fetch('/api/files'", editPassport);
-        Assert.Contains("'RequestVerificationToken': token", editPassport);
-    }
-
     private static void AssertMethodHasAttribute(string source, string methodName, string attribute)
     {
         var methodStart = source.IndexOf($"Task<IActionResult> {methodName}(", StringComparison.Ordinal);
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -71,24 +71,13 @@ public sealed class ApplicationAuditCoverageTests
     public void AuditPages_ShouldUseAdminShellAndSharedCompactRows()
     {
         var globalAuditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));
-        var passportAuditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
-        var partialPath = RepoFileOrEmpty("web", "Views", "Shared", "_AuditEventRow.cshtml");
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
         Assert.DoesNotContain("Back to batteries", globalAuditView);
         Assert.Contains("<h1 class=\"mb-1\">Administration</h1>", globalAuditView);
         Assert.Contains("_AdminTabs", globalAuditView);
-        Assert.True(File.Exists(partialPath), "_AuditEventRow.cshtml should render the shared compact audit row.");
-
-        var partial = File.ReadAllText(partialPath);
-        Assert.Contains("bp-audit-event-row", partial);
-        Assert.Contains("bp-audit-event-main", partial);
-        Assert.Contains("bp-audit-event-facts", partial);
-        Assert.Contains("bp-audit-event-chip", partial);
         Assert.Contains("_AuditLine", globalAuditView);
-        Assert.Contains("_AuditEventRow", passportAuditView);
         Assert.DoesNotContain("bp-ledger-event ", globalAuditView);
-        Assert.DoesNotContain("bp-ledger-event ", passportAuditView);
         Assert.Contains(".bp-audit-event-row", css);
         Assert.Contains("grid-template-columns: minmax(128px, 150px) minmax(300px, 0.46fr) minmax(360px, 0.54fr)", css);
         Assert.Contains("border: 1px solid #c5d4e6", css);
@@ -113,27 +102,6 @@ public sealed class ApplicationAuditCoverageTests
         Assert.Contains(".bp-audit-filter-grid {\n    grid-template-columns: 1fr;", css);
     }
 
-    [Fact]
-    public void PassportAuditPages_ShouldUseExpandedBatteryTimeline()
-    {
-        var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var auditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
-
-        Assert.Contains("ListPassportTimelineEventsAsync", clusterAdmin);
-        Assert.Contains("_AuditEventRow", auditView);
-    }
-
-    [Fact]
-    public void BatteryAndPassportWritePaths_ShouldAppendMeaningfulAuditEvents()
-    {
-        var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var snapshot = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportSnapshotService.cs"));
-
-        Assert.Contains("\"battery.updated\"", clusterAdmin);
-        Assert.Contains("\"passport.created\"", snapshot);
-        Assert.Contains("\"battery.passport.created\"", snapshot);
-    }
-
     [Fact]
     public void AdminSecurityAndPolicyActions_ShouldAppendApplicationAuditEvents()
     {
diff --git a/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs b/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
--- a/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
+++ b/BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs
@@ -2,38 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class BatteryAdminWorkflowTests
 {
-    [Fact]
-    public void BatteryCreatePage_ShouldPreviewGeneratedBatteryId()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("Battery ID preview", view);
-        Assert.Contains("data-battery-id-preview", view);
-        Assert.Contains("/admin/batteries/id-preview", view);
-    }
-
-    [Fact]
-    public void AdminClustersView_ShouldManageBatteriesWithEmbeddedPassportHistory()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-
-        Assert.Contains("Battery ID", view);
-        Assert.Contains("Battery Model", view);
-        Assert.Contains("row.HistoryUrl", view);
-        Assert.Contains("Create passport", view);
-        Assert.DoesNotContain("data-battery-passport-history", view);
-        Assert.DoesNotContain("Battery version", view);
-    }
-
-    [Fact]
-    public void AdminBatteryEdit_ShouldEditBatteryAndReturnToBatteryListWithPendingSnapshot()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("battery-edit", view);
-        Assert.Contains("readonly", view);
-        Assert.Contains("New passport", view);
-    }
 
     [Fact]
     public void AdminBatteryCreate_ShouldRequireClusterAndSnapshotShouldClearPendingFlag()
@@ -43,83 +11,6 @@ public sealed class BatteryAdminWorkflowTests
         Assert.DoesNotContain("snapshot[\"newPassportRequired\"] = true;", controller);
     }
 
-    [Fact]
-    public void AdminBatteryList_ShouldUseBatteriesTabAsCanonicalDestination()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
-        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-
-        Assert.Contains("\"passports\" => \"batteries\"", controller);
-        Assert.DoesNotContain("href=\"/admin/clusters?tab=passports\"", help);
-        Assert.Contains("href=\"/admin/batteries\"", help);
-        Assert.Contains("Back to battery list", conformance);
-    }
-
-    [Fact]
-    public void BatteryCreate_ShouldDefaultModelToLatestFamilyModel()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("data-latest-product-version", view);
-        Assert.Contains("selectLatestProductVersion", view);
-    }
-
-    [Fact]
-    public void BatteryCreatePage_ShouldNotAskForPassportId()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("data-field-key=\"general.batteryIdPreview\"", view);
-        Assert.Contains("if (!isNewBattery)", view);
-        Assert.DoesNotContain("@(isBatteryEdit ? \"Battery ID\" : \"Passport ID\")", view);
-        Assert.DoesNotContain("else if (isNew) { <input type=\"text\" name=\"passportId\" value=\"@passport.PassportId\" required /> }", view);
-    }
-
-    [Fact]
-    public void AdminBatteryActions_ShouldUseUnifiedIconButtons()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-action-button", css);
-        Assert.Contains("bp-action-icon", css);
-        Assert.Contains("data-action-icon=\"edit\"", clusters);
-        Assert.Contains("data-action-icon=\"conformance\"", clusters);
-        Assert.Contains("data-action-icon=\"create-passport\"", clusters);
-        Assert.DoesNotContain("&#9776;", clusters);
-        Assert.DoesNotContain("&#9998;", clusters);
-        Assert.DoesNotContain("&#10003;", clusters);
-    }
-
-    [Fact]
-    public void FollowupBatteryCreate_ShouldRequireClusterAndUniqueSerial()
-    {
-        var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
-        var view = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("GetBySerialNumberAsync", repository);
-        Assert.Contains("name=\"clusterId\" required", view);
-        Assert.Contains("is-policy-locked", view);
-        Assert.Contains("data-editable-policy-locked", view);
-    }
-
-    [Fact]
-    public void FollowupPassportCreation_ShouldBeSnapshotOnlyAndAvoidDraftNewPassportConflict()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var deltaService = File.ReadAllText(RepoFile("web", "Services", "BatteryPassportDeltaService.cs"));
-        var table = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-
-        Assert.Contains("CreatePassportSnapshotAsync", clusterAdminController);
-        Assert.Contains("Redirect($\"/cluster-admin/passports", clusterAdminController);
-        Assert.DoesNotContain("return Redirect($\"/admin/passports/{Uri.EscapeDataString(passportId)}/edit\")", controller);
-        Assert.Contains("NumericBsonEquals", deltaService);
-        Assert.Contains("row.DisplayStatus", table);
-        Assert.Contains("row.ShowNewPassportRequired = row.NewPassportRequired && !IsDraftStatus(row.LatestPassportStatus);", File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs")));
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -1,4 +1,3 @@
-using BatteryPassWeb.Models.Trust;
 using BatteryPassWeb.Services;
 using MongoDB.Bson;
 
@@ -6,19 +5,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class BatteryFamilyAccessApiRevisedTests
 {
-    [Fact]
-    public void PassportFactory_ShouldExposeRevisedIdentityAndPassportStatus()
-    {
-        var passport = MinimalPassport(registryStatus: "draft", trustState: TrustState.Signed);
-
-        var model = new PassportViewModelFactory().Create(passport);
-
-        Assert.Equal("Compact 7M", model.BatteryFamily);
-        Assert.Equal("2.0", model.BatteryVersion);
-        Assert.Equal("SN-001", model.BatterySerialNumber);
-        Assert.Equal("Signed", model.PassportStatus);
-        Assert.Equal("Original", model.BatteryStatus);
-    }
 
     [Fact]
     public void ProductTemplateSource_ShouldDocumentBatteryFamilyMapping()
@@ -39,17 +25,11 @@ public sealed class BatteryFamilyAccessApiRevisedTests
             RepoFile("web", "Views", "Registry", "Index.cshtml"),
             RepoFile("web", "Views", "Passport", "Summary.cshtml"),
             RepoFile("web", "Views", "Passport", "Detail.cshtml"),
-            RepoFile("web", "Views", "Admin", "EditPassport.cshtml"),
             RepoFile("web", "Views", "Admin", "Clusters.cshtml")
         };
 
         var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));
 
-        Assert.Contains("Battery Family", text);
-        Assert.Contains("Battery Model", text);
-        Assert.Contains("Battery serial number", text);
-        Assert.Contains("Passport status", text);
-        Assert.Contains("Battery status", text);
         Assert.DoesNotContain(">Name <", text);
         Assert.DoesNotContain(">Model Number<", text);
         Assert.DoesNotContain("Product/battery version", text);
@@ -57,22 +37,6 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.DoesNotContain("Product templates", text);
     }
 
-    [Fact]
-    public void Software_ShouldBeConfiguredPerBatteryFamilyModel()
-    {
-        var modelSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
-        var serviceSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-        var apiSource = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
-
-        Assert.Contains("string SoftwareVersion", modelSource);
-        Assert.Contains("string SoftwareReleaseDate", modelSource);
-        Assert.Contains("string SoftwareLatestUpdate", modelSource);
-        Assert.Contains("IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions", modelSource);
-        Assert.Contains("record BatteryProductSoftwareVersion", modelSource);
-        Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", serviceSource);
-        Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/software\")]", apiSource);
-    }
-
     [Fact]
     public void ExternalApi_ShouldUseTokensOnlyAndExposeBatteryVersionValidateSign()
     {
@@ -91,62 +55,12 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.DoesNotContain("EnsureBatterySecret", initializer);
     }
 
-    [Fact]
-    public void ValidateSignPublish_ShouldUseSharedAutoRepublishWorkflow()
-    {
-        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
-        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
-
-        Assert.Contains("ShouldAutoPublishAfterSign", workflow);
-        Assert.Contains("AutoPublished", workflow);
-        Assert.Contains("registryInfo.hasBeenPublished", repository);
-    }
-
-    [Fact]
-    public void AccessControl_ShouldDefineRevisedRolesAndTrustVisibility()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
-
-        Assert.Contains("Normal User", source);
-        Assert.Contains("Notified Body", source);
-        Assert.Contains("Market Surveillance Authorities", source);
-        Assert.Contains("Commission", source);
-        Assert.Contains("Legitimate interest: repair, repurposing, remanufacturing", source);
-        Assert.Contains("CanViewTrustConformanceAsync", source);
-        Assert.Contains("marketSurveillanceAuthority", source);
-        Assert.Contains("commission", source);
-        Assert.DoesNotContain("notifiedBody\") ||", source);
-        Assert.DoesNotContain("legitimateInterest\") ||", source);
-    }
-
-    [Fact]
-    public void RegistryAndAdminViews_ShouldUseIconActionsAndRecoverableArchive()
-    {
-        var registry = File.ReadAllText(RepoFile("web", "Views", "Registry", "Index.cshtml"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var adminClusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
-
-        Assert.Contains("/detail", registry); // the Registry opens each battery's detail page (decision 30)
-        Assert.Contains("aria-label=\"Summary report\"", batteryTable);
-        Assert.Contains("title=\"Summary report\"", batteryTable);
-        Assert.Contains("aria-label=\"Detailed report\"", batteryTable);
-        Assert.Contains("title=\"Detailed report\"", batteryTable);
-        Assert.Contains("title=\"Edit battery\"", batteryTable);
-        Assert.Contains("title=\"Passport history\"", batteryTable);
-        Assert.Contains("title=\"Create passport\"", batteryTable);
-        Assert.Contains("title=\"Conformance\"", batteryTable);
-        Assert.Contains("bp-confirm-modal", adminClusters);
-        Assert.Contains("UnarchivePassportAsync", repository);
-    }
-
     [Fact]
     public void AccountAndClusterGuardrails_ShouldBePresent()
     {
         var accountController = RepoFile("web", "Controllers", "AccountController.cs");
         var accountView = RepoFile("web", "Views", "Account", "Index.cshtml");
         var clusterRepository = File.ReadAllText(RepoFile("web", "Services", "ClusterRepository.cs"));
-        var passportRepository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
         var authService = File.ReadAllText(RepoFile("web", "Services", "AuthService.cs"));
         var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
@@ -154,7 +68,6 @@ public sealed class BatteryFamilyAccessApiRevisedTests
 
         Assert.True(File.Exists(accountController));
         Assert.True(File.Exists(accountView));
-        Assert.Contains("CountPassportsByClusterAsync", passportRepository);
         Assert.Contains("CountMembershipsByClusterAsync", clusterRepository);
         Assert.Contains("ForceDeleteClusterAsync", adminController);
         Assert.Contains("I understand this deletes linked cluster data", adminController);
@@ -186,65 +99,6 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.Contains("/cluster-admin/api-tokens/delete", view);
     }
 
-    private static BsonDocument MinimalPassport(string registryStatus, string trustState)
-    {
-        return new BsonDocument
-        {
-            ["passportId"] = "did:web:acme.battery.pass:test-001",
-            ["registryInfo"] = new BsonDocument
-            {
-                ["status"] = registryStatus,
-                ["updatedAt"] = "2026-05-11T00:00:00Z"
-            },
-            ["trust"] = new BsonDocument
-            {
-                ["state"] = trustState,
-                ["isDirty"] = false
-            },
-            ["validation"] = new BsonDocument
-            {
-                ["isValid"] = trustState.Equals(TrustState.Signed, StringComparison.OrdinalIgnoreCase),
-                ["hash"] = "hash-1"
-            },
-            ["app"] = new BsonDocument
-            {
-                ["display"] = new BsonDocument
-                {
-                    ["name"] = "Old display name",
-                    ["modelNumber"] = "Old model",
-                    ["serialNumber"] = "SN-001",
-                    ["manufacturerName"] = "Scania Industrial Batteries",
-                    ["facilityId"] = "LINE-1"
-                },
-                ["product"] = new BsonDocument
-                {
-                    ["productId"] = "compact-7m",
-                    ["productName"] = "Compact 7M",
-                    ["productVersion"] = "2.0",
-                    ["softwareVersion"] = "4.0",
-                    ["softwareReleaseDate"] = "2026-05-01",
-                    ["softwareLatestUpdate"] = "2026-05-08"
-                },
-                ["media"] = new BsonDocument(),
-                ["documents"] = new BsonDocument(),
-                ["operations"] = new BsonDocument()
-            },
-            ["aspects"] = new BsonDocument
-            {
-                ["generalProductInformation"] = new BsonDocument
-                {
-                    ["payload"] = new BsonDocument
-                    {
-                        ["batteryStatus"] = "Original",
-                        ["batteryCategory"] = "industrial",
-                        ["batteryMass"] = 320,
-                        ["manufacturingDate"] = "2026-05-01T00:00:00Z"
-                    }
-                }
-            }
-        };
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
--- a/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
+++ b/BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs
@@ -6,45 +6,6 @@ public sealed class BatteryRouteAndRegistryTests
 {
     private static readonly string TestSourceFile = CurrentSourceFile();
 
-    [Fact]
-    public void Registry_ShouldUseBatteryRowsAndPassportCounts()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
-        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-
-        Assert.Contains("batteries.ListAsync", controller); // the new model's batteries, cut per reader (decision 30)
-        Assert.Contains("BatteryTablePageViewModel", File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs")));
-        Assert.Contains("PassportCount", view);
-        Assert.Contains("passports", view);
-        Assert.Contains("@row.BatteryId", view);
-        Assert.Contains("@row.BatteryModel", view);
-        Assert.DoesNotContain("@row.PassportId</span></td>", view);
-    }
-
-    [Fact]
-    public void RegistrySearch_ShouldSupportPassportBatterySerialAndAllClusterReadSearch()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Services", "BatteryTableService.cs"));
-        var repository = File.ReadAllText(RepoFile("web", "Services", "BatteryRepository.cs"));
-        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-
-        Assert.Contains("ResolveSearchAsync", controller);
-        Assert.Contains("SearchByClusterAsync", repository);
-        Assert.Contains("HasAllClusterReadScope", controller);
-        Assert.Contains("Cluster search is not available for this role.", controller);
-        Assert.Contains("AccessMessage", view);
-    }
-
-    [Fact]
-    public void AccessControl_ShouldAllowClusterMembersToSeeSignedOrPublishedRegistryPassports()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
-
-        Assert.Contains("registryStatus.Equals(\"published\"", source);
-        Assert.Contains("registryStatus.Equals(\"signed\"", source);
-        Assert.Contains("passportPublishPolicyService.HasCurrentValidSignature(passport)", source);
-    }
-
     [Fact]
     public void PassportReports_ShouldShowBatteryIdPassportIdAndHistoricalLatestState()
     {
@@ -84,23 +45,6 @@ public sealed class BatteryRouteAndRegistryTests
         Assert.Contains("/login?next=", hero);
     }
 
-    [Fact]
-    public void AdminBatteryList_ShouldUseCompactBatteryRowsWithoutEmbeddedHistory()
-    {
-        var view = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "BatteryViewModels.cs"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("@row.PassportCount", view);
-        Assert.Contains("@row.PassportCount passports", view);
-        Assert.DoesNotContain("bp-passport-history-row", view);
-        Assert.DoesNotContain("data-battery-passport-history", view);
-        Assert.Contains("row.HistoryUrl", view);
-        Assert.Contains("NewPassportRequired", model);
-        Assert.Contains(".bp-admin-battery-table .bp-battery-id-cell", css);
-        Assert.Contains("max-width: none", css);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var roots = new[]
diff --git a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
--- a/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ConformanceLayoutTests.cs
@@ -11,269 +11,6 @@ public sealed class ConformanceLayoutTests
         Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
     }
 
-    [Fact]
-    public void Program_ShouldRegisterRequiredDataCompletionService()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<DemoRequiredDataCompletionService>", source);
-    }
-
-    [Fact]
-    public void Program_ShouldRegisterPassportEvidenceService()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<PassportEvidenceService>", source);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldRenderTrustSummaryAndActions()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("Conformance", markup);
-        Assert.Contains("Blocking errors", markup);
-        Assert.Contains("Warnings", markup);
-        Assert.Contains("Validate passport", markup);
-        Assert.Contains("Back to battery list", markup);
-        Assert.Contains("/admin/clusters?tab=batteries", markup);
-        Assert.Contains("bp-conformance-return-row", markup);
-        Assert.Contains("bp-conformance-return-link", markup);
-        Assert.True(
-            markup.IndexOf("bp-conformance-return-row", StringComparison.Ordinal) <
-            markup.IndexOf("bp-conformance-step-workflow", StringComparison.Ordinal),
-            "Back navigation should sit outside and above the conformance workflow.");
-        Assert.Contains(".bp-conformance-return-row", css);
-        Assert.Contains(".bp-conformance-return-link", css);
-        Assert.Contains("Latest validation", markup);
-        Assert.Contains("What is stopping signing?", markup);
-        Assert.Contains("Full-data requirements", markup);
-        Assert.Contains("Completion checklist", markup);
-        Assert.Contains("Required data by section", markup);
-        Assert.Contains("Missing required data", markup);
-        Assert.Contains("Invalid format", markup);
-        Assert.Contains("Invalid value", markup);
-        Assert.Contains("Business-rule data", markup);
-        Assert.Contains("Product template", markup);
-        Assert.Contains("bp-conformance-shell", markup);
-        Assert.Contains("bp-issue-list", markup);
-        Assert.Contains("bp-completion-checklist", markup);
-        Assert.Contains("bp-completion-section", markup);
-        Assert.Contains("showValidatePassport", markup);
-        Assert.Contains("bp-action-control", markup);
-        Assert.DoesNotContain("showCompleteRequiredData", markup);
-        Assert.Contains("showSignPassport", markup);
-        Assert.Contains("showPublishPassport", markup);
-    }
-
-    [Fact]
-    public void ConformanceWorkflow_ShouldHideUnavailableActionsInsteadOfRenderingBlockedButtons()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-
-        Assert.DoesNotContain("Action blocked", markup);
-        Assert.DoesNotContain("bp-action-block-message", markup);
-        Assert.DoesNotContain("bp-button-blocked", markup);
-        Assert.DoesNotContain("disabled=\"", markup);
-        Assert.DoesNotContain("disabled=@", markup);
-        Assert.DoesNotContain("Sign passport is disabled", markup);
-        Assert.DoesNotContain("@if (showCompleteRequiredData)", markup);
-        Assert.DoesNotContain("Complete required demo data", markup);
-        Assert.Contains("@if (showSignPassport)", markup);
-        Assert.Contains("@if (showPublishPassport)", markup);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldUseGuidedWorkflowAndCleanEvidenceLayout()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-conformance-step-workflow", markup);
-        Assert.Contains("bp-workflow-panel", markup);
-        Assert.Contains("bp-trust-step", markup);
-        Assert.Contains("Validate passport data", markup);
-        Assert.Contains("Sign clean snapshot", markup);
-        Assert.Contains("Publish for registry and public access", markup);
-        Assert.Contains("bp-evidence-grid", markup);
-        Assert.Contains("bp-validation-drawer", markup);
-
-        Assert.Contains(".bp-conformance-step-workflow", css);
-        Assert.Contains(".bp-workflow-panel", css);
-        Assert.Contains(".bp-trust-step", css);
-        Assert.Contains(".bp-evidence-grid", css);
-        Assert.Contains(".bp-validation-drawer", css);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldUseStepBasedTrustWorkflow()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-conformance-step-workflow", markup);
-        Assert.Contains("bp-trust-step", markup);
-        Assert.Contains("Validate passport data", markup);
-        Assert.Contains("Sign clean snapshot", markup);
-        Assert.Contains("Publish for registry and public access", markup);
-        Assert.Contains("bp-trust-step-content", markup);
-        Assert.Contains("bp-trust-step-status", markup);
-        Assert.Contains(".bp-conformance-step-workflow", css);
-        Assert.Contains(".bp-trust-step", css);
-        Assert.DoesNotContain("bp-readiness-hero", markup);
-        Assert.DoesNotContain("bp-conformance-main-grid", markup);
-    }
-
-    [Fact]
-    public void ConformanceWorkflowSteps_ShouldCollapseUnlessActionOrIssueNeedsAttention()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("var validateStepOpen", markup);
-        Assert.Contains("PassportReadinessAction.Validate", markup);
-        Assert.Contains("PassportReadinessAction.CompleteData", markup);
-        Assert.Contains("var signStepOpen", markup);
-        Assert.Contains("showSignPassport", markup);
-        Assert.Contains("PassportReadinessAction.ReviewDiagnostics", markup);
-        Assert.Contains("var publishStepOpen", markup);
-        Assert.Contains("showPublishPassport", markup);
-        Assert.Contains("var evidenceStepOpen", markup);
-        Assert.Contains("evidencePack.BlockingCount > 0", markup);
-        Assert.Contains("evidencePack.UploadedUnsignedCount > 0", markup);
-        Assert.Contains("evidencePack.ChangedSinceSigningCount > 0", markup);
-
-        Assert.Contains("<details class=\"bp-trust-step @validateStepClass\" open=\"@validateStepOpen\">", markup);
-        Assert.Contains("<details class=\"bp-trust-step @signStepClass\" open=\"@signStepOpen\">", markup);
-        Assert.Contains("<details class=\"bp-trust-step @publishStepClass\" open=\"@publishStepOpen\">", markup);
-        Assert.Contains("<summary class=\"bp-trust-step-header\">", markup);
-        Assert.Contains("<details class=\"bp-card bp-conformance-panel bp-evidence-panel bp-evidence-step\" open=\"@evidenceStepOpen\">", markup);
-        Assert.Contains("<summary class=\"bp-evidence-step-header\">", markup);
-
-        Assert.Contains(".bp-trust-step > summary", css);
-        Assert.Contains(".bp-trust-step[open] > .bp-trust-step-header", css);
-        Assert.Contains(".bp-evidence-step > summary", css);
-        Assert.Contains(".bp-evidence-step[open] > .bp-evidence-step-header", css);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldOnlyRenderValidateActionWhenValidationIsNeeded()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var readiness = File.ReadAllText(RepoFile("web", "Services", "PassportReadinessService.cs"));
-        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
-
-        Assert.Contains("var showValidatePassport = readiness.CanValidate;", markup);
-        Assert.Contains("@if (showValidatePassport)", markup);
-        Assert.True(
-            markup.IndexOf("@if (showValidatePassport)", StringComparison.Ordinal) <
-            markup.IndexOf("Validate passport</button>", StringComparison.Ordinal),
-            "Validate action should be guarded by readiness.CanValidate.");
-
-        Assert.Contains("CanValidate = canValidate", readiness);
-        Assert.Contains("Validate passport", readiness);
-        Assert.Contains("Passport validation is already current", workflow);
-        Assert.Contains("verification.IsValid", workflow);
-        Assert.Contains("!GetBoolean(document, \"trust\", \"isDirty\")", workflow);
-    }
-
-    [Fact]
-    public void PassportViewModel_ShouldExposeTrustFields()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
-
-        Assert.Contains("TrustState", source);
-        Assert.Contains("TrustIsDirty", source);
-        Assert.Contains("TrustLastValidatedAt", source);
-        Assert.Contains("TrustBlockingErrorCount", source);
-        Assert.Contains("TrustWarningCount", source);
-    }
-
-    [Fact]
-    public void AdminPages_ShouldLinkToConformance()
-    {
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var batteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-
-        Assert.Contains("/conformance", edit);
-        Assert.Contains("ConformanceUrl", batteryTable);
-    }
-
-    [Fact]
-    public void ConformanceModel_ShouldExposeGuidedReadinessData()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ConformanceViewModel.cs"));
-
-        Assert.Contains("PassportReadinessDecision", source);
-        Assert.Contains("GroupedBlockingIssues", source);
-        Assert.Contains("GroupedWarningIssues", source);
-        Assert.Contains("ConformanceIssueGroupViewModel", source);
-    }
-
-    [Fact]
-    public void ConformanceModel_ShouldExposeEvidencePack()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ConformanceViewModel.cs"));
-
-        Assert.Contains("EvidencePackResult", source);
-        Assert.Contains("EvidencePack", source);
-    }
-
-    [Fact]
-    public void AdminController_ShouldComputePassportReadinessForConformance()
-    {
-        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<PassportReadinessService>", program);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldRenderOneGuidedNextActionAndCollapsibleDiagnostics()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-conformance-step-workflow", markup);
-        Assert.Contains("bp-trust-step", markup);
-        Assert.Contains("Validate passport data", markup);
-        Assert.Contains("Sign clean snapshot", markup);
-        Assert.Contains("Publish for registry and public access", markup);
-        Assert.Contains("bp-blocker-groups", markup);
-        Assert.Contains("bp-advanced-diagnostics", markup);
-        Assert.Contains("<details", markup);
-        Assert.DoesNotContain("Action blocked", markup);
-        Assert.DoesNotContain("disabled=\"", markup);
-
-        Assert.Contains(".bp-conformance-step-workflow", css);
-        Assert.Contains(".bp-trust-step", css);
-        Assert.Contains(".bp-blocker-groups", css);
-        Assert.Contains(".bp-advanced-diagnostics", css);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldRenderEvidenceReadinessPanel()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("Evidence readiness", markup);
-        Assert.Contains("Model.EvidencePack", markup);
-        Assert.Contains("Missing required", markup);
-        Assert.Contains("Changed since signing", markup);
-        Assert.Contains("Uploaded unsigned", markup);
-        Assert.Contains("Verified documents", markup);
-        Assert.Contains("bp-evidence-panel", markup);
-        Assert.Contains("bp-evidence-item", markup);
-        Assert.Contains("bp-evidence-status", markup);
-
-        Assert.Contains(".bp-evidence-panel", css);
-        Assert.Contains(".bp-evidence-list", css);
-        Assert.Contains(".bp-evidence-item", css);
-        Assert.Contains(".bp-evidence-status", css);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
--- a/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
+++ b/BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs
@@ -40,18 +40,9 @@ public sealed class CopyIdButtonLayoutTests
     [Fact]
     public void AdminAndRegistryIdSurfaces_ShouldRenderInlineCopyButtons()
     {
-        var sharedBatteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
-        Assert.Contains("_CopyIdButton", sharedBatteryTable);
-        Assert.Contains("bp-id-copy-row", sharedBatteryTable);
-
         var files = new[]
         {
             RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
-            RepoFile("web", "Views", "Admin", "Conformance.cshtml"),
-            RepoFile("web", "Views", "Admin", "Audit.cshtml"),
-            RepoFile("web", "Views", "Admin", "Revisions.cshtml"),
-            RepoFile("web", "Views", "Admin", "EditPassport.cshtml"),
-            RepoFile("web", "Views", "ClusterAdmin", "BatteryPassports.cshtml"),
             RepoFile("web", "Views", "Help", "Index.cshtml")
         };
 
diff --git a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
--- a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
+++ b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
@@ -2,13 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class DataRequirementsAdminLayoutTests
 {
-    [Fact]
-    public void Program_ShouldRegisterDataCompletionPolicyService()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<DataCompletionPolicyService>", source);
-    }
 
     [Fact]
     public void AdminController_ShouldUseProductScopedRequirementsInsteadOfGlobalAdminSave()
@@ -19,24 +12,6 @@ public sealed class DataRequirementsAdminLayoutTests
         Assert.DoesNotContain("SaveDataRequirements", source);
     }
 
-    [Fact]
-    public void AdminWorkspace_ShouldNotRenderGlobalDataRequirementsTab()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var tabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
-        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs"));
-        var adminClusterModel = model[..model.IndexOf("public sealed class ProductTemplateSummaryViewModel", StringComparison.Ordinal)];
-
-        Assert.Contains("href=\"/admin/families\"", tabs);
-        Assert.DoesNotContain("tab=data-requirements", markup);
-        Assert.DoesNotContain("tab=data-requirements", tabs);
-        Assert.DoesNotContain("selectedTab == \"data-requirements\"", markup);
-        Assert.DoesNotContain("Save data requirements", markup);
-        Assert.DoesNotContain("Model.DataRequirements", markup);
-
-        Assert.DoesNotContain("DataRequirements", adminClusterModel);
-    }
-
     [Fact]
     public void ProductTemplateEditor_ShouldRemainRequirementSourceOfTruth()
     {
diff --git a/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs b/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs
--- a/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/AppRolesTests.cs
@@ -72,10 +72,8 @@ public sealed class AppRolesTests
         var old = User("legitimateInterest");
 
         Assert.False(AccessControlService.HasLegitimateInterestRole(old));
-        Assert.False(AccessControlService.HasGlobalReportReadRole(old));
-        Assert.False(AccessControlService.CanSeeSignedPassports(old));
         Assert.Equal("Cluster Member", AccessControlService.DisplayRoleLabel(old));
-        Assert.All(LiRoles, role => Assert.True(AccessControlService.HasGlobalReportReadRole(User(role))));
+        Assert.All(LiRoles, role => Assert.True(AccessControlService.HasLegitimateInterestRole(User(role))));
     }
 
     private static ClaimsPrincipal User(string role) =>
diff --git a/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs b/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
--- a/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
+++ b/BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs
@@ -2,18 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class ErrorHandlingRolloutTests
 {
-    [Fact]
-    public void TrustChangingAdminActions_ShouldShowServiceErrorInsteadOfClaimingSuccess()
-    {
-        var repository = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
-        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
-
-        Assert.Contains("if (!trustUpdated)", workflow);
-        Assert.Contains("if (!published)", workflow);
-        Assert.Contains("Task<bool> UpdateTrustSignatureAsync", repository);
-        Assert.Contains("Task<bool> PublishPassportAsync", repository);
-        Assert.Contains("result.MatchedCount > 0", repository);
-    }
 
     [Fact]
     public void RolloutGuide_ShouldDocumentFullTrustQrAndFailureChecklist()
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs
@@ -15,21 +15,6 @@ public sealed class ExternalApiBatteryIdTests
         Assert.DoesNotContain("/battery-version", help);
     }
 
-    [Fact]
-    public void ExternalApi_ShouldListBatteryPassportsAndUsePassportLifecycleLabel()
-    {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
-        var help = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
-        var seed = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-
-        Assert.Contains("[HttpGet(\"batteries/{id}/passports\")]", controller);
-        Assert.Contains("api.VersionsAsync", controller);
-        Assert.Contains("Passport Lifecycle", help);
-        Assert.DoesNotContain("create, validate, sign and publish passport", help);
-        Assert.Contains("Sample token (Passport Lifecycle)", seed);
-        Assert.DoesNotContain("Sample token (create, validate, sign and publish passport)", seed);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
@@ -3,29 +3,6 @@ namespace BatteryPassWeb.Tests;
 public sealed class ExternalApiBatteryVersionTests
 {
 
-    [Fact]
-    public void BatteryTemplateUpdateService_ShouldCopySoftwareReleaseMetadata()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));
-
-        Assert.Contains("[\"app.product.softwareReleaseDate\"]", source);
-        Assert.Contains("[\"app.product.softwareLatestUpdate\"]", source);
-        Assert.Contains("Unknown Software Version", source);
-    }
-
-    [Fact]
-    public void ProductTemplateService_ShouldChangeSinglePassportByBatteryVersion()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-
-        Assert.Contains("ChangePassportBatteryVersionAsync", source);
-        Assert.Contains("requestedBatteryVersion", source);
-        Assert.Contains("BuildSafeTemplatePushUpdate(", source);
-        Assert.Contains("selectedVersion", source);
-        Assert.Contains("app.product.productVersion", source);
-        Assert.DoesNotContain("PushTemplateAsync", source);
-    }
-
     [Fact]
     public void ExternalApiHelp_ShouldNotDocumentRemovedSoftwarePatch()
     {
@@ -35,16 +12,6 @@ public sealed class ExternalApiBatteryVersionTests
         Assert.DoesNotContain("software parameters", markup);
     }
 
-    [Fact]
-    public void PassportViewModelFactory_ShouldExposeSoftwareVersionAsBatteryVersionParameter()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));
-
-        Assert.Contains("softwareVersion", source);
-        Assert.Contains("SoftwareReleaseDate", source);
-        Assert.Contains("SoftwareLatestUpdate", source);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs b/BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs
@@ -56,10 +56,7 @@ public sealed class ExternalApiHashOnlyTokenTests
         Assert.Contains("TempData[\"GeneratedCredential\"] = newToken", admin);
         Assert.Contains("TempData[\"GeneratedCredential\"] = tokenValue", clusterAdmin);
         Assert.Contains("GeneratedCredential", clusterAdmin);
-        Assert.Contains("SampleReadToken = _options.EnableDemoData", help);
-        Assert.Contains("ExternalApiInitializer.SampleReadTokenValue", help);
-        Assert.Contains("ExternalApiInitializer.SampleReadWriteTokenValue", help);
-        Assert.Contains("ExternalApiInitializer.SampleLifecycleTokenValue", help);
+        Assert.DoesNotContain("SampleReadToken", help);
 
         Assert.DoesNotContain("TryRevealToken", admin);
         Assert.DoesNotContain("TryRevealToken", help);
diff --git a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
--- a/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
+++ b/BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs
@@ -73,8 +73,8 @@ public sealed class HelpWorkbenchLayoutTests
         Assert.Contains("const getWorkbenchContext", markup);
         Assert.Contains("resolveTemplatePath(template.pathTemplate", markup);
         Assert.Contains("batteryIdEl?.addEventListener", markup);
-        Assert.Contains("tokenPresetEl?.addEventListener('change'", markup);
-        Assert.DoesNotContain("tokenPresetEl.value = template.tokenPreset;", markup);
+        Assert.DoesNotContain("tester-token-preset", markup);
+        Assert.Contains("Paste a token from API Token Management.", markup);
     }
 
     [Fact]
@@ -120,31 +120,6 @@ public sealed class HelpWorkbenchLayoutTests
         Assert.Contains("window.location.origin", markup);
     }
 
-    [Fact]
-    public void HelpWorkbench_ShouldOnlyExposePrivilegedDemoTokensToApprovedTesters()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
-        var helpModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ExternalApiHelpViewModel.cs"));
-
-        Assert.Contains("CanUsePrivilegedDemoTokens", helpModel);
-        Assert.Contains("DemoWriteSignTestingEnabled", helpModel);
-        Assert.Contains("PrivilegedDemoTokenMessage", helpModel);
-        Assert.Contains("AccessControlService", controller);
-        Assert.Contains("CanUsePrivilegedDemoTokensAsync", controller);
-        Assert.Contains("CanAdministerClusterAsync(User, ExternalApiInitializer.SampleApiClusterId", controller);
-        Assert.Contains("EnablePublicDemoReadToken", controller);
-        Assert.Contains("EnableDemoWriteSignTesting", controller);
-        Assert.DoesNotContain("SampleReadWriteToken = readWriteTokenDocument != null", controller);
-        Assert.DoesNotContain("SampleLifecycleToken = lifecycleTokenDocument != null", controller);
-
-        Assert.Contains("@if (Model.CanUsePrivilegedDemoTokens)", markup);
-        Assert.Contains("approved tester", markup, StringComparison.OrdinalIgnoreCase);
-        Assert.Contains("const canUsePrivilegedDemoTokens", markup);
-        Assert.Contains("sample-read-write': canUsePrivilegedDemoTokens ? sampleReadWriteToken : ''", markup);
-        Assert.Contains("sample-lifecycle': canUsePrivilegedDemoTokens ? sampleLifecycleToken : ''", markup);
-    }
-
     [Fact]
     public void HelpController_ShouldAllowAnonymousReadOnlyWorkbench()
     {
@@ -186,9 +161,6 @@ public sealed class HelpWorkbenchLayoutTests
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
         var controller = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
-        var initializer = File.ReadAllText(RepoFile("web", "Services", "ExternalApiInitializer.cs"));
-        var helpController = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));
-        var helpModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "ExternalApiHelpViewModel.cs"));
 
         var documentedRouteFragments = System.Text.RegularExpressions.Regex
             .Matches(controller, "\\[Http(?:Get|Post|Patch)\\(\"([^\"]+)\"\\)\\]")
@@ -223,11 +195,6 @@ public sealed class HelpWorkbenchLayoutTests
             Assert.Contains($"{templateKey}: {{", markup);
         }
 
-        Assert.Contains("SampleLifecycleTokenId", helpController);
-        Assert.Contains("SampleLifecycleToken", helpModel);
-        Assert.Contains("const sampleLifecycleToken", markup);
-        Assert.Contains("'sample-lifecycle': canUsePrivilegedDemoTokens ? sampleLifecycleToken : ''", markup);
-        Assert.Contains("SAMPLEBATTERYPASSPORTLIFECYC001", initializer);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -10,7 +10,6 @@ public sealed class MobileThemeSessionLayoutTests
     public void MobileCss_ShouldAddMobileOnlyNavigationTouchAndTableGuardrails()
     {
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-        var sharedBatteryTable = File.ReadAllText(RepoFile("web", "Views", "Shared", "_BatteryTable.cshtml"));
 
         Assert.Contains("@media (max-width: 767px)", css);
         Assert.Contains(".bp-header-nav-row::after", css);
@@ -22,9 +21,6 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains(".bp-id-copy-row", css);
         Assert.Contains("grid-template-columns: minmax(0, 1fr) 44px;", css);
 
-        Assert.Contains("bp-mobile-card-table", sharedBatteryTable);
-        Assert.Contains("data-label=\"Battery ID\"", sharedBatteryTable);
-        Assert.Contains("data-label=\"Actions\"", sharedBatteryTable);
     }
 
     [Fact]
@@ -60,7 +56,6 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.True(File.Exists(partialPath));
 
         var clusterTabs = File.ReadAllText(partialPath);
-        var passports = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Passports.cshtml"));
         var users = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "Users.cshtml"));
         var apiTokens = File.ReadAllText(RepoFile("web", "Views", "ClusterAdmin", "ApiTokens.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
@@ -70,16 +65,14 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains("bp-admin-mobile-tab-form", clusterTabs);
         Assert.Contains("Cluster admin section", clusterTabs);
         Assert.Contains("data-admin-mobile-tab-select", clusterTabs);
-        Assert.Contains("value=\"/cluster-admin/passports\"", clusterTabs);
+        Assert.DoesNotContain("/cluster-admin/passports", clusterTabs);
         Assert.Contains("value=\"/cluster-admin/users\"", clusterTabs);
         Assert.Contains("value=\"/cluster-admin/api-tokens\"", clusterTabs);
         Assert.Contains("aria-label=\"Local admin tabs\"", clusterTabs);
         Assert.Contains("bp-tab-row", clusterTabs);
-        Assert.Contains("Managed passports", clusterTabs);
         Assert.Contains("API token management", clusterTabs);
         Assert.True(mobileFormIndex >= 0);
         Assert.True(localNavIndex > mobileFormIndex);
-        Assert.Contains("@await Html.PartialAsync(\"_ClusterAdminTabs\", \"passports\")", passports);
         Assert.Contains("@await Html.PartialAsync(\"_ClusterAdminTabs\", \"users\")", users);
         Assert.Contains("@await Html.PartialAsync(\"_ClusterAdminTabs\", \"api-tokens\")", apiTokens);
         Assert.Contains(".bp-admin-page .bp-tab-row", css);
diff --git a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
--- a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
+++ b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
@@ -82,7 +82,6 @@ public sealed class Phase6ADocumentationTests
         Assert.Contains("CP13M-SOUTH-002", accounts);
         Assert.Contains("CORE-FLEET-002", accounts);
         Assert.Contains("Token Value", apiHelp);
-        Assert.Contains("Passport Lifecycle token", apiHelp);
         Assert.DoesNotContain("create, validate, sign and publish passport token", apiHelp);
         Assert.DoesNotContain("Battery Secret", apiHelp);
     }
diff --git a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
@@ -2,62 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class ProductTemplateWorkflowLayoutTests
 {
-    [Fact]
-    public void Program_ShouldRegisterProductTemplateServices()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<ProductTemplateService>", source);
-        Assert.DoesNotContain("DemoScenarioResetService", source);
-    }
-
-    [Fact]
-    public void AdminController_ShouldExposeProductTemplateRoutesAndUseProductPolicies()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-        var service = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
-        var dataCompletionSource = File.ReadAllText(RepoFile("web", "Services", "DataCompletionPolicyService.cs"));
-
-        Assert.Contains("batteryProductTemplateVersions", service);
-        Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", service);
-        Assert.Contains("productVersion", service);
-        Assert.DoesNotContain("PushTemplateAsync(string productId, string productVersion, string softwareVersion", service);
-        Assert.Contains("GetProductVersionPolicyAsync", dataCompletionSource);
-        Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
-    }
-
-    [Fact]
-    public void AdminPages_ShouldExposeProductsTabAndTemplateBasedNewPassportFlow()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var tabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
-
-        Assert.Contains("href=\"/admin/families\"", tabs);
-        Assert.Contains("_AdminTabs", clusters);
-        Assert.Contains("name=\"productId\"", edit);
-        Assert.Contains("name=\"softwareVersion\"", edit);
-        Assert.Contains("data-product-template-select", edit);
-        Assert.DoesNotContain("Complete required demo data", help);
-        Assert.Contains("Open battery families", help);
-    }
-
-    [Fact]
-    public void AdminProductTemplateUi_ShouldUseCalmerMinimalSurfaceStyling()
-    {
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-template-sync-note", edit);
-        Assert.DoesNotContain("Template sync", edit);
-        Assert.DoesNotContain("Overrides:", edit);
-        Assert.DoesNotContain("bp-template-sync-meta", edit);
-        Assert.Contains(".bp-summary-title", css);
-        Assert.DoesNotContain("background: #1d4ed8", css);
-        Assert.DoesNotContain("radial-gradient(circle at 92% 8%, rgba(28, 118, 103", css);
-        Assert.DoesNotContain("bp-template-sync-card", edit);
-    }
 
     [Fact]
     public void ProductEditor_ShouldExposeProductVersionsAndBaseTemplateControls()
@@ -78,56 +22,6 @@ public sealed class ProductTemplateWorkflowLayoutTests
         Assert.DoesNotContain("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
     }
 
-    [Fact]
-    public void AdminAndClusterAdmin_ShouldExposeEditableFieldPolicy()
-    {
-        var sharedEdit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("FieldEditableByKey", sharedEdit);
-        Assert.Contains("FieldVisibleByKey", sharedEdit);
-        Assert.Contains("AddSingleton<EditableFieldPolicyService>", program);
-    }
-
-    [Fact]
-    public void EditPassport_ShouldUpdateProductTemplateFieldsLiveFromCatalogData()
-    {
-        var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-        var editModelSource = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));
-
-        Assert.Contains("data-product-template-catalog", edit);
-        Assert.Contains("data-product-template-select", edit);
-        Assert.DoesNotContain("id=\"admin-software\"", edit);
-        Assert.Contains("name=\"productVersion\"", edit);
-        Assert.Contains("data-product-version-select", edit);
-        Assert.Contains("data-product-software-version", edit);
-        Assert.Contains("data-product-software-release", edit);
-        Assert.Contains("data-product-software-update", edit);
-        Assert.Contains("SelectedProductVersion", editModelSource);
-        Assert.Contains("data-required-passport-validation", edit);
-        Assert.Contains("data-field-key", edit);
-        Assert.Contains("has-missing-fields", edit);
-        Assert.Contains("markMissingRequiredFields", edit);
-        Assert.Contains("applyProductTemplateSelection", edit);
-        Assert.Contains("updateProductVersionOptions", edit);
-        Assert.DoesNotContain("updateSoftwareOptions(productVersion", edit);
-        Assert.DoesNotContain("updateSoftwareOptions", edit);
-        Assert.Contains("materialNickel", edit);
-        Assert.Contains("ratedEnergy", edit);
-        Assert.Contains("recycledNickelPre", edit);
-        Assert.Contains("carbonRawMaterial", edit);
-    }
-
-    [Fact]
-    public void ConformanceWorkflow_ShouldNotOfferDemoCompletion()
-    {
-        var conformance = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-
-        Assert.DoesNotContain("Complete required demo data", conformance);
-        Assert.DoesNotContain("showCompleteRequiredData", conformance);
-        Assert.Contains("Product template", conformance);
-    }
-
     [Fact]
     public void Documentation_ShouldExplainProductTemplateWorkflow()
     {
diff --git a/BatteryPassWeb.Tests/QrWorkflowTests.cs b/BatteryPassWeb.Tests/QrWorkflowTests.cs
--- a/BatteryPassWeb.Tests/QrWorkflowTests.cs
+++ b/BatteryPassWeb.Tests/QrWorkflowTests.cs
@@ -90,7 +90,6 @@ public sealed class QrWorkflowTests
     public void LandingPage_ShouldOpenQrScannerFromInlineSearchIcon()
     {
         var landing = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));
-        var search = File.ReadAllText(RepoFile("web", "Views", "Home", "Search.cshtml"));
         var home = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
@@ -121,8 +120,6 @@ public sealed class QrWorkflowTests
         Assert.Contains("ViewData[\"SearchNotFound\"]", home);
         Assert.Contains("return RedirectToAction(nameof(Index));", home);
         Assert.Contains("notFound = \"1\"", home);
-        Assert.DoesNotContain("data-qr-scanner", search);
-        Assert.DoesNotContain("data-qr-trigger", search);
         Assert.Contains(".bp-search-input-shell", css);
         Assert.Contains(".bp-qr-trigger", css);
         Assert.Contains(".bp-qr-trigger-icon", css);
diff --git a/BatteryPassWeb.Tests/SecurityLimitsTests.cs b/BatteryPassWeb.Tests/SecurityLimitsTests.cs
--- a/BatteryPassWeb.Tests/SecurityLimitsTests.cs
+++ b/BatteryPassWeb.Tests/SecurityLimitsTests.cs
@@ -50,27 +50,23 @@ public sealed class SecurityLimitsTests
         var loginController = File.ReadAllText(RepoFile("web", "Controllers", "LoginController.cs"));
         var authApiController = File.ReadAllText(RepoFile("web", "Controllers", "AuthApiController.cs"));
         var externalApiController = File.ReadAllText(RepoFile("web", "Controllers", "DppApiController.cs"));
-        var filesApiController = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
 
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", loginController);
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.Login)", authApiController);
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiRead)", externalApiController);
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiWrite)", externalApiController);
         Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.ExternalApiLifecycle)", externalApiController);
-        Assert.Contains("EnableRateLimiting(SecurityRateLimitPolicyNames.FileUpload)", filesApiController);
     }
 
     [Fact]
     public void FileUpload_ShouldCheckConfiguredFileSizeBeforeBuffering()
     {
-        var controller = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
+        // The old files API went with step 3d-1; uploads go through the documents service, behind the request size limit.
+        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
+        var documents = File.ReadAllText(RepoFile("web", "Dpp", "Documents", "DocumentService.cs"));
 
-        Assert.Contains("MaxUploadBytes", controller);
-        Assert.Contains("StatusCodes.Status413PayloadTooLarge", controller);
-        Assert.True(
-            controller.IndexOf("file.Length > _options.MaxUploadBytes", StringComparison.Ordinal)
-            < controller.IndexOf("new MemoryStream", StringComparison.Ordinal),
-            "Upload size should be checked before buffering the uploaded file.");
+        Assert.Contains("return options.MaxUploadBytes + 65_536;", program);
+        Assert.Contains("options.Value.MaxUploadBytes", documents);
     }
 
     private static string RepoFile(params string[] parts)
diff --git a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs
@@ -2,26 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class SigningWorkflowLayoutTests
 {
-    [Fact]
-    public void Program_ShouldRegisterSigningRevisionAndAuditServices()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<CanonicalPassportSnapshotService>", source);
-        Assert.Contains("AddSingleton<DemoSigningKeyService>", source);
-        Assert.Contains("AddSingleton<PassportTrustService>", source);
-        Assert.Contains("AddSingleton<AuditRevisionService>", source);
-    }
-
-    [Fact]
-    public void AdminController_ShouldExposeSignPublishAuditAndRevisionRoutes()
-    {
-        var workflow = File.ReadAllText(RepoFile("web", "Services", "PassportTrustWorkflowService.cs"));
-
-        Assert.Contains("CreateSignedRevisionAsync", workflow);
-        Assert.Contains("UpdateTrustSignatureAsync", workflow);
-        Assert.Contains("AppendAuditEventAsync", workflow);
-    }
 
     [Fact]
     public void PassportsApiController_ShouldExposeSignPublishAndPublicVerifyRoutes()
@@ -34,133 +14,6 @@ public sealed class SigningWorkflowLayoutTests
         Assert.Contains("PassportReader.Verify(version)", source);
     }
 
-    [Fact]
-    public void PassportRepository_ShouldPersistValidationSignatureAndPublishProofOnPassportDocument()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));
-
-        Assert.Contains(".Set(\"validation.isValid\"", source);
-        Assert.Contains(".Set(\"validation.status\"", source);
-        Assert.Contains(".Set(\"validation.validatedAt\"", source);
-        Assert.Contains(".Set(\"validation.validationSummary\"", source);
-        Assert.Contains(".Set(\"validation.blockingErrorCount\"", source);
-        Assert.Contains(".Set(\"validation.warningCount\"", source);
-        Assert.Contains(".Set(\"validation.passedCount\"", source);
-        Assert.Contains(".Set(\"validation.canSign\"", source);
-        Assert.Contains(".Set(\"validation.signedAt\"", source);
-        Assert.Contains(".Set(\"validation.hash\"", source);
-        Assert.Contains(".Set(\"validation.signature\"", source);
-        Assert.Contains(".Set(\"validation.proof\"", source);
-        Assert.Contains(".Set(\"validation.signedRevisionId\"", source);
-        Assert.Contains(".Set(\"validation.signatureProofCode\"", source);
-        Assert.Contains(".Set(\"trust.latestHash\"", source);
-        Assert.Contains(".Set(\"trust.latestProof\"", source);
-        Assert.Contains(".Set(\"trust.latestRevisionId\"", source);
-        Assert.Contains(".Set(\"registryInfo.publishedAt\"", source);
-        Assert.Contains(".Set(\"validation.publishedAt\"", source);
-        Assert.Contains(".Set(\"validation.publishedRevisionId\"", source);
-        Assert.Contains(".Set(\"validation.publishedHash\"", source);
-        Assert.Contains(".Set(\"validation.publishedProof\"", source);
-        Assert.Contains(".Set(\"validation.publishedSignatureProofCode\"", source);
-        Assert.Contains(".Set(\"trust.publishedRevisionId\"", source);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldRenderSigningActionsAndProofDiagnostics()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-
-        Assert.Contains("Sign passport", markup);
-        Assert.Contains("Publish passport", markup);
-        Assert.Contains("Verification diagnostics", markup);
-        Assert.Contains("Audit trail", markup);
-        Assert.Contains("Revision history", markup);
-        Assert.Contains("Model.VerificationResult", markup);
-    }
-
-    [Fact]
-    public void AdminEditPassport_ShouldLinkAuditAndRevisionLedgerPages()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.Contains("Audit trail", markup);
-        Assert.Contains("Revision history", markup);
-        Assert.Contains("/audit", markup);
-        Assert.Contains("/revisions", markup);
-    }
-
-    [Fact]
-    public void PassportViewModel_ShouldExposePublicProofMetadata()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
-
-        Assert.Contains("TrustLatestHash", source);
-        Assert.Contains("TrustLatestRevisionId", source);
-        Assert.Contains("TrustLastSignedAt", source);
-        Assert.Contains("TrustIssuer", source);
-        Assert.Contains("TrustVerificationMethod", source);
-        Assert.Contains("TrustProofStatus", source);
-        Assert.Contains("TrustVerificationMessage", source);
-    }
-
-    [Fact]
-    public void PassportSummaryShouldHideTrustDataAndDetailShouldRenderPrivilegedTrustTab()
-    {
-        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
-        var hero = File.ReadAllText(RepoFile("web", "Views", "Passport", "_PassportHero.cshtml"));
-        var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
-
-        Assert.DoesNotContain("Public verification", summary);
-        Assert.DoesNotContain("SignatureHolds", summary);
-        Assert.DoesNotContain("Conformance", summary);
-        Assert.Contains("@if (detail)", hero);
-        Assert.Contains("<div><dt>Signature</dt><dd>@(Model.SignatureHolds ? \"Verified\" : \"Doesn't match the signed passport\")</dd></div>", hero);
-        Assert.Contains("CanViewTrustConformanceAsync", access);
-        Assert.Contains("if (IsAdmin(user))", access);
-        Assert.Contains("GetAdministeredClusterIdsForUserAsync", access);
-        Assert.Contains("managedClusterIds.Contains", access);
-    }
-
-    [Fact]
-    public void AdminAuditAndRevisionViews_ShouldRenderLedgerPages()
-    {
-        var audit = File.ReadAllText(RepoFile("web", "Views", "Admin", "Audit.cshtml"));
-        var auditRow = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AuditEventRow.cshtml"));
-        var revisions = File.ReadAllText(RepoFile("web", "Views", "Admin", "Revisions.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("Audit trail", audit);
-        Assert.Contains("Model.AuditEvents", audit);
-        Assert.Contains("_AuditEventRow", audit);
-        Assert.Contains("What changed", auditRow);
-        Assert.Contains("bp-audit-event-time", auditRow);
-        Assert.Contains("Actor", auditRow);
-        Assert.Contains("Source", auditRow);
-        Assert.Contains("Changed fields", auditRow);
-        Assert.Contains("dirtyReason", audit);
-        Assert.Contains("metadata.ToJson()", auditRow);
-        Assert.Contains("Revision history", revisions);
-        Assert.Contains("Model.Revisions", revisions);
-        Assert.Contains("Document hashes", revisions);
-        Assert.Contains("Proof diagnostics", revisions);
-        Assert.Contains("Latest trust state", revisions);
-        Assert.Contains("passport.TrustIsDirty", revisions);
-        Assert.Contains(".bp-ledger", css);
-        Assert.Contains(".bp-ledger-timeline", css);
-        Assert.Contains(".bp-revision-card", css);
-    }
-
-    [Fact]
-    public void LocalAdmins_ShouldViewHistoryReadOnlyAndOnlyEditLatestCurrentPassport()
-    {
-        var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
-        var clusterController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-
-        Assert.Contains("CanViewBatteryHistoryAsync", access);
-        Assert.Contains("CanEditLatestBatteryPassportAsync", access);
-        Assert.Contains("isLatestForBattery", clusterController);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
--- a/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs
@@ -2,13 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class ValidationPolicyLayoutTests
 {
-    [Fact]
-    public void Program_ShouldRegisterPublishPolicyService()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Program.cs"));
-
-        Assert.Contains("AddSingleton<PassportPublishPolicyService>", source);
-    }
 
     [Fact]
     public void AdminController_ShouldSanitizeDraftSavesInsteadOfCreatingFakeVerifiedState()
@@ -19,48 +12,6 @@ public sealed class ValidationPolicyLayoutTests
         Assert.DoesNotContain("verification[\"signedAt\"] = now", source);
     }
 
-    [Fact]
-    public void EditPassportView_ShouldNotExposeDirectPublishedStatusSelection()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
-
-        Assert.DoesNotContain("value=\"published\"", markup);
-        Assert.Contains("Publishing is handled from the trust workflow", markup);
-    }
-
-    [Fact]
-    public void ConformanceView_ShouldExplainValidationPublishPolicy()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Conformance.cshtml"));
-
-        Assert.Contains("Publish gate", markup);
-        Assert.Contains("Save draft is always allowed", markup);
-        Assert.Contains("Official schema violations block signing", markup);
-        Assert.Contains("Publish requires a current valid signature proof", markup);
-    }
-
-    [Fact]
-    public void PublicRoutes_ShouldExposeOnlyPublishedCleanSignedPassportsToPublicUsers()
-    {
-        var home = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
-        var registry = File.ReadAllText(RepoFile("web", "Controllers", "RegistryController.cs"));
-        var passport = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));
-        var pages = File.ReadAllText(RepoFile("web", "Dpp", "Pages", "PassportPages.cs"));
-
-        Assert.Contains("pages.LocateAsync", home);
-
-        Assert.Contains("readers.ForAsync", registry); // the Registry lists per reader, published batteries only (decision 30)
-        Assert.Contains("!row.IsDraft", File.ReadAllText(RepoFile("web", "Dpp", "Pages", "RegistryList.cs")));
-
-        Assert.Contains("pages.SummaryAsync", passport);
-        Assert.Contains("pages.DetailAsync", passport);
-        Assert.Contains("PassportFilter.Cut", pages);
-        Assert.Contains("reader.VersionAsync", pages);
-
-        var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));
-        Assert.Contains("IsPubliclyVisible", access);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
```

`BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Step 3d-1: the old v1.2 app's code is gone; what the new app still uses stays (design 13 and 14).</summary>
[Collection(MongoCollection.Name)]
public sealed class OldAppRemovedTests(MongoFixture mongo)
{
    /// <summary>The repository's root folder, found from the web project.</summary>
    private static string RepoRoot => Path.GetDirectoryName(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Program.cs")))!;

    /// <summary>The old pages, services and models; the folder of the v1.2 data model stays as reference.</summary>
    public static TheoryData<string> RemovedFiles() => new(
        "web/Controllers/FilesApiController.cs",
        "web/Services/ProductTemplateService.cs",
        "web/Services/ProductTemplateModels.cs",
        "web/Services/PassportRepository.cs",
        "web/Services/BatteryRepository.cs",
        "web/Services/BatteryPassCanonicalDataCatalog.cs",
        "web/Services/DemoRequiredDataCompletionService.cs",
        "web/Services/EditableFieldPolicyService.cs",
        "web/Services/DataCompletionPolicyService.cs",
        "web/Services/PassportTrustWorkflowService.cs",
        "web/Services/PassportViewModelFactory.cs",
        "web/Services/BatteryIdService.cs",
        "web/Models/Trust/TrustModels.cs",
        "web/Models/ViewModels/PassportViewModel.cs",
        "web/Models/ViewModels/EditPassportViewModel.cs",
        "web/Views/Admin/EditPassport.cshtml",
        "web/Views/Admin/Conformance.cshtml",
        "web/Views/Admin/Audit.cshtml",
        "web/Views/Admin/Revisions.cshtml",
        "web/Views/ClusterAdmin/Passports.cshtml",
        "web/Views/ClusterAdmin/BatteryPassports.cshtml",
        "web/Views/Shared/_BatteryTable.cshtml",
        "web/Views/Home/Search.cshtml",
        "web/wwwroot/images/sample-battery.png");

    [Theory]
    [MemberData(nameof(RemovedFiles))]
    public void TheOldAppsFile_ShouldBeGone(string path)
    {
        Assert.False(File.Exists(Path.Combine([RepoRoot, .. path.Split('/')])), $"{path} should be gone");
    }

    [Fact]
    public void TheV12DataModel_ShouldStayInTheRepositoryAsReferenceButNotBeRead()
    {
        Assert.True(Directory.Exists(Path.Combine(RepoRoot, "BatteryPass", "io.BatteryPass.GeneralProductInformation")));
        foreach (var source in Directory.GetFiles(Path.Combine(RepoRoot, "web"), "*.cs", SearchOption.AllDirectories)
                     .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("io.BatteryPass.", File.ReadAllText(source), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheApp_ShouldRegisterNoneOfTheOldServices()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        foreach (var service in new[] { "PassportRepository", "BatteryRepository", "ProductTemplateService", "PassportTrustWorkflowService", "BatteryIdService", "EnableDemoData", "ID_GENERATION_SECRET" })
        {
            Assert.DoesNotContain(service, program, StringComparison.Ordinal);
        }

        Assert.Null(typeof(BatteryPassOptions).GetProperty("EnableDemoData"));
        Assert.Null(typeof(BatteryPassOptions).GetProperty("IdGenerationSecret"));
        foreach (var settings in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var text = File.ReadAllText(DppTestFiles.RepoFile("web", settings));
            Assert.DoesNotContain("EnableDemoData", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IdGenerationSecret", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Startup_ShouldGiveEachClusterItsDefaultTokensAndCreateNoSampleData()
    {
        var context = mongo.NewContext();
        var clusters = new ClusterRepository(context);
        var tokens = new ExternalApiRepository(context, new ExternalApiSecurityService("3d1-test-key-for-the-token-hashes-0000"));
        await clusters.UpsertClusterAsync("cluster-north", "Customer North");

        await new ExternalApiInitializer(tokens, clusters).InitializeAsync();
        await new ExternalApiInitializer(tokens, clusters).InitializeAsync();

        var created = await tokens.ListTokensAsync();
        Assert.Equal(["read", "readWrite"], created.Select(token => BsonHelpers.GetString(token, "accessMode")).Order());
        Assert.All(created, token => Assert.Equal("cluster-north", BsonHelpers.GetString(token, "autoClusterId")));
        Assert.DoesNotContain(created, token => token.GetValue("isSample", false).ToBoolean());
        Assert.Equal(["cluster-north"], (await clusters.ListClustersAsync()).Select(cluster => BsonHelpers.GetString(cluster, "clusterId")));
    }

    [Fact]
    public void TheHelpPage_ShouldOfferNoSampleTokens()
    {
        Assert.Equal([typeof(BatteryPassWeb.Dpp.Pages.BatteryPages)], typeof(HelpController).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Null(typeof(BatteryPassWeb.Models.ViewModels.ExternalApiHelpViewModel).GetProperty("SampleReadToken"));
        Assert.DoesNotContain("SAMPLEBATTERYPASSPORT", File.ReadAllText(DppTestFiles.RepoFile("web", "Services", "ExternalApiInitializer.cs")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Index", "/cluster-admin/users")]
    [InlineData("Passports", "/registry")]
    [InlineData("Secrets", "/cluster-admin/api-tokens")]
    public void TheClusterAdminsOldAddresses_ShouldLeadToTheirNewPages(string action, string expected)
    {
        var controller = new ClusterAdminController(
            new AccessControlService(new ClusterRepository(mongo.NewContext())),
            new ApplicationAuditService(mongo.NewContext()),
            new ClusterRepository(mongo.NewContext()),
            new ExternalApiRepository(mongo.NewContext(), new ExternalApiSecurityService("3d1-test-key-for-the-token-hashes-0000")))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } },
        };

        var result = (IActionResult)typeof(ClusterAdminController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!.Invoke(controller, null)!;

        Assert.Equal(expected, Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public void TheClusterAdminsTabs_ShouldBeTheirUsersAndApiTokens()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_ClusterAdminTabs.cshtml"));

        Assert.DoesNotContain("Managed passports", tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/users\"", tabs, StringComparison.Ordinal);
        Assert.Contains("href=\"/cluster-admin/api-tokens\"", tabs, StringComparison.Ordinal);
    }
}
```

```bash
git rm -q BatteryPassWeb.Tests/AdminEditGuidanceTests.cs BatteryPassWeb.Tests/AnonymousVerifyAccessTests.cs BatteryPassWeb.Tests/AuditRevisionServiceTests.cs BatteryPassWeb.Tests/BatteryAuditServiceTests.cs
git rm -q BatteryPassWeb.Tests/BatteryIdServiceTests.cs BatteryPassWeb.Tests/BatterySnapshotWorkflowTests.cs BatteryPassWeb.Tests/CanonicalPassportSnapshotServiceTests.cs BatteryPassWeb.Tests/DataCompletionPolicyServiceTests.cs
git rm -q BatteryPassWeb.Tests/DemoRequiredDataCompletionServiceTests.cs BatteryPassWeb.Tests/DocumentAccessControlTests.cs BatteryPassWeb.Tests/ExternalApiInitializerTests.cs BatteryPassWeb.Tests/JsonSchemaValidationServiceTests.cs
git rm -q BatteryPassWeb.Tests/LegacyNextExposureTests.cs BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs BatteryPassWeb.Tests/ManualFeedbackRound3Tests.cs BatteryPassWeb.Tests/PassportCanonicalDataTests.cs
git rm -q BatteryPassWeb.Tests/PassportEvidenceServiceTests.cs BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs BatteryPassWeb.Tests/PassportReadinessServiceTests.cs BatteryPassWeb.Tests/PassportSoftwarePresentationTests.cs
git rm -q BatteryPassWeb.Tests/PassportStatusLabelTests.cs BatteryPassWeb.Tests/PassportTrustServiceTests.cs BatteryPassWeb.Tests/PassportValidationServiceTests.cs BatteryPassWeb.Tests/ProductTemplateServiceTests.cs
git rm -q BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs BatteryPassWeb.Tests/SchemaRegistryServiceTests.cs BatteryPassWeb.Tests/SearchRegexHardeningTests.cs BatteryPassWeb.Tests/TrustDirtyStateTests.cs
git rm -q BatteryPassWeb.Tests/TrustModelsTests.cs BatteryPassWeb.Tests/TrustWorkflowAcceptanceTests.cs
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminEditGuidanceTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.AnonymousVerifyAccessTests|FullyQualifiedName~BatteryPassWeb.Tests.AntiForgeryProtectionTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.AuditRevisionServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAdminWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAuditServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryIdServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryRouteAndRegistryTests|FullyQualifiedName~BatteryPassWeb.Tests.BatterySnapshotWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.CanonicalPassportSnapshotServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DataCompletionPolicyServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DemoRequiredDataCompletionServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.DocumentAccessControlTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AppRolesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.OldAppRemovedTests|FullyQualifiedName~BatteryPassWeb.Tests.ErrorHandlingRolloutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryIdTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiHashOnlyTokenTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiInitializerTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.JsonSchemaValidationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.LegacyNextExposureTests|FullyQualifiedName~BatteryPassWeb.Tests.LocalAdminEditableFieldPolicyTests|FullyQualifiedName~BatteryPassWeb.Tests.ManualFeedbackRound3Tests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportCanonicalDataTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportEvidenceServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportPublishPolicyServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportReadinessServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportSoftwarePresentationTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportStatusLabelTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportTrustServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportValidationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.PublicVisibilityPolicyTests|FullyQualifiedName~BatteryPassWeb.Tests.QrWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.SchemaRegistryServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.SearchRegexHardeningTests|FullyQualifiedName~BatteryPassWeb.Tests.SecurityLimitsTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustDirtyStateTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustModelsTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustWorkflowAcceptanceTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: the build fails: the old `ExternalApiInitializer` and `ClusterAdminController` still take the old services the new tests no longer pass (CS7036).

- [ ] **Step 3: Replace `ClusterAdminController.cs`**

`web/Controllers/ClusterAdminController.cs`:

```csharp
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using MongoDB.Bson;
using BCryptNet = BCrypt.Net.BCrypt;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The cluster admins' workspace: their clusters' users and API tokens. Their batteries are listed on the Registry,
/// which replaces "Managed passports" (decision 30); step 4 brings the cluster admins' editing.
/// </summary>
[Authorize(Policy = "ClusterAdminOrAdmin")]
[Route("cluster-admin")]
public class ClusterAdminController : Controller
{
    private readonly AccessControlService _accessControlService;
    private readonly ApplicationAuditService _applicationAuditService;
    private readonly ClusterRepository _clusterRepository;
    private readonly ExternalApiRepository _externalApiRepository;

    public ClusterAdminController(
        AccessControlService accessControlService,
        ApplicationAuditService applicationAuditService,
        ClusterRepository clusterRepository,
        ExternalApiRepository externalApiRepository)
    {
        _accessControlService = accessControlService;
        _applicationAuditService = applicationAuditService;
        _clusterRepository = clusterRepository;
        _externalApiRepository = externalApiRepository;
    }

    [HttpGet("")]
    public IActionResult Index() => Redirect("/cluster-admin/users");

    /// <summary>The old "Managed passports" address leads to the Registry, which replaces it (decision 30).</summary>
    [HttpGet("passports")]
    public IActionResult Passports() => Redirect("/registry");

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] string? error, CancellationToken cancellationToken)
    {
        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var visibleClusters = AccessControlService.IsAdmin(User)
            ? clusters
            : clusters.Where(cluster => managedClusterIds.Contains(BsonHelpers.GetString(cluster, "clusterId"))).ToList();

        var memberships = await _clusterRepository.ListClusterMembershipsAsync(cancellationToken);
        var visibleMemberships = AccessControlService.IsAdmin(User)
            ? memberships
            : memberships
                .Where(membership => managedClusterIds.Contains(BsonHelpers.GetString(membership, "clusterId")))
                .ToList();
        var visibleEmails = visibleMemberships
            .Select(membership => BsonHelpers.GetString(membership, "email"))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var users = await _clusterRepository.ListUsersAsync(cancellationToken);
        var visibleUsers = AccessControlService.IsAdmin(User)
            ? users
            : users.Where(user => visibleEmails.Contains(BsonHelpers.GetString(user, "email"))).ToList();

        var model = new AdminClusterViewModel
        {
            SelectedTab = "users",
            Clusters = visibleClusters.Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            }).OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Users = visibleUsers.Select(user => new UserViewModel
            {
                Email = BsonHelpers.GetString(user, "email"),
                Name = BsonHelpers.GetString(user, "name"),
                Roles = (user.GetValue("roles", new BsonArray()) as BsonArray ?? new BsonArray())
                    .Select(role => role.ToString() ?? string.Empty)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(AccessControlService.DisplayRoleLabel)
                    .ToList()
            }).OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase).ToList(),
            Memberships = visibleMemberships.Select(membership => new ClusterMembershipViewModel
            {
                Email = BsonHelpers.GetString(membership, "email"),
                ClusterId = BsonHelpers.GetString(membership, "clusterId"),
                Role = BsonHelpers.GetString(membership, "role"),
                CreatedAt = BsonHelpers.GetString(membership, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(membership, "updatedAt")
            }).ToList()
        };

        ViewData["ErrorMessage"] = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error);
        return View(model);
    }

    [HttpGet("secrets")]
    public IActionResult Secrets() => Redirect("/cluster-admin/api-tokens");

    [HttpGet("api-tokens")]
    public async Task<IActionResult> ApiTokens(CancellationToken cancellationToken)
    {
        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        var visibleClusters = clusters
            .Where(cluster => managedClusterIds.Contains(BsonHelpers.GetString(cluster, "clusterId")))
            .Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            })
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var clusterNamesById = visibleClusters.ToDictionary(cluster => cluster.ClusterId, cluster => cluster.Name, StringComparer.OrdinalIgnoreCase);

        var tokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        var visibleTokens = tokens
            .Where(token => TokenIsInManagedScope(token, managedClusterIds))
            .Select(token => MapApiToken(token, clusterNamesById))
            .OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View("ApiTokens", new AdminClusterViewModel
        {
            SelectedTab = "api-token-management",
            Clusters = visibleClusters,
            ApiTokens = visibleTokens,
            StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
            GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty
        });
    }

    [HttpPost("api-tokens/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClusterApiToken(CancellationToken cancellationToken)
    {
        var clusterIds = Request.Form["clusterIds"]
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (clusterIds.Count == 0 || !await CanAdministerAllClustersAsync(clusterIds, cancellationToken))
        {
            TempData["ErrorMessage"] = "Choose at least one cluster you administer.";
            return Redirect("/cluster-admin/api-tokens");
        }

        var name = Text(Request.Form, "name", "Cluster API token");
        // A cluster token reads its clusters' batteries or also sends their live values; creating and publishing are the
        // team's (decision 29), so nothing above read + write is given here.
        var accessMode = ParseTokenMode(Text(Request.Form, "accessMode", "read")) == ExternalTokenAccessMode.Read
            ? ExternalTokenAccessMode.Read
            : ExternalTokenAccessMode.ReadWrite;
        var actor = AccessControlService.CurrentEmail(User);
        var (tokenDocument, tokenValue) = await _externalApiRepository.CreateTokenAsync(
            name,
            accessMode,
            clusterIds,
            allowUnassigned: false,
            globalAccess: false,
            actor: string.IsNullOrWhiteSpace(actor) ? "cluster-admin" : actor,
            cancellationToken: cancellationToken);
        await AppendClusterAdminAuditAsync(
            "apiToken.created",
            "Cluster API token created.",
            new BsonDocument
            {
                ["tokenId"] = BsonHelpers.GetString(tokenDocument, "tokenId"),
                ["name"] = name,
                ["accessMode"] = accessMode.ToString(),
                ["clusterIds"] = new BsonArray(clusterIds.Select(clusterId => (BsonValue)clusterId))
            },
            "apiToken",
            BsonHelpers.GetString(tokenDocument, "tokenId"),
            cancellationToken);
        TempData["GeneratedCredential"] = tokenValue;
        TempData["StatusMessage"] = "API token created.";
        return Redirect("/cluster-admin/api-tokens");
    }

    [HttpPost("api-tokens/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteClusterApiToken(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        if (!await ValidateClusterTokenScopeAsync(tokenId, cancellationToken))
        {
            return NotFound();
        }

        await _externalApiRepository.SetTokenActiveAsync(tokenId, false, AccessControlService.CurrentEmail(User), cancellationToken);
        await AppendClusterAdminAuditAsync(
            "apiToken.deleted",
            "Cluster API token deactivated.",
            new BsonDocument { ["tokenId"] = tokenId },
            "apiToken",
            tokenId,
            cancellationToken);
        TempData["StatusMessage"] = "API token deleted.";
        return Redirect("/cluster-admin/api-tokens");
    }

    [HttpPost("api-tokens/regenerate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateClusterApiToken(CancellationToken cancellationToken)
    {
        var tokenId = Text(Request.Form, "tokenId");
        if (!await ValidateClusterTokenScopeAsync(tokenId, cancellationToken))
        {
            return NotFound();
        }

        var tokenValue = await _externalApiRepository.RegenerateTokenAsync(tokenId, AccessControlService.CurrentEmail(User), cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            TempData["ErrorMessage"] = "API token could not be regenerated.";
        }
        else
        {
            TempData["GeneratedCredential"] = tokenValue;
            TempData["StatusMessage"] = "API token regenerated.";
            await AppendClusterAdminAuditAsync(
                "apiToken.regenerated",
                "Cluster API token regenerated.",
                new BsonDocument { ["tokenId"] = tokenId },
                "apiToken",
                tokenId,
                cancellationToken);
        }

        return Redirect("/cluster-admin/api-tokens");
    }

    [HttpPost("users/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email").Trim().ToLowerInvariant();
        var clusterId = Text(Request.Form, "clusterId");
        var role = Text(Request.Form, "role", "member");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect($"/cluster-admin/users{(string.IsNullOrWhiteSpace(email) ? string.Empty : $"?openUser={Uri.EscapeDataString(email)}")}");
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        var currentEmail = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
        if (!AccessControlService.IsAdmin(User) && email.Equals(currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Cannot change your own local admin role.")}&openUser={Uri.EscapeDataString(email)}");
        }

        var existingUser = await _clusterRepository.GetUserByEmailAsync(email, cancellationToken);
        var existingRoles = existingUser?.GetValue("roles", new BsonArray()) is BsonArray roleArray
            ? roleArray
                .Select(entry => entry.ToString())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry!)
                .ToList()
            : [];
        if (!AccessControlService.IsAdmin(User) && existingRoles.Contains(AccessControlService.RoleAdmin, StringComparer.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Local admins cannot modify global admin users.")}&openUser={Uri.EscapeDataString(email)}");
        }

        var password = Text(Request.Form, "password");
        var passwordConfirmation = Text(Request.Form, "passwordConfirmation");
        if (!string.IsNullOrWhiteSpace(password) && !password.Equals(passwordConfirmation, StringComparison.Ordinal))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Passwords do not match.")}&openUser={Uri.EscapeDataString(email)}");
        }

        if (existingUser == null && string.IsNullOrWhiteSpace(password))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Password is required for new users.")}&openUser={Uri.EscapeDataString(email)}");
        }

        var roles = existingRoles.Where(existingRole => !existingRole.Equals(AccessControlService.RoleAdmin, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (roles.Count == 0)
        {
            roles.Add(AccessControlService.RoleNormalUser);
        }

        var displayName = Text(Request.Form, "name", email);
        var passwordHash = string.IsNullOrWhiteSpace(password) ? string.Empty : BCryptNet.HashPassword(password);
        await _clusterRepository.UpsertUserAsync(email, displayName, roles.ToList(), passwordHash, cancellationToken);
        await _clusterRepository.UpsertClusterMembershipAsync(email, clusterId, role, cancellationToken);
        await AppendClusterAdminAuditAsync(
            "cluster.user.saved",
            existingUser == null ? "Cluster user created." : "Cluster user updated.",
            new BsonDocument
            {
                ["email"] = email,
                ["name"] = displayName,
                ["clusterId"] = clusterId,
                ["role"] = role,
                ["passwordChanged"] = !string.IsNullOrWhiteSpace(passwordHash)
            },
            "user",
            email,
            cancellationToken);
        return Redirect($"/cluster-admin/users?openUser={Uri.EscapeDataString(email)}");
    }

    [HttpPost("users/delete-membership")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserMembership(CancellationToken cancellationToken)
    {
        var email = Text(Request.Form, "email");
        var clusterId = Text(Request.Form, "clusterId");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clusterId))
        {
            return Redirect($"/cluster-admin/users{(string.IsNullOrWhiteSpace(email) ? string.Empty : $"?openUser={Uri.EscapeDataString(email)}")}");
        }

        if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
        {
            return NotFound();
        }

        var currentEmail = AccessControlService.CurrentEmail(User).Trim().ToLowerInvariant();
        if (!AccessControlService.IsAdmin(User) && email.Trim().ToLowerInvariant().Equals(currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"/cluster-admin/users?error={Uri.EscapeDataString("Cannot change your own local admin role.")}&openUser={Uri.EscapeDataString(email)}");
        }

        await _clusterRepository.DeleteClusterMembershipAsync(email, clusterId, cancellationToken);
        await AppendClusterAdminAuditAsync(
            "cluster.user.membership.deleted",
            "Cluster user membership deleted.",
            new BsonDocument
            {
                ["email"] = email,
                ["clusterId"] = clusterId
            },
            "user",
            email,
            cancellationToken);
        return Redirect($"/cluster-admin/users?openUser={Uri.EscapeDataString(email)}");
    }

    private async Task<bool> ValidateClusterTokenScopeAsync(string tokenId, CancellationToken cancellationToken)
    {
        var token = await _externalApiRepository.GetTokenByIdAsync(tokenId, cancellationToken);
        if (token == null)
        {
            return false;
        }

        var managedClusterIds = await ManagedClusterIdsAsync(cancellationToken);
        return TokenIsInManagedScope(token, managedClusterIds);
    }

    private async Task<bool> CanAdministerAllClustersAsync(IEnumerable<string> clusterIds, CancellationToken cancellationToken)
    {
        foreach (var clusterId in clusterIds)
        {
            if (!await _accessControlService.CanAdministerClusterAsync(User, clusterId, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private bool TokenIsInManagedScope(BsonDocument token, IReadOnlySet<string> managedClusterIds)
    {
        if (AccessControlService.IsAdmin(User))
        {
            return true;
        }

        if (token.GetValue("globalAccess", false).ToBoolean()
            || token.GetValue("allowUnassigned", false).ToBoolean())
        {
            return false;
        }

        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
        var clusterIds = tokenClusterIds
            .Select(entry => entry.ToString() ?? string.Empty)
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .ToList();
        return clusterIds.Count > 0
            && clusterIds.All(clusterId => managedClusterIds.Contains(clusterId));
    }

    private static ApiTokenViewModel MapApiToken(BsonDocument token, IReadOnlyDictionary<string, string> clusterNamesById)
    {
        var tokenClusterIds = token.GetValue("clusterIds", new BsonArray()) as BsonArray ?? new BsonArray();
        var clusterNames = tokenClusterIds
            .Select(entry => entry.ToString() ?? string.Empty)
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Select(clusterId => ClusterTokenScopeLabel(clusterId, clusterNamesById))
            .ToList();
        return new ApiTokenViewModel
        {
            TokenId = BsonHelpers.GetString(token, "tokenId"),
            Name = BsonHelpers.GetString(token, "name"),
            AccessMode = BsonHelpers.GetString(token, "accessMode"),
            GlobalAccess = token.GetValue("globalAccess", false).ToBoolean(),
            AllowUnassigned = token.GetValue("allowUnassigned", false).ToBoolean(),
            IsActive = token.GetValue("isActive", false).ToBoolean(),
            IsSample = token.GetValue("isSample", false).ToBoolean(),
            ClusterIdsLabel = clusterNames.Count == 0 ? "No clusters" : string.Join(", ", clusterNames),
            ClusterScopeNames = clusterNames,
            CreatedAt = BsonHelpers.GetString(token, "createdAt"),
            UpdatedAt = BsonHelpers.GetString(token, "updatedAt"),
            LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
        };
    }

    private static string ClusterTokenScopeLabel(string clusterId, IReadOnlyDictionary<string, string> clusterNamesById)
    {
        return clusterNamesById.TryGetValue(clusterId, out var clusterName) && !string.IsNullOrWhiteSpace(clusterName)
            ? clusterName
            : clusterId;
    }

    private static ExternalTokenAccessMode ParseTokenMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "readwritesign" => ExternalTokenAccessMode.Lifecycle,
            "lifecycle" => ExternalTokenAccessMode.Lifecycle,
            "sign" => ExternalTokenAccessMode.Sign,
            "readwrite" => ExternalTokenAccessMode.ReadWrite,
            _ => ExternalTokenAccessMode.Read
        };
    }

    private async Task<HashSet<string>> ManagedClusterIdsAsync(CancellationToken cancellationToken)
    {
        if (AccessControlService.IsAdmin(User))
        {
            var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
            return clusters
                .Select(cluster => BsonHelpers.GetString(cluster, "clusterId"))
                .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var managedClusterIds = await _accessControlService.GetAdministeredClusterIdsForUserAsync(User, cancellationToken);
        return managedClusterIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ClusterViewModel> BuildClusterViewModels(IEnumerable<BsonDocument> clusters) =>
        clusters
            .Select(cluster => new ClusterViewModel
            {
                ClusterId = BsonHelpers.GetString(cluster, "clusterId"),
                Name = BsonHelpers.GetString(cluster, "name"),
                CreatedAt = BsonHelpers.GetString(cluster, "createdAt"),
                UpdatedAt = BsonHelpers.GetString(cluster, "updatedAt")
            })
            .OrderBy(cluster => cluster.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Text(IFormCollection form, string key, string? fallback = "")
    {
        var value = form[key].FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }

    private string CurrentActor()
    {
        var email = AccessControlService.CurrentEmail(User);
        return string.IsNullOrWhiteSpace(email) ? "cluster-admin" : email;
    }

    private Task AppendClusterAdminAuditAsync(
        string eventType,
        string message,
        BsonDocument? metadata,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        return _applicationAuditService.AppendApplicationAuditEventAsync(
            eventType,
            CurrentActor(),
            "clusterAdmin",
            "cluster-admin-ui",
            message,
            metadata,
            entityType,
            entityId,
            cancellationToken: cancellationToken);
    }
}
```

- [ ] **Step 4: Write `ApplicationAuditLogViewModel.cs`**

`web/Models/ViewModels/ApplicationAuditLogViewModel.cs`:

```csharp
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Models.ViewModels;

/// <summary>The audit log page (plan 2d-1): the events in plain English, by day.</summary>
public sealed class ApplicationAuditLogViewModel
{
    public ApplicationAuditFilter Filter { get; init; } = new();
    public IReadOnlyList<BsonDocument> Events { get; init; } = [];

    /// <summary>The events in plain English, by day, newest first.</summary>
    public IReadOnlyList<AuditDay> Days { get; init; } = [];

    /// <summary>How many events the page shows; "Show 50 more" asks for 50 more.</summary>
    public int Show { get; init; } = 50;

    public bool HasMore { get; init; }

    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;
}
```

- [ ] **Step 5: Replace `AccessControlService.cs`**

`web/Services/AccessControlService.cs`:

```csharp
using System.Security.Claims;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed class AccessControlService
{
    public const string RoleAdmin = "admin";
    public const string RoleClusterAdmin = "clusterAdmin";
    public const string RoleNormalUser = "member";
    public const string RoleNotifiedBody = "notifiedBody";
    public const string RoleMarketSurveillanceAuthority = "marketSurveillanceAuthority";
    public const string RoleCommission = "commission";
    public const string RoleLiRepair = "liRepair";
    public const string RoleLiRecycling = "liRecycling";
    public const string RoleLiTreatment = "liTreatment";

    /// <summary>
    /// The three legitimate-interest roles of decision 6 (design 5.2), in place of the single "legitimateInterest" role,
    /// which no longer gives anything beyond public data.
    /// </summary>
    public static IReadOnlyList<string> LegitimateInterestRoles { get; } = [RoleLiRepair, RoleLiRecycling, RoleLiTreatment];

    private readonly ClusterRepository _clusterRepository;

    public AccessControlService(ClusterRepository clusterRepository)
    {
        _clusterRepository = clusterRepository;
    }

    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(RoleAdmin);

    public static bool HasLegitimateInterestRole(ClaimsPrincipal user) => LegitimateInterestRoles.Any(user.IsInRole);

    public static string DisplayRoleLabel(ClaimsPrincipal user)
    {
        if (user.IsInRole(RoleAdmin)) return "Global Admin";
        if (user.IsInRole(RoleClusterAdmin)) return "Cluster Admin";
        if (user.IsInRole(RoleCommission)) return "Commission";
        if (user.IsInRole(RoleMarketSurveillanceAuthority)) return "Market Surveillance Authorities";
        if (user.IsInRole(RoleNotifiedBody)) return "Notified Body";
        if (user.IsInRole(RoleLiRepair)) return "Legitimate interest: repair, repurposing, remanufacturing";
        if (user.IsInRole(RoleLiRecycling)) return "Legitimate interest: recycling and battery treatment";
        if (user.IsInRole(RoleLiTreatment)) return "Legitimate interest: authorised or WEEE treatment";
        if (user.IsInRole(RoleNormalUser)) return "Normal User";
        return "Cluster Member";
    }

    public static string DisplayRoleLabel(string role)
    {
        return role switch
        {
            RoleAdmin => "Global Admin",
            RoleClusterAdmin => "Cluster Admin",
            RoleCommission => "Commission",
            RoleMarketSurveillanceAuthority => "Market Surveillance Authorities",
            RoleNotifiedBody => "Notified Body",
            RoleLiRepair => "Legitimate interest: repair, repurposing, remanufacturing",
            RoleLiRecycling => "Legitimate interest: recycling and battery treatment",
            RoleLiTreatment => "Legitimate interest: authorised or WEEE treatment",
            RoleNormalUser => "Normal User",
            _ => "Cluster Member"
        };
    }

    public static string CurrentEmail(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email)
        ?? user.Identity?.Name
        ?? string.Empty;

    public async Task<IReadOnlyList<string>> GetAdministeredClusterIdsForUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return [];
        }

        var email = CurrentEmail(user).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return [];
        }

        var memberships = await _clusterRepository.GetClusterMembershipsForUserAsync(email, cancellationToken);
        return memberships
            .Where(membership => BsonHelpers.GetString(membership, "role").Equals(RoleClusterAdmin, StringComparison.OrdinalIgnoreCase))
            .Select(membership => BsonHelpers.GetString(membership, "clusterId"))
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> CanAdministerClusterAsync(ClaimsPrincipal user, string clusterId, CancellationToken cancellationToken = default)
    {
        if (IsAdmin(user))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(clusterId))
        {
            return false;
        }

        var managedClusterIds = await GetAdministeredClusterIdsForUserAsync(user, cancellationToken);
        return managedClusterIds.Contains(clusterId, StringComparer.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 6: Replace `ExternalApiInitializer.cs`**

`web/Services/ExternalApiInitializer.cs`:

```csharp
namespace BatteryPassWeb.Services;

/// <summary>
/// Prepares the API tokens at startup: their indexes, the clusters' indexes, and each registered cluster's default read
/// and read-write tokens (a cluster token neither creates nor publishes, so it gets no "Full access" token). The old
/// model's sample passport, sample tokens and data repairs went with step 3d-1.
/// </summary>
public sealed class ExternalApiInitializer
{
    private readonly ExternalApiRepository _externalApiRepository;
    private readonly ClusterRepository _clusterRepository;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public ExternalApiInitializer(ExternalApiRepository externalApiRepository, ClusterRepository clusterRepository)
    {
        _externalApiRepository = externalApiRepository;
        _clusterRepository = clusterRepository;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _externalApiRepository.EnsureIndexesAsync(cancellationToken);
            await _clusterRepository.EnsureIndexesAsync(cancellationToken);
            await EnsureClusterTokensAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureClusterTokensAsync(CancellationToken cancellationToken)
    {
        var clusters = await _clusterRepository.ListClustersAsync(cancellationToken);
        if (clusters.Count == 0)
        {
            return;
        }

        var existingTokens = await _externalApiRepository.ListTokensAsync(cancellationToken);
        foreach (var cluster in clusters)
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var clusterName = BsonHelpers.GetString(cluster, "name");
            if (string.IsNullOrWhiteSpace(clusterId))
            {
                continue;
            }

            var hasReadToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("read", StringComparison.OrdinalIgnoreCase));
            if (!hasReadToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - read",
                    ExternalTokenAccessMode.Read,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }

            var hasReadWriteToken = existingTokens.Any(token =>
                BsonHelpers.GetString(token, "autoClusterId").Equals(clusterId, StringComparison.OrdinalIgnoreCase)
                && BsonHelpers.GetString(token, "accessMode").Equals("readWrite", StringComparison.OrdinalIgnoreCase));
            if (!hasReadWriteToken)
            {
                await _externalApiRepository.CreateTokenAsync(
                    $"{clusterName} - readwrite",
                    ExternalTokenAccessMode.ReadWrite,
                    [clusterId],
                    allowUnassigned: false,
                    globalAccess: false,
                    actor: "system",
                    autoClusterId: clusterId,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
```

- [ ] **Step 7: Trim what stays**

Patch `3d1-task1-code.patch` (`web/Configuration/BatteryPassOptions.cs`, `web/Controllers/AdminController.cs`, `web/Controllers/HelpController.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Models/ViewModels/ExternalApiHelpViewModel.cs`, `web/Program.cs`, `web/Services/ExternalApiRepository.cs`, `web/Views/Admin/Clusters.cshtml`, `web/Views/ClusterAdmin/ApiTokens.cshtml`, `web/Views/Help/Index.cshtml`, `web/Views/Shared/_ClusterAdminTabs.cshtml`, `web/appsettings.Development.json`, `web/appsettings.json`). Save the block as `../3d1-task1-code.patch`, outside the worktree, and run `git apply ../3d1-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Configuration/BatteryPassOptions.cs b/web/Configuration/BatteryPassOptions.cs
--- a/web/Configuration/BatteryPassOptions.cs
+++ b/web/Configuration/BatteryPassOptions.cs
@@ -23,15 +23,11 @@ public sealed class BatteryPassOptions
     public string DemoAdminEmail { get; set; } = "admin@example.test";
     public string DemoAdminPassword { get; set; } = "<the fallback password>";
     public string ExternalApiEncryptionKey { get; set; } = string.Empty;
-    public string IdGenerationSecret { get; set; } = string.Empty;
     public string PowerAutomateResetWebhookUrl { get; set; } = string.Empty;
     public string PowerAutomateResetWebhookSecret { get; set; } = string.Empty;
     public string PasswordResetAppName { get; set; } = "Battery Pass";
     public string AppBaseUrl { get; set; } = string.Empty;
     public bool RequireHttpsRedirection { get; set; } = true;
-    public bool EnableDemoData { get; set; }
-    public bool EnablePublicDemoReadToken { get; set; }
-    public bool EnableDemoWriteSignTesting { get; set; }
     public bool EnableDemoAdminFallback { get; set; }
     public bool EnableRateLimiting { get; set; } = DefaultEnableRateLimiting;
     public int RateLimitWindowSeconds { get; set; } = DefaultRateLimitWindowSeconds;
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -1,7 +1,6 @@
 using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp.Audit;
 using BatteryPassWeb.Dpp.Store;
-using BatteryPassWeb.Models.Trust;
 using BatteryPassWeb.Models.ViewModels;
 using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Authorization;
@@ -19,7 +18,6 @@ namespace BatteryPassWeb.Controllers;
 [Route("admin")]
 public class AdminController : Controller
 {
-    private readonly PassportRepository _passportRepository;
     private readonly ClusterRepository _clusterRepository;
     private readonly ExternalApiRepository _externalApiRepository;
     private readonly ApplicationAuditService _applicationAuditService;
@@ -27,14 +25,12 @@ public class AdminController : Controller
     private readonly DppStore _dppStore;
 
     public AdminController(
-        PassportRepository passportRepository,
         ClusterRepository clusterRepository,
         ExternalApiRepository externalApiRepository,
         ApplicationAuditService applicationAuditService,
         ApplicationSettingsService applicationSettingsService,
         DppStore dppStore)
     {
-        _passportRepository = passportRepository;
         _clusterRepository = clusterRepository;
         _externalApiRepository = externalApiRepository;
         _applicationAuditService = applicationAuditService;
@@ -185,9 +181,6 @@ public class AdminController : Controller
                     LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
                 };
             }).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList(),
-            SamplePassportId = ExternalApiInitializer.SamplePassportId,
-            SampleReadToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadTokenId, ExternalApiInitializer.SampleReadTokenValue, cancellationToken),
-            SampleReadWriteToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadWriteTokenId, ExternalApiInitializer.SampleReadWriteTokenValue, cancellationToken),
             StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
             ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
             GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty,
@@ -274,12 +267,11 @@ public class AdminController : Controller
     public async Task<IActionResult> DeleteCluster(CancellationToken cancellationToken)
     {
         var clusterId = Text(Request.Form, "clusterId");
-        var passportCount = await _passportRepository.CountPassportsByClusterAsync(clusterId, cancellationToken);
         var batteryCount = await _dppStore.CountPacksInClusterAsync(clusterId, cancellationToken);
         var membershipCount = await _clusterRepository.CountMembershipsByClusterAsync(clusterId, cancellationToken);
-        if (passportCount > 0 || batteryCount > 0 || membershipCount > 0)
+        if (batteryCount > 0 || membershipCount > 0)
         {
-            TempData["ErrorMessage"] = $"Cluster {clusterId} has {passportCount} linked passport(s), {batteryCount} linked battery(ies) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
+            TempData["ErrorMessage"] = $"Cluster {clusterId} has {batteryCount} linked battery(ies) and {membershipCount} linked user membership(s). Use force delete only after confirming linked data removal.";
             return Redirect("/admin/clusters?tab=clusters");
         }
 
@@ -306,7 +298,6 @@ public class AdminController : Controller
             return Redirect("/admin/clusters?tab=clusters");
         }
 
-        await _passportRepository.ClearPassportClusterAsync(clusterId, cancellationToken);
         var batteriesCleared = await _dppStore.ClearClusterAsync(clusterId, cancellationToken);
         await _clusterRepository.DeleteClusterAsync(clusterId, cancellationToken);
         await AppendAdminAuditAsync(
@@ -977,17 +968,6 @@ public class AdminController : Controller
             cancellationToken: cancellationToken);
     }
 
-    private async Task<string> ResolveTokenValueAsync(string tokenId, string fallback, CancellationToken cancellationToken)
-    {
-        var tokenDocument = await _externalApiRepository.GetTokenByIdAsync(tokenId, cancellationToken);
-        if (tokenDocument == null)
-        {
-            return fallback;
-        }
-
-        return fallback;
-    }
-
     private static string Text(IFormCollection form, string key, string? fallback = "")
     {
         var value = form[key].FirstOrDefault()?.Trim();
diff --git a/web/Controllers/HelpController.cs b/web/Controllers/HelpController.cs
--- a/web/Controllers/HelpController.cs
+++ b/web/Controllers/HelpController.cs
@@ -1,10 +1,7 @@
-using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Models.ViewModels;
-using BatteryPassWeb.Services;
 using Microsoft.AspNetCore.Authorization;
 using Microsoft.AspNetCore.Mvc;
-using Microsoft.Extensions.Options;
 
 namespace BatteryPassWeb.Controllers;
 
@@ -12,21 +9,11 @@ namespace BatteryPassWeb.Controllers;
 [Route("help")]
 public class HelpController : Controller
 {
-    private readonly ExternalApiRepository _externalApiRepository;
-    private readonly AccessControlService _accessControlService;
-    private readonly BatteryPassOptions _options;
     private readonly BatteryPages _batteryPages;
 
-    public HelpController(
-        ExternalApiRepository externalApiRepository,
-        AccessControlService accessControlService,
-        IOptions<BatteryPassOptions> options,
-        BatteryPages batteryPages)
+    public HelpController(BatteryPages batteryPages)
     {
         _batteryPages = batteryPages;
-        _externalApiRepository = externalApiRepository;
-        _accessControlService = accessControlService;
-        _options = options.Value;
     }
 
     /// <summary>The guide for everyone: finding a passport and what each role sees.</summary>
@@ -49,63 +36,15 @@ public class HelpController : Controller
     [HttpGet("api")]
     public async Task<IActionResult> Index(CancellationToken cancellationToken)
     {
-        var readTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadTokenId, cancellationToken);
-        var readWriteTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleReadWriteTokenId, cancellationToken);
-        var lifecycleTokenDocument = await _externalApiRepository.GetTokenByIdAsync(ExternalApiInitializer.SampleLifecycleTokenId, cancellationToken);
-        var canUsePrivilegedDemoTokens = await CanUsePrivilegedDemoTokensAsync(cancellationToken);
-
         var model = new ExternalApiHelpViewModel
         {
             BasePath = "/api/external/v1",
             PublicBaseUrl = RequestBaseUrl(),
             ExampleBatteryId = (await _batteryPages.ListAsync(null, cancellationToken)).Rows.FirstOrDefault(row => !row.IsDraft)?.Identifier ?? string.Empty,
-            SampleReadToken = _options.EnableDemoData && _options.EnablePublicDemoReadToken && readTokenDocument != null
-                ? ExternalApiInitializer.SampleReadTokenValue
-                : string.Empty,
-            SampleReadWriteToken = canUsePrivilegedDemoTokens && readWriteTokenDocument != null
-                ? ExternalApiInitializer.SampleReadWriteTokenValue
-                : string.Empty,
-            SampleLifecycleToken = canUsePrivilegedDemoTokens && lifecycleTokenDocument != null
-                ? ExternalApiInitializer.SampleLifecycleTokenValue
-                : string.Empty,
-            DemoWriteSignTestingEnabled = _options.EnableDemoData && _options.EnableDemoWriteSignTesting,
-            CanUsePrivilegedDemoTokens = canUsePrivilegedDemoTokens,
-            PrivilegedDemoTokenMessage = PrivilegedDemoTokenMessage()
         };
 
         return View(model);
     }
 
     private string RequestBaseUrl() => $"{Request.Scheme}://{Request.Host}";
-
-    private async Task<bool> CanUsePrivilegedDemoTokensAsync(CancellationToken cancellationToken)
-    {
-        if (!_options.EnableDemoData || !_options.EnableDemoWriteSignTesting)
-        {
-            return false;
-        }
-
-        if (User.IsInRole(AccessControlService.RoleAdmin))
-        {
-            return true;
-        }
-
-        return User.IsInRole(AccessControlService.RoleClusterAdmin)
-            && await _accessControlService.CanAdministerClusterAsync(User, ExternalApiInitializer.SampleApiClusterId, cancellationToken);
-    }
-
-    private string PrivilegedDemoTokenMessage()
-    {
-        if (!_options.EnableDemoData)
-        {
-            return "Demo data is disabled for this environment.";
-        }
-
-        if (!_options.EnableDemoWriteSignTesting)
-        {
-            return "Demo write/sign testing is disabled for this environment.";
-        }
-
-        return "Use an approved tester account: global admin or demo-cluster cluster admin.";
-    }
 }
diff --git a/web/Models/ViewModels/AdminClusterViewModel.cs b/web/Models/ViewModels/AdminClusterViewModel.cs
--- a/web/Models/ViewModels/AdminClusterViewModel.cs
+++ b/web/Models/ViewModels/AdminClusterViewModel.cs
@@ -1,4 +1,3 @@
-using BatteryPassWeb.Models.Trust;
 using BatteryPassWeb.Services;
 
 namespace BatteryPassWeb.Models.ViewModels;
@@ -11,95 +10,12 @@ public sealed class AdminClusterViewModel
     public IReadOnlyList<UserViewModel> Users { get; init; } = [];
     public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
     public IReadOnlyList<ApiTokenViewModel> ApiTokens { get; init; } = [];
-    public string SamplePassportId { get; init; } = string.Empty;
-    public string SampleReadToken { get; init; } = string.Empty;
-    public string SampleReadWriteToken { get; init; } = string.Empty;
     public string StatusMessage { get; init; } = string.Empty;
     public string ErrorMessage { get; init; } = string.Empty;
     public string GeneratedCredential { get; init; } = string.Empty;
     public int SessionTimeoutMinutes { get; init; } = 180;
 }
 
-public sealed class ProductTemplateSummaryViewModel
-{
-    public string ProductId { get; init; } = string.Empty;
-    public string ProductName { get; init; } = string.Empty;
-    public string Description { get; init; } = string.Empty;
-    public string ImageUrl { get; init; } = string.Empty;
-    public int ModuleCount { get; init; }
-    public int BatteryVersionCount { get; init; }
-    public int RequiredFieldCount { get; init; }
-    public int DocumentCount { get; init; }
-}
-
-public sealed class ProductVersionEditViewModel
-{
-    public string Version { get; init; } = string.Empty;
-    public double BatteryMassKg { get; init; }
-    public double RatedEnergyKwh { get; init; }
-    public double RatedCapacityAh { get; init; }
-    public double RatedMaximumPowerKw { get; init; }
-    public double NominalVoltageV { get; init; }
-    public double ExpectedLifetimeYears { get; init; }
-    public double ExpectedCycles { get; init; }
-    public double SupplyChainIndex { get; init; }
-    public double CarbonFootprint { get; init; }
-    public string PerformanceClass { get; init; } = string.Empty;
-    public IReadOnlyDictionary<string, double> MaterialMassesKg { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
-    public IReadOnlyDictionary<string, double> CarbonStages { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
-    public IReadOnlyDictionary<string, ProductTemplateRecycledContentViewModel> RecycledContent { get; init; } = new Dictionary<string, ProductTemplateRecycledContentViewModel>(StringComparer.OrdinalIgnoreCase);
-    public string SoftwareVersion { get; init; } = string.Empty;
-    public string SoftwareReleaseDate { get; init; } = string.Empty;
-    public string SoftwareLatestUpdate { get; init; } = string.Empty;
-    public IReadOnlyList<ProductSoftwareVersionEditViewModel> SoftwareVersions { get; init; } = [];
-    public IReadOnlyList<ProductTemplateDocumentViewModel> TemplateDocuments { get; init; } = [];
-    public IReadOnlyList<string> RequiredFieldKeys { get; init; } = [];
-}
-
-public sealed class ProductSoftwareVersionEditViewModel
-{
-    public string SoftwareVersion { get; init; } = string.Empty;
-    public string SoftwareReleaseDate { get; init; } = string.Empty;
-    public string SoftwareLatestUpdate { get; init; } = string.Empty;
-}
-
-public sealed class ProductTemplateDocumentViewModel
-{
-    public string DocumentKey { get; init; } = string.Empty;
-    public string Label { get; init; } = string.Empty;
-    public string FileName { get; init; } = string.Empty;
-    public string Visibility { get; init; } = string.Empty;
-}
-
-public sealed class ProductTemplateFormCatalogItemViewModel
-{
-    public string ProductId { get; init; } = string.Empty;
-    public string ProductName { get; init; } = string.Empty;
-    public string ImageUrl { get; init; } = string.Empty;
-    public int ModuleCount { get; init; }
-    public double BatteryMassKg { get; init; }
-    public double RatedEnergyKwh { get; init; }
-    public double RatedCapacityAh { get; init; }
-    public double RatedMaximumPowerKw { get; init; }
-    public double NominalVoltageV { get; init; }
-    public double ExpectedLifetimeYears { get; init; }
-    public double ExpectedCycles { get; init; }
-    public double SupplyChainIndex { get; init; }
-    public double CarbonFootprint { get; init; }
-    public string PerformanceClass { get; init; } = string.Empty;
-    public IReadOnlyDictionary<string, double> MaterialMassesKg { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
-    public IReadOnlyDictionary<string, double> CarbonStages { get; init; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
-    public IReadOnlyDictionary<string, ProductTemplateRecycledContentViewModel> RecycledContent { get; init; } = new Dictionary<string, ProductTemplateRecycledContentViewModel>(StringComparer.OrdinalIgnoreCase);
-    public IReadOnlyList<ProductVersionEditViewModel> ProductVersions { get; init; } = [];
-    public string LatestProductVersion { get; init; } = string.Empty;
-}
-
-public sealed class ProductTemplateRecycledContentViewModel
-{
-    public double PreConsumerShare { get; init; }
-    public double PostConsumerShare { get; init; }
-}
-
 public sealed class ClusterViewModel
 {
     public string ClusterId { get; init; } = string.Empty;
diff --git a/web/Models/ViewModels/ExternalApiHelpViewModel.cs b/web/Models/ViewModels/ExternalApiHelpViewModel.cs
--- a/web/Models/ViewModels/ExternalApiHelpViewModel.cs
+++ b/web/Models/ViewModels/ExternalApiHelpViewModel.cs
@@ -7,10 +7,4 @@ public sealed class ExternalApiHelpViewModel
 
     /// <summary>A published battery's ID (#6), to try the reading calls on; empty when none is published yet.</summary>
     public string ExampleBatteryId { get; init; } = string.Empty;
-    public string SampleReadToken { get; init; } = string.Empty;
-    public string SampleReadWriteToken { get; init; } = string.Empty;
-    public string SampleLifecycleToken { get; init; } = string.Empty;
-    public bool DemoWriteSignTestingEnabled { get; init; }
-    public bool CanUsePrivilegedDemoTokens { get; init; }
-    public string PrivilegedDemoTokenMessage { get; init; } = string.Empty;
 }
diff --git a/web/Program.cs b/web/Program.cs
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -75,9 +75,6 @@ builder.Services.Configure<BatteryPassOptions>(options =>
     options.ExternalApiEncryptionKey = Environment.GetEnvironmentVariable("EXTERNAL_API_ENCRYPTION_KEY")
         ?? builder.Configuration["BatteryPass:ExternalApiEncryptionKey"]
         ?? string.Empty;
-    options.IdGenerationSecret = Environment.GetEnvironmentVariable("ID_GENERATION_SECRET")
-        ?? builder.Configuration["BatteryPass:IdGenerationSecret"]
-        ?? string.Empty;
     options.PowerAutomateResetWebhookUrl = Environment.GetEnvironmentVariable("POWER_AUTOMATE_RESET_WEBHOOK_URL")
         ?? builder.Configuration["BatteryPass:PowerAutomateResetWebhookUrl"]
         ?? string.Empty;
@@ -95,15 +92,6 @@ builder.Services.Configure<BatteryPassOptions>(options =>
     options.RequireHttpsRedirection = DeploymentSecurityConfiguration.ParseRequireHttpsRedirection(
         Environment.GetEnvironmentVariable("REQUIRE_HTTPS_REDIRECTION")
             ?? builder.Configuration["BatteryPass:RequireHttpsRedirection"]);
-    options.EnableDemoData = ParseFeatureFlag(
-        Environment.GetEnvironmentVariable("ENABLE_DEMO_DATA")
-            ?? builder.Configuration["BatteryPass:EnableDemoData"]);
-    options.EnablePublicDemoReadToken = ParseFeatureFlag(
-        Environment.GetEnvironmentVariable("ENABLE_PUBLIC_DEMO_READ_TOKEN")
-            ?? builder.Configuration["BatteryPass:EnablePublicDemoReadToken"]);
-    options.EnableDemoWriteSignTesting = ParseFeatureFlag(
-        Environment.GetEnvironmentVariable("ENABLE_DEMO_WRITE_SIGN_TESTING")
-            ?? builder.Configuration["BatteryPass:EnableDemoWriteSignTesting"]);
     options.EnableDemoAdminFallback = ParseFeatureFlag(
         Environment.GetEnvironmentVariable("ENABLE_DEMO_ADMIN_FALLBACK")
             ?? builder.Configuration["BatteryPass:EnableDemoAdminFallback"]);
@@ -204,43 +192,13 @@ builder.Services.AddHttpsRedirection(options =>
     options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
 });
 builder.Services.AddSingleton<MongoContext>();
-builder.Services.AddSingleton<PassportRepository>();
-builder.Services.AddSingleton<BatteryRepository>();
-builder.Services.AddSingleton<BatteryTableService>();
-builder.Services.AddSingleton<BatteryPassportSnapshotService>();
-builder.Services.AddSingleton<BatteryPassportDeltaService>();
-builder.Services.AddSingleton<BatteryTemplateUpdateService>();
-builder.Services.AddSingleton<BatteryRouteResolutionService>();
 builder.Services.AddSingleton<ClusterRepository>();
-builder.Services.AddSingleton<PassportViewModelFactory>();
-builder.Services.AddSingleton<SchemaRegistryService>();
-builder.Services.AddSingleton<JsonSchemaValidationService>();
-builder.Services.AddSingleton<PassportValidationService>();
-builder.Services.AddSingleton<PassportPublishPolicyService>();
-builder.Services.AddSingleton<PassportReadinessService>();
-builder.Services.AddSingleton<PassportEvidenceService>();
-builder.Services.AddSingleton<DataCompletionPolicyService>();
-builder.Services.AddSingleton<EditableFieldPolicyService>();
-builder.Services.AddSingleton<ProductTemplateService>();
-builder.Services.AddSingleton<DemoRequiredDataCompletionService>();
-builder.Services.AddSingleton<PassportDataNormalizationService>();
-builder.Services.AddSingleton<CanonicalPassportSnapshotService>();
 builder.Services.AddSingleton<DemoSigningKeyService>();
-builder.Services.AddSingleton<PassportTrustService>();
-builder.Services.AddSingleton<AuditRevisionService>();
-builder.Services.AddSingleton<BatteryAuditService>();
 builder.Services.AddSingleton<ApplicationAuditService>();
 builder.Services.AddSingleton<ApplicationSettingsService>();
 builder.Services.AddSingleton<AuthenticationSessionService>();
-builder.Services.AddSingleton<PassportTrustWorkflowService>();
 builder.Services.AddSingleton<AccessControlService>();
 builder.Services.AddSingleton<PassportQrCodeService>();
-builder.Services.AddSingleton(provider =>
-{
-    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;
-    var environment = provider.GetRequiredService<IWebHostEnvironment>();
-    return new BatteryIdService(options.IdGenerationSecret, environment.IsDevelopment());
-});
 builder.Services.AddSingleton<AuthService>();
 builder.Services.AddHttpClient<IEmailSender, PowerAutomateEmailSender>();
 builder.Services.AddSingleton<ExternalApiSecurityService>();
diff --git a/web/Services/ExternalApiRepository.cs b/web/Services/ExternalApiRepository.cs
--- a/web/Services/ExternalApiRepository.cs
+++ b/web/Services/ExternalApiRepository.cs
@@ -41,8 +41,6 @@ public sealed class ExternalApiTokenContext
 
 public sealed class ExternalApiRepository
 {
-    public const string ValidateSignPublish = "validateSignPublish";
-
     private readonly MongoContext _mongoContext;
     private readonly ExternalApiSecurityService _securityService;
 
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -565,7 +565,7 @@
                 <div class="bp-token-create-topline">
                     <label>
                         Label
-                        <input type="text" name="name" placeholder="e.g. North telemetry integration" required />
+                        <input type="text" name="name" placeholder="e.g. North battery system" required />
                     </label>
                     <label>
                         Permission
diff --git a/web/Views/ClusterAdmin/ApiTokens.cshtml b/web/Views/ClusterAdmin/ApiTokens.cshtml
--- a/web/Views/ClusterAdmin/ApiTokens.cshtml
+++ b/web/Views/ClusterAdmin/ApiTokens.cshtml
@@ -78,7 +78,7 @@
             <div class="bp-token-create-topline">
                 <label>
                     Label
-                    <input type="text" name="name" placeholder="e.g. North telemetry integration" required />
+                    <input type="text" name="name" placeholder="e.g. North battery system" required />
                 </label>
                 <label>
                     Permission
diff --git a/web/Views/Help/Index.cshtml b/web/Views/Help/Index.cshtml
--- a/web/Views/Help/Index.cshtml
+++ b/web/Views/Help/Index.cshtml
@@ -34,39 +34,6 @@
                     <span class="text-secondary small">No battery passport is published yet.</span>
                 }
             </div>
-            <div>
-                <p class="mb-1 text-secondary small">Sample read token</p>
-                @if (!string.IsNullOrWhiteSpace(Model.SampleReadToken))
-                {
-                    <code>@Model.SampleReadToken</code>
-                }
-                else
-                {
-                    <span class="text-secondary small">Demo read token disabled.</span>
-                }
-            </div>
-            <div>
-                <p class="mb-1 text-secondary small">Sample read-write token</p>
-                @if (Model.CanUsePrivilegedDemoTokens)
-                {
-                    <code>@Model.SampleReadWriteToken</code>
-                }
-                else
-                {
-                    <span class="text-secondary small">Available to an approved tester.</span>
-                }
-            </div>
-            <div>
-                <p class="mb-1 text-secondary small">Sample Passport Lifecycle token</p>
-                @if (Model.CanUsePrivilegedDemoTokens)
-                {
-                    <code>@Model.SampleLifecycleToken</code>
-                }
-                else
-                {
-                    <span class="text-secondary small">Available to an approved tester.</span>
-                }
-            </div>
         </div>
     </section>
 
@@ -457,20 +424,9 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                         </button>
                     </span>
                 </label>
-                <label>Token
-                    <select id="tester-token-preset">
-                        <option value="sample-read">Sample read token</option>
-                        <option value="sample-read-write">Sample read-write token</option>
-                        <option value="sample-lifecycle">Sample Passport Lifecycle token</option>
-                        <option value="custom">Your own token</option>
-                    </select>
-                    @if (!Model.CanUsePrivilegedDemoTokens)
-                    {
-                        <small class="bp-wb-hint">@Model.PrivilegedDemoTokenMessage</small>
-                    }
-                </label>
                 <label>Token value
-                    <input id="tester-token" value="@Model.SampleReadToken" autocomplete="off" spellcheck="false" />
+                    <input id="tester-token" value="" autocomplete="off" spellcheck="false" />
+                    <small class="bp-wb-hint">Paste a token from API Token Management. Its value is shown once, when it is created.</small>
                     <small id="tester-team-note" class="bp-wb-hint" hidden>This call needs a team (global) token: paste one from API Token Management.</small>
                 </label>
             </div>
@@ -561,10 +517,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
         (() => {
             const basePath = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.BasePath));
             const exampleBatteryId = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.ExampleBatteryId));
-            const sampleReadToken = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleReadToken));
-            const sampleReadWriteToken = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleReadWriteToken));
-            const sampleLifecycleToken = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SampleLifecycleToken));
-            const canUsePrivilegedDemoTokens = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.CanUsePrivilegedDemoTokens));
 
             const docSearch = document.getElementById('doc-search');
             const docItems = Array.from(document.querySelectorAll('.bp-help-doc-item'));
@@ -620,7 +572,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
             const pathEl = document.getElementById('tester-path');
             const authFormatEl = document.getElementById('tester-auth-format');
             const batteryIdEl = document.getElementById('tester-battery-id');
-            const tokenPresetEl = document.getElementById('tester-token-preset');
             const tokenEl = document.getElementById('tester-token');
             const bodyEl = document.getElementById('tester-body');
             const rawRequestEl = document.getElementById('tester-raw-request');
@@ -658,86 +609,72 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 readFull: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 listClusters: {
                     method: 'GET',
                     pathTemplate: `${basePath}/clusters`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 listClusterBatteries: {
                     method: 'GET',
                     pathTemplate: `${basePath}/clusters/demo-cluster/batteries`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 readSection: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/section/identifiersAndProductData`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 readSectionPerformance: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/section/performanceAndDurability`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 readSectionCircularity: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/section/circularityAndResourceEfficiency`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 readValuesAliases: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/values?path=stateOfChargeSoC&path=%2311&path=batteryMass`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 readPathsAll: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/paths`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 readBatteryPassports: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/passports`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 writeLiveValues: {
                     method: 'POST',
                     pathTemplate: `${basePath}/batteries/{batteryId}/live-values`,
-                    body: liveValuesBody,
-                    tokenPreset: 'sample-read-write'
+                    body: liveValuesBody
                 },
                 previewLiveValues: {
                     method: 'POST',
                     pathTemplate: `${basePath}/batteries/{batteryId}/live-values?preview=true`,
-                    body: liveValuesBody,
-                    tokenPreset: 'sample-read-write'
+                    body: liveValuesBody
                 },
                 readLiveHistory24h: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batteries/{batteryId}/live-values/history?hours=24`,
-                    body: '',
-                    tokenPreset: 'sample-read'
+                    body: ''
                 },
                 listBatches: {
                     method: 'GET',
                     pathTemplate: `${basePath}/batches`,
-                    body: '',
-                    tokenPreset: 'custom'
+                    body: ''
                 },
                 listOperators: {
                     method: 'GET',
                     pathTemplate: `${basePath}/operators`,
-                    body: '',
-                    tokenPreset: 'custom'
+                    body: ''
                 },
                 createBattery: {
                     method: 'POST',
@@ -750,23 +687,15 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                             batterySerialNumber: 'SN-API-REPLACE-ME',
                             manufacturingDate: new Date().toISOString().slice(0, 7)
                         }
-                    }, null, 2),
-                    tokenPreset: 'custom'
+                    }, null, 2)
                 },
                 publishBattery: {
                     method: 'POST',
                     pathTemplate: `${basePath}/batteries/{batteryId}/publish`,
-                    body: '',
-                    tokenPreset: 'custom'
+                    body: ''
                 }
             };
 
-            const tokenByPreset = {
-                'sample-read': sampleReadToken,
-                'sample-read-write': canUsePrivilegedDemoTokens ? sampleReadWriteToken : '',
-                'sample-lifecycle': canUsePrivilegedDemoTokens ? sampleLifecycleToken : ''
-            };
-
             const utf8ToBase64 = (value) => {
                 const bytes = new TextEncoder().encode(value);
                 let binary = '';
@@ -787,7 +716,7 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
 
                 renderJsonWindow(targetWindow, 'Readable battery paths', 'Loading readable values...');
 
-                const authorization = `Basic ${utf8ToBase64(`${sampleReadToken}:`)}`;
+                const authorization = `Basic ${utf8ToBase64(`${tokenEl.value.trim()}:`)}`;
                 try {
                     const httpResponse = await fetch(link.href, {
                         headers: {
@@ -857,15 +786,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 return (pathTemplate || '').replaceAll('{batteryId}', context.batteryId);
             };
 
-            const setTokenFromPreset = () => {
-                const presetValue = tokenPresetEl.value;
-                if (presetValue === 'custom') {
-                    return;
-                }
-
-                tokenEl.value = tokenByPreset[presetValue] || '';
-            };
-
             const buildRawRequest = () => {
                 const method = methodEl.value || 'GET';
                 const absoluteUrl = resolveAbsoluteUrl(pathEl.value);
@@ -972,7 +892,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 if (authorization.toLowerCase().startsWith('basic ')) {
                     const payload = authorization.slice(6).trim();
                     const decodedToken = decodeBase64Token(payload);
-                    tokenPresetEl.value = 'custom';
                     if (decodedToken) {
                         authFormatEl.value = 'base64';
                         tokenEl.value = decodedToken;
@@ -1052,8 +971,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 templateSelect.value = 'readFull';
                 authFormatEl.value = 'base64';
                 batteryIdEl.value = exampleBatteryId;
-                tokenPresetEl.value = 'sample-read';
-                tokenEl.value = sampleReadToken;
                 liveInputs.forEach(input => { input.value = ''; });
                 measuredNowEl.checked = true;
                 measuredAtEl.disabled = true;
@@ -1063,11 +980,6 @@ GET @Model.BasePath/batteries/{id}/paths?section=performanceAndDurability</code>
                 renderAnswer(null);
             });
 
-            tokenPresetEl?.addEventListener('change', () => {
-                setTokenFromPreset();
-                syncRawRequestFromControls();
-            });
-
             batteryIdEl?.addEventListener('input', refreshTemplatePathFromContext);
             batteryIdEl?.addEventListener('change', refreshTemplatePathFromContext);
 
diff --git a/web/Views/Shared/_ClusterAdminTabs.cshtml b/web/Views/Shared/_ClusterAdminTabs.cshtml
--- a/web/Views/Shared/_ClusterAdminTabs.cshtml
+++ b/web/Views/Shared/_ClusterAdminTabs.cshtml
@@ -8,7 +8,6 @@
 <form class="bp-admin-mobile-tab-form mt-4" aria-label="Cluster administration section">
     <label for="bp-cluster-admin-mobile-tab-select">Cluster admin section</label>
     <select id="bp-cluster-admin-mobile-tab-select" data-admin-mobile-tab-select>
-        <option value="/cluster-admin/passports" selected="@IsSelected("passports")">Managed passports</option>
         <option value="/cluster-admin/users" selected="@IsSelected("users")">Users</option>
         <option value="/cluster-admin/api-tokens" selected="@IsSelected("api-tokens")">API token management</option>
     </select>
@@ -16,7 +15,6 @@
 
 <nav class="bp-local-admin-nav" aria-label="Local admin tabs">
     <div class="bp-tab-row">
-        <a class="@TabClass("passports")" href="/cluster-admin/passports">Managed passports</a>
         <a class="@TabClass("users")" href="/cluster-admin/users">Users</a>
         <a class="@TabClass("api-tokens")" href="/cluster-admin/api-tokens">API token management</a>
     </div>
diff --git a/web/appsettings.Development.json b/web/appsettings.Development.json
--- a/web/appsettings.Development.json
+++ b/web/appsettings.Development.json
@@ -14,10 +14,6 @@
     "DemoAdminEmail": "admin@example.test",
     "DemoAdminPassword": "<the fallback password>",
     "ExternalApiEncryptionKey": "local-development-encryption-key",
-    "IdGenerationSecret": "battery-pass-local-development-id-secret",
-    "EnableDemoData": true,
-    "EnablePublicDemoReadToken": true,
-    "EnableDemoWriteSignTesting": true,
     "EnableDemoAdminFallback": true,
     "EnableRateLimiting": true,
     "RateLimitWindowSeconds": 60,
diff --git a/web/appsettings.json b/web/appsettings.json
--- a/web/appsettings.json
+++ b/web/appsettings.json
@@ -14,10 +14,6 @@
     "DemoAdminEmail": "admin@example.test",
     "DemoAdminPassword": "<the fallback password>",
     "ExternalApiEncryptionKey": "replace-with-32-random-characters",
-    "IdGenerationSecret": "",
-    "EnableDemoData": false,
-    "EnablePublicDemoReadToken": false,
-    "EnableDemoWriteSignTesting": false,
     "EnableDemoAdminFallback": false,
     "EnableRateLimiting": true,
     "RateLimitWindowSeconds": 60,
```

- [ ] **Step 8: Delete the old files**

```bash
git rm -q web/Controllers/FilesApiController.cs web/Models/Trust/DataCompletionPolicyModels.cs web/Models/Trust/PassportEvidenceModels.cs web/Models/Trust/PassportReadinessModels.cs
git rm -q web/Models/Trust/TrustModels.cs web/Models/ViewModels/BatteryViewModels.cs web/Models/ViewModels/ConformanceViewModel.cs web/Models/ViewModels/EditPassportViewModel.cs
git rm -q web/Models/ViewModels/PassportDetailViewModel.cs web/Models/ViewModels/PassportSummaryPageViewModel.cs web/Models/ViewModels/PassportSummaryViewModel.cs web/Models/ViewModels/PassportViewModel.cs
git rm -q web/Models/ViewModels/SearchPageViewModel.cs web/Services/AuditRevisionService.cs web/Services/BatteryAuditService.cs web/Services/BatteryIdService.cs
git rm -q web/Services/BatteryImageCatalog.cs web/Services/BatteryPassCanonicalDataCatalog.cs web/Services/BatteryPassportDeltaService.cs web/Services/BatteryPassportSnapshotService.cs
git rm -q web/Services/BatteryRepository.cs web/Services/BatteryRouteResolutionService.cs web/Services/BatteryTableService.cs web/Services/BatteryTemplateUpdateService.cs
git rm -q web/Services/CanonicalPassportSnapshotService.cs web/Services/DataCompletionPolicyService.cs web/Services/DemoRequiredDataCompletionService.cs web/Services/EditableFieldPolicyService.cs
git rm -q web/Services/JsonSchemaValidationService.cs web/Services/PassportDataNormalizationService.cs web/Services/PassportEvidenceService.cs web/Services/PassportPublishPolicyService.cs
git rm -q web/Services/PassportReadinessService.cs web/Services/PassportRepository.cs web/Services/PassportTrustService.cs web/Services/PassportTrustWorkflowService.cs
git rm -q web/Services/PassportValidationService.cs web/Services/PassportViewModelFactory.cs web/Services/ProductTemplateModels.cs web/Services/ProductTemplateService.cs
git rm -q web/Services/SchemaRegistryService.cs web/Services/SearchRegexBuilder.cs web/Views/Admin/Audit.cshtml web/Views/Admin/Conformance.cshtml
git rm -q web/Views/Admin/EditPassport.cshtml web/Views/Admin/Revisions.cshtml web/Views/ClusterAdmin/BatteryPassports.cshtml web/Views/ClusterAdmin/Index.cshtml
git rm -q web/Views/ClusterAdmin/Passports.cshtml web/Views/Home/Search.cshtml web/Views/Shared/_AuditEventRow.cshtml web/Views/Shared/_BatteryTable.cshtml
git rm -q web/wwwroot/images/compact13.png web/wwwroot/images/compact7.png web/wwwroot/images/core.png web/wwwroot/images/sample-battery.png
```

- [ ] **Step 9: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminEditGuidanceTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.AnonymousVerifyAccessTests|FullyQualifiedName~BatteryPassWeb.Tests.AntiForgeryProtectionTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.AuditRevisionServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAdminWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryAuditServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryIdServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryRouteAndRegistryTests|FullyQualifiedName~BatteryPassWeb.Tests.BatterySnapshotWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.CanonicalPassportSnapshotServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.ConformanceLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.CopyIdButtonLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DataCompletionPolicyServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.DemoRequiredDataCompletionServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.DocumentAccessControlTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AppRolesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.OldAppRemovedTests|FullyQualifiedName~BatteryPassWeb.Tests.ErrorHandlingRolloutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryIdTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiHashOnlyTokenTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiInitializerTests|FullyQualifiedName~BatteryPassWeb.Tests.HelpWorkbenchLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.JsonSchemaValidationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.LegacyNextExposureTests|FullyQualifiedName~BatteryPassWeb.Tests.LocalAdminEditableFieldPolicyTests|FullyQualifiedName~BatteryPassWeb.Tests.ManualFeedbackRound3Tests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportCanonicalDataTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportEvidenceServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportPublishPolicyServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportReadinessServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportSoftwarePresentationTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportStatusLabelTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportTrustServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.PassportValidationServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.PublicVisibilityPolicyTests|FullyQualifiedName~BatteryPassWeb.Tests.QrWorkflowTests|FullyQualifiedName~BatteryPassWeb.Tests.SchemaRegistryServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.SearchRegexHardeningTests|FullyQualifiedName~BatteryPassWeb.Tests.SecurityLimitsTests|FullyQualifiedName~BatteryPassWeb.Tests.SigningWorkflowLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustDirtyStateTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustModelsTests|FullyQualifiedName~BatteryPassWeb.Tests.TrustWorkflowAcceptanceTests|FullyQualifiedName~BatteryPassWeb.Tests.ValidationPolicyLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:   152`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1979`, none failed.

- [ ] **Step 10: Commit**

```bash
git add BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AdminHelpPageTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/AntiForgeryProtectionTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/BatteryAdminWorkflowTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs BatteryPassWeb.Tests/BatteryRouteAndRegistryTests.cs BatteryPassWeb.Tests/ConformanceLayoutTests.cs BatteryPassWeb.Tests/CopyIdButtonLayoutTests.cs BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs BatteryPassWeb.Tests/Dpp/AppRolesTests.cs BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs BatteryPassWeb.Tests/ExternalApiBatteryIdTests.cs BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs BatteryPassWeb.Tests/ExternalApiHashOnlyTokenTests.cs BatteryPassWeb.Tests/HelpWorkbenchLayoutTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Phase6ADocumentationTests.cs BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs BatteryPassWeb.Tests/QrWorkflowTests.cs BatteryPassWeb.Tests/SecurityLimitsTests.cs BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs web/Controllers/ClusterAdminController.cs web/Models/ViewModels/ApplicationAuditLogViewModel.cs web/Services/AccessControlService.cs web/Services/ExternalApiInitializer.cs web/Configuration/BatteryPassOptions.cs web/Controllers/AdminController.cs web/Controllers/HelpController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Models/ViewModels/ExternalApiHelpViewModel.cs web/Program.cs web/Services/ExternalApiRepository.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/ApiTokens.cshtml web/Views/Help/Index.cshtml web/Views/Shared/_ClusterAdminTabs.cshtml web/appsettings.Development.json web/appsettings.json
git commit -m "feat(dpp): the old app's pages, services, sample data and tests go" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The old Next.js app goes

`web-next-legacy` was an older Next.js version of the app, never deployed with it (decision 3 above). It stays in the tag `app-v1.2-before-dpp-v3`.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs`
- Modify: `.ebignore`
- Delete: `web-next-legacy/.env.example`
- Delete: `web-next-legacy/.gitignore`
- Delete: `web-next-legacy/AGENTS.md`
- Delete: `web-next-legacy/CLAUDE.md`
- Delete: `web-next-legacy/README.md`
- Delete: `web-next-legacy/eslint.config.mjs`
- Delete: `web-next-legacy/next.config.ts`
- Delete: `web-next-legacy/package-lock.json`
- Delete: `web-next-legacy/package.json`
- Delete: `web-next-legacy/postcss.config.mjs`
- Delete: `web-next-legacy/public/file.svg`
- Delete: `web-next-legacy/public/globe.svg`
- Delete: `web-next-legacy/public/next.svg`
- Delete: `web-next-legacy/public/sample-battery.png`
- Delete: `web-next-legacy/public/vercel.svg`
- Delete: `web-next-legacy/public/window.svg`
- Delete: `web-next-legacy/src/app/[passportId]/page.tsx`
- Delete: `web-next-legacy/src/app/[passportId]/summary/page.tsx`
- Delete: `web-next-legacy/src/app/admin/clusters/actions.ts`
- Delete: `web-next-legacy/src/app/admin/clusters/page.tsx`
- Delete: `web-next-legacy/src/app/admin/page.tsx`
- Delete: `web-next-legacy/src/app/admin/passports/[passportId]/edit/page.tsx`
- Delete: `web-next-legacy/src/app/admin/passports/actions.ts`
- Delete: `web-next-legacy/src/app/admin/passports/new/page.tsx`
- Delete: `web-next-legacy/src/app/admin/passports/page.tsx`
- Delete: `web-next-legacy/src/app/api/auth/login/route.ts`
- Delete: `web-next-legacy/src/app/api/auth/logout/route.ts`
- Delete: `web-next-legacy/src/app/api/files/[fileId]/route.ts`
- Delete: `web-next-legacy/src/app/api/files/route.ts`
- Delete: `web-next-legacy/src/app/api/passports/[passportId]/route.ts`
- Delete: `web-next-legacy/src/app/api/passports/route.ts`
- Delete: `web-next-legacy/src/app/cluster-admin/page.tsx`
- Delete: `web-next-legacy/src/app/cluster-admin/passports/[passportId]/edit/page.tsx`
- Delete: `web-next-legacy/src/app/cluster-admin/passports/actions.ts`
- Delete: `web-next-legacy/src/app/cluster-admin/passports/page.tsx`
- Delete: `web-next-legacy/src/app/cluster-admin/users/actions.ts`
- Delete: `web-next-legacy/src/app/cluster-admin/users/page.tsx`
- Delete: `web-next-legacy/src/app/favicon.ico`
- Delete: `web-next-legacy/src/app/globals.css`
- Delete: `web-next-legacy/src/app/layout.tsx`
- Delete: `web-next-legacy/src/app/login/page.tsx`
- Delete: `web-next-legacy/src/app/page.tsx`
- Delete: `web-next-legacy/src/app/registry/page.tsx`
- Delete: `web-next-legacy/src/app/search/page.tsx`
- Delete: `web-next-legacy/src/components/admin/section-editor.tsx`
- Delete: `web-next-legacy/src/components/app-shell.tsx`
- Delete: `web-next-legacy/src/components/auth/login-form.tsx`
- Delete: `web-next-legacy/src/components/charts/donut-chart.tsx`
- Delete: `web-next-legacy/src/components/charts/pie-chart.tsx`
- Delete: `web-next-legacy/src/components/cluster-admin/cluster-passport-editor.tsx`
- Delete: `web-next-legacy/src/components/passport/passport-card.tsx`
- Delete: `web-next-legacy/src/components/passport/passport-detail-report.tsx`
- Delete: `web-next-legacy/src/components/passport/passport-summary-report.tsx`
- Delete: `web-next-legacy/src/components/passport/section-nav.tsx`
- Delete: `web-next-legacy/src/components/passport/verification-badge.tsx`
- Delete: `web-next-legacy/src/components/registry/registry-table.tsx`
- Delete: `web-next-legacy/src/components/theme/theme-provider.tsx`
- Delete: `web-next-legacy/src/components/theme/theme-toggle.tsx`
- Delete: `web-next-legacy/src/components/ui/button.tsx`
- Delete: `web-next-legacy/src/components/ui/card.tsx`
- Delete: `web-next-legacy/src/components/ui/input.tsx`
- Delete: `web-next-legacy/src/components/ui/status-pill.tsx`
- Delete: `web-next-legacy/src/lib/admin/cluster-passport-form.ts`
- Delete: `web-next-legacy/src/lib/admin/passport-form.ts`
- Delete: `web-next-legacy/src/lib/auth/cluster-access.ts`
- Delete: `web-next-legacy/src/lib/auth/cluster-guards.ts`
- Delete: `web-next-legacy/src/lib/auth/demo-admin.ts`
- Delete: `web-next-legacy/src/lib/auth/header-state.ts`
- Delete: `web-next-legacy/src/lib/auth/login-redirect.ts`
- Delete: `web-next-legacy/src/lib/auth/session.ts`
- Delete: `web-next-legacy/src/lib/auth/users.ts`
- Delete: `web-next-legacy/src/lib/crypto/canonicalize.ts`
- Delete: `web-next-legacy/src/lib/crypto/signing.ts`
- Delete: `web-next-legacy/src/lib/db/client.ts`
- Delete: `web-next-legacy/src/lib/db/clusters.ts`
- Delete: `web-next-legacy/src/lib/db/collections.ts`
- Delete: `web-next-legacy/src/lib/db/files.ts`
- Delete: `web-next-legacy/src/lib/db/mongo-errors.ts`
- Delete: `web-next-legacy/src/lib/db/passports.ts`
- Delete: `web-next-legacy/src/lib/env/load-app-env.ts`
- Delete: `web-next-legacy/src/lib/files/demo-pdf.ts`
- Delete: `web-next-legacy/src/lib/registry/search-routing.ts`
- Delete: `web-next-legacy/src/lib/schemas/registry.ts`
- Delete: `web-next-legacy/src/lib/seed/indexes.ts`
- Delete: `web-next-legacy/src/lib/seed/sample-clusters.ts`
- Delete: `web-next-legacy/src/lib/seed/sample-passport.ts`
- Delete: `web-next-legacy/src/lib/seed/seed.ts`
- Delete: `web-next-legacy/src/lib/view-model/passport-view-model.ts`
- Delete: `web-next-legacy/src/types/passport.ts`
- Delete: `web-next-legacy/tests/admin-clusters-page-source.test.ts`
- Delete: `web-next-legacy/tests/canonicalize.test.ts`
- Delete: `web-next-legacy/tests/cluster-access.test.ts`
- Delete: `web-next-legacy/tests/cluster-admin-navigation.test.ts`
- Delete: `web-next-legacy/tests/cluster-local-admin-form.test.ts`
- Delete: `web-next-legacy/tests/cluster-route-guards.test.ts`
- Delete: `web-next-legacy/tests/cluster-seed-data.test.ts`
- Delete: `web-next-legacy/tests/demo-admin-fallback.test.ts`
- Delete: `web-next-legacy/tests/demo-pdf.test.ts`
- Delete: `web-next-legacy/tests/dev-server-safety.test.ts`
- Delete: `web-next-legacy/tests/file-storage.test.ts`
- Delete: `web-next-legacy/tests/header-state.test.ts`
- Delete: `web-next-legacy/tests/load-app-env.test.ts`
- Delete: `web-next-legacy/tests/login-return-redirect.test.ts`
- Delete: `web-next-legacy/tests/passport-db-no-runtime-fallback.test.ts`
- Delete: `web-next-legacy/tests/passport-detail-tabs.test.tsx`
- Delete: `web-next-legacy/tests/passport-form.test.ts`
- Delete: `web-next-legacy/tests/passport-summary-report.test.tsx`
- Delete: `web-next-legacy/tests/passport-view-model.test.ts`
- Delete: `web-next-legacy/tests/registry-admin-access.test.ts`
- Delete: `web-next-legacy/tests/registry-search-routing.test.ts`
- Delete: `web-next-legacy/tests/sample-passport-rich.test.ts`
- Delete: `web-next-legacy/tests/sample-passport-schema-validation.test.ts`
- Delete: `web-next-legacy/tests/schema-registry.test.ts`
- Delete: `web-next-legacy/tests/seed-indexes.test.ts`
- Delete: `web-next-legacy/tests/server-action-forms.test.ts`
- Delete: `web-next-legacy/tests/signing.test.ts`
- Delete: `web-next-legacy/tsconfig.json`

- [ ] **Step 1: Write the failing tests**

Patch `3d1-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs`). Save the block as `../3d1-task2-tests.patch`, outside the worktree, and run `git apply ../3d1-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs b/BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs
--- a/BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs
@@ -49,6 +49,13 @@ public sealed class OldAppRemovedTests(MongoFixture mongo)
         Assert.False(File.Exists(Path.Combine([RepoRoot, .. path.Split('/')])), $"{path} should be gone");
     }
 
+    [Fact]
+    public void TheOldNextJsApp_ShouldBeGone()
+    {
+        Assert.False(Directory.Exists(Path.Combine(RepoRoot, "web-next-legacy")));
+        Assert.DoesNotContain("web-next-legacy", File.ReadAllText(Path.Combine(RepoRoot, ".ebignore")), StringComparison.Ordinal);
+    }
+
     [Fact]
     public void TheV12DataModel_ShouldStayInTheRepositoryAsReferenceButNotBeRead()
     {
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.OldAppRemovedTests"`
Expected: `Failed:     1, Passed:    32`: `TheOldNextJsApp_ShouldBeGone`, since the folder is still there.

- [ ] **Step 3: Stop naming the folder in the deployment bundle rules**

Patch `3d1-task2-code.patch` (`.ebignore`). Save the block as `../3d1-task2-code.patch`, outside the worktree, and run `git apply ../3d1-task2-code.patch` from the worktree root:

```diff
diff --git a/.ebignore b/.ebignore
--- a/.ebignore
+++ b/.ebignore
@@ -1,5 +1,4 @@
 # Elastic Beanstalk source bundle guardrails
-web-next-legacy/
 **/node_modules/
 **/.next/
 
```

- [ ] **Step 4: Delete the old files**

```bash
git rm -rq web-next-legacy
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.OldAppRemovedTests"`
Expected: `Passed!  - Failed:     0, Passed:    33`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1980`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/OldAppRemovedTests.cs .ebignore
git commit -m "chore(dpp): the old Next.js app goes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  1980, Skipped:     0, Total:  1980`: the 2158 from before, 179 fewer after Task 1 (the tests of the removed code go, the new checks come), then 1 in Task 2.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Look at the pages**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too, and sign in as a team member. Never point it at `battery_pass_demo`. Check, in light and dark theme:

1. **The team's pages** open as before: Batteries, Battery families, Sites and operators, Registered clusters, Users, API Token Management, Audit log, Help.
2. **Cluster admin** (the header link): it opens on Users; the tabs are Users and API token management. `/cluster-admin/passports` opens the Registry.
3. **Help, API:** the toolbar has no sample tokens; the workbench's "Token value" starts empty. Paste a token from API Token Management and send "Read full battery" for a published battery: the answer is 200.
4. **Old addresses** answer "not found": `/cluster-admin/passports/<anything>/edit` and `/api/files/<anything>`.
5. **A narrow window** (about 390 px): Cluster admin's Users and the API help page don't scroll sideways.

## After this plan

- **3d-2** resets `battery_pass_v3` after the product owner has reviewed the accounts: the old data, the new test data and the four sample tokens go; clusters, real API tokens and the audit log stay. It is a script the product owner runs, tried first on a throwaway database, and asked about again before it runs.
- **3e** gives the admin, cluster admin and help pages a cleaner look, with mock-ups first, and makes the help one place that shows each reader only the parts for their role.
- **Known small gaps:**
  - `docs/end-user-testing-guide.md`, `docs/qa-test-pack.md` and `docs/sample-cluster-test-accounts.md` still describe the old app; their rewrite is step 6.
  - The API help page's examples still name the cluster `demo-cluster`, the old sample cluster, which stays until 3d-2.

