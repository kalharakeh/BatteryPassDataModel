# DPP V3 Redesign R2: A Picture per Battery Model — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The second part of the battery pages' redesign (decision 36; amends decision 20). Point 8: the Compact 13M showed the 7M's picture, because pictures were per family and the Compact family holds both. The product owner chose a picture per model, with the family's as fallback:
- **A model may have its own picture** (PNG or JPEG). Its batteries' public summary shows it instead of the family's. Without one, the family's shows, as before.
- **The family page's model section** saves and removes it, beside the module description.
- **The demo's Compact 13M** gets back the old app's double-stack picture (`compact13.png`, from the tag `app-v1.2-before-dpp-v3`). Clearing the demo data removes it with the model.

**Architecture:**
- **`BatteryModel.PictureFileId`/`PictureContentType`**, like the family's (app data, not passport data, decision 20). `DppStore.SetModelPictureAsync` sets or clears them.
- **`ProductDataPages`:**
  - `SetModelPictureAsync` (the family's check, now shared as `PictureProblem`), `RemoveModelPictureAsync` and `ModelPictureAsync`.
  - A replaced or removed picture's file is deleted. Each change is logged (`dpp.family-picture-set`, on the model).
- **`ProductDataController`:** `POST /admin/models/{id}/picture` and `…/picture/remove` (back to the model's `#modules` section), and `GET /dpp/models/{id}/picture` for anyone, like the family's.
- **`PassportPages`:** the model's picture, else the family's.
- **Demo:** `DemoModel.Picture`, seeded after the module description; `ClearAsync` deletes a removed demo model's picture file.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9, xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded.

**Spec:** the owner's 17 points and the UX review (in the managing session's notes); the approved mock-up `docs/mockups/dpp-v3-battery-page-redesign.html` (owner's choice of 2 Nov 2026: layout B); decision 36 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at 4f65caa. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas** or `battery_pass_v3`. The tests use a throwaway MongoDB (EphemeralMongo).
- **Builds and test runs:**
  - Run every `dotnet test` and `dotnet build` with `--disable-build-servers`, in a shell where `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` was run first. Whole-suite runs also take `--blame-hang --blame-hang-timeout 10m`.
  - Runs have sometimes taken far longer while the machine was busy with other sessions: a slowdown, not a stuck test. If a run seems stuck for more than 20 minutes, stop only the `dotnet` and `testhost` processes your own run started (by their process IDs, never by name) and run it again.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers` shows `Passed:  2268`. After this plan: 2273. The suite takes about two to four minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../r1-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New files are given whole.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.** The new styles go to `bp-product-data.css` (battery pages) and the new `bp-status.css` (every page, R3).
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## What happens to the old tests

Nothing is removed.
- **`ProductDataControllerTests`:** the anonymous actions are now `FamilyPicture` and `ModelPicture` (task 1).
- **`DemoSeedTests`:** the seed stores one more file, the 13M's picture: 20 instead of 19 (task 2).

---

### Task 1: A battery model's own picture

A model's picture is stored like its family's, checked the same way (a PNG or JPEG within the upload limit), served to anyone, and shown on its batteries' public summary instead of the family's. Removing it shows the family's again. The family page's model section gets the form, with the picture it shows now.

**Files:**
- Test (create): `BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs`
- Modify: `web/Controllers/ProductDataController.cs`
- Modify: `web/Dpp/Pages/PassportPages.cs`
- Modify: `web/Dpp/Pages/ProductDataPages.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Views/ProductData/Family.cshtml`
- Modify: `web/wwwroot/css/bp-product-data.css`

- [ ] **Step 1: Write the failing tests**

Patch `r2-task1-tests.patch` (`BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs`). Save the block as `../r2-task1-tests.patch`, outside the worktree, and run `git apply ../r2-task1-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs b/BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs
index 09ee85d..375555a 100644
--- a/BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs
@@ -21,7 +21,7 @@ public sealed class ProductDataControllerTests(MongoFixture mongo)
         var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
         Assert.All(actions.Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null), action =>
             Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
-        Assert.Equal(["FamilyPicture"], actions.Where(action => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null).Select(action => action.Name));
+        Assert.Equal(["FamilyPicture", "ModelPicture"], actions.Where(action => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null).Select(action => action.Name));
     }
 
     [Fact]
```

`BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs`:

```csharp
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Redesign R2: a picture per battery model, shown instead of its family's (owner's point 8: the Compact 13M showed the
/// 7M's picture). A model without one shows its family's picture, as before (decision 20).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ModelPictureTests(MongoFixture mongo)
{
    private const string Team = "team@example.com";

    private static IFormFile Upload(byte[] content, string name) => new FormFile(new MemoryStream(content), 0, content.Length, "picture", name);

    [Fact]
    public async Task AModelsPicture_ShouldBeShownInsteadOfItsFamilysAndServedToAnyone()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetFamilyPictureAsync("family-test", "file-1", "image/png");

        var problem = await PageTesting.Pages(rig).SetModelPictureAsync(Team, "model-test", "model.png", DocumentTesting.Png());
        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, "VPC8C05-6788"))!;
        var file = Assert.IsType<FileContentResult>(await PageTesting.Controller(rig).ModelPicture("model-test", CancellationToken.None));

        Assert.Null(problem);
        Assert.Equal("/dpp/models/model-test/picture", summary.PictureHref);
        Assert.Equal(FileTypes.Png, file.ContentType);
        Assert.Equal(DocumentTesting.Png(), file.FileContents);
        Assert.IsType<NotFoundResult>(await PageTesting.Controller(rig).ModelPicture("no-such-model", CancellationToken.None));
    }

    [Fact]
    public async Task RemovingAModelsPicture_ShouldShowTheFamilysAgain()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.SetFamilyPictureAsync("family-test", "file-1", "image/png");
        await PageTesting.Pages(rig).SetModelPictureAsync(Team, "model-test", "model.png", DocumentTesting.Png());

        Assert.True(await PageTesting.Pages(rig).RemoveModelPictureAsync(Team, "model-test"));
        var summary = (await PassportPagesTests.Pages(rig).SummaryAsync(Reader.Anonymous, "VPC8C05-6788"))!;

        Assert.Equal("/dpp/families/family-test/picture", summary.PictureHref);
        Assert.Null((await rig.Store.GetAsync<BatteryModel>("model-test"))!.PictureFileId);
        Assert.IsType<NotFoundResult>(await PageTesting.Controller(rig).ModelPicture("model-test", CancellationToken.None));
        Assert.False(await PageTesting.Pages(rig).RemoveModelPictureAsync(Team, "model-test")); // nothing left to remove
    }

    [Fact]
    public async Task AModelsPicture_ShouldBeAPngOrJpegOfAKnownModel()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        Assert.Equal("the picture must be a PNG or JPEG file", await PageTesting.Pages(rig).SetModelPictureAsync(Team, "model-test", "model.pdf", DocumentTesting.Pdf()));
        Assert.Equal("the battery model was not found", await PageTesting.Pages(rig).SetModelPictureAsync(Team, "no-such-model", "model.png", DocumentTesting.Png()));
        Assert.Null((await rig.Store.GetAsync<BatteryModel>("model-test"))!.PictureFileId);
    }

    [Fact]
    public async Task TheFamilyPage_ShouldSaveAndRemoveAModelsPictureInItsSection()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "ProductData", "Family.cshtml"));

        var saved = Assert.IsType<RedirectResult>(await controller.UploadModelPicture("model-test", Upload(DocumentTesting.Png(), "model.png"), CancellationToken.None));
        var savedMessage = controller.TempData[ProductDataController.StatusKey];
        var removed = Assert.IsType<RedirectResult>(await controller.RemoveModelPicture("model-test", CancellationToken.None));

        Assert.Equal($"{ProductDataPages.FamilyHref("family-test", "model-test")}#modules", saved.Url);
        Assert.Equal("Picture saved. Its batteries' public summary shows it instead of the family's.", savedMessage);
        Assert.Equal($"{ProductDataPages.FamilyHref("family-test", "model-test")}#modules", removed.Url);
        Assert.Equal("Picture removed. Its batteries show the family's picture again.", controller.TempData[ProductDataController.StatusKey]);
        Assert.Contains("action=\"/admin/models/@selected.Id/picture\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/models/@selected.Id/picture/remove\"", page, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/dpp/models/@selected.Id/picture\"", page, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ModelPictureTests | FullyQualifiedName~ProductDataControllerTests"`
Expected: the build fails with CS1061 (for example `error CS1061 'BatteryModel' does not contain a definition for 'PictureFileId'`).

- [ ] **Step 3: Write the code**

Patch `r2-task1-code.patch` (`web/Controllers/ProductDataController.cs`, `web/Dpp/Pages/PassportPages.cs`, `web/Dpp/Pages/ProductDataPages.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`, `web/Views/ProductData/Family.cshtml`, `web/wwwroot/css/bp-product-data.css`). Save the block as `../r2-task1-code.patch`, outside the worktree, and run `git apply ../r2-task1-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/ProductDataController.cs b/web/Controllers/ProductDataController.cs
index a82c78f..c1b509e 100644
--- a/web/Controllers/ProductDataController.cs
+++ b/web/Controllers/ProductDataController.cs
@@ -134,6 +134,49 @@ public sealed class ProductDataController(ProductDataPages pages, RecordEditor e
         return picture is { } found ? File(found.Content, found.ContentType) : NotFound();
     }
 
+    /// <summary>A model's own picture, shown on its batteries' public summary instead of the family's (redesign R2).</summary>
+    [HttpPost("models/{modelId}/picture")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> UploadModelPicture(string modelId, IFormFile? picture, CancellationToken cancellationToken)
+    {
+        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is not { } model)
+        {
+            return NotFound();
+        }
+
+        var problem = picture is null || picture.Length == 0
+            ? "choose a PNG or JPEG picture first"
+            : await pages.SetModelPictureAsync(Who, modelId, picture.FileName, await ReadAsync(picture, cancellationToken), cancellationToken);
+        TempData[problem is null ? StatusKey : ErrorKey] = problem is null
+            ? "Picture saved. Its batteries' public summary shows it instead of the family's."
+            : $"The picture was not saved: {problem}.";
+        return Redirect($"{ProductDataPages.FamilyHref(model.FamilyId, modelId)}#modules");
+    }
+
+    [HttpPost("models/{modelId}/picture/remove")]
+    [ValidateAntiForgeryToken]
+    public async Task<IActionResult> RemoveModelPicture(string modelId, CancellationToken cancellationToken)
+    {
+        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is not { } model)
+        {
+            return NotFound();
+        }
+
+        TempData[StatusKey] = await pages.RemoveModelPictureAsync(Who, modelId, cancellationToken)
+            ? "Picture removed. Its batteries show the family's picture again."
+            : "The model had no picture of its own.";
+        return Redirect($"{ProductDataPages.FamilyHref(model.FamilyId, modelId)}#modules");
+    }
+
+    /// <summary>A model's picture is shown on the public summary like the family's, so anyone may load it.</summary>
+    [AllowAnonymous]
+    [HttpGet("~/dpp/models/{modelId}/picture")]
+    public async Task<IActionResult> ModelPicture(string modelId, CancellationToken cancellationToken)
+    {
+        var picture = await pages.ModelPictureAsync(modelId, cancellationToken);
+        return picture is { } found ? File(found.Content, found.ContentType) : NotFound();
+    }
+
     [HttpPost("families/{familyId}/models")]
     [ValidateAntiForgeryToken]
     public async Task<IActionResult> CreateModel(string familyId, [FromForm] string? name, [FromForm] string? copyFrom, CancellationToken cancellationToken)
diff --git a/web/Dpp/Pages/PassportPages.cs b/web/Dpp/Pages/PassportPages.cs
index 23e8089..2584c0c 100644
--- a/web/Dpp/Pages/PassportPages.cs
+++ b/web/Dpp/Pages/PassportPages.cs
@@ -269,7 +269,10 @@ public sealed class PassportPages(DppStore store, PassportReader reader, Attribu
             current,
             identifier,
             family?.Name,
-            family?.PictureFileId is null ? null : $"/dpp/families/{Uri.EscapeDataString(family.Id)}/picture",
+            // The model's own picture, else its family's (redesign R2, decision 20).
+            sources.Model.PictureFileId is not null
+                ? $"/dpp/models/{Uri.EscapeDataString(sources.Model.Id)}/picture"
+                : family?.PictureFileId is null ? null : $"/dpp/families/{Uri.EscapeDataString(family.Id)}/picture",
             identifiers.IsTestIdentifier(identifier) || identifiers.IsTestIdentifier(version.DigitalProductPassportId),
             PassportReader.Verify(version),
             AttributeCatalog.All.Any(a => a.Audience != Audience.Public && AudienceRules.MaySee(who, a, clusters)),
diff --git a/web/Dpp/Pages/ProductDataPages.cs b/web/Dpp/Pages/ProductDataPages.cs
index 1786367..6c9c42c 100644
--- a/web/Dpp/Pages/ProductDataPages.cs
+++ b/web/Dpp/Pages/ProductDataPages.cs
@@ -476,20 +476,13 @@ public sealed class ProductDataPages(
             return "the battery family was not found";
         }
 
-        var limit = options.Value.MaxUploadBytes;
-        var contentType = content.Length == 0 ? null : FileTypes.Detect(content);
-        if (contentType is not (FileTypes.Png or FileTypes.Jpeg))
-        {
-            return "the picture must be a PNG or JPEG file";
-        }
-
-        if (limit > 0 && content.LongLength > limit)
+        if (PictureProblem(content) is { } problem)
         {
-            return $"the picture is larger than the {limit}-byte upload limit";
+            return problem;
         }
 
         var fileId = await documents.UploadFileAsync(Path.GetFileName(fileName), content, cancellationToken);
-        await store.SetFamilyPictureAsync(familyId, fileId, contentType, cancellationToken);
+        await store.SetFamilyPictureAsync(familyId, fileId, FileTypes.Detect(content)!, cancellationToken);
         await AuditAsync(PictureEvent, who, $"Picture of battery family {familyId} set", "dpp-family", familyId, cancellationToken);
         return null;
     }
@@ -518,6 +511,68 @@ public sealed class ProductDataPages(
     }
 
     /// <summary>The family's picture; null when the family or its picture doesn't exist.</summary>
+    /// <summary>Stores a PNG or JPEG as a model's own picture, shown instead of its family's (redesign R2); the problem when it isn't accepted.</summary>
+    public async Task<string?> SetModelPictureAsync(string who, string modelId, string fileName, byte[] content, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(content);
+        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is not { } model)
+        {
+            return "the battery model was not found";
+        }
+
+        if (PictureProblem(content) is { } problem)
+        {
+            return problem;
+        }
+
+        var fileId = await documents.UploadFileAsync(Path.GetFileName(fileName), content, cancellationToken);
+        await store.SetModelPictureAsync(modelId, fileId, FileTypes.Detect(content)!, cancellationToken);
+        if (model.PictureFileId is { } replaced)
+        {
+            await documents.DeleteFileAsync(replaced, cancellationToken);
+        }
+
+        await AuditAsync(PictureEvent, who, $"Picture of battery model {model.Name} set", "dpp-model", modelId, cancellationToken);
+        return null;
+    }
+
+    /// <summary>Removes a model's own picture, so its batteries show the family's again (redesign R2); false when it had none.</summary>
+    public async Task<bool> RemoveModelPictureAsync(string who, string modelId, CancellationToken cancellationToken = default)
+    {
+        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is not { PictureFileId: { } fileId } model)
+        {
+            return false;
+        }
+
+        await store.SetModelPictureAsync(modelId, null, null, cancellationToken);
+        await documents.DeleteFileAsync(fileId, cancellationToken);
+        await AuditAsync(PictureEvent, who, $"Picture of battery model {model.Name} removed", "dpp-model", modelId, cancellationToken);
+        return true;
+    }
+
+    public async Task<(byte[] Content, string ContentType)?> ModelPictureAsync(string modelId, CancellationToken cancellationToken = default)
+    {
+        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
+        if (model?.PictureFileId is null || model.PictureContentType is null)
+        {
+            return null;
+        }
+
+        return (await documents.DownloadFileAsync(model.PictureFileId, cancellationToken), model.PictureContentType);
+    }
+
+    /// <summary>Why a picture isn't accepted (not a PNG or JPEG, or over the upload limit); null when it is.</summary>
+    private string? PictureProblem(byte[] content)
+    {
+        var limit = options.Value.MaxUploadBytes;
+        if ((content.Length == 0 ? null : FileTypes.Detect(content)) is not (FileTypes.Png or FileTypes.Jpeg))
+        {
+            return "the picture must be a PNG or JPEG file";
+        }
+
+        return limit > 0 && content.LongLength > limit ? $"the picture is larger than the {limit}-byte upload limit" : null;
+    }
+
     public async Task<(byte[] Content, string ContentType)?> FamilyPictureAsync(string familyId, CancellationToken cancellationToken = default)
     {
         var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
index ca2bd2f..e1e8247 100644
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -46,6 +46,11 @@ public sealed class BatteryModel : IDppRecord
 
     /// <summary>What the model's modules are, in a few words (decision 31). App data, not passport data; null when not typed.</summary>
     public string? ModuleDescription { get; set; }
+
+    /// <summary>The model's own picture, shown instead of its family's (redesign R2); null to show the family's. Not passport data.</summary>
+    public string? PictureFileId { get; set; }
+
+    public string? PictureContentType { get; set; }
 }
 
 /// <summary>Manufacturer identifier and information, and the manufacturing place (#12, #15, #16).</summary>
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
index d1ba179..69af43b 100644
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -640,6 +640,16 @@ public sealed class DppStore
         return result.MatchedCount == 1;
     }
 
+    /// <summary>Points a model at its own picture, or back at its family's with nulls (redesign R2); false when no model has that id.</summary>
+    public async Task<bool> SetModelPictureAsync(string modelId, string? fileId, string? contentType, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<BatteryModel>().UpdateOneAsync(
+            model => model.Id == modelId,
+            Builders<BatteryModel>.Update.Set(model => model.PictureFileId, fileId).Set(model => model.PictureContentType, contentType),
+            cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
     private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());
 
     private IMongoDatabase RequireDatabase() =>
diff --git a/web/Views/ProductData/Family.cshtml b/web/Views/ProductData/Family.cshtml
index 8951b21..524b328 100644
--- a/web/Views/ProductData/Family.cshtml
+++ b/web/Views/ProductData/Family.cshtml
@@ -133,6 +133,39 @@
     @if (selected is not null)
     {
         <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="modules">
+            @* The model's own picture, shown instead of the family's (redesign R2, owner's point 8). *@
+            <form method="post" action="/admin/models/@selected.Id/picture" enctype="multipart/form-data" class="bp-model-picture" data-upload-row>
+                @Html.AntiForgeryToken()
+                <div class="bp-model-picture-preview">
+                    @if (selected.PictureFileId is not null)
+                    {
+                        <img src="/dpp/models/@selected.Id/picture" alt="@selected.Name battery model picture" />
+                    }
+                    else if (hasPicture)
+                    {
+                        <img src="/dpp/families/@familyId/picture" alt="@Model.Family.Name battery family picture" />
+                    }
+                </div>
+                <div class="d-grid gap-2">
+                    <span><strong>Picture of @selected.Name</strong> <em class="bp-field-marker">Not passport data</em></span>
+                    <small class="bp-field-help">@(selected.PictureFileId is null ? "It shows the family's picture. Give it its own when it looks different, for example a double stack." : "Its own picture: its batteries' public summary shows it instead of the family's.")</small>
+                    <div class="bp-attr-file">
+                        <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
+                            Choose picture
+                            <input type="file" name="picture" class="bp-document-hidden-file" accept="image/png,image/jpeg" data-kind-words="a PNG or JPEG" data-max-bytes="@AppOptions.Value.MaxUploadBytes" data-upload-file />
+                        </label>
+                        <span class="bp-help-text" data-upload-chosen>No file chosen</span>
+                        <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap" data-upload-button>Save picture</button>
+                    </div>
+                </div>
+            </form>
+            @if (selected.PictureFileId is not null)
+            {
+                <form method="post" action="/admin/models/@selected.Id/picture/remove" class="bp-model-picture-remove">
+                    @Html.AntiForgeryToken()
+                    <button type="submit" class="bp-text-button">Remove its picture and show the family's</button>
+                </form>
+            }
             <form method="post" action="/admin/models/@selected.Id/module-description" class="bp-module-description">
                 @Html.AntiForgeryToken()
                 <label for="module-description">Module description of @selected.Name <em class="bp-field-marker">Not passport data</em></label>
diff --git a/web/wwwroot/css/bp-product-data.css b/web/wwwroot/css/bp-product-data.css
index a342a30..0588ba5 100644
--- a/web/wwwroot/css/bp-product-data.css
+++ b/web/wwwroot/css/bp-product-data.css
@@ -2447,6 +2447,45 @@ main .bp-field-marker.is-published {
   min-width: 0;
 }
 
+/* A model's own picture, beside its module description (redesign R2). */
+.bp-model-picture {
+  display: grid;
+  grid-template-columns: 96px minmax(0, 1fr);
+  gap: 14px;
+  align-items: start;
+  margin-bottom: 12px;
+}
+
+.bp-model-picture-preview {
+  display: grid;
+  place-items: center;
+  width: 96px;
+  height: 72px;
+  border: 1px solid var(--bp-border);
+  border-radius: 10px;
+  overflow: hidden;
+}
+
+.bp-model-picture-preview img {
+  max-width: 100%;
+  max-height: 100%;
+  object-fit: contain;
+}
+
+.bp-model-picture-remove {
+  margin: -6px 0 14px 110px;
+}
+
+@media (max-width: 575.98px) {
+  .bp-model-picture {
+    grid-template-columns: minmax(0, 1fr);
+  }
+
+  .bp-model-picture-remove {
+    margin-left: 0;
+  }
+}
+
 /* A frozen passport's values: shown, not editable (redesign R1). The fieldset only disables its fields. */
 .bp-frozen-fields {
   display: contents;
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~ModelPictureTests | FullyQualifiedName~ProductDataControllerTests"`
Expected: `Passed!  - Failed:     0, Passed:    15`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2272`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ModelPictureTests.cs BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs web/Controllers/ProductDataController.cs web/Dpp/Pages/PassportPages.cs web/Dpp/Pages/ProductDataPages.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs web/Views/ProductData/Family.cshtml web/wwwroot/css/bp-product-data.css
git commit -m "feat(dpp): a battery model's own picture, shown instead of its family's" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The demo Compact 13M gets back its double-stack picture

The old app had `compact13.png` for the Compact 13M; the rebuild seeded only the family's. The demo catalogue now names it for the 13M model, and the seed stores it. Clearing the demo data removes the model's picture file with the model.

**Files:**
- Test (modify): `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Modify: `web/Dpp/Demo/DemoCatalogue.cs`
- Modify: `web/Dpp/Demo/DemoDataService.cs`
- Create: `web/Dpp/Demo/Pictures/compact13.png`
- Modify: `web/Dpp/Demo/demo-catalogue.json`

- [ ] **Step 1: Write the failing tests**

Patch `r2-task2-tests.patch` (`BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`). Save the block as `../r2-task2-tests.patch`, outside the worktree, and run `git apply ../r2-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
index e7f74e7..0870e8b 100644
--- a/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
+++ b/BatteryPassWeb.Tests/Demo/DemoSeedTests.cs
@@ -15,6 +15,21 @@ public sealed class DemoSeedTests(MongoFixture mongo)
 {
     private static FilterDefinition<BsonDocument> Eq(string field, BsonValue value) => Builders<BsonDocument>.Filter.Eq(field, value);
 
+    [Fact]
+    public async Task TheCompact13M_ShouldShowItsOwnPictureAndThe7MItsFamilys()
+    {
+        // Owner's point 8: the 13M is the double stack, so it gets back the old demo's compact13.png (redesign R2).
+        using var rig = await DemoRig.StartAsync(mongo);
+
+        await rig.Demo.SeedAsync(DemoRig.Who);
+        var models = (await rig.All(DppStore.Models)).ToDictionary(model => model["Name"].AsString);
+
+        Assert.Equal("image/png", models["Compact 13M"]["PictureContentType"].AsString);
+        Assert.True(models["Compact 7M"].GetValue("PictureFileId", BsonNull.Value).IsBsonNull);
+        Assert.True(models["Core"].GetValue("PictureFileId", BsonNull.Value).IsBsonNull);
+        Assert.Equal(700941, DemoCatalogue.Picture("compact13.png").Length);
+    }
+
     [Fact]
     public async Task TheSeed_ShouldMakeTheFamiliesModelsAndBatteries()
     {
@@ -107,7 +122,7 @@ public sealed class DemoSeedTests(MongoFixture mongo)
         Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
         Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
         Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
-        Assert.Equal(12 + 3 + 2 + 2, await rig.Count($"{DocumentStore.Bucket}.files"));
+        Assert.Equal(12 + 3 + 2 + 2 + 1, await rig.Count($"{DocumentStore.Bucket}.files")); // + the Compact 13M picture (redesign R2)
     }
 
     [Fact]
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoSeedTests"`
Expected: `Failed:     2, Passed:    14` (failing: `DemoSeedTests.TheBatteries_ShouldHaveModulesAccidentReportsAndDocuments`, `DemoSeedTests.TheCompact13M_ShouldShowItsOwnPictureAndThe7MItsFamilys`).

- [ ] **Step 3: Write the code**

`web/Dpp/Demo/Pictures/compact13.png` (a picture; copy it from the old app's tag, from the worktree root):

```bash
git show app-v1.2-before-dpp-v3:web/wwwroot/images/compact13.png > web/Dpp/Demo/Pictures/compact13.png
```

It is 700 941 bytes. The project embeds `Dpp/Demo/Pictures/*.png`, so nothing else is needed.

Patch `r2-task2-code.patch` (`web/Dpp/Demo/DemoCatalogue.cs`, `web/Dpp/Demo/DemoDataService.cs`, `web/Dpp/Demo/demo-catalogue.json`). Save the block as `../r2-task2-code.patch`, outside the worktree, and run `git apply ../r2-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Demo/DemoCatalogue.cs b/web/Dpp/Demo/DemoCatalogue.cs
index ae36474..7cbb184 100644
--- a/web/Dpp/Demo/DemoCatalogue.cs
+++ b/web/Dpp/Demo/DemoCatalogue.cs
@@ -10,8 +10,11 @@ public sealed record DemoAccount(string Email, string Name, string Role, string?
 
 public sealed record DemoFamily(string Key, string Name, string Picture);
 
-/// <summary>A battery model: its values, its module description and the restricted documents the seed uploads for it.</summary>
-public sealed record DemoModel(string Key, string Family, string Name, int Modules, string ModuleDescription, Dictionary<string, string> Documents, JsonObject Values);
+/// <summary>
+/// A battery model: its values, its module description, the restricted documents the seed uploads for it, and its own
+/// picture when it looks different from its family's (redesign R2).
+/// </summary>
+public sealed record DemoModel(string Key, string Family, string Name, int Modules, string ModuleDescription, Dictionary<string, string> Documents, JsonObject Values, string? Picture = null);
 
 public sealed record DemoNamed(string Key, string Name, JsonObject Values);
 
@@ -77,7 +80,7 @@ public sealed record DemoCatalogue(
         return JsonSerializer.Deserialize<DemoCatalogue>(stream, Json) ?? throw new InvalidOperationException("The demo catalogue is empty.");
     }
 
-    /// <summary>A family picture from the old demo (tag app-v1.2-before-dpp-v3).</summary>
+    /// <summary>A family or model picture from the old demo (tag app-v1.2-before-dpp-v3).</summary>
     public static byte[] Picture(string fileName)
     {
         using var stream = Resource(fileName);
diff --git a/web/Dpp/Demo/DemoDataService.cs b/web/Dpp/Demo/DemoDataService.cs
index 5726149..cf56ea1 100644
--- a/web/Dpp/Demo/DemoDataService.cs
+++ b/web/Dpp/Demo/DemoDataService.cs
@@ -139,6 +139,11 @@ public sealed class DemoDataService(
 
             var stored = await store.GetAsync<BatteryModel>(id, cancellationToken);
             await productData.SetModuleDescriptionAsync(who, stored!, model.ModuleDescription, cancellationToken);
+            if (model.Picture is { } picture)
+            {
+                await productData.SetModelPictureAsync(who, id, picture, DemoCatalogue.Picture(picture), cancellationToken);
+            }
+
             models[model.Key] = id;
         }
 
@@ -215,7 +220,18 @@ public sealed class DemoDataService(
 
         await ClearUnlessUsedAsync<ProductionBatch>(DppStore.Batches, "batches", (DppStore.Packs, "BatchId"), "real batteries", TargetKind.Batch, notes, Count, cancellationToken);
         await ClearUnlessUsedAsync<EconomicOperator>(DppStore.Operators, "operators", (DppStore.Packs, "OperatorId"), "real batteries", null, notes, Count, cancellationToken);
+        // A removed demo model takes its own picture with it (redesign R2); a kept one keeps it.
+        var modelPictures = (await Collection(DppStore.Models).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
+            .Where(model => model.GetValue("PictureFileId", BsonNull.Value).IsString)
+            .ToDictionary(model => model["_id"].AsString, model => model["PictureFileId"].AsString);
         await ClearUnlessUsedAsync<BatteryModel>(DppStore.Models, "models", (DppStore.Batches, "ModelId"), "real batches", TargetKind.Model, notes, Count, cancellationToken);
+        foreach (var (modelId, fileId) in modelPictures)
+        {
+            if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
+            {
+                await documentStore.DeleteFileAsync(fileId, cancellationToken);
+            }
+        }
         await ClearUnlessUsedAsync<ManufacturingSite>(DppStore.Sites, "sites", (DppStore.Batches, "SiteId"), "real batches", null, notes, Count, cancellationToken);
         foreach (var family in await Collection(DppStore.Families).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
         {
diff --git a/web/Dpp/Demo/demo-catalogue.json b/web/Dpp/Demo/demo-catalogue.json
index c0894e2..a6390c4 100644
--- a/web/Dpp/Demo/demo-catalogue.json
+++ b/web/Dpp/Demo/demo-catalogue.json
@@ -273,6 +273,7 @@
       "key": "compact-13m",
       "family": "compact",
       "name": "Compact 13M",
+      "picture": "compact13.png",
       "modules": 13,
       "moduleDescription": "Thirteen liquid-cooled NMC modules in two rows, with the battery management unit at the front.",
       "documents": {
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --filter "FullyQualifiedName~DemoSeedTests"`
Expected: `Passed!  - Failed:     0, Passed:    16`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2273`.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoSeedTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoDataService.cs web/Dpp/Demo/Pictures/compact13.png web/Dpp/Demo/demo-catalogue.json
git commit -m "feat(dpp): the demo Compact 13M shows its double-stack picture" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0, Passed:  2273, Skipped:     0, Total:  2273`. From the 2268 before: 2272 after Task 1, 2273 after Task 2.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat 4f65caa -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

1. **Restart the app with the usual launcher** and press "Reset demo data" (end of the Clusters tab).
2. **Battery families → Compact → Compact 13M:** its section shows its own picture (the double stack), with "Remove its picture and show the family's". Compact 7M shows the family's picture, with "Choose picture".
3. **Search DEMO-C13M-0001:** the public summary shows the double stack; DEMO-C7M-0001 still shows the 7M.

## Next

Plan R3 (the two statuses as badges, on every page), then R4 (the side-menu battery pages).
