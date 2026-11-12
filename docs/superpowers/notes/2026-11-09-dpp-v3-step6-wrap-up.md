# DPP V3 Step 6: Go-live Readiness — Wrap-up

Step 6 is built in ten plans: five parts (6a–6e), with 6a, 6b and 6d split further. Each plan was measured on a clean clone,
then replayed in the worktree with one whole suite. Each ended with a working app and a 0-warning build. The product
owner saw each part's screens before its plan, and tried 6d-2 on their own app. This note says:
- what there is now and what was decided;
- what the checks and the owner's trial found;
- what the cut-over needs;
- what was left open.

The split is the managing session's `step6-split-proposal.md` (4 Nov 2026). The identifier choice (O-1) is
`step6-o1-identifier-options.md`, and the release flow is `step6d-id-release-design.md`.

## What is built

| Part | Plan | What it adds | Tests |
|---|---|---|---|
| 6a-1 | `2026-11-04-dpp-v3-step6a1-tap-targets-and-database-name.md` | Text buttons and the header facts' links at least 24 px tall. In Production the app refuses to start without `MONGODB_DB`. | 2517 → 2528 (+11) |
| 6a-2 | `2026-11-04-dpp-v3-step6a2-print-style.md` | The public summary and the detail page print on A4 (see below). | 2528 → 2534 (+6) |
| 6e | `2026-11-04-dpp-v3-step6e-aws-cutover-runbook.md` | The cut-over runbook, `docs/aws-v3-cutover.md`. Docs only, no suite. | — |
| 6b-2 | `2026-11-04-dpp-v3-step6b2-test-pack.md` | The test pack rewritten for the new app, with no passwords. | 2534 → 2528 (−12, +6)¹ |
| 6b-1 | `2026-11-04-dpp-v3-step6b1-help-per-role.md` | The Help for every reader, and a small Help for visitors. | 2528 → 2534 (+6) |
| 6c | `2026-11-05-dpp-v3-step6c-role-matrix-fixes.md` | The role-matrix check, and the fixes for its seven faults (see below). | 2534 → 2542 (+8) |
| 6d-1 | `2026-11-06-dpp-v3-step6d1-battery-id-scheme.md` | The real battery and passport IDs (DUNS), shown short. | 2542 → 2579 (+37) |
| 6d-2 | `2026-11-08-dpp-v3-step6d2-released-battery-ids.md` | Battery IDs released in advance, picked when a battery is created. | 2579 → 2602 (+23) |
| 6d-2b | `2026-11-09-dpp-v3-step6d2b-id-settings-and-tabs.md` | Missing ID settings refused with a red notice; the Battery IDs page in tabs with a page per release. | 2602 → 2611 (+9) |
| 6d-3 | `2026-11-09-dpp-v3-step6d3-help-and-test-docs.md` | The Help and test documents for battery IDs, and the role check re-run. | 2611 → 2615 (+4) |

¹ 6b-2 removed the 12 tests that pinned the old app's documents (including one that asserted the old admin password was
in the doc) and added 6. Step 6 added 98 tests in all. The final whole suite is 2615/2615, with a 0-warning build.

**The commits** on `feature/dpp-v3-step2d-team-pages`, each after its plan's own commit:
- 6a-1: 594ee6d, 879185c;
- 6a-2: d2770d5;
- 6e: e5b238c;
- 6b-2: f27d58f;
- 6b-1: dc488fa;
- 6c: db7bd45;
- 6d-1: 0489ba4;
- 6d-2: abd0ff7;
- 6d-2b: 045648f;
- 6d-3: 9a15ec1.

**6a, tap targets and paper:**
- `.bp-text-button` ("Change", "Suggest the fade", …) and the header facts' links get a 24 px box (WCAG 2.5.8). The
  "Change" box overhangs its line, so the facts row stays level.
- A deployment that forgets `MONGODB_DB` would write into `battery_pass_demo`, the live app's database. In Production the
  app now refuses to start instead.
- Printing (`bp-print.css`, loaded for print only):
  - every tab and section open, one after another;
  - the live values as a table of each value's latest reading;
  - the QR code, the page's address and a "Printed …" line at the top;
  - document links with their address, and the "Test passport" tag kept;
  - no bars, menus, buttons or footer.

**6b, the Help and the test pack:**
- `/help` has a part per reader of design 5.2, under that reader's condition: everyone, cluster users and admins, market
  surveillance, the notified body, the Commission, the three legitimate-interest roles and the team.
- "Statuses" is the same list as the badges' explainer. Visitors get a small Help (three parts) and a Help link in the
  top bar.
