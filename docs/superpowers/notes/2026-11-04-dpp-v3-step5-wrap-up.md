# DPP V3 Step 5: Live Data and Uploads — Wrap-up

Step 5 is built in four plans. Each was run in its own session and ended with a working app; a review and two rounds of
the product owner's test followed. This note says:
- what there is now and what was decided;
- what the owner's test found;
- what was left open on purpose;
- what step 6 should know.

Decision 35 holds the product owner's answers (Q1–Q25). The mock-up approved on 2 Nov 2026 is
`docs/mockups/dpp-v3-step5-live-data-and-uploads.html` (screens B1–B4). The owner accepted step 5 on 4 Nov 2026 ("all
good").

## What is built

| Part | Plan | What it adds | Tests |
|---|---|---|---|
| 5a | `2026-11-02-dpp-v3-step5a-upload-engine.md` | The upload engine and the upload log (see below), the API's upload call, and step 4's follow-up: hand-overs compare normalised cluster IDs. | 2233 → 2255 (+22) |
| 5b | `2026-11-02-dpp-v3-step5b-battery-system-export.md` | The battery system's export (mapping BMS2_ACO), read as it is (see below). | 2459 → 2469 (+10)¹ |
| 5c | `2026-11-02-dpp-v3-step5c-upload-card.md` | "Upload a file" on each battery's page (see below), which replaces "Fill from export". | 2469 → 2481 (+12) |
| 5d | `2026-11-04-dpp-v3-step5d-status-uploads.md` | Status changes and repairs by upload, the help's sample files and the demo uploads (see below). | 2481 → 2499 (+18) |
| Review and owner's test | ed53f75, f4985aa, 80fbb49 | The fixes below. | 2499 → 2517 (+18) |

¹ Between 5a and 5b the battery page was redesigned (R1–R7, their own plans), which took the suite from 2255 to 2459;
plan 5b was updated onto the redesign before it ran. Step 5 added 80 tests in all. The final whole suite is 2517/2517,
with a 0-warning build.

**5a, the upload engine:**
- the app's own format, `dpp-v3-upload/1`;
- every battery goes through the one save pipeline, which decides what each sender may change;
- the upload log `dpp_uploads`, beside the audit log;
- the external API's `POST /api/external/v1/uploads`: up to 500 batteries and 2 MB, with `?preview=true`.

**5b, the battery system's export (mapping BMS2_ACO), read as it is:**
- #63 from its readable text;
- #83 in full cycles;
- E1–E4 in whole minutes;
- its module list (an empty one never overwrites);
- #17 only for the team's drafts without one;
- notes for what the export lacks;
- the remaining capacity, kept as app data and offered in the status window's fade suggestion.

**5c, "Upload a file" on each battery's page:** a side-menu section of the team's and the cluster admin's battery
pages, after "Live values", and none on a read-only page.
- One file for that battery.
- A check before saving, kept an hour (`dpp_upload_checks`, TTL), then "Save".
- The battery's Earlier uploads: the team sees all of them, a cluster admin their cluster's.
- "Fill from export" is merged into it.

**5d, status changes and repairs by upload:**
- on the battery's page only, through the page's status change (the same rules, version and audit line);
- the status change is saved first, then the live values, and a refusal saves nothing of the battery;
- the check shows a status line and the kept values;
- the API refuses status changes;
- a battery system's uploads are named "Battery system" in Earlier uploads;
- the help's two sample files, for the team and cluster admins only;
- DEMO-C7M-0001's two demo uploads.

**The demo data** shows each part:
- DEMO-C7M-0001 has an export from demo-north's battery system (7 modules, remaining capacity 131 Ah) and a file of live
  values from its cluster admin;
- the help page's upload file repairs it;
- the demo clusters' tokens, shown once after a reset, send through the API.

**New app data, outside the passport:**
- the upload log `dpp_uploads`: who, when, page or API, file, format, and per battery what happened to each value, the
  status line and the kept values;
- the kept checks `dpp_upload_checks`, for an hour;
- the pack's remaining capacity.

Both collections are made with their indexes at the app's first start: `at` and `pack_at` on the log, and the TTL index
`expires` on the checks. The reset tool treats them as test data. Clearing the demo data removes the demo uploads and the
demo batteries' kept checks.

## The decisions (decision 35)

- **Everything goes through the one save pipeline** (decision 8). The team may also send a battery's own values; a
  cluster admin and a battery system only live values (J-10) and the module list; market surveillance nothing.
- **Uploads happen on each battery's page, not on a central page** (the product owner, 2 Nov): one file for that
  battery. A file for many batteries goes through the API.
- **The export:**
  - any BMS2_ACO version is read, with a note when it isn't 01.32.07;
  - #63 from the readable text, rounded to a whole percent;
  - E1–E4 rounded down to whole minutes;
  - #83 rounded down to full cycles;
  - its module list replaces the battery's, whoever sends it.
- **The check before saving is the confirmation:** no extra window; a file that changes nothing is shown without
  "Save"; the check is kept an hour for its checker.
- **Who sees which uploads:** the team every upload of a battery; a cluster admin their cluster's (its admins' and its
  battery system's), not the team's.
- **Status changes by upload** (Q20–Q25):
  - in the app's format: `batteryStatus` among the values, or `"reason": "repair"` with `repairDescription` and
    `repairDate` (else the day of `measuredAt`);
  - a repair sets re-used, as on the page;
  - re-stated values left out of the file are the battery's current ones, shown as kept;
  - a refused status change saves nothing of the battery;
  - the API refuses a battery with a status change. This changed 5a, where `batteryStatus` was one refused value.
  - History and the audit log use the page's own line.
- **Sample files:** two, for DEMO-C7M-0001 only, as downloads for the signed-in team and cluster admins.

## What the review and the owner's test found, and how it was fixed

**The review** (whole-page walk per role at 1280 and 375 px after a demo reset) found two faults, fixed in ed53f75 with
6 tests:
- **An API token's ID on screen.** The live values said "…from the battery system by api-token:6aeaebcd…". The live values
  card printed the stored sender, which for an API token is its internal ID. `BatteryCards.By` leaves the ID out, and
  Earlier uploads name a team token's upload "API token".
- **A long e-mail cut at 375 px** in Earlier uploads. The card clipped one long word. The check's head and the rows'
  cells now wrap anywhere.

**The owner's first test** found three problems, fixed in f4985aa with 10 tests:
- **The API help's details buttons needed a sideways scroll**, for every call, not only Uploads (it dates from step 3c).
  `site.css` gave the calls' table `min-width: 1040px` in a 958 px column. `bp-api-help.css` sets the minimum to 0 and lets
  the long paths wrap.
- **A "Not checked" message followed the owner to another battery and another account.**
  - The battery pages' one-time messages carried no battery.
  - The cluster admin's page and `_CardMessages` read them with `TempData.Peek`, which never uses them up.
  - The TempData cookie belongs to the browser, so it outlived a sign-out.
  - The fix, `CardMessages` (Controllers): every message, the kept check ID and the saved upload ID carry their battery;
    the page takes them once and keeps only its own battery's (another battery's are dropped); the views read ViewData
    only; signing in and out clears and saves TempData. A kept check also shows only to the person who checked it.
