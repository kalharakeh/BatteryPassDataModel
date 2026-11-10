# DPP V3 Step 3d-3: Demo Data — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A demo data set that shows the app's features, which the team can reset or clear from one "Demo data" card on the admin pages, without touching real data (decision 32). It brings back the old app's demo button on the new model.

**Architecture:** Everything lives in `web/Dpp/Demo`.
- **The catalogue:** `DemoCatalogue` reads the embedded `demo-catalogue.json` and the two family pictures; `DemoPdf` makes the demo documents.
- **The service:** `DemoDataService` seeds through the app's own paths: `ProductDataPages`, `PackCreationService`, `SavePipeline`, `PublicationService`, `DocumentService`, `BatteryPages` and `BatteryApi`. So every value is checked, logged and signed as usual. It marks what it makes, clears only marked records, and resets.
- **The demo mark:** the six record classes get an `IsDemo` property.
- **The setting:** `BatteryPassOptions` gets `DemoUsersPassword` (`DEMO_USERS_PASSWORD`).
- **The admin page:** `AdminController` gets two actions; the Registered clusters tab gets the card; a new `_GeneratedCredentialsModal` shows the new tokens once.

**Tech Stack:** .NET 10, ASP.NET Core MVC with Razor views, MongoDB.Driver 3.9 (GridFS), xUnit 2.9, EphemeralMongo 3.2.0. Nothing new is downloaded; the two pictures come from the repository's own tag.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`: section 14, the 3d-3 bullet. Decisions record: decision 32. Earlier plans: steps 1–3d-2 in `docs/superpowers/plans/`.

---

## Before you start

