# DPP V3 Step 3a: Who Sees What — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The app knows who is reading and what they may see. The single "legitimate interest" role becomes the three of decision 6; sign-ins and API tokens become readers; one filter cuts a passport down for a reader; and the two addresses every passport already links to work: the access-checked file download and the battery's accident page.

**Architecture:** Builds on 2c's audience rule (`AudienceRules.MaySee`) and 2d's team pages. `AccessControlService` and the Users pages get the three LI roles. `DppReaders` turns a sign-in (its role, and its cluster memberships read on each request) or an API token into a `Reader`. `PassportFilter` builds a new passport from the catalogue's attributes the reader may see; 3b's pages and 3c's API show passports only through it. `DocumentService` keeps the files of unpublished records with the team (J-6). `DppController` serves `/dpp/documents/{id}` and `/dpp/packs/{packId}/accidents`; `ReaderPages` loads the accident page, and the team's battery and editor pages link to both.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0. Plain CSS from `site.css`; nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 5, 9, 12 and the 3a bullet of 14) and the decisions record (decisions 6, 20, 23, 28). Earlier plans: steps 1, 2a–2c, 2d-1 and 2d-2 in `docs/superpowers/plans/`; 2c's "After this plan" lists what the download needs.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 9e86bc8 (2d-2, E1–E4 and the step 3 split with its API decisions) plus the commit that adds this plan. Check with `git branch --show-current`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  1951`. After this plan: 2016. The suite takes about a minute.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3a-task1-code.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **No data changes:** this plan changes no stored data. Accounts that have the old role keep it until the product owner reviews them in 3d.

## Decisions taken with the product owner (29 Oct 2026)

1. **Step 3 in four parts** (design 14): 3a who sees what (this plan), 3b the public pages, 3c the API, 3d the switch-over and the reset.
2. **The accident page** looks as in the mock-up `docs/mockups/dpp-v3-accident-page.html`, approved as shown: the battery's card, then its reports newest first, each title opening the file; "No accidents reported" otherwise; a short "Not available" page for people who may not see it. Who uploaded a report isn't shown there.
3. **Accounts with the old "Person with legitimate interest" role** see public data only until the team gives them one of the three new roles. The product owner reviews every account in 3d and says which to keep.
4. **The external API stays** and is rebuilt on the new model in 3c; this plan only maps its tokens to readers.
5. **The look and behaviour** of the prototype (the accident page, the refusals, the links on the team pages, the three roles on the Users page) were approved as shown.

## Judgement calls in this plan

1. **The three roles** are stored as `liRepair`, `liRecycling` and `liTreatment`, and shown as "Legitimate interest: repair, repurposing, remanufacturing", "… recycling and battery treatment" and "… authorised or WEEE treatment". With the 6 other roles (`admin`, `clusterAdmin`, `member`, `notifiedBody`, `marketSurveillanceAuthority`, `commission`) they are the 9 app roles. The old `legitimateInterest` value is recognised nowhere: it gives nothing beyond public data, the Users page shows such an account as "Cluster Member", and saving it as it is makes it one. The old passport pages and APIs, which stay until 3b–3d, treat the three roles as they treated the old one.
2. **Readers** (design 5.2):
   - **A sign-in** reads with its app-wide role (team, notified body, market surveillance, Commission or one of the LI roles), plus cluster user or cluster admin of each cluster it is a member of. The memberships are read on every request, so a membership an admin removes counts at once.
   - **An API token** for named clusters reads like a cluster user of those clusters. A global token is design 5.2's team token, issued only for Scania's own systems, so it reads like the team. What tokens may write comes with 3c (J-10).
