# DPP V3 Step 6b-2: The Test Pack for the New App — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Step 6's test pack (design 14), rewritten for the new app. Today's three test documents describe the old app: product templates, software parameters, validate-sign-publish and the old demo battery. They also print the old admin password.

- **`docs/qa-test-pack.md`** stays the one starting point:
  - "Reset demo data" as the known start;
  - one browser across accounts, and every element fully visible (step 5's lessons);
  - the demo accounts per role, with no passwords (decision 32: `DEMO_USERS_PASSWORD`, handed out by the project owner);
  - the 8 demo batteries;
  - a scenario matrix of every reader (design 5.2) against the pages, the battery page's actions and the API's tokens;
  - the acceptance checklist, known limitations, and the bug and improvement templates.
- **`docs/end-user-testing-guide.md`** becomes the step-by-step script per reader, each step with what to expect: anyone, cluster user, cluster admin, the team, market surveillance, the five other roles, and the API.
- **`docs/sample-cluster-test-accounts.md`** becomes the demo data's reference: clusters, accounts, tokens (values shown once, never written), batteries and sample files.
- The pack is step 6c's script.

**Architecture:**
- Three documents rewritten in place.
- The tests that pinned the old documents' text go: 12 tests in 6 classes. Three classes test only the old documents (`ErrorHandlingRolloutTests`, `QaTesterPackDocumentationTests`, `Phase6ADocumentationTests`), and one method goes from each of three others.
- One new class, `BatteryPassWeb.Tests/Docs/TestPackTests.cs` (6 tests):
  - the pack's sections and pointers;
  - every demo account, battery and cluster of `web/Dpp/Demo/demo-catalogue.json` named in the documents, so a new demo record shows here;
  - the guide scripts every kind of reader;
  - no document prints a password;
  - the three checks of current pages that the old tests carried (the Help doesn't describe "Evidence readiness"; the API help says "Token Value", never "Battery Secret" or the old token words).
- No code or view changes.
- The old app's `docs/passport-admin-registry-manual-test-flow.md` goes (the owner, Q3); it stays in Git history and on the old branch. No test reads it.

**Tech Stack:** .NET 10, xUnit 2.9, System.Text.Json. Nothing new is downloaded.

**Spec:** `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-design.md`, sections 5.2 and 14 (step 6). Decision 32 (demo data). The step 6b split: the managing session's notes, `step6b-split-proposal.md`.

## Before you start

- **Where:** the worktree `.worktrees/cloned-kassem-rewrite`, from its root, in Git Bash.
- **Branch:** `feature/dpp-v3-step2d-team-pages` (local only).
  - This plan doesn't depend on 6b-1 (the Help per role): the two touch different files. It was measured on e5b238c.
  - Check with `git branch --show-current` and `git log --oneline -5`.
- **The local app must be closed.** While the v3 launcher runs the app from this worktree, it locks `web/bin/Debug`, and the builds below fail with MSB3027.
  - Check that no `BatteryPassWeb.exe` is running.
  - Never stop it yourself: ask the product owner to close it.
- **No database:** nothing here touches Atlas or any database.
- **Builds and test runs:**
  - Run `export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0` in the shell first.
  - Give every `dotnet test` and `dotnet build` `--disable-build-servers`. The whole suite also takes `--blame-hang --blame-hang-timeout 10m`.
  - **Task 1 runs only its own tests. The whole suite runs once**, in the final checks. Never run two whole suites at once.
- **Count:** this plan removes 12 tests and adds 6, so the whole suite has 6 fewer than before it. Note the count before you start; for example 2534 after plan 6a-2, then 2528. If 6b-1 ran first, add its count.
- **Patches and files:**
  - Save each patch to the file it names, outside the worktree (e.g. `../6b2-trim.patch`), and run `git apply ../<name>` from the worktree root.
  - Documents and new files are given whole: replace the file's content with exactly the block's.
- **Commits:** the commit command carries the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line. Never push.
- **`site.css` is not changed.**

## Found while prototyping

**Checked against the app:** each reader's detail-page tabs, Registry rows and access to a battery page were read on a prototype app after a demo reset (scratch clone at e5b238c, my own app and a throwaway mongod). They match the pack's matrix:
- cluster user and admin of demo-north: Purchaser, Shared, Live values, Modules, then the public sections;
- another cluster's: the public sections only;
- team and market surveillance: Purchaser, Shared, Authorities, Live values, Modules;
- notified body: Authorities;
- Commission: Shared and Authorities;
- repair, recycling, treatment: Shared, with 5, 3 and 1 values;
- the Registry: demo-north's published batteries for its users, plus "Waiting for you" (DEMO-CORE-0001) for its admin; every published battery for the team, market surveillance, the notified body, the Commission and the three legitimate-interest roles;
- another cluster's battery page: refused.

**The demo data's names come from `demo-catalogue.json`:**
- clusters Demo North, South and Fleet Operations;
- DEMO-C13M-0001 repurposed, then remanufactured, so three linked passports;
- the API's header: `Authorization: Basic <base64(token:)>`.

**Found for 6c** (not changed here): another cluster's *admin* opening a battery page gets a bare 404 (the browser's own error page), while a cluster *user* gets the app's "Not allowed" page. Noted in `step6c-findings-so-far.md`.

**Measured on a clean clone of e5b238c:**
- the tests fail first: 5 of 6, while `TheHelpPages_ShouldKeepTheNewWorkflowWords` holds before and after;
- then they pass with the documents: 24/24 across the new class, the three trimmed classes, `AdminFeedbackImplementationTests` (reads `power-automate-password-reset.md`, unchanged) and `ResetLauncherTests` (reads `README.md`, unchanged).

### Task 1: The test pack

**Files:**
- Create: `BatteryPassWeb.Tests/Docs/TestPackTests.cs`
- Delete: `BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs`, `BatteryPassWeb.Tests/QaTesterPackDocumentationTests.cs`, `BatteryPassWeb.Tests/Phase6ADocumentationTests.cs`
- Modify: `BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs`, `BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs`, `BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs` (one method each goes)
- Replace: `docs/qa-test-pack.md`, `docs/end-user-testing-guide.md`, `docs/sample-cluster-test-accounts.md`
- Delete: `docs/passport-admin-registry-manual-test-flow.md` (old app only; the owner's yes, Q3)

- [ ] **Step 1: The new tests, and the old documents' tests removed**

Create `BatteryPassWeb.Tests/Docs/TestPackTests.cs` with exactly this content:

````csharp
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BatteryPassWeb.Tests.Dpp;

namespace BatteryPassWeb.Tests.Docs;

/// <summary>
/// Step 6b-2, the test pack rewritten for the new app: qa-test-pack.md is the one starting point, end-user-testing-guide.md
/// the script per reader and sample-cluster-test-accounts.md the demo data. They follow the demo catalogue, so a new demo
/// account or battery shows here. They never print a password (decision 32: DEMO_USERS_PASSWORD only).
/// </summary>
public sealed partial class TestPackTests
{
    private static string Read(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(path)).Replace("\r\n", "\n");

    private static string Pack() => Read("docs", "qa-test-pack.md");

    private static string Guide() => Read("docs", "end-user-testing-guide.md");

    private static string Accounts() => Read("docs", "sample-cluster-test-accounts.md");

    private static JsonNode Catalogue() => JsonNode.Parse(Read("web", "Dpp", "Demo", "demo-catalogue.json"))!;

    [Fact]
    public void ThePack_ShouldBeTheOneStartingPoint()
    {
        var pack = Pack();

        foreach (var heading in new[] { "# QA Tester Pack", "## Quick start", "## Tester accounts", "## Demo batteries", "## Scenario matrix", "## Acceptance checklist", "## Known limitations", "## Bug report template" })
        {
            Assert.Contains(heading, pack, StringComparison.Ordinal);
        }

        Assert.Contains("docs/end-user-testing-guide.md", pack, StringComparison.Ordinal);
        Assert.Contains("docs/sample-cluster-test-accounts.md", pack, StringComparison.Ordinal);
        Assert.Contains("`/help`", pack, StringComparison.Ordinal);
        Assert.Contains("`/help/api`", pack, StringComparison.Ordinal);
        Assert.Contains("**Reset demo data**", pack, StringComparison.Ordinal);
        Assert.Contains("`DEMO_USERS_PASSWORD`", pack, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDemoAccount_ShouldBeInThePackAndTheAccounts()
    {
        var emails = Catalogue()["accounts"]!.AsArray().Select(account => account!["email"]!.GetValue<string>()).ToArray();

        Assert.Equal(12, emails.Length);
        foreach (var email in emails)
        {
            Assert.Contains($"`{email}`", Accounts(), StringComparison.Ordinal);
            Assert.Contains($"`{email}`", Pack(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryDemoBatteryAndCluster_ShouldBeInThePackAndTheAccounts()
    {
        var catalogue = Catalogue();
        foreach (var serial in catalogue["packs"]!.AsArray().Select(pack => pack!["serial"]!.GetValue<string>()))
        {
            Assert.Contains($"| {serial} |", Accounts(), StringComparison.Ordinal);
            Assert.Contains($"| {serial} |", Pack(), StringComparison.Ordinal);
        }

        foreach (var cluster in catalogue["clusters"]!.AsArray())
        {
            Assert.Contains($"| `{cluster!["id"]!.GetValue<string>()}` | {cluster["name"]!.GetValue<string>()} |", Accounts(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheGuide_ShouldScriptEveryKindOfReader()
    {
        var guide = Guide();

        foreach (var heading in new[] { "## 1. Anyone, not signed in", "## 2. Cluster user", "## 3. Cluster admin", "## 4. The team", "## 5. Market surveillance", "## 6. Notified body, Commission and the legitimate-interest roles", "## 7. The API" })
        {
            Assert.Contains(heading, guide, StringComparison.Ordinal);
        }

        foreach (var account in new[] { "demo.north.user", "demo.north.admin", "demo.msa", "demo.notified-body", "demo.commission", "demo.repair", "demo.recycling", "demo.treatment" })
        {
            Assert.Contains($"`{account}@example.test`", guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoTestDoc_ShouldPrintAPassword()
    {
        foreach (var doc in new[] { Pack(), Guide(), Accounts() })
        {
            Assert.DoesNotContain("Password123", doc, StringComparison.Ordinal);
            Assert.DoesNotMatch(PasswordValue(), doc);
        }
    }

    [Fact]
    public void TheHelpPages_ShouldKeepTheNewWorkflowWords()
    {
        // Kept from the old documentation tests: the help no longer describes the old evidence step, and the API help
        // names the token's value, never the old battery secret (decision 30, step 3c).
        var apiHelp = Read("web", "Views", "Help", "Index.cshtml");

        Assert.DoesNotContain("Evidence readiness", Read("web", "Views", "Help", "Guide.cshtml"), StringComparison.Ordinal);
        Assert.Contains("Token Value", apiHelp, StringComparison.Ordinal);
        Assert.DoesNotContain("create, validate, sign and publish passport token", apiHelp, StringComparison.Ordinal);
        Assert.DoesNotContain("Battery Secret", apiHelp, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?im)\bpassword\b\s*[:=]\s*`?[^\s`|]{4,}")]
    private static partial Regex PasswordValue();
}
````

Then run:

```bash
git rm -q BatteryPassWeb.Tests/ErrorHandlingRolloutTests.cs BatteryPassWeb.Tests/QaTesterPackDocumentationTests.cs BatteryPassWeb.Tests/Phase6ADocumentationTests.cs
```

Save as `../6b2-trim.patch` and run `git apply ../6b2-trim.patch`:

```diff
diff --git a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
index d18434a..3e8406d 100644
--- a/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
+++ b/BatteryPassWeb.Tests/BatteryFamilyAccessApiRevisedTests.cs
@@ -6,16 +6,6 @@ namespace BatteryPassWeb.Tests;
 public sealed class BatteryFamilyAccessApiRevisedTests
 {
 
-    [Fact]
-    public void ProductTemplateSource_ShouldDocumentBatteryFamilyMapping()
-    {
-        var docs = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"))
-            + File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));
-
-        Assert.Contains("Battery Family", docs);
-        Assert.Contains("Product Template", docs);
-        Assert.Contains("Product Template is the internal implementation name for Battery Family", docs);
-    }
 
     [Fact]
     public void UserFacingViews_ShouldNotUseRemovedIdentityLabels()
diff --git a/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs b/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
index 3865d52..ec930bd 100644
--- a/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
+++ b/BatteryPassWeb.Tests/ExternalApiBatteryCreateTests.cs
@@ -53,17 +53,6 @@ public sealed class ExternalApiBatteryCreateTests
         Assert.DoesNotContain("passport snapshot", help);
     }
 
-    [Fact]
-    public void QaDocs_ShouldIncludeExternalApiBatteryCreationChecks()
-    {
-        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
-        var qa = File.ReadAllText(RepoFile("docs", "qa-test-pack.md"));
-
-        Assert.Contains("POST /api/external/v1/batteries", guide);
-        Assert.Contains("duplicate serial", guide, StringComparison.OrdinalIgnoreCase);
-        Assert.Contains("POST /api/external/v1/batteries", qa);
-        Assert.Contains("battery.created", qa);
-    }
 
     private static string RepoFile(params string[] parts)
     {
diff --git a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
index ca8d2c1..5534130 100644
--- a/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
+++ b/BatteryPassWeb.Tests/ProductTemplateWorkflowLayoutTests.cs
@@ -22,19 +22,6 @@ public sealed class ProductTemplateWorkflowLayoutTests
         Assert.DoesNotContain("products/{productId}/versions/{productVersion}/software/{softwareVersion}/push", adminController);
     }
 
-    [Fact]
-    public void Documentation_ShouldExplainProductTemplateWorkflow()
-    {
-        var guide = File.ReadAllText(RepoFile("docs", "end-user-testing-guide.md"));
-        var accounts = File.ReadAllText(RepoFile("docs", "sample-cluster-test-accounts.md"));
-
-        Assert.Contains("product template", guide, StringComparison.OrdinalIgnoreCase);
-        Assert.Contains("Compact 7M", guide);
-        Assert.Contains("Compact 13M", guide);
-        Assert.Contains("Core", guide);
-        Assert.DoesNotContain("sample-end-user-storage-001", accounts);
-        Assert.DoesNotContain("demo-draft-incomplete-001", accounts);
-    }
 
     private static string RepoFile(params string[] parts)
     {
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet build BatteryPassWeb.Tests --disable-build-servers --no-incremental`, then `dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~TestPackTests"`
Expected: `Failed!  - Failed:     5, Passed:     1`.

- [ ] **Step 3: The three documents**

Replace `docs/qa-test-pack.md` with exactly this content:

````markdown
# QA Tester Pack

The starting point for anyone testing the battery passport app without knowing the project. It covers the new app (DPP V3): one passport per battery, built from the V3 attribute list, with a reader's role deciding what they see.

Keep these open while testing:

- `docs/end-user-testing-guide.md`: the step-by-step script per role.
- `docs/sample-cluster-test-accounts.md`: the demo accounts, clusters and batteries.
- `/help` in the app: each role's own help, including "Statuses" (the two badges).
- `/help/api` in the app: every API call, with a workbench to try one.

## Quick start

1. Open the app link provided by the project owner in a browser.
2. **The known start:** a team member signs in, opens Clusters, and at the end of the Clusters tab presses **Reset demo data**.
   - This makes the demo clusters, accounts, batteries and API tokens again.
   - The tokens' values are shown once, in the window that follows. Copy them then: they are never shown again and never written here.
3. **Passwords:** the demo accounts share one password, which the project owner gives you. It is set on the server (`DEMO_USERS_PASSWORD`) and never written in this pack or in the app.
4. **One browser:** test in one browser window. Sign out and in as each account in turn: some faults only show when the same browser changes accounts.
5. **Every element fully visible:** on every page and in every window, scroll each choice, field and button into view and check it is fully shown, not under a bar or cut. Reaching the last button isn't enough.

## Tester accounts

| Who | Account | Cluster | What to check |
|---|---|---|---|
| Team | the team's own account (from the project owner) | all | Product data, creating and publishing batteries, every battery page, marking passports, demo data, tokens |
| Cluster admin | `demo.north.admin@example.test` | demo-north | Their batteries' pages: live values, accident reports, status changes and repairs, hand-overs, uploads; their users and tokens. Their Registry also shows DEMO-CORE-0001 waiting to be accepted. |
| Cluster user | `demo.north.user@example.test` | demo-north | Their batteries in the Registry and on the detail page, read only |
| Other cluster admin | `demo.south.admin@example.test`, `demo.fleet.admin@example.test` | demo-south, demo-fleet | They see demo-north's batteries as the public does; demo-north's admin's "Waiting for you" from demo-fleet |
| Other cluster users | `demo.south.user@example.test`, `demo.fleet.user@example.test` | demo-south, demo-fleet | As above, read only |
| Market surveillance | `demo.msa@example.test` | — | Every published passport, every section, "Mark as invalid…" |
| Notified body | `demo.notified-body@example.test` | — | Public and authorities' values |
| Commission | `demo.commission@example.test` | — | Public, shared and authorities' values; no purchaser values |
| Repair, repurposing, remanufacturing | `demo.repair@example.test` | — | Public and the shared values #43, #45–#48 |
| Recycling, battery treatment | `demo.recycling@example.test` | — | Public and #43, #45, #48 |
| Authorised treatment, WEEE | `demo.treatment@example.test` | — | Public and #48 |
| Anyone | not signed in | — | The public page only |

## Demo batteries

| Battery | Cluster | What it shows |
|---|---|---|
| DEMO-C7M-0001 | demo-north | Published, several versions, repaired (status re-used, with its repair history), an accident report, live values with a week of history, 7 modules, two earlier uploads |
| DEMO-C7M-0002 | demo-north | A draft with missing values: the publication checks |
| DEMO-C7M-0003 | demo-north | Marked invalid by market surveillance ("Demo: wrong carbon footprint figures") |
| DEMO-C13M-0001 | demo-south | Repurposed, then remanufactured: three linked passports, the two older inactive |
| DEMO-C13M-0002 | demo-south | A draft |
| DEMO-CORE-0001 | demo-fleet | Published, an accident report, handed over to demo-north and waiting |
| DEMO-CORE-0002 | demo-fleet | A draft |
| DEMO-CORE-0003 | demo-fleet | Waste, marked for deletion |

All demo IDs start with `TEST-`; the pages show a "Test passport" tag for them.

## Scenario matrix

What each reader should get. ✓ = allowed or shown, – = not shown or refused. "Own" means the battery is in the reader's cluster.

| Reader | Public page | Detail page | Purchaser tab | Shared tab | Authorities tab | Live values, Modules tabs | Registry lists | Battery page (manage) | Mark as invalid |
|---|---|---|---|---|---|---|---|---|---|
| Anyone, not signed in | ✓ | – (sign-in) | – | – | – | – | – | – | – |
| Cluster user, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | – | – |
| Cluster admin, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | ✓ | – |
| Cluster user or admin, other cluster's battery | ✓ | public only | – | – | – | – | own cluster's | – (refused) | – |
| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every battery (Batteries tab) | ✓ | ✓ (and undo) |
| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every published | – | ✓ |
| Notified body | ✓ | ✓ | – | – | ✓ | – | every published | – | – |
| Commission | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – |
| Repair | ✓ | ✓ | – | #43, #45–#48 | – | – | every published | – | – |
| Recycling | ✓ | ✓ | – | #43, #45, #48 | – | – | every published | – | – |
| Treatment | ✓ | ✓ | – | #48 | – | – | every published | – | – |

**Battery page actions** (team and the battery's cluster admins, on an active passport):

| Action | Team | Own cluster admin |
|---|---|---|
| Live values, Accident reports ("Add a report"), Upload a file | ✓ | ✓ |
| Change status: Repaired, placed on the market again, Remanufactured, Repurposed, Waste | ✓ | ✓ |
| Change status: Correct the values | ✓ | – |
| Add a note | ✓ | ✓ |
| Hand over… / Accept or Decline on "Waiting for you" | moves directly, or cancels a waiting one | ✓ |
| Mark as invalid or for deletion…, and Undo | ✓ | – |
| Edit product data, create and publish batteries | ✓ | – |

**API** (`/api/external/v1`, a token in the `Authorization` header):

| Token | Reads | Writes |
|---|---|---|
| A cluster's "Read" token | that cluster's batteries, as its cluster user would | nothing |
| A cluster's "Read and write live values" token | the same | the live values (#63, #83, #87, E1–E4) and uploads of live values, for that cluster's batteries |
| The team's "Full access" token | every battery | creating batteries, publishing, live values, uploads |
| Any token | — | never a status change or repair (made on the battery's page only) |

`/api/passports` (the list of all passports) is for the team only.

## Acceptance checklist

- [ ] "Reset demo data" finishes and shows the tokens once.
- [ ] Every row of the scenario matrix holds, for every reader.
- [ ] The two badges show on every passport page with the right status. The Battery badge never shows to the public.
- [ ] DEMO-C7M-0002 lists what blocks publication; filling it in and pressing Publish gives a signed version 1 with a QR code.
- [ ] A repair on DEMO-C7M-0001 sets re-used, adds to the repair history and makes a new version.
- [ ] Hand-over: demo-fleet's DEMO-CORE-0001 waits on demo-north's Registry; Accept moves it, and demo-fleet then sees it as the public does.
- [ ] Market surveillance marks a passport invalid; the public page says so; only the team undoes it.
- [ ] Uploading the help's sample files on DEMO-C7M-0001 shows the check before saving, then saves.
- [ ] The API answers each token as in the API table, and refuses what it should with a plain message.
- [ ] The public and detail pages print on A4 with every tab open (browser print).
- [ ] Every page works at a phone's width (375 px) and at 150 % Windows scaling, with every element fully visible.

## Known limitations

- **Test identifiers:** all IDs start with `TEST-` until the identifier scheme is built (DUNS, plan 6d).
- **Pack temperature:** the battery system's export has no pack temperature yet; each export's notes say so.
- **Hand-overs:** no e-mail; the receiver sees them on their Registry. A waiting hand-over isn't cancelled by itself when the passport stops being active; accepting is then refused with a reason.
- **Signatures:** each version is signed when published; live values and accident reports change it in place without a new signature, by design.

## Bug report template

```text
Title:
Account (role) used:
Battery (serial number) and page address:
Screen width and browser:
Steps:
1.
2.
3.
Expected:
What happened:
Screenshot or file:
```

## Improvement suggestion template

```text
Page:
Who would use it (role):
What is hard today:
Suggestion:
```
````

Replace `docs/end-user-testing-guide.md` with exactly this content:

````markdown
# End-User Testing Guide

A step-by-step script for each kind of reader, on the demo data. Start with `docs/qa-test-pack.md` (accounts, the matrix of who sees what, the checklist). `docs/sample-cluster-test-accounts.md` lists the accounts and batteries.

Before each round:
- A team member presses **Reset demo data** (Clusters tab, at the end) and copies the tokens shown once.
- Use one browser. Sign out before signing in as the next account.
- On every page and in every window, check each element is fully visible: scroll it into view, and nothing covers it.
- Check at a desktop width, at a phone's width (375 px) and at 150 % Windows scaling.

Each step says what to do, then **Expected:** what should happen.

## 1. Anyone, not signed in

1. Open the home page and search `DEMO-C7M-0001`.
   **Expected:** the public page opens:
   - the Passport badge, but no Battery badge;
   - the key figures and the public sections as tabs;
   - the QR code, and the family's or model's picture.
2. Scan or open the QR code's address.
   **Expected:** the same public page.
3. Open `/dpp/DEMO-C7M-0001/detail`.
   **Expected:** the sign-in page.
4. Search `DEMO-C7M-0002` (a draft).
   **Expected:** not found. Drafts are the team's only.
5. Search `DEMO-C7M-0003`.
   **Expected:** the passport says it is invalid, with the date, and no reason.
6. Search `DEMO-C13M-0001`.
   **Expected:** the newest passport, with a line that it replaces an earlier passport of this battery. No battery status is named.
7. Print the public page (Ctrl+P, A4).
   **Expected:** the QR code, the page's address and "Printed …" at the top, every tab open, and no menus or buttons.

## 2. Cluster user: `demo.north.user@example.test`

1. Open the Registry.
   **Expected:** DEMO-C7M-0001 and DEMO-C7M-0003 only (demo-north's published batteries), each with both badges.
2. Open DEMO-C7M-0001's detail page (its icon in the Registry).
   **Expected:**
   - tabs Purchaser, Shared, Live values, Modules, then the public sections; no Authorities tab;
   - every value says when it was measured or entered, and how;
   - the Purchaser tab ends with the repair history.
3. The Live values tab.
   **Expected:** a small chart per value for the last 7 days, and "All readings" folded below.
4. Open `/dpp/DEMO-C13M-0001/detail` (demo-south's).
   **Expected:** the public sections only.
5. Open `/help`.
   **Expected:** "Your cluster's batteries", "Statuses", "Print a passport" and "Need more access?". No cluster admin or team parts.

## 3. Cluster admin: `demo.north.admin@example.test`

1. Open the Registry.
   **Expected:** demo-north's batteries, and a "Waiting for you" card for DEMO-CORE-0001 from demo-fleet.
2. Open DEMO-C7M-0001's battery page (its battery-page icon).
   **Expected:**
   - a header with the picture, the two badges and the facts;
   - a side menu with one section at a time;
   - on a phone, a section picker, and the actions in a bar at the bottom.
3. Live values: change the state of charge and save.
   **Expected:** saved in place, with no new version; the detail page shows the new value with its time.
4. Accident reports: "Add a report" with a small PDF.
   **Expected:** listed. The passport's accident page lists it for the battery's readers.
5. Change status: choose "Repaired", describe the repair, keep the pre-filled values, Next, confirm.
   **Expected:**
   - the battery stays re-used;
   - the repair history gains the entry;
   - a new version is published;
   - every choice and button in the window is fully visible at every size.
6. Add a note.
   **Expected:** the history gains a "Correction" entry, with no new version.
7. Upload a file: download "An upload file" from `/help`, choose it, then "Check the file".
   **Expected:** the check lists what would change, including the repair line and the kept values. "Save" saves it, and it shows under Earlier uploads.
8. Hand over…: type `demo-south`.
   **Expected:**
   - the app shows "Demo South Operations" to confirm;
   - after confirming, the battery waits and stays demo-north's until demo-south's admin accepts.
   - (Cancel it, or have `demo.south.admin` decline, to keep the demo as it was.)
9. On the Registry's "Waiting for you" card, press Accept for DEMO-CORE-0001.
   **Expected:** it becomes demo-north's. `demo.fleet.user` then sees it as the public does.
10. Cluster admin, Users and API tokens.
    **Expected:**
    - adding a user to demo-north works;
    - a new token's value is shown once;
    - "Deactivate token" switches a token off.
11. Open a demo-south battery's page address, e.g. a manage link copied from `demo.south.admin`'s Registry.
    **Expected:** refused.

## 4. The team

1. Batteries tab.
   **Expected:** every battery, drafts included, one row per battery, with both badges.
2. DEMO-C7M-0002 (a draft).
   **Expected:**
   - the missing values are listed once;
   - each field says required by Feb 2027, required later or conditional;
   - Publish stays blocked until the Feb 2027 values are in.
3. Fill them in and Publish.
   **Expected:**
   - version 1 is published, signed, with a QR code;
   - the public page opens from the QR code;
   - the detail page says "Signature: Verified".
4. Change a model value used by published batteries.
   **Expected:** the editor lists the affected passports and asks before publishing new versions.
5. DEMO-C7M-0001, More: Mark as invalid or for deletion…, Invalid, with a reason.
   **Expected:** the header's red line shows the reason and one Undo. The public page says invalid. Undo it.
6. DEMO-C13M-0001.
   **Expected:**
   - the chain of three passports, the older two inactive and read only;
   - "Change status" isn't offered on an inactive passport.
7. A published battery: Change status, Remanufactured.
   **Expected:**
   - the battery page asks who places it on the market;
   - "Scania" makes a draft new passport from the old battery;
   - publishing it makes the old one inactive and links the two.
8. Cluster: Change on a battery.
   **Expected:** it moves at once, and the move is in the audit log.
9. Clusters tab: Demo data, Clear demo data.
   **Expected:**
   - the demo records go and real data stays;
   - Reset demo data brings them back.

## 5. Market surveillance: `demo.msa@example.test`

1. Registry.
   **Expected:** every published battery, the invalid and marked ones included.
2. DEMO-C7M-0001's detail page.
   **Expected:** Purchaser, Shared, Authorities, Live values and Modules, then the public sections.
3. DEMO-C7M-0003.
   **Expected:** invalid, with the reason "Demo: wrong carbon footprint figures".
4. On an active passport, Mark as invalid… with a reason.
   **Expected:**
   - the public page says invalid;
   - no Undo for market surveillance (only the team undoes it);
   - the battery takes no new versions, status changes or live values.
5. Any battery page address (`/admin/batteries/…`, `/cluster-admin/batteries/…`).
   **Expected:** Not allowed.

## 6. Notified body, Commission and the legitimate-interest roles

For each account, open the Registry and DEMO-C7M-0001's detail page.

| Account | Expected tabs before the public sections | Shared tab lists |
|---|---|---|
| `demo.notified-body@example.test` | Authorities | — |
| `demo.commission@example.test` | Shared, Authorities | Materials, dismantling, part numbers, spare parts, safety measures |
| `demo.repair@example.test` | Shared | Materials, dismantling, part numbers, spare parts, safety measures |
| `demo.recycling@example.test` | Shared | Materials, dismantling, safety measures |
| `demo.treatment@example.test` | Shared | Safety measures |

**Expected for all five:**
- the Registry lists every published battery;
- no Purchaser, Live values or Modules tab;
- `/help` shows "What your account adds", naming exactly these values;
- a restricted document (e.g. the dismantling manual) downloads only for the roles whose tab lists it.

## 7. The API

Use the tokens copied after the reset, on `/help/api`'s workbench or with any HTTP client (`Authorization: Basic <base64(token:)>`, as `/help/api` shows).

1. demo-north's "Read" token, `GET /api/external/v1/clusters`.
   **Expected:** demo-north only.
2. The same token, `GET /api/external/v1/batteries/<a demo-south battery ID>`.
   **Expected:** refused.
3. The same token, `POST /api/external/v1/batteries/<DEMO-C7M-0001's battery ID>/live-values`.
   **Expected:** refused (read only).
4. demo-north's "Read and write live values" token, the same POST with a state of charge.
   **Expected:** saved. The live value history shows it.
5. The same token, `POST /api/external/v1/uploads?preview=true` with the help's upload file.
   **Expected:** the battery is refused: "status changes are made on the battery's page" (the file carries a repair).
6. The team's "Full access" token: `GET /api/external/v1/batches`, then `POST /api/external/v1/batteries`.
   **Expected:** a new draft battery with its battery ID.
7. `GET /api/passports` with a cluster token.
   **Expected:** refused (team only).

## Report

Use the bug report template in `docs/qa-test-pack.md`, with the account, battery, page address, screen width and browser.
````

Replace `docs/sample-cluster-test-accounts.md` with exactly this content:

````markdown
# Demo Accounts, Clusters and Batteries

What "Reset demo data" makes (decision 32). The team presses it at the end of the Clusters tab; "Clear demo data" removes it all again and keeps real data. Every record it makes carries a demo mark.

**Passwords:** every demo account signs in with the one password set on the server (`DEMO_USERS_PASSWORD`). The project owner gives it to testers. It is never written here or shown in the app. Without that setting, the demo data comes without accounts, and the Demo data card says so.

**Tokens:** the reset shows each token's value once, in its result window. Copy the values then. They are never stored readable and never written here.

## Clusters

| Cluster ID | Name | Batteries |
|---|---|---|
| `demo-north` | Demo North Operations | DEMO-C7M-0001, DEMO-C7M-0002, DEMO-C7M-0003 |
| `demo-south` | Demo South Operations | DEMO-C13M-0001 (and its two new passports), DEMO-C13M-0002 |
| `demo-fleet` | Demo Fleet Operations | DEMO-CORE-0001 (waiting to go to demo-north), DEMO-CORE-0002, DEMO-CORE-0003 |

## Accounts

| Account | Role | Cluster |
|---|---|---|
| `demo.north.admin@example.test` | Cluster admin | demo-north |
| `demo.north.user@example.test` | Cluster user | demo-north |
| `demo.south.admin@example.test` | Cluster admin | demo-south |
| `demo.south.user@example.test` | Cluster user | demo-south |
| `demo.fleet.admin@example.test` | Cluster admin | demo-fleet |
| `demo.fleet.user@example.test` | Cluster user | demo-fleet |
| `demo.msa@example.test` | Market surveillance | — |
| `demo.notified-body@example.test` | Notified body | — |
| `demo.commission@example.test` | Commission | — |
| `demo.repair@example.test` | Repair, repurposing and remanufacturing (legitimate interest) | — |
| `demo.recycling@example.test` | Recycling and battery treatment (legitimate interest) | — |
| `demo.treatment@example.test` | Authorised treatment and WEEE facilities (legitimate interest) | — |

The team's account is not demo data: the project owner gives testers a team account if they test the team's pages.

## Tokens

| Token | Access | Used for |
|---|---|---|
| Each demo cluster, "Read" | That cluster's batteries, read only | API reads |
| Each demo cluster, "Read and write live values" | The same, plus live values and uploads of live values | The battery system's feed |
| "Demo team (full access)" | Every battery; creating and publishing | The team's own systems |

## Batteries

| Battery | Family and model | Cluster | State | Shows |
|---|---|---|---|---|
| DEMO-C7M-0001 | Compact, Compact 7M | demo-north | Published, several versions; battery re-used (repaired) | The repair history, an accident report, a week of live values, 7 modules, an export from the battery system and a file from its cluster admin under "Upload a file" |
| DEMO-C7M-0002 | Compact, Compact 7M | demo-north | Draft | Missing values and what blocks publication |
| DEMO-C7M-0003 | Compact, Compact 7M | demo-north | Published, then marked invalid by `demo.msa` | The invalid notice; the reason "Demo: wrong carbon footprint figures", seen only by the team and market surveillance |
| DEMO-C13M-0001 | Compact, Compact 13M | demo-south | Repurposed, then remanufactured: three linked passports, the two older inactive, the newest active | The passports' chain and their notices |
| DEMO-C13M-0002 | Compact, Compact 13M | demo-south | Draft | |
| DEMO-CORE-0001 | Core, Core | demo-fleet | Published; an accident report; handed over to demo-north, waiting | "Waiting for you" on demo-north's Registry; Accept or Decline |
| DEMO-CORE-0002 | Core, Core | demo-fleet | Draft | |
| DEMO-CORE-0003 | Core, Core | demo-fleet | Waste, marked for deletion | The marked-for-deletion notice |

Drafts are seen by the team only. Every ID starts with `TEST-`, and the pages show a "Test passport" tag.

## Sample files

On `/help`, signed in as the team or a cluster admin, the "Upload a file" part offers two downloads for DEMO-C7M-0001:
- an upload file, with live values and a repair;
- a battery system export (mapping BMS2_ACO), with made-up values.
````

Then remove the old app's manual test flow:

```bash
git rm -q docs/passport-admin-registry-manual-test-flow.md
```

- [ ] **Step 4: Run the tests and the classes that read these documents**

Run: `dotnet test BatteryPassWeb.Tests --no-build --filter "FullyQualifiedName~TestPackTests|FullyQualifiedName~ExternalApiBatteryCreateTests|FullyQualifiedName~BatteryFamilyAccessApiRevisedTests|FullyQualifiedName~ProductTemplateWorkflowLayoutTests|FullyQualifiedName~AdminFeedbackImplementationTests|FullyQualifiedName~ResetLauncherTests"`
Expected: `Passed!  - Failed:     0, Passed:    24`.

Then: `grep -rln "qa-test-pack\|end-user-testing-guide\|sample-cluster-test-accounts" BatteryPassWeb.Tests --include=*.cs`
Expected: only `BatteryPassWeb.Tests/Docs/TestPackTests.cs`.

- [ ] **Step 5: Commit**

```bash
git add -A BatteryPassWeb.Tests docs/qa-test-pack.md docs/end-user-testing-guide.md docs/sample-cluster-test-accounts.md
git commit -m "docs(dpp): the test pack for the new app: one starting point, a script per reader and the demo data, with no passwords" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: Final checks

- [ ] **Step 1: Run the whole suite**

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --disable-build-servers --blame-hang --blame-hang-timeout 10m`
Expected: `Passed!  - Failed:     0`, with 6 fewer tests than before this plan (e.g. 2528 after 6a-2's 2534).

- [ ] **Step 2: Build without warnings**

Run: `dotnet build web/BatteryPassWeb.csproj --disable-build-servers --nologo -v q --no-incremental`
Expected: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 3: Confirm everything is committed and `site.css` is untouched**

Run: `git status --short` and `git diff --stat HEAD~2 -- web/wwwroot/css/site.css`
Expected: nothing listed by either.

## After this plan: the product owner's look

These are not steps of this plan. The managing session brings the three documents to the product owner to read.
- Step 6c then follows `docs/end-user-testing-guide.md` as its script.
- Anything it finds wrong in the pack is fixed with the app's own findings.

## Next

6b-1 (the Help per role) if it hasn't run, then 6c, the role-matrix check.
