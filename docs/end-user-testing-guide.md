# End-User Testing Guide

A step-by-step script for each kind of reader, on the demo data. Start with `docs/qa-test-pack.md` (accounts, the matrix of who sees what, the checklist). `docs/sample-cluster-test-accounts.md` lists the accounts and batteries.

Before each round:
- On a test site with the demo on: a team member presses **Reset demo data** (Clusters tab, at the end) and copies the tokens shown once. On the live site and the owner's PC there is no demo (decision 82): skip this, and do part 8 with the team's own account.
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
8. Open `/dpp/UN778474481-TEST-DEMO-C7M-0001.json` (or the battery's address with `Accept: application/json`), with no sign-in.
   **Expected:** the public part as data, with a `proof`, and no #13 (service provider) and no battery status. The same address with a browser's Accept gives the page. Select the Passport ID and Battery ID on the page and copy: each is the whole address, with no line break.
9. Open `/.well-known/did.json`.
   **Expected:** the site's public keys, no private part. The proof's key ID in the data is one of them.
10. Open a passport address the team removed (section 4, step 25), also its `.json` and `/v1`.
   **Expected:** every one answers 410: "This battery's passport no longer exists. It was removed after the battery was recycled or exported."

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
   **Expected:** "Your cluster's batteries", "Statuses", "Print a passport", "Your account" and "Need more access?". No cluster admin or team parts.
6. Open Account (top bar).
   **Expected:**
   - the fields "Your name" and "Your e-mail", and "Your role: Cluster member" as text;
   - the top bar names the cluster;
   - changing the name alone saves; changing the password or the e-mail asks for "Current password", and a new password of fewer than 12 characters is refused.
7. Open a battery that isn't demo (the team makes one with a real released ID and real product data) at its public address, then its detail page.
   **Expected:** the visitor's page only, with no extra sections.

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
   Then choose "Waste" on a battery you can spare, such as one the team has made for the test.
   **Expected:** a second question ("Yes, it is waste" or "No, go back"). After "Yes", the status is waste and only the team can undo it (section 4, step 8).
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
    - "Add user" with a new address makes an account in demo-north;
    - "Add user" with `demo.msa@example.test` and a typed password gives "This person already has an account. They were added to your cluster; …", and the name and password stay as they were;
    - a person from another cluster, a team account or your own account can't be changed: "You can't change this person's account here. Ask the Scania team.";
    - the x beside a cluster asks first ("Remove from cluster"), then removes the person;
    - a typed password of fewer than 12 characters is refused;
    - a new token's value is shown once;
    - "Deactivate token" switches a token off.
11. Open a demo-south battery's page address, e.g. a manage link copied from `demo.south.admin`'s Registry.
    **Expected:** refused.
12. After the team has taken demo-north's written authorisation away (section 4, step 23), open DEMO-C7M-0001's battery page again.
    **Expected:** a line says the Scania team hasn't recorded your authorisation; every section shows its values and no form; Change status and Hand over are disabled and open no window; declining or cancelling a hand-over still works and accepting doesn't.
13. With a model's questions unanswered (section 4, step 21), open a published battery of that model, Status section.
    **Expected:** "No status change can be made now: the passport can't get a new version until …", naming what is missing and who adds it; no "Change status"; "Add a note" is still offered. After the missing live value is typed: "Version N of the passport is published, with the saved changes that were waiting."

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
5. DEMO-C7M-0001, More: Mark as invalid or for deletion…, Invalid, with a reason, then Continue and "Yes, mark it".
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
8. The battery the cluster admin set to waste (section 3, step 5): open its battery page, Change status, "Undo the waste", with a reason.
   **Expected:** the battery goes back to the status of the last version before the waste, a new version is published, and the history shows the undo.
9. DEMO-C7M-0001, Accident reports: "Withdraw…" beside the report the cluster admin added, with a reason, then confirm.
   **Expected:**
   - the report disappears from the accident page and its file can no longer be opened;
   - the audit log has who, when, why and the file's name;
   - `demo.north.admin`'s History shows one line: "The manufacturer's team withdrew an accident report".
10. Cluster: Change on a battery.
   **Expected:** it moves at once, and the move is in the audit log.
11. Battery IDs tab: release VPC-12221 to VPC-12230 with "Release new IDs": quantity 10, reference "PO 4512, line 2", model Compact 7M.
   **Expected:** the release's page opens with "Released 10 battery IDs. Export them to Excel for the labels.", and its ten IDs Available, each with its battery ID (`UN778474481-VPC-12221` …).
12. The same, with VPC-12228 to VPC-12240 and quantity 12, and VPC-12401 twice and "VPC 12402" (with a space) in the list.
   **Expected:** nothing is released; the Release new IDs tab lists the reasons ("that is 13 IDs, not 12", "already released", "in this release twice", "only letters A–Z or a–z, digits 0–9 and single dashes") and keeps what you typed. A serial number with a small letter is allowed: VPC8C05s-00001 is released and shown as typed, its battery ID reads `UN778474481-VPC8C05s-00001`, VPC8C05S-00001 is then refused as the same serial number, and its pages open with the ID typed in capitals, in small letters or mixed. With "Serial number format" VPC8C05s-##### on a model's page, a release for that model, a new battery of it, "Change its battery ID…" and a corrected serial number on a draft refuse a serial number that doesn't fit, naming the format.
13. The Releases tab, then Open on PO 4512, line 2, and Export to Excel.
   **Expected:**
   - Releases lists PO 4512, line 2 and the demo data's release, tagged Test; the counts at the top are the real IDs only;
   - an Excel file with that release's ten IDs, one row per ID: serial number, battery ID, QR code address, model, reference, released by and when, status.
14. Create battery: a Compact 7M batch, pick VPC-12221.
   **Expected:** the picker offers Compact 7M's IDs first, then any model's, then the test IDs (the demo's spares); the draft shows `UN778474481-VPC-12221` and its QR code; the ID is Used.
15. On that draft, More: Change its battery ID…, VPC-12222.
   **Expected:** the draft now has VPC-12222 and its ID; VPC-12221 is Available again.
16. All IDs: search VPC-12230; in its row's ⋯ menu, Void…, with a reason. Then tick "Show voided", and then "Show test IDs".
   **Expected:** it leaves the list until "Show voided" is ticked, then shows as Voided with the reason; the create picker no longer offers it; the test IDs show only once ticked.
17. Signed out, open `/25S/UN778474481-VPC-12223`, then `/25S/UN778474481-VPC-12230`.
   **Expected:** "Not published yet", then "Not available".
18. Clusters tab: Demo data, Clear demo data.
   **Expected:**
   - the demo records go, the "Demo" release with them, and real data stays (PO 4512's IDs included);
   - Reset demo data brings them back.
19. Create battery with a real released serial number, on a demo batch, model or operator.
   **Expected:** refused with a plain sentence: a real battery ID can't be used with demo product data. The same battery on real product data is made. "Change its battery ID…" on a demo draft offers test IDs only.
20. Users tab: "Add user" with an address that already has an account.
   **Expected:** "This address already has an account. Change it from its row in the list." Giving an account that has no role across the app one, without a new password, is refused; with a new password it is saved. Your own account and the last team account can't be lowered.
21. Families, a model, "Three questions about model …" (a new copy of Compact 7M is unanswered).
   **Expected:**
   - each question says "Not answered yet", with the line that its batteries can't be published until all three are answered; the model and its batch say "Questions to answer", never "Complete";
   - a battery of it says "Not ready to publish yet", with "Answer the model's three questions" first;
   - Yes to cycling use lists #82 and the other cycling values as needed, No says "Not needed: this model isn't made for cycling use.";
   - on a model with a published battery, a Yes the battery lacks the values for asks first, with "Save the answers anyway";
   - a warranty of 8 years is kept as P8Y and shows "8 years" (18 months shows as 18 months); 0 and "P99999999999Y" are refused with "The warranty must be a whole number of years (1 to 100) or months (1 to 1200)."; the rated capacity #57 is listed as needed.
22. New battery, with the battery system's export for its serial number (the help's export file, for a draft's serial).
   **Expected:** "Draft battery created with its export's readings. Saved: N values, and the module list was set (the numbers depend on the file). Type its temperature: the export has no temperature yet." The overview shows the state of charge and cycles and lists #87 under its live values. An export for another serial creates nothing. An export dated before 2015-01 isn't taken as the production date.
23. Clusters tab, open demo-north: "Written authorisation": take it away, then record it again with today's date.
   **Expected:** the row's tag changes between "Authorised …" and "Not authorised"; the audit log has both entries; a future date is refused; with it away, `demo.north.admin`'s page and token are read only (section 3, step 12); "Reset demo data" keeps it away if you took it away, and ticks the others.
24. A published battery whose model says Yes to a question it lacks a value for (step 21), then the battery list.
   **Expected:** "Saved changes are waiting on N published batteries: …", with links, also while a search is typed. Press "Check all passports now": "Checked N published batteries: …" and nothing is published. Add the missing value as the cluster admin or team: the passport gets its new version, and the waiting line is gone. "Publish the waiting changes" appears only when a change waits and nothing is in the way.
25. DEMO-CORE-0003 (waste, marked for deletion): "Remove the passport…".
   **Expected:** a window that says it can't be undone and asks for the battery's ID; a wrong ID gives "Not removed: type the battery's ID exactly as it is shown, to confirm."; the right one removes it ("The passport of battery … is removed."). It is gone from the list; its addresses answer 410 (section 1, step 10); creating it again is refused; the audit log has one entry. Reset demo data brings it back.
26. A published real battery (not demo): its "EU registry" section.
   **Expected:** "Not recorded yet"; the list says "N published batteries are not yet in the EU registry" and offers the CSV. Record a day and "EUREG-TEST-42": "Registered on …", and the line is gone. A day in the future is refused. The public page, the data and the detail page as the cluster admin don't show it. A demo battery has no such section.
27. Families, Compact (demo): "Copy as real product data", then Release new IDs with "Test IDs" ticked, then Create battery.
   **Expected:**
   - the copy is a real family with its models (answers copied), batches, documents and sites; the demo family is as it was; a second press while one copy runs says "A copy is already running";
   - a real battery ID on the demo batch is refused with a sentence naming what is demo and the way out; on the copy it is made, but its first publication is blocked with lines such as "Still the demo's example value: #25 …" until they are replaced; a test ID on the copy or on the demo batch publishes at once;
   - Battery IDs: the test release isn't counted and goes with the reset; a real battery can't go into a demo customer ("A real battery can't belong to a demo customer.").
28. A key change (only with the person who runs the site): restart the app with another `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64`.
   **Expected:** the battery page's versions say "Not verified / Signed with a key this site doesn't know." (the team's page also says to list the earlier key in `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS`), the public data says `"proof": null`, and with the earlier key listed everything is Verified again.

## 5. Market surveillance: `demo.msa@example.test`

1. Registry.
   **Expected:** every published battery, the invalid and marked ones included.
2. DEMO-C7M-0001's detail page.
   **Expected:** Purchaser, Shared, Authorities, Live values and Modules, then the public sections.
3. DEMO-C7M-0003.
   **Expected:** invalid, with the reason "Demo: wrong carbon footprint figures".
4. On an active passport's detail page, in the Purchaser card, press Mark as invalid…, give a reason, then confirm ("Continue", then "Yes, mark it").
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
| `demo.notified-body@example.test` | Shared, Authorities | Materials, dismantling, part numbers, spare parts, safety measures |
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
6. The demo data's "Demo team (full access)" token: `GET /api/external/v1/batches`, then `POST /api/external/v1/batteries` with `"batterySerialNumber": "DEMO-SPARE-0001"`.
   **Expected:** a new draft demo battery with that released ID. The same with a serial number that isn't released is refused.
   A demo token on a battery that isn't demo is answered as an unknown ID, and no change is accepted.
7. `GET /api/passports` with a cluster token.
   **Expected:** refused (team only).
8. `GET /api/external/v1/batteries/<a demo battery ID>` with any token, and `GET /api/passports/<ID>/verify`.
   **Expected:** `signatureVerified: true` and `signatureReason: null`. After a key change: false with "Signed with a key this site doesn't know.".
9. `POST /api/external/v1/uploads` for a published battery, then the same for a battery that holds a change back.
   **Expected:** each battery has `passport: { outcome, version, blocking }`: "new version", "nothing changed" or "held back". With demo-north's authorisation taken away, a cluster token's POST of a live value is 403 with the line.
10. Any call for a removed battery.
   **Expected:** 410 with "This battery's passport no longer exists. It was removed after the battery was recycled or exported."

## 8. The live site: no demo data

The team's own account, on a site with `ENABLE_DEMO_DATA=false`. Trial batteries are test IDs on the real product families. Don't press "Clear demo data" while real test batteries are there that you want to keep: it removes test IDs and test batteries too.

1. **The first sign-in on a new database.** With `FIRST_TEAM_ACCOUNT_EMAIL` set to your address, open the sign-in page, press "Forgot password", type the address.
   **Expected:** "If an account exists, a temporary password has been sent." The e-mail arrives; its temporary password signs in to the "set a new password" step; after you choose a password of at least 12 characters you are on the team's pages.
2. **The lock.** Type a wrong password for an account you may lock, 11 times.
   **Expected:** the right password is then refused with "Invalid email or password. Try again later."; "Forgot password" unlocks the account at once.
3. **Load the product data.** Battery families, "Load product data from Excel". Choose the filled workbook and its files, press **Check**.
   **Expected:** a list of what would be saved, and every problem by sheet, row and column. Nothing is saved yet.
4. **Fix and load.** Correct the workbook until the check says "No problems", then press **Load**.
   **Expected:** the family, its models, batches, site, operator, hazardous substances and documents are on their pages, with the workbook's values and no demo example.
5. **Again.** Press Check with the same workbook.
   **Expected:** "Nothing to load". Change one cell: the check shows that one change.
6. **A missing document.** Leave one named file out.
   **Expected:** it is listed under "Documents still to come" and doesn't stop the load.
7. **The model's serial number format.** It comes with the workbook's load; open a model's page and see it (or set it there, for example `VPC8C05s-#####`). In the workbook, change one model's format and check: the check shows the old and the new format and how many batteries don't fit. Then, on Battery IDs, release `VPC8C05s-00001`, then `VPC8C05S-00001`, then a serial of another model.
   **Expected:** the first is released with its small letter; the second is refused as the same serial number; the third is refused and the format is named.
8. **A test battery.** Release a test ID ("Test IDs"), make a battery of a real model with it, upload its readings, publish it. Open its address as a visitor, typed three ways.
   **Expected:** the same public page every time, with the "Test passport" tag; the ID keeps the small letter of the serial.
9. **The use notice.** Sign in as a reader who sees restricted data (market surveillance, notified body) and open a published battery's detail page twice.
   **Expected:** the notice is next to the restricted data; the team's audit log, filter "Restricted data opened", has one line for the day.
10. **Tokens.** Clusters tab, API tokens, create a token for one cluster.
    **Expected:** only "Read" and "Read + write".
11. **The look.** In the browser's developer tools, open the Network and Console tabs and move through the home page, a passport and a team page.
    **Expected:** every request goes to the site itself; no policy message.

## Report

Use the bug report template in `docs/qa-test-pack.md`, with the account, battery, page address, screen width and browser.