3. **One filter:** `PassportFilter.Cut` builds a new passport from the catalogue's attributes the reader may see, at their own paths, and leaves everything else out, so nothing reaches a reader without an audience. E1–E4 are purchaser attributes in the catalogue, so they follow #87. Nothing in 3a shows a passport yet; the tests run the whole role matrix through the filter, and 3b and 3c use it.
4. **Nothing unpublished leaves the team (J-6):** a file of a model, batch or pack that no published passport uses yet is the team's only. Until now (2c) market surveillance, notified bodies, the Commission and the LI roles got such files too. A published battery without a cluster still serves the authorities and the LI roles; its cluster data waits for a cluster.
5. **The download:** the type stored at upload (read from the file's bytes), `Content-Disposition: inline` with the file's name, so PDFs and pictures open in the browser; `X-Content-Type-Options: nosniff`; `Cache-Control: private, no-store`, so restricted files stay out of shared caches.
6. **Refusals:** not signed in, the sign-in page opens and comes back afterwards. Signed in without access, or nothing at that address, a short "Not available" page with status 404, which doesn't say whether the battery or the file exists. Refused downloads are still logged as `dpp.document-denied` (2c).
7. **The accident page** uses today's passport cards from `site.css` (decision 23): the battery's serial number and ID (#11, #6), the test-identifier line where the IDs are test ones (design 10), and the reports with their date and file type and size. It has no link back: the team comes from the battery page, and 3b's pages link to it.
8. **The team's pages** link each accident report and the accident page on the battery page, and every uploaded file of the model and batch editors ("Open the file").

## File structure

| File | Responsibility |
|---|---|
| `web/Services/AccessControlService.cs`, `ClusterRepository.cs`, `ProductTemplateService.cs`, `web/Controllers/AdminController.cs` (modify) | The three LI roles in place of the old one |
| `web/Views/Admin/Clusters.cshtml`, `web/Views/ClusterAdmin/Users.cshtml` (modify) | The Users pages offer and name the three roles |
| `web/Dpp/Access/DppReaders.cs` | A sign-in or an API token as a `Reader` |
| `web/Dpp/Access/PassportFilter.cs` | A passport cut down to what a reader may see |
| `web/Dpp/Store/DppStore.cs`, `web/Dpp/Documents/DocumentService.cs` (modify) | The files of unpublished records stay with the team |
| `web/Dpp/Pages/ReaderPages.cs` | The accident page's data and the two addresses |
| `web/Controllers/DppController.cs`, `web/Views/Dpp/Accidents.cshtml`, `NotAvailable.cshtml` | `/dpp/documents/{id}` and `/dpp/packs/{packId}/accidents` |
| `web/Dpp/DppServiceCollectionExtensions.cs` (modify) | Registers `DppReaders` and `ReaderPages` |
| `web/Views/Batteries/Battery.cshtml`, `web/Views/ProductData/_EditorField.cshtml` (modify) | The team's pages link the files and the accident page |


---

### Task 1: The three legitimate-interest roles

The single "Person with legitimate interest" role becomes the three of decision 6 wherever roles are given, named or checked. Four older tests named the old role; they now name the three.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/AppRolesTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Services/ClusterRepository.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/Users.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3a-task1-tests.patch` (`BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs`). Save the block as `../3a-task1-tests.patch`, outside the worktree, and run `git apply ../3a-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -42,7 +42,9 @@ public sealed class AdminFeedbackImplementationTests
         Assert.Contains("[\"notifiedBody\"]", clustersView);
         Assert.Contains("[\"marketSurveillanceAuthority\"]", clustersView);
         Assert.Contains("[\"commission\"]", clustersView);
-        Assert.Contains("[\"legitimateInterest\"]", clustersView);
+        Assert.Contains("[\"liRepair\"]", clustersView);
+        Assert.Contains("[\"liRecycling\"]", clustersView);
+        Assert.Contains("[\"liTreatment\"]", clustersView);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -49,7 +49,9 @@ public sealed class AdminTestFeedbackFixesTests
         Assert.Contains("Notified Body", adminUsers);
         Assert.Contains("Market Surveillance Authorities", adminUsers);
         Assert.Contains("Commission", adminUsers);
-        Assert.Contains("Person with Legitimate Interest", adminUsers);
+        Assert.Contains("Legitimate interest: repair, repurposing, remanufacturing", adminUsers);
+        Assert.Contains("Legitimate interest: recycling and battery treatment", adminUsers);
+        Assert.Contains("Legitimate interest: authorised or WEEE treatment", adminUsers);
         Assert.Contains("Notified Body", clusterUsers);
         Assert.Contains("NormalizeClusterMembershipRole", clusterRepository);
         Assert.Contains("CreateIndexOptions { Unique = true", clusterRepository);
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -117,7 +117,7 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.Contains("Notified Body", source);
         Assert.Contains("Market Surveillance Authorities", source);
         Assert.Contains("Commission", source);
-        Assert.Contains("Person with Legitimate Interest", source);
+        Assert.Contains("Legitimate interest: repair, repurposing, remanufacturing", source);
         Assert.Contains("CanViewTrustConformanceAsync", source);
         Assert.Contains("marketSurveillanceAuthority", source);
         Assert.Contains("commission", source);
diff --git a/BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs b/BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs
--- a/BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs
+++ b/BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs
@@ -28,7 +28,9 @@ public sealed class PublicVisibilityPolicyTests
         Assert.Contains("RoleNotifiedBody", source);
         Assert.Contains("RoleMarketSurveillanceAuthority", source);
         Assert.Contains("RoleCommission", source);
-        Assert.Contains("RoleLegitimateInterest", source);
+        Assert.Contains("RoleLiRepair", source);
+        Assert.Contains("RoleLiRecycling", source);
+        Assert.Contains("RoleLiTreatment", source);
         Assert.Contains("CanSeeDraftPassports", source);
         Assert.Contains("CanSeeSignedPassports", source);
         Assert.Contains("HasGlobalReportReadRole", source);
```

`BatteryPassWeb.Tests/Dpp/AppRolesTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The 9 app roles of step 3 (design 5.2): today's 7, with "legitimateInterest" split into the three LI roles of decision 6.</summary>
public sealed class AppRolesTests
{
    private static readonly string[] LiRoles = ["liRepair", "liRecycling", "liTreatment"];

    private static string View(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. path]));

    private static IReadOnlyList<string> Keys(string view, string dictionary)
    {
        var start = view.IndexOf($"{dictionary} = new", StringComparison.Ordinal);
        var end = view.IndexOf("};", start, StringComparison.Ordinal);
        return Regex.Matches(view[start..end], @"\[""(\w+)""\] =").Select(match => match.Groups[1].Value).ToArray();
    }

    [Fact]
    public void TheUsersPage_ShouldOfferTheNineRoles()
    {
        var users = View("Admin", "Clusters.cshtml");

        Assert.Equal(
            ["admin", "notifiedBody", "marketSurveillanceAuthority", "commission", "liRepair", "liRecycling", "liTreatment", "member"],
            Keys(users, "globalAccessOptions"));
        Assert.Equal(["clusterAdmin", "member"], Keys(users, "clusterMembershipRoleOptions"));
        Assert.DoesNotContain("legitimateInterest", users, StringComparison.Ordinal);
        Assert.DoesNotContain("Person with Legitimate Interest", users, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLabel_ShouldNameTheRoleTheSameWay()
    {
        var users = View("Admin", "Clusters.cshtml");
        var clusterUsers = View("ClusterAdmin", "Users.cshtml");

        foreach (var role in LiRoles)
        {
            var label = AccessControlService.DisplayRoleLabel(role);
            Assert.StartsWith("Legitimate interest: ", label, StringComparison.Ordinal);
            Assert.Contains($"[\"{role}\"] = \"{label}\"", users, StringComparison.Ordinal);
            Assert.Contains($"\"{label}\" => \"{role}\"", users, StringComparison.Ordinal);
            Assert.Contains($"\"{role}\" => \"{label}\"", clusterUsers, StringComparison.Ordinal);
            Assert.Equal(label, AccessControlService.DisplayRoleLabel(User(role)));
        }

        Assert.Equal(3, LiRoles.Select(AccessControlService.DisplayRoleLabel).Distinct().Count());
        Assert.DoesNotContain("legitimateInterest", clusterUsers, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("liRepair", "liRepair")]
    [InlineData("liRecycling", "liRecycling")]
    [InlineData("liTreatment", "liTreatment")]
    [InlineData("legitimateInterest", "member")]
    public void SavingAUser_ShouldKeepTheThreeRolesAndDropTheOldOne(string given, string stored)
    {
        var normalizeSystemRole = typeof(AdminController).GetMethod("NormalizeSystemRole", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(stored, normalizeSystemRole.Invoke(null, [given]));
        Assert.Equal(stored, ClusterRepository.NormalizeClusterMembershipRole(given));
    }

    [Fact]
    public void TheOldRole_ShouldGiveNothingBeyondPublicData()
    {
        var old = User("legitimateInterest");

        Assert.False(AccessControlService.HasLegitimateInterestRole(old));
        Assert.False(AccessControlService.HasGlobalReportReadRole(old));
        Assert.False(AccessControlService.CanSeeSignedPassports(old));
        Assert.Equal("Cluster Member", AccessControlService.DisplayRoleLabel(old));
        Assert.All(LiRoles, role => Assert.True(AccessControlService.HasGlobalReportReadRole(User(role))));
    }

    private static ClaimsPrincipal User(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, "someone@example.com"), new Claim(ClaimTypes.Role, role)], "test"));
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AppRolesTests|FullyQualifiedName~BatteryPassWeb.Tests.PublicVisibilityPolicyTests"`
Expected: the build fails: `AccessControlService` has no `HasLegitimateInterestRole` (CS0117).

- [ ] **Step 3: Apply the roles patch**

Patch `3a-task1-code.patch` (`web/Controllers/AdminController.cs`, `web/Services/AccessControlService.cs`, `web/Services/ClusterRepository.cs`, `web/Services/ProductTemplateService.cs`, `web/Views/Admin/Clusters.cshtml`, `web/Views/ClusterAdmin/Users.cshtml`). Save the block as `../3a-task1-code.patch`, outside the worktree, and run `git apply ../3a-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -900,7 +900,9 @@ public class AdminController : Controller
             AccessControlService.RoleNotifiedBody => AccessControlService.RoleNotifiedBody,
             AccessControlService.RoleMarketSurveillanceAuthority => AccessControlService.RoleMarketSurveillanceAuthority,
             AccessControlService.RoleCommission => AccessControlService.RoleCommission,
-            AccessControlService.RoleLegitimateInterest => AccessControlService.RoleLegitimateInterest,
+            AccessControlService.RoleLiRepair => AccessControlService.RoleLiRepair,
+            AccessControlService.RoleLiRecycling => AccessControlService.RoleLiRecycling,
+            AccessControlService.RoleLiTreatment => AccessControlService.RoleLiTreatment,
             _ => AccessControlService.RoleNormalUser
         };
     }
diff --git a/web/Services/AccessControlService.cs b/web/Services/AccessControlService.cs
--- a/web/Services/AccessControlService.cs
+++ b/web/Services/AccessControlService.cs
@@ -11,7 +11,15 @@ public sealed class AccessControlService
     public const string RoleNotifiedBody = "notifiedBody";
     public const string RoleMarketSurveillanceAuthority = "marketSurveillanceAuthority";
     public const string RoleCommission = "commission";
-    public const string RoleLegitimateInterest = "legitimateInterest";
+    public const string RoleLiRepair = "liRepair";
+    public const string RoleLiRecycling = "liRecycling";
+    public const string RoleLiTreatment = "liTreatment";
+
+    /// <summary>
+    /// The three legitimate-interest roles of decision 6 (design 5.2), in place of the single "legitimateInterest" role,
+    /// which no longer gives anything beyond public data.
+    /// </summary>
+    public static IReadOnlyList<string> LegitimateInterestRoles { get; } = [RoleLiRepair, RoleLiRecycling, RoleLiTreatment];
 
     private readonly ClusterRepository _clusterRepository;
 
@@ -24,6 +32,8 @@ public sealed class AccessControlService
 
     public static bool IsClusterAdmin(ClaimsPrincipal user) => user.IsInRole(RoleClusterAdmin) || user.IsInRole(RoleAdmin);
 
+    public static bool HasLegitimateInterestRole(ClaimsPrincipal user) => LegitimateInterestRoles.Any(user.IsInRole);
+
     public static string DisplayRoleLabel(ClaimsPrincipal user)
     {
         if (user.IsInRole(RoleAdmin)) return "Global Admin";
@@ -31,7 +41,9 @@ public sealed class AccessControlService
         if (user.IsInRole(RoleCommission)) return "Commission";
         if (user.IsInRole(RoleMarketSurveillanceAuthority)) return "Market Surveillance Authorities";
         if (user.IsInRole(RoleNotifiedBody)) return "Notified Body";
-        if (user.IsInRole(RoleLegitimateInterest)) return "Person with Legitimate Interest";
+        if (user.IsInRole(RoleLiRepair)) return "Legitimate interest: repair, repurposing, remanufacturing";
+        if (user.IsInRole(RoleLiRecycling)) return "Legitimate interest: recycling and battery treatment";
+        if (user.IsInRole(RoleLiTreatment)) return "Legitimate interest: authorised or WEEE treatment";
         if (user.IsInRole(RoleNormalUser)) return "Normal User";
         return "Cluster Member";
     }
@@ -45,7 +57,9 @@ public sealed class AccessControlService
             RoleCommission => "Commission",
             RoleMarketSurveillanceAuthority => "Market Surveillance Authorities",
             RoleNotifiedBody => "Notified Body",
-            RoleLegitimateInterest => "Person with Legitimate Interest",
+            RoleLiRepair => "Legitimate interest: repair, repurposing, remanufacturing",
+            RoleLiRecycling => "Legitimate interest: recycling and battery treatment",
+            RoleLiTreatment => "Legitimate interest: authorised or WEEE treatment",
             RoleNormalUser => "Normal User",
             _ => "Cluster Member"
         };
@@ -59,13 +73,13 @@ public sealed class AccessControlService
         || user.IsInRole(RoleNotifiedBody)
         || user.IsInRole(RoleMarketSurveillanceAuthority)
         || user.IsInRole(RoleCommission)
-        || user.IsInRole(RoleLegitimateInterest);
+        || HasLegitimateInterestRole(user);
 
     public static bool HasGlobalReportReadRole(ClaimsPrincipal user) =>
         user.IsInRole(RoleNotifiedBody)
         || user.IsInRole(RoleMarketSurveillanceAuthority)
         || user.IsInRole(RoleCommission)
-        || user.IsInRole(RoleLegitimateInterest);
+        || HasLegitimateInterestRole(user);
 
     public static bool HasAllClusterReadScope(ClaimsPrincipal user) =>
         IsAdmin(user) || HasGlobalReportReadRole(user);
diff --git a/web/Services/ClusterRepository.cs b/web/Services/ClusterRepository.cs
--- a/web/Services/ClusterRepository.cs
+++ b/web/Services/ClusterRepository.cs
@@ -322,7 +322,9 @@ public sealed class ClusterRepository
             AccessControlService.RoleNotifiedBody => AccessControlService.RoleNotifiedBody,
             AccessControlService.RoleMarketSurveillanceAuthority => AccessControlService.RoleMarketSurveillanceAuthority,
             AccessControlService.RoleCommission => AccessControlService.RoleCommission,
-            AccessControlService.RoleLegitimateInterest => AccessControlService.RoleLegitimateInterest,
+            AccessControlService.RoleLiRepair => AccessControlService.RoleLiRepair,
+            AccessControlService.RoleLiRecycling => AccessControlService.RoleLiRecycling,
+            AccessControlService.RoleLiTreatment => AccessControlService.RoleLiTreatment,
             _ => AccessControlService.RoleNormalUser
         };
     }
diff --git a/web/Services/ProductTemplateService.cs b/web/Services/ProductTemplateService.cs
--- a/web/Services/ProductTemplateService.cs
+++ b/web/Services/ProductTemplateService.cs
@@ -780,7 +780,7 @@ public sealed class ProductTemplateService
         await _clusterRepository.UpsertUserAsync("notified.body@example.test", "Notified Body", [AccessControlService.RoleNotifiedBody], passwordHash, cancellationToken);
         await _clusterRepository.UpsertUserAsync("msa@example.test", "Market Surveillance Authorities", [AccessControlService.RoleMarketSurveillanceAuthority], passwordHash, cancellationToken);
         await _clusterRepository.UpsertUserAsync("commission@example.test", "Commission", [AccessControlService.RoleCommission], passwordHash, cancellationToken);
-        await _clusterRepository.UpsertUserAsync("legitimate.interest@example.test", "Person with Legitimate Interest", [AccessControlService.RoleLegitimateInterest], passwordHash, cancellationToken);
+        await _clusterRepository.UpsertUserAsync("legitimate.interest@example.test", "Legitimate interest: repair", [AccessControlService.RoleLiRepair], passwordHash, cancellationToken);
         foreach (var (clusterId, definition) in FixedClusters)
         {
             await _clusterRepository.UpsertClusterAsync(clusterId, definition.Name, cancellationToken);
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -12,7 +12,9 @@
         ["notifiedBody"] = "Notified Body",
         ["marketSurveillanceAuthority"] = "Market Surveillance Authorities",
         ["commission"] = "Commission",
-        ["legitimateInterest"] = "Person with Legitimate Interest",
+        ["liRepair"] = "Legitimate interest: repair, repurposing, remanufacturing",
+        ["liRecycling"] = "Legitimate interest: recycling and battery treatment",
+        ["liTreatment"] = "Legitimate interest: authorised or WEEE treatment",
         ["member"] = "Cluster Member"
     };
     var clusterMembershipRoleOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
@@ -34,7 +36,9 @@
         "notifiedBody" => "Notified Body",
         "marketSurveillanceAuthority" => "Market Surveillance Authorities",
         "commission" => "Commission",
-        "legitimateInterest" => "Person with Legitimate Interest",
+        "liRepair" => "Legitimate interest: repair, repurposing, remanufacturing",
+        "liRecycling" => "Legitimate interest: recycling and battery treatment",
+        "liTreatment" => "Legitimate interest: authorised or WEEE treatment",
         _ => clusterMembershipRoleOptions.TryGetValue(role, out var label) ? label : "Normal User"
     };
     string MembershipCountLabel(int count) => $"{count} {(count == 1 ? "membership" : "memberships")}";
@@ -80,7 +84,9 @@
             "Notified Body" => "notifiedBody",
             "Market Surveillance Authorities" => "marketSurveillanceAuthority",
             "Commission" => "commission",
-            "Person with Legitimate Interest" => "legitimateInterest",
+            "Legitimate interest: repair, repurposing, remanufacturing" => "liRepair",
+            "Legitimate interest: recycling and battery treatment" => "liRecycling",
+            "Legitimate interest: authorised or WEEE treatment" => "liTreatment",
             _ => "member"
         };
     }
diff --git a/web/Views/ClusterAdmin/Users.cshtml b/web/Views/ClusterAdmin/Users.cshtml
--- a/web/Views/ClusterAdmin/Users.cshtml
+++ b/web/Views/ClusterAdmin/Users.cshtml
@@ -14,7 +14,9 @@
         "notifiedBody" => "Notified Body",
         "marketSurveillanceAuthority" => "Market Surveillance Authorities",
         "commission" => "Commission",
-        "legitimateInterest" => "Person with Legitimate Interest",
+        "liRepair" => "Legitimate interest: repair, repurposing, remanufacturing",
+        "liRecycling" => "Legitimate interest: recycling and battery treatment",
+        "liTreatment" => "Legitimate interest: authorised or WEEE treatment",
         _ => clusterMembershipRoleOptions.TryGetValue(role, out var label) ? label : "Normal User"
     };
     string MembershipCountLabel(int count) => $"{count} {(count == 1 ? "membership" : "memberships")}";
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AppRolesTests|FullyQualifiedName~BatteryPassWeb.Tests.PublicVisibilityPolicyTests"`
Expected: `Passed!  - Failed:     0, Passed:    38`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1958`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs BatteryPassWeb.Tests/PublicVisibilityPolicyTests.cs BatteryPassWeb.Tests/Dpp/AppRolesTests.cs web/Controllers/AdminController.cs web/Services/AccessControlService.cs web/Services/ClusterRepository.cs web/Services/ProductTemplateService.cs web/Views/Admin/Clusters.cshtml web/Views/ClusterAdmin/Users.cshtml
git commit -m "feat(dpp): the three legitimate-interest roles in place of the single one" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: Readers, and the one visibility filter

`AudienceRulesTests` shares its role matrix, so the filter's test checks the same 11 kinds of reader against every attribute, E1–E4 included.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DppReadersTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/PassportFilterTests.cs`
- Create: `web/Dpp/Access/DppReaders.cs`
- Create: `web/Dpp/Access/PassportFilter.cs`

- [ ] **Step 1: Write the failing tests**

Patch `3a-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs`). Save the block as `../3a-task2-tests.patch`, outside the worktree, and run `git apply ../3a-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs b/BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs
--- a/BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs
@@ -6,9 +6,9 @@ namespace BatteryPassWeb.Tests.Dpp;
 /// <summary>Design 5.2's role matrix, over every attribute.</summary>
 public sealed class AudienceRulesTests
 {
-    private const string OwnCluster = "cluster-test";
+    internal const string OwnCluster = "cluster-test";
 
-    private static readonly string[] ReaderKinds =
+    internal static readonly string[] ReaderKinds =
     [
         "anonymous", "own-cluster-user", "own-cluster-admin", "other-cluster", "team", "notified-body",
         "market-surveillance", "commission", "li-repair", "li-recycling", "li-treatment",
@@ -61,7 +61,7 @@ public sealed class AudienceRulesTests
     }
 
     // Design 5.2 written out per reader; the shared rows by attribute number, from design 5.1.
-    private static bool Expected(string kind, AttributeDefinition attribute) => kind switch
+    internal static bool Expected(string kind, AttributeDefinition attribute) => kind switch
     {
         "team" or "market-surveillance" => true,
         "anonymous" or "other-cluster" => attribute.Audience == Audience.Public,
@@ -74,7 +74,7 @@ public sealed class AudienceRulesTests
         _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
     };
 
-    private static Reader ReaderFor(string kind) => kind switch
+    internal static Reader ReaderFor(string kind) => kind switch
     {
         "anonymous" => Reader.Anonymous,
         "own-cluster-user" => Reader.With("user@example.com", ReaderRole.ClusterUser, OwnCluster),
```

`BatteryPassWeb.Tests/Dpp/DppReadersTests.cs`:

```csharp
using System.Security.Claims;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Sign-ins and API tokens as readers (design 5.2): the app-wide role, and the clusters from the memberships.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppReadersTests(MongoFixture mongo)
{
    [Fact]
    public async Task NotSignedIn_ShouldBeAnonymous()
    {
        var reader = await Readers().ForAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Same(Reader.Anonymous, reader);
    }

    [Theory]
    [InlineData("admin", ReaderRole.Team)]
    [InlineData("notifiedBody", ReaderRole.NotifiedBody)]
    [InlineData("marketSurveillanceAuthority", ReaderRole.MarketSurveillance)]
    [InlineData("commission", ReaderRole.Commission)]
    [InlineData("liRepair", ReaderRole.LiRepair)]
    [InlineData("liRecycling", ReaderRole.LiRecycling)]
    [InlineData("liTreatment", ReaderRole.LiTreatment)]
    public async Task AnAppWideRole_ShouldBeTheReadersRole(string role, ReaderRole expected)
    {
        var reader = await Readers().ForAsync(User("Someone@Example.com", role));

        Assert.Equal([expected], reader.Roles);
        Assert.Empty(reader.ClusterIds);
        Assert.Equal("someone@example.com", reader.Who);
    }

    [Theory]
    [InlineData("legitimateInterest")]
    [InlineData("member")]
    [InlineData("viewer")]
    public async Task AnyOtherRoleWithoutMemberships_ShouldSeePublicDataOnly(string role)
    {
        var reader = await Readers().ForAsync(User("someone@example.com", role));

        Assert.Empty(reader.Roles);
        Assert.All(AttributeCatalog.All, attribute =>
            Assert.Equal(attribute.Audience == Audience.Public, AudienceRules.MaySee(reader, attribute, ["cluster-test"])));
    }

    [Fact]
    public async Task Memberships_ShouldMakeAClusterUserOrAdminOfEachCluster()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("user@example.com", "Cluster-North", "member");
        await clusters.UpsertClusterMembershipAsync("user@example.com", "cluster-south", "clusterAdmin");

        var reader = await new DppReaders(clusters).ForAsync(User("user@example.com", "clusterAdmin"));

        Assert.Equal([ReaderRole.ClusterUser, ReaderRole.ClusterAdmin], reader.Roles.Order());
        Assert.Equal(["cluster-north", "cluster-south"], reader.ClusterIds.Order());
        Assert.True(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), ["cluster-north"]));
        Assert.False(AudienceRules.MaySee(reader, AttributeCatalog.ByNumber(63), ["cluster-west"]));
    }

    [Fact]
    public async Task ARemovedMembership_ShouldCountAtOnce()
    {
        var clusters = new ClusterRepository(mongo.NewContext());
        await clusters.UpsertClusterMembershipAsync("user@example.com", "cluster-test", "member");
        var readers = new DppReaders(clusters);
        Assert.Equal(["cluster-test"], (await readers.ForAsync(User("user@example.com", "member"))).ClusterIds);

        await clusters.DeleteClusterMembershipAsync("user@example.com", "cluster-test");

        var after = await readers.ForAsync(User("user@example.com", "member"));
        Assert.Empty(after.ClusterIds);
        Assert.Empty(after.Roles);
    }

    [Fact]
    public void AClusterToken_ShouldReadLikeAClusterUserOfItsClusters()
    {
        var reader = DppReaders.For(new ExternalApiTokenContext { TokenId = "token-1", ClusterIds = ["Cluster-North", "cluster-south"] });

        Assert.Equal([ReaderRole.ClusterUser], reader.Roles);
        Assert.Equal(["cluster-north", "cluster-south"], reader.ClusterIds.Order());
        Assert.Equal("api-token:token-1", reader.Who);
    }

    [Fact]
    public void AGlobalToken_ShouldReadLikeTheTeam()
    {
        var reader = DppReaders.For(new ExternalApiTokenContext { TokenId = "token-2", GlobalAccess = true });

        Assert.Equal([ReaderRole.Team], reader.Roles);
    }

    private DppReaders Readers() => new(new ClusterRepository(mongo.NewContext()));

    private static ClaimsPrincipal User(string email, string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test"));
}
```

`BatteryPassWeb.Tests/Dpp/PassportFilterTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The one visibility filter (design 4 and 5.2): the whole passport, E1–E4 included, cut for every kind of reader.</summary>
public sealed class PassportFilterTests
{
    private const string OwnCluster = AudienceRulesTests.OwnCluster;

    public static TheoryData<string> EveryReader() => new(AudienceRulesTests.ReaderKinds);

    [Theory]
    [MemberData(nameof(EveryReader))]
    public void Cut_ShouldKeepExactlyWhatTheRoleMatrixGivesTheReader(string kind)
    {
        var passport = FullPassport();

        var cut = PassportFilter.Cut(passport, AudienceRulesTests.ReaderFor(kind), [OwnCluster]);

        var expected = AttributeCatalog.All.Where(attribute => AudienceRulesTests.Expected(kind, attribute)).Select(attribute => attribute.Path);
        Assert.Equal(expected.Order(), Paths(cut).Order());
    }

    [Fact]
    public void TheWholePassport_ShouldHoldEveryAttribute()
    {
        Assert.Equal(AttributeCatalog.All.Select(attribute => attribute.Path).Order(), Paths(FullPassport()).Order());
    }

    [Fact]
    public void Cut_ShouldCopyTheValuesAndLeaveThePassportAsItWas()
    {
        var passport = FullPassport();
        var before = passport.DeepClone();

        var team = PassportFilter.Cut(passport, AudienceRulesTests.ReaderFor("team"), [OwnCluster]);
        var anonymous = PassportFilter.Cut(passport, Reader.Anonymous, [OwnCluster]);
        team["dppHeader"]!["uniqueProductIdentifier"] = "changed";

        Assert.True(JsonNode.DeepEquals(before, passport));
        Assert.Equal(passport["dppHeader"]!["digitalProductPassportId"]!.GetValue<string>(), anonymous["dppHeader"]!["digitalProductPassportId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(passport["batteryPassportData"]!["identifiersAndProductData"]!["manufacturerInformation"], anonymous["batteryPassportData"]!["identifiersAndProductData"]!["manufacturerInformation"]));
    }

    [Fact]
    public void AnythingOutsideTheCatalogue_ShouldBeLeftOutEvenForTheTeam()
    {
        var passport = FullPassport();
        passport["notInTheFile"] = "x";
        passport["batteryPassportData"]!["performanceAndDurability"]!["remainingCapacity"] = 80;

        var cut = PassportFilter.Cut(passport, AudienceRulesTests.ReaderFor("team"), [OwnCluster]);

        Assert.Null(cut["notInTheFile"]);
        Assert.False(cut["batteryPassportData"]!["performanceAndDurability"]!.AsObject().ContainsKey("remainingCapacity"));
    }

    [Theory]
    [InlineData("own-cluster-user")]
    [InlineData("own-cluster-admin")]
    public void ADraftOrAnotherClustersBattery_ShouldGiveAClusterPublicDataOnly(string kind)
    {
        var publicPaths = AttributeCatalog.All.Where(attribute => attribute.Audience == Audience.Public).Select(attribute => attribute.Path).Order();

        Assert.Equal(publicPaths, Paths(PassportFilter.Cut(FullPassport(), AudienceRulesTests.ReaderFor(kind), [])).Order());
        Assert.Equal(publicPaths, Paths(PassportFilter.Cut(FullPassport(), AudienceRulesTests.ReaderFor(kind), ["cluster-south"])).Order());
    }

    [Theory]
    [InlineData("own-cluster-user", true)]
    [InlineData("market-surveillance", true)]
    [InlineData("team", true)]
    [InlineData("other-cluster", false)]
    [InlineData("commission", false)]
    [InlineData("anonymous", false)]
    public void E1ToE4_ShouldFollowTemperature(string kind, bool sees)
    {
        var cut = PassportFilter.Cut(FullPassport(), AudienceRulesTests.ReaderFor(kind), [OwnCluster]);
        var performance = cut["batteryPassportData"]?["performanceAndDurability"]?.AsObject();

        Assert.Equal(sees, performance?.ContainsKey("temperatureInformation") == true);
        Assert.All(AttributeCatalog.Extensions, extension => Assert.Equal(sees, performance?.ContainsKey(extension.Property) == true));
    }

    /// <summary>The seed passport with E1–E4 added, so every attribute of the catalogue is present.</summary>
    private static JsonObject FullPassport()
    {
        var seed = DppTestSeed.Load();
        foreach (var extension in AttributeCatalog.Extensions)
        {
            seed.Sources.Pack.Values[extension.Property] = DppStoreTesting.Value(JsonValue.Create(3600));
        }

        return PassportAssembler.Assemble(seed.Sources, seed.Header);
    }

    /// <summary>The attribute paths in a passport: the header's properties, and each section's properties.</summary>
    private static IEnumerable<string> Paths(JsonObject passport)
    {
        foreach (var (name, node) in passport)
        {
            if (name == AttributeDefinition.HeaderSection && node is JsonObject header)
            {
                foreach (var (property, _) in header)
                {
                    yield return $"{name}.{property}";
                }
            }
            else if (name == AttributeDefinition.DataRoot && node is JsonObject data)
            {
                foreach (var (section, sectionNode) in data)
                {
                    foreach (var (property, _) in sectionNode!.AsObject())
                    {
                        yield return $"{name}.{section}.{property}";
                    }
                }
            }
            else
            {
                yield return name;
            }
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AudienceRulesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppReadersTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportFilterTests"`
Expected: the build fails because `DppReaders` doesn't exist yet (CS0246); `PassportFilter` is missing too.

- [ ] **Step 3: Write `DppReaders.cs`**

`web/Dpp/Access/DppReaders.cs`:

```csharp
using System.Security.Claims;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// Who is reading (design 5.2): a sign-in or an API token as a <see cref="Reader"/>, with its app-wide roles and the
/// clusters it belongs to. Pages, the API and document downloads all ask here, then apply <see cref="AudienceRules"/>.
/// </summary>
public sealed class DppReaders(ClusterRepository clusters)
{
    /// <summary>The app-wide roles the team gives on the Users page, as readers. Any other role gives nothing extra.</summary>
    public static IReadOnlyDictionary<string, ReaderRole> AppRoles { get; } = new Dictionary<string, ReaderRole>(StringComparer.OrdinalIgnoreCase)
    {
        [AccessControlService.RoleAdmin] = ReaderRole.Team,
        [AccessControlService.RoleNotifiedBody] = ReaderRole.NotifiedBody,
        [AccessControlService.RoleMarketSurveillanceAuthority] = ReaderRole.MarketSurveillance,
        [AccessControlService.RoleCommission] = ReaderRole.Commission,
        [AccessControlService.RoleLiRepair] = ReaderRole.LiRepair,
        [AccessControlService.RoleLiRecycling] = ReaderRole.LiRecycling,
        [AccessControlService.RoleLiTreatment] = ReaderRole.LiTreatment,
    };

    /// <summary>
    /// The signed-in user as a reader: their app-wide role, and a cluster user or cluster admin of each cluster they are
    /// a member of. Read from the memberships on every request, so a membership removed by an admin counts at once.
    /// </summary>
    public async Task<Reader> ForAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var email = AccessControlService.CurrentEmail(user).Trim().ToLowerInvariant();
        if (user.Identity?.IsAuthenticated != true || email.Length == 0)
        {
            return Reader.Anonymous;
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(claim => AppRoles.TryGetValue(claim.Value, out var role) ? role : (ReaderRole?)null)
            .OfType<ReaderRole>()
            .ToHashSet();
        var clusterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var membership in await clusters.GetClusterMembershipsForUserAsync(email, cancellationToken))
        {
            var clusterId = BsonHelpers.GetString(membership, "clusterId");
            if (clusterId.Length == 0)
            {
                continue;
            }

            clusterIds.Add(ClusterRepository.NormalizeClusterId(clusterId));
            roles.Add(BsonHelpers.GetString(membership, "role").Equals(AccessControlService.RoleClusterAdmin, StringComparison.OrdinalIgnoreCase)
                ? ReaderRole.ClusterAdmin
                : ReaderRole.ClusterUser);
        }

        return new Reader(roles, clusterIds, email);
    }

    /// <summary>
    /// An API token as a reader (design 5.2). A token for named clusters reads like a cluster user of those clusters. A
    /// global token is the team's token, issued only for Scania's own systems, so it reads like the team.
    /// </summary>
    public static Reader For(ExternalApiTokenContext token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var who = $"api-token:{token.TokenId}";
        return token.GlobalAccess
            ? Reader.With(who, ReaderRole.Team)
            : new Reader(
                new HashSet<ReaderRole> { ReaderRole.ClusterUser },
                token.ClusterIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(ClusterRepository.NormalizeClusterId).ToHashSet(StringComparer.Ordinal),
                who);
    }
}
```

- [ ] **Step 4: Write `PassportFilter.cs`**

`web/Dpp/Access/PassportFilter.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// The one visibility filter (design principle 4): a passport cut down to what a reader may see. Pages, the API and
/// JSON exports show a passport only through here.
/// </summary>
public static class PassportFilter
{
    /// <summary>
    /// A new passport holding only the catalogue's attributes the reader may see, at their own paths. Anything that isn't
    /// a catalogue attribute is left out, so nothing reaches a reader without an audience. The passport itself is not
    /// changed.
    /// </summary>
    /// <param name="batteryClusterIds">
    /// The battery's cluster when its passport is published, as <see cref="AudienceRules.MaySee"/> takes it; empty for a
    /// draft or a battery without a cluster.
    /// </param>
    public static JsonObject Cut(JsonObject passport, Reader reader, IReadOnlyCollection<string> batteryClusterIds)
    {
        ArgumentNullException.ThrowIfNull(passport);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(batteryClusterIds);
        var cut = new JsonObject();
        foreach (var attribute in AttributeCatalog.All)
        {
            if (!AudienceRules.MaySee(reader, attribute, batteryClusterIds))
            {
                continue;
            }

            var path = attribute.Path.Split('.');
            if (Find(passport, path[..^1]) is { } source && source.TryGetPropertyValue(path[^1], out var value))
            {
                Ensure(cut, path[..^1])[path[^1]] = value?.DeepClone();
            }
        }

        return cut;
    }

    private static JsonObject? Find(JsonObject root, string[] path)
    {
        var node = root;
        foreach (var name in path)
        {
            node = node[name] as JsonObject;
            if (node is null)
            {
                return null;
            }
        }

        return node;
    }

    private static JsonObject Ensure(JsonObject root, string[] path)
    {
        var node = root;
        foreach (var name in path)
        {
            if (node[name] is not JsonObject next)
            {
                next = new JsonObject();
                node[name] = next;
            }

            node = next;
        }

        return node;
    }
}
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AudienceRulesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppReadersTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PassportFilterTests"`
Expected: `Passed!  - Failed:     0, Passed:   886`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1995`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/AudienceRulesTests.cs BatteryPassWeb.Tests/Dpp/DppReadersTests.cs BatteryPassWeb.Tests/Dpp/PassportFilterTests.cs web/Dpp/Access/DppReaders.cs web/Dpp/Access/PassportFilter.cs
git commit -m "feat(dpp): sign-ins and API tokens as readers, and the one visibility filter" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: The files of unpublished records stay with the team

2c served a model's or batch's files to the authorities and the LI roles even before any passport used them. J-6 keeps everything unpublished with the team.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs`
- Modify: `web/Dpp/Documents/DocumentService.cs`
- Modify: `web/Dpp/Store/DppStore.cs`

- [ ] **Step 1: Write the failing tests**

Patch `3a-task3-tests.patch` (`BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs`, `BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs`). Save the block as `../3a-task3-tests.patch`, outside the worktree, and run `git apply ../3a-task3-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs b/BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs
--- a/BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs
@@ -105,6 +105,9 @@ public sealed class DocumentServiceTests(MongoFixture mongo)
     [InlineData("li-treatment", true, false)] // #45 isn't shared with treatment facilities
     [InlineData("own-cluster-user", true, true)]
     [InlineData("own-cluster-user", false, false)] // a draft pack gives its cluster nothing (J-6)
+    [InlineData("team", false, true)]
+    [InlineData("market-surveillance", false, false)] // nothing unpublished leaves the team (J-6)
+    [InlineData("li-repair", false, false)]
     [InlineData("other-cluster", true, false)]
     [InlineData("anonymous", true, false)]
     public async Task Read_ShouldServeTheFileOnlyToItsAudience(string kind, bool published, bool allowed)
@@ -123,6 +126,20 @@ public sealed class DocumentServiceTests(MongoFixture mongo)
         }
     }
 
+    [Theory]
+    [InlineData("notified-body", true)]
+    [InlineData("market-surveillance", true)]
+    [InlineData("own-cluster-user", false)]
+    public async Task APublishedBatteryWithoutACluster_ShouldServeItsBatchsTestReportsToTheAuthorities(string kind, bool allowed)
+    {
+        var rig = await PublicationTesting.PublishedRigAsync(mongo);
+        await rig.Store.SetPackClusterAsync(rig.PackId, null);
+        var service = Service(rig);
+        var uploaded = (await service.UploadAsync(PublicationTesting.Team, TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance", new DocumentUpload("tests.pdf", DocumentTesting.Pdf()))).Document!;
+
+        Assert.Equal(allowed, await service.ReadAsync(ReaderFor(kind), uploaded.Id) is not null);
+    }
+
     [Fact]
     public async Task RefusedRead_ShouldBeLogged()
     {
@@ -193,6 +210,7 @@ public sealed class DocumentServiceTests(MongoFixture mongo)
         "market-surveillance" => Reader.With("msa@example.com", ReaderRole.MarketSurveillance),
         "li-repair" => Reader.With("repair@example.com", ReaderRole.LiRepair),
         "li-treatment" => Reader.With("treatment@example.com", ReaderRole.LiTreatment),
+        "notified-body" => Reader.With("nb@example.com", ReaderRole.NotifiedBody),
         "own-cluster-user" => Reader.With("user@example.com", ReaderRole.ClusterUser, "cluster-test"),
         "other-cluster" => Reader.With("other@example.com", ReaderRole.ClusterUser, "another-cluster"),
         _ => Reader.Anonymous,
diff --git a/BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs b/BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs
--- a/BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs
@@ -24,4 +24,19 @@ public sealed class PublishedClusterStoreTests(MongoFixture mongo)
         Assert.Equal(["cluster-test"], await rig.Store.FindPublishedClusterIdsUsingAsync(batchId: "batch-test-2026"));
         Assert.Empty(await rig.Store.FindPublishedClusterIdsUsingAsync(modelId: "no-such-model"));
     }
+
+    [Fact]
+    public async Task APublishedPack_ShouldCountWithOrWithoutACluster()
+    {
+        var draft = await PublicationTesting.RigAsync(mongo);
+        var published = await PublicationTesting.PublishedRigAsync(mongo);
+        await published.Store.SetPackClusterAsync(published.PackId, null);
+
+        Assert.False(await draft.Store.HasPublishedPackUsingAsync(packId: draft.PackId));
+        Assert.False(await draft.Store.HasPublishedPackUsingAsync(modelId: "model-test"));
+        Assert.True(await published.Store.HasPublishedPackUsingAsync(packId: published.PackId));
+        Assert.True(await published.Store.HasPublishedPackUsingAsync(modelId: "model-test"));
+        Assert.True(await published.Store.HasPublishedPackUsingAsync(batchId: "batch-test-2026"));
+        Assert.Empty(await published.Store.FindPublishedClusterIdsUsingAsync(packId: published.PackId));
+    }
 }
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DocumentServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublishedClusterStoreTests"`
Expected: the build fails: `DppStore` has no `HasPublishedPackUsingAsync` (CS1061).

- [ ] **Step 3: Keep unpublished files with the team**

Patch `3a-task3-code.patch` (`web/Dpp/Documents/DocumentService.cs`, `web/Dpp/Store/DppStore.cs`). Save the block as `../3a-task3-code.patch`, outside the worktree, and run `git apply ../3a-task3-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Documents/DocumentService.cs b/web/Dpp/Documents/DocumentService.cs
--- a/web/Dpp/Documents/DocumentService.cs
+++ b/web/Dpp/Documents/DocumentService.cs
@@ -190,13 +190,23 @@ public sealed class DocumentService(
             return false;
         }
 
-        var clusters = owner switch
+        if (owner is not (TargetKind.Model or TargetKind.Batch or TargetKind.Pack))
         {
-            TargetKind.Pack => await store.FindPublishedClusterIdsUsingAsync(packId: ownerId, cancellationToken: cancellationToken),
-            TargetKind.Batch => await store.FindPublishedClusterIdsUsingAsync(batchId: ownerId, cancellationToken: cancellationToken),
-            TargetKind.Model => await store.FindPublishedClusterIdsUsingAsync(modelId: ownerId, cancellationToken: cancellationToken),
-            _ => [],
-        };
+            return false;
+        }
+
+        var modelId = owner == TargetKind.Model ? ownerId : null;
+        var batchId = owner == TargetKind.Batch ? ownerId : null;
+        var packId = owner == TargetKind.Pack ? ownerId : null;
+
+        // Nothing unpublished leaves the team (J-6): until a published passport uses the record, its documents are the team's.
+        if (!reader.Roles.Contains(ReaderRole.Team)
+            && !await store.HasPublishedPackUsingAsync(modelId, batchId, packId, cancellationToken))
+        {
+            return false;
+        }
+
+        var clusters = await store.FindPublishedClusterIdsUsingAsync(modelId, batchId, packId, cancellationToken);
         return AudienceRules.MaySee(reader, attribute, clusters);
     }
 
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -321,6 +321,28 @@ public sealed class DppStore
     /// nobody: a draft is visible to the team only (J-6).
     /// </summary>
     public async Task<IReadOnlyList<string>> FindPublishedClusterIdsUsingAsync(string? modelId = null, string? batchId = null, string? packId = null, CancellationToken cancellationToken = default)
+    {
+        var published = await FindPublishedPackIdsUsingAsync(modelId, batchId, packId, cancellationToken);
+        if (published.Count == 0)
+        {
+            return [];
+        }
+
+        var clusters = await Collection<BatteryPack>()
+            .Find(pack => published.Contains(pack.Id) && pack.ClusterId != null)
+            .Project(pack => pack.ClusterId)
+            .ToListAsync(cancellationToken);
+        return clusters.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
+    }
+
+    /// <summary>
+    /// True when a published passport is built from this model, batch or pack record, whether or not its pack has a
+    /// cluster. Until then the record's documents are the team's only (J-6).
+    /// </summary>
+    public async Task<bool> HasPublishedPackUsingAsync(string? modelId = null, string? batchId = null, string? packId = null, CancellationToken cancellationToken = default) =>
+        (await FindPublishedPackIdsUsingAsync(modelId, batchId, packId, cancellationToken)).Count > 0;
+
+    private async Task<IReadOnlyList<string>> FindPublishedPackIdsUsingAsync(string? modelId, string? batchId, string? packId, CancellationToken cancellationToken)
     {
         if (_database is null)
         {
@@ -335,15 +357,10 @@ public sealed class DppStore
             return [];
         }
 
-        var published = await Collection<PassportVersion>()
+        return await Collection<PassportVersion>()
             .Find(version => packIds.Contains(version.PackId) && version.DppStatus == DppStatuses.Active)
             .Project(version => version.PackId)
             .ToListAsync(cancellationToken);
-        var clusters = await Collection<BatteryPack>()
-            .Find(pack => published.Contains(pack.Id) && pack.ClusterId != null)
-            .Project(pack => pack.ClusterId)
-            .ToListAsync(cancellationToken);
-        return clusters.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
     }
 
     /// <summary>Every record of one kind. Families, sites and operators are few; the pages sort them by name.</summary>
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DocumentServiceTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.PublishedClusterStoreTests"`
Expected: `Passed!  - Failed:     0, Passed:    33`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2002`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DocumentServiceTests.cs BatteryPassWeb.Tests/Dpp/PublishedClusterStoreTests.cs web/Dpp/Documents/DocumentService.cs web/Dpp/Store/DppStore.cs
git commit -m "feat(dpp): documents of unpublished records stay with the team" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 4: The document download and the accident page

Razor views compile with the build, so a mistake in a view fails Step 2's build like one in C#.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/DppControllerTests.cs`
- Create: `web/Controllers/DppController.cs`
- Create: `web/Dpp/Pages/ReaderPages.cs`
- Create: `web/Views/Dpp/Accidents.cshtml`
- Create: `web/Views/Dpp/NotAvailable.cshtml`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`
- Modify: `web/Views/Batteries/Battery.cshtml`
- Modify: `web/Views/ProductData/_EditorField.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `3a-task4-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`). Save the block as `../3a-task4-tests.patch`, outside the worktree, and run `git apply ../3a-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
--- a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
@@ -1,5 +1,6 @@
 using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp;
+using BatteryPassWeb.Dpp.Access;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Forms;
@@ -38,6 +39,8 @@ public sealed class DppServiceWiringTests
         Assert.NotNull(provider.GetRequiredService<RecordEditor>());
         Assert.Same(provider.GetRequiredService<AttributeForms>(), provider.GetRequiredService<RecordEditor>().Forms);
         Assert.NotNull(provider.GetRequiredService<BatteryPages>());
+        Assert.NotNull(provider.GetRequiredService<DppReaders>());
+        Assert.NotNull(provider.GetRequiredService<ReaderPages>());
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/DppControllerTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The addresses passports link to (design 9): uploaded documents and the accident page, for each kind of reader.</summary>
[Collection(MongoCollection.Name)]
public sealed class DppControllerTests(MongoFixture mongo)
{
    private const string OwnClusterUser = "user@example.com";
    private const string OtherClusterUser = "other@example.com";

    private static string View(string name) => File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Dpp", $"{name}.cshtml"));

    [Fact]
    public void TheAddresses_ShouldBeOpenToEveryoneAndOnlyRead()
    {
        var type = typeof(DppController);

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("dpp", type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.All(
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>()));
    }

    [Fact]
    public void TheLinksThePassportsCarry_ShouldBeTheseAddresses()
    {
        Assert.Equal(DocumentTesting.BaseUrl + ReaderPages.DocumentHref("doc-1 a"), DocumentTesting.Links.DocumentUrl("doc-1 a"));
        Assert.Equal(DocumentTesting.BaseUrl + ReaderPages.AccidentsHref("pack-1 a"), DocumentTesting.Links.AccidentPageUrl("pack-1 a"));
    }

    [Fact]
    public async Task ADocument_ShouldBeServedWithItsStoredTypeAsAFileThatIsNeverSniffedOrCached()
    {
        var rig = await RigAsync();
        var uploaded = await UploadDismantlingAsync(rig);
        var controller = Controller(rig, OwnClusterUser);

        var result = Assert.IsType<FileContentResult>(await controller.Document(uploaded.Id, CancellationToken.None));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(DocumentTesting.Pdf("manual"), result.FileContents);
        var headers = controller.Response.Headers;
        Assert.Equal("inline; filename=dismantling.pdf; filename*=UTF-8''dismantling.pdf", headers.ContentDisposition.ToString());
        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("private, no-store", headers.CacheControl.ToString());
    }

    [Fact]
    public async Task NotSignedIn_ShouldBeAskedToSignInFirst()
    {
        var rig = await RigAsync();
        var uploaded = await UploadDismantlingAsync(rig);

        Assert.IsType<ChallengeResult>(await Controller(rig, null).Document(uploaded.Id, CancellationToken.None));
        Assert.IsType<ChallengeResult>(await Controller(rig, null).Accidents(rig.PackId, CancellationToken.None));
        Assert.IsType<ChallengeResult>(await Controller(rig, null).Document("no-such-document", CancellationToken.None));
    }

    [Theory]
    [InlineData("other-cluster", "document")]
    [InlineData("other-cluster", "missing-document")]
    [InlineData("other-cluster", "accidents")]
    [InlineData("other-cluster", "missing-pack")]
    [InlineData("team", "missing-document")]
    [InlineData("team", "missing-pack")]
    public async Task SignedInWithoutAccess_ShouldGetNotAvailableWithoutSayingWhetherItExists(string who, string target)
    {
        var rig = await RigAsync();
        var uploaded = await UploadDismantlingAsync(rig);
        var controller = Controller(rig, who == "team" ? "team@example.com" : OtherClusterUser, who == "team" ? AccessControlService.RoleAdmin : "member");

        var result = target switch
        {
            "document" => await controller.Document(uploaded.Id, CancellationToken.None),
            "missing-document" => await controller.Document("doc-missing", CancellationToken.None),
            "accidents" => await controller.Accidents(rig.PackId, CancellationToken.None),
            _ => await controller.Accidents("pack-missing", CancellationToken.None),
        };

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("NotAvailable", view.ViewName);
        Assert.Equal(StatusCodes.Status404NotFound, view.StatusCode);
        Assert.Equal(target.Contains("document", StringComparison.Ordinal) ? "document" : "accidents", view.ViewData["Subject"]);
    }

    [Fact]
    public async Task TheAccidentPage_ShouldListTheReportsNewestFirstForTheBatterysCluster()
    {
        var rig = await RigAsync();
        var documents = PageTesting.Documents(rig);
        await documents.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("first.pdf", DocumentTesting.Pdf("1"), "Collision"));
        rig.Clock.Now = rig.Clock.Now.AddDays(2);
        await documents.AddAccidentReportAsync(PublicationTesting.Team, rig.PackId, new DocumentUpload("second.png", DocumentTesting.Png()));

        var view = Assert.IsType<ViewResult>(await Controller(rig, OwnClusterUser).Accidents(rig.PackId, CancellationToken.None));

        var page = Assert.IsType<AccidentsPage>(view.Model);
        Assert.Equal(rig.PackId, page.PackId);
        Assert.Equal(rig.Seed.Sources.Pack.Values["batterySerialNumber"].Value.GetValue<string>(), page.SerialNumber);
        Assert.Equal(rig.Seed.Sources.Pack.Values["uniqueProductIdentifier"].Value.GetValue<string>(), page.Identifier);
        Assert.Equal(["second.png", "Collision"], page.Reports.Select(report => report.Title));
    }

    [Fact]
    public async Task ADraftBatterysAccidentPage_ShouldBeTheTeamsOnly()
    {
        var rig = await RigAsync(published: false);

        var team = await Controller(rig, "team@example.com", AccessControlService.RoleAdmin).Accidents(rig.PackId, CancellationToken.None);
        var cluster = await Controller(rig, OwnClusterUser).Accidents(rig.PackId, CancellationToken.None);
        var surveillance = await Controller(rig, "msa@example.com", AccessControlService.RoleMarketSurveillanceAuthority).Accidents(rig.PackId, CancellationToken.None);

        Assert.Empty(Assert.IsType<AccidentsPage>(Assert.IsType<ViewResult>(team).Model).Reports);
        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(cluster).ViewName);
        Assert.Equal("NotAvailable", Assert.IsType<ViewResult>(surveillance).ViewName);
    }

    [Fact]
    public void TheViews_ShouldFollowTheMockUp()
    {
        var accidents = View("Accidents");
        var notAvailable = View("NotAvailable");

        Assert.Contains("No accidents reported", accidents, StringComparison.Ordinal);
        Assert.Contains("ReaderPages.DocumentHref(report.Id)", accidents, StringComparison.Ordinal);
        Assert.Contains("Identifiers.IsTestIdentifier(Model.Identifier)", accidents, StringComparison.Ordinal);
        Assert.DoesNotContain("UploadedBy", accidents, StringComparison.Ordinal);
        Assert.Contains("Not available", notAvailable, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTeamsPages_ShouldLinkTheFilesAndTheAccidentPage()
    {
        var battery = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));
        var field = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "_EditorField.cshtml"));

        Assert.Contains("ReaderPages.AccidentsHref(Model.Pack.Id)", battery, StringComparison.Ordinal);
        Assert.Contains("ReaderPages.DocumentHref(report.Id)", battery, StringComparison.Ordinal);
        Assert.Contains("<a href=\"@ReaderPages.DocumentHref(document.Id)\">Open the file</a>", field, StringComparison.Ordinal);
    }

    /// <summary>The seed pack, published or not, with a member of its cluster and a member of another cluster.</summary>
    private async Task<PublicationRig> RigAsync(bool published = true)
    {
        var rig = published ? await PublicationTesting.PublishedRigAsync(mongo) : await PublicationTesting.RigAsync(mongo);
        var clusters = new ClusterRepository(rig.Context);
        await clusters.UpsertClusterMembershipAsync(OwnClusterUser, BatteryTesting.North, "member");
        await clusters.UpsertClusterMembershipAsync(OtherClusterUser, BatteryTesting.South, "member");
        return rig;
    }

    private static async Task<DppDocument> UploadDismantlingAsync(PublicationRig rig) =>
        (await PageTesting.Documents(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, "model-test", "dismantlingInformation", new DocumentUpload("dismantling.pdf", DocumentTesting.Pdf("manual")))).Document!;

    private static DppController Controller(PublicationRig rig, string? email, string role = "member")
    {
        var http = new DefaultHttpContext
        {
            User = email is null
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test")),
        };
        var documents = PageTesting.Documents(rig);
        return new DppController(new DppReaders(new ClusterRepository(rig.Context)), documents, new ReaderPages(rig.Store, documents))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: the build fails because `DppController` doesn't exist yet (CS0246); `ReaderPages` is missing too.

- [ ] **Step 3: Write `DppController.cs`**

`web/Controllers/DppController.cs`:

```csharp
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The addresses passports link to (design 9): uploaded documents and the accident page, each served only to the
/// attribute's audience. Not signed in: the sign-in page comes first. Signed in without access: "Not available", which
/// doesn't say whether the battery or the file exists.
/// </summary>
[AllowAnonymous]
[Route("dpp")]
public sealed class DppController(DppReaders readers, DocumentService documents, ReaderPages pages) : Controller
{
    [HttpGet("documents/{documentId}")]
    public async Task<IActionResult> Document(string documentId, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var file = await documents.ReadAsync(reader, documentId, cancellationToken);
        if (file is null)
        {
            return Refused("document");
        }

        // The type stored at upload was read from the file's own bytes (2c), never taken from a request.
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(file.Document.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return File(file.Content, file.Document.ContentType);
    }

    [HttpGet("packs/{packId}/accidents")]
    public async Task<IActionResult> Accidents(string packId, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var page = await pages.AccidentsAsync(reader, packId, cancellationToken);
        if (page is null)
        {
            return Refused("accidents");
        }

        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return View(page);
    }

    private IActionResult Refused(string subject)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        ViewData["Subject"] = subject;
        var view = View("NotAvailable");
        view.StatusCode = StatusCodes.Status404NotFound;
        return view;
    }
}
```

- [ ] **Step 4: Write `ReaderPages.cs`**

`web/Dpp/Pages/ReaderPages.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The accident page (#92, design 9 and J-14): the battery, and its accident reports newest first.</summary>
public sealed record AccidentsPage(string PackId, string SerialNumber, string Identifier, IReadOnlyList<DppDocument> Reports);

/// <summary>
/// The pages passport readers open, cut to what the reader may see (step 3). The accident page comes first, because
/// every passport's #92 already links to it.
/// </summary>
public sealed class ReaderPages(DppStore store, DocumentService documents)
{
    /// <summary>The app's own address of an uploaded document, behind its access check (design 9).</summary>
    public static string DocumentHref(string documentId) => $"/dpp/documents/{Uri.EscapeDataString(documentId)}";

    /// <summary>The pack's accident page, the address #92 links to (<see cref="DppLinks.AccidentPageUrl"/>).</summary>
    public static string AccidentsHref(string packId) => $"/dpp/packs/{Uri.EscapeDataString(packId)}/accidents";

    /// <summary>The accident page when the reader may see #92 for this battery; otherwise null, whether or not it exists.</summary>
    public async Task<AccidentsPage?> AccidentsAsync(Reader reader, string packId, CancellationToken cancellationToken = default)
    {
        var reports = await documents.AccidentReportsAsync(reader, packId, cancellationToken);
        var pack = reports is null ? null : await store.GetAsync<BatteryPack>(packId, cancellationToken);
        return pack is null
            ? null
            : new AccidentsPage(pack.Id, Text(pack, "batterySerialNumber") ?? pack.Id, Text(pack, "uniqueProductIdentifier") ?? string.Empty, reports!);
    }

    /// <summary>"PDF", "PNG picture" or "JPEG picture": the stored, byte-checked type in words.</summary>
    public static string FileKind(string contentType) => contentType switch
    {
        "application/pdf" => "PDF",
        "image/png" => "PNG picture",
        "image/jpeg" => "JPEG picture",
        _ => contentType,
    };

    private static string? Text(BatteryPack pack, string property) =>
        pack.Values.GetValueOrDefault(property)?.Value is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
```

- [ ] **Step 5: Write `Accidents.cshtml`**

`web/Views/Dpp/Accidents.cshtml`:

```cshtml
@using System.Globalization
@using BatteryPassWeb.Dpp.Identifiers
@using BatteryPassWeb.Dpp.Pages
@inject IIdentifierScheme Identifiers
@model AccidentsPage
@{
    ViewData["Title"] = $"Accident information · {Model.SerialNumber}";
    var english = CultureInfo.GetCultureInfo("en-GB");
    string Day(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("d MMM yyyy", english);
    static string Size(long bytes) => bytes < 1024
        ? $"{bytes} bytes"
        : bytes < 1048576 ? $"{(bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture)} KB" : $"{(bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB";
}
<main class="bp-page">
    <section class="bp-card bp-report-wide-section">
        <span class="bp-summary-title">Accident information</span>
        <h1 class="h3 mt-2 mb-2" title="Battery serial number">@Model.SerialNumber</h1>
        <dl class="bp-field-grid mb-0">
            <div><dt>Battery ID</dt><dd class="bp-id-value">@Model.Identifier</dd></div>
        </dl>
        @if (Identifiers.IsTestIdentifier(Model.Identifier))
        {
            <p class="text-secondary small mt-2 mb-0">Test identifiers: this battery's IDs start with TEST-. Real ones come once the identifier scheme is chosen.</p>
        }
    </section>

    <section class="bp-card bp-report-wide-section">
        <span class="bp-summary-title">Accident reports</span>
        @if (Model.Reports.Count == 0)
        {
            <h2 class="h5 mt-2 mb-1">No accidents reported</h2>
            <p class="text-secondary mb-0">No accident involving this battery has been reported.</p>
        }
        else
        {
            <h2 class="h5 mt-2 mb-1">@Words.Count(Model.Reports.Count, "accident report")</h2>
            <p class="text-secondary">Reports of accidents involving this battery, newest first.</p>
            <div class="bp-table-scroll">
                <table class="table align-middle mb-0 bp-console-table bp-mobile-card-table">
                    <thead><tr><th>Report</th><th>Added</th><th>File</th></tr></thead>
                    <tbody>
                        @foreach (var report in Model.Reports)
                        {
                            <tr>
                                <td data-label="Report"><a href="@ReaderPages.DocumentHref(report.Id)">@report.Title</a></td>
                                <td data-label="Added">@Day(report.UploadedAtUtc)</td>
                                <td data-label="File">@ReaderPages.FileKind(report.ContentType) · @Size(report.Length)</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </section>
</main>
```

- [ ] **Step 6: Write `NotAvailable.cshtml`**

`web/Views/Dpp/NotAvailable.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Not available";
    var accidents = ViewData["Subject"] as string == "accidents";
}
<main class="bp-page">
    <section class="bp-card bp-report-wide-section">
        <span class="bp-summary-title">@(accidents ? "Accident information" : "Battery passport document")</span>
        <h1 class="h5 mt-2 mb-1">Not available</h1>
        @if (accidents)
        {
            <p class="text-secondary mb-0">This information is shown only to the battery's cluster, market surveillance and the manufacturer's team. If you think you should see it, ask your cluster admin.</p>
        }
        else
        {
            <p class="text-secondary mb-0">This document is shown only to those the battery passport names for it, such as the battery's cluster, the authorities or the manufacturer's team. If you think you should see it, ask your cluster admin.</p>
        }
    </section>
</main>
```

- [ ] **Step 7: Register the services and link the team pages**

Patch `3a-task4-code.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`, `web/Views/Batteries/Battery.cshtml`, `web/Views/ProductData/_EditorField.cshtml`). Save the block as `../3a-task4-code.patch`, outside the worktree, and run `git apply ../3a-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -1,3 +1,4 @@
+using BatteryPassWeb.Dpp.Access;
 using BatteryPassWeb.Dpp.Audit;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
@@ -42,6 +43,8 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<RecordEditor>();
         services.AddSingleton<ProductDataPages>();
         services.AddSingleton<BatteryPages>();
+        services.AddSingleton<DppReaders>();
+        services.AddSingleton<ReaderPages>();
         services.AddSingleton<AuditLines>();
         return services;
     }
diff --git a/web/Views/Batteries/Battery.cshtml b/web/Views/Batteries/Battery.cshtml
--- a/web/Views/Batteries/Battery.cshtml
+++ b/web/Views/Batteries/Battery.cshtml
@@ -334,7 +334,7 @@
     <section class="bp-card bp-console-form-card bp-battery-card mt-4" id="accidents">
         <span class="bp-summary-title">Accident reports <span class="bp-attr-number">#92</span></span>
         <h2>@(Model.AccidentReports.Count == 0 ? "No accidents reported" : Words.Count(Model.AccidentReports.Count, "accident report"))</h2>
-        <p class="mb-0">The passport links to the battery's accident page, which lists these reports or says none were reported. Seen only by the battery's cluster, market surveillance and your team.</p>
+        <p class="mb-0">The passport links to the battery's <a href="@ReaderPages.AccidentsHref(Model.Pack.Id)">accident page</a>, which lists these reports or says none were reported. Seen only by the battery's cluster, market surveillance and your team.</p>
         @{ await CardMessagesAsync("accidents"); }
         @if (Model.AccidentReports.Count > 0)
         {
@@ -342,7 +342,7 @@
                 @foreach (var report in Model.AccidentReports)
                 {
                     <li>
-                        <span><strong>@report.Title</strong> · @report.FileName · @Size(report.Length)</span>
+                        <span><a href="@ReaderPages.DocumentHref(report.Id)"><strong>@report.Title</strong></a> · @report.FileName · @Size(report.Length)</span>
                         <span class="bp-batteries-muted">@When(report.UploadedAtUtc) by @report.UploadedBy</span>
                     </li>
                 }
diff --git a/web/Views/ProductData/_EditorField.cshtml b/web/Views/ProductData/_EditorField.cshtml
--- a/web/Views/ProductData/_EditorField.cshtml
+++ b/web/Views/ProductData/_EditorField.cshtml
@@ -177,7 +177,7 @@
                 <input type="text" readonly value="@current" aria-label="Current file" data-upload-name />
                 @if (document is not null)
                 {
-                    <small class="bp-field-help">SHA-256 <code>@document.Sha256</code></small>
+                    <small class="bp-field-help"><a href="@ReaderPages.DocumentHref(document.Id)">Open the file</a> · SHA-256 <code>@document.Sha256</code></small>
                 }
                 <fieldset class="bp-attr-upload">
                     <legend>@(document is null ? "Upload a file" : "Upload a new file")</legend>
```

- [ ] **Step 8: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppControllerTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    16`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2016`, none failed.

- [ ] **Step 9: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs BatteryPassWeb.Tests/Dpp/DppControllerTests.cs web/Controllers/DppController.cs web/Dpp/Pages/ReaderPages.cs web/Views/Dpp/Accidents.cshtml web/Views/Dpp/NotAvailable.cshtml web/Dpp/DppServiceCollectionExtensions.cs web/Views/Batteries/Battery.cshtml web/Views/ProductData/_EditorField.cshtml
git commit -m "feat(dpp): the document download and the accident page that passports link to" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 5: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2016, Skipped:     0, Total:  2016`: the 1951 from before, 7 new in Task 1, 37 in Task 2 (11 of them the role matrix through the filter, one per kind of reader), 7 in Task 3 and 14 in Task 4.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Look at the pages**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too, and sign in as a team member. Never point it at `battery_pass_demo`. Check, in light and dark theme:

1. **Users** (menu): "Access" offers the three "Legitimate interest: …" roles and no "Person with Legitimate Interest". An account that had the old role shows "Cluster Member".
2. **A published battery's page**, Accident reports: add a report (a PDF) if it has none. Its title opens the file in the browser; "accident page" opens the page that lists it, newest first.
3. **A model or batch with an uploaded file** (#29, #45–#48): "Open the file" opens it.
4. **A draft battery's accident page** (`/dpp/packs/<its id>/accidents`): you see it, with "No accidents reported".
5. **Sign out** and open the accident page's address again: the sign-in page opens; after signing in you are back on the accident page.
6. **An account without access** (for example a new "Cluster Member" with no cluster): the accident page and a file's address say "Not available".
7. **A narrow window** (about 390 px): the accident page doesn't scroll sideways.

Remove any test account or report you added only for these checks.

## After this plan

- **3b** shows passports: the public summary (no login, from the QR code) and the detail page, both through `PassportFilter` with the reader from `DppReaders`. They replace today's passport pages, the QR code's target and the home page's search, and link to the accident page.
- **3c** rebuilds the external API on the new model; its tokens read through `DppReaders.For`, and `/api/passports` becomes team-only.
- **3d** removes the old model and pages and resets `battery_pass_v3`, after the product owner has reviewed the accounts (including those with the old role).
- **Known small gaps:**
  - Only an active passport counts as published for access (`DppStatus` "active", from 2c). When step 4 brings inactive, invalid and marked-for-deletion passports, design 5.2's "all versions alike" needs them to count too, so a battery's cluster keeps its data after its passport becomes inactive.
  - The download reads the whole file into memory (2c's note); fine at the 10 MB upload limit.
  - The accident page and the downloads have no rate limit of their own; the app's general limits apply.