- Work in the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only). This plan starts at c86b093 (the end of plan 3d-2's run), plus the commit that adds this plan. Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree (localhost:5186), it locks `web/bin/Debug`, and the builds below fail with MSB3027. Never stop it yourself: ask the product owner to close it.
- **Nothing here touches Atlas.** The tests use a throwaway MongoDB (EphemeralMongo). Don't press the new card against `battery_pass_v3`: the first demo data there is the product owner's, after this plan.
- **Baseline:** `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj` shows `Passed:  2042`. After this plan: 2078. The suite takes about two minutes.
- **Patches:** changed files are given as patches. Save each block to the file it names, outside the worktree (e.g. `../3d3-task1-code.patch`), and run `git apply ../<name>` from the worktree root. New and rewritten files are given whole. The two pictures come back from the tag with `git show`.
- **Commits:** each task's commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **Old app:** keep the tag `app-v1.2-before-dpp-v3` and the branch `security/demo-api-hardening`. The running app on Elastic Beanstalk and its database `battery_pass_demo` are never touched.

## Decisions taken with the product owner (30 Oct 2026, decision 32, details through the managing session)

1. **One "Demo data" card** on the team's admin pages, on the Registered clusters tab. It has two buttons:
   - **Reset demo data** removes the demo records and seeds them again.
   - **Clear demo data** removes them and keeps the real data.

   Both ask first through the page's confirm dialog. Both are logged, as "demo.seeded" and "demo.cleared". Team admins only.
2. **What the seed makes:**
   - **Families and models:** family Compact with models Compact 7M and Compact 13M, and family Core with model Core.
   - **Product data:** two sites, two operators and three batches.
   - **Batteries:** six, in several states.
   - **Clusters:** three, `demo-north`, `demo-south` and `demo-fleet`.
   - **Accounts:** one for every role but the team admin, plus a cluster admin and a member in each demo cluster.
   - **API tokens:** a read and a read-write token per demo cluster, and one team token with full access.
3. **The demo mark** goes on every record the seed makes.
   - Families, models, sites, batches, operators and batteries carry the `IsDemo` property.
   - Accounts, memberships, clusters and tokens carry the `isDemo` field.
   - Passport versions, documents and stored files go with the record they belong to.
4. **Clearing never breaks real data.** A demo record that real data uses stays, loses its mark and is listed, for example "Kept Compact 7M: 1 real batches use it".
5. **Demo accounts:**
   - Their password comes from `DEMO_USERS_PASSWORD`, never from code. Without it, everything but the accounts is seeded, and the card says so.
   - There is no demo team admin. The card tells the team to sign in with their own team account.
6. **Token values** are shown once, in a list window with a label, the access, the value and a copy button for each. They are never stored in plain text or logged.
7. **Passports** go through the normal publication, so their signatures verify. Identifiers are TEST- ones.
8. **No status change** until step 4 brings the status-change flow. Step 4 then adds a status-change demo battery to the seed.
9. **The old demo's numbers:**
   - Kept per product: modules, mass, capacity, nominal voltage, power, lifetime, cycles, carbon footprint and class, recycled content.
   - Its errors are fixed: each product has its own voltage limits around its nominal voltage, the hazardous substance is one real electrolyte substance with its own CAS number (LiPF6, 21324-40-3), and the carbon stages add up to the footprint.
   - The V3 file has no dimensions attribute, so no dimensions are set.
10. **Pictures:** the old Compact 7M and Core pictures, back from the tag, become the Compact and Core family pictures. Pictures belong to families, so Compact 13M's isn't used.

## Judgement calls in this plan

1. **Fixed names:** the cluster ids and the `demo.*@example.test` emails are fixed. A real record that already has one of them is left alone and listed. Demo batteries still go into such a real cluster.
2. **Fresh ids on every seed.** Families, models, sites, batches, operators and batteries get new ids each time, so the history of cleared demo batteries in the audit log never mixes with new ones. The audit log keeps those entries.
3. **Clearing removes published demo passports** with their versions. That is the one exception to "a published passport stays" (design 14), since they are test data.
4. **The six batteries:**

   | Battery | State | Also has |
   |---|---|---|
   | DEMO-C7M-0001 | published, then version 2 (its passport host changes) | seven days of live values, an accident report, 7 modules |
   | DEMO-C7M-0002 | a complete draft | |
   | DEMO-C13M-0001 | published | the live values, 13 modules |
   | DEMO-CORE-0001 | published | the live values, 10 modules, an accident report |
   | DEMO-C13M-0002 | a draft missing its manufacturing date and warranty period, so the publication checks show | |
   | DEMO-CORE-0002 | the same kind of draft | |

   - **The live values** are written every six hours for seven days: state of charge, cycles, temperature and E1–E4. E3 and E4 stay at 0, since charging in extreme temperatures is prevented.
   - **Restricted documents:** four per model and a test report per batch, as small PDFs the app makes.
5. **When seeding refuses:** when demo data is already there (reset or clear first), and without `APP_BASE_URL`. Reset checks for `APP_BASE_URL` before it clears anything.
6. **Data and code are separate.** The data is a JSON file in the app; the logic is in code. Values use the shapes the validator accepts. The seed stops with a clear message if any value is refused. Whatever it made up to that point is marked, so Clear removes it.
7. **The token window scrolls**, so its Okay button stays within reach with seven tokens.
8. **The seed takes about five seconds:** three publications, seventeen uploads and eighty-four live-value saves.

## File structure

| File | Responsibility |
|---|---|
| `web/Dpp/Store/DppRecords.cs` (modify) | `IsDemo` on the six record classes |
| `web/Configuration/BatteryPassOptions.cs`, `web/Program.cs` (modify) | `DemoUsersPassword` from `DEMO_USERS_PASSWORD`; the service registered |
| `web/BatteryPassWeb.csproj` (modify) | The demo catalogue and pictures embedded in the app |
| `web/Dpp/Demo/demo-catalogue.json`, `Pictures/*.png` | What the demo data is made of |
| `web/Dpp/Demo/DemoCatalogue.cs`, `DemoPdf.cs` | Reading the catalogue and pictures; the demo PDFs |
| `web/Dpp/Demo/DemoDataService.cs` | Seed, clear and reset |
| `web/Controllers/AdminController.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs` (modify) | The two actions and what the card shows |
| `web/Views/Admin/Clusters.cshtml` (modify), `web/Views/Shared/_GeneratedCredentialsModal.cshtml` | The card and the one-time token list |
| `BatteryPassWeb.Tests/Demo/*` | The tests, on a throwaway database |


---

### Task 1: The demo mark and the demo catalogue

The records get their demo mark, the app gets the `DEMO_USERS_PASSWORD` setting, and the catalogue of what the demo data is made of goes into the app with the two old pictures. Nothing is seeded yet.

**Files:**
- Test: `BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`
- Create: `web/Dpp/Demo/DemoCatalogue.cs`
- Create: `web/Dpp/Demo/DemoPdf.cs`
- Create (from the tag): `web/Dpp/Demo/Pictures/compact.png`
- Create (from the tag): `web/Dpp/Demo/Pictures/core.png`
- Create: `web/Dpp/Demo/demo-catalogue.json`
- Modify: `web/BatteryPassWeb.csproj`
- Modify: `web/Configuration/BatteryPassOptions.cs`
- Modify: `web/Dpp/Store/DppRecords.cs`
- Modify: `web/Program.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs`:

```csharp
using System.Text;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: what the demo data is made of, with the old demo's errors fixed (decision 32).</summary>
public sealed class DemoCatalogueTests
{
    private static readonly DemoCatalogue Catalogue = DemoCatalogue.Load();

    [Fact]
    public void TheCatalogue_ShouldHaveTheOldDemosFamiliesAndModels()
    {
        Assert.Equal(["Compact", "Core"], Catalogue.Families.Select(family => family.Name));
        Assert.Equal([("Compact 7M", "compact", 7), ("Compact 13M", "compact", 13), ("Core", "core", 10)], Catalogue.Models.Select(model => (model.Name, model.Family, model.Modules)));
        Assert.All(Catalogue.Models, model => Assert.False(string.IsNullOrWhiteSpace(model.ModuleDescription)));
    }

    [Theory]
    [InlineData("compact-7m", 400, 320, 460, 320)]
    [InlineData("compact-13m", 720, 576, 828, 565)]
    [InlineData("core", 640, 512, 736, 470)]
    public void EachModel_ShouldHaveItsOwnVoltagesAndMass(string key, int nominal, int minimum, int maximum, int mass)
    {
        var values = Catalogue.Models.Single(model => model.Key == key).Values;

        Assert.Equal(nominal, values["nominalVoltage"]!["voltValue"]!.GetValue<int>());
        Assert.Equal(minimum, values["minimumVoltage"]!["voltValue"]!.GetValue<int>());
        Assert.Equal(maximum, values["maximumVoltage"]!["voltValue"]!.GetValue<int>());
        Assert.Equal(mass, values["batteryMass"]!["gramKilogramValue"]!.GetValue<int>());
    }

    [Fact]
    public void TheCarbonStages_ShouldAddUpToTheFootprint()
    {
        string[] stages = ["contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage", "contributionOfMainProductProductionLifecycleStage", "contributionOfDistributionLifecycleStage", "contributionOfEndOfLifeAndRecyclingLifecycleStage"];
        foreach (var batch in Catalogue.Batches)
        {
            var total = batch.Values["batteryCarbonFootprintPerFunctionalUnit"]!["kgCO2EquivalentPerKilowattHourValue"]!.GetValue<decimal>();
            Assert.Equal(total, stages.Sum(stage => batch.Values[stage]!["kgCO2EquivalentPerKilowattHourValue"]!.GetValue<decimal>()));
        }
    }

    [Fact]
    public void TheHazardousSubstance_ShouldBeARealOneWithItsOwnCasNumber()
    {
        foreach (var model in Catalogue.Models)
        {
            var substance = model.Values["hazardousSubstancesAndTheirImpact"]!.AsArray().Single()!;
            Assert.Equal(("Lithium hexafluorophosphate", "21324-40-3"), (substance["chemicalName"]!.GetValue<string>(), substance["identifier"]!.GetValue<string>()));
        }
    }

    [Fact]
    public void TheText_ShouldHaveNoGarbledCharacters()
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(DppTestFiles.RepoFile("web", "Dpp", "Demo", "demo-catalogue.json")));

        Assert.DoesNotContain("Â", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", text, StringComparison.Ordinal);
        Assert.Contains("°C", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBatteries_ShouldBeInSeveralStates()
    {
        Assert.Equal(["DEMO-C7M-0001", "DEMO-C13M-0001", "DEMO-CORE-0001"], Catalogue.Packs.Where(pack => pack.Publish).Select(pack => pack.Serial));
        Assert.Equal(["DEMO-C7M-0001"], Catalogue.Packs.Where(pack => pack.NewVersion is not null).Select(pack => pack.Serial));
        Assert.Equal(2, Catalogue.Packs.Count(pack => pack.Accident is not null));
        Assert.Equal(2, Catalogue.Packs.Count(pack => !pack.Values.ContainsKey("manufacturingDate")));
        Assert.All(Catalogue.Packs, pack => Assert.False(pack.Values.ContainsKey("batteryStatus"), "the status is set when the battery is made (step 4 changes it)"));
    }

    [Fact]
    public void TheAccounts_ShouldCoverEveryRoleButTheTeamAdmin()
    {
        Assert.Equal(["commission", "liRecycling", "liRepair", "liTreatment", "marketSurveillanceAuthority", "member", "notifiedBody"], Catalogue.Accounts.Select(account => account.Role).Distinct().Order());
        Assert.All(Catalogue.Accounts, account => Assert.EndsWith("@example.test", account.Email, StringComparison.Ordinal));
        Assert.Equal(["demo-fleet", "demo-north", "demo-south"], Catalogue.Clusters.Select(cluster => cluster.Id).Order());
        Assert.Equal(3, Catalogue.Accounts.Count(account => account.ClusterRole == "clusterAdmin"));
    }

    [Fact]
    public void ThePictures_ShouldBeTheOldDemosPngFiles()
    {
        foreach (var family in Catalogue.Families)
        {
            Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], DemoCatalogue.Picture(family.Picture).Take(4));
        }
    }

    [Fact]
    public void ADemoDocument_ShouldBeAPdfThatOpens()
    {
        var pdf = Encoding.ASCII.GetString(DemoPdf.Make("Dismantling manual - Compact 7M", "Model document (with notes)."));

        Assert.StartsWith("%PDF-1.7\n", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", pdf, StringComparison.Ordinal);
        Assert.Contains("(Dismantling manual - Compact 7M) Tj", pdf, StringComparison.Ordinal);
        Assert.Contains("(Model document \\(with notes\\).) Tj", pdf, StringComparison.Ordinal);
        var startxref = int.Parse(pdf[(pdf.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref\n", pdf[startxref..], StringComparison.Ordinal);
    }

    [Fact]
    public void TheDemoMark_ShouldBeOnEveryKindOfRecordTheSeedMakes()
    {
        foreach (var type in new[] { typeof(BatteryFamily), typeof(BatteryModel), typeof(ManufacturingSite), typeof(ProductionBatch), typeof(EconomicOperator), typeof(BatteryPack) })
        {
            Assert.Equal(typeof(bool), type.GetProperty("IsDemo")?.PropertyType);
        }

        Assert.Null(new BatteryPassOptions().DemoUsersPassword);
        Assert.Contains("DEMO_USERS_PASSWORD", File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs")), StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoCatalogueTests"`
Expected: the build fails: the `BatteryPassWeb.Dpp.Demo` namespace and `DemoCatalogue` don't exist yet (CS0234, CS0246).

- [ ] **Step 3: Write `DemoCatalogue.cs`**

`web/Dpp/Demo/DemoCatalogue.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BatteryPassWeb.Dpp.Demo;

public sealed record DemoCluster(string Id, string Name);

/// <summary>A demo account: one app role, and optionally a cluster with its role there.</summary>
public sealed record DemoAccount(string Email, string Name, string Role, string? Cluster = null, string? ClusterRole = null);

public sealed record DemoFamily(string Key, string Name, string Picture);

/// <summary>A battery model: its values, its module description and the restricted documents the seed uploads for it.</summary>
public sealed record DemoModel(string Key, string Family, string Name, int Modules, string ModuleDescription, Dictionary<string, string> Documents, JsonObject Values);

public sealed record DemoNamed(string Key, string Name, JsonObject Values);

public sealed record DemoBatch(string Key, string Model, string Site, int Year, string TestReport, JsonObject Values);

/// <summary>
/// A demo battery. <see cref="Publish"/> publishes it; <see cref="NewVersion"/> then changes these values, which makes
/// version 2; <see cref="History"/> writes seven days of live values; <see cref="Modules"/> imports that many modules
/// from an export-shaped file; <see cref="Accident"/> adds an accident report.
/// </summary>
public sealed record DemoPack(
    string Serial,
    string Batch,
    string Operator,
    string Cluster,
    JsonObject Values,
    bool Publish = false,
    JsonObject? NewVersion = null,
    bool History = false,
    int Modules = 0,
    string? Accident = null);

/// <summary>
/// What the demo data seed makes (decision 32): the Compact and Core families of the old demo on the new model, with
/// sites, operators, batches, batteries in several states, clusters and accounts. Read from the embedded
/// demo-catalogue.json.
/// </summary>
public sealed record DemoCatalogue(
    IReadOnlyList<DemoCluster> Clusters,
    IReadOnlyList<DemoAccount> Accounts,
    IReadOnlyList<DemoFamily> Families,
    IReadOnlyList<DemoModel> Models,
    IReadOnlyList<DemoNamed> Sites,
    IReadOnlyList<DemoNamed> Operators,
    IReadOnlyList<DemoBatch> Batches,
    IReadOnlyList<DemoPack> Packs)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static DemoCatalogue Load()
    {
        using var stream = Resource("demo-catalogue.json");
        return JsonSerializer.Deserialize<DemoCatalogue>(stream, Json) ?? throw new InvalidOperationException("The demo catalogue is empty.");
    }

    /// <summary>A family picture from the old demo (tag app-v1.2-before-dpp-v3).</summary>
    public static byte[] Picture(string fileName)
    {
        using var stream = Resource(fileName);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static Stream Resource(string fileName) =>
        typeof(DemoCatalogue).Assembly.GetManifestResourceStream($"Dpp.Demo.{fileName}")
        ?? throw new InvalidOperationException($"The demo file {fileName} is missing from the app.");
}
```

- [ ] **Step 4: Write `DemoPdf.cs`**

`web/Dpp/Demo/DemoPdf.cs`:

```csharp
using System.Text;

namespace BatteryPassWeb.Dpp.Demo;

/// <summary>A one-page PDF with a title and a line of text, made by hand so the demo documents open in any viewer.</summary>
public static class DemoPdf
{
    public static byte[] Make(string title, string line)
    {
        var content = $"BT /F1 18 Tf 72 770 Td ({Escape(title)}) Tj 0 -30 Td /F1 11 Tf ({Escape(line)}) Tj 0 -18 Td (Demo data: this document is made up.) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>PDF text: ASCII only, with the three characters that would end the string escaped.</summary>
    private static string Escape(string text) =>
        new string(text.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
```

- [ ] **Step 5: Restore `compact.png`**

It is a picture from the old demo, so it comes back from the tag `app-v1.2-before-dpp-v3` instead of being shown here:

```bash
mkdir -p web/Dpp/Demo/Pictures
git show app-v1.2-before-dpp-v3:web/wwwroot/images/compact7.png > web/Dpp/Demo/Pictures/compact.png
```

- [ ] **Step 6: Restore `core.png`**

It is a picture from the old demo, so it comes back from the tag `app-v1.2-before-dpp-v3` instead of being shown here:

```bash
mkdir -p web/Dpp/Demo/Pictures
git show app-v1.2-before-dpp-v3:web/wwwroot/images/core.png > web/Dpp/Demo/Pictures/core.png
```

- [ ] **Step 7: Write `demo-catalogue.json`**

`web/Dpp/Demo/demo-catalogue.json`:

```json
{
  "clusters": [
    {
      "id": "demo-north",
      "name": "Demo North Operations"
    },
    {
      "id": "demo-south",
      "name": "Demo South Operations"
    },
    {
      "id": "demo-fleet",
      "name": "Demo Fleet Operations"
    }
  ],
  "accounts": [
    {
      "email": "demo.notified-body@example.test",
      "name": "Demo notified body",
      "role": "notifiedBody"
    },
    {
      "email": "demo.msa@example.test",
      "name": "Demo market surveillance",
      "role": "marketSurveillanceAuthority"
    },
    {
      "email": "demo.commission@example.test",
      "name": "Demo Commission",
      "role": "commission"
    },
    {
      "email": "demo.repair@example.test",
      "name": "Demo repair shop",
      "role": "liRepair"
    },
    {
      "email": "demo.recycling@example.test",
      "name": "Demo recycler",
      "role": "liRecycling"
    },
    {
      "email": "demo.treatment@example.test",
      "name": "Demo treatment facility",
      "role": "liTreatment"
    },
    {
      "email": "demo.north.admin@example.test",
      "name": "Demo north cluster admin",
      "role": "member",
      "cluster": "demo-north",
      "clusterRole": "clusterAdmin"
    },
    {
      "email": "demo.north.user@example.test",
      "name": "Demo north user",
      "role": "member",
      "cluster": "demo-north",
      "clusterRole": "member"
    },
    {
      "email": "demo.south.admin@example.test",
      "name": "Demo south cluster admin",
      "role": "member",
      "cluster": "demo-south",
      "clusterRole": "clusterAdmin"
    },
    {
      "email": "demo.south.user@example.test",
      "name": "Demo south user",
      "role": "member",
      "cluster": "demo-south",
      "clusterRole": "member"
    },
    {
      "email": "demo.fleet.admin@example.test",
      "name": "Demo fleet cluster admin",
      "role": "member",
      "cluster": "demo-fleet",
      "clusterRole": "clusterAdmin"
    },
    {
      "email": "demo.fleet.user@example.test",
      "name": "Demo fleet user",
      "role": "member",
      "cluster": "demo-fleet",
      "clusterRole": "member"
    }
  ],
  "families": [
    {
      "key": "compact",
      "name": "Compact",
      "picture": "compact.png"
    },
    {
      "key": "core",
      "name": "Core",
      "picture": "core.png"
    }
  ],
  "models": [
    {
      "key": "compact-7m",
      "family": "compact",
      "name": "Compact 7M",
      "modules": 7,
      "moduleDescription": "Seven liquid-cooled NMC modules in one pack housing, with the battery management unit at the front.",
      "documents": {
        "dismantlingInformation": "Dismantling manual",
        "partNumbersForComponents": "Part numbers for components",
        "informationOnSourcesOfSpareParts": "Sources of spare parts",
        "safetyMeasures": "Safety measures"
      },
      "values": {
        "batteryCategory": {
          "batteryCategoryValue": "industrial/non-stationary battery"
        },
        "batteryMass": {
          "gramKilogramUnit": "kg",
          "gramKilogramValue": 320
        },
        "separateCollectionSymbol": {
          "contentType": "image/svg+xml",
          "url": "https://www.example.com/battery/symbols/separate-collection.svg",
          "language": "en-GB",
          "resourceTitle": "Separate collection symbol"
        },
        "symbolsForCadmiumAndLead": {
          "contentType": "image/svg+xml",
          "url": "https://www.example.com/battery/symbols/cd-pb.svg",
          "language": "en-GB",
          "resourceTitle": "Symbols for cadmium and lead"
        },
        "extinguishingAgent": {
          "agentFireClass": "B",
          "extinguishingAgent": "Water"
        },
        "meaningOfLabelsAndSymbols": "The crossed-out wheeled bin means the battery must be collected separately from other waste.",
        "informationOfDueDiligenceReport": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/demo/due-diligence-report.pdf",
          "language": "en-GB",
          "resourceTitle": "Due diligence report"
        },
        "batteryChemistry": {
          "chemicalCodeValue": "Li-ion NMC"
        },
        "criticalRawMaterials": [
          "Cobalt",
          "Lithium",
          "Natural graphite",
          "Nickel"
        ],
        "materialsUsedInCathodeAnodeAndElectrolyte": [
          "Cathode: lithium nickel manganese cobalt oxide (NMC 811)",
          "Anode: graphite",
          "Electrolyte: LiPF6 in carbonate solvents"
        ],
        "hazardousSubstancesAndTheirImpact": [
          {
            "chemicalName": "Lithium hexafluorophosphate",
            "identifierType": "CAS No",
            "identifier": "21324-40-3",
            "concentration": 1.2,
            "hazardPhrases": [
              "H301",
              "H314",
              "H372"
            ],
            "precautionaryPhrases": [
              "P260",
              "P280",
              "P301+P310"
            ],
            "substanceLocation": "Electrolyte"
          }
        ],
        "informationOnTheRoleOfEndUsersInContributingToWastePrevention": {
          "contentType": "text/html",
          "url": "https://www.example.com/battery/waste-prevention",
          "language": "en-GB",
          "resourceTitle": "Your role in waste prevention"
        },
        "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries": {
          "contentType": "text/html",
          "url": "https://www.example.com/battery/separate-collection",
          "language": "en-GB",
          "resourceTitle": "Your role in separate collection"
        },
        "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/battery/collection-and-treatment.pdf",
          "language": "en-GB",
          "resourceTitle": "Collection, second life and treatment"
        },
        "ratedCapacity": {
          "amperehourMiliamperehourValue": 140,
          "ampereHourMiliamperehourUnit": "Ah"
        },
        "capacityFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "minimumVoltage": {
          "voltValue": 320,
          "voltUnit": "V"
        },
        "maximumVoltage": {
          "voltValue": 460,
          "voltUnit": "V"
        },
        "nominalVoltage": {
          "voltValue": 400,
          "voltUnit": "V",
          "temperatureRangeLowerBoundaryValue": -20,
          "temperatureRangeLowerBoundaryUnit": "°C",
          "temperatureRangeUpperBoundaryValue": 45,
          "temperatureRangeUpperBoundaryUnit": "°C"
        },
        "originalPowerCapability": {
          "wattValueAt80PercentStateOfCharge": 171000,
          "wattValueAt20PercentStateOfCharge": 126000,
          "wattUnit": "W"
        },
        "powerFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "maximumPermittedBatteryPower": {
          "wattValue": 180000,
          "wattUnit": "W"
        },
        "initialRoundTripEnergyEfficiency": {
          "percentUnit": "%",
          "percentageValue": 93
        },
        "roundTripEnergyEfficiencyAt50PerCentOfCycleLife": {
          "percentUnit": "%",
          "percentageValue": 90
        },
        "energyRoundTripEfficiencyFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "initialInternalResistanceOfBatteryCellAndPackModuleRecommended": {
          "ohmValueForBatteryPack": 1,
          "ohmValueForBatteryCell": 1,
          "ohmUnit": "Ohm"
        },
        "internalResistanceIncreaseOfPackCellAndModuleRecommended": {
          "internalResistanceIncreaseValueForBatteryPack": 0,
          "percentUnit": "%"
        },
        "expectedLifetimeInCalendarYears": 10,
        "expectedLifetimeNumberOfChargeDischargeCycles": 3000,
        "cycleLifeReferenceTest": "IEC 62660-1",
        "cRateOfRelevantCycleLifeTest": {
          "amperePerAmpereHourValue": 0.5,
          "amperePerAmpereHourUnit": "A/Ah"
        },
        "temperatureRangeIdleStateLowerBoundary": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": -20
        },
        "temperatureRangeIdleStateUpperBoundary": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 45
        }
      }
    },
    {
      "key": "compact-13m",
      "family": "compact",
      "name": "Compact 13M",
      "modules": 13,
      "moduleDescription": "Thirteen liquid-cooled NMC modules in two rows, with the battery management unit at the front.",
      "documents": {
        "dismantlingInformation": "Dismantling manual",
        "partNumbersForComponents": "Part numbers for components",
        "informationOnSourcesOfSpareParts": "Sources of spare parts",
        "safetyMeasures": "Safety measures"
      },
      "values": {
        "batteryCategory": {
          "batteryCategoryValue": "industrial/non-stationary battery"
        },
        "batteryMass": {
          "gramKilogramUnit": "kg",
          "gramKilogramValue": 565
        },
        "separateCollectionSymbol": {
          "contentType": "image/svg+xml",
          "url": "https://www.example.com/battery/symbols/separate-collection.svg",
          "language": "en-GB",
          "resourceTitle": "Separate collection symbol"
        },
        "symbolsForCadmiumAndLead": {
          "contentType": "image/svg+xml",
          "url": "https://www.example.com/battery/symbols/cd-pb.svg",
          "language": "en-GB",
          "resourceTitle": "Symbols for cadmium and lead"
        },
        "extinguishingAgent": {
          "agentFireClass": "B",
          "extinguishingAgent": "Water"
        },
        "meaningOfLabelsAndSymbols": "The crossed-out wheeled bin means the battery must be collected separately from other waste.",
        "informationOfDueDiligenceReport": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/demo/due-diligence-report.pdf",
          "language": "en-GB",
          "resourceTitle": "Due diligence report"
        },
        "batteryChemistry": {
          "chemicalCodeValue": "Li-ion NMC"
        },
        "criticalRawMaterials": [
          "Cobalt",
          "Lithium",
          "Natural graphite",
          "Nickel"
        ],
        "materialsUsedInCathodeAnodeAndElectrolyte": [
          "Cathode: lithium nickel manganese cobalt oxide (NMC 811)",
          "Anode: graphite",
          "Electrolyte: LiPF6 in carbonate solvents"
        ],
        "hazardousSubstancesAndTheirImpact": [
          {
            "chemicalName": "Lithium hexafluorophosphate",
            "identifierType": "CAS No",
            "identifier": "21324-40-3",
            "concentration": 1.2,
            "hazardPhrases": [
              "H301",
              "H314",
              "H372"
            ],
            "precautionaryPhrases": [
              "P260",
              "P280",
              "P301+P310"
            ],
            "substanceLocation": "Electrolyte"
          }
        ],
        "informationOnTheRoleOfEndUsersInContributingToWastePrevention": {
          "contentType": "text/html",
          "url": "https://www.example.com/battery/waste-prevention",
          "language": "en-GB",
          "resourceTitle": "Your role in waste prevention"
        },
        "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries": {
          "contentType": "text/html",
          "url": "https://www.example.com/battery/separate-collection",
          "language": "en-GB",
          "resourceTitle": "Your role in separate collection"
        },
        "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/battery/collection-and-treatment.pdf",
          "language": "en-GB",
          "resourceTitle": "Collection, second life and treatment"
        },
        "ratedCapacity": {
          "amperehourMiliamperehourValue": 260,
          "ampereHourMiliamperehourUnit": "Ah"
        },
        "capacityFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "minimumVoltage": {
          "voltValue": 576,
          "voltUnit": "V"
        },
        "maximumVoltage": {
          "voltValue": 828,
          "voltUnit": "V"
        },
        "nominalVoltage": {
          "voltValue": 720,
          "voltUnit": "V",
          "temperatureRangeLowerBoundaryValue": -20,
          "temperatureRangeLowerBoundaryUnit": "°C",
          "temperatureRangeUpperBoundaryValue": 45,
          "temperatureRangeUpperBoundaryUnit": "°C"
        },
        "originalPowerCapability": {
          "wattValueAt80PercentStateOfCharge": 304000,
          "wattValueAt20PercentStateOfCharge": 224000,
          "wattUnit": "W"
        },
        "powerFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "maximumPermittedBatteryPower": {
          "wattValue": 320000,
          "wattUnit": "W"
        },
        "initialRoundTripEnergyEfficiency": {
          "percentUnit": "%",
          "percentageValue": 93
        },
        "roundTripEnergyEfficiencyAt50PerCentOfCycleLife": {
          "percentUnit": "%",
          "percentageValue": 90
        },
        "energyRoundTripEfficiencyFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "initialInternalResistanceOfBatteryCellAndPackModuleRecommended": {
          "ohmValueForBatteryPack": 1,
          "ohmValueForBatteryCell": 1,
          "ohmUnit": "Ohm"
        },
        "internalResistanceIncreaseOfPackCellAndModuleRecommended": {
          "internalResistanceIncreaseValueForBatteryPack": 0,
          "percentUnit": "%"
        },
        "expectedLifetimeInCalendarYears": 10,
        "expectedLifetimeNumberOfChargeDischargeCycles": 3000,
        "cycleLifeReferenceTest": "IEC 62660-1",
        "cRateOfRelevantCycleLifeTest": {
          "amperePerAmpereHourValue": 0.5,
          "amperePerAmpereHourUnit": "A/Ah"
        },
        "temperatureRangeIdleStateLowerBoundary": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": -20
        },
        "temperatureRangeIdleStateUpperBoundary": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 45
        }
      }
    },
    {
      "key": "core",
      "family": "core",
      "name": "Core",
      "modules": 10,
      "moduleDescription": "Ten liquid-cooled NMC modules around a central cooling plate, with the battery management unit on top.",
      "documents": {
        "dismantlingInformation": "Dismantling manual",
        "partNumbersForComponents": "Part numbers for components",
        "informationOnSourcesOfSpareParts": "Sources of spare parts",
        "safetyMeasures": "Safety measures"
      },
      "values": {
        "batteryCategory": {
          "batteryCategoryValue": "industrial/non-stationary battery"
        },
        "batteryMass": {
          "gramKilogramUnit": "kg",
          "gramKilogramValue": 470
        },
        "separateCollectionSymbol": {
          "contentType": "image/svg+xml",
          "url": "https://www.example.com/battery/symbols/separate-collection.svg",
          "language": "en-GB",
          "resourceTitle": "Separate collection symbol"
        },
        "symbolsForCadmiumAndLead": {
          "contentType": "image/svg+xml",
          "url": "https://www.example.com/battery/symbols/cd-pb.svg",
          "language": "en-GB",
          "resourceTitle": "Symbols for cadmium and lead"
        },
        "extinguishingAgent": {
          "agentFireClass": "B",
          "extinguishingAgent": "Water"
        },
        "meaningOfLabelsAndSymbols": "The crossed-out wheeled bin means the battery must be collected separately from other waste.",
        "informationOfDueDiligenceReport": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/demo/due-diligence-report.pdf",
          "language": "en-GB",
          "resourceTitle": "Due diligence report"
        },
        "batteryChemistry": {
          "chemicalCodeValue": "Li-ion NMC"
        },
        "criticalRawMaterials": [
          "Cobalt",
          "Lithium",
          "Natural graphite",
          "Nickel"
        ],
        "materialsUsedInCathodeAnodeAndElectrolyte": [
          "Cathode: lithium nickel manganese cobalt oxide (NMC 811)",
          "Anode: graphite",
          "Electrolyte: LiPF6 in carbonate solvents"
        ],
        "hazardousSubstancesAndTheirImpact": [
          {
            "chemicalName": "Lithium hexafluorophosphate",
            "identifierType": "CAS No",
            "identifier": "21324-40-3",
            "concentration": 1.2,
            "hazardPhrases": [
              "H301",
              "H314",
              "H372"
            ],
            "precautionaryPhrases": [
              "P260",
              "P280",
              "P301+P310"
            ],
            "substanceLocation": "Electrolyte"
          }
        ],
        "informationOnTheRoleOfEndUsersInContributingToWastePrevention": {
          "contentType": "text/html",
          "url": "https://www.example.com/battery/waste-prevention",
          "language": "en-GB",
          "resourceTitle": "Your role in waste prevention"
        },
        "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries": {
          "contentType": "text/html",
          "url": "https://www.example.com/battery/separate-collection",
          "language": "en-GB",
          "resourceTitle": "Your role in separate collection"
        },
        "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/battery/collection-and-treatment.pdf",
          "language": "en-GB",
          "resourceTitle": "Collection, second life and treatment"
        },
        "ratedCapacity": {
          "amperehourMiliamperehourValue": 220,
          "ampereHourMiliamperehourUnit": "Ah"
        },
        "capacityFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "minimumVoltage": {
          "voltValue": 512,
          "voltUnit": "V"
        },
        "maximumVoltage": {
          "voltValue": 736,
          "voltUnit": "V"
        },
        "nominalVoltage": {
          "voltValue": 640,
          "voltUnit": "V",
          "temperatureRangeLowerBoundaryValue": -20,
          "temperatureRangeLowerBoundaryUnit": "°C",
          "temperatureRangeUpperBoundaryValue": 45,
          "temperatureRangeUpperBoundaryUnit": "°C"
        },
        "originalPowerCapability": {
          "wattValueAt80PercentStateOfCharge": 266000,
          "wattValueAt20PercentStateOfCharge": 196000,
          "wattUnit": "W"
        },
        "powerFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "maximumPermittedBatteryPower": {
          "wattValue": 280000,
          "wattUnit": "W"
        },
        "initialRoundTripEnergyEfficiency": {
          "percentUnit": "%",
          "percentageValue": 93
        },
        "roundTripEnergyEfficiencyAt50PerCentOfCycleLife": {
          "percentUnit": "%",
          "percentageValue": 90
        },
        "energyRoundTripEfficiencyFade": {
          "percentUnit": "%",
          "percentageValue": 0
        },
        "initialInternalResistanceOfBatteryCellAndPackModuleRecommended": {
          "ohmValueForBatteryPack": 1,
          "ohmValueForBatteryCell": 1,
          "ohmUnit": "Ohm"
        },
        "internalResistanceIncreaseOfPackCellAndModuleRecommended": {
          "internalResistanceIncreaseValueForBatteryPack": 0,
          "percentUnit": "%"
        },
        "expectedLifetimeInCalendarYears": 10,
        "expectedLifetimeNumberOfChargeDischargeCycles": 3500,
        "cycleLifeReferenceTest": "IEC 62660-1",
        "cRateOfRelevantCycleLifeTest": {
          "amperePerAmpereHourValue": 0.5,
          "amperePerAmpereHourUnit": "A/Ah"
        },
        "temperatureRangeIdleStateLowerBoundary": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": -20
        },
        "temperatureRangeIdleStateUpperBoundary": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 45
        }
      }
    }
  ],
  "sites": [
    {
      "key": "plant-north",
      "name": "Demo plant North",
      "values": {
        "uniqueManufacturerIdentifier": "urn:x-demo:manufacturer:plant-north",
        "manufacturerInformation": {
          "name": "Scania Industrial Batteries (demo)",
          "registeredTradeNameOrRegisteredTrademark": "Scania Industrial Batteries",
          "postalAddress": "Demo Street 1, 111 11 Stockholm, Sweden",
          "webAddress": "https://www.example.com",
          "eMailAddress": "batteries@example.com"
        },
        "manufacturingPlace": "Demo plant North, Stockholm, Sweden"
      }
    },
    {
      "key": "plant-south",
      "name": "Demo plant South",
      "values": {
        "uniqueManufacturerIdentifier": "urn:x-demo:manufacturer:plant-south",
        "manufacturerInformation": {
          "name": "Scania Industrial Batteries (demo)",
          "registeredTradeNameOrRegisteredTrademark": "Scania Industrial Batteries",
          "postalAddress": "Demo Street 1, 111 11 Stockholm, Sweden",
          "webAddress": "https://www.example.com",
          "eMailAddress": "batteries@example.com"
        },
        "manufacturingPlace": "Demo plant South, Malmo, Sweden"
      }
    }
  ],
  "operators": [
    {
      "key": "batteries",
      "name": "Scania Industrial Batteries (demo)",
      "values": {
        "economicOperatorId": "urn:x-demo:operator:industrial-batteries",
        "economicOperatorInformation": {
          "name": "Scania Industrial Batteries (demo)",
          "registeredTradeNameOrRegisteredTrademark": "Scania Industrial Batteries",
          "postalAddress": "Demo Street 1, 111 11 Stockholm, Sweden",
          "webAddress": "https://www.example.com",
          "eMailAddress": "batteries@example.com"
        }
      }
    },
    {
      "key": "fleet",
      "name": "Demo Fleet Services AB",
      "values": {
        "economicOperatorId": "urn:x-demo:operator:fleet-services",
        "economicOperatorInformation": {
          "name": "Demo Fleet Services AB",
          "registeredTradeNameOrRegisteredTrademark": "Demo Fleet Services",
          "postalAddress": "Fleet Road 2, 222 22 Lund, Sweden",
          "webAddress": "https://fleet.example.com",
          "eMailAddress": "fleet@example.com"
        }
      }
    }
  ],
  "batches": [
    {
      "key": "compact-7m",
      "model": "compact-7m",
      "site": "plant-north",
      "year": 2026,
      "testReport": "Compact 7M test report 2026",
      "values": {
        "batteryModelIdentifier": "CP7M",
        "carbonFootprintLabel": {
          "contentType": "image/png",
          "url": "https://www.example.com/demo/compact-7m/carbon-footprint-label.png",
          "language": "en-GB",
          "resourceTitle": "Carbon footprint label"
        },
        "euDeclarationOfConformity": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/demo/compact-7m/eu-declaration-of-conformity.pdf",
          "language": "en-GB",
          "resourceTitle": "EU declaration of conformity"
        },
        "batteryCarbonFootprintPerFunctionalUnit": {
          "kgCO2EquivalentPerKilowattHourValue": 64,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 34,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfMainProductProductionLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 21,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfDistributionLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 3,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfEndOfLifeAndRecyclingLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 6,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "carbonFootprintPerformanceClass": "Class B",
        "webLinkToPublicCarbonFootprintStudy": "https://www.example.com/demo/compact-7m/carbon-footprint-study",
        "nickelRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 30
        },
        "cobaltRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 26
        },
        "lithiumRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 23
        },
        "leadRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 35
        },
        "renewableContentShare": {
          "percentUnit": "%",
          "percentageValue": 38
        }
      }
    },
    {
      "key": "compact-13m",
      "model": "compact-13m",
      "site": "plant-north",
      "year": 2026,
      "testReport": "Compact 13M test report 2026",
      "values": {
        "batteryModelIdentifier": "CP13M",
        "carbonFootprintLabel": {
          "contentType": "image/png",
          "url": "https://www.example.com/demo/compact-13m/carbon-footprint-label.png",
          "language": "en-GB",
          "resourceTitle": "Carbon footprint label"
        },
        "euDeclarationOfConformity": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/demo/compact-13m/eu-declaration-of-conformity.pdf",
          "language": "en-GB",
          "resourceTitle": "EU declaration of conformity"
        },
        "batteryCarbonFootprintPerFunctionalUnit": {
          "kgCO2EquivalentPerKilowattHourValue": 67,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 36,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfMainProductProductionLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 22,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfDistributionLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 3,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfEndOfLifeAndRecyclingLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 6,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "carbonFootprintPerformanceClass": "Class B",
        "webLinkToPublicCarbonFootprintStudy": "https://www.example.com/demo/compact-13m/carbon-footprint-study",
        "nickelRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 32
        },
        "cobaltRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 28
        },
        "lithiumRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 25
        },
        "leadRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 37
        },
        "renewableContentShare": {
          "percentUnit": "%",
          "percentageValue": 38
        }
      }
    },
    {
      "key": "core",
      "model": "core",
      "site": "plant-south",
      "year": 2026,
      "testReport": "Core test report 2026",
      "values": {
        "batteryModelIdentifier": "CORE",
        "carbonFootprintLabel": {
          "contentType": "image/png",
          "url": "https://www.example.com/demo/core/carbon-footprint-label.png",
          "language": "en-GB",
          "resourceTitle": "Carbon footprint label"
        },
        "euDeclarationOfConformity": {
          "contentType": "application/pdf",
          "url": "https://www.example.com/demo/core/eu-declaration-of-conformity.pdf",
          "language": "en-GB",
          "resourceTitle": "EU declaration of conformity"
        },
        "batteryCarbonFootprintPerFunctionalUnit": {
          "kgCO2EquivalentPerKilowattHourValue": 59,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 31,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfMainProductProductionLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 20,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfDistributionLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 3,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "contributionOfEndOfLifeAndRecyclingLifecycleStage": {
          "kgCO2EquivalentPerKilowattHourValue": 5,
          "kgCO2EquivalentPerKilowattHourUnit": "kgCO2-eq/kWh"
        },
        "carbonFootprintPerformanceClass": "Class A",
        "webLinkToPublicCarbonFootprintStudy": "https://www.example.com/demo/core/carbon-footprint-study",
        "nickelRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 40
        },
        "cobaltRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 34
        },
        "lithiumRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 30
        },
        "leadRecycledContentShare": {
          "percentUnit": "%",
          "percentageValue": 44
        },
        "renewableContentShare": {
          "percentUnit": "%",
          "percentageValue": 38
        }
      }
    }
  ],
  "packs": [
    {
      "serial": "DEMO-C7M-0001",
      "batch": "compact-7m",
      "operator": "batteries",
      "cluster": "demo-north",
      "values": {
        "batterySerialNumber": "DEMO-C7M-0001",
        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
        "manufacturingDate": "2026-07",
        "warrantyPeriod": "P8Y",
        "stateOfChargeSoC": {
          "percentUnit": "%",
          "percentageValue": 76
        },
        "numberOfFullChargingAndDischargingCycles": 412,
        "temperatureInformation": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 23
        }
      },
      "publish": true,
      "newVersion": {
        "thirdPartyServiceProvider": "Demo Passport Hosting AB, Nordic region"
      },
      "history": true,
      "modules": 7,
      "accident": "Coolant leak found at the 12-month service; module 4 replaced."
    },
    {
      "serial": "DEMO-C7M-0002",
      "batch": "compact-7m",
      "operator": "batteries",
      "cluster": "demo-north",
      "values": {
        "batterySerialNumber": "DEMO-C7M-0002",
        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
        "manufacturingDate": "2026-07",
        "warrantyPeriod": "P8Y",
        "stateOfChargeSoC": {
          "percentUnit": "%",
          "percentageValue": 95
        },
        "numberOfFullChargingAndDischargingCycles": 3,
        "temperatureInformation": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 21
        }
      }
    },
    {
      "serial": "DEMO-C13M-0001",
      "batch": "compact-13m",
      "operator": "batteries",
      "cluster": "demo-south",
      "values": {
        "batterySerialNumber": "DEMO-C13M-0001",
        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
        "manufacturingDate": "2026-07",
        "warrantyPeriod": "P8Y",
        "stateOfChargeSoC": {
          "percentUnit": "%",
          "percentageValue": 64
        },
        "numberOfFullChargingAndDischargingCycles": 655,
        "temperatureInformation": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 25
        }
      },
      "publish": true,
      "history": true,
      "modules": 13
    },
    {
      "serial": "DEMO-C13M-0002",
      "batch": "compact-13m",
      "operator": "batteries",
      "cluster": "demo-south",
      "values": {
        "batterySerialNumber": "DEMO-C13M-0002",
        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
        "stateOfChargeSoC": {
          "percentUnit": "%",
          "percentageValue": 90
        },
        "numberOfFullChargingAndDischargingCycles": 0,
        "temperatureInformation": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 20
        }
      }
    },
    {
      "serial": "DEMO-CORE-0001",
      "batch": "core",
      "operator": "fleet",
      "cluster": "demo-fleet",
      "values": {
        "batterySerialNumber": "DEMO-CORE-0001",
        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
        "manufacturingDate": "2026-07",
        "warrantyPeriod": "P8Y",
        "stateOfChargeSoC": {
          "percentUnit": "%",
          "percentageValue": 58
        },
        "numberOfFullChargingAndDischargingCycles": 1210,
        "temperatureInformation": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 27
        }
      },
      "publish": true,
      "history": true,
      "modules": 10,
      "accident": "Collision during loading; the housing was inspected and found sound."
    },
    {
      "serial": "DEMO-CORE-0002",
      "batch": "core",
      "operator": "fleet",
      "cluster": "demo-fleet",
      "values": {
        "batterySerialNumber": "DEMO-CORE-0002",
        "thirdPartyServiceProvider": "Demo Passport Hosting AB",
        "stateOfChargeSoC": {
          "percentUnit": "%",
          "percentageValue": 88
        },
        "numberOfFullChargingAndDischargingCycles": 0,
        "temperatureInformation": {
          "degreeCelsiusUnit": "°C",
          "celsiusValue": 19
        }
      }
    }
  ]
}
```

- [ ] **Step 8: Add the demo mark, embed the catalogue and pictures, and read the setting**

Patch `3d3-task1-code.patch` (`web/BatteryPassWeb.csproj`, `web/Configuration/BatteryPassOptions.cs`, `web/Dpp/Store/DppRecords.cs`, `web/Program.cs`). Save the block as `../3d3-task1-code.patch`, outside the worktree, and run `git apply ../3d3-task1-code.patch` from the worktree root:

```diff
diff --git a/web/BatteryPassWeb.csproj b/web/BatteryPassWeb.csproj
--- a/web/BatteryPassWeb.csproj
+++ b/web/BatteryPassWeb.csproj
@@ -22,6 +22,8 @@
   <ItemGroup>
     <Content Remove="Dpp\Schemas\**" />
     <EmbeddedResource Include="Dpp\Schemas\v2.0.1\*.json" LogicalName="Dpp.Schemas.%(Filename)%(Extension)" />
+    <Content Remove="Dpp\Demo\**" />
+    <EmbeddedResource Include="Dpp\Demo\demo-catalogue.json;Dpp\Demo\Pictures\*.png" LogicalName="Dpp.Demo.%(Filename)%(Extension)" />
   </ItemGroup>
 
   <ItemGroup>
diff --git a/web/Configuration/BatteryPassOptions.cs b/web/Configuration/BatteryPassOptions.cs
--- a/web/Configuration/BatteryPassOptions.cs
+++ b/web/Configuration/BatteryPassOptions.cs
@@ -22,6 +22,9 @@ public sealed class BatteryPassOptions
     public int SessionTimeoutMinutes { get; set; } = DefaultSessionTimeoutMinutes;
     public string DemoAdminEmail { get; set; } = "admin@example.test";
     public string DemoAdminPassword { get; set; } = "<the fallback password>";
+
+    /// <summary>The demo accounts' password (decision 32, DEMO_USERS_PASSWORD); without it the demo data has no accounts.</summary>
+    public string? DemoUsersPassword { get; set; }
     public string ExternalApiEncryptionKey { get; set; } = string.Empty;
     public string PowerAutomateResetWebhookUrl { get; set; } = string.Empty;
     public string PowerAutomateResetWebhookSecret { get; set; } = string.Empty;
diff --git a/web/Dpp/Store/DppRecords.cs b/web/Dpp/Store/DppRecords.cs
--- a/web/Dpp/Store/DppRecords.cs
+++ b/web/Dpp/Store/DppRecords.cs
@@ -18,6 +18,9 @@ public sealed class BatteryFamily : IDppRecord
     [BsonId]
     public required string Id { get; init; }
 
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+
     public required string Name { get; set; }
 
     /// <summary>The family's picture (decision 20): a file in the dpp_files bucket, shown on the public summary. Not passport data.</summary>
@@ -32,6 +35,9 @@ public sealed class BatteryModel : IDppRecord
     [BsonId]
     public required string Id { get; init; }
 
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+
     public required string FamilyId { get; init; }
 
     public required string Name { get; set; }
@@ -48,6 +54,9 @@ public sealed class ManufacturingSite : IDppRecord
     [BsonId]
     public required string Id { get; init; }
 
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+
     public required string Name { get; set; }
 
     public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
@@ -59,6 +68,9 @@ public sealed class ProductionBatch : IDppRecord
     [BsonId]
     public required string Id { get; init; }
 
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+
     public required string ModelId { get; init; }
 
     public required string SiteId { get; init; }
@@ -74,6 +86,9 @@ public sealed class EconomicOperator : IDppRecord
     [BsonId]
     public required string Id { get; init; }
 
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+
     public required string Name { get; set; }
 
     public Dictionary<string, AttributeValue> Values { get; init; } = new(StringComparer.Ordinal);
@@ -85,6 +100,9 @@ public sealed class BatteryPack : IDppRecord
     [BsonId]
     public required string Id { get; init; }
 
+    /// <summary>Made by the demo data seed (decision 32); clearing the demo data removes it.</summary>
+    public bool IsDemo { get; set; }
+
     public required string BatchId { get; init; }
 
     public required string OperatorId { get; set; }
diff --git a/web/Program.cs b/web/Program.cs
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -72,6 +72,8 @@ builder.Services.Configure<BatteryPassOptions>(options =>
     options.DemoAdminPassword = Environment.GetEnvironmentVariable("DEMO_ADMIN_PASSWORD")
         ?? builder.Configuration["BatteryPass:DemoAdminPassword"]
         ?? "<the fallback password>";
+    options.DemoUsersPassword = Environment.GetEnvironmentVariable("DEMO_USERS_PASSWORD")
+        ?? builder.Configuration["BatteryPass:DemoUsersPassword"];
     options.ExternalApiEncryptionKey = Environment.GetEnvironmentVariable("EXTERNAL_API_ENCRYPTION_KEY")
         ?? builder.Configuration["BatteryPass:ExternalApiEncryptionKey"]
         ?? string.Empty;
```

- [ ] **Step 9: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoCatalogueTests"`
Expected: `Passed!  - Failed:     0, Passed:    12`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2054`, none failed.

- [ ] **Step 10: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCatalogueTests.cs web/Dpp/Demo/DemoCatalogue.cs web/Dpp/Demo/DemoPdf.cs web/Dpp/Demo/Pictures/compact.png web/Dpp/Demo/Pictures/core.png web/Dpp/Demo/demo-catalogue.json web/BatteryPassWeb.csproj web/Configuration/BatteryPassOptions.cs web/Dpp/Store/DppRecords.cs web/Program.cs
git commit -m "feat(dpp): the demo mark and the demo catalogue, with the old pictures" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 2: The demo data seed

The service makes the catalogue's records through the app's own creation, save and publication paths, and marks each one. Clearing comes in Task 3.

**Files:**
- Test: `BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`
- Test: `BatteryPassWeb.Tests/Demo/DemoTesting.cs`
- Create: `web/Dpp/Demo/DemoDataService.cs`
- Modify: `web/Program.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Demo/DemoSeedTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: the demo data shows the app's features, made through the app's own paths (decision 32).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoSeedTests(MongoFixture mongo)
{
    private static FilterDefinition<BsonDocument> Eq(string field, BsonValue value) => Builders<BsonDocument>.Filter.Eq(field, value);

    [Fact]
    public async Task TheSeed_ShouldMakeTheFamiliesModelsAndBatteries()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Empty(result.Notes);
        Assert.Equal(["Compact", "Core"], (await rig.All(DppStore.Families)).Select(family => family["Name"].AsString).Order());
        Assert.All(await rig.All(DppStore.Families), family => Assert.Equal("image/png", family["PictureContentType"].AsString));
        Assert.Equal(["Compact 13M", "Compact 7M", "Core"], (await rig.All(DppStore.Models)).Select(model => model["Name"].AsString).Order());
        Assert.All(await rig.All(DppStore.Models), model => Assert.False(string.IsNullOrWhiteSpace(model["ModuleDescription"].AsString)));
        Assert.Equal(2, await rig.Count(DppStore.Sites));
        Assert.Equal(2, await rig.Count(DppStore.Operators));
        Assert.Equal(3, await rig.Count(DppStore.Batches));
        Assert.Equal(6, await rig.Count(DppStore.Packs));
        Assert.Equal(["2 families, 3 models, 2 sites, 2 operators and 3 batches", "6 batteries, 3 of them published", "12 accounts and 7 API tokens"], result.Done);
    }

    [Fact]
    public async Task ThePublishedBatteries_ShouldHaveSignedVersionsAndTheSecondOneAVersionTwo()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var versions = await rig.All(DppStore.Versions);
        Assert.Equal(4, versions.Count);
        Assert.Equal(3, versions.Count(version => version["DppStatus"].AsString == "active"));
        Assert.All(versions, version => Assert.True(version.Contains("Proof")));
        var firstPack = (await rig.All(DppStore.Packs, Eq("Values.batterySerialNumber.Value", "DEMO-C7M-0001"))).Single()["_id"].AsString;
        Assert.Equal([1, 2], versions.Where(version => version["PackId"] == firstPack).Select(version => version["Number"].ToInt32()).Order());
        Assert.All(await rig.All(DppStore.Packs), pack => Assert.StartsWith("urn:x-battery-pass:TEST-", pack["Values"]["uniqueProductIdentifier"]["Value"].AsString, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheDrafts_ShouldShowAReadyOneAndTwoWithMissingValues()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var drafts = (await rig.All(DppStore.Packs)).Where(pack => pack["Values"]["batterySerialNumber"]["Value"].AsString.EndsWith("0002", StringComparison.Ordinal)).ToList();

        Assert.Equal(3, drafts.Count);
        Assert.Equal(2, drafts.Count(pack => !pack["Values"].AsBsonDocument.Contains("manufacturingDate")));
        Assert.Equal(0, await rig.Count(DppStore.Versions, Builders<BsonDocument>.Filter.In("PackId", drafts.Select(pack => pack["_id"]))));
    }

    [Fact]
    public async Task TheBatteries_ShouldHaveModulesAccidentReportsAndDocuments()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var packs = await rig.All(DppStore.Packs);
        Assert.Equal([0, 0, 0, 7, 10, 13], packs.Select(pack => pack["Modules"].AsBsonArray.Count).Order());
        Assert.Equal(2, await rig.Count(DocumentStore.Records, Eq("Property", "informationOnAccidents")));
        Assert.Equal(12, await rig.Count(DocumentStore.Records, Eq("OwnerKind", "Model")));
        Assert.Equal(3, await rig.Count(DocumentStore.Records, Eq("Property", "resultsOfTestReportsProvingCompliance")));
        Assert.Equal(12 + 3 + 2 + 2, await rig.Count($"{DocumentStore.Bucket}.files"));
    }

    [Fact]
    public async Task ThePublishedBatteries_ShouldHaveSevenDaysOfLiveValues()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var changes = await rig.All("applicationAuditEvents", Builders<BsonDocument>.Filter.And(Eq("eventType", "dpp.change"), Eq("metadata.keys.key", "stateOfChargeSoC")));
        var times = changes.Select(change => DateTime.Parse(change["metadata"]["keys"].AsBsonArray.First(key => key["key"] == "stateOfChargeSoC")["generatedAt"].AsString, null, System.Globalization.DateTimeStyles.RoundtripKind)).ToList();
        Assert.True(changes.Count >= 3 * 28, $"{changes.Count} live-value saves");
        Assert.InRange((DateTime.UtcNow - times.Min()).TotalHours, 160, 168);
    }

    [Fact]
    public async Task EveryRecordItMakes_ShouldBeMarkedAsDemoData()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs })
        {
            Assert.All(await rig.All(collection), record => Assert.True(record["IsDemo"].AsBoolean, $"{collection} {record["_id"]}"));
        }

        foreach (var collection in new[] { "users", "clusterMemberships", "clusters", "apiTokens" })
        {
            Assert.All(await rig.All(collection), record => Assert.True(record["isDemo"].AsBoolean, collection));
        }
    }

    [Fact]
    public async Task TheAccounts_ShouldCoverEveryRoleButTheTeamAdminAndUseTheSetting()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var users = await rig.All("users");
        Assert.Equal(12, users.Count);
        Assert.DoesNotContain(users, user => user["roles"].AsBsonArray.Contains("admin"));
        Assert.All(users, user => Assert.True(BCrypt.Net.BCrypt.Verify(DemoRig.Password, user["passwordHash"].AsString)));
        Assert.Equal(["clusterAdmin", "clusterAdmin", "clusterAdmin", "member", "member", "member"], (await rig.All("clusterMemberships")).Select(membership => membership["role"].AsString).Order());
    }

    [Fact]
    public async Task WithoutTheSetting_ShouldSeedEverythingButTheAccounts()
    {
        using var rig = await DemoRig.StartAsync(mongo, password: null);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal([DemoDataService.NoPassword], result.Notes);
        Assert.Equal(0, await rig.Count("users"));
        Assert.Equal(6, await rig.Count(DppStore.Packs));
    }

    [Fact]
    public async Task TheTokens_ShouldBeShownOnceAndStoredOnlyAsHashes()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal(7, result.Tokens.Count);
        Assert.Equal(["Full access", "Read", "Read", "Read", "Read and write live values", "Read and write live values", "Read and write live values"], result.Tokens.Select(token => token.Access).Order());
        var stored = (await rig.All("apiTokens")).Select(token => token.ToJson()).ToList();
        var audit = (await rig.All("applicationAuditEvents")).Select(entry => entry.ToJson()).ToList();
        foreach (var token in result.Tokens)
        {
            Assert.DoesNotContain(stored, text => text.Contains(token.Value, StringComparison.Ordinal));
            Assert.DoesNotContain(audit, text => text.Contains(token.Value, StringComparison.Ordinal));
        }

        Assert.Single(await rig.All("apiTokens", Eq("globalAccess", true)));
    }

    [Fact]
    public async Task RealRecordsWithTheDemoNames_ShouldBeLeftAlone()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var clusters = rig.Services.GetRequiredService<ClusterRepository>();
        await clusters.UpsertClusterAsync("demo-north", "Real North");
        await clusters.UpsertUserAsync("demo.msa@example.test", "Real MSA", ["marketSurveillanceAuthority"], "real-hash");

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Contains("Cluster demo-north was already there as real data: the demo batteries use it, and it stays real.", result.Notes);
        Assert.Contains("Account demo.msa@example.test was already there as real data: it stays as it is.", result.Notes);
        var north = (await rig.All("clusters", Eq("clusterId", "demo-north"))).Single();
        Assert.Equal(("Real North", false), (north["name"].AsString, north.GetValue("isDemo", false).AsBoolean));
        Assert.Equal("real-hash", (await rig.All("users", Eq("email", "demo.msa@example.test"))).Single()["passwordHash"].AsString);
    }

    [Fact]
    public async Task SeedingAgain_ShouldAskToResetOrClearFirst()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var again = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal(["The demo data is already there: reset it or clear it first."], again.Notes);
        Assert.Equal(6, await rig.Count(DppStore.Packs));
    }

    [Fact]
    public async Task WithoutTheAppsAddress_ShouldMakeNothing()
    {
        using var rig = await DemoRig.StartAsync(mongo, appBaseUrl: null);

        var result = await rig.Demo.SeedAsync(DemoRig.Who);

        Assert.Equal([DppLinks.NotConfigured], result.Notes);
        Assert.Equal(0, await rig.Count(DppStore.Families));
    }

    [Fact]
    public async Task TheSeed_ShouldBeLoggedOnceWithTheCountsButNoTokenValues()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        await rig.Demo.SeedAsync(DemoRig.Who);

        var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.SeededEvent))).Single();
        Assert.Equal(DemoRig.Who, entry["actor"].AsString);
        Assert.Equal((6, 3, 12, 7), (entry["metadata"]["batteries"].ToInt32(), entry["metadata"]["published"].ToInt32(), entry["metadata"]["accounts"].ToInt32(), entry["metadata"]["tokens"].ToInt32()));
    }
}
```

`BatteryPassWeb.Tests/Demo/DemoTesting.cs`:

```csharp
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>The app's own services on a fresh throwaway database, as the demo data card uses them.</summary>
internal sealed class DemoRig(ServiceProvider services, IMongoDatabase database) : IDisposable
{
    public const string Who = "team.admin@example.test";
    public const string Password = "demo-users-password-for-tests";

