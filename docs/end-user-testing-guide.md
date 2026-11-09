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
9. Battery IDs tab: release VPC-12221 to VPC-12230 with "Release new IDs": quantity 10, reference "PO 4512, line 2", model Compact 7M.
   **Expected:** the release's page opens with "Released 10 battery IDs. Export them to Excel for the labels.", and its ten IDs Available, each with its battery ID (`UN778474481-VPC-12221` …).
10. The same, with VPC-12228 to VPC-12240 and quantity 12, and VPC-12401 twice and vpc-12402 in the list.
   **Expected:** nothing is released; the Release new IDs tab lists the reasons ("that is 13 IDs, not 12", "already released", "in this release twice", "only capital letters A–Z, digits 0–9 and single dashes") and keeps what you typed.
11. The Releases tab, then Open on PO 4512, line 2, and Export to Excel.
   **Expected:**
   - Releases lists PO 4512, line 2 and the demo data's release, tagged Demo; the counts at the top are the real IDs only;
   - an Excel file with that release's ten IDs, one row per ID: serial number, battery ID, QR code address, model, reference, released by and when, status.
12. Create battery: a Compact 7M batch, pick VPC-12221.
   **Expected:** the picker offers Compact 7M's IDs first, then any model's, then the demo spares; the draft shows `UN778474481-VPC-12221` and its QR code; the ID is Used.
13. On that draft, More: Change its battery ID…, VPC-12222.
   **Expected:** the draft now has VPC-12222 and its ID; VPC-12221 is Available again.
14. All IDs: search VPC-12230; in its row's ⋯ menu, Void…, with a reason. Then tick "Show voided", and then "Show demo IDs".
   **Expected:** it leaves the list until "Show voided" is ticked, then shows as Voided with the reason; the create picker no longer offers it; the demo IDs show only once ticked.
15. Signed out, open `/25S/UN778474481-VPC-12223`, then `/25S/UN778474481-VPC-12230`.
   **Expected:** "Not published yet", then "Not available".
16. Clusters tab: Demo data, Clear demo data.
   **Expected:**
   - the demo records go, the "Demo" release with them, and real data stays (PO 4512's IDs included);
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
6. The team's "Full access" token: `GET /api/external/v1/batches`, then `POST /api/external/v1/batteries` with `"batterySerialNumber": "DEMO-SPARE-0001"`.
   **Expected:** a new draft battery with that released ID. The same with a serial number that isn't released is refused.
7. `GET /api/passports` with a cluster token.
   **Expected:** refused (team only).

## Report

Use the bug report template in `docs/qa-test-pack.md`, with the account, battery, page address, screen width and browser.