- **"The Change status window doesn't scroll"** came back as the first suspected cause. The windows got an explicit
  `max-height` and `overflow-y: auto`, and the owner steps were corrected: the 131 box is in the window's second step,
  after "Repaired" and "Next". That wasn't the cause.

**The owner's second test** found the real cause, fixed in 80fbb49 with 2 tests:
- **The cause:** the status window's Cancel/Next bar was `position: sticky; bottom: 0` with `margin: 0 -20px -20px` (from
  redesign R1).
  - The −20 px bottom margin made the window 20 px shorter than its content.
  - The sticky bar sat over its last 20 px.
  - The overlap doesn't count as overflow, so the window had nothing to scroll.
  - Each step's last choice ("Waste", "Correct the values") or last value was covered at every screen size whenever the
    step fit. The owner saw it at 150 % Windows scaling.
- **The fix:** each step has a head, a body that alone scrolls, and its buttons below the body, never over it.
- **The other windows** (note, mark, cluster, publish, hand-over, remove confirm) were checked the same way. None covers
  its content.

## The lesson for step 6's checks

- **Check that each item is fully visible, not only that the last button can be reached.** In every window and every
  step, scroll each choice, field and button into view inside the window, then check it is fully inside and on top
  (`elementFromPoint` at its top and bottom). The status window passed "last button reachable" and "stays on screen" in
  two reviews, but failed this check at every size.