    public ServiceProvider Services { get; } = services;
    public IMongoDatabase Database { get; } = database;
    public DemoDataService Demo => Services.GetRequiredService<DemoDataService>();
    public DppStore Store => Services.GetRequiredService<DppStore>();

    public static async Task<DemoRig> StartAsync(MongoFixture mongo, string? password = Password, string? appBaseUrl = DocumentTesting.BaseUrl)
    {
        var context = mongo.NewContext();
        var services = new ServiceCollection()
            .AddSingleton(Options.Create(new BatteryPassOptions { AppBaseUrl = appBaseUrl ?? string.Empty, DemoUsersPassword = password }))
            .AddSingleton(context)
            .AddSingleton<ApplicationAuditService>()
            .AddSingleton<ClusterRepository>()
            .AddSingleton(SigningTesting.Keys)
            .AddSingleton(new ExternalApiSecurityService("demo-test-key-for-the-token-hashes-0000"))
            .AddSingleton<ExternalApiRepository>()
            .AddDpp()
            .AddSingleton<DemoDataService>()
            .BuildServiceProvider();
        await services.GetRequiredService<DppStore>().EnsureIndexesAsync();
        await services.GetRequiredService<DocumentStore>().EnsureIndexesAsync();
        return new DemoRig(services, context.Database!);
    }