- `docs/qa-test-pack.md` is the one starting point: "Reset demo data", one browser across accounts, the demo accounts
  without passwords (`DEMO_USERS_PASSWORD`), the scenario matrix, the acceptance checks and the known limitations.
- `docs/end-user-testing-guide.md` is the script per reader. `docs/sample-cluster-test-accounts.md` is the demo data's
  reference.
- The old `passport-admin-registry-manual-test-flow.md` is gone (the owner, Q3); it stays in Git history.

**6c, the role-matrix check:** every reader against every page, window and API call. It ran in one browser across
accounts, at 1280, 375, and 1333×918 at 150 % in Edge and Chrome. Each control was checked as fully visible and on top.
The script (`walk6c.mjs`) is reused by 6d.

**6d, the battery IDs:**
- **Battery ID (#6):** an ISO/IEC 15459 item ID under issuing agency `UN` (DUNS), `UN` + `778474481` + the serial
  number's letters and digits, upper-cased (`UN778474481VPC12221`). It is written as
  `https://www.sib-scania-eubp.eu/25S/<item>`, which opens the public page as a scanned QR code does.
- **Passport ID (#5)**, per version: `…/dpp/<item>/v<n>`.
- **A new passport** of the same battery adds `R1`, `R2`, … to its own cleaned serial number.
- **Demo batteries** are marked `TEST` after the DUNS (`UN778474481TESTDEMOC7M0001`). A real serial number may not start
  with TEST.
- **Shown short** on pages; the full address is the link, what the copy button copies, and what prints.
- **#7 and #12** take the form `iso6523-actorid-upis::0060:<DUNS>`, with a gentle note for any other form.
- **Released in advance:** the team-only Battery IDs tab.
  - At the top, the real IDs by status and "Release new IDs".
  - Three tabs: Releases (first), All IDs (demo IDs hidden unless asked for) and Release new IDs.
  - Each release has its own page, with "Export to Excel" at the top; a new release lands there.
  - A release is all or nothing, with every reason listed, and race-safe (a transaction on Atlas).
  - Statuses: Available, Used (a draft has it), Published, and Voided with a reason, never reused.
- **Creating a battery** picks an available released serial number, on the page and in the API. A draft may swap its ID,
  and a removed draft gives it back.
- **Scanning** a released, unpublished ID shows "Not published yet"; a voided one "Not available".
- **Without the three ID settings** the Battery IDs page shows a red notice naming the missing ones. Nothing real is
  released, and no battery is created from a released real ID. The demo data still works.
- **Demo data:** "Reset demo data" releases a "Demo" set, makes the demo batteries from it, and leaves four spares
  (DEMO-SPARE-0001 to 0004). Clearing the demo data removes only demo IDs.

**New app data:**
- `dpp_battery_ids`, the released IDs: `_id` is the cleaned serial number, with indexes on release, battery, status and
  model, made at the app's first start.
- The reset tool (`tools/V3Reset`) treats it as new test data, like the batteries it points to.
- `dpp_counters` is no longer used.

## The owner's decisions

- **Print** (P1–P5, 4 Nov 2026, "Approve all"):
  - both pages print, with every tab and section open;
  - the live values as a table of the latest readings, not charts;
  - the QR code and address on the first page;
  - no screen furniture on paper;
  - plus a "Printed" time, document addresses, and the Test passport tag.
- **Help** (Q1, Q2, Q4, 4 Nov 2026, "Approve help pages"):
  - a part per reader;
  - "Statuses" shared with the badges' explainer;
  - a small public Help for visitors.
- **Test pack:** no passwords in any document (decision 32); the old manual test flow removed (Q3).
- **6c:** on phones, the lists' "What do these mean?" sits beside the count ("Beside the count", 5 Nov 2026).
- **The identifier scheme (O-1, 4 Nov 2026): DUNS** (`UN`, `778474481`, Scania Industrial Batteries AB), on the
  passport's own domain `https://www.sib-scania-eubp.eu/`.
- **Serial-based:** the ID's tail is the serial number's letters and digits. A new passport adds R1, R2, R3 (5 Nov 2026).
- **Released in advance** (6 Nov 2026):
  - #6 exists from release, so a draft has its QR code again (decision 30 stands);
  - a reference is required and a model is optional;
  - unused IDs are voided, never deleted;
  - the API creates from a released serial number but can't release.
- **Export: CSV, then .xlsx.** The owner chose CSV. Tested in Excel on a Swedish-format PC, a comma CSV opened in one
  column and 000123 became 123. A CSV splits for one locale only, so the manager chose a real .xlsx written with .NET's
  own zip support (no package).
- **Demo IDs:** "Same shape, marked TEST" (5 Nov 2026).
- **#7 and #12:** the ISO/IEC 6523 form with the DUNS; a gentle note, never a refusal.
- **6d-2b** (after trying 6d-2, 9 Nov 2026): missing settings must not fail silently; the Battery IDs page in tabs with
  a page per release ("okay approved").
- **The cut-over (6e, 4 Nov 2026):**
  - one environment, the new app replacing the old in place ("Kept is enough");
  - old addresses stop working ("No, demo only");
  - the owner copies the signing key;
  - the key is checked in Atlas, read-only, rather than shown on the page ("No, not needed");
  - both apps stay restorable.

## What the checks and the owner's trial found, and how it was fixed

**6a-1's check:** the header's "Change" box would push the Cluster value 5 px lower. The box overhangs its line instead.

**6c, the role-matrix check (5 Nov 2026, at dc488fa): 887 PASS, 78 FAIL, six faults**, plus a seventh found while
checking the fixes. All fixed in db7bd45 with 8 tests:
1. Another cluster's admin opening a battery page got a bare 404. It is now the app's "Not allowed" page, which doesn't
   say whether the battery exists.
2. The team's "Change" had its bottom 3 px covered by the Cluster value (from 6a-1's overhang). It is now drawn above.
3. The audit log named an API token `api-token:<id>` and a battery `pack pack-<id>`. It now says "Battery system" or
   "API token", and names the battery by its serial number.
4. On a phone, the lists' "What do these mean?" was in the hidden heading row: invisible, still focusable. It is now
   beside the count, on phones only.
5. On a phone, a long Passport badge made the team's battery page scroll sideways. The badge wraps.
6. A battery placed on the market again, whose newest passport was then marked invalid, couldn't be found by its serial
   number. The lookup now skips inactive earlier passports (decision 34).
7. On a phone, the explainer window's words were squeezed beside the widest badge. The list now stacks.

Also as a docs line: design 7.9 now says a repair that changes no passport value keeps its version.

**6d-1 and 6d-2, found while prototyping:**
- MongoDB's driver reports a direct connection to a replica set as "standalone". The store checks the server's own role
  before using a transaction. Found by a test on its own single-node replica set.
- A serial number both released and some battery's gets only the "already released" reason.
- The short-ID lookup builds a regex from the input, so it escapes it.

**The owner's trial of 6d-2 (9 Nov 2026), fixed in 6d-2b (045648f) with 9 tests:**
- **Missing ID settings failed silently.** The owner's launcher lacked them, and real releases got test URNs. Now the
  page shows a red notice naming the missing settings, and real releases and creates are refused with the same reason.
- **The Battery IDs page was hard to find your way in.** A new release's export was lost in a long list. It is now in
  tabs, and a release lands on its own page, with its export first.

**The role check re-run for 6d (6d-3):**
- **First run: 945 PASS, 70 FAIL, two faults**, both from 6d-1's web-address IDs (the test URNs had neither). Both were
  fixed in 6d-2b:
  - on a phone, the passport ID's full address has no spaces to wrap at, so public and detail pages scrolled sideways.
    A field's value may now break anywhere;
  - an API call naming a battery by its full battery ID in the address was "not found": the client escapes its slashes
    (`%2F`). The pages and the API now decode the ID once.
- **Final run, on 6d-2b and 6d-3: 1043 PASS, 0 FAIL.** That is pages 762, windows 246, API 18 and data changes 17. It
  includes the three tabs, a release's page, "Not allowed" for every other reader, and the void windows from the row
  menu.

**6e, found while writing the runbook (read-only checks):**
- DNS, HTTPS and the www redirect are already in place; the cut-over changes only the app version and its settings.
- The code and the settings must change in one update. The live environment sets `MONGODB_DB=battery_pass_demo`, so
  either change alone starts the wrong app against the wrong database.
- The signing key: each signature stores its own public key, so existing passports verify anywhere. The live site
  still needs the PC's key, so that all passports are signed by one key. (This corrects the split note of 4 Nov.)
- API tokens are salted hashes that don't use `EXTERNAL_API_ENCRYPTION_KEY`; it only has to be set.

## The lessons

- **Show the owner whole pages before the plan, and let them try the app.** Every screen was approved from shots. Still,
  the owner's own use of 6d-2 found what the shots couldn't: their launcher's missing settings, and a page that worked
  but was hard to find your way in.
- **A setting that changes what the app issues must be checked where it's used, and said on the page.** A silent
  fallback to test values looks right until the IDs are printed on labels.
- **Re-run the role check after a change of data shape, not only of pages.** The web-address IDs broke a phone layout and
  an API lookup that the test URNs never exercised.
- **Measure locale-dependent output on a real PC.** The CSV looked right in a text editor and split wrongly in Excel.

## The cut-over's prerequisites

The runbook is `docs/aws-v3-cutover.md` (6e). **It was written before 6d and needs one update before the cut-over.**
- **Add the three ID settings** to its settings table and to its one `update-environment` call:
  - `DPP_ID_AGENCY=UN`;
  - `DPP_ID_COMPANY=778474481`;
  - `DPP_ID_BASE_URL=https://www.sib-scania-eubp.eu`.
- **Add two checks after the switch:**
  - the Battery IDs page shows no red notice;
  - after "Reset demo data", a demo battery's ID reads `UN778474481TEST…`, and `/25S/<it>` opens its page.

Without the settings, the live site would refuse every real release and show its red notice.

**The settings, all in that one update:**
- `MONGODB_DB=battery_pass_v3` (the app refuses to start in Production without it);
- `APP_BASE_URL=https://www.sib-scania-eubp.eu/` (QR codes, document links);
- `EXTERNAL_API_ENCRYPTION_KEY` (must be set);
- `DEMO_USERS_PASSWORD` (a strong value of its own, only if demo accounts are wanted);
- `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64`: the PC's key, copied by the owner (runbook section 2.3) and checked in Atlas
  afterwards (`Proof.PublicKeyX`);
- the three `DPP_ID_*` settings above.

**The tags and branches:**
- `old-app-final` at 8c8bfe7 (where `app-v1.2-before-dpp-v3` already points), on the untouched
  `security/demo-api-hardening`;
- `new-app-v3` on this branch's last commit, and the branch on `origin`. The managing session pushes them after step 6.

**Still to confirm:**
- that `sib-scania-eubp.eu` is registered to Scania Industrial Batteries AB (O-1's open point);
- the owner's AWS bill, if the instance needs more memory after a day (runbook check 8).

**The data:**
- `battery_pass_v3` holds demo data only (the owner, 4 Nov 2026);
- "Reset demo data" once after the switch remakes the demo batteries, their links and their Demo release with the live
  address;
- `battery_pass_demo` stays untouched for a roll-back.

## Open follow-ups

- **The battery system's export team:** the export has no pack temperature (design 8.5's request); each export's notes
  say so.
- **Alexandra's access question:** her standards document (STD-800, chapter 3.5) gives the dynamic values to persons
  with a legitimate interest. The V3 file, which the app follows, gives them to the purchaser and market surveillance.
  The app follows the V3 file until she answers. Every page and the API use one rule, so a change is made in one place.
- **The 50-character limit is unverified.** The 36-character limit on a serial number (32 for demo) leaves room for
  `UN778474481` and `R99` within 50 characters, ISO/IEC 15459-4's limit as recalled. The search didn't confirm it in the
  standard's text. The owner or Scania's standards contact should check it.
- **The decisions document** doesn't yet record step 6's choices (a decision 38, as decision 35 did for step 5). Its
  "Still open: Identifier scheme" is out of date.
- **Small items older than step 6:**
  - tap targets that pass by their spacing but are under 24 px: the breadcrumbs (22 px), "More details" (21 px), the
    detail page's jump links (19 px), "Accident information" (20 px) and the model link in the label (15 px);
  - history doesn't say a change came from an upload (the audit log tags it "Upload");
  - a value's sender is stored as given (`api-token:<ID>`); the pages and the audit log leave the ID out;
  - a message for a battery is dropped if its page isn't the next battery page shown;
  - step 4's: a hand-over waiting on a passport that stops being active isn't cancelled by itself, and there is no
    e-mail for hand-overs.

## What comes next

Step 6 completes design 14's go-live readiness. Next:
- the runbook's update above;
- the owner's acceptance of step 6;
- the managing session pushes the branch and the tags;
- the owner follows the runbook.

For later work on this branch:
- **Builds and tests:** `--disable-build-servers` with `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0`;
  whole suites with `--blame-hang --blame-hang-timeout 10m`; one whole suite per plan.
- **Clean clones:** a clean clone checks out `site.css` with CRLF and fails two `ApplicationAuditCoverageTests`; they pass
  in the worktree.
- **The role check:** `walk6d3b.mjs` (the managing session's notes, `step6d3b/`) runs on a throwaway app with
  `ENABLE_RATE_LIMITING=false`, the three ID settings and its own mongod, after "Reset demo data".
- **A new setting that changes what the app issues** gets a check where it's used and a notice on the page, as the ID
  settings now have.