- **Test with one browser across accounts.** Sign out and in as another role in the same browser, and reload the page
  after a message. Per-account browser contexts hide whatever the browser keeps: the TempData message survived a sign-out
  and reached two other accounts.
- **Test at the owner's real scaling** (e.g. 150 % on a 1920×1080 screen: about 1333×918 CSS px at device scale 1.5), with
  a real mouse wheel, in Edge and Chrome.
- **A "before" run must use the old code end to end.** A dev app that serves `wwwroot` from the source folder picks up a
  CSS change at once, while its views stay compiled. Serve the old stylesheet, or rebuild.
- **Read every value an owner step names on the page itself.** Reading a field's value without checking it is visible
  sent the owner to the wrong step of the window.

## Open follow-ups

- **Small tap targets older than step 5:**
  - the status-help button, 22 px;
  - breadcrumbs, 22 px;
  - "More details", 21 px;
  - "Change", 19 px;
  - the sections' jump links, 19 px.

  These are under WCAG 2.5.8's 24 px, noted in the R6b phone pass and again in step 5's review.
- **The pack temperature** isn't in the export (design 8.5's request to the export team); each export's notes say so.
- **Uploads logged before 5c** show no values in Earlier uploads, and those before 5d no status line. None exist outside
  test data.
- **History doesn't say a change came from an upload.** The owner kept the page's line; the audit log tags it "Upload".
- **A value's sender is stored as given:** `Who` is "api-token:<ID>" for an API token, and only the pages leave the ID out.
- **A message for a battery is dropped** if its page isn't the next battery page shown. A refused check is then not seen
  again; the file can be checked again.
- **Step 4's open points stand:** a hand-over waiting on a passport that stops being active isn't cancelled by itself,
  and there is no e-mail for hand-overs.

## What step 6 should know

Step 6 is go-live readiness (design 14):
- the identifier scheme switch, once O-1 is decided;
- the help pages and test pack rewritten;
- manual checks over the role matrix;
- the deployment to AWS.

What step 5 leaves for it:
- **Help rewrite:** the guide's "Upload a file" part with the two sample files (`UploadSamples`,
  `GET /help/samples/{name}`) and the API page's Uploads call are written from the decisions above, and can be kept.
- **Role matrix checks:** a starting point is in the managing session's notes folder:
  - step 5's walk, which checks 8 roles at 1280 and 375, and access at the server;
  - the owner's steps script;
  - the window check, which looks for anything covered;
  - the cross-account message check.

  Use the lessons above. A walk that signs in many times quickly hits the sign-in rate limit, so run a throwaway app with
  `ENABLE_RATE_LIMITING=false` for it, never the real one.
- **Deployment:**
  - the app's start makes `dpp_uploads` (indexes `at`, `pack_at`) and `dpp_upload_checks` (TTL index `expires`);
  - no other new setting;
  - the TempData cookie (`.AspNetCore.Mvc.CookieTempDataProvider`) is now cleared on sign-in and sign-out.
- **The identifier switch** touches how an upload finds its battery: by battery ID, passport ID or serial number, as the
  API finds it. The export finds it by serial number.
- **Windows:** a new window with a fixed button bar should use the status window's head, body and buttons, not a sticky
  bar with negative margins.
- **Testing:**
  - builds and tests with `--disable-build-servers` and `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0`;
  - whole suites with `--blame-hang --blame-hang-timeout 10m`;
  - each task runs only its own tests, and the whole suite once per plan;
  - a clean clone checks out `site.css` with CRLF and fails two `ApplicationAuditCoverageTests`, so copy the worktree's
    `site.css` into a scratch clone.