    public Task<long> Count(string collection, FilterDefinition<BsonDocument>? filter = null) =>
        Database.GetCollection<BsonDocument>(collection).CountDocumentsAsync(filter ?? FilterDefinition<BsonDocument>.Empty);

    public Task<List<BsonDocument>> All(string collection, FilterDefinition<BsonDocument>? filter = null) =>
        Database.GetCollection<BsonDocument>(collection).Find(filter ?? FilterDefinition<BsonDocument>.Empty).ToListAsync();

    public void Dispose() => Services.Dispose();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoSeedTests"`
Expected: the build fails: `DemoDataService` doesn't exist yet (CS0246).

- [ ] **Step 3: Write `DemoDataService.cs`**

`web/Dpp/Demo/DemoDataService.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Demo;

/// <summary>A demo token's value, shown once after seeding and never stored or logged.</summary>
public sealed record DemoToken(string Label, string Access, string Value);

/// <summary>What a seed or a clear did, in plain lines, with notes for anything left as it was.</summary>
public sealed record DemoResult(IReadOnlyList<string> Done, IReadOnlyList<string> Notes, IReadOnlyList<DemoToken> Tokens);

/// <summary>
/// The demo data (decision 32). Seeding makes the catalogue's records through the app's own creation, save and
/// publication paths, so values are checked, logged and signed as usual, and marks each record it makes as demo data.
/// </summary>
public sealed class DemoDataService(
    MongoContext mongo,
    DppStore store,
    ProductDataPages productData,
    BatteryPages batteries,
    PackCreationService packCreation,
    SavePipeline pipeline,
    PublicationService publication,
    DocumentService documents,
    DppLinks links,
    BatteryApi api,
    ClusterRepository clusters,
    ExternalApiRepository tokens,
    ApplicationAuditService audit,
    IOptions<BatteryPassOptions> options,
    TimeProvider clock)
{
    public const string SeededEvent = "demo.seeded";
    public const string Mark = "IsDemo";
    public const string AccountMark = "isDemo";
    public const string NoPassword = "Set DEMO_USERS_PASSWORD to create the demo accounts.";

    /// <summary>Hours between two demo live values; seven days fit the detail page's history (168 hours).</summary>
    private const int HistoryStepHours = 6;
    private const int HistoryPoints = 28;

    private IMongoDatabase Database => mongo.Database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    /// <summary>True when the demo accounts can be made: DEMO_USERS_PASSWORD is set.</summary>
    public bool AccountsPossible => !string.IsNullOrWhiteSpace(options.Value.DemoUsersPassword);

    /// <summary>True when any demo battery is there; false without a database.</summary>
    public async Task<bool> HasDemoDataAsync(CancellationToken cancellationToken = default) =>
        mongo.Database is not null
        && await Collection(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq(Mark, true), cancellationToken: cancellationToken) > 0;

    public async Task<DemoResult> SeedAsync(string who, CancellationToken cancellationToken = default)
    {
        if (!links.IsConfigured)
        {
            return new DemoResult([], [DppLinks.NotConfigured], []);
        }

        if (await HasDemoDataAsync(cancellationToken))
        {
            return new DemoResult([], ["The demo data is already there: reset it or clear it first."], []);
        }

        var catalogue = DemoCatalogue.Load();
        var team = ChangeSender.Team(who);
        var notes = new List<string>();
        var demoTokens = new List<DemoToken>();

        foreach (var cluster in catalogue.Clusters)
        {
            if (await Collection("clusters").Find(Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id)).AnyAsync(cancellationToken))
            {
                notes.Add($"Cluster {cluster.Id} was already there as real data: the demo batteries use it, and it stays real.");
                continue;
            }

            await clusters.UpsertClusterAsync(cluster.Id, cluster.Name, cancellationToken);
            await MarkAccountRecordAsync("clusters", Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id), cancellationToken);
        }

        var accounts = await SeedAccountsAsync(catalogue, notes, cancellationToken);

        var families = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var family in catalogue.Families)
        {
            var id = await productData.CreateFamilyAsync(who, family.Name, cancellationToken);
            await MarkAsync(DppStore.Families, id, cancellationToken);
            await productData.SetFamilyPictureAsync(who, id, family.Picture, DemoCatalogue.Picture(family.Picture), cancellationToken);
            families[family.Key] = id;
        }

        var sites = await SeedNamedAsync(catalogue.Sites, TargetKind.Site, DppStore.Sites, name => productData.CreateSiteAsync(who, name, cancellationToken), team, cancellationToken);
        var operators = await SeedNamedAsync(catalogue.Operators, TargetKind.Operator, DppStore.Operators, name => productData.CreateOperatorAsync(who, name, cancellationToken), team, cancellationToken);

        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in catalogue.Models)
        {
            var id = await productData.CreateModelAsync(who, families[model.Family], model.Name, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"The demo model {model.Name} couldn't be made.");
            await MarkAsync(DppStore.Models, id, cancellationToken);
            await SaveAsync(team, TargetKind.Model, id, model.Values, cancellationToken);
            foreach (var (property, title) in model.Documents)
            {
                await UploadAsync(team, TargetKind.Model, id, property, $"{title} - {model.Name}", cancellationToken);
            }

            var stored = await store.GetAsync<BatteryModel>(id, cancellationToken);
            await productData.SetModuleDescriptionAsync(who, stored!, model.ModuleDescription, cancellationToken);
            models[model.Key] = id;
        }

        var batches = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in catalogue.Batches)
        {
            var (id, problem) = await productData.CreateBatchAsync(who, models[batch.Model], batch.Year, sites[batch.Site], cancellationToken);
            if (id is null)
            {
                throw new InvalidOperationException($"The demo batch {batch.Key} couldn't be made: {problem}.");
            }

            await MarkAsync(DppStore.Batches, id, cancellationToken);
            await SaveAsync(team, TargetKind.Batch, id, batch.Values, cancellationToken);
            await UploadAsync(team, TargetKind.Batch, id, "resultsOfTestReportsProvingCompliance", batch.TestReport, cancellationToken);
            batches[batch.Key] = id;
        }

        var published = 0;
        foreach (var demo in catalogue.Packs)
        {
            var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form"), cancellationToken);
            if (created.Pack is null)
            {
                var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
                notes.Add($"Battery {demo.Serial} wasn't made: {created.Problem} {string.Join("; ", rejected)}".TrimEnd() + ".");
                continue;
            }

            var packId = created.Pack.Id;
            await MarkAsync(DppStore.Packs, packId, cancellationToken);
            if (demo.Modules > 0)
            {
                await batteries.ImportModulesAsync(who, packId, ModuleExport(demo.Serial, demo.Modules), cancellationToken);
            }

            if (demo.Publish)
            {
                var result = await publication.PublishAsync(team, packId, cancellationToken);
                if (result.Version is null)
                {
                    throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
                }

                published++;
                if (demo.History)
                {
                    await WriteHistoryAsync(team, packId, demo, cancellationToken);
                }

                if (demo.NewVersion is not null)
                {
                    await SaveAsync(team, TargetKind.Pack, packId, demo.NewVersion, cancellationToken);
                }
            }

            if (demo.Accident is not null)
            {
                await documents.AddAccidentReportAsync(team, packId, new DocumentUpload("accident-report.pdf", DemoPdf.Make("Accident report", demo.Accident), demo.Accident, "en-GB"), cancellationToken);
            }
        }

        demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));

        var done = new List<string>
        {
            $"{catalogue.Families.Count} families, {catalogue.Models.Count} models, {catalogue.Sites.Count} sites, {catalogue.Operators.Count} operators and {catalogue.Batches.Count} batches",
            $"{catalogue.Packs.Count} batteries, {published} of them published",
            $"{accounts} accounts and {demoTokens.Count} API tokens",
        };
        await audit.AppendApplicationAuditEventAsync(
            SeededEvent,
            who,
            "admin",
            "admin-ui",
            "Demo data seeded.",
            new BsonDocument
            {
                ["families"] = catalogue.Families.Count,
                ["models"] = catalogue.Models.Count,
                ["batteries"] = catalogue.Packs.Count,
                ["published"] = published,
                ["accounts"] = accounts,
                ["tokens"] = demoTokens.Count,
            },
            "demo",
            "demo-data",
            cancellationToken: cancellationToken);
        return new DemoResult(done, notes, demoTokens);
    }

    private async Task<int> SeedAccountsAsync(DemoCatalogue catalogue, List<string> notes, CancellationToken cancellationToken)
    {
        var password = options.Value.DemoUsersPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            notes.Add(NoPassword);
            return 0;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var made = 0;
        foreach (var account in catalogue.Accounts)
        {
            var byEmail = Builders<BsonDocument>.Filter.Eq("email", account.Email);
            if (await Collection("users").Find(byEmail).AnyAsync(cancellationToken))
            {
                notes.Add($"Account {account.Email} was already there as real data: it stays as it is.");
                continue;
            }

            await clusters.UpsertUserAsync(account.Email, account.Name, [account.Role], hash, cancellationToken);
            await MarkAccountRecordAsync("users", byEmail, cancellationToken);
            if (account.Cluster is not null)
            {
                await clusters.UpsertClusterMembershipAsync(account.Email, account.Cluster, account.ClusterRole ?? "member", cancellationToken);
                await MarkAccountRecordAsync("clusterMemberships", Builders<BsonDocument>.Filter.And(byEmail, Builders<BsonDocument>.Filter.Eq("clusterId", account.Cluster)), cancellationToken);
            }

            made++;
        }

        return made;
    }

    /// <summary>Each demo cluster's read and read-write tokens, and one team token with full access; values shown once.</summary>
    private async Task<List<DemoToken>> SeedTokensAsync(string who, DemoCatalogue catalogue, CancellationToken cancellationToken)
    {
        var made = new List<DemoToken>();
        foreach (var cluster in catalogue.Clusters)
        {
            foreach (var (mode, access) in new[] { (ExternalTokenAccessMode.Read, "Read"), (ExternalTokenAccessMode.ReadWrite, "Read and write live values") })
            {
                var (document, value) = await tokens.CreateTokenAsync($"Demo {cluster.Name} ({access.ToLowerInvariant()})", mode, [cluster.Id], false, false, who, cancellationToken: cancellationToken);
                await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), cancellationToken);
                made.Add(new DemoToken($"{cluster.Name}", access, value));
            }
        }

        var (team, teamValue) = await tokens.CreateTokenAsync("Demo team (full access)", ExternalTokenAccessMode.Lifecycle, [], false, true, who, cancellationToken: cancellationToken);
        await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", team["_id"]), cancellationToken);
        made.Add(new DemoToken("Team, all clusters", "Full access", teamValue));
        return made;
    }

    private async Task<Dictionary<string, string>> SeedNamedAsync(
        IEnumerable<DemoNamed> records, TargetKind kind, string collection, Func<string, Task<string>> create, ChangeSender team, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var id = await create(record.Name);
            await MarkAsync(collection, id, cancellationToken);
            await SaveAsync(team, kind, id, record.Values, cancellationToken);
            ids[record.Key] = id;
        }

        return ids;
    }

    /// <summary>Seven days of live values every six hours, oldest first, through the API's own write path.</summary>
    private async Task WriteHistoryAsync(ChangeSender team, string packId, DemoPack demo, CancellationToken cancellationToken)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
        var now = clock.GetUtcNow().UtcDateTime;
        var cycles = demo.Values["numberOfFullChargingAndDischargingCycles"]!.GetValue<int>() - HistoryPoints / 2;
        for (var point = 0; point < HistoryPoints; point++)
        {
            var hoursAgo = HistoryStepHours * (HistoryPoints - 1 - point);
            var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                ["stateOfChargeSoC"] = 55 + (int)Math.Round(30 * Math.Sin(point * Math.PI / 4)),
                ["numberOfFullChargingAndDischargingCycles"] = cycles + point / 2,
                ["temperatureInformation"] = 22 + (int)Math.Round(6 * Math.Sin(point * Math.PI / 2)),
                ["timeSpentInExtremeTemperaturesAboveBoundary"] = 30 + point,
                ["timeSpentInExtremeTemperaturesBelowBoundary"] = 12 + point / 4,
                ["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = 0,
                ["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = 0,
            };
            await api.WriteLiveValuesAsync(team, pack, values, now.AddHours(-hoursAgo), preview: false, cancellationToken);
        }
    }

    private async Task SaveAsync(ChangeSender team, TargetKind kind, string id, JsonObject values, CancellationToken cancellationToken)
    {
        var result = await pipeline.SaveAsync(team, new ChangeSet(kind, id, Values(values), "form"), cancellationToken: cancellationToken);
        var rejected = result.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key}: {key.Message}").ToList();
        if (!result.Saved || rejected.Count > 0)
        {
            throw new InvalidOperationException($"The demo {kind} values weren't all accepted: {string.Join("; ", rejected)}");
        }
    }

    private async Task UploadAsync(ChangeSender team, TargetKind kind, string id, string property, string title, CancellationToken cancellationToken)
    {
        var fileName = $"{property}.pdf";
        var result = await documents.UploadAsync(team, kind, id, property, new DocumentUpload(fileName, DemoPdf.Make(title, $"{kind} document for the demo data."), title, "en-GB"), cancellationToken);
        if (result.Problem is not null)
        {
            throw new InvalidOperationException($"The demo document {title} couldn't be stored: {result.Problem}.");
        }
    }

    private static Dictionary<string, JsonNode?> Values(JsonObject values) =>
        values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);

    /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
    private static byte[] ModuleExport(string serial, int modules)
    {
        var children = new JsonArray();
        for (var position = 1; position <= modules; position++)
        {
            children.Add(new JsonObject
            {
                ["position"] = position,
                ["serialNumber"] = $"{serial}-M{position:D2}",
                ["connectivityId"] = $"CAN-{serial}-{position:D2}",
            });
        }

        var export = new JsonObject
        {
            ["metadata"] = new JsonObject
            {
                ["identifiers"] = new JsonObject
                {
                    ["deviceIdentifiers"] = new JsonObject { ["serialNumber"] = serial, ["childrenIdentifiers"] = children },
                },
            },
        };
        return Encoding.UTF8.GetBytes(export.ToJsonString());
    }

    private IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    private Task MarkAsync(string collection, string id, CancellationToken cancellationToken) =>
        Collection(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set(Mark, true), cancellationToken: cancellationToken);

    private Task MarkAccountRecordAsync(string collection, FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken) =>
        Collection(collection).UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(AccountMark, true), cancellationToken: cancellationToken);
}
```

- [ ] **Step 4: Register the service**

Patch `3d3-task2-code.patch` (`web/Program.cs`). Save the block as `../3d3-task2-code.patch`, outside the worktree, and run `git apply ../3d3-task2-code.patch` from the worktree root:

```diff
diff --git a/web/Program.cs b/web/Program.cs
--- a/web/Program.cs
+++ b/web/Program.cs
@@ -207,6 +207,7 @@ builder.Services.AddSingleton<ExternalApiSecurityService>();
 builder.Services.AddSingleton<ExternalApiRepository>();
 builder.Services.AddSingleton<ExternalApiInitializer>();
 builder.Services.AddDpp();
+builder.Services.AddSingleton<BatteryPassWeb.Dpp.Demo.DemoDataService>();
 
 var app = builder.Build();
 var batteryPassOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BatteryPassOptions>>().Value;
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoSeedTests"`
Expected: `Passed!  - Failed:     0, Passed:    13`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2067`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoSeedTests.cs BatteryPassWeb.Tests/Demo/DemoTesting.cs web/Dpp/Demo/DemoDataService.cs web/Program.cs
git commit -m "feat(dpp): the demo data seed, through the app's own paths" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 3: Clearing and resetting the demo data

Clearing removes the marked records with their versions, documents and files, and keeps any that real data uses, without their mark. Resetting clears, then seeds again. `DemoDataService.cs` is given whole again.

**Files:**
- Test: `BatteryPassWeb.Tests/Demo/DemoClearTests.cs`
- Replace: `web/Dpp/Demo/DemoDataService.cs`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Demo/DemoClearTests.cs`:

```csharp
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: clearing removes only demo data and never breaks real data; resetting seeds again (decision 32).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoClearTests(MongoFixture mongo)
{
    private static FilterDefinition<BsonDocument> Eq(string field, BsonValue value) => Builders<BsonDocument>.Filter.Eq(field, value);

    [Fact]
    public async Task Clearing_ShouldRemoveEveryDemoRecordWithItsVersionsDocumentsAndFiles()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        foreach (var collection in new[] { DppStore.Families, DppStore.Models, DppStore.Sites, DppStore.Batches, DppStore.Operators, DppStore.Packs, DppStore.Versions, DocumentStore.Records, $"{DocumentStore.Bucket}.files", $"{DocumentStore.Bucket}.chunks", "users", "clusterMemberships", "clusters", "apiTokens" })
        {
            Assert.True(await rig.Count(collection) == 0, $"{collection} still has records");
        }

        Assert.Empty(result.Notes);
        Assert.Contains("6 batteries removed", result.Done);
        Assert.Contains("12 accounts removed", result.Done);
    }

    [Fact]
    public async Task Clearing_ShouldKeepTheAuditLogAndLogItself()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var before = await rig.Count("applicationAuditEvents");

        await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(before + 1, await rig.Count("applicationAuditEvents"));
        var entry = (await rig.All("applicationAuditEvents", Eq("eventType", DemoDataService.ClearedEvent))).Single();
        Assert.Equal(6, entry["metadata"]["batteries"].ToInt32());
    }

    [Fact]
    public async Task ClearingAgain_ShouldSayThereWasNothingToClear()
    {
        using var rig = await DemoRig.StartAsync(mongo);

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Equal(["There was no demo data to clear."], result.Done);
    }

    [Fact]
    public async Task RealDataUsingDemoRecords_ShouldKeepThemWithoutTheirMark()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var store = rig.Store;
        var model = (await rig.All(DppStore.Models, Eq("Name", "Compact 7M"))).Single();
        var site = (await rig.All(DppStore.Sites)).First();
        var demoOperator = (await rig.All(DppStore.Operators)).First();
        await store.InsertAsync(new ProductionBatch { Id = "batch-real", ModelId = model["_id"].AsString, SiteId = site["_id"].AsString, Year = 2027 });
        await store.InsertAsync(new BatteryPack { Id = "pack-real", BatchId = "batch-real", OperatorId = demoOperator["_id"].AsString, ClusterId = "demo-north" });
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertUserAsync("real.person@example.com", "Real person", ["member"], "hash");
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertClusterMembershipAsync("real.person@example.com", "demo-south", "member");

        var result = await rig.Demo.ClearAsync(DemoRig.Who);

        Assert.Contains("Kept Compact 7M: 1 real batches use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains($"Kept {site["Name"].AsString}: 1 real batches use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains($"Kept {demoOperator["Name"].AsString}: 1 real batteries use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains("Kept Compact: 1 real models use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains("Kept cluster demo-north: 1 real batteries use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Contains("Kept cluster demo-south: 1 real accounts use it, so it is no longer marked as demo data.", result.Notes);
        Assert.Equal(["pack-real"], (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString));
        Assert.Equal(["Compact 7M"], (await rig.All(DppStore.Models)).Select(record => record["Name"].AsString));
        Assert.All(await rig.All(DppStore.Models), record => Assert.False(record["IsDemo"].AsBoolean));
        Assert.Equal(["demo-north", "demo-south"], (await rig.All("clusters")).Select(cluster => cluster["clusterId"].AsString).Order());
        Assert.All(await rig.All("clusters"), cluster => Assert.False(cluster["isDemo"].AsBoolean));
        Assert.Equal(["real.person@example.com"], (await rig.All("users")).Select(user => user["email"].AsString));
    }

    [Fact]
    public async Task Resetting_ShouldClearThenSeedWithNewBatteries()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var first = await rig.Demo.SeedAsync(DemoRig.Who);
        var firstPacks = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();

        var result = await rig.Demo.ResetAsync(DemoRig.Who);

        var packs = (await rig.All(DppStore.Packs)).Select(pack => pack["_id"].AsString).ToList();
        Assert.Equal(6, packs.Count);
        Assert.Empty(packs.Intersect(firstPacks));
        Assert.Equal(4, await rig.Count(DppStore.Versions));
        Assert.Equal(7, result.Tokens.Count);
        Assert.Empty(result.Tokens.Select(token => token.Value).Intersect(first.Tokens.Select(token => token.Value)));
        Assert.Contains("6 batteries removed", result.Done);
        Assert.Contains("6 batteries, 3 of them published", result.Done);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoClearTests"`
Expected: the build fails: `DemoDataService` has no `ClearAsync`, `ResetAsync` or `ClearedEvent` yet (CS1061, CS0117).

- [ ] **Step 3: Replace `DemoDataService.cs`**

`web/Dpp/Demo/DemoDataService.cs`:

```csharp
using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Demo;

/// <summary>A demo token's value, shown once after seeding and never stored or logged.</summary>
public sealed record DemoToken(string Label, string Access, string Value);

/// <summary>What a seed or a clear did, in plain lines, with notes for anything left as it was.</summary>
public sealed record DemoResult(IReadOnlyList<string> Done, IReadOnlyList<string> Notes, IReadOnlyList<DemoToken> Tokens);

/// <summary>
/// The demo data (decision 32). Seeding makes the catalogue's records through the app's own creation, save and
/// publication paths, so values are checked, logged and signed as usual, and marks each record it makes as demo data.
/// Clearing removes only marked records; a marked record that real data uses stays, loses its mark and is listed.
/// </summary>
public sealed class DemoDataService(
    MongoContext mongo,
    DppStore store,
    ProductDataPages productData,
    BatteryPages batteries,
    PackCreationService packCreation,
    SavePipeline pipeline,
    PublicationService publication,
    DocumentService documents,
    DocumentStore documentStore,
    DppLinks links,
    BatteryApi api,
    ClusterRepository clusters,
    ExternalApiRepository tokens,
    ApplicationAuditService audit,
    IOptions<BatteryPassOptions> options,
    TimeProvider clock)
{
    public const string SeededEvent = "demo.seeded";
    public const string ClearedEvent = "demo.cleared";
    public const string Mark = "IsDemo";
    public const string AccountMark = "isDemo";
    public const string NoPassword = "Set DEMO_USERS_PASSWORD to create the demo accounts.";

    /// <summary>Hours between two demo live values; seven days fit the detail page's history (168 hours).</summary>
    private const int HistoryStepHours = 6;
    private const int HistoryPoints = 28;

    private IMongoDatabase Database => mongo.Database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    /// <summary>True when the demo accounts can be made: DEMO_USERS_PASSWORD is set.</summary>
    public bool AccountsPossible => !string.IsNullOrWhiteSpace(options.Value.DemoUsersPassword);

    /// <summary>True when any demo battery is there; false without a database.</summary>
    public async Task<bool> HasDemoDataAsync(CancellationToken cancellationToken = default) =>
        mongo.Database is not null
        && await Collection(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq(Mark, true), cancellationToken: cancellationToken) > 0;

    /// <summary>Clears the demo data, then seeds it again.</summary>
    public async Task<DemoResult> ResetAsync(string who, CancellationToken cancellationToken = default)
    {
        if (!links.IsConfigured)
        {
            return new DemoResult([], [DppLinks.NotConfigured], []);
        }

        var cleared = await ClearAsync(who, cancellationToken);
        var seeded = await SeedAsync(who, cancellationToken);
        return new DemoResult([.. cleared.Done, .. seeded.Done], [.. cleared.Notes, .. seeded.Notes], seeded.Tokens);
    }

    public async Task<DemoResult> SeedAsync(string who, CancellationToken cancellationToken = default)
    {
        if (!links.IsConfigured)
        {
            return new DemoResult([], [DppLinks.NotConfigured], []);
        }

        if (await HasDemoDataAsync(cancellationToken))
        {
            return new DemoResult([], ["The demo data is already there: reset it or clear it first."], []);
        }

        var catalogue = DemoCatalogue.Load();
        var team = ChangeSender.Team(who);
        var notes = new List<string>();
        var demoTokens = new List<DemoToken>();

        foreach (var cluster in catalogue.Clusters)
        {
            if (await Collection("clusters").Find(Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id)).AnyAsync(cancellationToken))
            {
                notes.Add($"Cluster {cluster.Id} was already there as real data: the demo batteries use it, and it stays real.");
                continue;
            }

            await clusters.UpsertClusterAsync(cluster.Id, cluster.Name, cancellationToken);
            await MarkAccountRecordAsync("clusters", Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id), cancellationToken);
        }

        var accounts = await SeedAccountsAsync(catalogue, notes, cancellationToken);

        var families = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var family in catalogue.Families)
        {
            var id = await productData.CreateFamilyAsync(who, family.Name, cancellationToken);
            await MarkAsync(DppStore.Families, id, cancellationToken);
            await productData.SetFamilyPictureAsync(who, id, family.Picture, DemoCatalogue.Picture(family.Picture), cancellationToken);
            families[family.Key] = id;
        }

        var sites = await SeedNamedAsync(catalogue.Sites, TargetKind.Site, DppStore.Sites, name => productData.CreateSiteAsync(who, name, cancellationToken), team, cancellationToken);
        var operators = await SeedNamedAsync(catalogue.Operators, TargetKind.Operator, DppStore.Operators, name => productData.CreateOperatorAsync(who, name, cancellationToken), team, cancellationToken);

        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in catalogue.Models)
        {
            var id = await productData.CreateModelAsync(who, families[model.Family], model.Name, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"The demo model {model.Name} couldn't be made.");
            await MarkAsync(DppStore.Models, id, cancellationToken);
            await SaveAsync(team, TargetKind.Model, id, model.Values, cancellationToken);
            foreach (var (property, title) in model.Documents)
            {
                await UploadAsync(team, TargetKind.Model, id, property, $"{title} - {model.Name}", cancellationToken);
            }

            var stored = await store.GetAsync<BatteryModel>(id, cancellationToken);
            await productData.SetModuleDescriptionAsync(who, stored!, model.ModuleDescription, cancellationToken);
            models[model.Key] = id;
        }

        var batches = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in catalogue.Batches)
        {
            var (id, problem) = await productData.CreateBatchAsync(who, models[batch.Model], batch.Year, sites[batch.Site], cancellationToken);
            if (id is null)
            {
                throw new InvalidOperationException($"The demo batch {batch.Key} couldn't be made: {problem}.");
            }

            await MarkAsync(DppStore.Batches, id, cancellationToken);
            await SaveAsync(team, TargetKind.Batch, id, batch.Values, cancellationToken);
            await UploadAsync(team, TargetKind.Batch, id, "resultsOfTestReportsProvingCompliance", batch.TestReport, cancellationToken);
            batches[batch.Key] = id;
        }

        var published = 0;
        foreach (var demo in catalogue.Packs)
        {
            var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form"), cancellationToken);
            if (created.Pack is null)
            {
                var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
                notes.Add($"Battery {demo.Serial} wasn't made: {created.Problem} {string.Join("; ", rejected)}".TrimEnd() + ".");
                continue;
            }

            var packId = created.Pack.Id;
            await MarkAsync(DppStore.Packs, packId, cancellationToken);
            if (demo.Modules > 0)
            {
                await batteries.ImportModulesAsync(who, packId, ModuleExport(demo.Serial, demo.Modules), cancellationToken);
            }

            if (demo.Publish)
            {
                var result = await publication.PublishAsync(team, packId, cancellationToken);
                if (result.Version is null)
                {
                    throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
                }

                published++;
                if (demo.History)
                {
                    await WriteHistoryAsync(team, packId, demo, cancellationToken);
                }

                if (demo.NewVersion is not null)
                {
                    await SaveAsync(team, TargetKind.Pack, packId, demo.NewVersion, cancellationToken);
                }
            }

            if (demo.Accident is not null)
            {
                await documents.AddAccidentReportAsync(team, packId, new DocumentUpload("accident-report.pdf", DemoPdf.Make("Accident report", demo.Accident), demo.Accident, "en-GB"), cancellationToken);
            }
        }

        demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken));

        var done = new List<string>
        {
            $"{catalogue.Families.Count} families, {catalogue.Models.Count} models, {catalogue.Sites.Count} sites, {catalogue.Operators.Count} operators and {catalogue.Batches.Count} batches",
            $"{catalogue.Packs.Count} batteries, {published} of them published",
            $"{accounts} accounts and {demoTokens.Count} API tokens",
        };
        await audit.AppendApplicationAuditEventAsync(
            SeededEvent,
            who,
            "admin",
            "admin-ui",
            "Demo data seeded.",
            new BsonDocument
            {
                ["families"] = catalogue.Families.Count,
                ["models"] = catalogue.Models.Count,
                ["batteries"] = catalogue.Packs.Count,
                ["published"] = published,
                ["accounts"] = accounts,
                ["tokens"] = demoTokens.Count,
            },
            "demo",
            "demo-data",
            cancellationToken: cancellationToken);
        return new DemoResult(done, notes, demoTokens);
    }

    public async Task<DemoResult> ClearAsync(string who, CancellationToken cancellationToken = default)
    {
        var notes = new List<string>();
        var removed = new Dictionary<string, int>(StringComparer.Ordinal);
        void Count(string what) => removed[what] = removed.GetValueOrDefault(what) + 1;

        // Batteries first: nothing real can depend on a demo battery. Their versions and documents go with them.
        foreach (var packId in await DemoIdsAsync(DppStore.Packs, cancellationToken))
        {
            await Collection(DppStore.Versions).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
            await documentStore.DeleteOwnedAsync(TargetKind.Pack, packId, cancellationToken);
            await store.DeleteAsync<BatteryPack>(packId, cancellationToken);
            Count("batteries");
        }

        await ClearUnlessUsedAsync<ProductionBatch>(DppStore.Batches, "batches", (DppStore.Packs, "BatchId"), "real batteries", TargetKind.Batch, notes, Count, cancellationToken);
        await ClearUnlessUsedAsync<EconomicOperator>(DppStore.Operators, "operators", (DppStore.Packs, "OperatorId"), "real batteries", null, notes, Count, cancellationToken);
        await ClearUnlessUsedAsync<BatteryModel>(DppStore.Models, "models", (DppStore.Batches, "ModelId"), "real batches", TargetKind.Model, notes, Count, cancellationToken);
        await ClearUnlessUsedAsync<ManufacturingSite>(DppStore.Sites, "sites", (DppStore.Batches, "SiteId"), "real batches", null, notes, Count, cancellationToken);
        foreach (var family in await Collection(DppStore.Families).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
        {
            var id = family["_id"].AsString;
            var used = await Collection(DppStore.Models).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("FamilyId", id), cancellationToken: cancellationToken);
            if (used > 0)
            {
                await Unmark(DppStore.Families, id, cancellationToken);
                notes.Add($"Kept {BsonName(family)}: {used} real models use it, so it is no longer marked as demo data.");
                continue;
            }

            if (family.GetValue("PictureFileId", BsonNull.Value) is { IsString: true } picture)
            {
                await documentStore.DeleteFileAsync(picture.AsString, cancellationToken);
            }

            await store.DeleteAsync<BatteryFamily>(id, cancellationToken);
            Count("families");
        }

        var demoTokens = Builders<BsonDocument>.Filter.Eq(AccountMark, true);
        removed["API tokens"] = (int)(await Collection("apiTokens").DeleteManyAsync(demoTokens, cancellationToken)).DeletedCount;
        foreach (var user in await Collection("users").Find(Builders<BsonDocument>.Filter.Eq(AccountMark, true)).ToListAsync(cancellationToken))
        {
            await clusters.DeleteUserAsync(BsonHelpers.GetString(user, "email"), cancellationToken);
            Count("accounts");
        }

        foreach (var cluster in await Collection("clusters").Find(Builders<BsonDocument>.Filter.Eq(AccountMark, true)).ToListAsync(cancellationToken))
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var uses = new List<string>();
            var packs = await Collection(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ClusterId", clusterId), cancellationToken: cancellationToken);
            var members = await Collection("clusterMemberships").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clusterId", clusterId), cancellationToken: cancellationToken);
            var realTokens = await Collection("apiTokens").CountDocumentsAsync(
                Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("autoClusterId", clusterId), Builders<BsonDocument>.Filter.AnyEq("clusterIds", clusterId)),
                cancellationToken: cancellationToken);
            if (packs > 0) uses.Add($"{packs} real batteries");
            if (members > 0) uses.Add($"{members} real accounts");
            if (realTokens > 0) uses.Add($"{realTokens} real API tokens");
            if (uses.Count > 0)
            {
                await MarkAccountRecordAsync("clusters", Builders<BsonDocument>.Filter.Eq("clusterId", clusterId), cancellationToken, false);
                notes.Add($"Kept cluster {clusterId}: {string.Join(", ", uses)} use it, so it is no longer marked as demo data.");
                continue;
            }

            await clusters.DeleteClusterAsync(clusterId, cancellationToken);
            Count("clusters");
        }

        var done = removed.Where(entry => entry.Value > 0).Select(entry => $"{entry.Value} {entry.Key} removed").ToList();
        if (done.Count == 0 && notes.Count == 0)
        {
            done.Add("There was no demo data to clear.");
        }

        var metadata = new BsonDocument(removed.Select(entry => new BsonElement(entry.Key.Replace(' ', '_'), entry.Value)))
        {
            ["kept"] = notes.Count,
        };
        await audit.AppendApplicationAuditEventAsync(ClearedEvent, who, "admin", "admin-ui", "Demo data cleared.", metadata, "demo", "demo-data", cancellationToken: cancellationToken);
        return new DemoResult(done, notes, []);
    }

    private async Task<int> SeedAccountsAsync(DemoCatalogue catalogue, List<string> notes, CancellationToken cancellationToken)
    {
        var password = options.Value.DemoUsersPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            notes.Add(NoPassword);
            return 0;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var made = 0;
        foreach (var account in catalogue.Accounts)
        {
            var byEmail = Builders<BsonDocument>.Filter.Eq("email", account.Email);
            if (await Collection("users").Find(byEmail).AnyAsync(cancellationToken))
            {
                notes.Add($"Account {account.Email} was already there as real data: it stays as it is.");
                continue;
            }

            await clusters.UpsertUserAsync(account.Email, account.Name, [account.Role], hash, cancellationToken);
            await MarkAccountRecordAsync("users", byEmail, cancellationToken);
            if (account.Cluster is not null)
            {
                await clusters.UpsertClusterMembershipAsync(account.Email, account.Cluster, account.ClusterRole ?? "member", cancellationToken);
                await MarkAccountRecordAsync("clusterMemberships", Builders<BsonDocument>.Filter.And(byEmail, Builders<BsonDocument>.Filter.Eq("clusterId", account.Cluster)), cancellationToken);
            }

            made++;
        }

        return made;
    }

    /// <summary>Each demo cluster's read and read-write tokens, and one team token with full access; values shown once.</summary>
    private async Task<List<DemoToken>> SeedTokensAsync(string who, DemoCatalogue catalogue, CancellationToken cancellationToken)
    {
        var made = new List<DemoToken>();
        foreach (var cluster in catalogue.Clusters)
        {
            foreach (var (mode, access) in new[] { (ExternalTokenAccessMode.Read, "Read"), (ExternalTokenAccessMode.ReadWrite, "Read and write live values") })
            {
                var (document, value) = await tokens.CreateTokenAsync($"Demo {cluster.Name} ({access.ToLowerInvariant()})", mode, [cluster.Id], false, false, who, cancellationToken: cancellationToken);
                await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), cancellationToken);
                made.Add(new DemoToken($"{cluster.Name}", access, value));
            }
        }

        var (team, teamValue) = await tokens.CreateTokenAsync("Demo team (full access)", ExternalTokenAccessMode.Lifecycle, [], false, true, who, cancellationToken: cancellationToken);
        await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", team["_id"]), cancellationToken);
        made.Add(new DemoToken("Team, all clusters", "Full access", teamValue));
        return made;
    }

    private async Task<Dictionary<string, string>> SeedNamedAsync(
        IEnumerable<DemoNamed> records, TargetKind kind, string collection, Func<string, Task<string>> create, ChangeSender team, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var id = await create(record.Name);
            await MarkAsync(collection, id, cancellationToken);
            await SaveAsync(team, kind, id, record.Values, cancellationToken);
            ids[record.Key] = id;
        }

        return ids;
    }

    /// <summary>Seven days of live values every six hours, oldest first, through the API's own write path.</summary>
    private async Task WriteHistoryAsync(ChangeSender team, string packId, DemoPack demo, CancellationToken cancellationToken)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
        var now = clock.GetUtcNow().UtcDateTime;
        var cycles = demo.Values["numberOfFullChargingAndDischargingCycles"]!.GetValue<int>() - HistoryPoints / 2;
        for (var point = 0; point < HistoryPoints; point++)
        {
            var hoursAgo = HistoryStepHours * (HistoryPoints - 1 - point);
            var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                ["stateOfChargeSoC"] = 55 + (int)Math.Round(30 * Math.Sin(point * Math.PI / 4)),
                ["numberOfFullChargingAndDischargingCycles"] = cycles + point / 2,
                ["temperatureInformation"] = 22 + (int)Math.Round(6 * Math.Sin(point * Math.PI / 2)),
                ["timeSpentInExtremeTemperaturesAboveBoundary"] = 30 + point,
                ["timeSpentInExtremeTemperaturesBelowBoundary"] = 12 + point / 4,
                ["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = 0,
                ["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = 0,
            };
            await api.WriteLiveValuesAsync(team, pack, values, now.AddHours(-hoursAgo), preview: false, cancellationToken);
        }
    }

    private async Task ClearUnlessUsedAsync<T>(
        string collection,
        string plural,
        (string Collection, string Field) usedBy,
        string usersWord,
        TargetKind? documentsOf,
        List<string> notes,
        Action<string> count,
        CancellationToken cancellationToken)
        where T : class, IDppRecord
    {
        foreach (var record in await Collection(collection).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
        {
            var id = record["_id"].AsString;
            var used = await Collection(usedBy.Collection).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq(usedBy.Field, id), cancellationToken: cancellationToken);
            if (used > 0)
            {
                await Unmark(collection, id, cancellationToken);
                notes.Add($"Kept {BsonName(record)}: {used} {usersWord} use it, so it is no longer marked as demo data.");
                continue;
            }

            if (documentsOf is { } owner)
            {
                await documentStore.DeleteOwnedAsync(owner, id, cancellationToken);
            }

            await store.DeleteAsync<T>(id, cancellationToken);
            count(plural);
        }
    }

    private async Task SaveAsync(ChangeSender team, TargetKind kind, string id, JsonObject values, CancellationToken cancellationToken)
    {
        var result = await pipeline.SaveAsync(team, new ChangeSet(kind, id, Values(values), "form"), cancellationToken: cancellationToken);
        var rejected = result.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key}: {key.Message}").ToList();
        if (!result.Saved || rejected.Count > 0)
        {
            throw new InvalidOperationException($"The demo {kind} values weren't all accepted: {string.Join("; ", rejected)}");
        }
    }

    private async Task UploadAsync(ChangeSender team, TargetKind kind, string id, string property, string title, CancellationToken cancellationToken)
    {
        var fileName = $"{property}.pdf";
        var result = await documents.UploadAsync(team, kind, id, property, new DocumentUpload(fileName, DemoPdf.Make(title, $"{kind} document for the demo data."), title, "en-GB"), cancellationToken);
        if (result.Problem is not null)
        {
            throw new InvalidOperationException($"The demo document {title} couldn't be stored: {result.Problem}.");
        }
    }

    private static Dictionary<string, JsonNode?> Values(JsonObject values) =>
        values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);

    /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
    private static byte[] ModuleExport(string serial, int modules)
    {
        var children = new JsonArray();
        for (var position = 1; position <= modules; position++)
        {
            children.Add(new JsonObject
            {
                ["position"] = position,
                ["serialNumber"] = $"{serial}-M{position:D2}",
                ["connectivityId"] = $"CAN-{serial}-{position:D2}",
            });
        }

        var export = new JsonObject
        {
            ["metadata"] = new JsonObject
            {
                ["identifiers"] = new JsonObject
                {
                    ["deviceIdentifiers"] = new JsonObject { ["serialNumber"] = serial, ["childrenIdentifiers"] = children },
                },
            },
        };
        return Encoding.UTF8.GetBytes(export.ToJsonString());
    }

    private IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    private async Task<List<string>> DemoIdsAsync(string collection, CancellationToken cancellationToken) =>
        (await Collection(collection).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(cancellationToken))
        .Select(document => document["_id"].AsString)
        .ToList();

    private Task MarkAsync(string collection, string id, CancellationToken cancellationToken) =>
        Collection(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set(Mark, true), cancellationToken: cancellationToken);

    private Task Unmark(string collection, string id, CancellationToken cancellationToken) =>
        Collection(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set(Mark, false), cancellationToken: cancellationToken);

    private Task MarkAccountRecordAsync(string collection, FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken, bool isDemo = true) =>
        Collection(collection).UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(AccountMark, isDemo), cancellationToken: cancellationToken);

    private static string BsonName(BsonDocument record) =>
        record.GetValue("Name", BsonNull.Value) is { IsString: true } name ? name.AsString : record["_id"].AsString;
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoClearTests"`
Expected: `Passed!  - Failed:     0, Passed:     5`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2072`, none failed.

- [ ] **Step 5: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoClearTests.cs web/Dpp/Demo/DemoDataService.cs
git commit -m "feat(dpp): clearing and resetting the demo data, never breaking real data" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 4: The Demo data card

The Registered clusters tab gets the card with its two buttons, and the page shows the new tokens once in a list window.

**Files:**
- Test: `BatteryPassWeb.Tests/Demo/DemoCardTests.cs`
- Create: `web/Views/Shared/_GeneratedCredentialsModal.cshtml`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Models/ViewModels/AdminClusterViewModel.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`

- [ ] **Step 1: Write the failing tests**

`BatteryPassWeb.Tests/Demo/DemoCardTests.cs`:

```csharp
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Demo;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Models.ViewModels;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Demo;

/// <summary>Step 3d-3: the "Demo data" card on the team's admin pages (decision 32).</summary>
[Collection(MongoCollection.Name)]
public sealed class DemoCardTests(MongoFixture mongo)
{
    private static AdminController Controller(DemoRig rig)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, DemoRig.Who), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)], "test")),
        };
        var services = rig.Services;
        return new AdminController(
            services.GetRequiredService<ClusterRepository>(),
            services.GetRequiredService<ExternalApiRepository>(),
            services.GetRequiredService<ApplicationAuditService>(),
            new ApplicationSettingsService(services.GetRequiredService<MongoContext>(), services.GetRequiredService<IOptions<BatteryPassOptions>>()),
            rig.Store,
            rig.Demo)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    [Fact]
    public async Task ResettingFromTheCard_ShouldSeedAndShowEveryNewTokenOnce()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        var controller = Controller(rig);

        var result = await controller.ResetDemoData(CancellationToken.None);

        Assert.Equal("/admin/clusters?tab=clusters", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Demo data reset: There was no demo data to clear; 2 families, 3 models, 2 sites, 2 operators and 3 batches; 6 batteries, 3 of them published; 12 accounts and 7 API tokens.", controller.TempData["StatusMessage"]);
        var tokens = JsonSerializer.Deserialize<List<DemoToken>>((string)controller.TempData["GeneratedCredentials"]!)!;
        Assert.Equal(7, tokens.Count);
        Assert.All(tokens, token => Assert.False(string.IsNullOrWhiteSpace(token.Value)));
    }

    [Fact]
    public async Task ClearingFromTheCard_ShouldClearAndSayWhatWasRemoved()
    {
        using var rig = await DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var controller = Controller(rig);

        await controller.ClearDemoData(CancellationToken.None);

        Assert.StartsWith("Demo data cleared: 6 batteries removed; ", (string)controller.TempData["StatusMessage"]!, StringComparison.Ordinal);
        Assert.Null(controller.TempData["GeneratedCredentials"]);
        Assert.Equal(0, await rig.Count(DppStore.Packs));
    }

    [Fact]
    public async Task WithoutTheAppsAddress_ShouldSayWhyAndClearNothing()
    {
        using var rig = await DemoRig.StartAsync(mongo, appBaseUrl: null);
        await rig.Services.GetRequiredService<ClusterRepository>().UpsertClusterAsync("demo-north", "Demo North Operations");
        await rig.Database.GetCollection<MongoDB.Bson.BsonDocument>("clusters").UpdateOneAsync(
            MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("clusterId", "demo-north"), MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("isDemo", true));
        var controller = Controller(rig);

        await controller.ResetDemoData(CancellationToken.None);

        Assert.Equal(DppLinks.NotConfigured, controller.TempData["ErrorMessage"]);
        Assert.Equal(1, await rig.Count("clusters"));
    }

    [Fact]
    public async Task ThePage_ShouldSayWhetherTheDemoDataIsThereAndWhetherAccountsCanBeMade()
    {
        using var rig = await DemoRig.StartAsync(mongo, password: null);
        var controller = Controller(rig);

        var before = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await controller.Clusters("clusters", null, CancellationToken.None)).Model);
        await rig.Demo.SeedAsync(DemoRig.Who);
        var after = Assert.IsType<AdminClusterViewModel>(Assert.IsType<ViewResult>(await controller.Clusters("clusters", null, CancellationToken.None)).Model);

        Assert.Equal((false, false), (before.HasDemoData, before.DemoAccountsPossible));
        Assert.True(after.HasDemoData);
    }

    [Fact]
    public void TheActions_ShouldBeForAdminsOnlyAndPostedFromThePage()
    {
        Assert.Equal("AdminOnly", typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        foreach (var (action, route) in new[] { (nameof(AdminController.ResetDemoData), "demo-data/reset"), (nameof(AdminController.ClearDemoData), "demo-data/clear") })
        {
            var method = typeof(AdminController).GetMethod(action)!;
            Assert.Equal(route, method.GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    [Fact]
    public void TheCard_ShouldAskFirstAndTheWindowShouldShowTheTokensOnce()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "Clusters.cshtml"));
        var card = page[page.IndexOf("data-demo-data-card", StringComparison.Ordinal)..page.IndexOf("bp-cluster-create-strip", StringComparison.Ordinal)];
        var window = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_GeneratedCredentialsModal.cshtml"));

        Assert.Contains("action=\"/admin/demo-data/reset\"", card, StringComparison.Ordinal);
        Assert.Contains("action=\"/admin/demo-data/clear\"", card, StringComparison.Ordinal);
        Assert.Equal(2, card.Split("data-confirm-action").Length - 1);
        Assert.Equal(2, card.Split("@Html.AntiForgeryToken()").Length - 1);
        Assert.Contains("DemoDataService.NoPassword", card, StringComparison.Ordinal);
        Assert.Contains("@await Html.PartialAsync(\"_GeneratedCredentialsModal\", Model.GeneratedCredentials)", page, StringComparison.Ordinal);
        Assert.Contains("Store these credentials now", window, StringComparison.Ordinal);
        Assert.Contains("These tokens are shown only once.", window, StringComparison.Ordinal);
        Assert.Contains("data-generated-token-copy", window, StringComparison.Ordinal);
    }

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

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoCardTests"`
Expected: the build fails: `AdminController` has no `ResetDemoData` or `ClearDemoData`, and the page model has no demo fields yet (CS0117, CS1061).

- [ ] **Step 3: Write `_GeneratedCredentialsModal.cshtml`**

`web/Views/Shared/_GeneratedCredentialsModal.cshtml`:

```cshtml
@model IReadOnlyList<BatteryPassWeb.Dpp.Demo.DemoToken>
@* The one-time window for several new tokens at once (decision 32): the same warning as _GeneratedCredentialModal. *@
@if (Model.Count > 0)
{
    <div class="bp-generated-token-modal" data-generated-tokens-modal>
        <div class="bp-generated-token-backdrop"></div>
        @* A long list scrolls inside the window, so its Okay button stays within reach on any screen. *@
        <section class="bp-generated-token-panel overflow-auto" style="max-height: 90vh;" role="dialog" aria-modal="true" aria-labelledby="bp-generated-tokens-title">
            <p class="bp-summary-title">Generated tokens</p>
            <h2 id="bp-generated-tokens-title">Store these credentials now</h2>
            <p class="bp-generated-token-copy">These tokens are shown only once. Copy each one before leaving this page.</p>
            <ul class="list-unstyled mb-3">
                @foreach (var token in Model)
                {
                    <li class="mb-2" data-generated-token-row data-generated-token-value="@token.Value">
                        <strong>@token.Label</strong> <span class="text-secondary">@token.Access</span>
                        <span class="d-flex gap-2 align-items-center">
                            <code class="bp-generated-token-value flex-grow-1 text-break" data-generated-token-text>@token.Value</code>
                            <button type="button" class="bp-secondary-button bp-button-compact" data-generated-token-copy>Copy</button>
                        </span>
                    </li>
                }
            </ul>
            <div class="bp-generated-token-actions">
                <button type="button" class="bp-primary-button" data-generated-tokens-ok>Okay</button>
            </div>
        </section>
    </div>

    <script>
        (function () {
            const writeClipboard = async (value) => {
                if (navigator.clipboard && window.isSecureContext) {
                    await navigator.clipboard.writeText(value);
                    return;
                }

                const textarea = document.createElement('textarea');
                textarea.value = value;
                textarea.setAttribute('readonly', '');
                textarea.style.position = 'fixed';
                textarea.style.opacity = '0';
                document.body.appendChild(textarea);
                textarea.select();
                document.execCommand('copy');
                textarea.remove();
            };

            document.querySelectorAll('[data-generated-tokens-modal]').forEach((modal) => {
                modal.querySelectorAll('[data-generated-token-row]').forEach((row) => {
                    const button = row.querySelector('[data-generated-token-copy]');
                    button?.addEventListener('click', async () => {
                        await writeClipboard(row.dataset.generatedTokenValue || '');
                        button.textContent = 'Copied';
                    });
                });
                modal.querySelector('[data-generated-tokens-ok]')?.addEventListener('click', () => {
                    modal.hidden = true;
                });
            });
        })();
    </script>
}
```

- [ ] **Step 4: Add the card, its actions and the token window**

Patch `3d3-task4-code.patch` (`web/Controllers/AdminController.cs`, `web/Models/ViewModels/AdminClusterViewModel.cs`, `web/Views/Admin/Clusters.cshtml`). Save the block as `../3d3-task4-code.patch`, outside the worktree, and run `git apply ../3d3-task4-code.patch` from the worktree root:

```diff
diff --git a/web/Controllers/AdminController.cs b/web/Controllers/AdminController.cs
--- a/web/Controllers/AdminController.cs
+++ b/web/Controllers/AdminController.cs
@@ -1,5 +1,6 @@
 using BatteryPassWeb.Configuration;
 using BatteryPassWeb.Dpp.Audit;
+using BatteryPassWeb.Dpp.Demo;
 using BatteryPassWeb.Dpp.Store;
 using BatteryPassWeb.Models.ViewModels;
 using BatteryPassWeb.Services;
@@ -23,19 +24,22 @@ public class AdminController : Controller
     private readonly ApplicationAuditService _applicationAuditService;
     private readonly ApplicationSettingsService _applicationSettingsService;
     private readonly DppStore _dppStore;
+    private readonly DemoDataService _demoData;
 
     public AdminController(
         ClusterRepository clusterRepository,
         ExternalApiRepository externalApiRepository,
         ApplicationAuditService applicationAuditService,
         ApplicationSettingsService applicationSettingsService,
-        DppStore dppStore)
+        DppStore dppStore,
+        DemoDataService demoData)
     {
         _clusterRepository = clusterRepository;
         _externalApiRepository = externalApiRepository;
         _applicationAuditService = applicationAuditService;
         _applicationSettingsService = applicationSettingsService;
         _dppStore = dppStore;
+        _demoData = demoData;
     }
 
     [HttpGet("")]
@@ -184,6 +188,9 @@ public class AdminController : Controller
             StatusMessage = TempData["StatusMessage"]?.ToString() ?? string.Empty,
             ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? string.Empty,
             GeneratedCredential = TempData["GeneratedCredential"]?.ToString() ?? string.Empty,
+            GeneratedCredentials = TempData["GeneratedCredentials"] is string tokens ? JsonSerializer.Deserialize<List<DemoToken>>(tokens) ?? [] : [],
+            HasDemoData = await _demoData.HasDemoDataAsync(cancellationToken),
+            DemoAccountsPossible = _demoData.AccountsPossible,
             SessionTimeoutMinutes = await _applicationSettingsService.GetSessionTimeoutMinutesAsync(cancellationToken)
         };
 
@@ -948,6 +955,46 @@ public class AdminController : Controller
         return string.IsNullOrWhiteSpace(actor) ? "admin" : actor;
     }
 
+    /// <summary>Removes the demo data and makes it again (decision 32); the new tokens are shown once.</summary>
+    [HttpPost("demo-data/reset")]
+    [ValidateAntiForgeryToken]
+    public Task<IActionResult> ResetDemoData(CancellationToken cancellationToken) =>
+        DemoDataAsync("Demo data reset", () => _demoData.ResetAsync(CurrentActor(), cancellationToken));
+
+    /// <summary>Removes the demo data and keeps the real data (decision 32).</summary>
+    [HttpPost("demo-data/clear")]
+    [ValidateAntiForgeryToken]
+    public Task<IActionResult> ClearDemoData(CancellationToken cancellationToken) =>
+        DemoDataAsync("Demo data cleared", () => _demoData.ClearAsync(CurrentActor(), cancellationToken));
+
+    private async Task<IActionResult> DemoDataAsync(string heading, Func<Task<DemoResult>> run)
+    {
+        try
+        {
+            var result = await run();
+            var message = string.Join(" ", result.Notes);
+            if (result.Done.Count > 0)
+            {
+                TempData["StatusMessage"] = $"{heading}: {string.Join("; ", result.Done.Select(line => line.TrimEnd('.')))}. {message}".TrimEnd();
+            }
+            else
+            {
+                TempData["ErrorMessage"] = message;
+            }
+
+            if (result.Tokens.Count > 0)
+            {
+                TempData["GeneratedCredentials"] = JsonSerializer.Serialize(result.Tokens);
+            }
+        }
+        catch (InvalidOperationException exception)
+        {
+            TempData["ErrorMessage"] = $"The demo data couldn't be made: {exception.Message}";
+        }
+
+        return Redirect("/admin/clusters?tab=clusters");
+    }
+
     private Task AppendAdminAuditAsync(
         string eventType,
         string message,
diff --git a/web/Models/ViewModels/AdminClusterViewModel.cs b/web/Models/ViewModels/AdminClusterViewModel.cs
--- a/web/Models/ViewModels/AdminClusterViewModel.cs
+++ b/web/Models/ViewModels/AdminClusterViewModel.cs
@@ -1,3 +1,4 @@
+using BatteryPassWeb.Dpp.Demo;
 using BatteryPassWeb.Services;
 
 namespace BatteryPassWeb.Models.ViewModels;
@@ -13,6 +14,9 @@ public sealed class AdminClusterViewModel
     public string StatusMessage { get; init; } = string.Empty;
     public string ErrorMessage { get; init; } = string.Empty;
     public string GeneratedCredential { get; init; } = string.Empty;
+    public IReadOnlyList<DemoToken> GeneratedCredentials { get; init; } = [];
+    public bool HasDemoData { get; init; }
+    public bool DemoAccountsPossible { get; init; }
     public int SessionTimeoutMinutes { get; init; } = 180;
 }
 
diff --git a/web/Views/Admin/Clusters.cshtml b/web/Views/Admin/Clusters.cshtml
--- a/web/Views/Admin/Clusters.cshtml
+++ b/web/Views/Admin/Clusters.cshtml
@@ -109,11 +109,34 @@
     }
     @* data-generated-token-modal data-generated-token-copy data-generated-token-ok navigator.clipboard.writeText *@
     @await Html.PartialAsync("_GeneratedCredentialModal", Model.GeneratedCredential)
+    @await Html.PartialAsync("_GeneratedCredentialsModal", Model.GeneratedCredentials)
 
     @if (selectedTab == "clusters")
     {
         var clusterRowIndex = 0;
         <div class="bp-management-shell bp-cluster-management-shell mt-4">
+            <section class="bp-card bp-console-form-card bp-demo-data-card" data-demo-data-card>
+                <div>
+                    <span class="bp-summary-title">Demo data</span>
+                    <h2 class="h5 mb-2">Demo data</h2>
+                    <p class="text-secondary mb-1">The Compact and Core families with six batteries in several states, three demo clusters, an account for every role but the team admin, and API tokens. Real data is never changed. Sign in with your own team account; the demo accounts cover the other roles.</p>
+                    <p class="mb-0" data-demo-data-state>@(Model.HasDemoData ? "The demo data is there." : "There is no demo data.")</p>
+                    @if (!Model.DemoAccountsPossible)
+                    {
+                        <p class="text-secondary mb-0" data-demo-data-no-accounts>@BatteryPassWeb.Dpp.Demo.DemoDataService.NoPassword</p>
+                    }
+                </div>
+                <div class="d-flex flex-wrap gap-2 align-items-end">
+                    <form method="post" action="/admin/demo-data/reset">
+                        @Html.AntiForgeryToken()
+                        <button type="submit" class="bp-primary-button" data-confirm-action data-confirm-title="Reset demo data" data-confirm-message="Remove the demo data and make it again? Real data stays as it is. The new API tokens are shown once." data-confirm-submit="Reset demo data">Reset demo data</button>
+                    </form>
+                    <form method="post" action="/admin/demo-data/clear">
+                        @Html.AntiForgeryToken()
+                        <button type="submit" class="bp-danger-button" data-confirm-action data-confirm-title="Clear demo data" data-confirm-message="Remove the demo data? Real data stays as it is, and a demo record that real data uses is kept." data-confirm-submit="Clear demo data">Clear demo data</button>
+                    </form>
+                </div>
+            </section>
             <section class="bp-card bp-cluster-create-strip bp-console-form-card">
                 <div>
                     <span class="bp-summary-title">Cluster setup</span>
```

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter "FullyQualifiedName~BatteryPassWeb.Tests.Demo.DemoCardTests"`
Expected: `Passed!  - Failed:     0, Passed:     6`.

Then the whole suite: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed:  2078`, none failed.

- [ ] **Step 6: Commit**

```bash
git add BatteryPassWeb.Tests/Demo/DemoCardTests.cs web/Views/Shared/_GeneratedCredentialsModal.cshtml web/Controllers/AdminController.cs web/Models/ViewModels/AdminClusterViewModel.cs web/Views/Admin/Clusters.cshtml
git commit -m "feat(dpp): the Demo data card and the one-time list of new tokens" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```


---

### Task 5: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`
Expected: `Passed!  - Failed:     0, Passed:  2078, Skipped:     0, Total:  2078`: the 2042 from before, then 12, 13, 5 and 6 new tests in Tasks 1 to 4.

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed**

Run: `git status --short`
Expected: nothing listed.

## After this plan: the product owner's first demo data

Not steps of this plan. The managing session brings them to the product owner, after 3d-2's reset:

1. **For demo accounts**, add `DEMO_USERS_PASSWORD` to the app's environment, as the launcher does for the other settings. Without it, the demo data has no accounts, and the card says so.
2. **Start the app with the usual launcher**, sign in with a team account, and go to Administration → Registered clusters → Demo data → **Reset demo data**.
3. **Store the tokens.** The seven new token values are shown once; copy the ones you need.
4. **Look around:**
   - the batteries list (six demo batteries);
   - a published battery's summary and detail page (its seven days of live values);
   - a draft's publication check (missing values);
   - the Registry as a demo cluster user;
   - the audit log ("Demo data seeded.").
5. **"Clear demo data"** removes it all again whenever real data starts. Anything real data uses stays and is listed.

**Later:** step 4 adds a status-change demo battery once that flow exists; 3e gives the card the cleaner look with the other admin pages.

