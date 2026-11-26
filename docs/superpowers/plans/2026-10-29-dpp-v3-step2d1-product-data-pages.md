# DPP V3 Step 2d-1: The Team's Product Data Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The team edits product data on the new pages: battery families (name, picture, models and the model editor), batches, and manufacturing sites and economic operators, with every value entered through a form built from the official schema and saved through the save pipeline. "Battery families" is replaced in place; "Editable fields" and the old family editor go.

**Architecture:** Builds on 2c. `AttributeForms` reads each attribute's form from the v2.0.1 schema; `FormValues` turns typed text into the schema's form and back; `RefusalWording` puts the pipeline's refusals into plain words. `RecordEditor` shows a record's attributes by schema section, reads a posted form against a stamp of what the page showed, and saves through `SavePipeline`, which now lets the team remove values. `ProductDataPages` loads the pages and makes the changes that aren't passport data (new records, names, the picture, copies, removals). `ProductDataController` and the views in `Views/ProductData` put it on today's admin layout with the calmer finish of decision 27, in their own stylesheet and script.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), JsonSchema.Net 9.5.0, xUnit 2.9, EphemeralMongo 3.2.0. Plain CSS and JavaScript, with inline SVG icons; nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md` (sections 4.2, 6.2, 12 and 14) and the decisions record (decisions 7, 18, 20 and 23–27). Mock-up of the screens with no counterpart today: `docs/mockups/dpp-v3-team-pages-new-screens.html`. Earlier plans: step 1, 2a, 2b and 2c in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). It starts at 2c's last commit (626a57f) and holds the design, decisions and mock-up commit for 2d. Check with `git branch --show-current`.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  1635`. After this plan: 1820. The suite takes about 25 seconds.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../2d1-task1-store.patch`), and run `git apply ../<name>` from the worktree root. `git apply` copes with this worktree's mixed line endings. Each patch applies to the files as the previous tasks left them, so keep the task order.
- **Commits:** end each message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (a second `-m`). Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.
- **`APP_BASE_URL`** stays required for document links (from 2c). The pages still open without it; uploads then say why they can't link.

## Judgement calls in this plan

1. **The new pages replace the old ones in place** (decision 23). "Battery families" opens `/admin/families`; "Sites and operators" gets a permanent menu entry at `/admin/sites-and-operators`. "Editable fields" and the old family editor (`/admin/products/...`) go, with the admin actions, views, view-model parts and `LocalAdminEditableFieldPolicyService` that only they used. The services the cluster-admin pages and the API still share (`ProductTemplateService`, `EditableFieldPolicyService`, `DataCompletionPolicyService`) stay until steps 3 and 4.
2. **The form comes from the schema.** `AttributeForms` gives every attribute one of these shapes, from its v2.0.1 schema: one input; grouped inputs (e.g. #14's name and address); a list of texts, one per line; rows (#44); a public document link; a restricted upload (decision 7); or a fixed value with no input (#20). Units with one allowed value and single-valued enums are filled in by the app. The manufacturing date (#17) is year and month (decision 18). `FormValues` only converts text; the save pipeline judges every value.
3. **Missing** means empty although the passport needs it by February 2027 (the file's M or (M) with the Feb 2027 mark). Conditional values and values needed only later aren't counted. Each editor has a "Show only the values still needed" switch.
4. **Only what was edited on the page counts** (design 12). The page posts back, in the hidden field `shown`, a fingerprint per field of what it showed (the first 6 bytes of SHA-256, in hex, over the field's trimmed non-blank texts, line endings as `\n`), plus one for the record's name.
   - A field left as shown is skipped, so a page left open never undoes a newer value.
   - A field edited here whose stored value changed meanwhile is refused, with the typed text kept: "Someone else saved this value after you opened this page. What you typed is still here: save again to use it, or Cancel edits to see theirs."
   - A field emptied here is a removal.
   - A post without the stamp counts every field as edited and removes nothing.
5. **Removing a value** (decision 24): only the team, through its editors, by emptying the field. A value that isn't conditional, is needed by February 2027 and is used by an active published passport stays: "The published passports need this value, so it can't be removed. Change it instead." Removals republish like changes. Uploads and feeds keep "empty never overwrites".
6. **The audit entry keeps the value each change replaced** (design 6.2, step 8): `previous`, the stored value as JSON text or null, for every saved or removed key. The audit log's filter offers the new record types (`dpp-family`, `dpp-model`, `dpp-batch`, `dpp-site`, `dpp-operator`, `dpp-pack`, `dpp-document`).
7. **Quantities can't be negative** (from the parity check): #21, 57, 64, 65, 66, 67, 70, 78, 81, 82 and 85, except parts whose name contains "temperature".
8. **Removing a record** (decision 25): families, models, batches, sites and operators, only while nothing uses them. Otherwise the page says why, e.g. "It still has 1 battery model. Remove them first." or "1 battery uses it.". Removing deletes the record with its uploaded documents (and a family's picture), and is logged as `dpp.record-removed`.
9. **A new model from a copy** (decision 26): "Start from" offers every model, grouped by family. The copy gets the typed values, marked as entered by the team member now; uploads, fixed values and batches aren't copied.
10. **Names are unique**, ignoring case and surrounding spaces: families, models within their family, sites and operators. A save renames a record only when its name field was edited.
11. **The republish question** (design 4.2): a typed change that reaches published passports waits for "Publish new versions" in a dialog. An upload that would do the same needs the "Publish new versions" tick on its form.
12. **Unsaved edits are never lost silently:** leaving the page, Cancel edits and uploading a document (which reloads the page) ask first. After a save, the editor returns to the section and place where Save was pressed and confirms it in the save bar.
13. **Batches** are one per model, year and site. **The family picture** must be a PNG or JPEG, recognised by its content (`FileTypes`, now shared with `DocumentService`); it is served to anyone at `/dpp/families/{id}/picture`, because the public summary will show it (decision 20).
14. **The calmer finish** (decision 27) lives in `bp-product-data.css`, loaded only on these pages; `site.css` is never changed. One heading scale: page title 2rem, the record being edited 1.4rem, card titles 1.25rem, card items 1.125rem. Motion only under `prefers-reduced-motion: no-preference`. A dark theme for every part. Narrow screens from 1024 px down: the editor's side menu turns into tabs.
15. **Return links are checked:** `returnTo` only accepts `/admin/families/...` addresses, without `//` or a backslash.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Store/DppRecords.cs`, `DppStore.cs` (modify) | Lists for the pages, the family picture, removals in one update, deleting a record |
| `web/Dpp/Documents/DocumentStore.cs` (modify) | Listing a record's documents, reading a file by id, deleting a record's documents |
| `web/Dpp/Editing/SavePipeline.cs` (modify) | The team's removals and the previous value in the audit entry |
| `web/Dpp/Values/AttributeValueValidator.cs` (modify) | Non-negative quantities |
| `web/Dpp/Forms/AttributeForm.cs`, `AttributeForms.cs` | The form of every attribute, from the schema |
| `web/Dpp/Forms/FormValues.cs` | Typed text to the schema's form, and back |
| `web/Dpp/Forms/RefusalWording.cs` | The pipeline's refusals in plain words, on the right input |
| `web/Dpp/Pages/RecordEditor.cs` | Sections, missing values, the stamp, saving an editor |
| `web/Dpp/Pages/ProductDataPages.cs`, `Words.cs` | Loading the pages; new records, names, picture, copies, removals |
| `web/Dpp/Documents/FileTypes.cs`, `DocumentService.cs` (modify) | Recognising PDF, PNG and JPEG by their bytes, in one place |
| `web/Controllers/ProductDataController.cs` | The routes under `/admin` and the family picture |
| `web/Views/ProductData/*.cshtml` | The families list, family page, batch page, sites and operators, and the shared editor |
| `web/wwwroot/css/bp-product-data.css`, `web/wwwroot/js/bp-record-editor.js` | The calmer finish; the editor's menu, switch, rows, dialogs and return to place |
| `web/Views/Admin/AuditLog.cshtml` (modify) | The new record types in the filter |
| `web/Views/Shared/_AdminTabs.cshtml` (modify) | The menu: "Battery families" and "Sites and operators"; no "Editable fields" |
| `web/Controllers/AdminController.cs` and the old views, view model and tests (modify, delete) | The old family editor and "Editable fields" removed |


---

### Task 1: Lists for the pages, and the family picture

The store gains what the pages need: lists of families, models (by name) and batches (newest first), several records by id, pack counts per batch, the active passport versions, renaming, and the family picture (decision 20). The document store lists a record's documents and reads a stored file by id.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/DppStoreListingTests.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Dpp/Documents/DocumentStore.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/DppStoreListingTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class DppStoreListingTests(MongoFixture mongo)
{
    [Fact]
    public async Task Lists_ShouldReturnTheRecordsOfEachKind()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        await store.InsertAsync(new BatteryModel { Id = "model-a", FamilyId = "family-test", Name = "AAA-1" });
        await store.InsertAsync(new BatteryModel { Id = "model-other", FamilyId = "family-other", Name = "OTHER" });
        await store.InsertAsync(new ProductionBatch { Id = "batch-2027", ModelId = "model-test", SiteId = "site-test", Year = 2027 });

        Assert.Equal(["family-test"], (await store.ListAsync<BatteryFamily>()).Select(f => f.Id));
        Assert.Equal(["model-a", "model-test"], (await store.ListModelsAsync("family-test")).Select(m => m.Id)); // by name
        Assert.Equal(["batch-2027", "batch-test-2026"], (await store.ListBatchesAsync("model-test")).Select(b => b.Id)); // newest first
        Assert.Equal(["site-test"], (await store.ListAsync<ManufacturingSite>()).Select(s => s.Id));
        Assert.Equal(["operator-test"], (await store.ListAsync<EconomicOperator>()).Select(o => o.Id));
    }

    [Fact]
    public async Task GetMany_ShouldLeaveOutIdsThatDontExist()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        var models = await store.GetManyAsync<BatteryModel>(["model-test", "no-such-model", "model-test"]);

        Assert.Equal(["model-test"], models.Select(m => m.Id));
        Assert.Empty(await store.GetManyAsync<BatteryModel>([]));
    }

    [Fact]
    public async Task PackCounts_ShouldCountPublishedPacksApart()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var draft = await rig.Store.CountPacksByBatchAsync(["batch-test-2026", "batch-empty"]);
        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);
        var published = await rig.Store.CountPacksByBatchAsync(["batch-test-2026"]);

        Assert.Equal((1, 0), draft["batch-test-2026"]);
        Assert.Equal((0, 0), draft["batch-empty"]);
        Assert.Equal((1, 1), published["batch-test-2026"]);
    }

    [Fact]
    public async Task ActiveVersions_ShouldListOnlyPublishedPacks()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        Assert.Empty(await rig.Store.ListActiveVersionsAsync([rig.PackId]));

        await rig.Publication.PublishAsync(PublicationTesting.Team, rig.PackId);

        var version = Assert.Single(await rig.Store.ListActiveVersionsAsync([rig.PackId, "no-such-pack"]));
        Assert.Equal(1, version.Number);
    }

    [Fact]
    public async Task Rename_ShouldChangeTheNameOnly()
    {
        var (store, _, _) = await DppStoreTesting.SeededStoreAsync(mongo);

        Assert.True(await store.RenameAsync<ManufacturingSite>("site-test", "  Plant 2  "));
        Assert.False(await store.RenameAsync<BatteryFamily>("no-such-family", "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.RenameAsync<ProductionBatch>("batch-test-2026", "x"));

        var site = (await store.GetAsync<ManufacturingSite>("site-test"))!;
        Assert.Equal("Plant 2", site.Name);
        Assert.Equal(3, site.Values.Count);
    }

    [Fact]
    public async Task FamilyPicture_ShouldBeStoredAndReadBack()
    {
        var (store, context, _) = await DppStoreTesting.SeededStoreAsync(mongo);
        var documents = new DocumentStore(context);
        var fileId = await documents.UploadFileAsync("family.png", DocumentTesting.Png());

        Assert.True(await store.SetFamilyPictureAsync("family-test", fileId, "image/png"));

        var family = (await store.GetAsync<BatteryFamily>("family-test"))!;
        Assert.Equal("image/png", family.PictureContentType);
        Assert.Equal(DocumentTesting.Png(), await documents.DownloadFileAsync(family.PictureFileId!));
        Assert.False(await store.SetFamilyPictureAsync("no-such-family", fileId, "image/png"));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppStoreListingTests"`
Expected: the build fails with CS1061 errors: `DppStore` has no `ListAsync`, `ListModelsAsync`, `ListBatchesAsync`, `GetManyAsync`, `CountPacksByBatchAsync`, `ListActiveVersionsAsync`, `RenameAsync` or `SetFamilyPictureAsync` yet, `BatteryFamily` has no picture fields, and `DocumentStore` has no `DownloadFileAsync`.

- [ ] **Step 3: Apply the store patch**

Patch `2d1-task1-store.patch` (`web/Dpp/Store/DppRecords.cs`, `web/Dpp/Store/DppStore.cs`, `web/Dpp/Documents/DocumentStore.cs`). Save the block as `../2d1-task1-store.patch`, outside the worktree, and run `git apply ../2d1-task1-store.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -19,6 +19,11 @@ public sealed class BatteryFamily : IDppRecord
     public required string Id { get; init; }
 
     public required string Name { get; set; }
+
+    /// <summary>The family's picture (decision 20): a file in the dpp_files bucket, shown on the public summary. Not passport data.</summary>
+    public string? PictureFileId { get; set; }
+
+    public string? PictureContentType { get; set; }
 }
 
 /// <summary>The 32 model attributes, plus the starting values of the 5 re-stated ones (#59, #69, #74, #79, #81).</summary>
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -283,6 +283,120 @@ public sealed class DppStore
         return clusters.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
     }
 
+    /// <summary>Every record of one kind. Families, sites and operators are few; the pages sort them by name.</summary>
+    public async Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken = default)
+        where T : class, IDppRecord
+    {
+        if (_database is null)
+        {
+            return [];
+        }
+
+        return await Collection<T>().Find(Builders<T>.Filter.Empty).ToListAsync(cancellationToken);
+    }
+
+    /// <summary>The records with these ids; ids that don't exist are left out.</summary>
+    public async Task<IReadOnlyList<T>> GetManyAsync<T>(IEnumerable<string> ids, CancellationToken cancellationToken = default)
+        where T : class, IDppRecord
+    {
+        ArgumentNullException.ThrowIfNull(ids);
+        var wanted = ids.Distinct(StringComparer.Ordinal).ToArray();
+        if (_database is null || wanted.Length == 0)
+        {
+            return [];
+        }
+
+        return await Collection<T>().Find(Builders<T>.Filter.In("_id", wanted)).ToListAsync(cancellationToken);
+    }
+
+    public async Task<IReadOnlyList<BatteryModel>> ListModelsAsync(string familyId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null)
+        {
+            return [];
+        }
+
+        return await Collection<BatteryModel>().Find(model => model.FamilyId == familyId).SortBy(model => model.Name).ToListAsync(cancellationToken);
+    }
+
+    /// <summary>A model's batches, newest year first.</summary>
+    public async Task<IReadOnlyList<ProductionBatch>> ListBatchesAsync(string modelId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null)
+        {
+            return [];
+        }
+
+        return await Collection<ProductionBatch>().Find(batch => batch.ModelId == modelId).SortByDescending(batch => batch.Year).ToListAsync(cancellationToken);
+    }
+
+    /// <summary>The active versions of these packs: one per published pack, none for a draft.</summary>
+    public async Task<IReadOnlyList<PassportVersion>> ListActiveVersionsAsync(IEnumerable<string> packIds, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(packIds);
+        var ids = packIds.Distinct(StringComparer.Ordinal).ToArray();
+        if (_database is null || ids.Length == 0)
+        {
+            return [];
+        }
+
+        var versions = await Collection<PassportVersion>()
+            .Find(version => ids.Contains(version.PackId) && version.DppStatus == DppStatuses.Active)
+            .ToListAsync(cancellationToken);
+        return versions.GroupBy(version => version.PackId).Select(group => group.MaxBy(version => version.Number)!).ToArray();
+    }
+
+    /// <summary>For each of these batches: how many packs it has, and how many of them are published.</summary>
+    public async Task<IReadOnlyDictionary<string, (int Packs, int Published)>> CountPacksByBatchAsync(IEnumerable<string> batchIds, CancellationToken cancellationToken = default)
+    {
+        ArgumentNullException.ThrowIfNull(batchIds);
+        var ids = batchIds.Distinct(StringComparer.Ordinal).ToArray();
+        var counts = ids.ToDictionary(id => id, _ => (Packs: 0, Published: 0), StringComparer.Ordinal);
+        if (_database is null || ids.Length == 0)
+        {
+            return counts;
+        }
+
+        var packs = await Collection<BatteryPack>()
+            .Find(pack => ids.Contains(pack.BatchId))
+            .Project(pack => new { pack.Id, pack.BatchId })
+            .ToListAsync(cancellationToken);
+        var published = (await ListActiveVersionsAsync(packs.Select(pack => pack.Id), cancellationToken))
+            .Select(version => version.PackId)
+            .ToHashSet(StringComparer.Ordinal);
+        foreach (var pack in packs)
+        {
+            var (total, live) = counts[pack.BatchId];
+            counts[pack.BatchId] = (total + 1, live + (published.Contains(pack.Id) ? 1 : 0));
+        }
+
+        return counts;
+    }
+
+    /// <summary>Renames a family, model, site or operator; false when no record has that id.</summary>
+    public async Task<bool> RenameAsync<T>(string id, string name, CancellationToken cancellationToken = default)
+        where T : class, IDppRecord
+    {
+        if (typeof(T) != typeof(BatteryFamily) && typeof(T) != typeof(BatteryModel) && typeof(T) != typeof(ManufacturingSite) && typeof(T) != typeof(EconomicOperator))
+        {
+            throw new ArgumentException($"{typeof(T).Name} has no name.");
+        }
+
+        ArgumentException.ThrowIfNullOrWhiteSpace(name);
+        var result = await Collection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("_id", id), Builders<T>.Update.Set("Name", name.Trim()), cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
+    /// <summary>Points a family at its new picture (decision 20); false when no family has that id.</summary>
+    public async Task<bool> SetFamilyPictureAsync(string familyId, string fileId, string contentType, CancellationToken cancellationToken = default)
+    {
+        var result = await Collection<BatteryFamily>().UpdateOneAsync(
+            family => family.Id == familyId,
+            Builders<BatteryFamily>.Update.Set(family => family.PictureFileId, fileId).Set(family => family.PictureContentType, contentType),
+            cancellationToken: cancellationToken);
+        return result.MatchedCount == 1;
+    }
+
     private IMongoCollection<T> Collection<T>() => RequireDatabase().GetCollection<T>(CollectionName<T>());
 
     private IMongoDatabase RequireDatabase() =>
diff --git a/web/Dpp/Documents/DocumentStore.cs b/web/Dpp/Documents/DocumentStore.cs
--- a/web/Dpp/Documents/DocumentStore.cs
+++ b/web/Dpp/Documents/DocumentStore.cs
@@ -109,9 +109,13 @@ public sealed class DocumentStore
     public Task<byte[]> DownloadAsync(DppDocument document, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(document);
-        return Files().DownloadAsBytesAsync(ObjectId.Parse(document.FileId), cancellationToken: cancellationToken);
+        return DownloadFileAsync(document.FileId, cancellationToken);
     }
 
+    /// <summary>The bytes of a stored file by its id: a document's FileId, or a family picture.</summary>
+    public Task<byte[]> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default) =>
+        Files().DownloadAsBytesAsync(ObjectId.Parse(fileId), cancellationToken: cancellationToken);
+
     private IMongoCollection<DppDocument> Collection() => RequireDatabase().GetCollection<DppDocument>(Records);
 
     private GridFSBucket Files() => new(RequireDatabase(), new GridFSBucketOptions { BucketName = Bucket });
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppStoreListingTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1641`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/DppStoreListingTests.cs web/Dpp/Store/DppRecords.cs web/Dpp/Store/DppStore.cs web/Dpp/Documents/DocumentStore.cs
git commit -m "feat(dpp): list families, models, batches and documents for the team pages"
```


---

### Task 2: Team removals, the value each change replaced, and non-negative quantities

The save pipeline lets the team remove values (decision 24), keeps the replaced value in the audit entry, and the value check refuses negative quantities.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/ValueRemovalTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Dpp/Editing/SavePipeline.cs`
- Modify: `web/Dpp/Values/AttributeValueValidator.cs`

- [ ] **Step 1: Write the failing tests**

Patch `2d1-task2-tests.patch` (`BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs`). Save the block as `../2d1-task2-tests.patch`, outside the worktree, and run `git apply ../2d1-task2-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs b/BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs
--- a/BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs
@@ -43,4 +43,15 @@ public sealed class AttributeValueValidatorTests
 
         Assert.Contains(issues, issue => issue.Message.Contains(expectedMessagePart, StringComparison.Ordinal));
     }
+
+    [Theory]
+    [InlineData(21, """{"gramKilogramValue":-1,"gramKilogramUnit":"kg"}""", false)]
+    [InlineData(21, """{"gramKilogramValue":0,"gramKilogramUnit":"kg"}""", true)]
+    [InlineData(88, """{"celsiusValue":-20,"degreeCelsiusUnit":"°C"}""", true)] // a temperature may be below zero
+    public void PhysicalQuantities_ShouldNotBeNegative(int number, string json, bool valid)
+    {
+        var issues = Validator.Validate(AttributeCatalog.ByNumber(number), JsonNode.Parse(json));
+
+        Assert.Equal(valid, issues.Count == 0);
+    }
 }
```

`BatteryPassWeb.Tests/Dpp/ValueRemovalTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Removing a stored value: the team may, through its editors; uploads and feeds can't, because for them empty never
/// overwrites (design 6.2, step 5). A value a published passport needs by February 2027 can't be removed. The audit log
/// keeps the value each change replaced (design 6.2, step 8).
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ValueRemovalTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    private static ChangeSet Remove(params string[] keys) =>
        new(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?>(), "form", Removals: keys);

    [Fact]
    public async Task Team_ShouldRemoveAValueOfAnUnpublishedModel()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        Assert.True(result.Saved);
        Assert.Equal(new KeyResult("batteryMass", KeyOutcome.Removed, string.Empty), Assert.Single(result.Keys));
        Assert.False((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("batteryMass"));
    }

    [Fact]
    public async Task AValueAPublishedPassportNeeds_ShouldNotBeRemoved()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        Assert.False(result.Saved);
        var key = Assert.Single(result.Keys);
        Assert.Equal(KeyOutcome.Rejected, key.Outcome);
        Assert.Contains("published passport", key.Message, StringComparison.Ordinal);
        Assert.True((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("batteryMass"));
    }

    [Fact]
    public async Task AConditionalValueOfAPublishedPassport_ShouldBeRemovedAndPublishANewVersion()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("symbolsForCadmiumAndLead"));

        Assert.Equal(KeyOutcome.Removed, Assert.Single(result.Keys).Outcome);
        Assert.False((await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values.ContainsKey("symbolsForCadmiumAndLead"));
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task OnlyTheTeam_ShouldRemoveValues()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var change = new ChangeSet(TargetKind.Pack, rig.PackId, new Dictionary<string, JsonNode?>(), "feed", Removals: ["stateOfChargeSoC"]);

        var result = await rig.Pipeline.SaveAsync(ChangeSender.Feed("bms-feed", "cluster-test"), change);

        Assert.Equal(KeyOutcome.Rejected, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task AValueThatIsntStored_ShouldHaveNothingToRemove()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        var result = await rig.Pipeline.SaveAsync(PublicationTesting.Team, Remove("batteryMass"));

        Assert.False(result.Saved);
        Assert.Equal(KeyOutcome.Ignored, Assert.Single(result.Keys).Outcome);
    }

    [Fact]
    public async Task AChange_ShouldLogTheValueItReplaced()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var before = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!.ToJsonString();

        await rig.Pipeline.SaveAsync(PublicationTesting.Team, new ChangeSet(
            TargetKind.Model,
            ModelId,
            new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonNode.Parse("""{"gramKilogramValue":600,"gramKilogramUnit":"kg"}""") },
            "form"));

        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        var logged = Assert.Single(events, e => e["eventType"].AsString == SavePipeline.AuditEventType);
        Assert.Equal(before, logged["metadata"]["keys"][0]["previous"].AsString);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ValueRemovalTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueValidatorTests"`
Expected: the build fails: `ChangeSet` has no parameter named `Removals` (CS1739) and `KeyOutcome` has no `Removed` (CS0117).

- [ ] **Step 3: Apply the pipeline patch**

Patch `2d1-task2-pipeline.patch` (`web/Dpp/Store/DppStore.cs`, `web/Dpp/Editing/SavePipeline.cs`, `web/Dpp/Values/AttributeValueValidator.cs`). Save the block as `../2d1-task2-pipeline.patch`, outside the worktree, and run `git apply ../2d1-task2-pipeline.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -116,17 +116,25 @@ public sealed class DppStore
     /// Sets the given values on one record, leaving its other values as they are; false when no record has that id.
     /// Each key is set on its own, so a live feed and a form saving at the same time don't overwrite each other.
     /// </summary>
-    public async Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken = default)
+    public Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken = default)
+        where T : class, IDppRecord =>
+        SetValuesAsync<T>(id, values, [], cancellationToken);
+
+    /// <summary>Sets the given values and removes the <paramref name="removals"/> in one update; false when no record has that id.</summary>
+    public async Task<bool> SetValuesAsync<T>(string id, IReadOnlyDictionary<string, AttributeValue> values, IReadOnlyCollection<string> removals, CancellationToken cancellationToken = default)
         where T : class, IDppRecord
     {
         ArgumentNullException.ThrowIfNull(values);
+        ArgumentNullException.ThrowIfNull(removals);
         RequireDatabase();
-        if (values.Count == 0)
+        if (values.Count == 0 && removals.Count == 0)
         {
             throw new ArgumentException("There are no values to set.", nameof(values));
         }
 
-        var update = Builders<T>.Update.Combine(values.Select(entry => Builders<T>.Update.Set($"Values.{entry.Key}", entry.Value)));
+        var update = Builders<T>.Update.Combine(
+            values.Select(entry => Builders<T>.Update.Set($"Values.{entry.Key}", entry.Value))
+                .Concat(removals.Select(key => Builders<T>.Update.Unset($"Values.{key}"))));
         var result = await Collection<T>().UpdateOneAsync(Builders<T>.Filter.Eq("_id", id), update, cancellationToken: cancellationToken);
         return result.MatchedCount == 1;
     }
diff --git a/web/Dpp/Editing/SavePipeline.cs b/web/Dpp/Editing/SavePipeline.cs
--- a/web/Dpp/Editing/SavePipeline.cs
+++ b/web/Dpp/Editing/SavePipeline.cs
@@ -31,18 +31,23 @@ public enum KeyOutcome
     Ignored,
 
     Rejected,
+
+    /// <summary>A stored value the team removed (or would remove, in a preview).</summary>
+    Removed,
 }
 
 public sealed record KeyResult(string Key, KeyOutcome Outcome, string Message);
 
 /// <summary>One change set for one record. <paramref name="Source"/> is "form", "upload" or "feed".</summary>
 /// <param name="MeasuredAtUtc">When the values were generated (e.g. a feed's measurement time); the save time if null.</param>
+/// <param name="Removals">Stored values to remove. Only the team removes values; for everyone else empty never overwrites.</param>
 public sealed record ChangeSet(
     TargetKind Target,
     string TargetId,
     IReadOnlyDictionary<string, JsonNode?> Values,
     string Source,
-    DateTime? MeasuredAtUtc = null);
+    DateTime? MeasuredAtUtc = null,
+    IReadOnlyCollection<string>? Removals = null);
 
 public sealed record SaveResult(bool Found, bool Saved, IReadOnlyList<KeyResult> Keys);
 
@@ -79,9 +84,12 @@ public sealed class SavePipeline(
         }
 
         var evaluation = Evaluate(sender, changes.Target, target.ClusterId, changes.Values);
-        if (preview || evaluation.Accepted.Count == 0)
+        var removals = await EvaluateRemovalsAsync(sender, changes, target, cancellationToken);
+        var results = evaluation.Results.Concat(removals).ToArray();
+        var removed = removals.Where(result => result.Outcome == KeyOutcome.Removed).Select(result => result.Key).ToArray();
+        if (preview || (evaluation.Accepted.Count == 0 && removed.Length == 0))
         {
-            return new SaveResult(true, false, evaluation.Results);
+            return new SaveResult(true, false, results);
         }
 
         var generatedAt = changes.MeasuredAtUtc?.ToUniversalTime() ?? clock.GetUtcNow().UtcDateTime;
@@ -89,7 +97,7 @@ public sealed class SavePipeline(
             entry => entry.Key,
             entry => new AttributeValue { Value = entry.Value, GeneratedAtUtc = generatedAt, Source = changes.Source, Who = sender.Who },
             StringComparer.Ordinal);
-        var saved = await SetValuesAsync(changes.Target, changes.TargetId, stored, cancellationToken);
+        var saved = await ApplyAsync(changes.Target, changes.TargetId, stored, removed, cancellationToken);
         if (saved)
         {
             await audit.AppendApplicationAuditEventAsync(
@@ -97,18 +105,84 @@ public sealed class SavePipeline(
                 sender.Who,
                 sender.Kind.ToString(),
                 changes.Source,
-                $"{stored.Count} value(s) saved on {changes.Target} {changes.TargetId}",
-                AuditMetadata(evaluation.Results),
+                removed.Length == 0
+                    ? $"{stored.Count} value(s) saved on {changes.Target} {changes.TargetId}"
+                    : $"{stored.Count} value(s) saved and {removed.Length} removed on {changes.Target} {changes.TargetId}",
+                AuditMetadata(results, target.Values),
                 entityType: $"dpp-{changes.Target.ToString().ToLowerInvariant()}",
                 entityId: changes.TargetId,
                 cancellationToken: cancellationToken);
             foreach (var listener in _listeners)
             {
-                await listener.ValuesSavedAsync(changes.Target, changes.TargetId, stored.Keys.ToArray(), sender.Who, cancellationToken);
+                await listener.ValuesSavedAsync(changes.Target, changes.TargetId, stored.Keys.Concat(removed).ToArray(), sender.Who, cancellationToken);
             }
         }
 
-        return new SaveResult(true, saved, evaluation.Results);
+        return new SaveResult(true, saved, results);
+    }
+
+    /// <summary>
+    /// Judges each value to remove: a catalogue attribute of this record that the sender may change, stored now, and
+    /// not one a published passport built from this record needs by February 2027 (it would block the new version and
+    /// leave the published passport out of date). Only the team removes values.
+    /// </summary>
+    private async Task<IReadOnlyList<KeyResult>> EvaluateRemovalsAsync(ChangeSender sender, ChangeSet changes, Target target, CancellationToken cancellationToken)
+    {
+        if (changes.Removals is not { Count: > 0 } removals)
+        {
+            return [];
+        }
+
+        bool? published = null;
+        var results = new List<KeyResult>();
+        foreach (var key in removals.Distinct(StringComparer.Ordinal))
+        {
+            var (outcome, message, _) = EvaluateKey(sender, changes.Target, target.ClusterId, key, JsonValue.Create("remove"), checkValue: false);
+            if (outcome != KeyOutcome.Saved)
+            {
+                results.Add(new KeyResult(key, outcome, message));
+                continue;
+            }
+
+            var attribute = AttributeCatalog.FindByProperty(key)!;
+            if (sender.Kind != SenderKind.Team)
+            {
+                results.Add(new KeyResult(key, KeyOutcome.Rejected, $"#{attribute.Number} is removed only by the team; an empty value never overwrites"));
+            }
+            else if (changes.Values.ContainsKey(key))
+            {
+                results.Add(new KeyResult(key, KeyOutcome.Ignored, "set and removed at once; the new value is kept"));
+            }
+            else if (!target.Values.ContainsKey(key))
+            {
+                results.Add(new KeyResult(key, KeyOutcome.Ignored, "no value to remove"));
+            }
+            else if (attribute.Requirement != Requirement.Conditional && attribute.MandatoryFromFeb2027
+                && (published ??= await UsedByPublishedPassportAsync(changes.Target, changes.TargetId, cancellationToken)))
+            {
+                results.Add(new KeyResult(key, KeyOutcome.Rejected, "The published passports need this value, so it can't be removed. Change it instead."));
+            }
+            else
+            {
+                results.Add(new KeyResult(key, KeyOutcome.Removed, string.Empty));
+            }
+        }
+
+        return results;
+    }
+
+    private async Task<bool> UsedByPublishedPassportAsync(TargetKind target, string id, CancellationToken cancellationToken)
+    {
+        var packIds = target switch
+        {
+            TargetKind.Pack => [id],
+            TargetKind.Model => await store.FindPackIdsUsingAsync(modelId: id, cancellationToken: cancellationToken),
+            TargetKind.Site => await store.FindPackIdsUsingAsync(siteId: id, cancellationToken: cancellationToken),
+            TargetKind.Batch => await store.FindPackIdsUsingAsync(batchId: id, cancellationToken: cancellationToken),
+            TargetKind.Operator => await store.FindPackIdsUsingAsync(operatorId: id, cancellationToken: cancellationToken),
+            _ => [],
+        };
+        return packIds.Count > 0 && (await store.ListActiveVersionsAsync(packIds, cancellationToken)).Count > 0;
     }
 
     /// <summary>Judges every key without saving anything; also used when a pack is created.</summary>
@@ -132,8 +206,9 @@ public sealed class SavePipeline(
         return new Evaluation(results, accepted);
     }
 
+    /// <param name="checkValue">False when only the attribute and the sender's rights are judged (a removal).</param>
     private (KeyOutcome Outcome, string Message, JsonNode? Value) EvaluateKey(
-        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw)
+        ChangeSender sender, TargetKind target, string? packClusterId, string key, JsonNode? raw, bool checkValue = true)
     {
         if (AttributeCatalog.FindByProperty(key) is not { } attribute)
         {
@@ -160,6 +235,11 @@ public sealed class SavePipeline(
             return (KeyOutcome.Rejected, $"#{attribute.Number} can't be changed by this sender", null);
         }
 
+        if (!checkValue)
+        {
+            return (KeyOutcome.Saved, string.Empty, null);
+        }
+
         if (IsEmpty(raw))
         {
             return (KeyOutcome.KeptEmpty, "empty values never overwrite", null);
@@ -211,43 +291,53 @@ public sealed class SavePipeline(
         _ => value.GetValueKind() == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetValue<string>()),
     };
 
-    private async Task<(bool Found, string? ClusterId)> FindTargetAsync(TargetKind target, string id, CancellationToken cancellationToken)
+    /// <summary>The record a change set is for: whether it exists, its cluster (packs only) and its stored values.</summary>
+    private sealed record Target(bool Found, string? ClusterId, IReadOnlyDictionary<string, AttributeValue> Values);
+
+    private static readonly Target NotFound = new(false, null, new Dictionary<string, AttributeValue>());
+
+    private async Task<Target> FindTargetAsync(TargetKind target, string id, CancellationToken cancellationToken)
     {
-        switch (target)
-        {
-            case TargetKind.Pack:
-                var pack = await store.GetAsync<BatteryPack>(id, cancellationToken);
-                return (pack is not null, pack?.ClusterId);
-            case TargetKind.Model:
-                return (await store.GetAsync<BatteryModel>(id, cancellationToken) is not null, null);
-            case TargetKind.Site:
-                return (await store.GetAsync<ManufacturingSite>(id, cancellationToken) is not null, null);
-            case TargetKind.Batch:
-                return (await store.GetAsync<ProductionBatch>(id, cancellationToken) is not null, null);
-            case TargetKind.Operator:
-                return (await store.GetAsync<EconomicOperator>(id, cancellationToken) is not null, null);
-            default:
-                throw new ArgumentOutOfRangeException(nameof(target), target, null);
-        }
+        return target switch
+        {
+            TargetKind.Pack => await store.GetAsync<BatteryPack>(id, cancellationToken) is { } pack ? new Target(true, pack.ClusterId, pack.Values) : NotFound,
+            TargetKind.Model => await store.GetAsync<BatteryModel>(id, cancellationToken) is { } model ? new Target(true, null, model.Values) : NotFound,
+            TargetKind.Site => await store.GetAsync<ManufacturingSite>(id, cancellationToken) is { } site ? new Target(true, null, site.Values) : NotFound,
+            TargetKind.Batch => await store.GetAsync<ProductionBatch>(id, cancellationToken) is { } batch ? new Target(true, null, batch.Values) : NotFound,
+            TargetKind.Operator => await store.GetAsync<EconomicOperator>(id, cancellationToken) is { } economicOperator ? new Target(true, null, economicOperator.Values) : NotFound,
+            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
+        };
     }
 
-    private Task<bool> SetValuesAsync(TargetKind target, string id, IReadOnlyDictionary<string, AttributeValue> values, CancellationToken cancellationToken) => target switch
+    private Task<bool> ApplyAsync(TargetKind target, string id, IReadOnlyDictionary<string, AttributeValue> values, IReadOnlyCollection<string> removals, CancellationToken cancellationToken) => target switch
     {
-        TargetKind.Pack => store.SetValuesAsync<BatteryPack>(id, values, cancellationToken),
-        TargetKind.Model => store.SetValuesAsync<BatteryModel>(id, values, cancellationToken),
-        TargetKind.Site => store.SetValuesAsync<ManufacturingSite>(id, values, cancellationToken),
-        TargetKind.Batch => store.SetValuesAsync<ProductionBatch>(id, values, cancellationToken),
-        TargetKind.Operator => store.SetValuesAsync<EconomicOperator>(id, values, cancellationToken),
+        TargetKind.Pack => store.SetValuesAsync<BatteryPack>(id, values, removals, cancellationToken),
+        TargetKind.Model => store.SetValuesAsync<BatteryModel>(id, values, removals, cancellationToken),
+        TargetKind.Site => store.SetValuesAsync<ManufacturingSite>(id, values, removals, cancellationToken),
+        TargetKind.Batch => store.SetValuesAsync<ProductionBatch>(id, values, removals, cancellationToken),
+        TargetKind.Operator => store.SetValuesAsync<EconomicOperator>(id, values, removals, cancellationToken),
         _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
     };
 
-    private static BsonDocument AuditMetadata(IReadOnlyList<KeyResult> results) => new()
+    /// <summary>Each key's outcome, and for a saved or removed value the one it replaced, so the history shows before and after.</summary>
+    private static BsonDocument AuditMetadata(IReadOnlyList<KeyResult> results, IReadOnlyDictionary<string, AttributeValue> before) => new()
     {
-        ["keys"] = new BsonArray(results.Select(result => new BsonDocument
+        ["keys"] = new BsonArray(results.Select(result =>
         {
-            ["key"] = result.Key,
-            ["outcome"] = result.Outcome.ToString(),
-            ["message"] = result.Message,
+            var entry = new BsonDocument
+            {
+                ["key"] = result.Key,
+                ["outcome"] = result.Outcome.ToString(),
+                ["message"] = result.Message,
+            };
+            if (result.Outcome is KeyOutcome.Saved or KeyOutcome.Removed)
+            {
+                entry["previous"] = before.TryGetValue(result.Key, out var previous) && previous.Value is not null
+                    ? previous.Value.ToJsonString()
+                    : BsonNull.Value;
+            }
+
+            return entry;
         })),
     };
 }
diff --git a/web/Dpp/Values/AttributeValueValidator.cs b/web/Dpp/Values/AttributeValueValidator.cs
--- a/web/Dpp/Values/AttributeValueValidator.cs
+++ b/web/Dpp/Values/AttributeValueValidator.cs
@@ -52,6 +52,58 @@ public sealed partial class AttributeValueValidator(OfficialSchemas schemas)
         {
             yield return new SchemaIssue($"/{fixedUnit.UnitProperty}", $"unit: must be \"{fixedUnit.Unit}\" for #{attribute.Number}");
         }
+
+        // Masses, capacities, voltages, powers, resistances, lifetimes and C-rates can't be negative (as in the app
+        // before V3); the schema itself bounds only percentages. Temperatures, also the test temperatures inside these
+        // values, can be below zero.
+        if (NonNegative.Contains(attribute.Number) && value is not null)
+        {
+            foreach (var (path, number) in Numbers(value, string.Empty))
+            {
+                if (number < 0)
+                {
+                    yield return new SchemaIssue(path, "minimum: must not be below 0");
+                }
+            }
+        }
+    }
+
+    private static readonly HashSet<int> NonNegative = [21, 57, 64, 65, 66, 67, 70, 78, 81, 82, 85];
+
+    private static IEnumerable<(string Path, double Number)> Numbers(JsonNode node, string path)
+    {
+        switch (node)
+        {
+            case JsonObject obj:
+                foreach (var (key, child) in obj)
+                {
+                    if (child is not null && !key.Contains("temperature", StringComparison.OrdinalIgnoreCase))
+                    {
+                        foreach (var found in Numbers(child, $"{path}/{key}"))
+                        {
+                            yield return found;
+                        }
+                    }
+                }
+
+                break;
+            case JsonArray array:
+                for (var index = 0; index < array.Count; index++)
+                {
+                    if (array[index] is { } item)
+                    {
+                        foreach (var found in Numbers(item, $"{path}/{index}"))
+                        {
+                            yield return found;
+                        }
+                    }
+                }
+
+                break;
+            case JsonValue leaf when leaf.GetValueKind() == JsonValueKind.Number:
+                yield return (path, double.Parse(leaf.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture));
+                break;
+        }
     }
 
     private static bool IsUtcTimestamp(string text) => DateTime.TryParseExact(
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ValueRemovalTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeValueValidatorTests"`
Expected: `Passed!  - Failed:     0, Passed:    30`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1650`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ValueRemovalTests.cs BatteryPassWeb.Tests/Dpp/AttributeValueValidatorTests.cs web/Dpp/Store/DppStore.cs web/Dpp/Editing/SavePipeline.cs web/Dpp/Values/AttributeValueValidator.cs
git commit -m "feat(dpp): let the team remove values and log the value each change replaced"
```


---

### Task 3: The form of every attribute

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/AttributeFormsTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/FormValuesTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/RefusalWordingTests.cs`
- Create: `web/Dpp/Forms/AttributeForm.cs`
- Create: `web/Dpp/Forms/AttributeForms.cs`
- Create: `web/Dpp/Forms/FormValues.cs`
- Create: `web/Dpp/Forms/RefusalWording.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/AttributeFormsTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class AttributeFormsTests
{
    internal static readonly AttributeForms Forms = new(DppStoreTesting.Schemas);

    [Fact]
    public void EveryAttribute_ShouldHaveAForm()
    {
        Assert.All(AttributeCatalog.All, attribute => Assert.Same(attribute, Forms.For(attribute).Attribute));
    }

    [Theory]
    [InlineData(11, FormKind.Single, InputKind.Text, null)]
    [InlineData(17, FormKind.Single, InputKind.Month, null)]
    [InlineData(19, FormKind.Single, InputKind.Text, null)]
    [InlineData(21, FormKind.Single, InputKind.Number, "kg")]
    [InlineData(57, FormKind.Single, InputKind.Integer, "Ah")]
    [InlineData(59, FormKind.Single, InputKind.Integer, "%")]
    [InlineData(81, FormKind.Single, InputKind.Number, "years")]
    [InlineData(82, FormKind.Single, InputKind.Integer, null)]
    [InlineData(87, FormKind.Single, InputKind.Integer, "°C")]
    [InlineData(30, FormKind.Single, InputKind.Number, "kgCO2eq/kWh")]
    [InlineData(42, FormKind.Lines, InputKind.Lines, null)]
    [InlineData(22, FormKind.Single, InputKind.Select, null)]
    public void OneInputAttributes_ShouldBeLabelledByTheirName(int number, FormKind kind, InputKind input, string? unit)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(kind, form.Kind);
        var only = Assert.Single(form.Inputs);
        Assert.Equal(input, only.Kind);
        Assert.Equal(unit, only.Unit);
        Assert.Equal(form.Attribute.Name, only.Label);
        Assert.True(only.Required);
    }

    [Fact]
    public void UnitsWithOneAllowedValue_ShouldBeFilledInByTheApp()
    {
        Assert.Equal("kg", Forms.For(AttributeCatalog.ByNumber(21)).Constants["gramKilogramUnit"]);
        Assert.Equal("Ah", Forms.For(AttributeCatalog.ByNumber(57)).Constants["ampereHourMiliamperehourUnit"]);
        Assert.Equal("%", Forms.For(AttributeCatalog.ByNumber(59)).Constants["percentUnit"]);
    }

    [Fact]
    public void OperatorInformation_ShouldBeAGroupWithTheSchemasRequiredParts()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(14));

        Assert.Equal(FormKind.Group, form.Kind);
        Assert.Equal(
            ["name:True", "registeredTradeNameOrRegisteredTrademark:True", "postalAddress:True", "webAddress:False", "eMailAddress:False"],
            form.Inputs.Select(input => $"{input.Path}:{input.Required}"));
        Assert.Equal("Registered trade name or trademark", form.Inputs[1].Label);
    }

    [Fact]
    public void Voltages_ShouldKeepTheirOptionalTestTemperatures()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(66));

        Assert.Equal(FormKind.Group, form.Kind);
        Assert.Equal(
            ["temperatureRangeLowerBoundaryValue", "temperatureRangeUpperBoundaryValue", "voltValue"],
            form.Inputs.Select(input => input.Path).Order(StringComparer.Ordinal));
        Assert.Equal("V", form.Inputs.Single(input => input.Path == "voltValue").Unit);
        Assert.All(form.Inputs.Where(input => input.Path.StartsWith("temperature", StringComparison.Ordinal)), input => Assert.Equal(("°C", false), (input.Unit, input.Required)));
    }

    [Fact]
    public void BatteryCategory_ShouldBeFilledInByTheApp()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(20));

        Assert.Equal(FormKind.Fixed, form.Kind);
        Assert.Empty(form.Inputs);
        Assert.Equal("industrial/non-stationary battery", form.Constants["batteryCategoryValue"]);
    }

    [Fact]
    public void Chemistry_ShouldOfferTheSchemasCodes()
    {
        var chemistry = Forms.For(AttributeCatalog.ByNumber(41)).Inputs.Single(input => input.Path == "chemicalCodeValue");

        Assert.Equal(InputKind.Select, chemistry.Kind);
        Assert.Contains("Li-ion NMC", chemistry.Options);
        Assert.Equal(10, chemistry.Options.Count);
    }

    [Fact]
    public void HazardousSubstances_ShouldBeRows()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(44));

        Assert.Equal(FormKind.Rows, form.Kind);
        Assert.Equal(InputKind.Lines, form.Inputs.Single(input => input.Path == "hazardPhrases").Kind);
        Assert.Equal(["CAS No", "EC No"], form.Inputs.Single(input => input.Path == "identifierType").Options);
        Assert.False(form.Inputs.Single(input => input.Path == "substanceLocation").Required);
    }

    [Theory]
    [InlineData(23)]
    [InlineData(28)]
    [InlineData(38)]
    [InlineData(54)]
    public void PublicDocuments_ShouldBeLinks(int number)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(FormKind.Link, form.Kind);
        Assert.Equal(["contentType", "language", "resourceTitle", "url"], form.Inputs.Select(input => input.Path).Order(StringComparer.Ordinal));
        Assert.Equal(AttributeForms.LinkContentTypes, form.Inputs.Single(input => input.Path == "contentType").Options);
        Assert.Equal(InputKind.Url, form.Inputs.Single(input => input.Path == "url").Kind);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(45)]
    [InlineData(48)]
    [InlineData(92)]
    public void RestrictedDocuments_ShouldBeUploads(int number)
    {
        Assert.Equal(FormKind.Upload, Forms.For(AttributeCatalog.ByNumber(number)).Kind);
    }

    [Fact]
    public void GroupedInputs_ShouldReadInAHelpfulOrderWithLabels()
    {
        Assert.Equal(
            ["Voltage", "Test temperature from", "Test temperature to"],
            Forms.For(AttributeCatalog.ByNumber(66)).Inputs.Select(input => input.Label));
        Assert.Equal(["Chemistry", "Other chemistry"], Forms.For(AttributeCatalog.ByNumber(41)).Inputs.Select(input => input.Label));
        Assert.Equal(["Pack", "Module", "Cell"], Forms.For(AttributeCatalog.ByNumber(78)).Inputs.Select(input => input.Label));
        Assert.Equal(["Link", "Title", "File type", "Language"], Forms.For(AttributeCatalog.ByNumber(28)).Inputs.Select(input => input.Label));
    }

    [Fact]
    public void MeaningOfLabels_ShouldBeATextArea()
    {
        Assert.Equal(InputKind.LongText, Assert.Single(Forms.For(AttributeCatalog.ByNumber(27)).Inputs).Kind);
    }

    [Fact]
    public void FieldNames_ShouldSayWhichAttributeAndPart()
    {
        var mass = Forms.For(AttributeCatalog.ByNumber(21));
        var agent = Forms.For(AttributeCatalog.ByNumber(26));
        var substances = Forms.For(AttributeCatalog.ByNumber(44));

        Assert.Equal("v.batteryMass", mass.FieldName(mass.Inputs[0]));
        Assert.Equal("v.extinguishingAgent.agentFireClass", agent.FieldName(agent.Inputs.Single(input => input.Path == "agentFireClass")));
        Assert.Equal("v.hazardousSubstancesAndTheirImpact[2].identifier", substances.FieldName(substances.Inputs.Single(input => input.Path == "identifier"), 2));
        Assert.Equal("v.hazardousSubstancesAndTheirImpact.rows", substances.RowCountField);
    }
}
```

`BatteryPassWeb.Tests/Dpp/FormValuesTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Tests.Dpp;

public sealed class FormValuesTests
{
    private static readonly AttributeForms Forms = AttributeFormsTests.Forms;

    public static TheoryData<string> SeedValues()
    {
        var sources = DppTestSeed.Load().Sources;
        var data = new TheoryData<string>();
        foreach (var key in new[] { sources.Model.Values, sources.Batch.Values, sources.Site.Values, sources.Operator.Values, sources.Pack.Values }.SelectMany(values => values.Keys))
        {
            data.Add(key);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SeedValues))]
    public void EverySeedValue_ShouldComeBackUnchangedThroughItsForm(string property)
    {
        var sources = DppTestSeed.Load().Sources;
        var stored = new[] { sources.Model.Values, sources.Batch.Values, sources.Site.Values, sources.Operator.Values, sources.Pack.Values }
            .Select(values => values.GetValueOrDefault(property)).First(value => value is not null)!.Value;
        var form = Forms.For(AttributeCatalog.FindByProperty(property)!);

        var texts = new Dictionary<string, string>(FormValues.Show(form, stored), StringComparer.Ordinal)
        {
            [form.RowCountField] = FormValues.RowCount(stored).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var read = FormValues.Read(form, name => texts.GetValueOrDefault(name));

        if (form.Kind == FormKind.Upload)
        {
            Assert.Null(read); // uploaded documents are never part of the save form
        }
        else
        {
            Assert.True(FormValues.SameValue(stored, read), $"{property}: {stored?.ToJsonString()} came back as {read?.ToJsonString()}");
        }
    }

    [Theory]
    [InlineData("512.5", "512.5")]
    [InlineData("512,5", "512.5")]
    [InlineData(" 600 ", "600")]
    [InlineData("about 500", "\"about 500\"")] // stays text, so the save pipeline refuses it with the schema's reason
    public void Numbers_ShouldBeReadInvariantly(string typed, string expectedValue)
    {
        var form = Forms.For(AttributeCatalog.ByNumber(21));

        var value = FormValues.Read(form, name => name == "v.batteryMass" ? typed : null);

        Assert.Equal($"{{\"gramKilogramValue\":{expectedValue},\"gramKilogramUnit\":\"kg\"}}", value!.ToJsonString());
    }

    [Fact]
    public void BlankForms_ShouldReadAsNothing()
    {
        Assert.Null(FormValues.Read(Forms.For(AttributeCatalog.ByNumber(21)), _ => "  "));
        Assert.Null(FormValues.Read(Forms.For(AttributeCatalog.ByNumber(14)), _ => null));
        Assert.Null(FormValues.Read(Forms.For(AttributeCatalog.ByNumber(42)), _ => "\n \n"));
    }

    [Fact]
    public void TemperatureUnits_ShouldOnlyBeAddedWithATemperature()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(66));

        var plain = FormValues.Read(form, name => name == "v.nominalVoltage.voltValue" ? "650" : null)!;
        var withRange = FormValues.Read(form, name => name switch
        {
            "v.nominalVoltage.voltValue" => "650",
            "v.nominalVoltage.temperatureRangeLowerBoundaryValue" => "-20",
            _ => null,
        })!;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"voltValue":650,"voltUnit":"V"}"""), plain));
        Assert.Equal(-20, withRange["temperatureRangeLowerBoundaryValue"]!.GetValue<long>());
        Assert.Equal("°C", withRange["temperatureRangeLowerBoundaryUnit"]!.GetValue<string>());
        Assert.Equal("°C", withRange["temperatureRangeUpperBoundaryUnit"]!.GetValue<string>());
    }

    [Fact]
    public void Lines_ShouldBeTrimmedAndBlankLinesDropped()
    {
        var value = FormValues.Read(Forms.For(AttributeCatalog.ByNumber(42)), _ => " Cobalt \r\n\r\nLithium\n");

        Assert.Equal("""["Cobalt","Lithium"]""", value!.ToJsonString());
    }

    [Fact]
    public void Rows_ShouldDropBlankRows()
    {
        var form = Forms.For(AttributeCatalog.ByNumber(44));
        var posted = new Dictionary<string, string>
        {
            [form.RowCountField] = "3",
            ["v.hazardousSubstancesAndTheirImpact[0].chemicalName"] = "Lithium hexafluorophosphate",
            ["v.hazardousSubstancesAndTheirImpact[0].identifierType"] = "CAS No",
            ["v.hazardousSubstancesAndTheirImpact[0].identifier"] = "21324-40-3",
            ["v.hazardousSubstancesAndTheirImpact[0].concentration"] = "1.5",
            ["v.hazardousSubstancesAndTheirImpact[0].hazardPhrases"] = "H301\nH314",
            ["v.hazardousSubstancesAndTheirImpact[0].precautionaryPhrases"] = "P280",
            ["v.hazardousSubstancesAndTheirImpact[1].chemicalName"] = " ",
            ["v.hazardousSubstancesAndTheirImpact[2].identifier"] = "7439-93-2",
        };

        var value = FormValues.Read(form, name => posted.GetValueOrDefault(name))!.AsArray();

        Assert.Equal(2, value.Count);
        Assert.Equal("""["H301","H314"]""", value[0]!["hazardPhrases"]!.ToJsonString());
        Assert.Equal("7439-93-2", value[1]!["identifier"]!.GetValue<string>());
    }

    [Fact]
    public void BatteryCategory_ShouldAlwaysReadAsItsOnlyValue()
    {
        var value = FormValues.Read(Forms.For(AttributeCatalog.ByNumber(20)), _ => null);

        Assert.Equal("""{"batteryCategoryValue":"industrial/non-stationary battery"}""", value!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"a":1.50,"b":"x"}""", """{"b":"x","a":1.5}""", true)]
    [InlineData("""{"a":2}""", """{"a":2.0}""", true)]
    [InlineData("""{"a":2}""", """{"a":3}""", false)]
    [InlineData("null", """{"a":3}""", false)]
    public void SameValue_ShouldIgnoreNumberSpellingAndKeyOrder(string first, string second, bool same)
    {
        Assert.Equal(same, FormValues.SameValue(JsonNode.Parse(first), JsonNode.Parse(second)));
    }
}
```

`BatteryPassWeb.Tests/Dpp/RefusalWordingTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The save pipeline's reasons, as it really words them, turned into plain sentences for the field.</summary>
public sealed class RefusalWordingTests
{
    [Theory]
    [InlineData(71, "properties: Some properties did not match the required schema: [\"percentageValue\"]; /percentageValue maximum: 120 should be at most 100", "Must be at most 100.")]
    [InlineData(53, "properties: Some properties did not match the required schema: [\"percentageValue\"]; /percentageValue minimum: -1 should be at least 0", "Must be at least 0.")]
    [InlineData(53, "properties: Some properties did not match the required schema: [\"percentageValue\"]; /percentageValue type: Value is \"string\" but should be \"integer\"", "Must be a whole number.")]
    [InlineData(30, "properties: Some properties did not match the required schema: [\"kgCO2EquivalentPerKilowattHourValue\"]; /kgCO2EquivalentPerKilowattHourValue type: Value is \"string\" but should be \"number\"", "Must be a number.")]
    [InlineData(66, "properties: Some properties did not match the required schema: [\"temperatureRangeLowerBoundaryValue\"]; /temperatureRangeLowerBoundaryValue type: Value is \"number\" but should be \"integer\"", "Test temperature from must be a whole number.")]
    [InlineData(41, "properties: Some properties did not match the required schema: [\"chemicalCodeValue\"]; /chemicalCodeValue enum: Value should match one of the values specified by the enum", "Chemistry must be one of the listed choices.")]
    [InlineData(28, "properties: Some properties did not match the required schema: [\"url\"]; /url pattern: The string value is not a match for the indicated regular expression", "Link must be a web address starting with https:// or http://.")]
    [InlineData(25, "required: Required properties [\"contentType\"] are not present", "File type is missing.")]
    [InlineData(15, "required: Required properties [\"registeredTradeNameOrRegisteredTrademark\",\"postalAddress\"] are not present", "Registered trade name or trademark and Postal address are missing.")]
    [InlineData(44, "items: Some items do not match the required schema; failing indexes: [0]; /0 required: Required properties [\"identifierType\",\"identifier\",\"concentration\"] are not present", "Row 1: Identifier type, Identifier and Concentration are missing.")]
    [InlineData(29, "#29 must link to a file uploaded to the app", "#29 must link to a file uploaded to the app.")]
    [InlineData(21, "#21 can't be changed by this sender", "#21 can't be changed by this sender.")]
    public void Reasons_ShouldReadAsPlainSentences(int number, string message, string expected)
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(expected, RefusalWording.Describe(form, message));
    }

    [Theory]
    [InlineData(15, "required: Required properties [\"registeredTradeNameOrRegisteredTrademark\",\"postalAddress\"] are not present", "postalAddress,registeredTradeNameOrRegisteredTrademark")]
    [InlineData(66, "properties: Some properties did not match the required schema: [\"temperatureRangeLowerBoundaryValue\"]; /temperatureRangeLowerBoundaryValue type: Value is \"number\" but should be \"integer\"", "temperatureRangeLowerBoundaryValue")]
    [InlineData(28, "properties: Some properties did not match the required schema: [\"url\"]; /url pattern: The string value is not a match for the indicated regular expression", "url")]
    [InlineData(29, "#29 must link to a file uploaded to the app", "")]
    public void TheRefusedParts_ShouldBeNamed(int number, string message, string expected)
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(number));

        Assert.Equal(expected, string.Join(",", RefusalWording.Parts(form, message).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void AnUnknownRule_ShouldStillSayWhatWasRefused()
    {
        var form = AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(11));

        Assert.Equal("Was refused (maxLength: Value is too long).", RefusalWording.Describe(form, "maxLength: Value is too long"));
        Assert.Equal(string.Empty, RefusalWording.Describe(form, " "));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeFormsTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.FormValuesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RefusalWordingTests"`
Expected: the build fails because the namespace `BatteryPassWeb.Dpp.Forms` doesn't exist yet (CS0234, CS0246 and CS0103 errors).

- [ ] **Step 3: Write `AttributeForm.cs`**

`web/Dpp/Forms/AttributeForm.cs`:

```csharp
using BatteryPassWeb.Dpp.Catalog;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>How an attribute is entered on the team's pages, following its v2.0.1 schema shape.</summary>
public enum FormKind
{
    /// <summary>One input: a text, a number, or a number whose fixed unit the app adds (e.g. #21 in kg).</summary>
    Single,

    /// <summary>Several inputs that make one object, e.g. #14's name and address, or #66's voltage and test temperatures.</summary>
    Group,

    /// <summary>A list of texts, one per line (#42, #43).</summary>
    Lines,

    /// <summary>A list of objects, one row each (#44).</summary>
    Rows,

    /// <summary>A public document: a link to the company website with its content type, title and language (decision 7).</summary>
    Link,

    /// <summary>A restricted document, uploaded to the app (decision 7); not part of the save form.</summary>
    Upload,

    /// <summary>A value the schema allows only one way (#20); the app fills it in.</summary>
    Fixed,
}

public enum InputKind
{
    Text,

    /// <summary>Text that runs to a sentence or more (#27), shown as a text area.</summary>
    LongText,
    Number,
    Integer,
    Month,
    Url,
    Select,
    Lines,
}

/// <summary>
/// One input of an attribute's form. <paramref name="Path"/> is the schema property it fills inside the value, or ""
/// when the input is the whole value.
/// </summary>
public sealed record FormInput(
    string Path,
    string Label,
    InputKind Kind,
    bool Required,
    IReadOnlyList<string> Options,
    string? Unit = null,
    string? Hint = null);

/// <summary>
/// The form of one attribute. <see cref="Constants"/> are the schema properties the app fills in itself, such as
/// units that have only one allowed value. For <see cref="FormKind.Rows"/>, <see cref="Inputs"/> describe one row.
/// </summary>
public sealed record AttributeForm(
    AttributeDefinition Attribute,
    FormKind Kind,
    IReadOnlyList<FormInput> Inputs,
    IReadOnlyDictionary<string, string> Constants)
{
    public const string Prefix = "v.";

    /// <summary>The posted field that holds the number of rows of a <see cref="FormKind.Rows"/> form.</summary>
    public string RowCountField => $"{Prefix}{Attribute.Property}.rows";

    /// <summary>The HTML name of an input: v.batteryMass, v.extinguishingAgent.agentFireClass or v.hazardousSubstancesAndTheirImpact[0].identifier.</summary>
    public string FieldName(FormInput input, int? row = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var owner = row is null ? $"{Prefix}{Attribute.Property}" : $"{Prefix}{Attribute.Property}[{row}]";
        return input.Path.Length == 0 || Kind == FormKind.Single ? owner : $"{owner}.{input.Path}";
    }
}
```

- [ ] **Step 4: Write `AttributeForms.cs`**

`web/Dpp/Forms/AttributeForms.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Schemas;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// The form of every catalogue attribute, read from the official v2.0.1 schema (design 6.2): which inputs it has,
/// their kind and label, and which parts of the value the app fills in itself.
/// </summary>
public sealed class AttributeForms
{
    /// <summary>Content types offered for public document links; a stored value outside the list is still shown.</summary>
    public static readonly IReadOnlyList<string> LinkContentTypes = ["application/pdf", "text/html", "image/png", "image/jpeg"];

    /// <summary>
    /// The inputs inside grouped values, by schema property name, in the order the form shows them (the schema's own
    /// order puts e.g. the upper test temperature before the lower one).
    /// </summary>
    private static readonly (string Path, string Label)[] Parts =
    [
        ("name", "Name"),
        ("registeredTradeNameOrRegisteredTrademark", "Registered trade name or trademark"),
        ("postalAddress", "Postal address"),
        ("webAddress", "Website"),
        ("eMailAddress", "Email"),
        ("chemicalCodeValue", "Chemistry"),
        ("additionallyPossibleValue", "Other chemistry"),
        ("agentFireClass", "Fire class"),
        ("extinguishingAgent", "Extinguishing agent"),
        ("voltValue", "Voltage"),
        ("wattValue", "Power"),
        ("wattValueAt80PercentStateOfCharge", "Power at 80 % state of charge"),
        ("wattValueAt20PercentStateOfCharge", "Power at 20 % state of charge"),
        ("ohmValueForBatteryPack", "Pack"),
        ("ohmValueForBatteryModule", "Module"),
        ("ohmValueForBatteryCell", "Cell"),
        ("internalResistanceIncreaseValueForBatteryPack", "Pack"),
        ("internalResistanceIncreaseValueForBatteryModule", "Module"),
        ("internalResistanceIncreaseValueForBatteryCell", "Cell"),
        ("temperatureRangeLowerBoundaryValue", "Test temperature from"),
        ("temperatureRangeUpperBoundaryValue", "Test temperature to"),
        ("url", "Link"),
        ("resourceTitle", "Title"),
        ("contentType", "File type"),
        ("language", "Language"),
        ("chemicalName", "Chemical name"),
        ("identifierType", "Identifier type"),
        ("identifier", "Identifier"),
        ("concentration", "Concentration"),
        ("hazardPhrases", "Hazard phrases"),
        ("precautionaryPhrases", "Precautionary phrases"),
        ("substanceLocation", "Where in the battery"),
    ];

    private static readonly Dictionary<string, string> Labels = Parts.ToDictionary(part => part.Path, part => part.Label, StringComparer.Ordinal);

    private static readonly Dictionary<string, int> Order = Parts.Select((part, index) => (part.Path, index)).ToDictionary(entry => entry.Path, entry => entry.index, StringComparer.Ordinal);

    private readonly Dictionary<string, AttributeForm> _forms;

    public AttributeForms(OfficialSchemas schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        _forms = AttributeCatalog.All.ToDictionary(attribute => attribute.Property, attribute => Build(schemas.FullDocument, attribute), StringComparer.Ordinal);
        Languages = EnumValues(OfficialSchemas.Resolve(schemas.FullDocument, schemas.FullDocument["$defs"]!["RelatedResource"]!["properties"]!["language"]!));
    }

    /// <summary>The languages a document may be given (the schema's list), for links and uploads.</summary>
    public IReadOnlyList<string> Languages { get; }

    public AttributeForm For(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return _forms[attribute.Property];
    }

    private static AttributeForm Build(JsonNode document, AttributeDefinition attribute)
    {
        var node = OfficialSchemas.FindProperty(document, attribute.Section, attribute.Property)
            ?? throw new InvalidOperationException($"#{attribute.Number} {attribute.Property} is not in the schema.");
        var noConstants = new Dictionary<string, string>(StringComparer.Ordinal);

        if (DppLinks.RestrictedDocumentProperties.Contains(attribute.Property))
        {
            return new AttributeForm(attribute, FormKind.Upload, [], noConstants);
        }

        var type = node["type"]?.GetValue<string>();
        if (type == "array")
        {
            var items = OfficialSchemas.Resolve(document, node["items"]!);
            if (items["type"]?.GetValue<string>() == "object")
            {
                var (rowInputs, _) = ObjectInputs(document, items, attribute);
                return new AttributeForm(attribute, FormKind.Rows, Arrange(rowInputs), noConstants);
            }

            return new AttributeForm(attribute, FormKind.Lines, [new FormInput(string.Empty, attribute.Name, InputKind.Lines, true, [], Hint: "One per line.")], noConstants);
        }

        if (type != "object")
        {
            return new AttributeForm(attribute, FormKind.Single, [ScalarInput(string.Empty, attribute.Name, node, required: true, attribute)], noConstants);
        }

        var (inputs, constants) = ObjectInputs(document, node, attribute);
        if (node["properties"]?["url"] is not null && node["properties"]?["contentType"] is not null)
        {
            return new AttributeForm(attribute, FormKind.Link, Arrange(inputs), constants);
        }

        var kind = inputs.Count switch
        {
            0 => FormKind.Fixed,
            1 => FormKind.Single,
            _ => FormKind.Group,
        };

        // A value with one input is labelled by the attribute itself; grouped inputs need their own label.
        return kind == FormKind.Single
            ? new AttributeForm(attribute, kind, [inputs[0] with { Label = attribute.Name, Required = true }], constants)
            : new AttributeForm(attribute, kind, Arrange(inputs), constants);
    }

    /// <summary>The inputs of an object value, and its constants: single-valued enums, and units (the catalogue's unit).</summary>
    private static (IReadOnlyList<FormInput> Inputs, Dictionary<string, string> Constants) ObjectInputs(JsonNode document, JsonObject node, AttributeDefinition attribute)
    {
        var required = node["required"]?.AsArray().Select(name => name!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        var inputs = new List<FormInput>();
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, child) in node["properties"]?.AsObject() ?? [])
        {
            var property = OfficialSchemas.Resolve(document, child!);
            var options = EnumValues(property);
            if (options.Count == 1)
            {
                constants[name] = options[0];
                continue;
            }

            if (options.Count > 1 && name.EndsWith("Unit", StringComparison.Ordinal))
            {
                constants[name] = attribute.Unit is { } unit && options.Contains(unit) ? unit : options[0];
                continue;
            }

            if (name == "contentType" && node["properties"]?["url"] is not null)
            {
                inputs.Add(new FormInput(name, name, InputKind.Select, required.Contains(name), LinkContentTypes));
                continue;
            }

            if (property["type"]?.GetValue<string>() == "array")
            {
                inputs.Add(new FormInput(name, name, InputKind.Lines, required.Contains(name), [], Hint: "One per line."));
                continue;
            }

            inputs.Add(ScalarInput(name, name, property, required.Contains(name), attribute));
        }

        return (inputs, constants);
    }

    private static FormInput ScalarInput(string path, string label, JsonObject property, bool required, AttributeDefinition attribute)
    {
        var options = EnumValues(property);
        if (options.Count > 0)
        {
            return new FormInput(path, label, InputKind.Select, required, options);
        }

        var unit = path.StartsWith("temperatureRange", StringComparison.Ordinal) ? "°C" : attribute.Unit;
        return property["type"]?.GetValue<string>() switch
        {
            "integer" => new FormInput(path, label, InputKind.Integer, required, [], unit),
            "number" => new FormInput(path, label, InputKind.Number, required, [], path == "concentration" ? null : unit),
            _ => property["format"]?.GetValue<string>() switch
            {
                // The file and the regulation give the manufacturing date as year and month (decision 18).
                "date" => new FormInput(path, label, InputKind.Month, required, [], Hint: "Year and month."),
                "duration" => new FormInput(path, label, InputKind.Text, required, [], Hint: "A duration such as P8Y (8 years) or P6M (6 months)."),
                "uri" when path == "url" || attribute.Property.StartsWith("webLink", StringComparison.Ordinal) => new FormInput(path, label, InputKind.Url, required, [], Hint: "A link to your website, starting with https://."),
                "uri" => new FormInput(path, label, InputKind.Text, required, [], Hint: "A URI, for example urn:… or https://…."),
                _ when path == "url" => new FormInput(path, label, InputKind.Url, required, [], Hint: "A link to your website, starting with https://."),
                _ when attribute.Property == "meaningOfLabelsAndSymbols" => new FormInput(path, label, InputKind.LongText, required, []),
                _ => new FormInput(path, label, InputKind.Text, required, []),
            },
        };
    }

    private static IReadOnlyList<string> EnumValues(JsonObject property) =>
        property["enum"]?.AsArray().Select(value => value!.GetValue<string>()).ToArray() ?? [];

    /// <summary>Labels the inputs of a grouped value and puts them in the form's order.</summary>
    private static FormInput[] Arrange(IEnumerable<FormInput> inputs) =>
        inputs.Select(input => input with { Label = Label(input.Path) }).OrderBy(input => Order[input.Path]).ToArray();

    private static string Label(string name) =>
        Labels.TryGetValue(name, out var label) ? label : throw new InvalidOperationException($"No label for the schema property '{name}'.");
}
```

- [ ] **Step 5: Write `FormValues.cs`**

`web/Dpp/Forms/FormValues.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Signing;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// Turns what was typed into an attribute's form into its value in the schema's own form, and a stored value back
/// into the texts the form shows. Nothing here judges a value: the save pipeline checks it against the schema.
/// </summary>
public static class FormValues
{
    private const string TemperatureRange = "temperatureRange";

    /// <summary>The value typed for one attribute; null when nothing was entered, so the stored value is kept.</summary>
    /// <param name="field">Gives the posted text of a field name, or null when it wasn't posted.</param>
    public static JsonNode? Read(AttributeForm form, Func<string, string?> field)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(field);
        switch (form.Kind)
        {
            case FormKind.Upload:
                return null;
            case FormKind.Fixed:
                return WithConstants(new JsonObject(), form.Constants);
            case FormKind.Single when form.Inputs[0].Path.Length == 0:
            case FormKind.Lines:
                return Parse(form.Inputs[0], field(form.FieldName(form.Inputs[0])));
            case FormKind.Rows:
                var count = int.TryParse(field(form.RowCountField), NumberStyles.None, CultureInfo.InvariantCulture, out var rows) ? rows : 0;
                var items = Enumerable.Range(0, count).Select(row => ReadObject(form, field, row)).OfType<JsonObject>().ToArray();
                return items.Length == 0 ? null : new JsonArray(items);
            default:
                return ReadObject(form, field, row: null);
        }
    }

    /// <summary>The texts the form shows for a stored value, by field name.</summary>
    public static IReadOnlyDictionary<string, string> Show(AttributeForm form, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(form);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (value is null || form.Kind is FormKind.Upload or FormKind.Fixed)
        {
            return texts;
        }

        if (form.Kind == FormKind.Rows)
        {
            var row = 0;
            foreach (var item in value as JsonArray ?? [])
            {
                foreach (var input in form.Inputs)
                {
                    texts[form.FieldName(input, row)] = Text(item?[input.Path]);
                }

                row++;
            }

            return texts;
        }

        foreach (var input in form.Inputs)
        {
            texts[form.FieldName(input)] = Text(input.Path.Length == 0 ? value : value[input.Path]);
        }

        return texts;
    }

    /// <summary>How many rows a stored list has; 0 for anything else.</summary>
    public static int RowCount(JsonNode? value) => value is JsonArray array ? array.Count : 0;

    /// <summary>True when two values are the same once written canonically (numbers in their shortest form, keys sorted).</summary>
    public static bool SameValue(JsonNode? first, JsonNode? second) =>
        CanonicalJson.Serialize(first) == CanonicalJson.Serialize(second);

    private static JsonObject? ReadObject(AttributeForm form, Func<string, string?> field, int? row)
    {
        var value = new JsonObject();
        foreach (var input in form.Inputs)
        {
            if (Parse(input, field(form.FieldName(input, row))) is { } part)
            {
                value[input.Path] = part;
            }
        }

        return value.Count == 0 ? null : WithConstants(value, form.Constants);
    }

    /// <summary>Adds the constants; the temperature-range units only when a temperature was entered.</summary>
    private static JsonObject WithConstants(JsonObject value, IReadOnlyDictionary<string, string> constants)
    {
        var hasRange = value.Any(property => property.Key.StartsWith(TemperatureRange, StringComparison.Ordinal));
        foreach (var (name, constant) in constants)
        {
            if (hasRange || !name.StartsWith(TemperatureRange, StringComparison.Ordinal))
            {
                value[name] = constant;
            }
        }

        return value;
    }

    /// <summary>
    /// The typed text as a JSON value; null when blank. A number that doesn't parse stays text, so the save pipeline
    /// refuses it with the schema's reason instead of the form guessing.
    /// </summary>
    private static JsonNode? Parse(FormInput input, string? typed)
    {
        var text = typed?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        switch (input.Kind)
        {
            case InputKind.Integer when long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole):
                return JsonValue.Create(whole);
            case InputKind.Number when decimal.TryParse(text.Contains('.', StringComparison.Ordinal) ? text : text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number):
                return JsonValue.Create(number);
            case InputKind.Lines:
                var lines = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
                return lines.Length == 0 ? null : new JsonArray(lines.Select(line => (JsonNode)JsonValue.Create(line)).ToArray());
            default:
                return JsonValue.Create(text);
        }
    }

    private static string Text(JsonNode? value) => value switch
    {
        null => string.Empty,
        JsonArray lines => string.Join("\n", lines.Select(Text)),
        JsonObject => value.ToJsonString(),
        _ when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        _ => CanonicalJson.Serialize(value),
    };
}
```

- [ ] **Step 6: Write `RefusalWording.cs`**

`web/Dpp/Forms/RefusalWording.cs`:

```csharp
using System.Text.RegularExpressions;

namespace BatteryPassWeb.Dpp.Forms;

/// <summary>
/// Turns the save pipeline's reason for refusing a value into plain words for the field it belongs to. The pipeline
/// reports schema issues as "/location keyword: detail", joined with "; ", e.g.
/// "/percentageValue maximum: 120 should be at most 100"; its own rules come as sentences ("#29 must link to …").
/// </summary>
public static partial class RefusalWording
{
    public static string Describe(AttributeForm form, string message)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var sentences = new List<string>();
        foreach (var issue in message.Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (issue.StartsWith("failing indexes", StringComparison.Ordinal))
            {
                continue; // part of a list summary; each failing row is reported on its own
            }

            var match = IssuePattern().Match(issue);
            if (!match.Success)
            {
                sentences.Add(issue); // one of the pipeline's own sentences
                continue;
            }

            var keyword = match.Groups["keyword"].Value;
            if (keyword is "properties" or "items")
            {
                continue; // a summary: the issue inside is reported on its own
            }

            sentences.Add(Sentence(form, Part(form, match.Groups["location"].Value), keyword, match.Groups["detail"].Value));
        }

        return string.Join(" ", sentences.Select(Finish));
    }

    /// <summary>
    /// The inputs of a grouped value that a refusal is about (by schema property), so the page marks only those; empty
    /// when the message doesn't name one.
    /// </summary>
    public static IReadOnlySet<string> Parts(AttributeForm form, string message)
    {
        ArgumentNullException.ThrowIfNull(form);
        var paths = form.Inputs.Select(input => input.Path).ToHashSet(StringComparer.Ordinal);
        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issue in (message ?? string.Empty).Split("; ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = IssuePattern().Match(issue);
            if (!match.Success || match.Groups["keyword"].Value is "properties" or "items")
            {
                continue;
            }

            var located = match.Groups["location"].Value.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(segment => paths.Contains(segment));
            if (located is not null)
            {
                parts.Add(located);
            }

            if (match.Groups["keyword"].Value == "required")
            {
                parts.UnionWith(Quoted().Matches(match.Groups["detail"].Value).Select(quoted => quoted.Groups[1].Value).Where(paths.Contains));
            }
        }

        return parts;
    }

    /// <summary>
    /// The part of the value an issue is about: null for a one-input value (the field itself), the input's label inside
    /// a grouped value, or "Row 2" (with the label) inside a list of rows.
    /// </summary>
    private static string? Part(AttributeForm form, string location)
    {
        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var row = segments.Length > 0 && int.TryParse(segments[0], out var index) ? $"Row {index + 1}" : null;
        var path = segments.LastOrDefault(segment => !int.TryParse(segment, out _));
        var label = form.Kind == FormKind.Single ? null : form.Inputs.FirstOrDefault(input => input.Path == path)?.Label;
        return (row, label) switch
        {
            (null, null) => null,
            (null, _) => label,
            (_, null) => row,
            _ => $"{row}, {label}",
        };
    }

    /// <summary>
    /// One issue in plain words. <paramref name="part"/> is null when the issue is about the whole field. Schema
    /// property names in <paramref name="detail"/> (e.g. ["postalAddress"]) have labels in <paramref name="form"/>.
    /// </summary>
    private static string Sentence(AttributeForm form, string? part, string keyword, string detail)
    {
        if (keyword == "required")
        {
            // A row says which row it is; a grouped value names its missing parts.
            var missing = Quoted().Matches(detail).Select(match => form.Inputs.FirstOrDefault(input => input.Path == match.Groups[1].Value)?.Label ?? match.Groups[1].Value).ToArray();
            var list = missing.Length <= 1 ? string.Concat(missing) : $"{string.Join(", ", missing[..^1])} and {missing[^1]}";
            var sentence = $"{list} {(missing.Length == 1 ? "is" : "are")} missing";
            return part is null ? sentence : $"{part}: {sentence}";
        }

        var limit = LastNumber().Match(detail).Value;
        var rule = keyword switch
        {
            "maximum" => $"must be at most {limit}",
            "minimum" => $"must be at least {limit}",
            "type" when detail.EndsWith("\"integer\"", StringComparison.Ordinal) => "must be a whole number",
            "type" when detail.EndsWith("\"number\"", StringComparison.Ordinal) => "must be a number",
            "type" when detail.EndsWith("\"string\"", StringComparison.Ordinal) => "must be text",
            "enum" => "must be one of the listed choices",
            "pattern" => "must be a web address starting with https:// or http://",
            "minItems" => "needs at least one line",
            _ => $"was refused ({keyword}: {detail})",
        };

        // Under a one-input field the field itself is the subject; inside a group, the part is.
        return part is null ? rule : $"{part} {rule}";
    }

    private static string Finish(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToUpperInvariant(sentence[0]) + sentence[1..].TrimEnd('.') + ".";

    [GeneratedRegex(@"^(?<location>/\S*)?\s*(?<keyword>[A-Za-z]+): (?<detail>.*)$")]
    private static partial Regex IssuePattern();

    [GeneratedRegex("\"(\\w+)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"-?\d+(?:\.\d+)?(?=\D*$)")]
    private static partial Regex LastNumber();
}
```

- [ ] **Step 7: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AttributeFormsTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.FormValuesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RefusalWordingTests"`
Expected: `Passed!  - Failed:     0, Passed:   128`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1778`, none failed.

- [ ] **Step 8: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/AttributeFormsTests.cs BatteryPassWeb.Tests/Dpp/FormValuesTests.cs BatteryPassWeb.Tests/Dpp/RefusalWordingTests.cs web/Dpp/Forms/AttributeForm.cs web/Dpp/Forms/AttributeForms.cs web/Dpp/Forms/FormValues.cs web/Dpp/Forms/RefusalWording.cs
git commit -m "feat(dpp): build each attribute's form from the official schema"
```


---

### Task 4: The record editor

The editor behind the model, batch, site and operator pages, with the stamp of what the page showed. `PageTesting` starts here with the editor's helpers; Tasks 5 and 6 add to it.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/PageTesting.cs`
- Test: `BatteryPassWeb.Tests/Dpp/RecordEditorTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/EditorStampTests.cs`
- Create: `web/Dpp/Pages/RecordEditor.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/PageTesting.cs`:

```csharp
using System.Globalization;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The team-page services around a publication rig, and forms filled in the way the page shows them.</summary>
internal static class PageTesting
{
    public static RecordEditor Editor(PublicationRig rig) =>
        new(AttributeFormsTests.Forms, rig.Pipeline, rig.Store, new DocumentStore(rig.Context));

    public static DocumentService Documents(PublicationRig rig) => new(
        new DocumentStore(rig.Context),
        rig.Store,
        rig.Pipeline,
        DocumentTesting.Links,
        Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }),
        new ApplicationAuditService(rig.Context),
        rig.Clock);

    /// <summary>Every field of a record's editor as the page shows it, with the row counts of its lists.</summary>
    public static Dictionary<string, string> ShownForm(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in RecordEditor.AttributesOf(kind))
        {
            var form = AttributeFormsTests.Forms.For(attribute);
            var value = stored.GetValueOrDefault(attribute.Property)?.Value;
            foreach (var (name, text) in FormValues.Show(form, value))
            {
                texts[name] = text;
            }

            if (form.Kind == FormKind.Rows)
            {
                texts[form.RowCountField] = FormValues.RowCount(value).ToString(CultureInfo.InvariantCulture);
            }
        }

        return texts;
    }

    public static Func<string, string?> Field(IReadOnlyDictionary<string, string> form) => name => form.GetValueOrDefault(name);
}
```

`BatteryPassWeb.Tests/Dpp/RecordEditorTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class RecordEditorTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    [Theory]
    [InlineData(TargetKind.Model, 37)]
    [InlineData(TargetKind.Batch, 16)]
    [InlineData(TargetKind.Site, 3)]
    [InlineData(TargetKind.Operator, 2)]
    public void EachRecord_ShouldEditItsOwnAttributes(TargetKind kind, int count)
    {
        Assert.Equal(count, RecordEditor.AttributesOf(kind).Count);
    }

    [Theory]
    [InlineData(TargetKind.Model, 28)]
    [InlineData(TargetKind.Batch, 4)]
    [InlineData(TargetKind.Site, 3)]
    [InlineData(TargetKind.Operator, 2)]
    public void AnEmptyRecord_ShouldMissOnlyItsValuesNeededByFeb2027(TargetKind kind, int count)
    {
        // Values needed only later (8 of a batch's, 2 of a model's) and conditional ones aren't counted.
        Assert.Equal(count, RecordEditor.CountMissing(kind, new Dictionary<string, AttributeValue>()));
    }

    [Fact]
    public async Task MissingValues_ShouldBeTheEmptyOnesNeededByFeb2027()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        IReadOnlyDictionary<string, AttributeValue> Without(string property) => stored.Where(pair => pair.Key != property).ToDictionary();

        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Model, stored));
        Assert.Equal(0, RecordEditor.CountMissing(TargetKind.Model, Without("symbolsForCadmiumAndLead"))); // #24 is conditional
        Assert.Equal(1, RecordEditor.CountMissing(TargetKind.Model, Without("batteryMass"))); // #21 is needed by Feb 2027

        var sections = await PageTesting.Editor(rig).SectionsAsync(TargetKind.Model, ModelId, Without("batteryMass"));
        var missing = Assert.Single(sections.SelectMany(section => section.Fields), field => field.IsMissing);
        Assert.Equal(21, missing.Attribute.Number);
        Assert.Equal([1, 0, 0, 0, 0, 0], sections.Select(section => section.Missing));
    }

    [Fact]
    public async Task UnchangedForm_ShouldChangeNothing()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;

        var post = PageTesting.Editor(rig).Read(TargetKind.Model, stored, PageTesting.Field(PageTesting.ShownForm(TargetKind.Model, stored)));

        Assert.Empty(post.Changed);
    }

    [Fact]
    public async Task OneEditedField_ShouldBeTheOnlyChange()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "520";

        var post = PageTesting.Editor(rig).Read(TargetKind.Model, stored, PageTesting.Field(form));

        var change = Assert.Single(post.Changed);
        Assert.Equal("batteryMass", change.Key);
        Assert.Equal("520", post.Texts["v.batteryMass"]);
    }

    [Fact]
    public async Task Sections_ShouldFollowTheSchemaSectionsAndShowTheUploadedFile()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var uploaded = await PageTesting.Documents(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, ModelId, "dismantlingInformation", new DocumentUpload("manual.pdf", DocumentTesting.Pdf()));
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;

        var sections = await editor.SectionsAsync(TargetKind.Model, ModelId, stored);

        Assert.Equal(
            ["Identifiers and product data", "Symbols, labels and conformity", "Due diligence", "Materials and composition", "Circularity", "Performance and durability"],
            sections.Select(section => section.Title));
        Assert.Equal(37, sections.Sum(section => section.Fields.Count));
        var dismantling = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 45);
        Assert.Equal(uploaded.Document!.Id, dismantling.Document?.Id);
        Assert.Equal(("Required by Feb 2027", "Shared"), (dismantling.Marker, dismantling.AudienceLabel));
        var substances = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 44);
        Assert.Equal(FormValues.RowCount(stored["hazardousSubstancesAndTheirImpact"].Value) + 1, substances.Rows); // one blank row to add another
    }

    [Fact]
    public async Task SavingADraftsModel_ShouldNotAsk()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "520";

        var result = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, editor.Read(TargetKind.Model, stored, PageTesting.Field(form)), confirmed: false);

        Assert.Equal((EditorOutcome.Saved, 1), (result.Outcome, result.SavedCount));
        Assert.Equal("520", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task ChangingAPublishedPassportsModel_ShouldAskBeforePublishing()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.ratedCapacity"] = "210";
        var post = editor.Read(TargetKind.Model, stored, PageTesting.Field(form));

        var asked = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false);

        Assert.Equal(EditorOutcome.NeedsConfirmation, asked.Outcome);
        Assert.Equal(new AffectedPassport(rig.PackId, "VPC8C05-6788", "cluster-test", 1), Assert.Single(asked.Affected));
        Assert.True(FormValues.SameValue(stored["ratedCapacity"].Value, (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["ratedCapacity"].Value));

        var confirmed = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: true);

        Assert.Equal(EditorOutcome.Saved, confirmed.Outcome);
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task ARefusedValue_ShouldNotStopTheOthers()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "520";
        form["v.initialRoundTripEnergyEfficiency"] = "120";

        var result = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, editor.Read(TargetKind.Model, stored, PageTesting.Field(form)), confirmed: false);

        Assert.Equal((EditorOutcome.Refused, 1), (result.Outcome, result.SavedCount));
        Assert.Equal(["initialRoundTripEnergyEfficiency"], result.Refusals(AttributeFormsTests.Forms).Keys);
        Assert.Equal("520", (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task NothingToSave_ShouldSaySo()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var empty = new EditorPost(new Dictionary<string, System.Text.Json.Nodes.JsonNode>(), new Dictionary<string, string>(), new Dictionary<string, int>());
        var change = new EditorPost(new Dictionary<string, System.Text.Json.Nodes.JsonNode> { ["manufacturingPlace"] = "Example town" }, new Dictionary<string, string>(), new Dictionary<string, int>());

        Assert.Equal(EditorOutcome.NothingChanged, (await editor.SaveAsync(PublicationTesting.Team, TargetKind.Site, "site-test", empty, confirmed: false)).Outcome);
        Assert.Equal(EditorOutcome.NotFound, (await editor.SaveAsync(PublicationTesting.Team, TargetKind.Site, "no-such-site", change, confirmed: false)).Outcome);
    }
}
```

`BatteryPassWeb.Tests/Dpp/EditorStampTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The editor page carries a stamp of what each field showed. A save then changes only the fields that were edited on
/// the page, so a page left open never undoes someone else's newer value; a field edited on both sides is refused.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EditorStampTests(MongoFixture mongo)
{
    private const string ModelId = "model-test";

    private static async Task<IReadOnlyDictionary<string, AttributeValue>> StoredAsync(PublicationRig rig) =>
        (await rig.Store.GetAsync<BatteryModel>(ModelId))!.Values;

    // Someone else saves a mass of 600 after the page was opened.
    private static Task SomeoneElseSavesAMassAsync(PublicationRig rig) => rig.Pipeline.SaveAsync(
        ChangeSender.Team("colleague@example.com"),
        new ChangeSet(TargetKind.Model, ModelId, new Dictionary<string, JsonNode?> { ["batteryMass"] = JsonNode.Parse("""{"gramKilogramValue":600,"gramKilogramUnit":"kg"}""") }, "form"));

    [Fact]
    public async Task APageLeftOpen_ShouldNotUndoANewerValue()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var stamp = editor.Stamp(TargetKind.Model, opened, "Model test");
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        await SomeoneElseSavesAMassAsync(rig);

        var post = editor.Read(TargetKind.Model, await StoredAsync(rig), PageTesting.Field(form), stamp);

        Assert.Empty(post.Changed);
        Assert.Empty(post.Conflicts);
        Assert.Equal(EditorOutcome.NothingChanged, (await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false)).Outcome);
        Assert.Equal("600", (await StoredAsync(rig))["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());
    }

    [Fact]
    public async Task AFieldEditedHere_ShouldSaveWhileAnotherFieldChangedElsewhere()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var stamp = editor.Stamp(TargetKind.Model, opened, "Model test");
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        form["v.ratedCapacity"] = "210";
        await SomeoneElseSavesAMassAsync(rig);

        var post = editor.Read(TargetKind.Model, await StoredAsync(rig), PageTesting.Field(form), stamp);

        Assert.Equal(["ratedCapacity"], post.Changed.Keys);
    }

    [Fact]
    public async Task AFieldEditedOnBothSides_ShouldBeRefusedAndKeepWhatWasTyped()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var stamp = editor.Stamp(TargetKind.Model, opened, "Model test");
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        form["v.batteryMass"] = "700";
        await SomeoneElseSavesAMassAsync(rig);
        var now = await StoredAsync(rig);

        var post = editor.Read(TargetKind.Model, now, PageTesting.Field(form), stamp);
        var save = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false);

        Assert.Equal(["batteryMass"], post.Conflicts);
        Assert.Equal(EditorOutcome.Refused, save.Outcome);
        Assert.Contains("after you opened this page", save.Refusals(AttributeFormsTests.Forms)["batteryMass"].Text, StringComparison.Ordinal);
        Assert.Equal("600", (await StoredAsync(rig))["batteryMass"].Value!["gramKilogramValue"]!.ToJsonString());

        // Shown again: the typed 700 stays in its field, the other fields show the stored values, and the new stamp lets
        // the next save go through.
        var sections = await editor.SectionsAsync(TargetKind.Model, ModelId, now, post, save.Refusals(AttributeFormsTests.Forms));
        var mass = sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Property == "batteryMass");
        Assert.Equal("700", mass.Text("v.batteryMass"));
        var again = editor.Read(TargetKind.Model, now, PageTesting.Field(form), editor.Stamp(TargetKind.Model, now, "Model test"));
        Assert.Equal(["batteryMass"], again.Changed.Keys);
    }

    [Fact]
    public async Task AnEmptiedField_ShouldRemoveItsValue()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        form["v.batteryMass"] = string.Empty;

        var post = editor.Read(TargetKind.Model, opened, PageTesting.Field(form), editor.Stamp(TargetKind.Model, opened, "Model test"));
        var save = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, ModelId, post, confirmed: false);

        Assert.Equal(["batteryMass"], post.Removals);
        Assert.Equal((EditorOutcome.Saved, 0, 1), (save.Outcome, save.SavedCount, save.RemovedCount));
        Assert.False((await StoredAsync(rig)).ContainsKey("batteryMass"));
    }

    [Fact]
    public async Task EveryRowRemoved_ShouldRemoveTheList()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var opened = await StoredAsync(rig);
        var form = PageTesting.ShownForm(TargetKind.Model, opened);
        foreach (var name in form.Keys.Where(name => name.StartsWith("v.hazardousSubstancesAndTheirImpact[", StringComparison.Ordinal)).ToArray())
        {
            form[name] = string.Empty;
        }

        var post = editor.Read(TargetKind.Model, opened, PageTesting.Field(form), editor.Stamp(TargetKind.Model, opened, "Model test"));

        Assert.Equal(["hazardousSubstancesAndTheirImpact"], post.Removals);
    }

    [Fact]
    public async Task WithoutAStamp_AnEmptyFieldShouldKeepItsValue()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var opened = await StoredAsync(rig);

        var post = PageTesting.Editor(rig).Read(TargetKind.Model, opened, PageTesting.Field(new Dictionary<string, string> { ["v.batteryMass"] = "520" }));

        Assert.Equal(["batteryMass"], post.Changed.Keys);
        Assert.Empty(post.Removals);
    }

    [Fact]
    public void TheName_ShouldCountAsEditedOnlyWhenItDiffersFromWhatThePageShowed()
    {
        var stamp = new RecordEditor(AttributeFormsTests.Forms, null!, null!, null!).Stamp(TargetKind.Site, new Dictionary<string, AttributeValue>(), "Plant A");

        Assert.False(RecordEditor.NameEdited(stamp, "Plant A"));
        Assert.False(RecordEditor.NameEdited(stamp, " Plant A "));
        Assert.True(RecordEditor.NameEdited(stamp, "Plant B"));
        Assert.True(RecordEditor.NameEdited(null, "Plant A"));
    }
}
```

Patch `2d1-task4-tests.patch` (`BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs`). Save the block as `../2d1-task4-tests.patch`, outside the worktree, and run `git apply ../2d1-task4-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
--- a/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
+++ b/BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs
@@ -2,7 +2,9 @@ using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Forms;
 using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Publication;
 using BatteryPassWeb.Services;
 using Microsoft.Extensions.DependencyInjection;
@@ -32,6 +34,8 @@ public sealed class DppServiceWiringTests
         Assert.NotNull(provider.GetRequiredService<DocumentService>());
         Assert.True(provider.GetRequiredService<DppLinks>().IsConfigured);
         Assert.Same(provider.GetRequiredService<PublicationService>(), Assert.Single(provider.GetServices<IPackChangeListener>()));
+        Assert.NotNull(provider.GetRequiredService<RecordEditor>());
+        Assert.Same(provider.GetRequiredService<AttributeForms>(), provider.GetRequiredService<RecordEditor>().Forms);
     }
 
     [Fact]
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RecordEditorTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.EditorStampTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: the build fails because `BatteryPassWeb.Dpp.Pages` and `RecordEditor` don't exist yet (CS0234 and CS0246 errors).

- [ ] **Step 3: Write `RecordEditor.cs`**

`web/Dpp/Pages/RecordEditor.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>What was typed into a record's editor: the values that differ from the stored ones, and every text as typed.</summary>
public sealed record EditorPost(
    IReadOnlyDictionary<string, JsonNode> Changed,
    IReadOnlyDictionary<string, string> Texts,
    IReadOnlyDictionary<string, int> Rows)
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Stored values whose fields were emptied on the page: the team removes them.</summary>
    public IReadOnlySet<string> Removals { get; init; } = None;

    /// <summary>Fields edited on the page whose stored value someone else changed after the page was opened.</summary>
    public IReadOnlySet<string> Conflicts { get; init; } = None;

    /// <summary>The fields edited on the page; null when the page sent no stamp, so every field counts as edited.</summary>
    public IReadOnlySet<string>? Edited { get; init; }

    public bool WasEdited(string property) => Edited is null || Edited.Contains(property);
}

/// <summary>A published passport that a change reaches, with the version it is on now.</summary>
public sealed record AffectedPassport(string PackId, string SerialNumber, string? ClusterId, int CurrentVersion);

public enum EditorOutcome
{
    Saved,
    NothingChanged,
    Refused,
    NeedsConfirmation,
    NotFound,
}

/// <summary>
/// The result of saving an editor. <see cref="EditorOutcome.Refused"/> means some values were refused and the others
/// saved (J-12); <see cref="EditorOutcome.NeedsConfirmation"/> means nothing was saved yet because the change would
/// publish new versions of <see cref="Affected"/>. After a save, <see cref="Affected"/> lists the passports that got one.
/// </summary>
public sealed record EditorSave(EditorOutcome Outcome, IReadOnlyList<KeyResult> Keys, IReadOnlyList<AffectedPassport> Affected)
{
    public int SavedCount => Keys.Count(key => key.Outcome == KeyOutcome.Saved);

    public int RemovedCount => Keys.Count(key => key.Outcome == KeyOutcome.Removed);

    public int RefusedCount => Keys.Count(key => key.Outcome == KeyOutcome.Rejected);

    /// <summary>The refused values in plain words, with the parts of a grouped value they are about, by attribute property.</summary>
    public IReadOnlyDictionary<string, Refusal> Refusals(AttributeForms forms) => Keys
        .Where(key => key.Outcome == KeyOutcome.Rejected && AttributeCatalog.FindByProperty(key.Key) is not null)
        .ToDictionary(
            key => key.Key,
            key =>
            {
                var form = forms.For(AttributeCatalog.FindByProperty(key.Key)!);
                return new Refusal(RefusalWording.Describe(form, key.Message), RefusalWording.Parts(form, key.Message));
            },
            StringComparer.Ordinal);
}

/// <summary>Why a value was refused, in plain words, and which inputs of a grouped value it is about.</summary>
public sealed record Refusal(string Text, IReadOnlySet<string> Parts);

/// <summary>One attribute on an editor page, with what its inputs show and whether the record has a value for it.</summary>
public sealed record EditorField(
    AttributeForm Form,
    IReadOnlyDictionary<string, string> Texts,
    int Rows,
    string? Refusal,
    DppDocument? Document,
    bool HasValue)
{
    public AttributeDefinition Attribute => Form.Attribute;

    /// <summary>The inputs of a grouped value the refusal is about; empty means the whole field.</summary>
    public IReadOnlySet<string> RefusedParts { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>For a document field without an uploaded file in the app: the link its stored value points to.</summary>
    public string? StoredLink { get; init; }

    /// <summary>
    /// Empty although the passport needs it by February 2027 (the file's M or (M), with the Feb 2027 mark): what the team
    /// still has to fill in. Conditional values and values needed only later are not counted.
    /// </summary>
    public bool IsMissing => !HasValue && Attribute.Requirement != Requirement.Conditional && Attribute.MandatoryFromFeb2027;

    /// <summary>True when this input of a grouped value should be marked as refused.</summary>
    public bool IsRefused(FormInput input) =>
        Refusal is not null && (RefusedParts.Count == 0 || RefusedParts.Contains(input.Path));

    /// <summary>Who may see the value (or download the document), in words: design 5.2, the rule of <c>AudienceRules.MaySee</c>.</summary>
    public string SeenBy => Attribute.Audience switch
    {
        Audience.Authorities => "notified bodies, market surveillance, the Commission and your team",
        Audience.Shared => $"{SharedGroups()}, the battery's cluster, market surveillance, the Commission and your team",
        Audience.Purchaser => "the battery's cluster, market surveillance and your team",
        _ => "everyone",
    };

    private string SharedGroups()
    {
        var groups = new List<string>();
        if (Attribute.SharedWith.HasFlag(LegitimateInterestGroups.Repair))
        {
            groups.Add("repairers and remanufacturers");
        }

        if (Attribute.SharedWith.HasFlag(LegitimateInterestGroups.Recycling))
        {
            groups.Add("recyclers");
        }

        if (Attribute.SharedWith.HasFlag(LegitimateInterestGroups.Treatment))
        {
            groups.Add("treatment facilities");
        }

        return string.Join(", ", groups);
    }

    public string Text(string fieldName) => Texts.GetValueOrDefault(fieldName, string.Empty);

    /// <summary>The requirement in the grey marker: the file's M, (M) or C, and whether it is needed by February 2027.</summary>
    public string Marker => Attribute.Requirement == Requirement.Conditional
        ? "Conditional"
        : Attribute.MandatoryFromFeb2027 ? "Required by Feb 2027" : "Required later";

    /// <summary>Who may see the value, shown only when it isn't public.</summary>
    public string? AudienceLabel => Attribute.Audience switch
    {
        Audience.Purchaser => "Purchaser",
        Audience.Shared => "Shared",
        Audience.Authorities => "Authorities",
        _ => null,
    };
}

/// <summary>A group of fields with its side-menu entry and intro line; one per schema section.</summary>
public sealed record EditorSection(string Id, string Title, string Note, IReadOnlyList<EditorField> Fields)
{
    public int Filled => Fields.Count(item => item.HasValue);

    public int Missing => Fields.Count(item => item.IsMissing);
}

/// <summary>
/// The editor behind the model, batch, site and operator pages (design 6.2, decision 23): it shows a record's attributes
/// grouped by schema section, reads what was typed, keeps only the values that changed, asks before a change publishes
/// new versions, and saves through the save pipeline.
/// </summary>
public sealed class RecordEditor(AttributeForms forms, SavePipeline pipeline, DppStore store, DocumentStore documents)
{
    /// <summary>The title and intro line of each schema section.</summary>
    private static readonly Dictionary<string, (string Title, string Note)> SectionTexts = new(StringComparer.Ordinal)
    {
        [AttributeDefinition.HeaderSection] = ("Identifiers", "The operator's unique identifier, as registered."),
        ["identifiersAndProductData"] = ("Identifiers and product data", "What the battery is and who makes it."),
        ["symbolsLabelsAndDocumentationOfConformity"] = ("Symbols, labels and conformity", "Public documents are links to your website; test reports are uploaded and shown only to authorities."),
        ["batteryCarbonFootprint"] = ("Carbon footprint", "Per functional unit and per life-cycle stage. Not needed until after February 2027."),
        ["supplyChainDueDiligence"] = ("Due diligence", "A link to your supply chain due diligence report. Not needed until after February 2027."),
        ["batteryMaterialsAndComposition"] = ("Materials and composition", "Chemistry, critical raw materials and hazardous substances."),
        ["circularityAndResourceEfficiency"] = ("Circularity", "Recycled and renewable content, and what repairers, recyclers and end users need to know."),
        ["performanceAndDurability"] = ("Performance and durability", "Fades and expected lifetime are starting values; each battery re-states them when its status changes."),
    };

    public AttributeForms Forms => forms;

    // A site's and an operator's few values sit in the model's sections; their intro says what they are about.
    private static string Note(TargetKind kind, string section) => (kind, section) switch
    {
        (TargetKind.Site, "identifiersAndProductData") => "The manufacturer and the place where the batteries are made.",
        (TargetKind.Operator, "identifiersAndProductData") => "The economic operator that places the batteries on the market.",
        _ => SectionTexts[section].Note,
    };

    /// <summary>The attributes the team edits on a model, batch, site or operator, in catalogue order.</summary>
    public static IReadOnlyList<AttributeDefinition> AttributesOf(TargetKind kind) => AttributeCatalog.All
        .Where(attribute => kind switch
        {
            TargetKind.Model => attribute.Storage is StorageLocation.Model or StorageLocation.ModelWithPackOverride,
            TargetKind.Batch => attribute.Storage == StorageLocation.Batch,
            TargetKind.Site => attribute.Storage == StorageLocation.Site,
            TargetKind.Operator => attribute.Storage == StorageLocation.Operator,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "packs have their own page"),
        })
        .ToArray();

    /// <summary>How many of the record's attributes have a value.</summary>
    public static int CountFilled(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored) =>
        AttributesOf(kind).Count(attribute => stored.ContainsKey(attribute.Property));

    /// <summary>How many of the record's values the passport needs by February 2027 are still empty (see <see cref="EditorField.IsMissing"/>).</summary>
    public static int CountMissing(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored) =>
        AttributesOf(kind).Count(attribute =>
            !stored.ContainsKey(attribute.Property) && attribute.Requirement != Requirement.Conditional && attribute.MandatoryFromFeb2027);

    /// <summary>
    /// The values the schema allows only one way, which have no input (#20 battery category). A new record starts with
    /// them, so they never show as missing and a save counts only what was typed.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode> OneWayValues(TargetKind kind) =>
        AttributesOf(kind)
            .Select(forms.For)
            .Where(form => form.Kind == FormKind.Fixed)
            .ToDictionary(form => form.Attribute.Property, form => FormValues.Read(form, _ => null)!, StringComparer.Ordinal);

    /// <summary>
    /// What the page shows, as a short fingerprint per field (and one for the name): the page posts it back, so a save
    /// can tell the fields edited on the page from those it only showed. A page left open then never undoes a newer
    /// value saved by someone else, an upload or a feed.
    /// </summary>
    public string Stamp(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, string? name)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var parts = AttributesOf(kind)
            .Select(forms.For)
            .Where(form => form.Kind is not (FormKind.Upload or FormKind.Fixed))
            .Select(form => $"{form.Attribute.Property}:{Fingerprint(FormValues.Show(form, stored.GetValueOrDefault(form.Attribute.Property)?.Value))}");
        return string.Join(',', name is null ? parts : parts.Prepend($"{NameKey}:{Fingerprint([new(NameKey, name)])}"));
    }

    /// <summary>True when the posted name differs from the one the page showed (always, when the page sent no stamp).</summary>
    public static bool NameEdited(string? stamp, string? postedName) =>
        ParseStamp(stamp)?.GetValueOrDefault(NameKey) is not { } shown || shown != Fingerprint([new(NameKey, postedName ?? string.Empty)]);

    /// <summary>
    /// Reads the posted form. With the page's <paramref name="stamp"/>, only the fields edited on the page count: an
    /// edited field that differs from the stored value is a change, an emptied one a removal, and one whose stored value
    /// someone else changed meanwhile a conflict. Without a stamp, every non-blank value that differs is a change and
    /// nothing is removed.
    /// </summary>
    /// <param name="field">Gives the posted text of a field name, or null when it wasn't posted.</param>
    public EditorPost Read(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored, Func<string, string?> field, string? stamp = null)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(field);
        var shown = ParseStamp(stamp);
        var changed = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        var edited = new HashSet<string>(StringComparer.Ordinal);
        var removals = new HashSet<string>(StringComparer.Ordinal);
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in AttributesOf(kind))
        {
            var form = forms.For(attribute);
            var count = form.Kind == FormKind.Rows && int.TryParse(field(form.RowCountField), out var posted) ? posted : 0;
            rows[attribute.Property] = count;
            var names = FieldNames(form, count).ToArray();
            foreach (var name in names)
            {
                texts[name] = field(name) ?? string.Empty;
            }

            var current = stored.GetValueOrDefault(attribute.Property)?.Value;
            var wasShown = shown?.GetValueOrDefault(attribute.Property);
            if (wasShown is not null)
            {
                if (Fingerprint(names.Select(name => new KeyValuePair<string, string>(name, texts[name]))) == wasShown)
                {
                    continue; // not edited on the page: whatever is stored now stays
                }

                edited.Add(attribute.Property);
            }

            var changedMeanwhile = wasShown is not null && Fingerprint(FormValues.Show(form, current)) != wasShown;
            var value = FormValues.Read(form, field);
            if (value is null)
            {
                // Emptied on the page: a removal, only when the page says it was edited there.
                if (wasShown is not null && current is not null && names.All(name => string.IsNullOrWhiteSpace(texts[name])))
                {
                    (changedMeanwhile ? conflicts : removals).Add(attribute.Property);
                }

                continue;
            }

            if (FormValues.SameValue(current, value))
            {
                continue;
            }

            if (changedMeanwhile)
            {
                conflicts.Add(attribute.Property);
                continue;
            }

            changed[attribute.Property] = value;
        }

        return new EditorPost(changed, texts, rows)
        {
            Edited = shown is null ? null : edited,
            Removals = removals,
            Conflicts = conflicts,
        };
    }

    private const string NameKey = "@name";

    private static Dictionary<string, string>? ParseStamp(string? stamp)
    {
        if (string.IsNullOrWhiteSpace(stamp))
        {
            return null;
        }

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in stamp.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.LastIndexOf(':');
            if (colon > 0)
            {
                parsed[part[..colon]] = part[(colon + 1)..];
            }
        }

        return parsed;
    }

    /// <summary>
    /// A short fingerprint of a field's texts: the non-blank ones, trimmed, with browser line endings evened out, so
    /// an untouched field posts back exactly the fingerprint it was shown with.
    /// </summary>
    private static string Fingerprint(IEnumerable<KeyValuePair<string, string>> texts)
    {
        var canonical = new StringBuilder();
        foreach (var (name, text) in texts
            .Select(entry => (entry.Key, Text: entry.Value.Replace("\r\n", "\n", StringComparison.Ordinal).Trim()))
            .Where(entry => entry.Text.Length > 0)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            canonical.Append(name).Append('\u001f').Append(text).Append('\u001e');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())), 0, 6).ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The editor's sections. Without <paramref name="post"/> the fields show the stored values; with it, what was typed,
    /// so a refused value can be corrected in place.
    /// </summary>
    public async Task<IReadOnlyList<EditorSection>> SectionsAsync(
        TargetKind kind,
        string id,
        IReadOnlyDictionary<string, AttributeValue> stored,
        EditorPost? post = null,
        IReadOnlyDictionary<string, Refusal>? refusals = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stored);
        var fields = new List<EditorField>();
        foreach (var attribute in AttributesOf(kind))
        {
            var form = forms.For(attribute);
            var value = stored.GetValueOrDefault(attribute.Property)?.Value;

            // After a post, a field edited on the page shows what was typed; the others show what is stored now.
            var typed = post is not null && post.WasEdited(attribute.Property);
            var texts = typed ? post!.Texts : FormValues.Show(form, value);

            // Rows show the stored ones plus one blank row to add another; as typed, exactly the rows that were posted.
            var rows = typed ? post!.Rows.GetValueOrDefault(attribute.Property) : FormValues.RowCount(value) + 1;
            var document = form.Kind == FormKind.Upload
                ? (await documents.ListAsync(kind, id, attribute.Property, cancellationToken)).FirstOrDefault()
                : null;
            var refusal = refusals?.GetValueOrDefault(attribute.Property);
            fields.Add(new EditorField(form, texts, rows, refusal?.Text, document, value is not null)
            {
                RefusedParts = refusal?.Parts ?? new HashSet<string>(StringComparer.Ordinal),
                StoredLink = form.Kind == FormKind.Upload && value?["url"] is JsonValue url && url.TryGetValue<string>(out var link) ? link : null,
            });
        }

        return fields
            .GroupBy(field => field.Attribute.Section)
            .Select(group => new EditorSection($"section-{group.Key}", SectionTexts[group.Key].Title, Note(kind, group.Key), group.ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Saves the changed values. A change that would publish new versions of published passports is only saved once
    /// <paramref name="confirmed"/>; until then the result lists those passports and nothing is stored.
    /// </summary>
    public async Task<EditorSave> SaveAsync(ChangeSender sender, TargetKind kind, string id, EditorPost post, bool confirmed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);
        var conflicts = post.Conflicts.Select(key => new KeyResult(key, KeyOutcome.Rejected, ConflictMessage)).ToArray();
        if (post.Changed.Count == 0 && post.Removals.Count == 0)
        {
            return conflicts.Length > 0
                ? new EditorSave(EditorOutcome.Refused, conflicts, [])
                : new EditorSave(EditorOutcome.NothingChanged, [], []);
        }

        var changes = new ChangeSet(
            kind,
            id,
            post.Changed.ToDictionary(entry => entry.Key, entry => (JsonNode?)entry.Value.DeepClone(), StringComparer.Ordinal),
            "form",
            Removals: post.Removals.ToArray());
        var preview = await pipeline.SaveAsync(sender, changes, preview: true, cancellationToken);
        if (!preview.Found)
        {
            return new EditorSave(EditorOutcome.NotFound, [], []);
        }

        var publishes = preview.Keys.Any(key => key.Outcome is KeyOutcome.Saved or KeyOutcome.Removed && AttributeCatalog.FindByProperty(key.Key)?.Change != ChangeRule.Live);
        var affected = publishes ? await AffectedAsync(kind, id, cancellationToken) : [];
        if (affected.Count > 0 && !confirmed)
        {
            return new EditorSave(EditorOutcome.NeedsConfirmation, [.. preview.Keys, .. conflicts], affected);
        }

        // Saved or refused: Affected now lists the passports that got a new version.
        var result = await pipeline.SaveAsync(sender, changes, preview: false, cancellationToken);
        IReadOnlyList<KeyResult> keys = [.. result.Keys, .. conflicts];
        return new EditorSave(keys.Any(key => key.Outcome == KeyOutcome.Rejected) ? EditorOutcome.Refused : EditorOutcome.Saved, keys, affected);
    }

    /// <summary>Why an edit made on a page that was open while someone else saved the same value is refused.</summary>
    public const string ConflictMessage =
        "Someone else saved this value after you opened this page. What you typed is still here: save again to use it, or Cancel edits to see theirs.";

    /// <summary>The published passports built from this record, which a static change gives a new version.</summary>
    public async Task<IReadOnlyList<AffectedPassport>> AffectedAsync(TargetKind kind, string id, CancellationToken cancellationToken = default)
    {
        var packIds = kind switch
        {
            TargetKind.Model => await store.FindPackIdsUsingAsync(modelId: id, cancellationToken: cancellationToken),
            TargetKind.Site => await store.FindPackIdsUsingAsync(siteId: id, cancellationToken: cancellationToken),
            TargetKind.Batch => await store.FindPackIdsUsingAsync(batchId: id, cancellationToken: cancellationToken),
            TargetKind.Operator => await store.FindPackIdsUsingAsync(operatorId: id, cancellationToken: cancellationToken),
            _ => [id],
        };
        var versions = (await store.ListActiveVersionsAsync(packIds, cancellationToken)).ToDictionary(version => version.PackId, StringComparer.Ordinal);
        var packs = await store.GetManyAsync<BatteryPack>(versions.Keys, cancellationToken);
        return packs
            .Select(pack => new AffectedPassport(
                pack.Id,
                pack.Values.GetValueOrDefault("batterySerialNumber")?.Value?.GetValue<string>() ?? pack.Id,
                pack.ClusterId,
                versions[pack.Id].Number))
            .OrderBy(passport => passport.SerialNumber, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> FieldNames(AttributeForm form, int rows) => form.Kind switch
    {
        FormKind.Upload or FormKind.Fixed => [],
        FormKind.Rows => Enumerable.Range(0, rows).SelectMany(row => form.Inputs.Select(input => form.FieldName(input, row))),
        _ => form.Inputs.Select(input => form.FieldName(input)),
    };
}
```

- [ ] **Step 4: Register the forms and the editor**

Patch `2d1-task4-wiring.patch` (`web/Dpp/DppServiceCollectionExtensions.cs`). Save the block as `../2d1-task4-wiring.patch`, outside the worktree, and run `git apply ../2d1-task4-wiring.patch` from the worktree root:

```diff
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -1,6 +1,8 @@
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
+using BatteryPassWeb.Dpp.Forms;
 using BatteryPassWeb.Dpp.Identifiers;
+using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Publication;
 using BatteryPassWeb.Dpp.Schemas;
 using BatteryPassWeb.Dpp.Signing;
@@ -35,6 +37,8 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<DppLinks>();
         services.AddSingleton<DocumentStore>();
         services.AddSingleton<DocumentService>();
+        services.AddSingleton<AttributeForms>();
+        services.AddSingleton<RecordEditor>();
         return services;
     }
 }
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RecordEditorTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.EditorStampTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.DppServiceWiringTests"`
Expected: `Passed!  - Failed:     0, Passed:    25`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1801`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/PageTesting.cs BatteryPassWeb.Tests/Dpp/RecordEditorTests.cs BatteryPassWeb.Tests/Dpp/EditorStampTests.cs web/Dpp/Pages/RecordEditor.cs BatteryPassWeb.Tests/Dpp/DppServiceWiringTests.cs web/Dpp/DppServiceCollectionExtensions.cs
git commit -m "feat(dpp): edit a record's values by schema section, saving only what was edited"
```


---

### Task 5: The page services

Loading the pages, and the changes that aren't passport data: new records, names, the family picture, copies and removals. The PDF, PNG and JPEG check moves out of `DocumentService` into `FileTypes`, so the picture uses it too.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/ProductDataPagesTests.cs`
- Test: `BatteryPassWeb.Tests/Dpp/RecordLifecycleTests.cs`
- Create: `web/Dpp/Documents/FileTypes.cs`
- Create: `web/Dpp/Pages/Words.cs`
- Create: `web/Dpp/Pages/ProductDataPages.cs`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PageTesting.cs`
- Modify: `web/Dpp/Documents/DocumentService.cs`
- Modify: `web/Dpp/Store/DppStore.cs`
- Modify: `web/Dpp/Documents/DocumentStore.cs`
- Modify: `web/Dpp/DppServiceCollectionExtensions.cs`

- [ ] **Step 1: Write the failing tests**

Patch `2d1-task5-tests.patch` (`BatteryPassWeb.Tests/Dpp/PageTesting.cs`). Save the block as `../2d1-task5-tests.patch`, outside the worktree, and run `git apply ../2d1-task5-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PageTesting.cs b/BatteryPassWeb.Tests/Dpp/PageTesting.cs
--- a/BatteryPassWeb.Tests/Dpp/PageTesting.cs
+++ b/BatteryPassWeb.Tests/Dpp/PageTesting.cs
@@ -48,4 +48,12 @@ internal static class PageTesting
     }
 
     public static Func<string, string?> Field(IReadOnlyDictionary<string, string> form) => name => form.GetValueOrDefault(name);
+
+    public static ProductDataPages Pages(PublicationRig rig) => new(
+        rig.Store,
+        Editor(rig),
+        new DocumentStore(rig.Context),
+        new ApplicationAuditService(rig.Context),
+        Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }),
+        rig.Clock);
 }
```

`BatteryPassWeb.Tests/Dpp/ProductDataPagesTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class ProductDataPagesTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    [Fact]
    public async Task Families_ShouldCountModelsBatchesAndBatteries()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);

        var row = Assert.Single((await PageTesting.Pages(rig).FamiliesAsync()).Families);

        Assert.Equal(("family-test", 1, 1, 1, 1), (row.Family.Id, row.Models, row.Batches, row.Packs, row.Published));
    }

    [Fact]
    public async Task ANewModel_ShouldShowWhatItStillNeedsByFeb2027()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var modelId = (await pages.CreateModelAsync(Who, "family-test", "C7M-2"))!;

        var family = Assert.Single((await pages.FamiliesAsync()).Families);
        var page = (await pages.FamilyAsync("family-test", modelId))!;

        Assert.Equal(27, family.Missing); // the seeded model and batch are complete; #20 is filled in on creation
        var chips = page.Models.ToDictionary(chip => chip.Model.Id, chip => chip.Missing);
        Assert.Equal((0, 27), (chips["model-test"], chips[modelId]));
        Assert.Equal(27, page.ModelEditor!.Missing);
    }

    [Fact]
    public async Task ANewModel_ShouldStartWithTheValueTheSchemaAllowsOnlyOneWay()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var modelId = (await PageTesting.Pages(rig).CreateModelAsync(Who, "family-test", "C7M-3"))!;

        var stored = (await rig.Store.GetAsync<BatteryModel>(modelId))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.batteryMass"] = "512";
        var post = PageTesting.Editor(rig).Read(TargetKind.Model, stored, PageTesting.Field(form));

        var category = Assert.Single(stored); // #20 battery category has no input: the schema allows one value
        Assert.Equal(("batteryCategory", "industrial/non-stationary battery"), (category.Key, category.Value.Value["batteryCategoryValue"]!.GetValue<string>()));
        Assert.Equal((Who, "form"), (category.Value.Who, category.Value.Source));
        Assert.Equal(["batteryMass"], post.Changed.Keys); // so typing one value saves one value
    }

    [Fact]
    public async Task Family_ShouldOpenItsFirstModelWithItsBatchesAndEditor()
    {
        var rig = await PublicationTesting.RigAsync(mongo);

        var page = (await PageTesting.Pages(rig).FamilyAsync("family-test", modelId: null))!;

        Assert.Equal("model-test", page.SelectedModel?.Id);
        var batch = Assert.Single(page.Batches);
        Assert.Equal(("Example plant", 16, 16, 1, 0), (batch.SiteName, batch.Filled, batch.Total, batch.Packs, batch.Published));
        Assert.Equal((TargetKind.Model, 37, 37), (page.ModelEditor!.Kind, page.ModelEditor.Filled, page.ModelEditor.Total));
        Assert.Equal("/admin/models/model-test", page.ModelEditor.SaveAction);
        Assert.Null(await PageTesting.Pages(rig).FamilyAsync("no-such-family", null));
    }

    [Fact]
    public async Task AQuestionToConfirm_ShouldReachTheEditor()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var editor = PageTesting.Editor(rig);
        var stored = (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values;
        var form = PageTesting.ShownForm(TargetKind.Model, stored);
        form["v.ratedCapacity"] = "210";
        var post = editor.Read(TargetKind.Model, stored, PageTesting.Field(form));
        var save = await editor.SaveAsync(PublicationTesting.Team, TargetKind.Model, "model-test", post, confirmed: false);

        var page = (await PageTesting.Pages(rig).FamilyAsync("family-test", "model-test", new EditorState(post, save)))!;

        Assert.Equal("VPC8C05-6788", Assert.Single(page.ModelEditor!.Confirm).SerialNumber);
        Assert.Equal(1, page.ModelEditor.PublishedPassports);
        Assert.Equal("210", page.ModelEditor.Sections.SelectMany(section => section.Fields).Single(field => field.Attribute.Number == 57).Text("v.ratedCapacity"));
    }

    [Fact]
    public async Task NewBatches_ShouldBeOnePerYearAndSite()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.Contains("already has a 2026 batch", (await pages.CreateBatchAsync(Who, "model-test", 2026, "site-test")).Problem);
        Assert.Contains("calendar year", (await pages.CreateBatchAsync(Who, "model-test", 26, "site-test")).Problem);
        Assert.Contains("manufacturing site", (await pages.CreateBatchAsync(Who, "model-test", 2027, "no-such-site")).Problem);
        Assert.Contains("model was not found", (await pages.CreateBatchAsync(Who, "no-such-model", 2027, "site-test")).Problem);

        var (batchId, problem) = await pages.CreateBatchAsync(Who, "model-test", 2027, "site-test");

        Assert.Null(problem);
        Assert.Equal(2027, (await rig.Store.GetAsync<ProductionBatch>(batchId!))!.Year);
    }

    [Fact]
    public async Task NewRecords_ShouldBeCreatedAndLogged()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        var familyId = await pages.CreateFamilyAsync(Who, "  Compact 7M ");
        var modelId = await pages.CreateModelAsync(Who, familyId, "C7M-1");
        var siteId = await pages.CreateSiteAsync(Who, "Plant 2");
        var operatorId = await pages.CreateOperatorAsync(Who, "Operator 2");

        Assert.Equal("Compact 7M", (await rig.Store.GetAsync<BatteryFamily>(familyId))!.Name);
        Assert.Equal(familyId, (await rig.Store.GetAsync<BatteryModel>(modelId!))!.FamilyId);
        Assert.NotNull(await rig.Store.GetAsync<ManufacturingSite>(siteId));
        Assert.NotNull(await rig.Store.GetAsync<EconomicOperator>(operatorId));
        Assert.Null(await pages.CreateModelAsync(Who, "no-such-family", "X"));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Equal(4, events.Count(e => e["eventType"].AsString == ProductDataPages.CreatedEvent));
    }

    [Fact]
    public async Task Rename_ShouldOnlyActOnANewName()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.False(await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", " Example plant "));
        Assert.False(await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", "  "));
        Assert.True(await pages.RenameAsync<ManufacturingSite>(Who, "site-test", "Example plant", "Plant 1"));

        Assert.Equal("Plant 1", (await rig.Store.GetAsync<ManufacturingSite>("site-test"))!.Name);
    }

    [Fact]
    public async Task FamilyPicture_ShouldBeAPngOrJpeg()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.Contains("PNG or JPEG", await pages.SetFamilyPictureAsync(Who, "family-test", "manual.pdf", DocumentTesting.Pdf()));
        Assert.Contains("PNG or JPEG", await pages.SetFamilyPictureAsync(Who, "family-test", "empty.png", []));
        Assert.Contains("not found", await pages.SetFamilyPictureAsync(Who, "no-such-family", "f.png", DocumentTesting.Png()));
        Assert.Null(await pages.FamilyPictureAsync("family-test"));

        Assert.Null(await pages.SetFamilyPictureAsync(Who, "family-test", "C:\\pictures\\family.png", DocumentTesting.Png()));

        var picture = (await pages.FamilyPictureAsync("family-test"))!.Value;
        Assert.Equal(DocumentTesting.Png(), picture.Content);
        Assert.Equal(FileTypes.Png, picture.ContentType);
    }

    [Fact]
    public async Task SitesAndOperators_ShouldOpenTheChosenRecord()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        var none = await pages.SitesAndOperatorsAsync(null, null);
        var site = await pages.SitesAndOperatorsAsync("site-test", null);
        var economicOperator = await pages.SitesAndOperatorsAsync(null, "operator-test");

        Assert.Null(none.Editor);
        Assert.Equal((1, 3, 3), (Assert.Single(none.Sites).Batches, none.Sites[0].Filled, none.Sites[0].Total));
        Assert.Equal(1, Assert.Single(none.Operators).Packs);
        Assert.Equal((TargetKind.Site, "/admin/sites/site-test"), (site.Editor!.Kind, site.Editor.SaveAction));
        Assert.Equal((TargetKind.Operator, 2), (economicOperator.Editor!.Kind, economicOperator.Editor.Total));
    }
}
```

`BatteryPassWeb.Tests/Dpp/RecordLifecycleTests.cs`:

```csharp
using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Removing records made by mistake (only while nothing uses them), starting a model from a copy, and refusing a name
/// another record has.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RecordLifecycleTests(MongoFixture mongo)
{
    private const string Who = "team@example.com";

    [Fact]
    public async Task RecordsInUse_ShouldSayWhyTheyCantBeRemoved()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);

        Assert.Equal("It still has 1 battery model. Remove them first.", await pages.WhyNotRemovableAsync(RecordKind.Family, "family-test"));
        Assert.Equal("1 battery uses it.", await pages.WhyNotRemovableAsync(RecordKind.Model, "model-test"));
        Assert.Equal("1 battery belongs to it.", await pages.WhyNotRemovableAsync(RecordKind.Batch, "batch-test-2026"));
        Assert.Equal("1 batch was made there.", await pages.WhyNotRemovableAsync(RecordKind.Site, "site-test"));
        Assert.Equal("1 battery names it as economic operator.", await pages.WhyNotRemovableAsync(RecordKind.Operator, "operator-test"));

        var (removed, problem, _) = await pages.RemoveAsync(Who, RecordKind.Model, "model-test");
        Assert.False(removed);
        Assert.Equal("1 battery uses it.", problem);
        Assert.NotNull(await rig.Store.GetAsync<BatteryModel>("model-test"));
    }

    [Fact]
    public async Task UnusedRecords_ShouldBeRemovedWithTheirDocumentsAndLogged()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var familyId = await pages.CreateFamilyAsync(Who, "Mistake family");
        var modelId = (await pages.CreateModelAsync(Who, familyId, "Mistake model"))!;
        var (batchId, _) = await pages.CreateBatchAsync(Who, modelId, 2026, "site-test");
        await PageTesting.Documents(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, modelId, "dismantlingInformation", new DocumentUpload("manual.pdf", DocumentTesting.Pdf()));

        Assert.Equal("It still has 1 batch. Remove them first.", await pages.WhyNotRemovableAsync(RecordKind.Model, modelId));
        Assert.True((await pages.RemoveAsync(Who, RecordKind.Batch, batchId!)).Removed);
        Assert.True((await pages.RemoveAsync(Who, RecordKind.Model, modelId)).Removed);
        Assert.True((await pages.RemoveAsync(Who, RecordKind.Family, familyId)).Removed);

        Assert.Null(await rig.Store.GetAsync<ProductionBatch>(batchId!));
        Assert.Null(await rig.Store.GetAsync<BatteryModel>(modelId));
        Assert.Null(await rig.Store.GetAsync<BatteryFamily>(familyId));
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Model, modelId, "dismantlingInformation"));
        var events = await new ApplicationAuditService(rig.Context).ListApplicationAuditEventsAsync(new ApplicationAuditFilter());
        Assert.Equal(3, events.Count(e => e["eventType"].AsString == ProductDataPages.RemovedEvent));
    }

    [Fact]
    public async Task ACopiedModel_ShouldStartWithTheTypedValuesButNotTheDocuments()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var source = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;

        var copyId = await pages.CreateModelAsync(Who, "family-test", "Model test 2", "model-test");

        var copy = (await rig.Store.GetAsync<BatteryModel>(copyId!))!;
        var uploads = RecordEditor.AttributesOf(TargetKind.Model)
            .Where(attribute => AttributeFormsTests.Forms.For(attribute).Kind == BatteryPassWeb.Dpp.Forms.FormKind.Upload)
            .Select(attribute => attribute.Property)
            .ToHashSet();
        Assert.Equal(source.Values.Keys.Where(key => !uploads.Contains(key)).Order(), copy.Values.Keys.Order());
        Assert.True(JsonNode.DeepEquals(source.Values["batteryMass"].Value, copy.Values["batteryMass"].Value));
        Assert.All(copy.Values.Values, value => Assert.Equal((Who, "form"), (value.Who, value.Source)));
    }

    [Fact]
    public async Task ANameAnotherRecordHas_ShouldBeRefused()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var pages = PageTesting.Pages(rig);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        var site = (await rig.Store.GetAsync<ManufacturingSite>("site-test"))!;

        Assert.NotNull(await pages.NameTakenAsync(RecordKind.Model, model.Name.ToUpperInvariant(), "family-test"));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Model, model.Name, "another-family"));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Model, model.Name, "family-test", exceptId: "model-test"));
        Assert.NotNull(await pages.NameTakenAsync(RecordKind.Site, $" {site.Name} "));
        Assert.Null(await pages.NameTakenAsync(RecordKind.Operator, "A new operator"));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ProductDataPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RecordLifecycleTests"`
Expected: the build fails because `ProductDataPages` doesn't exist yet (CS0246 errors).

- [ ] **Step 3: Write `FileTypes.cs`**

`web/Dpp/Documents/FileTypes.cs`:

```csharp
namespace BatteryPassWeb.Dpp.Documents;

/// <summary>Recognises the accepted file types by their first bytes, never by the file name.</summary>
public static class FileTypes
{
    public const string Pdf = "application/pdf";
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";

    /// <summary>The content type of a PDF, PNG or JPEG file; null for anything else.</summary>
    public static string? Detect(ReadOnlySpan<byte> content) =>
        content.StartsWith("%PDF-"u8) ? Pdf
        : content.StartsWith(PngSignature) ? Png
        : content.StartsWith(JpegSignature) ? Jpeg
        : null;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
}
```

- [ ] **Step 4: Write `Words.cs`**

`web/Dpp/Pages/Words.cs`:

```csharp
using System.Globalization;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>Counts in plain English on the team pages: "1 value", "3 values", never "value(s)".</summary>
public static class Words
{
    /// <summary>The count and the word, singular for 1: <c>Count(3, "value")</c> is "3 values".</summary>
    /// <param name="many">The plural when it isn't <paramref name="one"/> plus "s".</param>
    public static string Count(int count, string one, string? many = null) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? one : many ?? one + "s")}";

    /// <summary>"is" or "are", to agree with a count.</summary>
    public static string Is(int count) => count == 1 ? "is" : "are";
}
```

- [ ] **Step 5: Write `ProductDataPages.cs`**

`web/Dpp/Pages/ProductDataPages.cs`:

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>The records the team pages create, rename and remove.</summary>
public enum RecordKind
{
    Family,
    Model,
    Batch,
    Site,
    Operator,
}

/// <summary>Where a record is removed, and why it can't be yet (null when it can).</summary>
public sealed record RemovalView(string Action, string? Blocked);

/// <summary>A model a new one can start from, with its family's name for grouping.</summary>
public sealed record CopySource(string FamilyName, BatteryModel Model);

/// <summary>One record's editor on a page: where it saves, what it shows, and the republish question when there is one.</summary>
public sealed record RecordEditorView(
    TargetKind Kind,
    string Id,
    string Name,
    string SaveAction,
    string CancelHref,
    IReadOnlyList<EditorSection> Sections,
    IReadOnlyList<AffectedPassport> Confirm,
    int PublishedPassports,
    IReadOnlyList<string> Languages,
    string Stamp,
    RemovalView Removal)
{
    public string UploadAction(string property) => $"{SaveAction}/documents/{Uri.EscapeDataString(property)}";

    public string KindLabel => Kind switch
    {
        TargetKind.Model => "Battery model",
        TargetKind.Batch => "Batch",
        TargetKind.Site => "Manufacturing site",
        TargetKind.Operator => "Economic operator",
        _ => "Record",
    };

    public int Filled => Sections.Sum(section => section.Filled);

    public int Total => Sections.Sum(section => section.Fields.Count);

    /// <summary>Values the passport needs by February 2027 that are still empty.</summary>
    public int Missing => Sections.Sum(section => section.Missing);
}

/// <summary>One field as its editor shows it.</summary>
public sealed record EditorFieldView(RecordEditorView Editor, EditorField Field);

/// <summary>An editor shown again after a post: what was typed and how the save went.</summary>
public sealed record EditorState(EditorPost Post, EditorSave Save);

public sealed record FamilyRow(BatteryFamily Family, int Models, int Batches, int Packs, int Published, int Missing);

/// <summary>How complete a record is: values filled in, of how many, and how many the passport needs by Feb 2027 that are still empty.</summary>
public sealed record Completeness(int Filled, int Total, int Missing);

public sealed record ModelChip(BatteryModel Model, int Filled, int Total, int Missing);

public sealed record BatchRow(ProductionBatch Batch, string SiteName, int Filled, int Total, int Missing, int Packs, int Published);

public sealed record SiteRow(ManufacturingSite Site, int Filled, int Total, int Missing, int Batches);

public sealed record OperatorRow(EconomicOperator Operator, int Filled, int Total, int Missing, int Packs);

public sealed record FamiliesPage(IReadOnlyList<FamilyRow> Families);

public sealed record FamilyPage(
    BatteryFamily Family,
    IReadOnlyList<ModelChip> Models,
    BatteryModel? SelectedModel,
    IReadOnlyList<BatchRow> Batches,
    IReadOnlyList<ManufacturingSite> Sites,
    RecordEditorView? ModelEditor)
{
    /// <summary>Removing the family: possible once it has no models.</summary>
    public RemovalView Removal { get; init; } = new(string.Empty, null);

    /// <summary>The models a new model can start from: this family's first, then the others by family.</summary>
    public IReadOnlyList<CopySource> CopySources { get; init; } = [];
}

public sealed record BatchPage(ProductionBatch Batch, BatteryModel Model, BatteryFamily Family, ManufacturingSite Site, RecordEditorView Editor);

public sealed record SitesAndOperatorsPage(IReadOnlyList<SiteRow> Sites, IReadOnlyList<OperatorRow> Operators, RecordEditorView? Editor);

/// <summary>
/// The team's product data pages (decision 23): battery families with their models and batches, and the shared
/// manufacturing sites and economic operators. Passport values are saved by <see cref="RecordEditor"/>; this class
/// loads the pages and makes the changes that aren't passport data (new records, names, the family picture).
/// </summary>
public sealed class ProductDataPages(
    DppStore store,
    RecordEditor editor,
    DocumentStore documents,
    ApplicationAuditService audit,
    IOptions<BatteryPassOptions> options,
    TimeProvider clock)
{
    public const string CreatedEvent = "dpp.record-created";
    public const string RenamedEvent = "dpp.record-renamed";
    public const string PictureEvent = "dpp.family-picture-set";
    public const string RemovedEvent = "dpp.record-removed";

    public async Task<FamiliesPage> FamiliesAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<FamilyRow>();
        foreach (var family in (await store.ListAsync<BatteryFamily>(cancellationToken)).OrderBy(family => family.Name, StringComparer.OrdinalIgnoreCase))
        {
            var models = await store.ListModelsAsync(family.Id, cancellationToken);
            var batches = new List<ProductionBatch>();
            foreach (var model in models)
            {
                batches.AddRange(await store.ListBatchesAsync(model.Id, cancellationToken));
            }

            var counts = (await store.CountPacksByBatchAsync(batches.Select(batch => batch.Id), cancellationToken)).Values;
            var missing = models.Sum(model => RecordEditor.CountMissing(TargetKind.Model, model.Values))
                + batches.Sum(batch => RecordEditor.CountMissing(TargetKind.Batch, batch.Values));
            rows.Add(new FamilyRow(family, models.Count, batches.Count, counts.Sum(count => count.Packs), counts.Sum(count => count.Published), missing));
        }

        return new FamiliesPage(rows);
    }

    /// <summary>A family with its models; the selected model (the first one if none is asked for) with its batches and editor.</summary>
    public async Task<FamilyPage?> FamilyAsync(string familyId, string? modelId, EditorState? state = null, CancellationToken cancellationToken = default)
    {
        var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
        if (family is null)
        {
            return null;
        }

        var models = await store.ListModelsAsync(familyId, cancellationToken);
        var total = RecordEditor.AttributesOf(TargetKind.Model).Count;
        var chips = models.Select(model => new ModelChip(model, RecordEditor.CountFilled(TargetKind.Model, model.Values), total, RecordEditor.CountMissing(TargetKind.Model, model.Values))).ToArray();
        var selected = models.FirstOrDefault(model => model.Id == modelId) ?? models.FirstOrDefault();
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var removal = new RemovalView($"/admin/families/{Uri.EscapeDataString(familyId)}/remove", await WhyNotRemovableAsync(RecordKind.Family, familyId, cancellationToken));
        var copySources = await CopySourcesAsync(family, models, cancellationToken);
        if (selected is null)
        {
            return new FamilyPage(family, chips, null, [], sites, null) { Removal = removal, CopySources = copySources };
        }

        var batches = await store.ListBatchesAsync(selected.Id, cancellationToken);
        var counts = await store.CountPacksByBatchAsync(batches.Select(batch => batch.Id), cancellationToken);
        var siteNames = sites.ToDictionary(site => site.Id, site => site.Name, StringComparer.Ordinal);
        var batchTotal = RecordEditor.AttributesOf(TargetKind.Batch).Count;
        var rows = batches
            .Select(batch => new BatchRow(
                batch,
                siteNames.GetValueOrDefault(batch.SiteId, batch.SiteId),
                RecordEditor.CountFilled(TargetKind.Batch, batch.Values),
                batchTotal,
                RecordEditor.CountMissing(TargetKind.Batch, batch.Values),
                counts[batch.Id].Packs,
                counts[batch.Id].Published))
            .ToArray();
        var view = await EditorAsync(TargetKind.Model, selected.Id, selected.Name, selected.Values, $"/admin/models/{Uri.EscapeDataString(selected.Id)}", FamilyHref(familyId, selected.Id), state, cancellationToken);
        return new FamilyPage(family, chips, selected, rows, sites, view) { Removal = removal, CopySources = copySources };
    }

    private async Task<IReadOnlyList<CopySource>> CopySourcesAsync(BatteryFamily family, IReadOnlyList<BatteryModel> own, CancellationToken cancellationToken)
    {
        var families = (await store.ListAsync<BatteryFamily>(cancellationToken)).ToDictionary(item => item.Id, item => item.Name, StringComparer.Ordinal);
        var others = (await store.ListAsync<BatteryModel>(cancellationToken))
            .Where(model => model.FamilyId != family.Id && families.ContainsKey(model.FamilyId))
            .Select(model => new CopySource(families[model.FamilyId], model))
            .OrderBy(source => source.FamilyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.Model.Name, StringComparer.OrdinalIgnoreCase);
        return own.Select(model => new CopySource(family.Name, model)).Concat(others).ToArray();
    }

    public async Task<BatchPage?> BatchAsync(string batchId, EditorState? state = null, CancellationToken cancellationToken = default)
    {
        var batch = await store.GetAsync<ProductionBatch>(batchId, cancellationToken);
        var model = batch is null ? null : await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        var family = model is null ? null : await store.GetAsync<BatteryFamily>(model.FamilyId, cancellationToken);
        var site = batch is null ? null : await store.GetAsync<ManufacturingSite>(batch.SiteId, cancellationToken);
        if (batch is null || model is null || family is null || site is null)
        {
            return null;
        }

        var href = $"/admin/batches/{Uri.EscapeDataString(batch.Id)}";
        var view = await EditorAsync(TargetKind.Batch, batch.Id, $"Batch {batch.Year} · {site.Name}", batch.Values, href, href, state, cancellationToken);
        return new BatchPage(batch, model, family, site, view);
    }

    /// <summary>The site and operator lists, with the editor of the one asked for (a site wins over an operator).</summary>
    public async Task<SitesAndOperatorsPage> SitesAndOperatorsAsync(string? siteId, string? operatorId, EditorState? state = null, CancellationToken cancellationToken = default)
    {
        var sites = (await store.ListAsync<ManufacturingSite>(cancellationToken)).OrderBy(site => site.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var operators = (await store.ListAsync<EconomicOperator>(cancellationToken)).OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var batchesPerSite = (await store.ListAsync<ProductionBatch>(cancellationToken)).GroupBy(batch => batch.SiteId).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var siteRows = sites.Select(site => new SiteRow(site, RecordEditor.CountFilled(TargetKind.Site, site.Values), RecordEditor.AttributesOf(TargetKind.Site).Count, RecordEditor.CountMissing(TargetKind.Site, site.Values), batchesPerSite.GetValueOrDefault(site.Id))).ToArray();
        var operatorRows = new List<OperatorRow>();
        foreach (var item in operators)
        {
            var packs = (await store.FindPackIdsUsingAsync(operatorId: item.Id, cancellationToken: cancellationToken)).Count;
            operatorRows.Add(new OperatorRow(item, RecordEditor.CountFilled(TargetKind.Operator, item.Values), RecordEditor.AttributesOf(TargetKind.Operator).Count, RecordEditor.CountMissing(TargetKind.Operator, item.Values), packs));
        }

        RecordEditorView? view = null;
        if (sites.FirstOrDefault(site => site.Id == siteId) is { } selectedSite)
        {
            var href = $"/admin/sites/{Uri.EscapeDataString(selectedSite.Id)}";
            view = await EditorAsync(TargetKind.Site, selectedSite.Id, selectedSite.Name, selectedSite.Values, href, SitesHref("site", selectedSite.Id), state, cancellationToken);
        }
        else if (operators.FirstOrDefault(item => item.Id == operatorId) is { } selectedOperator)
        {
            var href = $"/admin/operators/{Uri.EscapeDataString(selectedOperator.Id)}";
            view = await EditorAsync(TargetKind.Operator, selectedOperator.Id, selectedOperator.Name, selectedOperator.Values, href, SitesHref("operator", selectedOperator.Id), state, cancellationToken);
        }

        return new SitesAndOperatorsPage(siteRows, operatorRows, view);
    }

    public async Task<string> CreateFamilyAsync(string who, string name, CancellationToken cancellationToken = default)
    {
        var family = new BatteryFamily { Id = NewId("family"), Name = name.Trim() };
        await store.InsertAsync(family, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Battery family {family.Name} created", "dpp-family", family.Id, cancellationToken);
        return family.Id;
    }

    /// <summary>
    /// The new model's id; null when the family doesn't exist. With <paramref name="copyFromId"/>, the model starts with
    /// that model's typed values, as entered by <paramref name="who"/> now; its uploaded documents stay with it.
    /// </summary>
    public async Task<string?> CreateModelAsync(string who, string familyId, string name, string? copyFromId = null, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryFamily>(familyId, cancellationToken) is null)
        {
            return null;
        }

        var values = OneWayValues(TargetKind.Model, who);
        var source = string.IsNullOrEmpty(copyFromId) ? null : await store.GetAsync<BatteryModel>(copyFromId, cancellationToken);
        if (source is not null)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var attribute in RecordEditor.AttributesOf(TargetKind.Model))
            {
                if (editor.Forms.For(attribute).Kind is not (Forms.FormKind.Upload or Forms.FormKind.Fixed)
                    && source.Values.TryGetValue(attribute.Property, out var copied))
                {
                    values[attribute.Property] = new AttributeValue { Value = copied.Value.DeepClone(), GeneratedAtUtc = now, Source = "form", Who = who };
                }
            }
        }

        var model = new BatteryModel { Id = NewId("model"), FamilyId = familyId, Name = name.Trim(), Values = values };
        await store.InsertAsync(model, cancellationToken);
        await AuditAsync(
            CreatedEvent,
            who,
            source is null ? $"Battery model {model.Name} created" : $"Battery model {model.Name} created as a copy of {source.Name} ({Words.Count(values.Count, "value")})",
            "dpp-model",
            model.Id,
            cancellationToken);
        return model.Id;
    }

    /// <summary>
    /// Why a new or renamed record can't take this name: another family, another model of the same family, another
    /// site or another operator already has it (ignoring case). Null when the name is free.
    /// </summary>
    public async Task<string?> NameTakenAsync(RecordKind kind, string name, string? familyId = null, string? exceptId = null, CancellationToken cancellationToken = default)
    {
        var wanted = name.Trim();
        bool Same(string id, string other) => id != exceptId && string.Equals(other.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
        return kind switch
        {
            RecordKind.Family when (await store.ListAsync<BatteryFamily>(cancellationToken)).Any(item => Same(item.Id, item.Name)) => $"another battery family is already called {wanted}",
            RecordKind.Model when (await store.ListModelsAsync(familyId ?? string.Empty, cancellationToken)).Any(item => Same(item.Id, item.Name)) => $"this family already has a model called {wanted}",
            RecordKind.Site when (await store.ListAsync<ManufacturingSite>(cancellationToken)).Any(item => Same(item.Id, item.Name)) => $"another manufacturing site is already called {wanted}",
            RecordKind.Operator when (await store.ListAsync<EconomicOperator>(cancellationToken)).Any(item => Same(item.Id, item.Name)) => $"another economic operator is already called {wanted}",
            _ => null,
        };
    }

    /// <summary>
    /// Why a record can't be removed yet: something still uses it. Only unused records are removed, so no battery or
    /// passport ever loses what it was built from. Null when it can be removed.
    /// </summary>
    public async Task<string?> WhyNotRemovableAsync(RecordKind kind, string id, CancellationToken cancellationToken = default)
    {
        switch (kind)
        {
            case RecordKind.Family:
                var models = (await store.ListModelsAsync(id, cancellationToken)).Count;
                return models > 0 ? $"It still has {Words.Count(models, "battery model")}. Remove them first." : null;
            case RecordKind.Model:
                var modelPacks = (await store.FindPackIdsUsingAsync(modelId: id, cancellationToken: cancellationToken)).Count;
                var batches = (await store.ListBatchesAsync(id, cancellationToken)).Count;
                return modelPacks > 0 ? $"{Words.Count(modelPacks, "battery", "batteries")} {(modelPacks == 1 ? "uses" : "use")} it."
                    : batches > 0 ? $"It still has {Words.Count(batches, "batch", "batches")}. Remove them first." : null;
            case RecordKind.Batch:
                var batchPacks = (await store.FindPackIdsUsingAsync(batchId: id, cancellationToken: cancellationToken)).Count;
                return batchPacks > 0 ? $"{Words.Count(batchPacks, "battery", "batteries")} {(batchPacks == 1 ? "belongs" : "belong")} to it." : null;
            case RecordKind.Site:
                var siteBatches = (await store.ListAsync<ProductionBatch>(cancellationToken)).Count(batch => batch.SiteId == id);
                var sitePacks = (await store.FindPackIdsUsingAsync(siteId: id, cancellationToken: cancellationToken)).Count;
                return siteBatches > 0 ? $"{Words.Count(siteBatches, "batch", "batches")} {(siteBatches == 1 ? "was" : "were")} made there."
                    : sitePacks > 0 ? $"{Words.Count(sitePacks, "battery", "batteries")} {(sitePacks == 1 ? "was" : "were")} made there." : null;
            case RecordKind.Operator:
                var operatorPacks = (await store.FindPackIdsUsingAsync(operatorId: id, cancellationToken: cancellationToken)).Count;
                return operatorPacks > 0 ? $"{Words.Count(operatorPacks, "battery", "batteries")} {(operatorPacks == 1 ? "names" : "name")} it as economic operator." : null;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>Removes an unused record with its uploaded files; the reason when it can't be removed.</summary>
    public async Task<(bool Removed, string? Problem, string Name)> RemoveAsync(string who, RecordKind kind, string id, CancellationToken cancellationToken = default)
    {
        var name = kind switch
        {
            RecordKind.Family => (await store.GetAsync<BatteryFamily>(id, cancellationToken))?.Name,
            RecordKind.Model => (await store.GetAsync<BatteryModel>(id, cancellationToken))?.Name,
            RecordKind.Batch => await store.GetAsync<ProductionBatch>(id, cancellationToken) is { } batch ? $"Batch {batch.Year}" : null,
            RecordKind.Site => (await store.GetAsync<ManufacturingSite>(id, cancellationToken))?.Name,
            RecordKind.Operator => (await store.GetAsync<EconomicOperator>(id, cancellationToken))?.Name,
            _ => null,
        };
        if (name is null)
        {
            return (false, "it was not found", string.Empty);
        }

        if (await WhyNotRemovableAsync(kind, id, cancellationToken) is { } blocked)
        {
            return (false, blocked, name);
        }

        switch (kind)
        {
            case RecordKind.Family:
                var family = await store.GetAsync<BatteryFamily>(id, cancellationToken);
                await store.DeleteAsync<BatteryFamily>(id, cancellationToken);
                if (family?.PictureFileId is { } pictureId)
                {
                    await documents.DeleteFileAsync(pictureId, cancellationToken);
                }

                break;
            case RecordKind.Model:
                await store.DeleteAsync<BatteryModel>(id, cancellationToken);
                await documents.DeleteOwnedAsync(TargetKind.Model, id, cancellationToken);
                break;
            case RecordKind.Batch:
                await store.DeleteAsync<ProductionBatch>(id, cancellationToken);
                await documents.DeleteOwnedAsync(TargetKind.Batch, id, cancellationToken);
                break;
            case RecordKind.Site:
                await store.DeleteAsync<ManufacturingSite>(id, cancellationToken);
                break;
            case RecordKind.Operator:
                await store.DeleteAsync<EconomicOperator>(id, cancellationToken);
                break;
        }

        await AuditAsync(RemovedEvent, who, $"{KindName(kind)} {name} removed", $"dpp-{kind.ToString().ToLowerInvariant()}", id, cancellationToken);
        return (true, null, name);
    }

    public static string KindName(RecordKind kind) => kind switch
    {
        RecordKind.Family => "Battery family",
        RecordKind.Model => "Battery model",
        RecordKind.Batch => "Batch",
        RecordKind.Site => "Manufacturing site",
        _ => "Economic operator",
    };

    /// <summary>A new batch of a model: one per calendar year and manufacturing site (decision 3).</summary>
    public async Task<(string? BatchId, string? Problem)> CreateBatchAsync(string who, string modelId, int year, string siteId, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
        {
            return (null, "the battery model was not found");
        }

        if (await store.GetAsync<ManufacturingSite>(siteId, cancellationToken) is null)
        {
            return (null, "choose a manufacturing site; add it under Sites and operators first if it's missing");
        }

        if (year is < 2000 or > 2100)
        {
            return (null, "the year must be a calendar year such as 2026");
        }

        var batch = new ProductionBatch { Id = NewId("batch"), ModelId = modelId, SiteId = siteId, Year = year, Values = OneWayValues(TargetKind.Batch, who) };
        try
        {
            await store.InsertAsync(batch, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return (null, $"this model already has a {year} batch from that site");
        }

        await AuditAsync(CreatedEvent, who, $"Batch {year} of model {modelId} created", "dpp-batch", batch.Id, cancellationToken);
        return (batch.Id, null);
    }

    public async Task<string> CreateSiteAsync(string who, string name, CancellationToken cancellationToken = default)
    {
        var site = new ManufacturingSite { Id = NewId("site"), Name = name.Trim(), Values = OneWayValues(TargetKind.Site, who) };
        await store.InsertAsync(site, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Manufacturing site {site.Name} created", "dpp-site", site.Id, cancellationToken);
        return site.Id;
    }

    public async Task<string> CreateOperatorAsync(string who, string name, CancellationToken cancellationToken = default)
    {
        var item = new EconomicOperator { Id = NewId("operator"), Name = name.Trim(), Values = OneWayValues(TargetKind.Operator, who) };
        await store.InsertAsync(item, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Economic operator {item.Name} created", "dpp-operator", item.Id, cancellationToken);
        return item.Id;
    }

    /// <summary>Renames a family, model, site or operator when the name changed; true when it did.</summary>
    public async Task<bool> RenameAsync<T>(string who, string id, string currentName, string? name, CancellationToken cancellationToken = default)
        where T : class, IDppRecord
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed == currentName || !await store.RenameAsync<T>(id, trimmed, cancellationToken))
        {
            return false;
        }

        var entityType = typeof(T).Name switch
        {
            nameof(BatteryFamily) => "dpp-family",
            nameof(BatteryModel) => "dpp-model",
            nameof(ManufacturingSite) => "dpp-site",
            _ => "dpp-operator",
        };
        await AuditAsync(RenamedEvent, who, $"{currentName} renamed to {trimmed}", entityType, id, cancellationToken);
        return true;
    }

    /// <summary>Stores a PNG or JPEG as the family's picture (decision 20); the problem when it isn't accepted.</summary>
    public async Task<string?> SetFamilyPictureAsync(string who, string familyId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (await store.GetAsync<BatteryFamily>(familyId, cancellationToken) is null)
        {
            return "the battery family was not found";
        }

        var limit = options.Value.MaxUploadBytes;
        var contentType = content.Length == 0 ? null : FileTypes.Detect(content);
        if (contentType is not (FileTypes.Png or FileTypes.Jpeg))
        {
            return "the picture must be a PNG or JPEG file";
        }

        if (limit > 0 && content.LongLength > limit)
        {
            return $"the picture is larger than the {limit}-byte upload limit";
        }

        var fileId = await documents.UploadFileAsync(Path.GetFileName(fileName), content, cancellationToken);
        await store.SetFamilyPictureAsync(familyId, fileId, contentType, cancellationToken);
        await AuditAsync(PictureEvent, who, $"Picture of battery family {familyId} set", "dpp-family", familyId, cancellationToken);
        return null;
    }

    /// <summary>The family's picture; null when the family or its picture doesn't exist.</summary>
    public async Task<(byte[] Content, string ContentType)?> FamilyPictureAsync(string familyId, CancellationToken cancellationToken = default)
    {
        var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
        if (family?.PictureFileId is null || family.PictureContentType is null)
        {
            return null;
        }

        return (await documents.DownloadFileAsync(family.PictureFileId, cancellationToken), family.PictureContentType);
    }

    public static string FamilyHref(string familyId, string? modelId = null) =>
        modelId is null
            ? $"/admin/families/{Uri.EscapeDataString(familyId)}"
            : $"/admin/families/{Uri.EscapeDataString(familyId)}?model={Uri.EscapeDataString(modelId)}";

    public static string SitesHref(string kind, string id) => $"/admin/sites-and-operators?{kind}={Uri.EscapeDataString(id)}";

    /// <summary>A link back to a family page, passed along when the team goes to add a site; anything else is dropped.</summary>
    public static string? SafeReturn(string? href) =>
        href is { Length: > 0 } && href.StartsWith("/admin/families/", StringComparison.Ordinal) && !href.Contains("//", StringComparison.Ordinal) && !href.Contains('\\')
            ? href
            : null;

    private async Task<RecordEditorView> EditorAsync(
        TargetKind kind,
        string id,
        string name,
        IReadOnlyDictionary<string, Values.AttributeValue> stored,
        string saveAction,
        string cancelHref,
        EditorState? state,
        CancellationToken cancellationToken)
    {
        var refusals = state?.Save.Refusals(editor.Forms);
        var sections = await editor.SectionsAsync(kind, id, stored, state?.Post, refusals, cancellationToken);
        var published = await editor.AffectedAsync(kind, id, cancellationToken);
        var recordKind = kind switch
        {
            TargetKind.Model => RecordKind.Model,
            TargetKind.Batch => RecordKind.Batch,
            TargetKind.Site => RecordKind.Site,
            _ => RecordKind.Operator,
        };
        return new RecordEditorView(
            kind,
            id,
            name,
            saveAction,
            cancelHref,
            sections,
            state?.Save.Outcome == EditorOutcome.NeedsConfirmation ? state.Save.Affected : [],
            published.Count,
            editor.Forms.Languages,
            editor.Stamp(kind, stored, kind == TargetKind.Batch ? null : name),
            new RemovalView($"{saveAction}/remove", await WhyNotRemovableAsync(recordKind, id, cancellationToken)));
    }

    private Task AuditAsync(string eventType, string who, string summary, string entityType, string entityId, CancellationToken cancellationToken) =>
        audit.AppendApplicationAuditEventAsync(eventType, who, "Team", "form", summary, entityType: entityType, entityId: entityId, cancellationToken: cancellationToken);

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    // A new record starts with the values the schema allows only one way (see RecordEditor.OneWayValues).
    private Dictionary<string, AttributeValue> OneWayValues(TargetKind kind, string who)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return editor.OneWayValues(kind).ToDictionary(
            entry => entry.Key,
            entry => new AttributeValue { Value = entry.Value.DeepClone(), GeneratedAtUtc = now, Source = "form", Who = who },
            StringComparer.Ordinal);
    }
}
```

- [ ] **Step 6: Apply the services patch**

Patch `2d1-task5-services.patch` (`web/Dpp/Documents/DocumentService.cs`, `web/Dpp/Store/DppStore.cs`, `web/Dpp/Documents/DocumentStore.cs`, `web/Dpp/DppServiceCollectionExtensions.cs`). Save the block as `../2d1-task5-services.patch`, outside the worktree, and run `git apply ../2d1-task5-services.patch` from the worktree root:

```diff
diff --git a/web/Dpp/Documents/DocumentService.cs b/web/Dpp/Documents/DocumentService.cs
--- a/web/Dpp/Documents/DocumentService.cs
+++ b/web/Dpp/Documents/DocumentService.cs
@@ -213,20 +213,12 @@ public sealed class DocumentService(
             return (null, $"the file is larger than the {limit}-byte upload limit");
         }
 
-        ReadOnlySpan<byte> content = upload.Content;
-        var contentType = content.StartsWith("%PDF-"u8) ? "application/pdf"
-            : content.StartsWith(PngSignature) ? "image/png"
-            : content.StartsWith(JpegSignature) ? "image/jpeg"
-            : null;
+        var contentType = FileTypes.Detect(upload.Content);
         return contentType is null
             ? (null, "only PDF, PNG and JPEG files are accepted, recognised by their content")
             : (contentType, null);
     }
 
-    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
-
-    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
-
     private JsonObject DocumentLink(string contentType, string documentId, string title, string? language)
     {
         var link = new JsonObject
diff --git a/web/Dpp/Store/DppStore.cs b/web/Dpp/Store/DppStore.cs
--- a/web/Dpp/Store/DppStore.cs
+++ b/web/Dpp/Store/DppStore.cs
@@ -395,6 +395,14 @@ public sealed class DppStore
         return result.MatchedCount == 1;
     }
 
+    /// <summary>Deletes one record; false when no record has that id. Callers check first that nothing uses it.</summary>
+    public async Task<bool> DeleteAsync<T>(string id, CancellationToken cancellationToken = default)
+        where T : class, IDppRecord
+    {
+        var result = await Collection<T>().DeleteOneAsync(Builders<T>.Filter.Eq("_id", id), cancellationToken);
+        return result.DeletedCount == 1;
+    }
+
     /// <summary>Points a family at its new picture (decision 20); false when no family has that id.</summary>
     public async Task<bool> SetFamilyPictureAsync(string familyId, string fileId, string contentType, CancellationToken cancellationToken = default)
     {
diff --git a/web/Dpp/Documents/DocumentStore.cs b/web/Dpp/Documents/DocumentStore.cs
--- a/web/Dpp/Documents/DocumentStore.cs
+++ b/web/Dpp/Documents/DocumentStore.cs
@@ -106,6 +106,34 @@ public sealed class DocumentStore
             .ToListAsync(cancellationToken);
     }
 
+    /// <summary>Deletes every document of a removed record, with its stored file.</summary>
+    public async Task DeleteOwnedAsync(TargetKind ownerKind, string ownerId, CancellationToken cancellationToken = default)
+    {
+        if (_database is null)
+        {
+            return;
+        }
+
+        foreach (var document in await Collection().Find(d => d.OwnerKind == ownerKind && d.OwnerId == ownerId).ToListAsync(cancellationToken))
+        {
+            await DeleteFileAsync(document.FileId, cancellationToken);
+            await Collection().DeleteOneAsync(d => d.Id == document.Id, cancellationToken);
+        }
+    }
+
+    /// <summary>Deletes a stored file; a file that is already gone is fine.</summary>
+    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
+    {
+        try
+        {
+            await Files().DeleteAsync(ObjectId.Parse(fileId), cancellationToken);
+        }
+        catch (GridFSFileNotFoundException)
+        {
+            // already gone
+        }
+    }
+
     public Task<byte[]> DownloadAsync(DppDocument document, CancellationToken cancellationToken = default)
     {
         ArgumentNullException.ThrowIfNull(document);
diff --git a/web/Dpp/DppServiceCollectionExtensions.cs b/web/Dpp/DppServiceCollectionExtensions.cs
--- a/web/Dpp/DppServiceCollectionExtensions.cs
+++ b/web/Dpp/DppServiceCollectionExtensions.cs
@@ -39,6 +39,7 @@ public static class DppServiceCollectionExtensions
         services.AddSingleton<DocumentService>();
         services.AddSingleton<AttributeForms>();
         services.AddSingleton<RecordEditor>();
+        services.AddSingleton<ProductDataPages>();
         return services;
     }
 }
```

- [ ] **Step 7: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ProductDataPagesTests|FullyQualifiedName~BatteryPassWeb.Tests.Dpp.RecordLifecycleTests"`
Expected: `Passed!  - Failed:     0, Passed:    14`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1815`, none failed.

- [ ] **Step 8: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ProductDataPagesTests.cs BatteryPassWeb.Tests/Dpp/RecordLifecycleTests.cs web/Dpp/Documents/FileTypes.cs web/Dpp/Pages/Words.cs web/Dpp/Pages/ProductDataPages.cs BatteryPassWeb.Tests/Dpp/PageTesting.cs web/Dpp/Documents/DocumentService.cs web/Dpp/Store/DppStore.cs web/Dpp/Documents/DocumentStore.cs web/Dpp/DppServiceCollectionExtensions.cs
git commit -m "feat(dpp): load the team pages and add, rename, copy and remove product records"
```


---

### Task 6: The pages: controller, views, stylesheet and script

Razor views compile with the build, so a mistake in a view fails Step 6's build like one in C#.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs`
- Create: `web/Controllers/ProductDataController.cs`
- Create: `web/Views/ProductData/_Messages.cshtml`
- Create: `web/Views/ProductData/_Completeness.cshtml`
- Create: `web/Views/ProductData/_EditorField.cshtml`
- Create: `web/Views/ProductData/_RecordEditor.cshtml`
- Create: `web/Views/ProductData/Families.cshtml`
- Create: `web/Views/ProductData/Family.cshtml`
- Create: `web/Views/ProductData/Batch.cshtml`
- Create: `web/Views/ProductData/SitesAndOperators.cshtml`
- Create: `web/wwwroot/css/bp-product-data.css`
- Create: `web/wwwroot/js/bp-record-editor.js`
- Test (modify): `BatteryPassWeb.Tests/Dpp/PageTesting.cs`
- Modify: `web/Views/Admin/AuditLog.cshtml`

- [ ] **Step 1: Write the failing tests**

Patch `2d1-task6-tests.patch` (`BatteryPassWeb.Tests/Dpp/PageTesting.cs`). Save the block as `../2d1-task6-tests.patch`, outside the worktree, and run `git apply ../2d1-task6-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/Dpp/PageTesting.cs b/BatteryPassWeb.Tests/Dpp/PageTesting.cs
--- a/BatteryPassWeb.Tests/Dpp/PageTesting.cs
+++ b/BatteryPassWeb.Tests/Dpp/PageTesting.cs
@@ -1,16 +1,22 @@
 using System.Globalization;
+using System.Security.Claims;
 using BatteryPassWeb.Configuration;
+using BatteryPassWeb.Controllers;
 using BatteryPassWeb.Dpp.Documents;
 using BatteryPassWeb.Dpp.Editing;
 using BatteryPassWeb.Dpp.Forms;
 using BatteryPassWeb.Dpp.Pages;
 using BatteryPassWeb.Dpp.Values;
 using BatteryPassWeb.Services;
+using Microsoft.AspNetCore.Http;
+using Microsoft.AspNetCore.Mvc;
+using Microsoft.AspNetCore.Mvc.ViewFeatures;
 using Microsoft.Extensions.Options;
+using Microsoft.Extensions.Primitives;
 
 namespace BatteryPassWeb.Tests.Dpp;
 
-/// <summary>The team-page services around a publication rig, and forms filled in the way the page shows them.</summary>
+/// <summary>The team-page services around a publication rig, and forms posted the way a browser would.</summary>
 internal static class PageTesting
 {
     public static RecordEditor Editor(PublicationRig rig) =>
@@ -56,4 +62,30 @@ internal static class PageTesting
         new ApplicationAuditService(rig.Context),
         Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }),
         rig.Clock);
+
+    /// <summary>The controller as a signed-in team member (role "admin") would reach it, with the given form posted.</summary>
+    public static ProductDataController Controller(PublicationRig rig, IReadOnlyDictionary<string, string>? form = null)
+    {
+        var http = new DefaultHttpContext
+        {
+            User = new ClaimsPrincipal(new ClaimsIdentity(
+                [new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)],
+                "test")),
+        };
+        http.Request.Form = new FormCollection((form ?? new Dictionary<string, string>()).ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
+        return new ProductDataController(Pages(rig), Editor(rig), rig.Store, Documents(rig))
+        {
+            ControllerContext = new ControllerContext { HttpContext = http },
+            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
+        };
+    }
+
+    private sealed class NoTempDataStorage : ITempDataProvider
+    {
+        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
+
+        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
+        {
+        }
+    }
 }
```

`BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs`:

```csharp
using System.Reflection;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

[Collection(MongoCollection.Name)]
public sealed class ProductDataControllerTests(MongoFixture mongo)
{
    [Fact]
    public void Pages_ShouldBeForTheTeamAndEveryPostProtectedAgainstForgery()
    {
        var type = typeof(ProductDataController);
        Assert.Equal("AdminOnly", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.All(actions.Where(action => action.GetCustomAttribute<HttpPostAttribute>() is not null), action =>
            Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        Assert.Equal(["FamilyPicture"], actions.Where(action => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null).Select(action => action.Name));
    }

    [Fact]
    public async Task SavingAChange_ShouldReturnToTheEditorWithAMessage()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var form = PageTesting.ShownForm(TargetKind.Model, (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values);
        form["v.batteryMass"] = "520";
        form["name"] = "EXB-NMC-100";
        var controller = PageTesting.Controller(rig, form);

        var result = Assert.IsType<RedirectResult>(await controller.SaveModel("model-test", confirmed: false, CancellationToken.None));

        Assert.Equal("/admin/families/family-test?model=model-test#editor", result.Url);
        Assert.Equal("1 value saved.", controller.TempData[ProductDataController.EditorStatusKey]);
    }

    [Fact]
    public async Task ARefusedValue_ShouldShowThePageAgainWithWhatWasTyped()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var form = PageTesting.ShownForm(TargetKind.Model, (await rig.Store.GetAsync<BatteryModel>("model-test"))!.Values);
        form["v.initialRoundTripEnergyEfficiency"] = "120";
        var controller = PageTesting.Controller(rig, form);

        var view = Assert.IsType<ViewResult>(await controller.SaveModel("model-test", confirmed: false, CancellationToken.None));

        Assert.Equal("Family", view.ViewName);
        var field = Assert.IsType<FamilyPage>(view.Model).ModelEditor!.Sections.SelectMany(section => section.Fields).Single(f => f.Attribute.Number == 71);
        Assert.NotNull(field.Refusal);
        Assert.Equal("120", field.Text("v.initialRoundTripEnergyEfficiency"));
        Assert.Contains("1 value not saved", (string)controller.ViewData[ProductDataController.EditorErrorKey]!);
    }

    [Fact]
    public async Task AChangeToAPublishedPassport_ShouldAskThenPublish()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var form = PageTesting.ShownForm(TargetKind.Batch, (await rig.Store.GetAsync<ProductionBatch>("batch-test-2026"))!.Values);
        form["v.carbonFootprintPerformanceClass"] = "Class A";

        var asked = Assert.IsType<ViewResult>(await PageTesting.Controller(rig, form).SaveBatch("batch-test-2026", confirmed: false, CancellationToken.None));
        Assert.Single(Assert.IsType<BatchPage>(asked.Model).Editor.Confirm);
        Assert.Equal(1, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);

        var controller = PageTesting.Controller(rig, form);
        Assert.IsType<RedirectResult>(await controller.SaveBatch("batch-test-2026", confirmed: true, CancellationToken.None));

        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
        Assert.Equal("1 value saved. 1 passport got a new version.", controller.TempData[ProductDataController.EditorStatusKey]);
    }

    [Fact]
    public async Task UploadingATestReport_ShouldLinkTheBatchToTheFile()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        using var content = new MemoryStream(DocumentTesting.Pdf("reports"));
        var file = new FormFile(content, 0, content.Length, "file", "test-reports.pdf");

        var result = Assert.IsType<RedirectResult>(await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", file, "Test reports", "en-GB", confirmed: false, CancellationToken.None));

        Assert.Equal("/admin/batches/batch-test-2026#editor", result.Url);
        Assert.Equal("test-reports.pdf uploaded.", controller.TempData[ProductDataController.EditorStatusKey]);
        var document = Assert.Single(await new DocumentStore(rig.Context).ListAsync(TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance"));
        Assert.Equal("Test reports", document.Title);
    }

    [Fact]
    public async Task AnUploadThatPublishesNewVersions_ShouldWaitForTheTick()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var controller = PageTesting.Controller(rig);
        FormFile File(MemoryStream content) => new(content, 0, content.Length, "file", "test-reports.pdf");
        using var first = new MemoryStream(DocumentTesting.Pdf("reports"));
        using var second = new MemoryStream(DocumentTesting.Pdf("reports"));

        await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", File(first), null, null, confirmed: false, CancellationToken.None);

        Assert.Contains("would publish new versions of 1 passport", (string)controller.TempData[ProductDataController.EditorErrorKey]!);
        Assert.Empty(await new DocumentStore(rig.Context).ListAsync(TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance"));

        await controller.UploadBatchDocument("batch-test-2026", "resultsOfTestReportsProvingCompliance", File(second), null, null, confirmed: true, CancellationToken.None);

        Assert.Single(await new DocumentStore(rig.Context).ListAsync(TargetKind.Batch, "batch-test-2026", "resultsOfTestReportsProvingCompliance"));
        Assert.Equal(2, (await rig.Store.GetActiveVersionAsync(rig.PackId))?.Number);
    }

    [Fact]
    public async Task AnUploadWithoutAFile_ShouldSaySo()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);

        await controller.UploadModelDocument("model-test", "dismantlingInformation", null, null, null, confirmed: false, CancellationToken.None);

        Assert.Equal("Choose a file first.", controller.TempData[ProductDataController.EditorErrorKey]);
    }

    [Fact]
    public async Task ADuplicateBatch_ShouldBeRefusedWithAReason()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);

        var result = Assert.IsType<RedirectResult>(await controller.CreateBatch("model-test", 2026, "site-test", CancellationToken.None));

        Assert.Equal("/admin/families/family-test?model=model-test", result.Url);
        Assert.Contains("already has a 2026 batch", (string)controller.TempData[ProductDataController.ErrorKey]!);
    }

    [Fact]
    public async Task ADuplicateModelName_ShouldBeRefused()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var model = (await rig.Store.GetAsync<BatteryModel>("model-test"))!;
        var controller = PageTesting.Controller(rig);

        await controller.CreateModel("family-test", model.Name.ToUpperInvariant(), null, CancellationToken.None);

        Assert.Contains("already has a model called", (string)controller.TempData[ProductDataController.ErrorKey]!);
        Assert.Single(await rig.Store.ListModelsAsync("family-test"));
    }

    [Fact]
    public async Task ThePicture_ShouldBeServedWithItsType()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        await PageTesting.Pages(rig).SetFamilyPictureAsync("team@example.com", "family-test", "family.png", DocumentTesting.Png());
        var controller = PageTesting.Controller(rig);

        var file = Assert.IsType<FileContentResult>(await controller.FamilyPicture("family-test", CancellationToken.None));

        Assert.Equal(FileTypes.Png, file.ContentType);
        Assert.Equal(DocumentTesting.Png(), file.FileContents);
        Assert.IsType<NotFoundResult>(await controller.FamilyPicture("no-such-family", CancellationToken.None));
    }

    [Fact]
    public async Task UnknownRecords_ShouldBeNotFound()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var controller = PageTesting.Controller(rig);

        Assert.IsType<NotFoundResult>(await controller.Family("no-such-family", null, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Batch("no-such-batch", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SaveModel("no-such-model", false, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.SaveSite("no-such-site", false, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ProductDataControllerTests"`
Expected: the build fails because `ProductDataController` doesn't exist yet (CS0246 errors).

- [ ] **Step 3: Write `ProductDataController.cs`**

`web/Controllers/ProductDataController.cs`:

```csharp
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The team's product data pages (design 12, decision 23): battery families with their models and batches, and the
/// manufacturing sites and economic operators. Passport values go through the save pipeline via <see cref="RecordEditor"/>.
/// </summary>
[Authorize(Policy = "AdminOnly")]
[Route("admin")]
public sealed class ProductDataController(ProductDataPages pages, RecordEditor editor, DppStore store, DocumentService documents) : Controller
{
    public const string StatusKey = "StatusMessage";
    public const string ErrorKey = "ErrorMessage";

    /// <summary>Messages about an editor's own save or upload, shown in the editor's header card.</summary>
    public const string EditorStatusKey = "EditorStatusMessage";
    public const string EditorErrorKey = "EditorErrorMessage";

    /// <summary>The editor's anchor: after a save or upload the page opens at the editor, not at its top.</summary>
    public const string EditorAnchor = "#editor";

    /// <summary>Neutral information on the page or the editor ("Nothing changed."): neither a success nor a problem.</summary>
    public const string InfoKey = "InfoMessage";

    public const string EditorInfoKey = "EditorInfoMessage";

    /// <summary>The editor's hidden field with the stamp of what the page showed (RecordEditor.Stamp).</summary>
    public const string StampField = "shown";

    [HttpGet("families")]
    public async Task<IActionResult> Families(CancellationToken cancellationToken) =>
        View(await pages.FamiliesAsync(cancellationToken));

    [HttpPost("families")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFamily([FromForm] string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new battery family a name.";
            return Redirect("/admin/families");
        }

        if (await pages.NameTakenAsync(RecordKind.Family, name, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The battery family wasn't added: {taken}.";
            return Redirect("/admin/families");
        }

        var familyId = await pages.CreateFamilyAsync(Who, name, cancellationToken);
        TempData[StatusKey] = "Battery family added. Add its first model below.";
        return Redirect(ProductDataPages.FamilyHref(familyId));
    }

    [HttpGet("families/{familyId}")]
    public async Task<IActionResult> Family(string familyId, [FromQuery] string? model, CancellationToken cancellationToken)
    {
        var page = await pages.FamilyAsync(familyId, model, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost("families/{familyId}/name")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameFamily(string familyId, [FromForm] string? name, CancellationToken cancellationToken)
    {
        var family = await store.GetAsync<BatteryFamily>(familyId, cancellationToken);
        if (family is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(name) && await pages.NameTakenAsync(RecordKind.Family, name, exceptId: familyId, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The name wasn't changed: {taken}.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        if (await pages.RenameAsync<BatteryFamily>(Who, familyId, family.Name, name, cancellationToken))
        {
            TempData[StatusKey] = "Name saved.";
        }
        else
        {
            TempData[InfoKey] = "Nothing changed.";
        }

        return Redirect(ProductDataPages.FamilyHref(familyId));
    }

    [HttpPost("families/{familyId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFamily(string familyId, CancellationToken cancellationToken)
    {
        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Family, familyId, cancellationToken);
        if (!removed)
        {
            TempData[ErrorKey] = $"The battery family wasn't removed: {problem}";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        TempData[StatusKey] = $"Battery family {name} removed.";
        return Redirect("/admin/families");
    }

    [HttpPost("families/{familyId}/picture")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadFamilyPicture(string familyId, IFormFile? picture, CancellationToken cancellationToken)
    {
        if (picture is null || picture.Length == 0)
        {
            TempData[ErrorKey] = "Choose a PNG or JPEG picture first.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        var problem = await pages.SetFamilyPictureAsync(Who, familyId, picture.FileName, await ReadAsync(picture, cancellationToken), cancellationToken);
        TempData[problem is null ? StatusKey : ErrorKey] = problem is null ? "Picture saved." : $"The picture was not saved: {problem}.";
        return Redirect(ProductDataPages.FamilyHref(familyId));
    }

    /// <summary>The family picture is shown on the public summary (decision 20), so anyone may load it.</summary>
    [AllowAnonymous]
    [HttpGet("~/dpp/families/{familyId}/picture")]
    public async Task<IActionResult> FamilyPicture(string familyId, CancellationToken cancellationToken)
    {
        var picture = await pages.FamilyPictureAsync(familyId, cancellationToken);
        return picture is { } found ? File(found.Content, found.ContentType) : NotFound();
    }

    [HttpPost("families/{familyId}/models")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateModel(string familyId, [FromForm] string? name, [FromForm] string? copyFrom, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new battery model a name.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        if (await pages.NameTakenAsync(RecordKind.Model, name, familyId, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The battery model wasn't added: {taken}.";
            return Redirect(ProductDataPages.FamilyHref(familyId));
        }

        var source = string.IsNullOrEmpty(copyFrom) ? null : await store.GetAsync<BatteryModel>(copyFrom, cancellationToken);
        var modelId = await pages.CreateModelAsync(Who, familyId, name, source?.Id, cancellationToken);
        if (modelId is null)
        {
            return NotFound();
        }

        TempData[EditorStatusKey] = source is null
            ? "Battery model added. Fill in its passport values below."
            : $"Battery model added with the values of {source.Name}. Check them and change what differs; uploaded documents weren't copied.";
        return Redirect(ProductDataPages.FamilyHref(familyId, modelId) + EditorAnchor);
    }

    [HttpPost("models/{modelId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveModel(string modelId, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Model, modelId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The battery model wasn't removed: {problem}";
            return Redirect(ProductDataPages.FamilyHref(model.FamilyId, modelId) + EditorAnchor);
        }

        TempData[StatusKey] = $"Battery model {name} removed.";
        return Redirect(ProductDataPages.FamilyHref(model.FamilyId));
    }

    [HttpPost("models/{modelId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveModel(string modelId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return await SaveAsync<BatteryModel>(
            TargetKind.Model,
            modelId,
            model.Name,
            model.FamilyId,
            model.Values,
            confirmed,
            ProductDataPages.FamilyHref(model.FamilyId, modelId),
            async state => View("Family", await pages.FamilyAsync(model.FamilyId, modelId, state, cancellationToken)),
            cancellationToken);
    }

    [HttpPost("models/{modelId}/batches")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBatch(string modelId, [FromForm] int? year, [FromForm] string? siteId, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        var (batchId, problem) = await pages.CreateBatchAsync(Who, modelId, year ?? 0, siteId ?? string.Empty, cancellationToken);
        if (problem is not null)
        {
            TempData[ErrorKey] = $"The batch was not added: {problem}.";
            return Redirect(ProductDataPages.FamilyHref(model.FamilyId, modelId));
        }

        TempData[StatusKey] = "Batch added.";
        return Redirect($"/admin/batches/{Uri.EscapeDataString(batchId!)}");
    }

    [HttpPost("models/{modelId}/documents/{property}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadModelDocument(string modelId, string property, IFormFile? file, [FromForm] string? title, [FromForm] string? language, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var model = await store.GetAsync<BatteryModel>(modelId, cancellationToken);
        return model is null
            ? NotFound()
            : await UploadAsync(TargetKind.Model, modelId, property, file, title, language, confirmed, ProductDataPages.FamilyHref(model.FamilyId, modelId), cancellationToken);
    }

    [HttpGet("batches/{batchId}")]
    public async Task<IActionResult> Batch(string batchId, CancellationToken cancellationToken)
    {
        var page = await pages.BatchAsync(batchId, cancellationToken: cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpPost("batches/{batchId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBatch(string batchId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var batch = await store.GetAsync<ProductionBatch>(batchId, cancellationToken);
        if (batch is null)
        {
            return NotFound();
        }

        return await SaveAsync<ProductionBatch>(
            TargetKind.Batch,
            batchId,
            null,
            null,
            batch.Values,
            confirmed,
            $"/admin/batches/{Uri.EscapeDataString(batchId)}",
            async state => View("Batch", await pages.BatchAsync(batchId, state, cancellationToken)),
            cancellationToken);
    }

    [HttpPost("batches/{batchId}/documents/{property}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadBatchDocument(string batchId, string property, IFormFile? file, [FromForm] string? title, [FromForm] string? language, [FromForm] bool confirmed, CancellationToken cancellationToken) =>
        await store.GetAsync<ProductionBatch>(batchId, cancellationToken) is null
            ? NotFound()
            : await UploadAsync(TargetKind.Batch, batchId, property, file, title, language, confirmed, $"/admin/batches/{Uri.EscapeDataString(batchId)}", cancellationToken);

    [HttpPost("batches/{batchId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBatch(string batchId, CancellationToken cancellationToken)
    {
        var batch = await store.GetAsync<ProductionBatch>(batchId, cancellationToken);
        var model = batch is null ? null : await store.GetAsync<BatteryModel>(batch.ModelId, cancellationToken);
        if (batch is null || model is null)
        {
            return NotFound();
        }

        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Batch, batchId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The batch wasn't removed: {problem}";
            return Redirect($"/admin/batches/{Uri.EscapeDataString(batchId)}{EditorAnchor}");
        }

        TempData[StatusKey] = $"{name} removed.";
        return Redirect(ProductDataPages.FamilyHref(model.FamilyId, model.Id));
    }

    [HttpGet("sites-and-operators")]
    public async Task<IActionResult> SitesAndOperators([FromQuery] string? site, [FromQuery(Name = "operator")] string? economicOperator, CancellationToken cancellationToken) =>
        View(await pages.SitesAndOperatorsAsync(site, economicOperator, cancellationToken: cancellationToken));

    [HttpPost("sites")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSite([FromForm] string? name, [FromForm] string? returnTo, CancellationToken cancellationToken)
    {
        var back = ProductDataPages.SafeReturn(returnTo);
        var listHref = back is null ? "/admin/sites-and-operators" : $"/admin/sites-and-operators?returnTo={Uri.EscapeDataString(back)}";
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new manufacturing site a name.";
            return Redirect(listHref);
        }

        if (await pages.NameTakenAsync(RecordKind.Site, name, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The manufacturing site wasn't added: {taken}.";
            return Redirect(listHref);
        }

        var siteId = await pages.CreateSiteAsync(Who, name, cancellationToken);
        TempData[EditorStatusKey] = "Manufacturing site added. Fill in its passport values below.";
        return Redirect(ProductDataPages.SitesHref("site", siteId) + (back is null ? string.Empty : $"&returnTo={Uri.EscapeDataString(back)}") + EditorAnchor);
    }

    [HttpPost("sites/{siteId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSite(string siteId, CancellationToken cancellationToken)
    {
        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Site, siteId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The manufacturing site wasn't removed: {problem}";
            return Redirect(ProductDataPages.SitesHref("site", siteId) + EditorAnchor);
        }

        TempData[StatusKey] = $"Manufacturing site {name} removed.";
        return Redirect("/admin/sites-and-operators");
    }

    [HttpPost("sites/{siteId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSite(string siteId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var site = await store.GetAsync<ManufacturingSite>(siteId, cancellationToken);
        if (site is null)
        {
            return NotFound();
        }

        return await SaveAsync<ManufacturingSite>(
            TargetKind.Site,
            siteId,
            site.Name,
            null,
            site.Values,
            confirmed,
            ProductDataPages.SitesHref("site", siteId),
            async state => View("SitesAndOperators", await pages.SitesAndOperatorsAsync(siteId, null, state, cancellationToken)),
            cancellationToken);
    }

    [HttpPost("operators")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOperator([FromForm] string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData[ErrorKey] = "Give the new economic operator a name.";
            return Redirect("/admin/sites-and-operators");
        }

        if (await pages.NameTakenAsync(RecordKind.Operator, name, cancellationToken: cancellationToken) is { } taken)
        {
            TempData[ErrorKey] = $"The economic operator wasn't added: {taken}.";
            return Redirect("/admin/sites-and-operators");
        }

        var operatorId = await pages.CreateOperatorAsync(Who, name, cancellationToken);
        TempData[EditorStatusKey] = "Economic operator added. Fill in its passport values below.";
        return Redirect(ProductDataPages.SitesHref("operator", operatorId) + EditorAnchor);
    }

    [HttpPost("operators/{operatorId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOperator(string operatorId, CancellationToken cancellationToken)
    {
        var (removed, problem, name) = await pages.RemoveAsync(Who, RecordKind.Operator, operatorId, cancellationToken);
        if (!removed)
        {
            TempData[EditorErrorKey] = $"The economic operator wasn't removed: {problem}";
            return Redirect(ProductDataPages.SitesHref("operator", operatorId) + EditorAnchor);
        }

        TempData[StatusKey] = $"Economic operator {name} removed.";
        return Redirect("/admin/sites-and-operators");
    }

    [HttpPost("operators/{operatorId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOperator(string operatorId, [FromForm] bool confirmed, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync<EconomicOperator>(operatorId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return await SaveAsync<EconomicOperator>(
            TargetKind.Operator,
            operatorId,
            item.Name,
            null,
            item.Values,
            confirmed,
            ProductDataPages.SitesHref("operator", operatorId),
            async state => View("SitesAndOperators", await pages.SitesAndOperatorsAsync(null, operatorId, state, cancellationToken)),
            cancellationToken);
    }

    private string Who => AccessControlService.CurrentEmail(User) is { Length: > 0 } email ? email : "admin";

    /// <summary>
    /// Saves an editor post: the record's name (when it has one) and its changed values. Refused values and the
    /// republish question show the page again with what was typed; anything else redirects back to it.
    /// </summary>
    private async Task<IActionResult> SaveAsync<T>(
        TargetKind kind,
        string id,
        string? currentName,
        string? familyId,
        IReadOnlyDictionary<string, AttributeValue> stored,
        bool confirmed,
        string pageHref,
        Func<EditorState, Task<IActionResult>> showAgain,
        CancellationToken cancellationToken)
        where T : class, IDppRecord
    {
        var form = Request.Form;
        var stamp = form[StampField].ToString();
        var post = editor.Read(kind, stored, name => form.TryGetValue(name, out var value) ? value.ToString() : null, stamp);
        var postedName = form["name"].ToString();
        string? nameTaken = null;
        var renamed = false;
        if (currentName is not null && !string.IsNullOrWhiteSpace(postedName) && RecordEditor.NameEdited(stamp, postedName))
        {
            var nameKind = kind switch { TargetKind.Model => RecordKind.Model, TargetKind.Site => RecordKind.Site, _ => RecordKind.Operator };
            nameTaken = await pages.NameTakenAsync(nameKind, postedName, familyId, id, cancellationToken);
            renamed = nameTaken is null && await pages.RenameAsync<T>(Who, id, currentName, postedName, cancellationToken);
        }

        var save = await editor.SaveAsync(ChangeSender.Team(Who), kind, id, post, confirmed, cancellationToken);
        var nameNote = nameTaken is null ? string.Empty : $" The name wasn't changed: {nameTaken}.";
        var notes = (renamed ? " Name saved." : string.Empty)
            + (save.Affected.Count > 0 && save.Outcome is EditorOutcome.Saved or EditorOutcome.Refused
                ? $" {Words.Count(save.Affected.Count, "passport")} got a new version."
                : string.Empty);
        var done = (save.SavedCount, save.RemovedCount) switch
        {
            (> 0, > 0) => $"{Words.Count(save.SavedCount, "value")} saved and {save.RemovedCount} removed.",
            (> 0, _) => $"{Words.Count(save.SavedCount, "value")} saved.",
            (_, > 0) => $"{Words.Count(save.RemovedCount, "value")} removed.",
            _ => string.Empty,
        };
        switch (save.Outcome)
        {
            case EditorOutcome.NotFound:
                return NotFound();
            case EditorOutcome.NeedsConfirmation:
                return await showAgain(new EditorState(post, save));
            case EditorOutcome.Refused:
                ViewData[EditorErrorKey] = $"{done} {Words.Count(save.RefusedCount, "value")} not saved; {(save.RefusedCount == 1 ? "it's" : "they're")} marked below.{notes}{nameNote}".TrimStart();
                return await showAgain(new EditorState(post, save));
            case EditorOutcome.NothingChanged:
                if (nameTaken is not null)
                {
                    TempData[EditorErrorKey] = nameNote.Trim();
                }
                else
                {
                    TempData[renamed ? EditorStatusKey : EditorInfoKey] = renamed ? "Name saved." : "Nothing changed.";
                }

                return Redirect(pageHref + EditorAnchor);
            default:
                TempData[EditorStatusKey] = $"{done}{notes}";
                if (nameTaken is not null)
                {
                    TempData[EditorErrorKey] = nameNote.Trim();
                }

                return Redirect(pageHref + EditorAnchor);
        }
    }

    private async Task<IActionResult> UploadAsync(TargetKind kind, string id, string property, IFormFile? file, string? title, string? language, bool confirmed, string pageHref, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData[EditorErrorKey] = "Choose a file first.";
            return Redirect(pageHref + EditorAnchor);
        }

        // Like a typed change, an upload that publishes new versions of published passports is only made once confirmed (design 4.2).
        var published = await editor.AffectedAsync(kind, id, cancellationToken);
        if (published.Count > 0 && !confirmed)
        {
            TempData[EditorErrorKey] = $"The file was not uploaded: it would publish new versions of {Words.Count(published.Count, "passport")}. Tick \"Publish new versions\" next to the file, choose it again and upload.";
            return Redirect(pageHref + EditorAnchor);
        }

        var result = await documents.UploadAsync(ChangeSender.Team(Who), kind, id, property, new DocumentUpload(file.FileName, await ReadAsync(file, cancellationToken), title, language), cancellationToken);
        TempData[result.Problem is null ? EditorStatusKey : EditorErrorKey] = result.Problem is null
            ? $"{result.Document!.FileName} uploaded."
            : $"The file was not uploaded: {result.Problem}.";
        return Redirect(pageHref + EditorAnchor);
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
```

- [ ] **Step 4: Write `_Messages.cshtml`**

`web/Views/ProductData/_Messages.cshtml`:

```cshtml
@using BatteryPassWeb.Controllers
@{
    var status = TempData[ProductDataController.StatusKey] as string ?? ViewData[ProductDataController.StatusKey] as string;
    var error = TempData[ProductDataController.ErrorKey] as string ?? ViewData[ProductDataController.ErrorKey] as string;
    var info = TempData[ProductDataController.InfoKey] as string;
}
@if (!string.IsNullOrWhiteSpace(info))
{
    <p class="alert alert-secondary bp-status-line is-info mt-3" role="status"><span>@info</span></p>
}
@if (!string.IsNullOrWhiteSpace(status))
{
    <p class="alert alert-success bp-status-line is-done mt-3" role="status">
        <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 8.5l3 3 6-7" /></svg>
        <span>@status</span>
    </p>
}
@if (!string.IsNullOrWhiteSpace(error))
{
    <p class="alert alert-danger bp-status-line is-problem mt-3" role="alert">
        <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 4.5v4.5M8 11.5v.5" /></svg>
        <span>@error</span>
    </p>
}
```

- [ ] **Step 5: Write `_Completeness.cshtml`**

`web/Views/ProductData/_Completeness.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model Completeness
<span class="bp-completeness">
    <progress class="bp-progress is-small" value="@Model.Filled" max="@Model.Total" aria-hidden="true"></progress>
    <span>@Model.Filled of @Model.Total filled</span>
    @if (Model.Missing > 0)
    {
        <span class="bp-missing-note">@Model.Missing missing for Feb 2027</span>
    }
</span>
```

- [ ] **Step 6: Write `_EditorField.cshtml`**

`web/Views/ProductData/_EditorField.cshtml`:

```cshtml
@using System.Globalization
@using BatteryPassWeb.Configuration
@using BatteryPassWeb.Dpp.Catalog
@using BatteryPassWeb.Dpp.Forms
@using BatteryPassWeb.Dpp.Pages
@using Microsoft.Extensions.Options
@inject IOptions<BatteryPassOptions> Options
@model EditorFieldView
@{
    var field = Model.Field;
    var form = field.Form;
    var definition = field.Attribute;
    var refusalId = $"refusal-{definition.Property}";

    // A unit in brackets, unless the name already says it ("Expected lifetime in calendar years").
    string WithUnit(string label, string? unit) =>
        unit is null || label.Contains(unit, StringComparison.OrdinalIgnoreCase) ? label : $"{label} ({unit})";

    var required = definition.Requirement != Requirement.Conditional;
    var hint = form.Kind switch
    {
        FormKind.Link => "A link to the document on your website.",
        FormKind.Rows => "One row per substance. Phrases go one per line.",
        FormKind.Fixed => "Set automatically: the only value allowed.",
        FormKind.Group when required && form.Inputs.All(input => !input.Required) => "Fill in at least one.",
        _ => form.Inputs.Count == 1 ? form.Inputs[0].Hint : null,
    };

    // Friendly names for the file types a public document link can declare.
    var optionNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["application/pdf"] = "PDF document",
        ["text/html"] = "Web page",
        ["image/png"] = "PNG image",
        ["image/jpeg"] = "JPEG image",
    };

    async Task Input(FormInput input, string name, string text, string? formId = null, bool invalid = false)
    {
        var ariaInvalid = invalid ? "true" : null;
        var describedBy = invalid ? refusalId : null;
        switch (input.Kind)
        {
            case InputKind.Select:
                <select name="@name" form="@formId" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy">
                    <option value=""></option>
                    @foreach (var option in input.Options)
                    {
                        <option value="@option" selected="@(option == text)">@(optionNames.GetValueOrDefault(option, option))</option>
                    }
                    @if (text.Length > 0 && !input.Options.Contains(text))
                    {
                        <option value="@text" selected>@text</option>
                    }
                </select>
                break;
            case InputKind.Lines:
                <textarea name="@name" rows="4" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy">@text</textarea>
                break;
            case InputKind.LongText:
                <textarea name="@name" rows="3" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy">@text</textarea>
                break;
            case InputKind.Month:
                <input type="month" name="@name" value="@text" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy" />
                break;
            case InputKind.Url:
                <input type="url" name="@name" value="@text" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy" />
                break;
            case InputKind.Number:
                <input type="text" inputmode="decimal" name="@name" value="@text" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy" />
                break;
            case InputKind.Integer:
                <input type="text" inputmode="numeric" name="@name" value="@text" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy" />
                break;
            default:
                <input type="text" name="@name" value="@text" aria-label="@input.Label" aria-invalid="@ariaInvalid" aria-describedby="@describedBy" />
                break;
        }
    }

    async Task Parts(IEnumerable<FormInput> inputs, int? row = null)
    {
        <div class="bp-attr-parts">
            @foreach (var input in inputs)
            {
                var wide = input.Kind is InputKind.Url or InputKind.LongText;
                var refused = row is null && field.IsRefused(input);
                <label class="@($"{(wide ? "bp-attr-wide" : string.Empty)} {(refused ? "is-invalid-part" : string.Empty)}".Trim())">
                    <span>@WithUnit(input.Label, input.Unit)@if (!input.Required) {<span class="bp-attr-optional"> (optional)</span>}</span>
                    @{ await Input(input, form.FieldName(input, row), field.Text(form.FieldName(input, row)), invalid: refused || (row is not null && field.Refusal is not null)); }
                </label>
            }
        </div>
    }

    // A row's title: its first filled-in text, so a list of substances reads as one.
    string RowTitle(int row) =>
        form.Inputs.Select(input => field.Text(form.FieldName(input, row)).Trim()).FirstOrDefault(text => text.Length > 0) is { } first
            ? $"Row {row + 1} · {(first.Length > 60 ? first[..60] + "…" : first)}"
            : $"Row {row + 1}";

    static string Size(long bytes) => bytes < 1024
        ? $"{bytes} bytes"
        : bytes < 1048576 ? $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB" : $"{(bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB";
}

<div class="bp-attr-row@(field.Refusal is null ? string.Empty : " is-invalid")@(field.IsMissing ? " is-missing" : string.Empty)">
    <div class="bp-attr-label">
        <strong><span class="bp-attr-number">#@definition.Number</span> @WithUnit(definition.Name, form.Kind == FormKind.Single ? form.Inputs[0].Unit : null)</strong>
        @if (field.Marker != "Required by Feb 2027")
        {
            <em class="bp-field-marker">@field.Marker</em>
        }
        @if (field.IsMissing)
        {
            <span class="bp-attr-missing">Missing</span>
        }
        @if (field.AudienceLabel is not null)
        {
            <details class="bp-attr-audience">
                <summary><svg viewBox="0 0 16 16" aria-hidden="true"><rect x="3.5" y="7" width="9" height="6.5" rx="1.5" /><path d="M5.5 7V5.5a2.5 2.5 0 0 1 5 0V7" /></svg>Not public</summary>
                <span>@(form.Kind == FormKind.Upload ? "Downloaded" : "Seen") only by @field.SeenBy.</span>
            </details>
        }
        @if (hint is not null)
        {
            <small class="bp-field-help">@hint</small>
        }
    </div>

    <div class="bp-attr-inputs">
        @switch (form.Kind)
        {
            case FormKind.Single:
            case FormKind.Lines:
                await Input(form.Inputs[0], form.FieldName(form.Inputs[0]), field.Text(form.FieldName(form.Inputs[0])), invalid: field.Refusal is not null);
                break;

            case FormKind.Fixed:
                <div class="bp-attr-fixed">@string.Join(", ", form.Constants.Values)</div>
                break;

            case FormKind.Group:
            case FormKind.Link:
                await Parts(form.Inputs);
                break;

            case FormKind.Rows:
                <input type="hidden" name="@form.RowCountField" value="@field.Rows" />
                @for (var row = 0; row < field.Rows; row++)
                {
                    var blank = row == field.Rows - 1;
                    <div class="bp-attr-repeat@(blank ? " is-new-row" : string.Empty)">
                        <div class="bp-attr-repeat-head">
                            <span class="bp-attr-row-title">@(blank ? "New row" : RowTitle(row))</span>
                            @if (!blank)
                            {
                                <button type="button" class="bp-secondary-button bp-button-compact" data-remove-row>Remove row</button>
                            }
                        </div>
                        @{ await Parts(form.Inputs, row); }
                    </div>
                }
                <button type="button" class="bp-secondary-button bp-button-compact bp-add-row" data-add-row hidden>+ Add a row</button>
                break;

            case FormKind.Upload:
                var formId = $"upload-{definition.Property}";
                var document = field.Document;
                var limit = Options.Value.MaxUploadBytes;
                var current = document is not null
                    ? $"{document.FileName} · {Size(document.Length)} · uploaded {document.UploadedAtUtc:d MMM yyyy} by {document.UploadedBy}"
                    : field.StoredLink is { } link ? $"Linked: {link}" : "No file uploaded yet";
                <input type="text" readonly value="@current" aria-label="Current file" data-upload-name />
                @if (document is not null)
                {
                    <small class="bp-field-help">SHA-256 <code>@document.Sha256</code></small>
                }
                <fieldset class="bp-attr-upload">
                    <legend>@(document is null ? "Upload a file" : "Upload a new file")</legend>
                    <div class="bp-attr-file">
                        <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap mb-0">
                            Choose file
                            <input type="file" name="file" class="bp-document-hidden-file" accept="application/pdf,image/png,image/jpeg" form="@formId" data-upload-file />
                        </label>
                        <span class="bp-help-text" data-upload-chosen>No file chosen</span>
                        <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap" form="@formId" data-upload-button>Upload</button>
                    </div>
                    <div class="bp-attr-parts">
                        <label>
                            <span>Title (optional)</span>
                            <input type="text" name="title" form="@formId" value="@document?.Title" />
                        </label>
                        <label>
                            <span>Language (optional)</span>
                            @{ await Input(new FormInput("language", "Language", InputKind.Select, false, Model.Editor.Languages), "language", document?.Language ?? string.Empty, formId); }
                        </label>
                    </div>
                    <small class="bp-field-help">
                        PDF, PNG or JPEG@(limit > 0 ? $", up to {(limit / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture)} MB" : string.Empty), checked by its content. Uploaded on its own, not with Save.
                    </small>
                    @if (Model.Editor.PublishedPassports > 0)
                    {
                        <label class="bp-attr-confirm">
                            <input type="checkbox" name="confirmed" value="true" form="@formId" required />
                            <span>Publish new versions of the @Words.Count(Model.Editor.PublishedPassports, "passport") that use this file</span>
                        </label>
                    }
                </fieldset>
                break;
        }

        @if (field.Refusal is not null)
        {
            <small class="bp-field-help text-danger" id="@refusalId">@field.Refusal</small>
        }
    </div>
</div>
```

- [ ] **Step 7: Write `_RecordEditor.cshtml`**

`web/Views/ProductData/_RecordEditor.cshtml`:

```cshtml
@using BatteryPassWeb.Controllers
@using BatteryPassWeb.Dpp.Editing
@using BatteryPassWeb.Dpp.Forms
@using BatteryPassWeb.Dpp.Pages
@model RecordEditorView
@{
    // A small record (a site or an operator) shows its few fields in one column, without a side menu.
    var withMenu = Model.Total > 5;
    var fields = Model.Sections.SelectMany(section => section.Fields).ToArray();
    var later = fields.Any(field => field.Marker == "Required later");
    var conditional = fields.Any(field => field.Marker == "Conditional");
    var kindWord = Model.KindLabel.ToLowerInvariant();
    var editorStatus = (TempData[ProductDataController.EditorStatusKey] ?? ViewData[ProductDataController.EditorStatusKey]) as string;
    var editorError = (TempData[ProductDataController.EditorErrorKey] ?? ViewData[ProductDataController.EditorErrorKey]) as string;
    var editorInfo = TempData[ProductDataController.EditorInfoKey] as string;

    // A batch's page is already titled with its year and site; its editor is simply its passport values.
    var eyebrow = Model.Kind == TargetKind.Batch ? Model.KindLabel : $"{Model.KindLabel} · passport values";
    var title = Model.Kind == TargetKind.Batch ? "Passport values" : Model.Name;
}

<section class="bp-card bp-console-form-card bp-editor-card mt-4" id="editor">
    <div class="bp-editor-top">
        <div class="bp-editor-heading">
            <span class="bp-summary-title">@eyebrow</span>
            <h2 class="bp-editor-title">@title</h2>
            <p class="bp-editor-summary">
                @if (Model.Missing > 0)
                {
                    <span class="bp-missing-dot" aria-hidden="true"></span>
                    <span><strong>@Words.Count(Model.Missing, "value")</strong> the passport needs by Feb 2027 @Words.Is(Model.Missing) missing.</span>
                }
                else
                {
                    <span class="bp-done-mark" aria-hidden="true">✓</span>
                    <span>Every value the passport needs by Feb 2027 is filled in.</span>
                }
                <span>@(Model.PublishedPassports > 0
                    ? $"Used by {Words.Count(Model.PublishedPassports, "published passport")}: saving a change publishes new versions, and you're asked first."
                    : "No published passport uses it yet.")</span>
            </p>
        </div>
        <div class="bp-editor-progress" data-completeness>
            <progress class="bp-progress" value="@Model.Filled" max="@Model.Total" aria-label="Values filled in">@Model.Filled of @Model.Total</progress>
            <span>@Model.Filled of @Model.Total filled</span>
        </div>
    </div>

    @if (editorStatus is not null)
    {
        <p class="alert alert-success bp-status-line bp-editor-status is-done" role="status">
            <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 8.5l3 3 6-7" /></svg>
            <span>@editorStatus</span>
        </p>
    }
    @if (editorInfo is not null)
    {
        <p class="alert alert-secondary bp-status-line bp-editor-status is-info" role="status"><span>@editorInfo</span></p>
    }
    @if (editorError is not null)
    {
        <p class="alert alert-danger bp-status-line bp-editor-status is-problem" role="alert">
            <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 4.5v4.5M8 11.5v.5" /></svg>
            <span>@editorError</span>
        </p>
    }

    <div class="bp-editor-tools">
        @if (Model.Kind != TargetKind.Batch)
        {
            <label class="bp-editor-name">
                <span>Name <em class="bp-field-marker">Not passport data</em></span>
                <input type="text" name="name" value="@Model.Name" form="record-editor" required />
            </label>
        }
        <div class="bp-editor-legend">
            <span>Values without a marker are required by Feb 2027.</span>
            @if (later)
            {
                <span><em class="bp-field-marker">Required later</em> only after Feb 2027.</span>
            }
            @if (conditional)
            {
                <span><em class="bp-field-marker">Conditional</em> only if it applies to this battery.</span>
            }
            <span>Empty a field to remove its value.</span>
        </div>
        @if (Model.Missing > 0)
        {
            <label class="bp-editor-switch">
                <input type="checkbox" role="switch" data-missing-only />
                <span>Show only missing values</span>
            </label>
        }
    </div>

    <form method="post" action="@Model.SaveAction" class="@(withMenu ? "bp-edit-grid" : "d-grid") bp-editor-body" id="record-editor" data-record-editor data-cancel-href="@Model.CancelHref">
        @Html.AntiForgeryToken()
        <input type="hidden" name="@ProductDataController.StampField" value="@Model.Stamp" />
        @if (withMenu)
        {
            <nav class="bp-edit-nav" aria-label="@Model.KindLabel sections">
                @foreach (var group in Model.Sections)
                {
                    <button type="button" class="bp-edit-nav-button@(group.Missing > 0 ? " has-missing-values" : string.Empty)" data-section-target="@group.Id" aria-pressed="false"
                            title="@(group.Missing > 0 ? $"{group.Title}: {Words.Count(group.Missing, "value")} needed by Feb 2027 {Words.Is(group.Missing)} missing" : group.Title)">
                        <span class="bp-edit-nav-label">@group.Title</span>
                        <span class="bp-edit-nav-count">@(group.Missing > 0 ? $"{group.Missing} missing" : $"{group.Filled} of {group.Fields.Count} filled")</span>
                    </button>
                }
            </nav>
        }

        <div class="bp-edit-form">
            @foreach (var group in Model.Sections)
            {
                <section id="@group.Id" class="bp-card bp-edit-section bp-console-form-card@(group.Missing == 0 ? " has-no-missing" : string.Empty)" data-section-title="@group.Title">
                    <h3 class="bp-edit-section-title">@group.Title</h3>
                    <p class="bp-template-sync-note">@group.Note</p>
                    <div class="bp-attr-list">
                        @foreach (var field in group.Fields)
                        {
                            @await Html.PartialAsync("_EditorField", new EditorFieldView(Model, field))
                        }
                    </div>
                </section>
            }

            <div class="bp-product-editor-savebar">
                <span class="bp-savebar-text" data-save-status>Only the values you change are saved.</span>
                <div class="bp-savebar-actions">
                    <button type="button" class="bp-secondary-button" data-cancel-edits data-cancel-href="@Model.CancelHref">Cancel edits</button>
                    <button type="submit" class="bp-primary-button">Save @kindWord</button>
                </div>
            </div>
        </div>

        @if (Model.Confirm.Count > 0)
        {
            <div class="bp-modal" data-confirm-modal>
                <div class="bp-modal-backdrop"></div>
                <div class="bp-modal-panel" role="dialog" aria-modal="true" aria-labelledby="confirm-publish-title">
                    <h2 id="confirm-publish-title" class="h5 mb-2">Save and publish new versions?</h2>
                    <p class="mb-2">
                        This change is part of <strong>@Words.Count(Model.Confirm.Count, "published passport")</strong>. Each one gets a new
                        signed version; the current version is archived and stays readable.
                    </p>
                    <ul class="mb-3">
                        @foreach (var passport in Model.Confirm.Take(10))
                        {
                            <li>@passport.SerialNumber · @(passport.ClusterId ?? "no cluster") · version @passport.CurrentVersion → @(passport.CurrentVersion + 1)</li>
                        }
                        @if (Model.Confirm.Count > 10)
                        {
                            <li class="text-secondary">and @(Model.Confirm.Count - 10) more</li>
                        }
                    </ul>
                    <div class="d-flex justify-content-end flex-wrap gap-2">
                        <button type="button" class="bp-secondary-button" data-confirm-stay>Keep editing</button>
                        <button type="submit" class="bp-primary-button" name="confirmed" value="true">Save and publish @Words.Count(Model.Confirm.Count, "version")</button>
                    </div>
                </div>
            </div>
        }
    </form>
</section>

@foreach (var field in fields.Where(field => field.Form.Kind == FormKind.Upload))
{
    <form id="upload-@field.Attribute.Property" method="post" enctype="multipart/form-data" action="@Model.UploadAction(field.Attribute.Property)" data-upload-form>
        @Html.AntiForgeryToken()
    </form>
}

<details class="bp-card bp-remove-record mt-4">
    <summary>Remove this @kindWord</summary>
    @if (Model.Removal.Blocked is { } blocked)
    {
        <p class="mb-0 mt-2">It can't be removed: @blocked</p>
    }
    else
    {
        <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
            @Html.AntiForgeryToken()
            <span>Nothing uses it yet. Removing it deletes its values@(Model.Kind is TargetKind.Model or TargetKind.Batch ? " and uploaded documents" : string.Empty); this can't be undone.</span>
            <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove @kindWord</button>
        </form>
    }
</details>

<div class="bp-modal" data-upload-guard-modal hidden>
    <div class="bp-modal-backdrop" data-upload-dismiss></div>
    <div class="bp-modal-panel" role="dialog" aria-modal="true" aria-labelledby="upload-guard-title" aria-describedby="upload-guard-description">
        <h2 id="upload-guard-title" class="h5 mb-2">Save your edits before uploading?</h2>
        <p id="upload-guard-description" class="mb-3">
            Uploading a file reloads the page, which discards the values you've typed but not saved. Save them first,
            then choose the file again, or upload now and discard them.
        </p>
        <div class="d-flex justify-content-end flex-wrap gap-2">
            <button type="button" class="bp-secondary-button" data-upload-stay>Keep editing</button>
            <button type="button" class="bp-danger-button" data-upload-discard>Discard edits and upload</button>
        </div>
    </div>
</div>

<div class="bp-modal" data-cancel-modal hidden>
    <div class="bp-modal-backdrop" data-cancel-dismiss></div>
    <div class="bp-modal-panel" role="dialog" aria-modal="true" aria-labelledby="cancel-edits-title" aria-describedby="cancel-edits-description">
        <h2 id="cancel-edits-title" class="h5 mb-2">Discard unsaved edits?</h2>
        <p id="cancel-edits-description" class="mb-3">You have unsaved changes. Leaving this page will discard them.</p>
        <div class="d-flex justify-content-end flex-wrap gap-2">
            <button type="button" class="bp-secondary-button" data-cancel-stay>Keep editing</button>
            <button type="button" class="bp-danger-button" data-cancel-discard>Discard changes</button>
        </div>
    </div>
</div>
```

- [ ] **Step 8: Write `Families.cshtml`**

`web/Views/ProductData/Families.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model FamiliesPage
@{
    ViewData["Title"] = "Battery families";
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <div>
        <h1 class="mb-1">Administration</h1>
        <p class="text-secondary mb-0">Manage battery families, sites and operators, batteries, cluster assignments, registered clusters, user memberships, credentials, and audit history.</p>
    </div>

    @await Html.PartialAsync("_AdminTabs", "products")
    @await Html.PartialAsync("_Messages")

    <section class="bp-card bp-product-template-hero bp-console-form-card mt-4">
        <div>
            <span class="bp-summary-title">Battery families</span>
            <h2>Battery families, models and batches</h2>
            <p class="mb-0">
                A family groups battery models. Each model holds its passport values; each batch (one per calendar year
                and manufacturing site) holds the carbon footprint, recycled content and conformity documents.
            </p>
        </div>
        <form method="post" action="/admin/families" class="bp-add-family mt-3">
            @Html.AntiForgeryToken()
            <label class="bp-product-version-selected-field">
                <span>New battery family</span>
                <input type="text" name="name" placeholder="Family name, e.g. Compact 7M" required />
            </label>
            <button type="submit" class="bp-primary-button text-nowrap">Add battery family</button>
        </form>
    </section>

    @if (Model.Families.Count == 0)
    {
        <div class="bp-card mt-4">No battery families yet. Add the first one above, then its models, their batches and the family picture.</div>
    }
    else
    {
        <section class="bp-product-template-grid mt-4">
            @foreach (var row in Model.Families)
            {
                <article class="bp-card bp-product-template-card">
                    <div class="bp-product-template-card-media">
                        @if (row.Family.PictureFileId is not null)
                        {
                            <img src="/dpp/families/@Uri.EscapeDataString(row.Family.Id)/picture" alt="@row.Family.Name battery family picture" />
                        }
                        else
                        {
                            <span class="bp-help-text text-center">No picture yet</span>
                        }
                    </div>
                    <div class="bp-product-template-card-body">
                        <h3>@row.Family.Name</h3>
                        <dl class="bp-product-template-meta">
                            <div><dt>Battery models</dt><dd>@row.Models</dd></div>
                            <div><dt>Batches</dt><dd>@row.Batches</dd></div>
                            <div><dt>Batteries</dt><dd>@row.Packs</dd></div>
                            <div><dt>Published</dt><dd>@row.Published</dd></div>
                        </dl>
                        @if (row.Missing > 0)
                        {
                            <span class="bp-missing-note">@row.Missing missing for Feb 2027</span>
                        }
                        else if (row.Models > 0)
                        {
                            <span class="bp-done-note">Complete for Feb 2027</span>
                        }
                        <a href="@ProductDataPages.FamilyHref(row.Family.Id)" class="bp-secondary-button">Edit family</a>
                    </div>
                </article>
            }
        </section>
    }
</main>
```

- [ ] **Step 9: Write `Family.cshtml`**

`web/Views/ProductData/Family.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model FamilyPage
@{
    var selected = Model.SelectedModel;
    ViewData["Title"] = selected is null ? Model.Family.Name : $"{selected.Name} · {Model.Family.Name}";
    var familyId = Uri.EscapeDataString(Model.Family.Id);
    var hasPicture = Model.Family.PictureFileId is not null;
    var here = ProductDataPages.FamilyHref(Model.Family.Id, selected?.Id);
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <nav class="bp-breadcrumb" aria-label="Breadcrumb">
        <a href="/admin/families">Battery families</a>
        @if (selected is null)
        {
            <span aria-current="page">@Model.Family.Name</span>
        }
        else
        {
            <a href="@ProductDataPages.FamilyHref(Model.Family.Id)">@Model.Family.Name</a>
            <span aria-current="page">@selected.Name</span>
        }
    </nav>

    <section class="bp-card bp-console-form-card bp-product-editor-hero">
        <div class="d-grid gap-3">
            <div>
                <span class="bp-summary-title">Battery family</span>
                <h1>@Model.Family.Name</h1>
                <p class="mb-0">
                    The family's battery models, each with its passport values and its batches. Batteries take their model
                    and batch values from here.
                </p>
            </div>
            <div class="bp-family-identity">
                <form method="post" action="/admin/families/@familyId/name" class="bp-family-identity-field">
                    @Html.AntiForgeryToken()
                    <label for="family-name">Family name <em class="bp-field-marker">Not passport data</em></label>
                    <div class="d-flex gap-2">
                        <input id="family-name" type="text" name="name" value="@Model.Family.Name" required />
                        <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap">Rename</button>
                    </div>
                </form>
                <form method="post" action="/admin/families/@familyId/picture" enctype="multipart/form-data" class="bp-family-identity-field" data-upload-row>
                    @Html.AntiForgeryToken()
                    <span>Picture, shown on the public summary (PNG or JPEG)</span>
                    <div class="d-flex gap-2">
                        <input type="text" readonly value="@(hasPicture ? "A picture is set" : "No picture yet")" aria-label="Current picture" data-upload-name />
                        <label class="bp-secondary-button bp-button-compact bp-document-smart-upload position-relative text-nowrap flex-shrink-0 mb-0">
                            Choose picture
                            <input type="file" name="picture" class="bp-document-hidden-file" accept="image/png,image/jpeg" data-upload-file />
                        </label>
                        <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap" data-upload-button>Save picture</button>
                    </div>
                </form>
            </div>
        </div>
        <div class="bp-product-editor-preview">
            @if (hasPicture)
            {
                <img src="/dpp/families/@familyId/picture" alt="@Model.Family.Name battery family picture" />
            }
            else
            {
                <span class="bp-help-text text-center">No picture yet</span>
            }
        </div>
    </section>

    @await Html.PartialAsync("_Messages")

    <section class="bp-card bp-product-version-console bp-console-form-card mt-4">
        <div class="bp-product-version-console-header">
            <div>
                <h2>Battery models</h2>
                <p class="bp-help-text mb-0">Choose a model to edit its passport values and its batches.</p>
            </div>
        </div>

        @if (Model.Models.Count > 0)
        {
            <div class="bp-product-version-tab-rail bp-product-version-chip-rail">
                @foreach (var chip in Model.Models)
                {
                    var isSelected = chip.Model.Id == selected?.Id;
                    <article class="bp-product-version-tab bp-product-version-chip @(isSelected ? "is-active" : string.Empty)">
                        <a class="bp-product-version-tab-main" aria-current="@(isSelected ? "page" : null)" href="@(ProductDataPages.FamilyHref(Model.Family.Id, chip.Model.Id))#editor">
                            <strong>@chip.Model.Name</strong>
                        </a>
                        <small class="bp-chip-meta"
                               title="@(chip.Missing > 0 ? $"{Words.Count(chip.Missing, "value")} needed by Feb 2027 {Words.Is(chip.Missing)} missing" : "Every value needed by Feb 2027 is filled in")">
                            <span class="@(chip.Missing > 0 ? "bp-missing-note" : "bp-done-note")">@(chip.Missing > 0 ? $"{chip.Missing} missing" : "Complete")</span>
                            <span>@chip.Filled of @chip.Total filled</span>
                        </small>
                    </article>
                }
            </div>
        }

        <form method="post" action="/admin/families/@familyId/models" class="bp-product-version-selected-panel bp-add-model">
            @Html.AntiForgeryToken()
            <label class="bp-product-version-selected-field">
                <span>@(Model.Models.Count == 0 ? "First battery model" : "New battery model")</span>
                <input type="text" name="name" placeholder="Model name, e.g. EXB-NMC-120" required />
            </label>
            @if (Model.CopySources.Count > 0)
            {
                <label class="bp-product-version-selected-field">
                    <span>Start from</span>
                    <select name="copyFrom">
                        <option value="">Empty</option>
                        @foreach (var group in Model.CopySources.GroupBy(source => source.FamilyName))
                        {
                            <optgroup label="@group.Key">
                                @foreach (var source in group)
                                {
                                    <option value="@source.Model.Id">Copy of @source.Model.Name</option>
                                }
                            </optgroup>
                        }
                    </select>
                </label>
            }
            <div class="bp-product-version-selected-actions">
                <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap">Add model</button>
            </div>
        </form>
    </section>

    @if (Model.ModelEditor is { } modelEditor)
    {
        @await Html.PartialAsync("_RecordEditor", modelEditor)
    }

    @if (selected is not null)
    {
        <section class="bp-card bp-product-editor-section bp-console-form-card mt-4" id="batches">
            <div class="bp-product-section-heading">
                <div>
                    <h2>Batches of @selected.Name</h2>
                    <p class="bp-help-text mb-0">One per calendar year and manufacturing site: carbon footprint, recycled content, the declaration of conformity and test reports.</p>
                </div>
            </div>
            @if (Model.Batches.Count == 0)
            {
                <p class="mb-0">No batches yet.</p>
            }
            else
            {
                <div class="bp-table-scroll">
                    <table class="table table-hover align-middle bp-console-table mb-0">
                        <thead>
                            <tr><th>Year</th><th>Manufacturing site</th><th>Values</th><th>Batteries</th><th class="bp-action-cell">Actions</th></tr>
                        </thead>
                        <tbody>
                            @foreach (var row in Model.Batches)
                            {
                                <tr>
                                    <td>@row.Batch.Year</td>
                                    <td>@row.SiteName</td>
                                    <td>@await Html.PartialAsync("_Completeness", new Completeness(row.Filled, row.Total, row.Missing))</td>
                                    <td>@row.Packs (@row.Published published)</td>
                                    <td class="bp-action-cell"><a class="bp-secondary-button bp-button-compact text-nowrap" href="/admin/batches/@Uri.EscapeDataString(row.Batch.Id)#editor">Edit batch</a></td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
            @if (Model.Sites.Count == 0)
            {
                <div class="bp-product-version-selected-panel">
                    <div class="bp-product-version-selected-meta">
                        <strong>Add a manufacturing site first</strong>
                        <span>A batch is made at a manufacturing site, and there is none yet. You'll come back here after adding it.</span>
                    </div>
                    <div class="bp-product-version-selected-actions">
                        <a class="bp-secondary-button bp-button-compact text-nowrap" href="/admin/sites-and-operators?returnTo=@Uri.EscapeDataString(here + "#batches")">Add a manufacturing site</a>
                    </div>
                </div>
            }
            else
            {
                <form method="post" action="/admin/models/@Uri.EscapeDataString(selected.Id)/batches" class="bp-product-version-selected-panel">
                    @Html.AntiForgeryToken()
                    <label class="bp-product-version-selected-field">
                        <span>Year of the new batch</span>
                        <input type="text" inputmode="numeric" pattern="[0-9]{4}" name="year" value="@DateTime.UtcNow.Year" required />
                    </label>
                    <label class="bp-product-version-selected-field">
                        <span>Manufacturing site</span>
                        <select name="siteId" required>
                            <option value="">Choose a site…</option>
                            @foreach (var site in Model.Sites)
                            {
                                <option value="@site.Id">@site.Name</option>
                            }
                        </select>
                    </label>
                    <div class="bp-product-version-selected-actions">
                        <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap">Add batch</button>
                    </div>
                </form>
            }
        </section>
    }

    <details class="bp-card bp-remove-record mt-4">
        <summary>Remove this battery family</summary>
        @if (Model.Removal.Blocked is { } blocked)
        {
            <p class="mb-0 mt-2">It can't be removed: @blocked</p>
        }
        else
        {
            <form method="post" action="@Model.Removal.Action" class="d-flex flex-wrap align-items-center gap-3 mt-2">
                @Html.AntiForgeryToken()
                <span>It has no models. Removing it deletes its name and picture; this can't be undone.</span>
                <button type="submit" class="bp-danger-button bp-button-compact text-nowrap">Remove battery family</button>
            </form>
        }
    </details>
</main>

@section Scripts {
    <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 10: Write `Batch.cshtml`**

`web/Views/ProductData/Batch.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model BatchPage
@{
    ViewData["Title"] = $"Batch {Model.Batch.Year} · {Model.Site.Name} · {Model.Model.Name}";
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <nav class="bp-breadcrumb" aria-label="Breadcrumb">
        <a href="/admin/families">Battery families</a>
        <a href="@ProductDataPages.FamilyHref(Model.Family.Id)">@Model.Family.Name</a>
        <a href="@ProductDataPages.FamilyHref(Model.Family.Id, Model.Model.Id)#batches">@Model.Model.Name</a>
        <span aria-current="page">Batch @Model.Batch.Year · @Model.Site.Name</span>
    </nav>
    <header class="bp-page-heading">
        <span class="bp-summary-title">Batch</span>
        <h1>@Model.Batch.Year · @Model.Site.Name</h1>
        <p class="mb-0">
            Model @Model.Model.Name of @Model.Family.Name. Every battery of this model made at @Model.Site.Name in
            @Model.Batch.Year shares these values.
        </p>
    </header>

    @await Html.PartialAsync("_Messages")
    @await Html.PartialAsync("_RecordEditor", Model.Editor)
</main>

@section Scripts {
    <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 11: Write `SitesAndOperators.cshtml`**

`web/Views/ProductData/SitesAndOperators.cshtml`:

```cshtml
@using BatteryPassWeb.Dpp.Pages
@model SitesAndOperatorsPage
@{
    ViewData["Title"] = "Sites and operators";
    var back = ProductDataPages.SafeReturn(Context.Request.Query["returnTo"].ToString());
    string Keep(string href) => back is null ? href : $"{href}&returnTo={Uri.EscapeDataString(back)}";
}

<main class="bp-page bp-admin-page">
    <link rel="stylesheet" href="~/css/bp-product-data.css" asp-append-version="true" />
    <div>
        <h1 class="mb-1">Administration</h1>
        <p class="text-secondary mb-0">Manage battery families, sites and operators, batteries, cluster assignments, registered clusters, user memberships, credentials, and audit history.</p>
    </div>

    @await Html.PartialAsync("_AdminTabs", "sites-and-operators")
    @if (back is not null)
    {
        <div class="bp-page-return-row mt-3">
            <a href="@back" class="bp-page-return-link">Back to the battery model, to add its batch</a>
        </div>
    }
    @await Html.PartialAsync("_Messages")

    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4">
        <div class="bp-product-section-heading">
            <div>
                <h2>Manufacturing sites</h2>
                <p class="bp-help-text mb-0">Manufacturer identifier and information, and the manufacturing place (#12, #15, #16). Chosen when you add a batch.</p>
            </div>
        </div>
        @if (Model.Sites.Count == 0)
        {
            <p class="mb-0">No manufacturing sites yet.</p>
        }
        else
        {
            <div class="bp-table-scroll">
                <table class="table table-hover align-middle bp-console-table mb-0">
                    <thead><tr><th>Site</th><th>Values</th><th>Batches</th></tr></thead>
                    <tbody>
                        @foreach (var row in Model.Sites)
                        {
                            <tr class="@(Model.Editor?.Id == row.Site.Id ? "table-active" : string.Empty)">
                                <td><a href="@(Keep(ProductDataPages.SitesHref("site", row.Site.Id)))#editor" aria-current="@(Model.Editor?.Id == row.Site.Id ? "page" : null)">@row.Site.Name</a></td>
                                <td>@await Html.PartialAsync("_Completeness", new Completeness(row.Filled, row.Total, row.Missing))</td>
                                <td>@row.Batches</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
        <form method="post" action="/admin/sites" class="d-flex gap-2">
            @Html.AntiForgeryToken()
            @if (back is not null)
            {
                <input type="hidden" name="returnTo" value="@back" />
            }
            <input type="text" name="name" placeholder="New site name" aria-label="New site name" class="flex-grow-1" required />
            <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap">Add site</button>
        </form>
    </section>
    <section class="bp-card bp-product-editor-section bp-console-form-card mt-4">
        <div class="bp-product-section-heading">
            <div>
                <h2>Economic operators</h2>
                <p class="bp-help-text mb-0">The operator identifier and information (#7, #14). Chosen when you add a battery.</p>
            </div>
        </div>
        @if (Model.Operators.Count == 0)
        {
            <p class="mb-0">No economic operators yet.</p>
        }
        else
        {
            <div class="bp-table-scroll">
                <table class="table table-hover align-middle bp-console-table mb-0">
                    <thead><tr><th>Operator</th><th>Values</th><th>Batteries</th></tr></thead>
                    <tbody>
                        @foreach (var row in Model.Operators)
                        {
                            <tr class="@(Model.Editor?.Id == row.Operator.Id ? "table-active" : string.Empty)">
                                <td><a href="@(ProductDataPages.SitesHref("operator", row.Operator.Id))#editor" aria-current="@(Model.Editor?.Id == row.Operator.Id ? "page" : null)">@row.Operator.Name</a></td>
                                <td>@await Html.PartialAsync("_Completeness", new Completeness(row.Filled, row.Total, row.Missing))</td>
                                <td>@row.Packs</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
        <form method="post" action="/admin/operators" class="d-flex gap-2">
            @Html.AntiForgeryToken()
            <input type="text" name="name" placeholder="New operator name" aria-label="New operator name" class="flex-grow-1" required />
            <button type="submit" class="bp-secondary-button bp-button-compact text-nowrap">Add operator</button>
        </form>
    </section>

    @if (Model.Editor is { } editor)
    {
        @await Html.PartialAsync("_RecordEditor", editor)
    }
    else
    {
        <div class="bp-card mt-4">Choose a site or an operator above to edit its passport values.</div>
    }
</main>

@section Scripts {
    <script src="~/js/bp-record-editor.js" asp-append-version="true"></script>
}
```

- [ ] **Step 12: Write `bp-product-data.css`**

`web/wwwroot/css/bp-product-data.css`:

```css
/* The team's product data pages and their record editors (decisions 23 and 27). The palette, fonts, cards, buttons
   and inputs come from site.css; this file, loaded only on these pages, gives them a calmer finish: one card per
   thing, quiet outlined tags, orange only for "missing", green only for "done", one heading scale and gentle motion. */

/* ==== Shared on every team page ==================================================================================== */

/* Where you are: Battery families › family › model › batch. */
.bp-breadcrumb {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  margin-bottom: 12px;
  font-size: 0.9rem;
}

.bp-breadcrumb > * + *::before {
  content: "›";
  margin-right: 6px;
  color: var(--bp-muted);
}

.bp-breadcrumb a {
  color: var(--bp-primary);
  font-weight: 600;
  text-decoration: none;
}

.bp-breadcrumb a:hover,
.bp-breadcrumb a:focus-visible {
  text-decoration: underline;
}

.bp-breadcrumb [aria-current] {
  color: var(--bp-text);
  font-weight: 600;
}

/* One heading scale: the page title 2rem, the record being edited 1.4rem, card titles 1.25rem, card items 1.125rem. */
.bp-product-editor-hero h1,
.bp-page-heading h1 {
  margin: 2px 0 6px;
  font-size: 2rem;
  font-weight: 650;
}

.bp-product-editor-section h2,
.bp-product-section-heading h2,
.bp-product-version-console-header h2,
.bp-product-template-hero h2 {
  margin: 0 0 4px;
  font-size: 1.25rem;
  font-weight: 650;
}

/* A page heading without a card (the batch page). */
.bp-page-heading p {
  max-width: 980px;
  color: #52637a;
}

/* Stacked cards share one inner margin, so their text lines up. */
.bp-admin-page :is(.bp-product-editor-hero, .bp-product-version-console, .bp-product-editor-section, .bp-product-template-hero) {
  padding: 24px 28px;
}

/* Help text readable on white (6:1). */
.bp-help-text,
.bp-field-help {
  color: #52637a;
}

/* Typed text is regular weight, placeholders grey, also where site.css makes labels bold. */
main :is(input:not([type="checkbox"]), select, textarea) {
  font-weight: 400;
}

main :is(input, textarea)::placeholder {
  color: #7a8aa0;
  font-weight: 400;
}

/* A visible focus ring on every field. */
main :is(input:not([type="checkbox"]):not([type="file"]), select, textarea):focus {
  border-color: var(--bp-primary);
  box-shadow: 0 0 0 3px rgba(42, 110, 207, 0.25);
  outline: none;
}

/* "Choose file" hides its file input: show the focus on the button instead. */
.bp-document-smart-upload:focus-within {
  outline: 2px solid var(--bp-primary);
  outline-offset: 2px;
}

/* Markers ("Required later", "Conditional", "Not passport data"): small outlined tags in sentence case. */
main .bp-field-marker {
  display: inline-block;
  padding: 1px 8px;
  border: 1px solid #d6dee8;
  border-radius: 999px;
  background: transparent;
  color: #405367;
  font-size: 0.75rem;
  font-style: normal;
  font-weight: 600;
  letter-spacing: 0;
  text-transform: none;
  vertical-align: 1px;
}

/* "Missing" is an orange dot and words; "done" a green tick. Orange and green mean nothing else here. */
.bp-missing-note,
.bp-done-note,
.bp-attr-missing {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  width: fit-content;
  font-weight: 600;
}

.bp-missing-note,
.bp-attr-missing {
  color: #c2410c;
}

.bp-missing-note::before,
.bp-attr-missing::before,
.bp-missing-dot {
  content: "";
  flex: none;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #c2410c;
}

.bp-done-note {
  color: #166534;
}

.bp-done-note::before {
  content: "\2713";
}

/* A thin progress bar: how many values are filled in. */
.bp-progress {
  width: 200px;
  height: 6px;
  border: 0;
  border-radius: 999px;
  background: #e6ebf1;
  overflow: hidden;
  appearance: none;
}

.bp-progress.is-small {
  width: 64px;
  height: 5px;
}

.bp-progress::-webkit-progress-bar {
  border-radius: 999px;
  background: #e6ebf1;
}

.bp-progress::-webkit-progress-value {
  border-radius: 999px;
  background: var(--bp-primary);
}

.bp-progress::-moz-progress-bar {
  border-radius: 999px;
  background: var(--bp-primary);
}

/* "11 of 16 filled · 4 missing for Feb 2027" in tables. */
.bp-completeness {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #52637a;
  font-size: 0.85rem;
  font-weight: 600;
  white-space: nowrap;
}

/* Messages are a slim tinted line, not a block. */
main .bp-status-line.alert {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 0;
  padding: 8px 12px;
  border: 0;
  border-radius: 8px;
  font-size: 0.92rem;
  font-weight: 600;
}

main .bp-status-line.is-done {
  background: #ecfdf3;
  color: #166534;
}

main .bp-status-line.is-problem {
  background: #fef2f2;
  color: #991b1b;
}

main .bp-status-line.is-info {
  background: #f1f5f9;
  color: #334155;
}

.bp-status-line svg {
  flex: none;
  width: 16px;
  height: 16px;
  fill: none;
  stroke: currentColor;
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* "Remove this …": closed until asked for, at the end of the page. */
.bp-remove-record {
  padding: 14px 28px;
}

.bp-remove-record summary {
  cursor: pointer;
  color: #52637a;
  font-weight: 600;
}

.bp-remove-record summary:hover,
.bp-remove-record[open] summary {
  color: var(--bp-danger);
}

/* The dialogs. */
.bp-modal-panel {
  border-radius: 14px;
}

/* "Choose file" and "Choose picture" are labels styled as buttons: the same weight and colour as the buttons beside them. */
.bp-console-form-card label.bp-secondary-button {
  color: #334155;
  font-size: 0.88rem;
  font-weight: 400;
}

/* A table row that is open below: a light blue tint, not a grey band. */
.bp-console-table tr.table-active {
  --bs-table-bg-state: rgba(42, 110, 207, 0.08);
}

/* ==== Families list ================================================================================================= */

.bp-add-family {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 12px;
}

.bp-add-family .bp-product-version-selected-field {
  flex: 1 1 320px;
  max-width: 520px;
}

.bp-product-template-card-media,
.bp-product-editor-preview {
  border-color: #e6ebf1;
  border-radius: 14px;
  background: #f5f7fa;
}

.bp-product-template-card-body h3 {
  font-size: 1.125rem;
  font-weight: 650;
}

.bp-product-template-meta div {
  padding: 8px 10px;
  border: 0;
  border-radius: 10px;
  background: #f5f7fa;
}

.bp-product-template-meta dt {
  color: #52637a;
  font-size: 0.78rem;
  font-weight: 500;
  letter-spacing: 0;
  text-transform: none;
}

.bp-product-template-meta dd {
  font-weight: 650;
}

/* ==== Family page =================================================================================================== */

/* The family's name and picture, in the page header. */
.bp-family-identity {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
  gap: 12px 20px;
}

.bp-family-identity-field {
  display: grid;
  gap: 6px;
  margin: 0;
}

.bp-family-identity-field > label,
.bp-family-identity-field > span {
  color: #405367;
  font-size: 0.8rem;
  font-weight: 700;
}

.bp-family-identity-field input[type="text"] {
  flex: 1 1 auto;
  min-width: 0;
}

/* "A picture is set" is a note about the picture, not a field to type in. */
.bp-family-identity input[data-upload-name][readonly] {
  border-style: dashed;
  background: transparent;
}

/* The model cards wrap onto more lines instead of scrolling, so no model is ever out of sight. */
.bp-product-version-chip-rail {
  flex-wrap: wrap;
  overflow: visible;
}

/* The model cards: the name, then "26 missing · 2 of 37 filled"; the open one outlined in blue. */
.bp-product-version-tab.bp-product-version-chip {
  flex: 1 1 220px;
  max-width: 280px;
  grid-template-columns: minmax(0, 1fr);
  gap: 4px;
  padding: 12px 14px;
  border: 1px solid #e3e9f0;
  background: var(--bp-surface);
}

.bp-product-version-tab.bp-product-version-chip:hover {
  border-color: #b6ccde;
}

.bp-product-version-tab.bp-product-version-chip.is-active {
  border-color: var(--bp-primary);
  box-shadow: 0 0 0 1px var(--bp-primary), 0 6px 16px rgba(10, 40, 80, 0.08);
}

.bp-product-version-chip .bp-product-version-tab-main strong {
  font-size: 1rem;
  font-weight: 650;
}

.bp-chip-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 2px 10px;
  color: #52637a;
  font-size: 0.8rem;
}

/* ==== The record editor ============================================================================================= */

.bp-editor-card {
  padding: 24px 28px;
}

.bp-editor-top {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px 32px;
}

.bp-editor-heading {
  display: grid;
  flex: 1 1 420px;
  gap: 4px;
  min-width: 0;
}

.bp-editor-card .bp-editor-title {
  margin: 0;
  color: var(--bp-text);
  font-size: 1.4rem;
  font-weight: 650;
}

.bp-editor-summary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px 10px;
  margin: 2px 0 0;
  color: #52637a;
  font-size: 0.92rem;
}

.bp-editor-summary strong {
  color: var(--bp-text);
  font-weight: 650;
}

.bp-done-mark {
  color: #15803d;
  font-weight: 700;
}

.bp-editor-progress {
  display: grid;
  justify-items: end;
  gap: 6px;
  min-width: 200px;
  color: #52637a;
  font-size: 0.85rem;
  font-weight: 600;
}

#editor .bp-editor-status {
  margin-top: 16px;
}

/* The name and the filter on one line, the legend as one quiet line under them. */
.bp-editor-tools {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px 28px;
  margin-top: 20px;
  padding-top: 18px;
  border-top: 1px solid #eef1f5;
}

.bp-editor-card .bp-editor-name {
  display: grid;
  flex: 0 1 340px;
  gap: 6px;
  margin: 0;
}

.bp-editor-name > span {
  color: #405367;
  font-size: 0.8rem;
  font-weight: 700;
}

.bp-editor-legend {
  display: flex;
  flex: 1 1 100%;
  flex-wrap: wrap;
  order: 1;
  gap: 2px 14px;
  color: #52637a;
  font-size: 0.82rem;
  line-height: 1.6;
}

.bp-editor-card .bp-editor-switch {
  display: flex;
  align-items: center;
  align-self: flex-end;
  gap: 10px;
  margin: 0 0 0 auto;
  padding-bottom: 10px;
  color: var(--bp-text);
  font-size: 0.9rem;
  font-weight: 600;
  white-space: nowrap;
}

.bp-editor-switch input[role="switch"] {
  position: relative;
  flex: none;
  width: 34px;
  height: 20px;
  margin: 0;
  border-radius: 999px;
  background: #cbd5e1;
  cursor: pointer;
  appearance: none;
  transition: background-color 0.15s ease;
}

.bp-editor-switch input[role="switch"]::before {
  content: "";
  position: absolute;
  top: 2px;
  left: 2px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 1px 2px rgba(15, 23, 42, 0.25);
  transition: transform 0.15s ease;
}

.bp-editor-switch input[role="switch"]:checked {
  background: var(--bp-primary);
}

.bp-editor-switch input[role="switch"]:checked::before {
  transform: translateX(14px);
}

.bp-editor-switch input[role="switch"]:focus-visible {
  outline: 2px solid var(--bp-primary);
  outline-offset: 2px;
}

/* The side menu and the sections, inside the one card. */
.bp-editor-body {
  margin-top: 22px;
}

@media (min-width: 1024px) {
  .bp-editor-card .bp-edit-grid {
    grid-template-columns: 236px minmax(0, 1fr);
    gap: 28px;
  }

  /* The menu stays in view while a long section scrolls (site.css's top assumes a sticky header). */
  .bp-edit-nav {
    align-self: start;
    top: 16px;
  }
}

.bp-editor-card .bp-edit-nav {
  gap: 2px;
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button {
  position: relative;
  display: grid;
  gap: 1px;
  padding: 9px 12px 9px 14px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  box-shadow: none;
  color: #334155;
  text-align: left;
  transition: background-color 0.15s ease, color 0.15s ease;
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button:hover {
  background: #f3f6fa;
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button.is-active {
  background: #eaf2fd;
  color: var(--bp-scania-blue);
  font-weight: 400;
}

/* The open section has a blue bar; a section with a refused value a red one. */
.bp-editor-card .bp-edit-nav .bp-edit-nav-button.is-active::before,
.bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-missing-fields::before {
  content: "";
  position: absolute;
  top: 8px;
  bottom: 8px;
  left: 0;
  width: 3px;
  border-radius: 3px;
  background: var(--bp-primary);
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-missing-fields {
  color: #991b1b;
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-missing-fields::before {
  background: #dc2626;
}

.bp-edit-nav-label {
  overflow: hidden;
  font-size: 0.92rem;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button .bp-edit-nav-count {
  float: none;
  margin: 0;
  color: #52637a;
  font-size: 0.78rem;
  font-weight: 500;
}

.bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-missing-values .bp-edit-nav-count::before {
  content: "";
  display: inline-block;
  width: 6px;
  height: 6px;
  margin-right: 6px;
  border-radius: 50%;
  background: #c2410c;
  vertical-align: 1px;
}

/* Unsaved edits in a section: a blue dot after its name (blue is "yours", orange is "missing"). */
.bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-edits .bp-edit-nav-label::after {
  content: " •";
  color: var(--bp-primary);
  font-weight: 800;
}

/* Sections are areas of the one card, not cards of their own. */
.bp-editor-card .bp-edit-section.bp-card {
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
  scroll-margin-top: 16px;
}

.bp-editor-card .bp-edit-section-title {
  margin: 0 0 2px;
  font-size: 1.12rem;
  font-weight: 650;
}

.bp-editor-card .bp-template-sync-note {
  margin: 0 0 6px;
  color: #52637a;
  font-size: 0.88rem;
}

/* One row per attribute: what it is on the left, its inputs on the right, a hairline between rows. */
.bp-attr-list {
  display: grid;
}

.bp-attr-row {
  display: grid;
  grid-template-columns: minmax(220px, 320px) minmax(0, 1fr);
  gap: 8px 28px;
  padding: 18px 0;
  border-top: 1px solid #eef1f5;
}

.bp-attr-row:first-child {
  padding-top: 4px;
  border-top: 0;
}

.bp-attr-label {
  display: grid;
  align-content: start;
  gap: 6px;
}

.bp-attr-label strong {
  color: var(--bp-text);
  font-size: 0.95rem;
  font-weight: 600;
  line-height: 1.35;
}

.bp-attr-label .bp-field-marker {
  justify-self: start;
}

.bp-attr-number {
  margin-right: 2px;
  color: #56667c;
  font-variant-numeric: tabular-nums;
}

.bp-attr-missing {
  font-size: 0.8rem;
}

/* Who may see a value that isn't public: one quiet line, the list on request. */
.bp-attr-audience summary {
  width: fit-content;
  cursor: pointer;
  color: #52637a;
  font-size: 0.8rem;
  font-weight: 500;
}

.bp-attr-audience summary svg {
  width: 13px;
  height: 13px;
  margin-right: 5px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.6;
  vertical-align: -2px;
}

.bp-attr-audience span {
  display: block;
  margin-top: 2px;
  color: #52637a;
  font-size: 0.8rem;
  line-height: 1.4;
}

.bp-attr-inputs {
  display: grid;
  align-content: start;
  gap: 8px;
  min-width: 0;
}

/* Every input of the editor has one size. */
.bp-attr-list :is(input:not([type="checkbox"]):not([type="file"]), select, textarea) {
  min-height: 42px;
  font-size: 1rem;
}

/* One right edge: a single input ends where the second part of a group ends. */
.bp-attr-inputs > input,
.bp-attr-inputs > select,
.bp-attr-fixed {
  max-width: calc((100% - 28px) * 2 / 3 + 14px);
}

/* At most three parts per line; four parts read two by two. */
.bp-attr-parts {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(max(190px, (100% - 28px) / 3), 1fr));
  gap: 4px 14px;
}

.bp-attr-parts:has(> label:nth-child(4):last-child) {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

/* Each part is a label over its input; the labels of one line share a row, so the inputs line up. */
.bp-attr-parts label {
  display: grid;
  grid-row: span 2;
  grid-template-rows: subgrid;
  gap: 4px;
  margin-bottom: 8px;
}

.bp-attr-parts label > span {
  align-self: end;
  color: #405367;
  font-size: 0.8rem;
  font-weight: 500;
}

.bp-attr-parts label > input,
.bp-attr-parts label > select {
  align-self: start;
}

.bp-attr-parts .bp-attr-wide {
  grid-column: 1 / -1;
}

.bp-attr-optional {
  color: #52637a;
}

.bp-attr-row code {
  color: var(--bp-muted);
  word-break: break-all;
}

.bp-attr-fixed {
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--bp-bg);
  color: var(--bp-text);
}

/* Lists of rows (#44): each row in a box, titled by its content; the empty row waits behind "+ Add a row". */
.bp-attr-repeat {
  padding: 12px;
  border: 1px solid var(--bp-border);
  border-radius: 12px;
  background: var(--bp-surface);
}

.bp-attr-repeat-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.bp-attr-row-title {
  color: var(--bp-text);
  font-size: 0.88rem;
  font-weight: 650;
}

.bp-attr-repeat.is-removed,
.bp-attr-repeat.is-new-row.is-collapsed {
  display: none;
}

.bp-add-row {
  justify-self: start;
}

/* A document: the current file, then a framed box to upload another; it is sent on its own, not with Save. */
.bp-attr-upload {
  display: grid;
  gap: 8px;
  margin: 0;
  padding: 8px 12px 10px;
  border: 1px dashed var(--bp-border);
  border-radius: 10px;
}

.bp-attr-upload legend {
  float: none;
  width: auto;
  margin: 0;
  padding: 0 6px;
  color: #405367;
  font-size: 0.78rem;
  font-weight: 700;
}

.bp-attr-file {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
}

.bp-attr-file [data-upload-chosen] {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bp-attr-confirm {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin: 0;
  color: #9a3412;
  font-weight: 600;
}

/* A refused value: red on the field itself or only on the refused parts of a grouped value. */
.bp-attr-row.is-invalid .bp-attr-inputs > input,
.bp-attr-row.is-invalid .bp-attr-inputs > select,
.bp-attr-row.is-invalid .bp-attr-inputs > textarea,
.bp-attr-row.is-invalid .bp-attr-repeat :is(input, select, textarea),
.bp-attr-parts label.is-invalid-part :is(input, select, textarea) {
  border-color: #dc2626;
  background: #fff7f7;
  outline: 3px solid rgba(220, 38, 38, 0.12);
}

.bp-attr-row.is-invalid .bp-field-marker {
  border-color: #fca5a5;
  color: #991b1b;
}

/* "Show only missing values": rows that are filled in or fine step aside. */
.bp-only-missing .bp-attr-row:not(.is-missing):not(.is-invalid),
.bp-only-missing .bp-edit-section.has-no-missing {
  display: none;
}

/* The save bar: opaque, covering the gap below it; quiet while there is nothing to save, lifted when there is. */
.bp-editor-card .bp-product-editor-savebar {
  bottom: 12px;
  margin-top: 8px;
  padding: 10px 12px 10px 18px;
  border: 1px solid var(--bp-border);
  border-radius: 12px;
  background: var(--bp-surface);
  box-shadow: 0 12px 0 var(--bp-surface), 0 4px 14px rgba(10, 40, 80, 0.06);
  transition: box-shadow 0.2s ease, border-color 0.2s ease;
}

.bp-editor-card .bp-product-editor-savebar.has-unsaved {
  border-color: rgba(42, 110, 207, 0.45);
  box-shadow: 0 12px 0 var(--bp-surface), 0 10px 28px rgba(10, 40, 80, 0.14);
}

.bp-editor-card .bp-savebar-text {
  min-width: 0;
  overflow: hidden;
  color: #52637a;
  font-size: 0.9rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bp-editor-card .has-unsaved .bp-savebar-text {
  color: var(--bp-text);
  font-weight: 600;
}

.bp-editor-card .has-unsaved .bp-savebar-text::before {
  content: "";
  display: inline-block;
  width: 7px;
  height: 7px;
  margin-right: 8px;
  border-radius: 50%;
  background: var(--bp-primary);
  vertical-align: 1px;
}

.bp-editor-card .bp-savebar-text.is-done {
  color: #166534;
  font-weight: 600;
}

.bp-editor-card .bp-savebar-text.is-done::before {
  content: "\2713  ";
}

.bp-savebar-actions {
  display: flex;
  flex-shrink: 0;
  flex-wrap: nowrap;
  gap: 8px;
}

.bp-product-editor-savebar button {
  white-space: nowrap;
}

.bp-editor-card .bp-product-editor-savebar:not(.has-unsaved) .bp-primary-button {
  border-color: var(--bp-border);
  background: transparent;
  color: var(--bp-primary);
}

.bp-editor-card .bp-product-editor-savebar:not(.has-unsaved) .bp-secondary-button {
  visibility: hidden;
}

/* ==== Motion: gentle, and only for people who have not asked their system for less ================================= */

@media (prefers-reduced-motion: no-preference) {
  .bp-editor-card .bp-edit-section:not([hidden]) {
    animation: bp-section-in 0.18s ease-out;
  }

  .bp-status-line,
  .bp-savebar-text.is-done {
    animation: bp-fade-in 0.3s ease-out;
  }
}

@keyframes bp-section-in {
  from {
    opacity: 0;
    transform: translateY(4px);
  }

  to {
    opacity: 1;
    transform: none;
  }
}

@keyframes bp-fade-in {
  from {
    opacity: 0;
  }

  to {
    opacity: 1;
  }
}

/* ==== Dark theme ===================================================================================================== */

[data-theme="dark"] .bp-help-text,
[data-theme="dark"] .bp-field-help,
[data-theme="dark"] .bp-completeness,
[data-theme="dark"] .bp-chip-meta,
[data-theme="dark"] .bp-editor-summary,
[data-theme="dark"] .bp-editor-progress,
[data-theme="dark"] .bp-editor-legend,
[data-theme="dark"] .bp-editor-card .bp-template-sync-note,
[data-theme="dark"] .bp-editor-card .bp-savebar-text,
[data-theme="dark"] .bp-editor-card .bp-edit-nav .bp-edit-nav-button .bp-edit-nav-count,
[data-theme="dark"] .bp-attr-audience summary,
[data-theme="dark"] .bp-attr-audience span,
[data-theme="dark"] .bp-attr-optional,
[data-theme="dark"] .bp-remove-record summary,
[data-theme="dark"] .bp-product-template-meta dt {
  color: var(--bp-muted);
}

[data-theme="dark"] .bp-family-identity-field > label,
[data-theme="dark"] .bp-family-identity-field > span,
[data-theme="dark"] .bp-editor-name > span,
[data-theme="dark"] .bp-editor-card .bp-attr-parts label > span,
[data-theme="dark"] .bp-attr-upload legend,
[data-theme="dark"] .bp-console-form-card label {
  color: var(--bp-muted);
}

[data-theme="dark"] main :is(input, textarea)::placeholder {
  color: #7f93ad;
}

[data-theme="dark"] main :is(input:not([type="checkbox"]):not([type="file"]), select, textarea):focus {
  border-color: var(--bp-primary);
  box-shadow: 0 0 0 3px rgba(122, 182, 255, 0.35);
}

/* site.css gives cards' inputs a white background that its dark rule doesn't reach. */
[data-theme="dark"] .bp-console-form-card :is(input:not([type="checkbox"]), select, textarea) {
  border-color: var(--bp-border);
  background: #101f35;
  color: var(--bp-text);
}

[data-theme="dark"] .bp-console-form-card :is(input, textarea)[readonly] {
  background: #0b1628;
  color: var(--bp-muted);
}

[data-theme="dark"] main .bp-field-marker {
  border-color: #2f4566;
  background: transparent;
  color: #c3d3e6;
}

[data-theme="dark"] .bp-missing-note,
[data-theme="dark"] .bp-attr-missing,
[data-theme="dark"] .bp-attr-confirm {
  color: #fdba74;
}

[data-theme="dark"] .bp-done-note,
[data-theme="dark"] .bp-editor-card .bp-savebar-text.is-done {
  color: #86efac;
}

[data-theme="dark"] .bp-progress,
[data-theme="dark"] .bp-progress::-webkit-progress-bar {
  background: #24395a;
}

[data-theme="dark"] main .bp-status-line.is-done {
  background: rgba(34, 197, 94, 0.12);
  color: #86efac;
}

[data-theme="dark"] main .bp-status-line.is-problem {
  background: rgba(255, 107, 120, 0.12);
  color: var(--bp-danger);
}

[data-theme="dark"] main .bp-status-line.is-info {
  background: rgba(148, 163, 184, 0.12);
  color: var(--bp-muted);
}

[data-theme="dark"] .bp-console-form-card label.bp-secondary-button {
  color: var(--bp-text);
}

[data-theme="dark"] .bp-console-table tr.table-active {
  --bs-table-bg-state: rgba(122, 182, 255, 0.12);
}

[data-theme="dark"] .bp-page-heading p {
  color: var(--bp-muted);
}

[data-theme="dark"] .bp-product-template-card-media,
[data-theme="dark"] .bp-product-editor-preview,
[data-theme="dark"] .bp-product-template-meta div {
  border-color: var(--bp-border);
  background: #12233b;
}

[data-theme="dark"] .bp-product-version-tab.bp-product-version-chip {
  border-color: var(--bp-border);
  background: #101f35;
}

[data-theme="dark"] .bp-product-version-tab.bp-product-version-chip.is-active {
  border-color: var(--bp-primary);
}

[data-theme="dark"] .bp-editor-tools,
[data-theme="dark"] .bp-attr-row {
  border-color: #1d2f48;
}

[data-theme="dark"] .bp-attr-number {
  color: #8ea2bd;
}

[data-theme="dark"] .bp-editor-card .bp-edit-nav .bp-edit-nav-button {
  background: transparent;
  color: var(--bp-text);
}

[data-theme="dark"] .bp-editor-card .bp-edit-nav .bp-edit-nav-button:hover {
  background: #12233b;
}

[data-theme="dark"] .bp-editor-card .bp-edit-nav .bp-edit-nav-button.is-active,
[data-theme="dark"] .bp-editor-card .bp-edit-nav .bp-edit-nav-button.is-active .bp-edit-nav-count {
  background: #132d4f;
  color: #cce3ff;
}

[data-theme="dark"] .bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-missing-fields {
  color: var(--bp-danger);
}

[data-theme="dark"] .bp-editor-card .bp-product-editor-savebar.has-unsaved {
  border-color: rgba(122, 182, 255, 0.5);
}

[data-theme="dark"] .bp-editor-switch input[role="switch"] {
  background: #33476a;
}

[data-theme="dark"] .bp-editor-switch input[role="switch"]:checked {
  background: var(--bp-primary);
}

/* The dialogs: site.css draws them white in both themes. */
[data-theme="dark"] .bp-modal-panel {
  border-color: var(--bp-border);
  background: #101f35;
  color: var(--bp-text);
}

/* The refused red stays readable on the dark surfaces. */
[data-theme="dark"] .bp-attr-row.is-invalid .bp-attr-inputs > input,
[data-theme="dark"] .bp-attr-row.is-invalid .bp-attr-inputs > select,
[data-theme="dark"] .bp-attr-row.is-invalid .bp-attr-inputs > textarea,
[data-theme="dark"] .bp-attr-row.is-invalid .bp-attr-repeat :is(input, select, textarea),
[data-theme="dark"] .bp-attr-parts label.is-invalid-part :is(input, select, textarea) {
  border-color: var(--bp-danger);
  background: rgba(255, 107, 120, 0.1);
  outline-color: rgba(255, 107, 120, 0.18);
}

[data-theme="dark"] .bp-attr-row.is-invalid .bp-field-marker {
  border-color: rgba(255, 107, 120, 0.5);
  color: var(--bp-danger);
}

/* ==== Narrower screens =============================================================================================== */

/* Up to 1280 px: a narrower label column, so groups keep their parts side by side. */
@media (max-width: 1279.98px) {
  .bp-attr-row {
    grid-template-columns: minmax(170px, 230px) minmax(0, 1fr);
    gap: 8px 20px;
  }
}

/* Below 1024 px: the side menu becomes a row of tabs above the form, underlined when open. */
@media (max-width: 1023.98px) {
  .bp-editor-card,
  .bp-admin-page :is(.bp-product-editor-hero, .bp-product-version-console, .bp-product-editor-section, .bp-product-template-hero) {
    padding: 18px 16px;
  }

  .bp-remove-record {
    padding: 12px 16px;
  }

  .bp-editor-progress {
    justify-items: start;
  }

  .bp-editor-card .bp-editor-switch {
    margin-left: 0;
  }

  .bp-edit-grid {
    grid-template-columns: minmax(0, 1fr);
  }

  .bp-editor-card .bp-edit-nav {
    position: sticky;
    top: 0;
    z-index: 3;
    grid-auto-flow: column;
    grid-auto-columns: max-content;
    overflow-x: auto;
    padding: 6px 0;
    background: var(--bp-surface);
  }

  .bp-editor-card .bp-edit-nav .bp-edit-nav-button.is-active::before,
  .bp-editor-card .bp-edit-nav .bp-edit-nav-button.has-missing-fields::before {
    top: auto;
    right: 12px;
    bottom: 0;
    left: 12px;
    width: auto;
    height: 2px;
  }

  .bp-editor-card .bp-edit-section.bp-card {
    scroll-margin-top: 64px;
  }

  .bp-editor-card .bp-product-editor-savebar {
    padding: 8px 10px 8px 14px;
  }
}

/* Between 721 and 1023 px the add-model form keeps its three parts in one row, inside the card's margin. */
@media (min-width: 721px) and (max-width: 1023.98px) {
  .bp-add-model {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto;
  }
}

@media (max-width: 900px) {
  .bp-attr-row {
    grid-template-columns: minmax(0, 1fr);
  }

  .bp-attr-file {
    grid-template-columns: auto minmax(0, 1fr);
  }

  .bp-attr-file [data-upload-button] {
    grid-column: 1 / -1;
    justify-self: start;
  }
}
```

- [ ] **Step 13: Write `bp-record-editor.js`**

`web/wwwroot/js/bp-record-editor.js`:

```javascript
// The team's product data pages: the editors' side menu, "Show only the values still needed", removing a row,
// "Discard unsaved edits?", the warning before leaving with unsaved edits, and the republish question.
// Without JavaScript every section stays visible and the forms still work.
(function () {
    // A chosen file shows its name; its Upload button waits for one.
    document.querySelectorAll('[data-upload-file]').forEach((file) => {
        const row = file.closest('[data-upload-row], .bp-attr-row');
        const chosen = row?.querySelector('[data-upload-chosen]');
        const name = row?.querySelector('[data-upload-name]');
        const button = row?.querySelector('[data-upload-button]');
        const update = () => {
            const picked = file.files && file.files.length > 0 ? file.files[0].name : null;
            if (button) {
                button.disabled = !picked;
            }

            if (chosen) {
                chosen.textContent = picked ? 'Ready to upload: ' + picked : 'No file chosen';
            } else if (name && picked) {
                name.value = 'Ready to upload: ' + picked;
            }
        };
        file.addEventListener('change', update);
        update();
    });

    // On a narrow screen the selected model's card can sit past the edge of the scrolling model row: bring it into view
    // within the row only, so the page itself doesn't jump.
    const selectedChip = document.querySelector('.bp-product-version-chip-rail .bp-product-version-chip.is-active');
    const rail = selectedChip?.closest('.bp-product-version-chip-rail');
    if (selectedChip && rail && rail.scrollWidth > rail.clientWidth) {
        rail.scrollLeft += selectedChip.getBoundingClientRect().left - rail.getBoundingClientRect().left - 16;
    }

    const form = document.querySelector('[data-record-editor]');
    if (!form) {
        return;
    }

    const navButtons = Array.from(form.querySelectorAll('.bp-edit-nav-button[data-section-target]'));
    const sections = Array.from(form.querySelectorAll('.bp-edit-section[id]'));

    function showSection(id) {
        sections.forEach((section) => { section.hidden = section.id !== id; });
        navButtons.forEach((button) => {
            const active = button.dataset.sectionTarget === id;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', active ? 'true' : 'false');
        });
    }

    // Opening a section from further down the page starts at its heading, not in its middle.
    navButtons.forEach((button) => button.addEventListener('click', () => {
        showSection(button.dataset.sectionTarget);
        const section = document.getElementById(button.dataset.sectionTarget);
        if (section && section.getBoundingClientRect().top < 0) {
            section.scrollIntoView({ block: 'start' });
        }
    }));

    // A list's empty row waits behind "+ Add a row", unless something was typed into it.
    form.querySelectorAll('[data-add-row]').forEach((add) => {
        const newRow = add.parentElement.querySelector('.bp-attr-repeat.is-new-row');
        const typed = newRow && Array.from(newRow.querySelectorAll('input, select, textarea')).some((input) => input.value.trim() !== '');
        if (!newRow || typed) {
            return;
        }

        newRow.classList.add('is-collapsed');
        add.hidden = false;
        add.addEventListener('click', () => {
            newRow.classList.remove('is-collapsed');
            add.hidden = true;
            newRow.querySelector('input, select, textarea')?.focus();
        });
    });

    // With a side menu, one section shows at a time: sections with a refused value are marked and the first of them
    // opens. A small record has no menu and shows all its sections.
    const refused = sections.filter((section) => section.querySelector('.is-invalid'));
    refused.forEach((section) => navButtons.find((button) => button.dataset.sectionTarget === section.id)?.classList.add('has-missing-fields'));
    if (navButtons.length > 0 && sections.length > 0) {
        showSection((refused[0] || sections[0]).id);
    }

    // After a save with refused values, bring the first one into view, ready to correct.
    const firstRefused = form.querySelector('.is-invalid');
    if (firstRefused) {
        firstRefused.scrollIntoView({ block: 'center' });
        (firstRefused.querySelector('.is-invalid-part input, .is-invalid-part select, .is-invalid-part textarea')
            || firstRefused.querySelector('input, select, textarea'))?.focus({ preventScroll: true });
    }

    // "Show only missing values": hides filled rows, sections with nothing missing and their side-menu entries, and
    // opens the first section that still needs something. It stays on after a save, for this page.
    const missingOnly = document.querySelector('[data-missing-only]');
    const missingOnlyKey = 'bp-missing-only:' + location.pathname + location.search;
    function applyMissingOnly(keepSection) {
        form.classList.toggle('bp-only-missing', missingOnly.checked);
        navButtons.forEach((button) => {
            button.hidden = missingOnly.checked && !button.classList.contains('has-missing-values') && !button.classList.contains('has-missing-fields');
        });
        const open = navButtons.find((button) => button.classList.contains('is-active'));
        if (missingOnly.checked && navButtons.length > 0 && !(keepSection && open && !open.hidden)) {
            const first = navButtons.find((button) => !button.hidden);
            if (first) {
                showSection(first.dataset.sectionTarget);
            }
        }
    }

    if (missingOnly) {
        try {
            missingOnly.checked = sessionStorage.getItem(missingOnlyKey) === 'on';
        } catch {
            // no storage: the switch starts off
        }

        if (missingOnly.checked) {
            applyMissingOnly(true);
        }

        missingOnly.addEventListener('change', () => {
            try {
                sessionStorage.setItem(missingOnlyKey, missingOnly.checked ? 'on' : 'off');
            } catch {
                // no storage: the switch is forgotten on the next page
            }

            applyMissingOnly(false);
        });
    }

    // "Remove row": empties the row so the save leaves it out, and hides it.
    form.querySelectorAll('[data-remove-row]').forEach((button) => {
        button.addEventListener('click', () => {
            const row = button.closest('.bp-attr-repeat');
            row.querySelectorAll('input, select, textarea').forEach((input) => { input.value = ''; });
            row.classList.add('is-removed');
        });
    });

    function formState() {
        const entries = [];
        for (const [key, value] of new FormData(form).entries()) {
            if (key !== '__RequestVerificationToken' && key !== 'confirmed') {
                entries.push(key + '=' + String(value));
            }
        }

        return entries.sort().join('&');
    }

    // A page shown again with refused values or the republish question holds typed values that aren't saved yet. Its
    // address is the save address, which can't be opened again: show the page's own address instead.
    const unsavedOnArrival = form.querySelector('.is-invalid, [data-confirm-modal]') !== null;
    const pristine = unsavedOnArrival ? '' : formState();
    let leaving = false;
    if (unsavedOnArrival && form.dataset.cancelHref) {
        history.replaceState(null, '', form.dataset.cancelHref + '#editor');
    }

    // The save bar says whether there are unsaved changes and where; the side menu marks those sections.
    const saveBar = form.querySelector('.bp-product-editor-savebar');
    const saveStatus = form.querySelector('[data-save-status]');
    const cancelEdits = form.querySelector('[data-cancel-edits]');
    const fieldsOf = (section) => Array.from(section.querySelectorAll('input, select, textarea')).filter((input) => input.name && input.type !== 'file');
    const initial = new Map(sections.flatMap((section) => fieldsOf(section).map((input) => [input, input.value])));
    function showUnsaved() {
        const dirty = unsavedOnArrival || formState() !== pristine;
        const edited = sections.filter((section) => fieldsOf(section).some((input) => initial.get(input) !== input.value));
        navButtons.forEach((button) => button.classList.toggle('has-edits', edited.some((section) => section.id === button.dataset.sectionTarget)));
        saveBar?.classList.toggle('has-unsaved', dirty);
        if (cancelEdits) {
            cancelEdits.disabled = !dirty;
        }

        if (saveStatus && !(saveStatus.classList.contains('is-done') && !dirty)) {
            const where = edited.map((section) => section.dataset.sectionTitle).filter(Boolean);
            saveStatus.classList.remove('is-done');
            saveStatus.textContent = !dirty
                ? 'No unsaved changes.'
                : where.length > 0 && navButtons.length > 0
                    ? 'Unsaved changes in ' + where.join(', ') + '. Save to keep them.'
                    : 'Unsaved changes. Save to keep them.';
        }
    }

    // After a save the page reloads: come back to the section and the place on the page where Save was pressed, and
    // say how it went in the save bar, which is where the eye is.
    const returnKey = 'bp-editor-return';
    form.addEventListener('submit', () => {
        try {
            const open = sections.find((section) => !section.hidden);
            sessionStorage.setItem(returnKey, JSON.stringify({ page: location.pathname + location.search, section: open?.id, y: window.scrollY, at: Date.now() }));
        } catch {
            // no storage: the page opens at the editor as before
        }
    });
    let back = null;
    try {
        back = JSON.parse(sessionStorage.getItem(returnKey) || 'null');
        sessionStorage.removeItem(returnKey);
    } catch {
        back = null;
    }

    if (back && !unsavedOnArrival && back.page === location.pathname + location.search && Date.now() - back.at < 15000) {
        if (back.section && navButtons.length > 0 && document.getElementById(back.section)) {
            showSection(back.section);
        }

        window.addEventListener('load', () => requestAnimationFrame(() => window.scrollTo({ top: back.y, behavior: 'instant' })));
        const done = document.querySelector('#editor .bp-editor-status.is-done span');
        if (done && saveStatus) {
            saveStatus.textContent = done.textContent;
            saveStatus.classList.add('is-done');
        }
    }

    form.addEventListener('input', showUnsaved);
    form.addEventListener('change', showUnsaved);
    form.addEventListener('click', (event) => {
        if (event.target.closest('[data-remove-row]')) {
            showUnsaved();
        }
    });
    showUnsaved();

    // Saving and "Discard changes" leave on purpose; anything else with unsaved edits asks first.
    form.addEventListener('submit', () => { leaving = true; });
    window.addEventListener('beforeunload', (event) => {
        if (!leaving && formState() !== pristine) {
            event.preventDefault();
            event.returnValue = '';
        }
    });

    // A dialog takes the keyboard focus while it is open, keeps Tab inside it, and gives the focus back when it closes.
    let returnFocus = null;
    function open(modal) {
        returnFocus = document.activeElement;
        modal.hidden = false;
        document.body.classList.add('bp-modal-open');
        modal.querySelector('.bp-modal-panel button')?.focus();
    }

    function close(modal) {
        modal.hidden = true;
        document.body.classList.remove('bp-modal-open');
        returnFocus?.focus?.();
        returnFocus = null;
    }

    document.addEventListener('keydown', (event) => {
        const modal = document.querySelector('.bp-modal:not([hidden])');
        if (event.key !== 'Tab' || !modal) {
            return;
        }

        const buttons = Array.from(modal.querySelectorAll('.bp-modal-panel button'));
        const first = buttons[0];
        const last = buttons[buttons.length - 1];
        if (!modal.contains(document.activeElement)) {
            event.preventDefault();
            first?.focus();
        } else if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    });

    // Uploading reloads the page: with unsaved edits, it asks first instead of losing them.
    const uploadModal = document.querySelector('[data-upload-guard-modal]');
    let uploadAnyway = null;
    document.querySelectorAll('[data-upload-form]').forEach((upload) => upload.addEventListener('submit', (event) => {
        if (uploadAnyway === upload || formState() === pristine || !uploadModal) {
            uploadAnyway = null; // a one-time pass: the next upload asks again
            leaving = true;
            return;
        }

        event.preventDefault();
        uploadModal.dataset.uploadForm = upload.id;
        open(uploadModal);
    }));
    uploadModal?.querySelector('[data-upload-stay]')?.addEventListener('click', () => close(uploadModal));
    uploadModal?.querySelector('[data-upload-dismiss]')?.addEventListener('click', () => close(uploadModal));
    uploadModal?.querySelector('[data-upload-discard]')?.addEventListener('click', () => {
        const upload = document.getElementById(uploadModal.dataset.uploadForm);
        close(uploadModal);
        // Only a form that can be sent passes the guard (e.g. "Publish new versions" must be ticked first).
        if (upload && upload.reportValidity()) {
            uploadAnyway = upload;
            upload.requestSubmit();
        }
    });

    const cancelButton = form.querySelector('[data-cancel-edits]');
    const cancelModal = document.querySelector('[data-cancel-modal]');
    const leave = () => {
        leaving = true;
        window.location.href = cancelButton.dataset.cancelHref;
    };
    cancelButton?.addEventListener('click', () => (formState() === pristine || !cancelModal ? leave() : open(cancelModal)));
    cancelModal?.querySelector('[data-cancel-stay]')?.addEventListener('click', () => close(cancelModal));
    cancelModal?.querySelector('[data-cancel-dismiss]')?.addEventListener('click', () => close(cancelModal));
    cancelModal?.querySelector('[data-cancel-discard]')?.addEventListener('click', leave);

    const confirmModal = form.querySelector('[data-confirm-modal]');
    if (confirmModal) {
        open(confirmModal);
        confirmModal.querySelector('[data-confirm-stay]')?.addEventListener('click', () => close(confirmModal));
    }

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') {
            return;
        }

        [cancelModal, confirmModal, uploadModal].filter((modal) => modal && !modal.hidden).forEach(close);
    });
})();
```

- [ ] **Step 14: Offer the new record types in the audit log filter**

Patch `2d1-task6-auditlog.patch` (`web/Views/Admin/AuditLog.cshtml`). Save the block as `../2d1-task6-auditlog.patch`, outside the worktree, and run `git apply ../2d1-task6-auditlog.patch` from the worktree root:

```diff
diff --git a/web/Views/Admin/AuditLog.cshtml b/web/Views/Admin/AuditLog.cshtml
--- a/web/Views/Admin/AuditLog.cshtml
+++ b/web/Views/Admin/AuditLog.cshtml
@@ -29,6 +29,13 @@
                     @Option("cluster", "Cluster", filter.EntityType)
                     @Option("apiToken", "API token", filter.EntityType)
                     @Option("account", "Account", filter.EntityType)
+                    @Option("dpp-family", "Battery family", filter.EntityType)
+                    @Option("dpp-model", "Battery model", filter.EntityType)
+                    @Option("dpp-batch", "Batch", filter.EntityType)
+                    @Option("dpp-site", "Manufacturing site", filter.EntityType)
+                    @Option("dpp-operator", "Economic operator", filter.EntityType)
+                    @Option("dpp-pack", "Battery pack", filter.EntityType)
+                    @Option("dpp-document", "Document", filter.EntityType)
                 </select>
             </label>
             <label>
```

- [ ] **Step 15: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.ProductDataControllerTests"`
Expected: `Passed!  - Failed:     0, Passed:    11`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1826`, none failed.

- [ ] **Step 16: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/ProductDataControllerTests.cs web/Controllers/ProductDataController.cs web/Views/ProductData/_Messages.cshtml web/Views/ProductData/_Completeness.cshtml web/Views/ProductData/_EditorField.cshtml web/Views/ProductData/_RecordEditor.cshtml web/Views/ProductData/Families.cshtml web/Views/ProductData/Family.cshtml web/Views/ProductData/Batch.cshtml web/Views/ProductData/SitesAndOperators.cshtml web/wwwroot/css/bp-product-data.css web/wwwroot/js/bp-record-editor.js BatteryPassWeb.Tests/Dpp/PageTesting.cs web/Views/Admin/AuditLog.cshtml
git commit -m "feat(dpp): the team's product data pages on the admin layout"
```


---

### Task 7: The menu, and the old family editor and "Editable fields" removed

The menu points at the new pages; the old family editor (`/admin/products/...`), "Editable fields" and what only they used go (decision 23). The old tests that covered them lose those parts.

**Files:**
- Test: `BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminHelpPageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`
- Test (modify): `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`
- Test (modify): `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`
- Test (modify): `BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs`
- Test (modify): `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`
- Test (modify): `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
- Test (modify): `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Program.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/Admin/Help.cshtml`
- Modify: `web/Views/Shared/_AdminTabs.cshtml`
- Delete: `web/Views/Admin/Product.cshtml`
- Delete: `web/Services/LocalAdminEditableFieldPolicyService.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs`:

```csharp
namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The admin menu after 2d-1: the new pages under the existing entries, no temporary or duplicate entries (decision 23).</summary>
public sealed class AdminMenuTests
{
    private static readonly string Menu = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));

    [Theory]
    [InlineData("/admin/families", "Battery families")]
    [InlineData("/admin/sites-and-operators", "Sites and operators")]
    public void NewPages_ShouldBeInBothTheTabsAndThePhoneMenu(string href, string label)
    {
        Assert.Contains($"href=\"{href}\">{label}</a>", Menu, StringComparison.Ordinal);
        Assert.Contains($"<option value=\"{href}\"", Menu, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovedPages_ShouldHaveNoEntry()
    {
        Assert.DoesNotContain("Editable fields", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("tab=local-editable-fields", Menu, StringComparison.Ordinal);
        Assert.DoesNotContain("tab=products", Menu, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLabel_ShouldAppearOnceInEachMenu()
    {
        foreach (var label in new[] { "Battery families", "Sites and operators", "Batteries", "Users" })
        {
            Assert.Equal(2, Menu.Split($">{label}<").Length - 1); // one tab, one phone-menu option
        }
    }
}
```

Patch `2d1-task7-tests.patch` (`BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs`, `BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs`, `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`, `BatteryPassWeb.Tests/AdminHelpPageTests.cs`, `BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs`, `BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs`, `BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs`, `BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs`, `BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs`, `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`, `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs`). Save the block as `../2d1-task7-tests.patch`, outside the worktree, and run `git apply ../2d1-task7-tests.patch` from the worktree root:

```diff
diff --git a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs
@@ -7,11 +7,11 @@ public sealed class AdminCredentialLayoutTests
     {
         var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var tokenTabStart = markup.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
-        var productsTabStart = markup.IndexOf("selectedTab == \"products\"", StringComparison.Ordinal);
+        var confirmModalStart = markup.IndexOf("<div class=\"bp-confirm-modal\" data-confirm-modal hidden>", StringComparison.Ordinal);
 
         Assert.True(tokenTabStart >= 0);
-        Assert.True(productsTabStart > tokenTabStart);
-        var tokenTab = markup.Substring(tokenTabStart, productsTabStart - tokenTabStart);
+        Assert.True(confirmModalStart > tokenTabStart);
+        var tokenTab = markup.Substring(tokenTabStart, confirmModalStart - tokenTabStart);
 
         Assert.Contains("bp-token-console", tokenTab);
         Assert.Contains("bp-token-console-summary", tokenTab);
diff --git a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
--- a/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
+++ b/BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs
@@ -155,7 +155,7 @@ public sealed class AdminDenseConsoleLayoutTests
         var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
         var usersTabStart = clusters.IndexOf("selectedTab == \"users\"", StringComparison.Ordinal);
-        var nextTabStart = clusters.IndexOf("selectedTab == \"local-editable-fields\"", StringComparison.Ordinal);
+        var nextTabStart = clusters.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
         var usersTab = clusters.Substring(usersTabStart, nextTabStart - usersTabStart);
 
         Assert.True(usersTabStart >= 0);
@@ -230,54 +230,6 @@ public sealed class AdminDenseConsoleLayoutTests
         Assert.Contains(".bp-edit-grid", css);
     }
 
-    [Fact]
-    public void AdminLocalEditableFields_ShouldUseConsolePolicyLayout()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-        var localEditableStart = clusters.IndexOf("selectedTab == \"local-editable-fields\"", StringComparison.Ordinal);
-        var apiTokenStart = clusters.IndexOf("selectedTab == \"api-token-management\"", StringComparison.Ordinal);
-        var localEditableTab = clusters.Substring(localEditableStart, apiTokenStart - localEditableStart);
-
-        Assert.True(localEditableStart >= 0);
-        Assert.True(apiTokenStart > localEditableStart);
-        Assert.Contains("Editable fields", localEditableTab);
-        Assert.Contains("name=\"editableAtCreationFieldKeys\"", localEditableTab);
-        Assert.Contains("name=\"editableAfterCreationFieldKeys\"", localEditableTab);
-        Assert.Contains("name=\"editableByLocalAdminFieldKeys\"", localEditableTab);
-        Assert.DoesNotContain("Local editable fields", localEditableTab);
-        Assert.Contains("bp-policy-console", clusters);
-        Assert.Contains("bp-local-editable-policy-table", localEditableTab);
-        Assert.Contains("bp-console-table", localEditableTab);
-        Assert.Contains("<th>Section</th>", localEditableTab);
-        Assert.Contains("<th>Total fields</th>", localEditableTab);
-        Assert.Contains("<th>Editable</th>", localEditableTab);
-        Assert.Contains("<th>After creation</th>", localEditableTab);
-        Assert.Contains("<th>Cluster admin</th>", localEditableTab);
-        Assert.Contains("<th>Examples</th>", localEditableTab);
-        Assert.Contains("bp-local-editable-drawer-row", localEditableTab);
-        Assert.Contains("data-local-editable-drawer-toggle", localEditableTab);
-        Assert.Contains("aria-expanded=\"false\"", localEditableTab);
-        Assert.Contains("class=\"bp-local-editable-drawer-row\" hidden", localEditableTab);
-        Assert.DoesNotContain("sectionIndex == 0 ? \"true\" : \"false\"", localEditableTab);
-        Assert.DoesNotContain("hidden=\"@(sectionIndex != 0)\"", localEditableTab);
-        Assert.Contains("policySection.Fields.Count", localEditableTab);
-        Assert.Contains("sectionCreationCount", localEditableTab);
-        Assert.Contains("sectionAfterCreationCount", localEditableTab);
-        Assert.Contains("sectionLocalAdminCount", localEditableTab);
-        Assert.Contains("bp-local-editable-field-toggle", localEditableTab);
-        Assert.DoesNotContain("name=\"editableFieldKeys\"", localEditableTab);
-        Assert.Contains("/admin/local-editable-fields/save", localEditableTab);
-        Assert.DoesNotContain("bp-policy-section-grid", localEditableTab);
-        Assert.DoesNotContain("bp-requirement-grid", localEditableTab);
-        Assert.Contains("bp-policy-meta", clusters);
-        Assert.Contains(".bp-policy-console", css);
-        Assert.Contains(".bp-local-editable-policy-table", css);
-        Assert.Contains(".bp-local-editable-drawer-row", css);
-        Assert.Contains(".bp-local-editable-field-grid", css);
-        Assert.Contains(".bp-local-editable-field-toggle", css);
-    }
-
     [Fact]
     public void AdminHelp_ShouldUsePersistentAdminTabsAndLighterWorkflowIntro()
     {
diff --git a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs
@@ -5,18 +5,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class AdminFeedbackFollowupTests
 {
-    [Fact]
-    public void BatteryFamilyResetButtons_ShouldUseBatteryFamilyCopy()
-    {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
-
-        Assert.Contains("Reset battery-family passports", clusters);
-        Assert.Contains("Reset battery-family passports", help);
-        Assert.DoesNotContain("Reset template passports", clusters);
-        Assert.DoesNotContain("Reset battery family passports", help);
-    }
-
     [Fact]
     public void PassportReportHero_ShouldNotShowPassportStatusAbovePassportId()
     {
diff --git a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
--- a/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
+++ b/BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs
@@ -49,17 +49,12 @@ public sealed class AdminFeedbackImplementationTests
     public void EditableFields_ShouldSeparateClusterAdminVisibilityFromEditability()
     {
         var policy = File.ReadAllText(RepoFile("web", "Services", "EditableFieldPolicyService.cs"));
-        var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
-        var adminView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var sharedEditView = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var editModel = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "EditPassportViewModel.cs"));
 
         Assert.Contains("VisibleToClusterAdmin", policy);
         Assert.Contains("VisibleToClusterAdminCount", policy);
-        Assert.Contains("visibleToClusterAdminFieldKeys", adminController);
-        Assert.Contains("visibleToClusterAdminFieldKeys", adminView);
-        Assert.Contains("Show for Cluster Admin", adminView);
         Assert.Contains("EditableFieldPolicyService", clusterAdminController);
         Assert.Contains("FieldVisibleByKey", editModel);
         Assert.Contains("data-visible-field-policy", sharedEditView);
diff --git a/BatteryPassWeb.Tests/AdminHelpPageTests.cs b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
--- a/BatteryPassWeb.Tests/AdminHelpPageTests.cs
+++ b/BatteryPassWeb.Tests/AdminHelpPageTests.cs
@@ -40,7 +40,6 @@ public sealed class AdminHelpPageTests
         Assert.Contains("Administration tabs", tabs);
         Assert.Contains("bp-tab-active", tabs);
         Assert.Contains("href=\"@($\"/admin/clusters?tab=batteries{batteryQuerySuffix}\")\"", tabs);
-        Assert.Contains("href=\"/admin/clusters?tab=local-editable-fields\"", tabs);
         Assert.Contains("href=\"/admin/help\"", tabs);
         Assert.DoesNotContain("Back to main admin page", markup);
         Assert.DoesNotContain("bp-page-return-row", markup);
@@ -70,7 +69,6 @@ public sealed class AdminHelpPageTests
         Assert.Contains("published, signed, clean, and verifiable", markup);
         Assert.Contains("Draft, dirty, unsigned, or unpublished passports stay visible to admins", markup);
         Assert.Contains("Battery families tab", markup);
-        Assert.Contains("required/optional switches", markup);
         Assert.Contains("saved product policy", markup);
         Assert.Contains("Trust &amp; conformance tab is visible only", markup);
         Assert.Contains("general admins or local cluster admins", markup);
@@ -82,7 +80,7 @@ public sealed class AdminHelpPageTests
         var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
 
         Assert.Contains("Open battery families", markup);
-        Assert.Contains("/admin/clusters?tab=products", markup);
+        Assert.Contains("/admin/families", markup);
         Assert.DoesNotContain("Parameter-by-parameter fill list", markup);
         Assert.DoesNotContain("bp-admin-help-parameter-grid", markup);
         Assert.DoesNotContain("bp-admin-help-parameter-list", markup);
@@ -96,7 +94,6 @@ public sealed class AdminHelpPageTests
 
         Assert.Contains("First-time passport checklist", markup);
         Assert.Contains("Dirty recovery checklist", markup);
-        Assert.Contains("Battery Model push", markup);
         Assert.Contains("What public users can see", markup);
         Assert.Contains("What admins can see", markup);
         Assert.Contains("Open battery families", markup);
@@ -173,23 +170,6 @@ public sealed class AdminHelpPageTests
         Assert.Contains(".bp-admin-help-status-table", css);
     }
 
-    [Fact]
-    public void AdminHelpView_ShouldKeepResetInMaintenanceDisclosureAfterPrimaryGuide()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("bp-admin-help-maintenance-reset", markup);
-        Assert.Contains("<summary>", markup);
-        Assert.Contains("Maintenance", markup);
-        Assert.True(
-            markup.IndexOf("bp-admin-help-maintenance-reset", StringComparison.Ordinal) >
-            markup.IndexOf("id=\"ready-checklist\"", StringComparison.Ordinal),
-            "The reset action should be available, but it should not dominate the first help screen.");
-
-        Assert.Contains(".bp-admin-help-maintenance-reset", css);
-    }
-
     [Fact]
     public void AdminPages_ShouldLinkToAdminHelp()
     {
diff --git a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
--- a/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
+++ b/BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs
@@ -91,16 +91,12 @@ public sealed class AdminTestFeedbackFixesTests
     public void ProductEditor_ShouldManagePerModelSoftwareVersions()
     {
         var models = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
-        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
         var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var updateService = File.ReadAllText(RepoFile("web", "Services", "BatteryTemplateUpdateService.cs"));
 
         Assert.Contains("BatteryProductSoftwareVersion", models);
         Assert.Contains("IReadOnlyList<BatteryProductSoftwareVersion> SoftwareVersions", models);
         Assert.Contains("HighestSoftwareVersion", models);
-        Assert.Contains("data-add-software-version", productView);
-        Assert.Contains("data-remove-software-version", productView);
-        Assert.Contains("softwareVersionRows", productView);
         Assert.Contains("data-product-software-version-select", edit);
         Assert.Contains("selectedVersion.SoftwareVersions", updateService);
     }
diff --git a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
--- a/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
+++ b/BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs
@@ -162,7 +162,6 @@ public sealed class ApplicationAuditCoverageTests
         Assert.Contains("\"cluster.user.saved\"", admin);
         Assert.Contains("\"apiToken.created\"", admin);
         Assert.Contains("\"apiToken.regenerated\"", admin);
-        Assert.Contains("\"editablePolicy.updated\"", admin);
         Assert.Contains("\"apiToken.created\"", clusterAdmin);
         Assert.Contains("\"cluster.user.saved\"", clusterAdmin);
         Assert.Contains("\"security.passwordReset.requested\"", login);
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -40,8 +40,7 @@ public sealed class BatteryFamilyAccessApiRevisedTests
             RepoFile("web", "Views", "Passport", "Summary.cshtml"),
             RepoFile("web", "Views", "Passport", "Detail.cshtml"),
             RepoFile("web", "Views", "Admin", "EditPassport.cshtml"),
-            RepoFile("web", "Views", "Admin", "Clusters.cshtml"),
-            RepoFile("web", "Views", "Admin", "Product.cshtml")
+            RepoFile("web", "Views", "Admin", "Clusters.cshtml")
         };
 
         var text = string.Join(Environment.NewLine, files.Select(File.ReadAllText));
@@ -64,7 +63,6 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         var modelSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateModels.cs"));
         var serviceSource = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));
         var apiSource = File.ReadAllText(RepoFile("web", "Controllers", "ExternalApiController.cs"));
-        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
 
         Assert.Contains("string SoftwareVersion", modelSource);
         Assert.Contains("string SoftwareReleaseDate", modelSource);
@@ -73,9 +71,6 @@ public sealed class BatteryFamilyAccessApiRevisedTests
         Assert.Contains("record BatteryProductSoftwareVersion", modelSource);
         Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", serviceSource);
         Assert.DoesNotContain("[HttpPatch(\"batteries/{passportId}/software\")]", apiSource);
-        Assert.Contains("data-software-version-row", productView);
-        Assert.Contains("data-add-software-version", productView);
-        Assert.Contains("Add software version", productView);
     }
 
     [Fact]
diff --git a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
--- a/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
+++ b/BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs
@@ -17,7 +17,6 @@ public sealed class DataRequirementsAdminLayoutTests
 
         Assert.Contains("DataCompletionPolicyService", source);
         Assert.Contains("GetPolicyForPassportAsync", source);
-        Assert.Contains("GetProductPolicyAsync", source);
         Assert.DoesNotContain("[HttpPost(\"data-requirements/save\")]", source);
         Assert.DoesNotContain("SaveDataRequirements", source);
     }
@@ -30,9 +29,7 @@ public sealed class DataRequirementsAdminLayoutTests
         var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "AdminClusterViewModel.cs"));
         var adminClusterModel = model[..model.IndexOf("public sealed class ProductTemplateSummaryViewModel", StringComparison.Ordinal)];
 
-        Assert.Contains("Battery families", markup);
-        Assert.Contains("tab=products", tabs);
-        Assert.Contains("Required fields", markup);
+        Assert.Contains("href=\"/admin/families\"", tabs);
         Assert.DoesNotContain("tab=data-requirements", markup);
         Assert.DoesNotContain("tab=data-requirements", tabs);
         Assert.DoesNotContain("selectedTab == \"data-requirements\"", markup);
@@ -45,11 +42,6 @@ public sealed class DataRequirementsAdminLayoutTests
     [Fact]
     public void ProductTemplateEditor_ShouldRemainRequirementSourceOfTruth()
     {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
-
-        Assert.Contains("Template completion policy", markup);
-        Assert.Contains("Required and optional parameters", markup);
-        Assert.Contains("name=\"requiredFieldKeys\"", markup);
         Assert.Contains("Battery families tab", File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml")));
     }
 
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
--- a/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs
@@ -67,28 +67,6 @@ public sealed class ExternalApiBatteryVersionTests
         Assert.Contains("Battery ID, family, and software parameters", markup);
     }
 
-    [Fact]
-    public void ProductTemplateEditor_ShouldShowPerModelSoftwareVersionRows()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
-
-        Assert.Contains("data-software-version-list", markup);
-        Assert.Contains("name=\"softwareVersion\"", markup);
-        Assert.Contains("name=\"softwareReleaseDate\"", markup);
-        Assert.Contains("name=\"softwareLatestUpdate\"", markup);
-        Assert.Contains("data-add-software-version", markup);
-        Assert.Contains("data-remove-software-version", markup);
-    }
-
-    [Fact]
-    public void ProductTemplateEditor_ShouldUseCleanImageLabelsWithoutRepeatedCategory()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
-
-        Assert.Contains("@imageOption.Label</option>", markup);
-        Assert.DoesNotContain("@imageOption.Label (@imageOption.Category)", markup);
-    }
-
     [Fact]
     public void PassportViewModelFactory_ShouldExposeSoftwareVersionAsBatteryVersionParameter()
     {
diff --git a/BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs b/BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs
--- a/BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs
+++ b/BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs
@@ -56,30 +56,6 @@ public sealed class LocalAdminEditableFieldPolicyTests
         Assert.False(cluster.EditableByLocalAdmin);
     }
 
-    [Fact]
-    public void CreateDefaultPolicy_ShouldAllowOnlyOperationalAndLocalIdentityFields()
-    {
-        var snapshot = LocalAdminEditableFieldPolicyService.CreateDefaultPolicy();
-
-        Assert.Contains("general.facilityId", snapshot.EditableFieldKeys);
-        Assert.Contains("general.batteryImageUrl", snapshot.EditableFieldKeys);
-        Assert.Contains("performance.stateOfCharge", snapshot.EditableFieldKeys);
-        Assert.Contains("performance.remainingCapacity", snapshot.EditableFieldKeys);
-        Assert.Contains("performance.remainingEnergy", snapshot.EditableFieldKeys);
-        Assert.Contains("performance.fullCycles", snapshot.EditableFieldKeys);
-        Assert.DoesNotContain("general.modelNumber", snapshot.EditableFieldKeys);
-    }
-
-    [Fact]
-    public void Service_ShouldPersistGlobalPolicyInMongo()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Services", "LocalAdminEditableFieldPolicyService.cs"));
-
-        Assert.Contains("localAdminEditableFieldPolicies", source);
-        Assert.Contains("editableFieldKeys", source);
-        Assert.Contains("DataCompletionPolicyService.CreateDefaultPolicy", source);
-    }
-
     private static string RepoFile(params string[] parts)
     {
         var directory = new DirectoryInfo(AppContext.BaseDirectory);
diff --git a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
--- a/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
+++ b/BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs
@@ -43,10 +43,8 @@ public sealed class MobileThemeSessionLayoutTests
         Assert.Contains("data-admin-mobile-tab-select", adminTabs);
         Assert.Contains("bp-admin-cluster-assignment-table bp-mobile-card-table", clusters);
         Assert.Contains("bp-cluster-management-table bp-mobile-card-table", clusters);
-        Assert.Contains("bp-local-editable-policy-table bp-mobile-card-table", clusters);
         Assert.Contains("data-label=\"Assign\"", clusters);
         Assert.Contains("data-label=\"Cluster ID\"", clusters);
-        Assert.Contains("data-label=\"Examples\"", clusters);
         Assert.Contains("bp-admin-help-workflow-table bp-mobile-card-table", adminHelp);
         Assert.Contains("bp-admin-help-status-table bp-mobile-card-table", adminHelp);
         Assert.Contains("bp-api-help-endpoint-table bp-mobile-card-table", apiHelp);
diff --git a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
--- a/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
+++ b/BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
@@ -2,30 +2,6 @@ namespace BatteryPassWeb.Tests;
 
 public sealed class Phase6ADocumentationTests
 {
-    [Fact]
-    public void AdminController_ShouldExposeAdminOnlyProductTemplateReset()
-    {
-        var source = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-
-        Assert.Contains("ProductTemplateService", source);
-        Assert.Contains("[HttpPost(\"product-templates/reset\")]", source);
-        Assert.Contains("ResetProductTemplateDemo", source);
-        Assert.Contains("_productTemplateService.ResetTemplateDemoAsync", source);
-        Assert.Contains("Product template demo reset completed", source);
-    }
-
-    [Fact]
-    public void AdminHelp_ShouldShowCompactProductTemplateResetAction()
-    {
-        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("Battery family reset", markup);
-        Assert.Contains("/admin/product-templates/reset", markup);
-        Assert.Contains("bp-admin-help-demo-reset", markup);
-        Assert.Contains(".bp-admin-help-demo-reset", css);
-    }
-
     [Fact]
     public void SampleAccounts_ShouldDocumentEveryProductTemplatePassport()
     {
@@ -80,7 +56,6 @@ public sealed class Phase6ADocumentationTests
     public void Guidance_ShouldMatchCurrentProductTemplateSoftwareAndUiWorkflow()
     {
         var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
-        var adminHelp = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
         var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
 
         Assert.Contains("software parameters", apiHelp);
@@ -88,11 +63,6 @@ public sealed class Phase6ADocumentationTests
         Assert.Contains("software-version endpoint", apiHelp);
         Assert.Contains("Battery Model and software changes may require a new passport", apiHelp);
 
-        Assert.Contains("Software parameters", adminHelp);
-        Assert.Contains("Detailed report General tab", adminHelp);
-        Assert.Contains("Battery Model and software API changes require a new passport", adminHelp);
-        Assert.Contains("release date and latest update values", adminHelp);
-
         Assert.Contains("software parameters", guide);
         Assert.Contains("Detailed report tabs", guide);
         Assert.Contains("software version parameter", guide);
@@ -104,7 +74,6 @@ public sealed class Phase6ADocumentationTests
     public void Documentation_ShouldExplainVersionedTemplatesCredentialsAndResetAccounts()
     {
         var apiHelp = File.ReadAllText(RepoFile("web", "Views", "Help", "Index.cshtml"));
-        var adminHelp = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
         var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
         var qa = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));
         var accounts = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));
@@ -120,7 +89,6 @@ public sealed class Phase6ADocumentationTests
         Assert.Contains("CP7M-NORTH-002", accounts);
         Assert.Contains("CP13M-SOUTH-002", accounts);
         Assert.Contains("CORE-FLEET-002", accounts);
-        Assert.Contains("Local editable fields", adminHelp);
         Assert.Contains("Token Value", apiHelp);
         Assert.Contains("Passport Lifecycle token", apiHelp);
         Assert.DoesNotContain("create, validate, sign and publish passport token", apiHelp);
diff --git a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
--- a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
@@ -19,14 +19,10 @@ public sealed class ProductTemplateWorkflowLayoutTests
         var dataCompletionSource = File.ReadAllText(RepoFile("web", "Services", "DataCompletionPolicyService.cs"));
 
         Assert.Contains("ProductTemplateService", source);
-        Assert.Contains("[HttpGet(\"products/{productId}\")]", source);
-        Assert.Contains("[HttpPost(\"products/save\")]", source);
-        Assert.Contains("[HttpPost(\"products/{productId}/versions/{productVersion}/push\")]", source);
         Assert.Contains("GetPolicyForPassportAsync", source);
         Assert.Contains("batteryProductTemplateVersions", service);
         Assert.DoesNotContain("batteryProductTemplateSoftwareVersions", service);
         Assert.Contains("productVersion", service);
-        Assert.Contains("PushProductVersionTemplate", source);
         Assert.DoesNotContain("PushTemplateAsync(string productId, string productVersion, string softwareVersion", service);
         Assert.Contains("GetProductVersionPolicyAsync", dataCompletionSource);
         Assert.DoesNotContain("[HttpPost(\"passports/{passportId}/complete-required-data\")]", source);
@@ -40,9 +36,8 @@ public sealed class ProductTemplateWorkflowLayoutTests
         var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var help = File.ReadAllText(RepoFile("web", "Views", "Admin", "Help.cshtml"));
 
-        Assert.Contains("tab=products", tabs);
+        Assert.Contains("href=\"/admin/families\"", tabs);
         Assert.Contains("_AdminTabs", clusters);
-        Assert.Contains("Battery families", clusters);
         Assert.Contains("name=\"productId\"", edit);
         Assert.Contains("name=\"softwareVersion\"", edit);
         Assert.Contains("data-product-template-select", edit);
@@ -53,11 +48,9 @@ public sealed class ProductTemplateWorkflowLayoutTests
     [Fact]
     public void AdminProductTemplateUi_ShouldUseCalmerMinimalSurfaceStyling()
     {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
         var edit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("bp-product-template-actions", clusters);
         Assert.Contains("bp-template-sync-note", edit);
         Assert.DoesNotContain("Template sync", edit);
         Assert.DoesNotContain("Overrides:", edit);
@@ -71,55 +64,9 @@ public sealed class ProductTemplateWorkflowLayoutTests
     [Fact]
     public void ProductEditor_ShouldExposeProductVersionsAndBaseTemplateControls()
     {
-        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
         var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
         var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
 
-        Assert.Contains("name=\"productVersion\"", productView);
-        Assert.Contains("data-product-version-list", productView);
-        Assert.Contains("data-product-version-row", productView);
-        Assert.Contains("data-product-version-tab-rail", productView);
-        Assert.Contains("bp-product-editor-workspace", productView);
-        Assert.Contains("bp-product-editor-content", productView);
-        Assert.Contains("data-product-version-tab", productView);
-        Assert.Contains("data-selected-product-version-label", productView);
-        Assert.Contains("data-version-scoped-section", productView);
-        Assert.Contains("data-template-document-list", productView);
-        Assert.Contains("TemplateDocuments", productView);
-        Assert.Contains("RequiredFieldKeys", productView);
-        Assert.Contains("collectRequiredFieldKeys", productView);
-        Assert.Contains("applyRequiredFieldKeys", productView);
-        Assert.Contains("data-add-product-version", productView);
-        Assert.Contains("data-edit-product-version", productView);
-        Assert.Contains("data-remove-product-version", productView);
-        Assert.Contains("row.addEventListener('click'", productView);
-        Assert.Contains("const versionInput = event.target.closest('[data-product-version-input]');", productView);
-        Assert.Contains("if (versionInput && !versionInput.readOnly)", productView);
-        Assert.Contains("selectProductVersion(version)", productView);
-        Assert.Contains("const productId = @Html.Raw(JsonSerializer.Serialize(Model.ProductId));", productView);
-        Assert.Contains("const isNewProduct = @Html.Raw(JsonSerializer.Serialize(isNew));", productView);
-        Assert.DoesNotContain("const productId = @JsonSerializer.Serialize(Model.ProductId);", productView);
-        Assert.Contains("name=\"productVersionsJson\"", productView);
-        Assert.Contains("data-base-product-select", productView);
-        Assert.Contains("data-base-product-version-select", productView);
-        Assert.Contains("data-software-version-list", productView);
-        Assert.Contains("name=\"softwareVersion\"", productView);
-        Assert.Contains("name=\"softwareReleaseDate\"", productView);
-        Assert.Contains("name=\"softwareLatestUpdate\"", productView);
-        Assert.DoesNotContain("data-base-software-select", productView);
-        Assert.Contains("bp-product-version-console", productView);
-        Assert.Contains("bp-product-version-chip-rail", productView);
-        Assert.Contains("bp-product-version-selected-panel", productView);
-        Assert.Contains("data-selected-product-version-input", productView);
-        Assert.Contains("bp-product-version-selected-field", productView);
-        Assert.Contains("bp-product-version-selected-meta", productView);
-        Assert.Contains("bp-product-version-selected-actions", productView);
-        Assert.Contains("data-push-selected-product-version", productView);
-        Assert.Contains("data-remove-selected-product-version", productView);
-        Assert.Contains("updateSelectedProductVersionControls", productView);
-        Assert.DoesNotContain("bp-product-version-current-pill", productView);
-        Assert.DoesNotContain("bp-product-version-tab-actions", productView);
-        Assert.DoesNotContain("bp-product-version-sidebar", productView);
         Assert.Contains(".bp-product-version-console", css);
         Assert.Contains(".bp-product-version-chip-rail", css);
         Assert.Contains(".bp-product-version-selected-panel", css);
@@ -130,54 +77,20 @@ public sealed class ProductTemplateWorkflowLayoutTests
         Assert.DoesNotContain(".bp-product-version-sidebar", css);
         Assert.Contains(".bp-product-version-tab.is-saved-version .bp-product-version-tab-input", css);
         Assert.Contains("overflow: hidden;", css);
-        Assert.DoesNotContain("Material composition for <span", productView);
-        Assert.DoesNotContain("Carbon lifecycle stages for <span", productView);
-        Assert.DoesNotContain("Circularity for <span", productView);
-        Assert.Contains("products/{productId}/versions/{productVersion}/push", adminController);
         Assert.DoesNotContain("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
-        Assert.Contains("ProductTemplateDocumentFormPayload", adminController);
-        Assert.DoesNotContain("does not create three default software versions", productView);
     }
 
     [Fact]
     public void AdminAndClusterAdmin_ShouldExposeEditableFieldPolicy()
     {
-        var clusters = File.ReadAllText(RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
-        var tabs = File.ReadAllText(RepoFile("web", "Views", "Shared", "_AdminTabs.cshtml"));
         var sharedEdit = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
         var adminController = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
-        var clusterAdminController = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
         var program = File.ReadAllText(RepoFile("web", "Program.cs"));
 
-        Assert.Contains("tab=local-editable-fields", tabs);
-        Assert.Contains("Editable fields", clusters);
-        Assert.Contains("editableAtCreationFieldKeys", clusters);
-        Assert.Contains("editableAfterCreationFieldKeys", clusters);
-        Assert.Contains("editableByLocalAdminFieldKeys", clusters);
-        Assert.Contains("/admin/local-editable-fields/save", clusters);
         Assert.Contains("FieldEditableByKey", sharedEdit);
         Assert.Contains("FieldVisibleByKey", sharedEdit);
         Assert.Contains("EditableFieldPolicyService", adminController);
-        Assert.Contains("LocalAdminEditableFieldPolicyService", adminController);
-        Assert.Contains("LocalAdminEditableFieldPolicyService", clusterAdminController);
         Assert.Contains("AddSingleton<EditableFieldPolicyService>", program);
-        Assert.Contains("AddSingleton<LocalAdminEditableFieldPolicyService>", program);
-    }
-
-    [Fact]
-    public void FollowupProductEditor_ShouldUsePendingSaveSoftwareVersionRows()
-    {
-        var productView = File.ReadAllText(RepoFile("web", "Views", "Admin", "Product.cshtml"));
-        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));
-
-        Assert.Contains("data-software-version-pending", productView);
-        Assert.Contains("data-save-software-version", productView);
-        Assert.Contains("incrementSoftwareVersion", productView);
-        Assert.Contains("new Date().toISOString().slice(0, 10)", productView);
-        Assert.Contains("data-remove-software-version", productView);
-        Assert.Contains("readonly", productView);
-        Assert.Contains(".bp-template-version-row", css);
-        Assert.Contains("grid-template-columns", css);
     }
 
     [Fact]
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AdminMenuTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryPassWeb.Tests.LocalAdminEditableFieldPolicyTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateWorkflowLayoutTests"`
Expected: `Failed!  - Failed:     7, Passed:   110`. The four menu test cases fail, because the menu still has "Editable fields" and the old `tab=products` link, and no "Sites and operators". Three updated old tests fail because they now expect the old pages gone: `AdminHelpPageTests.AdminHelpView_ShouldPointToProductTemplatesInsteadOfDuplicatingParameterList`, `DataRequirementsAdminLayoutTests.AdminWorkspace_ShouldNotRenderGlobalDataRequirementsTab` and `ProductTemplateWorkflowLayoutTests.AdminPages_ShouldExposeProductsTabAndTemplateBasedNewPassportFlow`.

- [ ] **Step 3: Remove the old pages and point the menu at the new ones**

Patch `2d1-task7-removal.patch` (`web/Controllers/AdminController.cs`, `web/Controllers/ClusterAdminController.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Program.cs`, `web/Views/Admin/Clusters.cshtml`, `web/Views/Admin/Help.cshtml`, `web/Views/Shared/_AdminTabs.cshtml`). Save the block as `../2d1-task7-removal.patch`, outside the worktree, and run `git apply ../2d1-task7-removal.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -117,7 +117,6 @@ public class AdminController : Controller
     private readonly PassportEvidenceService _passportEvidenceService;
     private readonly DataCompletionPolicyService _dataCompletionPolicyService;
     private readonly EditableFieldPolicyService _editableFieldPolicyService;
-    private readonly LocalAdminEditableFieldPolicyService _localAdminEditableFieldPolicyService;
     private readonly ProductTemplateService _productTemplateService;
     private readonly PassportTrustService _passportTrustService;
     private readonly AuditRevisionService _auditRevisionService;
@@ -144,7 +143,6 @@ public class AdminController : Controller
         PassportEvidenceService passportEvidenceService,
         DataCompletionPolicyService dataCompletionPolicyService,
         EditableFieldPolicyService editableFieldPolicyService,
-        LocalAdminEditableFieldPolicyService localAdminEditableFieldPolicyService,
         ProductTemplateService productTemplateService,
         PassportTrustService passportTrustService,
         AuditRevisionService auditRevisionService,
@@ -170,7 +168,6 @@ public class AdminController : Controller
         _passportEvidenceService = passportEvidenceService;
         _dataCompletionPolicyService = dataCompletionPolicyService;
         _editableFieldPolicyService = editableFieldPolicyService;
-        _localAdminEditableFieldPolicyService = localAdminEditableFieldPolicyService;
         _productTemplateService = productTemplateService;
         _passportTrustService = passportTrustService;
         _auditRevisionService = auditRevisionService;
@@ -967,127 +964,23 @@ public class AdminController : Controller
         });
     }
 
-    [HttpGet("products/new")]
-    public async Task<IActionResult> NewProduct([FromQuery] string? error, CancellationToken cancellationToken)
-    {
-        await _productTemplateService.EnsureDefaultTemplatesAsync(CurrentActor(), cancellationToken);
-        var starter = BatteryProductTemplateCatalog.DefaultProduct with
-        {
-            ProductId = string.Empty,
-            ProductName = string.Empty,
-            Description = string.Empty
-        };
-        var dataRequirements = DataCompletionPolicyService.CreateDefaultPolicy();
-        return View("Product", BuildProductEditModel(starter, dataRequirements, string.Empty, error));
-    }
-
-    [HttpGet("products/{productId}")]
-    public async Task<IActionResult> Product(string productId, [FromQuery] string? status, [FromQuery] string? error, CancellationToken cancellationToken)
-    {
-        await _productTemplateService.EnsureDefaultTemplatesAsync(CurrentActor(), cancellationToken);
-        var product = await _productTemplateService.GetProductAsync(productId, cancellationToken);
-        if (product == null)
-        {
-            return NotFound();
-        }
-
-        var dataRequirements = await _dataCompletionPolicyService.GetProductPolicyAsync(product.ProductId, cancellationToken);
-        return View("Product", BuildProductEditModel(product, dataRequirements, status, error));
-    }
-
-    [HttpPost("products/save")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> SaveProduct(CancellationToken cancellationToken)
+    [HttpGet("clusters")]
+    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
     {
-        var form = Request.Form;
-        var productId = Text(form, "productId").Trim().ToLowerInvariant();
-        if (string.IsNullOrWhiteSpace(productId))
+        if (tab?.Equals("passports", StringComparison.OrdinalIgnoreCase) == true)
         {
-            return Redirect($"/admin/products/new?error={Uri.EscapeDataString("Product ID is required.")}");
+            return Redirect(AdminBatteriesUrl(q));
         }
 
-        var existing = await _productTemplateService.GetProductAsync(productId, cancellationToken)
-            ?? BatteryProductTemplateCatalog.FindProduct(productId)
-            ?? (BatteryProductTemplateCatalog.DefaultProduct with { ProductId = productId });
-        var product = BuildProductTemplateFromForm(form, existing);
-        await _productTemplateService.SaveProductAsync(product, CurrentActor(), cancellationToken);
-        await AppendAdminAuditAsync(
-            "product.saved",
-            "Battery family template saved.",
-            new BsonDocument
-            {
-                ["productId"] = product.ProductId,
-                ["productName"] = product.ProductName,
-                ["versionCount"] = product.ProductVersions.Count
-            },
-            "product",
-            product.ProductId,
-            cancellationToken);
-        return Redirect($"/admin/products/{Uri.EscapeDataString(product.ProductId)}?status={Uri.EscapeDataString("Battery family saved. Push a Battery Model when you want matching batteries to receive safe template changes.")}");
-    }
-
-    [HttpPost("products/{productId}/versions/{productVersion}/push")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> PushProductVersionTemplate(string productId, string productVersion, CancellationToken cancellationToken)
-    {
-        var result = await _productTemplateService.PushProductVersionAsync(productId, productVersion, CurrentActor(), cancellationToken);
-        await AppendAdminAuditAsync(
-            "product.version.pushed",
-            "Battery Model template pushed to matching batteries.",
-            new BsonDocument
-            {
-                ["productId"] = productId,
-                ["productVersion"] = productVersion,
-                ["matchedBatteries"] = result.MatchedBatteries,
-                ["updatedBatteries"] = result.UpdatedBatteries,
-                ["skippedBatteries"] = result.SkippedBatteries
-            },
-            "product",
-            productId,
-            cancellationToken);
-        var message = $"Battery Model push finished: {result.UpdatedBatteries} of {result.MatchedBatteries} matching batteries updated. Manual overrides were preserved.";
-        return Redirect($"/admin/products/{Uri.EscapeDataString(productId)}?status={Uri.EscapeDataString(message)}");
-    }
-
-    [HttpPost("product-templates/reset")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> ResetProductTemplateDemo(CancellationToken cancellationToken)
-    {
-        try
-        {
-            var result = await _productTemplateService.ResetTemplateDemoAsync(CurrentActor(), cancellationToken);
-            await AppendAdminAuditAsync(
-                "product.demoReset",
-                "Product template demo data reset.",
-                new BsonDocument
-                {
-                    ["batteryCount"] = result.BatteryCount,
-                    ["passportCount"] = result.PassportCount
-                },
-                "product",
-                "demo-reset",
-                cancellationToken);
-            TempData["StatusMessage"] = $"Product template demo reset completed: seeded {result.BatteryCount} batteries and {result.PassportCount} passport snapshots from MongoDB product templates.";
-            return Redirect("/admin/clusters?tab=products");
-        }
-        catch (Exception exception) when (IsTrustPersistenceFailure(exception))
+        // The battery families moved to their own pages, and the attribute catalogue replaced the editable-fields tab.
+        if (tab?.Equals("products", StringComparison.OrdinalIgnoreCase) == true)
         {
-            TempData["ErrorMessage"] = $"{TrustWorkflowServiceErrorMessage} {exception.Message}";
-            return Redirect("/admin/clusters?tab=products");
+            return Redirect("/admin/families");
         }
-        catch (InvalidOperationException exception)
-        {
-            TempData["ErrorMessage"] = exception.Message;
-            return Redirect("/admin/clusters?tab=products");
-        }
-    }
 
-    [HttpGet("clusters")]
-    public async Task<IActionResult> Clusters([FromQuery] string? tab, [FromQuery] string? q, CancellationToken cancellationToken)
-    {
-        if (tab?.Equals("passports", StringComparison.OrdinalIgnoreCase) == true)
+        if (tab?.Equals("local-editable-fields", StringComparison.OrdinalIgnoreCase) == true)
         {
-            return Redirect(AdminBatteriesUrl(q));
+            return Redirect("/admin/clusters");
         }
 
         var selectedTab = NormalizeTab(tab);
@@ -1110,8 +1003,6 @@ public class AdminController : Controller
         var needsPassports = selectedTab is "battery" or "api-token-management";
         var needsUsers = selectedTab is "users" or "clusters";
         var needsCredentials = selectedTab == "api-token-management";
-        var needsProducts = selectedTab == "products";
-        var needsLocalEditablePolicy = selectedTab == "local-editable-fields";
         var batteryTable = needsBatteries
             ? await _batteryTableService.BuildAsync(User, BatteryTableScope.AdminBatteries, q, cancellationToken)
             : new BatteryTablePageViewModel();
@@ -1132,12 +1023,6 @@ public class AdminController : Controller
         IReadOnlyList<BsonDocument> apiTokens = needsCredentials
             ? await _externalApiRepository.ListTokensAsync(cancellationToken)
             : Array.Empty<BsonDocument>();
-        IReadOnlyList<BatteryProductTemplate> productTemplates = needsProducts
-            ? await _productTemplateService.ListProductsAsync(cancellationToken)
-            : Array.Empty<BatteryProductTemplate>();
-        var localEditableFieldPolicy = needsLocalEditablePolicy
-            ? await _editableFieldPolicyService.GetPolicyAsync(cancellationToken)
-            : EditableFieldPolicyService.CreateDefaultPolicy();
 
         var model = new AdminClusterViewModel
         {
@@ -1214,8 +1099,6 @@ public class AdminController : Controller
                     LastUsedAt = BsonHelpers.GetString(token, "lastUsedAt")
                 };
             }).OrderBy(token => token.Name, StringComparer.OrdinalIgnoreCase).ToList(),
-            ProductTemplates = BuildProductTemplateSummaries(productTemplates),
-            LocalEditableFieldPolicy = localEditableFieldPolicy,
             SamplePassportId = ExternalApiInitializer.SamplePassportId,
             SampleReadToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadTokenId, ExternalApiInitializer.SampleReadTokenValue, cancellationToken),
             SampleReadWriteToken = await ResolveTokenValueAsync(ExternalApiInitializer.SampleReadWriteTokenId, ExternalApiInitializer.SampleReadWriteTokenValue, cancellationToken),
@@ -1814,53 +1697,6 @@ public class AdminController : Controller
         return Redirect("/admin/clusters?tab=api-token-management");
     }
 
-    [HttpPost("local-editable-fields/save")]
-    [ValidateAntiForgeryToken]
-    public async Task<IActionResult> SaveLocalEditableFields(CancellationToken cancellationToken)
-    {
-        var creationKeys = FormKeys("editableAtCreationFieldKeys");
-        var afterCreationKeys = FormKeys("editableAfterCreationFieldKeys");
-        var visibleKeys = FormKeys("visibleToClusterAdminFieldKeys");
-        var localAdminKeys = FormKeys("editableByLocalAdminFieldKeys");
-        var allKeys = creationKeys
-            .Concat(afterCreationKeys)
-            .Concat(visibleKeys)
-            .Concat(localAdminKeys)
-            .Distinct(StringComparer.OrdinalIgnoreCase)
-            .Select(fieldKey => new EditableFieldPermission(
-                fieldKey,
-                creationKeys.Contains(fieldKey),
-                afterCreationKeys.Contains(fieldKey),
-                localAdminKeys.Contains(fieldKey),
-                visibleKeys.Contains(fieldKey)))
-            .ToList();
-
-        await _editableFieldPolicyService.SavePolicyAsync(allKeys, CurrentActor(), cancellationToken);
-        await _localAdminEditableFieldPolicyService.SavePolicyAsync(localAdminKeys.ToList(), CurrentActor(), cancellationToken);
-        await AppendAdminAuditAsync(
-            "editablePolicy.updated",
-            "Editable field policy updated.",
-            new BsonDocument
-            {
-                ["editableAtCreationCount"] = creationKeys.Count,
-                ["editableAfterCreationCount"] = afterCreationKeys.Count,
-                ["visibleToClusterAdminCount"] = visibleKeys.Count,
-                ["editableByLocalAdminCount"] = localAdminKeys.Count,
-                ["editableByLocalAdminFieldKeys"] = new BsonArray(localAdminKeys.Select(fieldKey => (BsonValue)fieldKey))
-            },
-            "editablePolicy",
-            "battery-fields",
-            cancellationToken);
-        TempData["StatusMessage"] = "Editable fields updated.";
-        return Redirect("/admin/clusters?tab=local-editable-fields");
-    }
-
-    private HashSet<string> FormKeys(string name) =>
-        Request.Form[name]
-            .Select(value => value?.Trim() ?? string.Empty)
-            .Where(value => !string.IsNullOrWhiteSpace(value))
-            .ToHashSet(StringComparer.OrdinalIgnoreCase);
-
     private static string NormalizeTab(string? value)
     {
         return value?.ToLowerInvariant() switch
@@ -1871,8 +1707,6 @@ public class AdminController : Controller
             "clusters" => "clusters",
             "users" => "users",
             "api-token-management" or "api-tokens" => "api-token-management",
-            "local-editable-fields" => "local-editable-fields",
-            "products" => "products",
             _ => "batteries"
         };
     }
@@ -2376,52 +2210,6 @@ public class AdminController : Controller
             .ToList();
     }
 
-    private static ProductTemplateEditViewModel BuildProductEditModel(
-        BatteryProductTemplate product,
-        DataCompletionPolicySnapshot dataRequirements,
-        string? status,
-        string? error)
-    {
-        var productVersions = BuildProductVersionEditModels(product.ProductVersions.Count == 0
-            ? [product.LatestProductVersion]
-            : product.ProductVersions);
-        var currentVersion = productVersions.FirstOrDefault();
-
-        return new ProductTemplateEditViewModel
-        {
-            ProductId = product.ProductId,
-            ProductName = product.ProductName,
-            Description = product.Description,
-            ImageUrl = product.ImageUrl,
-            ModuleCount = product.ModuleCount,
-            BatteryMassKg = currentVersion?.BatteryMassKg ?? product.BatteryMassKg,
-            RatedEnergyKwh = currentVersion?.RatedEnergyKwh ?? product.RatedEnergyKwh,
-            RatedCapacityAh = currentVersion?.RatedCapacityAh ?? product.RatedCapacityAh,
-            RatedMaximumPowerKw = currentVersion?.RatedMaximumPowerKw ?? product.RatedMaximumPowerKw,
-            NominalVoltageV = currentVersion?.NominalVoltageV ?? product.NominalVoltageV,
-            ExpectedLifetimeYears = currentVersion?.ExpectedLifetimeYears ?? product.ExpectedLifetimeYears,
-            ExpectedCycles = currentVersion?.ExpectedCycles ?? product.ExpectedCycles,
-            SupplyChainIndex = currentVersion?.SupplyChainIndex ?? product.SupplyChainIndex,
-            CarbonFootprint = currentVersion?.CarbonFootprint ?? product.CarbonFootprint,
-            PerformanceClass = currentVersion?.PerformanceClass ?? product.PerformanceClass,
-            MaterialMassesKg = currentVersion?.MaterialMassesKg ?? product.MaterialMassesKg,
-            CarbonStages = currentVersion?.CarbonStages ?? product.CarbonStages,
-            RecycledContent = currentVersion?.RecycledContent ?? product.RecycledContent.ToDictionary(
-                pair => pair.Key,
-                pair => new ProductTemplateRecycledContentViewModel
-                {
-                    PreConsumerShare = pair.Value.PreConsumerShare,
-                    PostConsumerShare = pair.Value.PostConsumerShare
-                },
-                StringComparer.OrdinalIgnoreCase),
-            DataRequirements = dataRequirements,
-            StatusMessage = string.IsNullOrWhiteSpace(status) ? string.Empty : Uri.UnescapeDataString(status),
-            ErrorMessage = string.IsNullOrWhiteSpace(error) ? string.Empty : Uri.UnescapeDataString(error),
-            ProductVersions = productVersions,
-            BaseProductCatalog = BuildProductTemplateFormCatalog(BatteryProductTemplateCatalog.DefaultProducts)
-        };
-    }
-
     private static IReadOnlyList<ProductVersionEditViewModel> BuildProductVersionEditModels(IEnumerable<BatteryProductVersion> productVersions)
     {
         return productVersions
@@ -2479,239 +2267,6 @@ public class AdminController : Controller
             .ToList();
     }
 
-    private static BatteryProductTemplate BuildProductTemplateFromForm(IFormCollection form, BatteryProductTemplate existing)
-    {
-        var requiredFieldKeys = form["requiredFieldKeys"]
-            .Select(value => value ?? string.Empty)
-            .Where(value => !string.IsNullOrWhiteSpace(value))
-            .ToList();
-
-        var productVersions = ReadProductVersions(form, existing, requiredFieldKeys);
-        var latestProductVersion = productVersions.FirstOrDefault() ?? existing.LatestProductVersion;
-
-        return existing with
-        {
-            ProductId = Text(form, "productId", existing.ProductId).Trim().ToLowerInvariant(),
-            ProductName = Text(form, "productName", existing.ProductName),
-            Description = Text(form, "description", existing.Description),
-            ImageUrl = BatteryImageCatalog.NormalizeKnownImageUrl(Text(form, "imageUrl", existing.ImageUrl), string.Empty),
-            ModuleCount = (int)Math.Max(1, Number(form, "moduleCount", existing.ModuleCount)),
-            BatteryMassKg = latestProductVersion.BatteryMassKg,
-            RatedEnergyKwh = latestProductVersion.RatedEnergyKwh,
-            RatedCapacityAh = latestProductVersion.RatedCapacityAh,
-            RatedMaximumPowerKw = latestProductVersion.RatedMaximumPowerKw,
-            NominalVoltageV = latestProductVersion.NominalVoltageV,
-            ExpectedLifetimeYears = latestProductVersion.ExpectedLifetimeYears,
-            ExpectedCycles = latestProductVersion.ExpectedCycles,
-            SupplyChainIndex = latestProductVersion.SupplyChainIndex,
-            CarbonFootprint = latestProductVersion.CarbonFootprint,
-            PerformanceClass = latestProductVersion.PerformanceClass,
-            MaterialMassesKg = latestProductVersion.MaterialMassesKg,
-            CarbonStages = latestProductVersion.CarbonStages,
-            RecycledContent = latestProductVersion.RecycledContent,
-            RequiredFieldKeys = latestProductVersion.RequiredFieldKeys,
-            ProductVersions = productVersions
-        };
-    }
-
-    private static IReadOnlyList<BatteryProductVersion> ReadProductVersions(
-        IFormCollection form,
-        BatteryProductTemplate existing,
-        IReadOnlyList<string> requiredFieldKeys)
-    {
-        var payloadJson = Text(form, "productVersionsJson");
-        if (!string.IsNullOrWhiteSpace(payloadJson))
-        {
-            try
-            {
-                var payloads = JsonSerializer.Deserialize<List<ProductVersionFormPayload>>(
-                    payloadJson,
-                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
-                var parsed = payloads
-                    .Select(payload => BuildProductVersionFromPayload(payload, existing, requiredFieldKeys))
-                    .Where(version => !string.IsNullOrWhiteSpace(version.Version))
-                    .GroupBy(version => version.Version, StringComparer.OrdinalIgnoreCase)
-                    .Select(group => group.First())
-                    .OrderBy(version => version.Version, VersionStringComparer.Descending)
-                    .ToList();
-                if (parsed.Count > 0)
-                {
-                    return parsed;
-                }
-            }
-            catch (JsonException)
-            {
-                // Fall back to the visible form fields below so a malformed client payload does not drop edits.
-            }
-        }
-
-        var softwareVersions = ReadSoftwareVersionsFromForm(form, existing.LatestProductVersion);
-        var highestSoftwareVersion = HighestSoftwareVersion(softwareVersions, existing.LatestProductVersion);
-        var productVersion = new BatteryProductVersion(
-            Text(form, "productVersion", existing.LatestProductVersion.Version),
-            Number(form, "batteryMassKg", existing.LatestProductVersion.BatteryMassKg),
-            Number(form, "ratedEnergyKwh", existing.LatestProductVersion.RatedEnergyKwh),
-            Number(form, "ratedCapacityAh", existing.LatestProductVersion.RatedCapacityAh),
-            Number(form, "ratedMaximumPowerKw", existing.LatestProductVersion.RatedMaximumPowerKw),
-            Number(form, "nominalVoltageV", existing.LatestProductVersion.NominalVoltageV),
-            Number(form, "expectedLifetimeYears", existing.LatestProductVersion.ExpectedLifetimeYears),
-            Number(form, "expectedCycles", existing.LatestProductVersion.ExpectedCycles),
-            BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(Number(form, "supplyChainIndex", existing.LatestProductVersion.SupplyChainIndex)),
-            BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(Number(form, "carbonFootprint", existing.LatestProductVersion.CarbonFootprint)),
-            BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(Text(form, "performanceClass", existing.LatestProductVersion.PerformanceClass)),
-            ReadNumberMap(form, "materialName", "materialMass", existing.LatestProductVersion.MaterialMassesKg),
-            ReadNumberMap(form, "carbonStage", "carbonStageValue", existing.LatestProductVersion.CarbonStages),
-            ReadRecycledContentMap(form, existing.LatestProductVersion.RecycledContent),
-            highestSoftwareVersion.SoftwareVersion,
-            highestSoftwareVersion.SoftwareReleaseDate,
-            highestSoftwareVersion.SoftwareLatestUpdate,
-            existing.LatestProductVersion.TemplateDocuments,
-            requiredFieldKeys)
-        {
-            SoftwareVersions = softwareVersions
-        };
-
-        return [productVersion];
-    }
-
-    private static BatteryProductVersion BuildProductVersionFromPayload(
-        ProductVersionFormPayload payload,
-        BatteryProductTemplate existing,
-        IReadOnlyList<string> requiredFieldKeys)
-    {
-        var fallback = existing.ProductVersions.FirstOrDefault(version =>
-                version.Version.Equals(payload.Version ?? string.Empty, StringComparison.OrdinalIgnoreCase))
-            ?? existing.LatestProductVersion;
-        var templateDocuments = ToTemplateDocuments(payload.TemplateDocuments, fallback.TemplateDocuments);
-        var softwareVersions = NormalizeSoftwareVersions(
-            payload.SoftwareVersions?.Select(row => new BatteryProductSoftwareVersion(
-                row.SoftwareVersion?.Trim() ?? string.Empty,
-                DateOnly(row.SoftwareReleaseDate ?? string.Empty),
-                DateOnly(row.SoftwareLatestUpdate ?? string.Empty))).ToList(),
-            fallback);
-        var highestSoftwareVersion = HighestSoftwareVersion(softwareVersions, fallback);
-
-        return new BatteryProductVersion(
-            payload.Version?.Trim() ?? fallback.Version,
-            payload.BatteryMassKg,
-            payload.RatedEnergyKwh,
-            payload.RatedCapacityAh,
-            payload.RatedMaximumPowerKw,
-            payload.NominalVoltageV,
-            payload.ExpectedLifetimeYears,
-            payload.ExpectedCycles,
-            BatteryPassCanonicalDataCatalog.NormalizeSupplyChainIndex(payload.SupplyChainIndex),
-            BatteryPassCanonicalDataCatalog.NormalizeCarbonFootprint(payload.CarbonFootprint),
-            BatteryPassCanonicalDataCatalog.NormalizePerformanceClass(payload.PerformanceClass ?? fallback.PerformanceClass),
-            payload.MaterialMassesKg?.Count > 0 ? payload.MaterialMassesKg : fallback.MaterialMassesKg,
-            payload.CarbonStages?.Count > 0 ? payload.CarbonStages : fallback.CarbonStages,
-            ToRecycledContent(payload.RecycledContent, fallback.RecycledContent),
-            highestSoftwareVersion.SoftwareVersion,
-            highestSoftwareVersion.SoftwareReleaseDate,
-            highestSoftwareVersion.SoftwareLatestUpdate,
-            templateDocuments,
-            payload.RequiredFieldKeys is null
-                ? requiredFieldKeys
-                : payload.RequiredFieldKeys
-                    .Where(value => !string.IsNullOrWhiteSpace(value))
-                    .Select(value => value.Trim())
-                    .Distinct(StringComparer.OrdinalIgnoreCase)
-                    .ToList())
-        {
-            SoftwareVersions = softwareVersions
-        };
-    }
-
-    private static IReadOnlyList<BatteryProductSoftwareVersion> ReadSoftwareVersionsFromForm(
-        IFormCollection form,
-        BatteryProductVersion fallback)
-    {
-        var versions = form["softwareVersion"];
-        var releases = form["softwareReleaseDate"];
-        var updates = form["softwareLatestUpdate"];
-        var rows = new List<BatteryProductSoftwareVersion>();
-        for (var index = 0; index < versions.Count; index++)
-        {
-            rows.Add(new BatteryProductSoftwareVersion(
-                versions[index]?.Trim() ?? string.Empty,
-                DateOnly(index < releases.Count ? releases[index] ?? string.Empty : string.Empty),
-                DateOnly(index < updates.Count ? updates[index] ?? string.Empty : string.Empty)));
-        }
-
-        return NormalizeSoftwareVersions(rows, fallback);
-    }
-
-    private static IReadOnlyList<BatteryProductSoftwareVersion> NormalizeSoftwareVersions(
-        IReadOnlyList<BatteryProductSoftwareVersion>? requested,
-        BatteryProductVersion fallback)
-    {
-        var rows = requested?
-            .Where(row => !string.IsNullOrWhiteSpace(row.SoftwareVersion))
-            .Select(row => new BatteryProductSoftwareVersion(
-                row.SoftwareVersion.Trim(),
-                DateOnly(row.SoftwareReleaseDate),
-                DateOnly(row.SoftwareLatestUpdate)))
-            .ToList() ?? [];
-
-        if (rows.Count == 0)
-        {
-            rows.AddRange(fallback.SoftwareVersions.Count == 0
-                ? [new BatteryProductSoftwareVersion(fallback.SoftwareVersion, fallback.SoftwareReleaseDate, fallback.SoftwareLatestUpdate)]
-                : fallback.SoftwareVersions);
-        }
-
-        return rows
-            .GroupBy(row => row.SoftwareVersion, StringComparer.OrdinalIgnoreCase)
-            .Select(group => group.First())
-            .OrderByDescending(row => row.SoftwareVersion, StringComparer.OrdinalIgnoreCase)
-            .ToList();
-    }
-
-    private static BatteryProductSoftwareVersion HighestSoftwareVersion(
-        IReadOnlyList<BatteryProductSoftwareVersion> rows,
-        BatteryProductVersion fallback) =>
-        rows
-            .Where(row => !string.IsNullOrWhiteSpace(row.SoftwareVersion))
-            .OrderByDescending(row => row.SoftwareVersion, StringComparer.OrdinalIgnoreCase)
-            .FirstOrDefault()
-        ?? new BatteryProductSoftwareVersion(fallback.SoftwareVersion, fallback.SoftwareReleaseDate, fallback.SoftwareLatestUpdate);
-
-    private static IReadOnlyList<ProductTemplateDocumentSeed> ToTemplateDocuments(
-        List<ProductTemplateDocumentFormPayload>? payload,
-        IReadOnlyList<ProductTemplateDocumentSeed> fallback)
-    {
-        if (payload == null)
-        {
-            return fallback;
-        }
-
-        var documents = payload
-            .Where(document => !string.IsNullOrWhiteSpace(document.DocumentKey))
-            .Select(document => new ProductTemplateDocumentSeed(
-                document.DocumentKey?.Trim() ?? string.Empty,
-                document.Label?.Trim() ?? string.Empty,
-                document.FileName?.Trim() ?? string.Empty,
-                string.IsNullOrWhiteSpace(document.Visibility) ? "private" : document.Visibility.Trim()))
-            .ToList();
-
-        return documents.Count == 0 ? fallback : documents;
-    }
-
-    private static IReadOnlyDictionary<string, ProductTemplateRecycledContent> ToRecycledContent(
-        Dictionary<string, ProductTemplateRecycledContentFormPayload>? payload,
-        IReadOnlyDictionary<string, ProductTemplateRecycledContent> fallback)
-    {
-        if (payload == null || payload.Count == 0)
-        {
-            return fallback;
-        }
-
-        return payload.ToDictionary(
-            pair => pair.Key,
-            pair => new ProductTemplateRecycledContent(pair.Value.PreConsumerShare, pair.Value.PostConsumerShare),
-            StringComparer.OrdinalIgnoreCase);
-    }
-
     private static IReadOnlyList<ProductTemplateFormCatalogItemViewModel> BuildProductTemplateFormCatalog(IEnumerable<BatteryProductTemplate> products)
     {
         return products
@@ -2749,100 +2304,6 @@ public class AdminController : Controller
             .ToList();
     }
 
-    private static IReadOnlyDictionary<string, double> ReadNumberMap(
-        IFormCollection form,
-        string nameKey,
-        string valueKey,
-        IReadOnlyDictionary<string, double> fallback)
-    {
-        var names = form[nameKey];
-        var values = form[valueKey];
-        if (names.Count == 0 || values.Count == 0)
-        {
-            return fallback;
-        }
-
-        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
-        for (var index = 0; index < names.Count; index++)
-        {
-            var name = names[index]?.Trim() ?? string.Empty;
-            if (string.IsNullOrWhiteSpace(name))
-            {
-                continue;
-            }
-
-            var fallbackValue = fallback.TryGetValue(name, out var existingValue) ? existingValue : 0;
-            result[name] = double.TryParse(index < values.Count ? values[index] : string.Empty, out var parsed)
-                ? parsed
-                : fallbackValue;
-        }
-
-        return result.Count == 0 ? fallback : result;
-    }
-
-    private static IReadOnlyDictionary<string, ProductTemplateRecycledContent> ReadRecycledContentMap(
-        IFormCollection form,
-        IReadOnlyDictionary<string, ProductTemplateRecycledContent> fallback)
-    {
-        var result = new Dictionary<string, ProductTemplateRecycledContent>(StringComparer.OrdinalIgnoreCase);
-        foreach (var material in new[] { "Nickel", "Cobalt", "Lithium", "Lead" })
-        {
-            var fallbackValue = fallback.TryGetValue(material, out var existing)
-                ? existing
-                : new ProductTemplateRecycledContent(0, 0);
-            result[material] = new ProductTemplateRecycledContent(
-                Number(form, $"recycled{material}Pre", fallbackValue.PreConsumerShare),
-                Number(form, $"recycled{material}Post", fallbackValue.PostConsumerShare));
-        }
-
-        return result;
-    }
-
-    private sealed class ProductVersionFormPayload
-    {
-        public string? Version { get; init; }
-        public double BatteryMassKg { get; init; }
-        public double RatedEnergyKwh { get; init; }
-        public double RatedCapacityAh { get; init; }
-        public double RatedMaximumPowerKw { get; init; }
-        public double NominalVoltageV { get; init; }
-        public double ExpectedLifetimeYears { get; init; }
-        public double ExpectedCycles { get; init; }
-        public double SupplyChainIndex { get; init; }
-        public double CarbonFootprint { get; init; }
-        public string? PerformanceClass { get; init; }
-        public Dictionary<string, double>? MaterialMassesKg { get; init; }
-        public Dictionary<string, double>? CarbonStages { get; init; }
-        public Dictionary<string, ProductTemplateRecycledContentFormPayload>? RecycledContent { get; init; }
-        public string? SoftwareVersion { get; init; }
-        public string? SoftwareReleaseDate { get; init; }
-        public string? SoftwareLatestUpdate { get; init; }
-        public List<ProductSoftwareVersionFormPayload>? SoftwareVersions { get; init; }
-        public List<ProductTemplateDocumentFormPayload>? TemplateDocuments { get; init; }
-        public List<string>? RequiredFieldKeys { get; init; }
-    }
-
-    private sealed class ProductSoftwareVersionFormPayload
-    {
-        public string? SoftwareVersion { get; init; }
-        public string? SoftwareReleaseDate { get; init; }
-        public string? SoftwareLatestUpdate { get; init; }
-    }
-
-    private sealed class ProductTemplateRecycledContentFormPayload
-    {
-        public double PreConsumerShare { get; init; }
-        public double PostConsumerShare { get; init; }
-    }
-
-    private sealed class ProductTemplateDocumentFormPayload
-    {
-        public string? DocumentKey { get; init; }
-        public string? Label { get; init; }
-        public string? FileName { get; init; }
-        public string? Visibility { get; init; }
-    }
-
     private sealed class VersionStringComparer : IComparer<string>
     {
         public static readonly VersionStringComparer Descending = new(descending: true);
diff --git a/web/Controllers/ClusterAdminController.cs b/web/Controllers/ClusterAdminController.cs
--- a/web/Controllers/ClusterAdminController.cs
+++ b/web/Controllers/ClusterAdminController.cs
@@ -50,7 +50,6 @@ public class ClusterAdminController : Controller
     private readonly ApplicationAuditService _applicationAuditService;
     private readonly PassportEvidenceService _passportEvidenceService;
     private readonly EditableFieldPolicyService _editableFieldPolicyService;
-    private readonly LocalAdminEditableFieldPolicyService _localAdminEditableFieldPolicyService;
     private readonly PassportTrustWorkflowService _passportTrustWorkflowService;
 
     public ClusterAdminController(
@@ -75,7 +74,6 @@ public class ClusterAdminController : Controller
         ApplicationAuditService applicationAuditService,
         PassportEvidenceService passportEvidenceService,
         EditableFieldPolicyService editableFieldPolicyService,
-        LocalAdminEditableFieldPolicyService localAdminEditableFieldPolicyService,
         PassportTrustWorkflowService passportTrustWorkflowService)
     {
         _passportRepository = passportRepository;
@@ -99,7 +97,6 @@ public class ClusterAdminController : Controller
         _applicationAuditService = applicationAuditService;
         _passportEvidenceService = passportEvidenceService;
         _editableFieldPolicyService = editableFieldPolicyService;
-        _localAdminEditableFieldPolicyService = localAdminEditableFieldPolicyService;
         _passportTrustWorkflowService = passportTrustWorkflowService;
     }
 
diff --git a/web/Models/ViewModels/AdminClusterViewModel.cs b/web/Models/ViewModels/AdminClusterViewModel.cs
--- a/web/Models/ViewModels/AdminClusterViewModel.cs
+++ b/web/Models/ViewModels/AdminClusterViewModel.cs
@@ -15,8 +15,6 @@ public sealed class AdminClusterViewModel
     public IReadOnlyList<UserViewModel> Users { get; init; } = [];
     public IReadOnlyList<ClusterMembershipViewModel> Memberships { get; init; } = [];
     public IReadOnlyList<ApiTokenViewModel> ApiTokens { get; init; } = [];
-    public IReadOnlyList<ProductTemplateSummaryViewModel> ProductTemplates { get; init; } = [];
-    public EditableFieldPolicySnapshot LocalEditableFieldPolicy { get; init; } = EditableFieldPolicyService.CreateDefaultPolicy();
     public string SamplePassportId { get; init; } = string.Empty;
     public string SampleReadToken { get; init; } = string.Empty;
     public string SampleReadWriteToken { get; init; } = string.Empty;
@@ -106,33 +104,6 @@ public sealed class ProductTemplateRecycledContentViewModel
     public double PostConsumerShare { get; init; }
 }
 
-public sealed class ProductTemplateEditViewModel
-{
-    public string ProductId { get; init; } = string.Empty;
-    public string ProductName { get; init; } = string.Empty;
-    public string Description { get; init; } = string.Empty;
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
-    public DataCompletionPolicySnapshot DataRequirements { get; init; } = new();
-    public string StatusMessage { get; init; } = string.Empty;
-    public string ErrorMessage { get; init; } = string.Empty;
-    public IReadOnlyList<ProductVersionEditViewModel> ProductVersions { get; init; } = [];
-    public IReadOnlyList<ProductTemplateFormCatalogItemViewModel> BaseProductCatalog { get; init; } = [];
-}
-
 public sealed class ClusterViewModel
 {
     public string ClusterId { get; init; } = string.Empty;
diff --git a/web/Program.cs b/web/Program.cs
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -225,7 +225,6 @@ builder.Services.AddSingleton<PassportReadinessService>();
 builder.Services.AddSingleton<PassportEvidenceService>();
 builder.Services.AddSingleton<DataCompletionPolicyService>();
 builder.Services.AddSingleton<EditableFieldPolicyService>();
-builder.Services.AddSingleton<LocalAdminEditableFieldPolicyService>();
 builder.Services.AddSingleton<ProductTemplateService>();
 builder.Services.AddSingleton<DemoRequiredDataCompletionService>();
 builder.Services.AddSingleton<PassportDataNormalizationService>();
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -7,22 +7,6 @@
     var query = Model.PassportsQuery ?? string.Empty;
     var querySuffix = string.IsNullOrWhiteSpace(query) ? string.Empty : $"&q={Uri.EscapeDataString(query)}";
     ViewData["AdminBatteriesQuerySuffix"] = querySuffix;
-    var editableAtCreationKeys = Model.LocalEditableFieldPolicy.PermissionByKey.Values
-        .Where(permission => permission.EditableAtCreation)
-        .Select(permission => permission.FieldKey)
-        .ToHashSet(StringComparer.OrdinalIgnoreCase);
-    var editableAfterCreationKeys = Model.LocalEditableFieldPolicy.PermissionByKey.Values
-        .Where(permission => permission.EditableAfterCreation)
-        .Select(permission => permission.FieldKey)
-        .ToHashSet(StringComparer.OrdinalIgnoreCase);
-    var visibleToClusterAdminKeys = Model.LocalEditableFieldPolicy.PermissionByKey.Values
-        .Where(permission => permission.VisibleToClusterAdmin)
-        .Select(permission => permission.FieldKey)
-        .ToHashSet(StringComparer.OrdinalIgnoreCase);
-    var editableByLocalAdminKeys = Model.LocalEditableFieldPolicy.PermissionByKey.Values
-        .Where(permission => permission.EditableByLocalAdmin)
-        .Select(permission => permission.FieldKey)
-        .ToHashSet(StringComparer.OrdinalIgnoreCase);
     var clusterCreateName = TempData["ClusterCreateName"]?.ToString() ?? string.Empty;
     var clusterCreateId = TempData["ClusterCreateId"]?.ToString() ?? string.Empty;
     var globalAccessOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
@@ -640,128 +624,6 @@
         </div>
     }
 
-    @if (selectedTab == "local-editable-fields")
-    {
-        var localEditableSectionIndex = 0;
-        <section class="bp-card bp-policy-console mt-4">
-            <div class="d-flex justify-content-between flex-wrap gap-3 align-items-start">
-                <div>
-                    <h2 class="h5 mb-1">Editable fields</h2>
-                    <p class="text-secondary mb-0">Choose which battery parameters can be edited at creation, after creation, shown to cluster admins, and edited by cluster admins.</p>
-                </div>
-                @if (!string.IsNullOrWhiteSpace(Model.LocalEditableFieldPolicy.UpdatedAt))
-                {
-                    <p class="bp-policy-meta mb-0">Updated @Model.LocalEditableFieldPolicy.UpdatedAt by @Model.LocalEditableFieldPolicy.UpdatedBy</p>
-                }
-            </div>
-            <form method="post" action="/admin/local-editable-fields/save" class="bp-local-editable-policy-form mt-3">
-                @Html.AntiForgeryToken()
-                <div class="bp-local-editable-policy-summary">
-                    <span class="bp-user-count-pill">@Model.LocalEditableFieldPolicy.Sections.Count sections</span>
-                    <span class="bp-user-count-pill">@Model.LocalEditableFieldPolicy.EditableAtCreationCount creation</span>
-                    <span class="bp-user-count-pill">@Model.LocalEditableFieldPolicy.EditableAfterCreationCount after creation</span>
-                    <span class="bp-user-count-pill">@Model.LocalEditableFieldPolicy.VisibleToClusterAdminCount shown to cluster admin</span>
-                    <span class="bp-user-count-pill">@Model.LocalEditableFieldPolicy.EditableByLocalAdminCount editable by cluster admin</span>
-                </div>
-                <div class="bp-table-card p-0">
-                    <div class="bp-table-scroll">
-                        <table class="table align-middle mb-0 bp-console-table bp-local-editable-policy-table bp-mobile-card-table">
-                            <thead>
-                                <tr>
-                                    <th>Section</th>
-                                    <th>Total fields</th>
-                                    <th>Editable</th>
-                                    <th>After creation</th>
-                                    <th>Shown</th>
-                                    <th>Cluster admin</th>
-                                    <th>Examples</th>
-                                    <th class="bp-action-cell">Actions</th>
-                                </tr>
-                            </thead>
-                            <tbody>
-                                @foreach (var policySection in Model.LocalEditableFieldPolicy.Sections.OrderBy(policySection => policySection.SortOrder))
-                                {
-                                    var sectionIndex = localEditableSectionIndex++;
-                                    var drawerId = $"local-editable-section-{sectionIndex}";
-                                    var orderedFields = policySection.Fields.OrderBy(field => field.SortOrder).ToList();
-                                    var sectionCreationCount = orderedFields.Count(field => editableAtCreationKeys.Contains(field.FieldKey));
-                                    var sectionAfterCreationCount = orderedFields.Count(field => editableAfterCreationKeys.Contains(field.FieldKey));
-                                    var sectionVisibleCount = orderedFields.Count(field => visibleToClusterAdminKeys.Contains(field.FieldKey));
-                                    var sectionLocalAdminCount = orderedFields.Count(field => editableByLocalAdminKeys.Contains(field.FieldKey));
-                                    var examples = string.Join(", ", orderedFields.Take(2).Select(field => field.Label));
-                                    <tr>
-                                        <td class="bp-local-editable-section-cell" data-label="Section">
-                                            <strong>@policySection.Label</strong>
-                                            <span>@policySection.SectionKey</span>
-                                        </td>
-                                        <td data-label="Total fields"><span class="bp-user-count-pill">@policySection.Fields.Count fields</span></td>
-                                        <td data-label="Editable"><span class="bp-local-editable-state-pill @(sectionCreationCount > 0 ? "is-on" : string.Empty)">@sectionCreationCount creation</span></td>
-                                        <td data-label="After creation"><span class="bp-local-editable-state-pill @(sectionAfterCreationCount > 0 ? "is-on" : string.Empty)">@sectionAfterCreationCount after</span></td>
-                                        <td data-label="Shown"><span class="bp-local-editable-state-pill @(sectionVisibleCount > 0 ? "is-on" : string.Empty)">@sectionVisibleCount shown</span></td>
-                                        <td data-label="Cluster admin"><span class="bp-local-editable-state-pill @(sectionLocalAdminCount > 0 ? "is-on" : string.Empty)">@sectionLocalAdminCount editable</span></td>
-                                        <td data-label="Examples"><span class="bp-local-editable-examples">@examples</span></td>
-                                        <td class="bp-action-cell" data-label="Actions">
-                                            <button type="button" class="bp-action-button bp-local-editable-drawer-toggle" data-local-editable-drawer-toggle aria-expanded="false" aria-controls="@drawerId" aria-label="Open @policySection.Label fields" title="Open @policySection.Label fields" data-tooltip="Open fields">
-                                                <svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true">
-                                                    <path d="m6 9 6 6 6-6" />
-                                                </svg>
-                                            </button>
-                                        </td>
-                                    </tr>
-                                    <tr id="@drawerId" class="bp-local-editable-drawer-row" hidden>
-                                        <td colspan="8">
-                                            <div class="bp-local-editable-field-grid">
-                                                @foreach (var field in orderedFields)
-                                                {
-                                                    var isEditableAtCreation = editableAtCreationKeys.Contains(field.FieldKey);
-                                                    var isEditableAfterCreation = editableAfterCreationKeys.Contains(field.FieldKey);
-                                                    var isVisibleToClusterAdmin = visibleToClusterAdminKeys.Contains(field.FieldKey);
-                                                    var isEditableByLocalAdmin = editableByLocalAdminKeys.Contains(field.FieldKey);
-                                                    <label class="bp-local-editable-field-card">
-                                                        <span class="bp-local-editable-field-copy">
-                                                            <strong>@field.Label</strong>
-                                                            <span>@field.Guidance</span>
-                                                            <code>@field.FieldKey</code>
-                                                        </span>
-                                                        <span class="bp-local-editable-toggle-group" data-editable-policy-row>
-                                                            <span class="bp-local-editable-toggle-wrap">
-                                                                <span class="bp-local-editable-toggle-label">Creation</span>
-                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableAtCreationFieldKeys" value="@field.FieldKey" checked="@isEditableAtCreation" aria-label="@field.Label editable at creation" data-editable-policy-creation />
-                                                                <span class="bp-local-editable-toggle-shell" aria-hidden="true"></span>
-                                                            </span>
-                                                            <span class="bp-local-editable-toggle-wrap">
-                                                                <span class="bp-local-editable-toggle-label">After</span>
-                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableAfterCreationFieldKeys" value="@field.FieldKey" checked="@isEditableAfterCreation" aria-label="@field.Label editable after creation" data-editable-policy-after />
-                                                                <span class="bp-local-editable-toggle-shell" aria-hidden="true"></span>
-                                                            </span>
-                                                            <span class="bp-local-editable-toggle-wrap">
-                                                                <span class="bp-local-editable-toggle-label">Show</span>
-                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="visibleToClusterAdminFieldKeys" value="@field.FieldKey" checked="@isVisibleToClusterAdmin" aria-label="@field.Label Show for Cluster Admin" title="Show for Cluster Admin" data-editable-policy-visible />
-                                                                <span class="bp-local-editable-toggle-shell" aria-hidden="true"></span>
-                                                            </span>
-                                                            <span class="bp-local-editable-toggle-wrap">
-                                                                <span class="bp-local-editable-toggle-label">Edit</span>
-                                                                <input class="bp-local-editable-field-toggle" type="checkbox" name="editableByLocalAdminFieldKeys" value="@field.FieldKey" checked="@isEditableByLocalAdmin" aria-label="@field.Label editable by cluster admins" data-editable-policy-local />
-                                                                <span class="bp-local-editable-toggle-shell" aria-hidden="true"></span>
-                                                            </span>
-                                                        </span>
-                                                    </label>
-                                                }
-                                            </div>
-                                        </td>
-                                    </tr>
-                                }
-                            </tbody>
-                        </table>
-                    </div>
-                </div>
-                <div class="bp-local-editable-policy-actions">
-                    <button type="submit" class="bp-primary-button">Save editable fields</button>
-                </div>
-            </form>
-        </section>
-    }
-
     @if (selectedTab == "api-token-management")
     {
         var activeTokenCount = Model.ApiTokens.Count(token => token.IsActive);
@@ -995,51 +857,6 @@
 
     }
 
-    @if (selectedTab == "products")
-    {
-        <section class="bp-card bp-product-template-hero mt-4">
-            <div>
-                <span class="bp-summary-title">Battery families</span>
-                <h2>Battery families and version parameters</h2>
-                <p class="mb-0">
-                    Battery families hold the shared battery data, required/optional policy, battery image,
-                    software release dates, and signed evidence references used when creating passports.
-                    Editing a family does not silently change batteries. Use a Battery Model push when you are ready.
-                </p>
-            </div>
-            <div class="bp-product-template-actions">
-                <a href="/admin/products/new" class="bp-secondary-button">Add battery family</a>
-                <form method="post" action="/admin/product-templates/reset">
-                    @Html.AntiForgeryToken()
-                    <button type="submit" class="bp-secondary-button">Reset battery-family passports</button>
-                </form>
-            </div>
-        </section>
-
-        <section class="bp-product-template-grid mt-4">
-            @foreach (var product in Model.ProductTemplates)
-            {
-                <article class="bp-card bp-product-template-card">
-                    <div class="bp-product-template-card-media">
-                        <img src="@product.ImageUrl" alt="@product.ProductName battery family image" />
-                    </div>
-                    <div class="bp-product-template-card-body">
-                        <span class="bp-summary-title">@product.ProductId</span>
-                        <h3>@product.ProductName</h3>
-                        <p>@product.Description</p>
-                        <dl class="bp-product-template-meta">
-                            <div><dt>Modules</dt><dd>@product.ModuleCount</dd></div>
-                            <div><dt>Battery Models</dt><dd>@product.BatteryVersionCount</dd></div>
-                            <div><dt>Required fields</dt><dd>@product.RequiredFieldCount</dd></div>
-                            <div><dt>Template documents</dt><dd>@product.DocumentCount</dd></div>
-                        </dl>
-                        <a href="/admin/products/@Uri.EscapeDataString(product.ProductId)" class="bp-primary-button">Edit family</a>
-                    </div>
-                </article>
-            }
-        </section>
-    }
-
     <div class="bp-confirm-modal" data-confirm-modal hidden>
         <div class="bp-confirm-modal-backdrop" data-confirm-cancel></div>
         <section class="bp-confirm-modal-panel" role="dialog" aria-modal="true" aria-labelledby="bp-confirm-title">
@@ -1113,57 +930,6 @@
                 }
             }
 
-            document.querySelectorAll('[data-local-editable-drawer-toggle]').forEach((button) => {
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
-            document.querySelectorAll('[data-editable-policy-row]').forEach((row) => {
-                const creation = row.querySelector('[data-editable-policy-creation]');
-                const afterCreation = row.querySelector('[data-editable-policy-after]');
-                const visibleToClusterAdmin = row.querySelector('[data-editable-policy-visible]');
-                const localAdmin = row.querySelector('[data-editable-policy-local]');
-                if (!creation || !afterCreation || !visibleToClusterAdmin || !localAdmin) {
-                    return;
-                }
-
-                creation.addEventListener('change', () => {
-                    if (!creation.checked) {
-                        afterCreation.checked = false;
-                        visibleToClusterAdmin.checked = false;
-                        localAdmin.checked = false;
-                    }
-                });
-                afterCreation.addEventListener('change', () => {
-                    if (afterCreation.checked) {
-                        creation.checked = true;
-                    } else {
-                        localAdmin.checked = false;
-                    }
-                });
-                visibleToClusterAdmin.addEventListener('change', () => {
-                    if (!visibleToClusterAdmin.checked) {
-                        localAdmin.checked = false;
-                    }
-                });
-                localAdmin.addEventListener('change', () => {
-                    if (localAdmin.checked) {
-                        creation.checked = true;
-                        afterCreation.checked = true;
-                        visibleToClusterAdmin.checked = true;
-                    }
-                });
-            });
-
             const tokenSearch = document.querySelector('[data-token-search]');
             const tokenRows = Array.from(document.querySelectorAll('[data-token-row]'));
             const tokenNoResults = document.querySelector('[data-token-no-results]');
diff --git a/web/Views/Admin/Help.cshtml b/web/Views/Admin/Help.cshtml
--- a/web/Views/Admin/Help.cshtml
+++ b/web/Views/Admin/Help.cshtml
@@ -38,7 +38,7 @@
             </nav>
             <nav class="bp-admin-help-primary-actions" aria-label="Primary admin actions">
                 <a href="/admin/passports/new" class="bp-primary-button">Create battery ID</a>
-                <a href="/admin/clusters?tab=products" class="bp-secondary-button">Open battery families</a>
+                <a href="/admin/families" class="bp-secondary-button">Open battery families</a>
                 <a href="/admin/clusters?tab=batteries" class="bp-secondary-button">Open batteries</a>
             </nav>
         </div>
@@ -104,13 +104,6 @@
                         <td>Validate, fix blockers, sign again, and publish the newest signed revision.</td>
                         <td class="bp-action-cell"><button type="button" class="bp-action-button bp-admin-help-detail-toggle" data-admin-help-detail-target="admin-detail-recover" aria-expanded="false" aria-controls="admin-detail-recover" aria-label="Show dirty recovery details" title="Show dirty recovery details" data-tooltip="Details"><svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg></button></td>
                     </tr>
-                    <tr>
-                        <td><span class="bp-admin-help-workflow-name"><strong>Battery Model push</strong><small>Apply shared model updates.</small></span></td>
-                        <td>Battery Family model data or software parameters changed.</td>
-                        <td><a href="/admin/clusters?tab=products">Battery families</a></td>
-                        <td>Matching batteries update, overrides are preserved, changed passports become dirty.</td>
-                        <td class="bp-action-cell"><button type="button" class="bp-action-button bp-admin-help-detail-toggle" data-admin-help-detail-target="admin-detail-model-push" aria-expanded="false" aria-controls="admin-detail-model-push" aria-label="Show Battery Model push details" title="Show Battery Model push details" data-tooltip="Details"><svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg></button></td>
-                    </tr>
                     <tr>
                         <td><span class="bp-admin-help-workflow-name"><strong>API Token Management</strong><small>External credentials and signing.</small></span></td>
                         <td>An integration needs read, write, validate, sign, or publish access.</td>
@@ -118,13 +111,6 @@
                         <td>Token Value is captured by the external system and scope is correct.</td>
                         <td class="bp-action-cell"><button type="button" class="bp-action-button bp-admin-help-detail-toggle" data-admin-help-detail-target="admin-detail-tokens" aria-expanded="false" aria-controls="admin-detail-tokens" aria-label="Show API token management details" title="Show API token management details" data-tooltip="Details"><svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg></button></td>
                     </tr>
-                    <tr>
-                        <td><span class="bp-admin-help-workflow-name"><strong>Local editable fields</strong><small>Global local-admin policy.</small></span></td>
-                        <td>Cluster admins need a controlled edit surface.</td>
-                        <td><a href="/admin/clusters?tab=local-editable-fields">Local editable fields</a></td>
-                        <td>MongoDB policy controls what local admins can render and save.</td>
-                        <td class="bp-action-cell"><button type="button" class="bp-action-button bp-admin-help-detail-toggle" data-admin-help-detail-target="admin-detail-local-fields" aria-expanded="false" aria-controls="admin-detail-local-fields" aria-label="Show local editable fields details" title="Show local editable fields details" data-tooltip="Details"><svg class="bp-action-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg></button></td>
-                    </tr>
                     <tr class="bp-admin-help-detail-row" data-admin-help-detail-row hidden>
                         <td colspan="5">
                             <div class="bp-admin-help-selected-panel" aria-live="polite" tabindex="-1" hidden>
@@ -132,7 +118,7 @@
                 <div class="bp-admin-help-detail-copy"><span class="bp-summary-title">First-time passport checklist</span><h3>Create a trusted passport from scratch</h3><p>Create battery ID, choose Battery Family and Battery Model, and save the draft copied from the template.</p></div>
                 <ol class="bp-admin-help-detail-list">
                     <li>Review copied battery family data and fill battery-specific information in every admin section.</li>
-                    <li>Open <a href="/admin/clusters?tab=products">battery families</a> to confirm required and optional fields for that battery family.</li>
+                    <li>Open <a href="/admin/families">battery families</a> to confirm required and optional fields for that battery family.</li>
                     <li>Validate passport, resolve every blocker, sign when blockers are zero, then publish when the gate says publishing is ready.</li>
                 </ol>
             </article>
@@ -169,19 +155,9 @@
                 <div class="bp-admin-help-detail-copy"><span class="bp-summary-title">Dirty recovery checklist</span><h3>Return edited data to clean trust</h3><p>Use this after any signed core data changes.</p></div>
                 <ol class="bp-admin-help-detail-list"><li>Save the admin edit. Draft save is always allowed.</li><li>Open conformance and confirm the passport is dirty or not publishable.</li><li>Validate passport again, fix blockers, sign again, then publish again when the latest proof is current.</li></ol>
             </article>
-            <article id="admin-detail-model-push" class="bp-admin-help-detail-panel" data-admin-help-detail-panel hidden>
-                <div class="bp-admin-help-detail-copy"><span class="bp-summary-title">Battery Model push</span><h3>Update matching batteries safely</h3><p>Open battery families and edit shared Battery Model data or software release date and latest update values. The app updates fields that still match the previous template baseline while preserving manual battery overrides.</p></div>
-                <div class="bp-admin-help-detail-grid">
-                    <section class="bp-admin-help-detail-card"><h4>Software parameters</h4><p>New passports copy Battery Model plus software version, release date, and latest update values from the selected battery family. The public summary and Detailed report General tab show these software parameters.</p></section>
-                    <section class="bp-admin-help-detail-card"><h4>Battery Model API changes</h4><p>Battery Model and software API changes require a new passport snapshot when the values differ from the latest passport. The software-version endpoint copies release date and latest update values from the selected Battery Model. Telemetry remains operational and does not require signing.</p></section>
-                </div>
-            </article>
             <article id="admin-detail-tokens" class="bp-admin-help-detail-panel" data-admin-help-detail-panel hidden>
                 <div class="bp-admin-help-detail-copy"><span class="bp-summary-title">External systems</span><h3>Use scoped credentials.</h3><p>Open API Token Management to create or regenerate external API tokens. The generated Token Value is the secret credential used in the Authorization header and is shown only at create/regenerate time.</p></div>
                 <p>Cluster scope on each token controls which batteries the external system can read, write, validate, sign, or publish.</p>
-            </article>
-            <article id="admin-detail-local-fields" class="bp-admin-help-detail-panel" data-admin-help-detail-panel hidden>
-                <div class="bp-admin-help-detail-copy"><span class="bp-summary-title">Local editable fields</span><h3>Keep local edit access narrow.</h3><p>Select the passport fields local admins may edit across all batteries. The saved MongoDB policy controls both what the cluster-admin edit form renders and what its save action accepts.</p></div>
             </article>
                             </div>
                         </td>
@@ -199,8 +175,8 @@
         </div>
         <div class="bp-admin-help-policy-note">
             <strong>Completion policy</strong>
-            <span>The <strong>Battery families tab</strong> controls required/optional switches per Battery Model. Validation, signing, publishing, edit guidance, and conformance checklists read the saved product policy and product-version policy.</span>
-            <a href="/admin/clusters?tab=products" class="bp-secondary-button bp-button-compact">Open battery families</a>
+            <span>The <strong>Battery families tab</strong> holds each battery model's passport values and its batches; the V3 attribute list decides which values are required, and by when.</span>
+            <a href="/admin/families" class="bp-secondary-button bp-button-compact">Open battery families</a>
         </div>
         <section id="status-dictionary" aria-label="Status dictionary">
             <div class="bp-table-scroll">
@@ -249,20 +225,6 @@
         </ul>
     </section>
 
-    <details class="bp-card bp-admin-help-demo-reset bp-admin-help-maintenance-reset" id="maintenance-reset">
-        <summary>
-            <span class="bp-summary-title">Maintenance</span>
-            <strong>Battery family reset</strong>
-            <small>Restore one unassigned demonstrator plus six clustered customer batteries.</small>
-        </summary>
-        <div class="bp-admin-help-maintenance-body">
-            <p>This guarded reset recreates Compact 7M, Compact 13M, and Core battery families with Battery Models, software parameters, evidence files, clusters, users, and passport records.</p>
-            <form method="post" action="/admin/product-templates/reset">
-                @Html.AntiForgeryToken()
-                <button type="submit" class="bp-secondary-button bp-button-accent">Reset battery-family passports</button>
-            </form>
-        </div>
-    </details>
 </main>
 
 @section Scripts {
diff --git a/web/Views/Shared/_AdminTabs.cshtml b/web/Views/Shared/_AdminTabs.cshtml
--- a/web/Views/Shared/_AdminTabs.cshtml
+++ b/web/Views/Shared/_AdminTabs.cshtml
@@ -13,9 +13,9 @@
         <option value="/admin/clusters?tab=battery" selected="@IsSelected("battery")">Battery cluster assignments</option>
         <option value="/admin/clusters?tab=clusters" selected="@IsSelected("clusters")">Registered clusters</option>
         <option value="/admin/clusters?tab=users" selected="@IsSelected("users")">Users</option>
-        <option value="/admin/clusters?tab=local-editable-fields" selected="@IsSelected("local-editable-fields")">Editable fields</option>
         <option value="/admin/clusters?tab=api-token-management" selected="@IsSelected("api-token-management")">API Token Management</option>
-        <option value="/admin/clusters?tab=products" selected="@IsSelected("products")">Battery families</option>
+        <option value="/admin/families" selected="@IsSelected("products")">Battery families</option>
+        <option value="/admin/sites-and-operators" selected="@IsSelected("sites-and-operators")">Sites and operators</option>
         <option value="/admin/audit" selected="@IsSelected("audit")">Audit log</option>
         <option value="/admin/help" selected="@IsSelected("help")">Help</option>
     </select>
@@ -26,9 +26,9 @@
     <a class="@TabClass("battery")" href="/admin/clusters?tab=battery">Battery cluster assignments</a>
     <a class="@TabClass("clusters")" href="/admin/clusters?tab=clusters">Registered clusters</a>
     <a class="@TabClass("users")" href="/admin/clusters?tab=users">Users</a>
-    <a class="@TabClass("local-editable-fields")" href="/admin/clusters?tab=local-editable-fields">Editable fields</a>
     <a class="@TabClass("api-token-management")" href="/admin/clusters?tab=api-token-management">API Token Management</a>
-    <a class="@TabClass("products")" href="/admin/clusters?tab=products">Battery families</a>
+    <a class="@TabClass("products")" href="/admin/families">Battery families</a>
+    <a class="@TabClass("sites-and-operators")" href="/admin/sites-and-operators">Sites and operators</a>
     <a class="@TabClass("audit")" href="/admin/audit">Audit log</a>
     <a class="@TabClass("help")" href="/admin/help">Help</a>
 </nav>
```

- [ ] **Step 4: Delete the files only the old pages used**

```bash
git rm -q web/Views/Admin/Product.cshtml web/Services/LocalAdminEditableFieldPolicyService.cs
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Dpp.AdminMenuTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminCredentialLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminDenseConsoleLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackFollowupTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminFeedbackImplementationTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminHelpPageTests|FullyQualifiedName~BatteryPassWeb.Tests.AdminTestFeedbackFixesTests|FullyQualifiedName~BatteryPassWeb.Tests.ApplicationAuditCoverageTests|FullyQualifiedName~BatteryPassWeb.Tests.BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~BatteryPassWeb.Tests.DataRequirementsAdminLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.ExternalApiBatteryVersionTests|FullyQualifiedName~BatteryPassWeb.Tests.LocalAdminEditableFieldPolicyTests|FullyQualifiedName~BatteryPassWeb.Tests.MobileThemeSessionLayoutTests|FullyQualifiedName~BatteryPassWeb.Tests.Phase6ADocumentationTests|FullyQualifiedName~BatteryPassWeb.Tests.ProductTemplateWorkflowLayoutTests"`
Expected: `Passed!  - Failed:     0, Passed:   117`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  1820`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Dpp/AdminMenuTests.cs BatteryPassWeb.Tests/AdminCredentialLayoutTests.cs BatteryPassWeb.Tests/AdminDenseConsoleLayoutTests.cs BatteryPassWeb.Tests/AdminFeedbackFollowupTests.cs BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs BatteryPassWeb.Tests/AdminHelpPageTests.cs BatteryPassWeb.Tests/AdminTestFeedbackFixesTests.cs BatteryPassWeb.Tests/ApplicationAuditCoverageTests.cs BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs BatteryPassWeb.Tests/DataRequirementsAdminLayoutTests.cs BatteryPassWeb.Tests/ExternalApiBatteryVersionTests.cs BatteryPassWeb.Tests/LocalAdminEditableFieldPolicyTests.cs BatteryPassWeb.Tests/MobileThemeSessionLayoutTests.cs BatteryPassWeb.Tests/Phase6ADocumentationTests.cs BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs web/Controllers/AdminController.cs web/Controllers/ClusterAdminController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Program.cs web/Views/Admin/Clusters.cshtml web/Views/Admin/Help.cshtml web/Views/Shared/_AdminTabs.cshtml
git commit -m "feat(dpp): point the menu at the new pages and remove the old family editor and editable fields"
```


---

### Task 8: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  1820, Skipped:     0, Total:  1820`: the 1635 from before, 191 new in Tasks 1–6, and 6 fewer after Task 7, where the old pages' tests go and the menu tests come.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

- [ ] **Step 4: Look at the pages**

Start the app from your PC against `battery_pass_v3`, as for the earlier steps, with `APP_BASE_URL` set too, and sign in as a team member. Never point it at `battery_pass_demo`. Check, in light and dark theme:

1. **Battery families** (menu): each family card says what is still missing, or "Complete"; "Add battery family" creates one and opens it.
2. **A family page:**
   - rename it, and set a PNG picture;
   - add a model "Start from" a copy of another, and see its cards wrap rather than scroll;
   - in the model editor, switch "Show only the values still needed" on and off;
   - change a value and save: the page returns to the same section and place, and the save bar says what was saved;
   - empty a field and save: the value is removed;
   - edit a field, then Cancel edits: it asks first.
3. **A batch page**, opened from the family page's batches.
4. **Sites and operators** (menu): open a site and an operator. Their few values show in one column, without the side menu (records with 5 values or fewer have none).
5. **Audit log:** the record-type filter offers the new types, and a save shows the value it replaced.
6. **A narrow window** (about 768 px): no page scrolls sideways, and the editor's menu is a row of tabs.

Remove the records you created; nothing uses them, so their pages offer Remove.

## After this plan

- **Plan 2d-2, batteries:** the batteries list, creating a battery, and the battery page (values, live values, cluster, publication checks, publishing, versions, accident reports), replacing "Batteries". It also restores the parity items of design section 14: #11, #17 and #19 fixed once published; a duplicate serial number refused; a battery's cluster must exist; deleting a cluster also counts its new packs; the list search; removing a draft pack. Optional: the SHA-256 check on the Conformance page, and the cluster assignments list folded into the batteries list.
- **Launcher:** if the launcher that starts the new version on your PC doesn't set `APP_BASE_URL` yet, add it beside `MONGODB_URI` and `MONGODB_DB`, so uploads can link their documents.
- **Known small gap:** the family name form on the family page has no stamp of its own, so two people renaming a family at once both succeed, the later name winning. The model, batch, site and operator names are stamped with their editors.
- **Step 3** adds the access-checked download route and the accident page that the document links already point to, and rewrites the help pages for the new model.

