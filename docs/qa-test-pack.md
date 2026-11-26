# QA Tester Pack

The starting point for anyone testing the battery passport app without knowing the project. It covers the new app (DPP V3): one passport per battery, built from the V3 attribute list, with a reader's role deciding what they see.

Keep these open while testing:

- `docs/end-user-testing-guide.md`: the step-by-step script per role.
- `docs/sample-cluster-test-accounts.md`: the demo accounts, clusters and batteries.
- `/help` in the app: each role's own help, including "Statuses" (the two badges).
- `/help/api` in the app: every API call, with a workbench to try one.

## Quick start

**Two kinds of site.** A *test site* (a developer's machine or a rehearsal site) has the demo switched on: the demo accounts, clusters and batteries below exist, and this quick start applies. The *live site* and the owner's own PC have the demo switched off (`ENABLE_DEMO_DATA=false`, decision 82): their database holds real product data only, there is no "Reset demo data" and there are no demo accounts. Test those with the team's own account and the part "The go-live version: live site, no demo data" below. Never press "Reset demo data" on a site that has real data.

1. Open the app link provided by the project owner in a browser.
2. **The known start:** a team member logs in (the team lands on "To do"), opens Admin, then the Clusters tab, and at its end presses **Reset demo data**.
   - This makes the demo clusters, accounts, batteries and API tokens again.
   - The tokens' values are shown once, in the window that follows. Copy them then: they are never shown again and never written here.
3. **Passwords:** the demo accounts share one password, which the project owner gives you. It is set on the server (`DEMO_USERS_PASSWORD`) and never written in this pack or in the app. The team's own account has its own password. A test site's fallback admin works only if the person who runs the site has set a password for it. Where the demo is switched off (`ENABLE_DEMO_DATA`), there is no Reset demo data and the demo accounts can't log in: ask the project owner for a site where it is on.
4. **One browser:** test in one browser window. Log out and in as each account in turn: some faults only show when the same browser changes accounts.
5. **Every element fully visible:** on every page and in every window, scroll each choice, field and button into view and check it is fully shown, not under a bar or cut. Reaching the last button isn't enough.

## Tester accounts

| Who | Account | Customer (cluster) | What to check |
|---|---|---|---|
| Team | the team's own account (from the project owner) | all | Lands on "To do" after logging in. Product data (the model's three questions, "Copy as real product data"), releasing battery IDs (test IDs too), creating and publishing batteries, every battery page, marking and removing passports, the EU registry record, "Check all passports now", each cluster's written authorisation, demo data, tokens |
| Customer admin | `demo.north.admin@example.test` | demo-north | Their batteries' pages: live values, accident reports, status changes and repairs, Waste (which asks a second question), hand-overs, uploads; their users and tokens (adding an address that already has an account only adds it to their customer). All of it works only while the Scania team has recorded the cluster's written authorisation (below). Lands on the Registry, which shows their customer ID and DEMO-CORE-0001 waiting to be accepted; Admin has their batteries, users and API tokens. |
| Customer user | `demo.north.user@example.test` | demo-north | Their batteries in the Registry and on the detail page, read only. On "Your account" the role reads "Customer user". |
| Other customer admin | `demo.south.admin@example.test`, `demo.fleet.admin@example.test` | demo-south, demo-fleet | They see demo-north's batteries as the public does; demo-north's admin's "Waiting for you" from demo-fleet |
| Other customer users | `demo.south.user@example.test`, `demo.fleet.user@example.test` | demo-south, demo-fleet | As above, read only |
| Market surveillance | `demo.msa@example.test` | — | Every published passport, every section, "Mark as invalid…" (a button in the Purchaser card, then a confirm step) |
| Notified body | `demo.notified-body@example.test` | — | Public, shared (#43, #45 to #48) and authorities' values |
| Commission | `demo.commission@example.test` | — | Public, shared and authorities' values; no purchaser values |
| Repair, repurposing, remanufacturing | `demo.repair@example.test` | — | Public and the shared values #43, #45–#48 |
| Recycling, battery treatment | `demo.recycling@example.test` | — | Public and #43, #45, #48 |
| Authorised treatment, WEEE | `demo.treatment@example.test` | — | Public and #48 |
| Anyone | not logged in | — | The public page only; the log-in page says "No account yet? Ask your contact at Scania." |

Every logged-in account has "Your account" in the account menu (the person's name at the right of the top bar), with "Your name", "Your e-mail", "Your role" and, to change the password or the e-mail, "Current password". A new password has at least 12 characters.

**The top bar** is one bar on every page: the app's name, the links, and the account menu under the person's name (e-mail, role and, for a customer's people, their customers; the links on a phone; "Your account"; light or dark mode; "Log out"). The team sees To do, Admin, Registry, Help; a customer's admin Registry, Admin, Help; every other reader Registry, Help; a visitor Help and "Log in". The Registry's link shows how many hand-overs wait for a customer's admin.

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

The demo models are answered: Compact 7M, Compact 13M and Core say yes to cycling use and to cadmium or lead; Compact 7M says yes to the commercial warranty, the other two say no (so DEMO-C13M-0002 and DEMO-CORE-0002 each miss only #17). The reset also records the written authorisation of the three demo clusters.

The demo batteries come from a release with the reference "Demo", on the Battery IDs tab, with four spare IDs (DEMO-SPARE-0001 to DEMO-SPARE-0004) for trying the create picker and "Change its battery ID…". With the three ID settings set (`DPP_ID_AGENCY`, `DPP_ID_COMPANY`, `DPP_ID_BASE_URL`), their IDs are marked TEST after the DUNS, such as `UN778474481-TEST-DEMO-C7M-0001`. Without them they are test IDs starting with `urn:x-battery-pass:TEST-`. Either way the pages show a "Test passport" tag.

## Scenario matrix

What each reader should get. ✓ = allowed or shown, – = not shown or refused. "Own" means the battery is in the reader's customer (cluster).

| Reader | Public page | Detail page | Purchaser tab | Shared tab | Authorities tab | Live values, Modules tabs | Registry lists | Battery page (manage) | Mark as invalid | Battery IDs tab |
|---|---|---|---|---|---|---|---|---|---|---|
| Anyone, not logged in | ✓ | – (log in) | – | – | – | – | – | – | – | – (log in) |
| Customer user, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own customer's | – | – | – (Not allowed) |
| Customer admin, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own customer's | ✓ | – | – (Not allowed) |
| Customer user or admin, other customer's battery | ✓ | public only | – | – | – | – | own customer's | – (refused) | – | – (Not allowed) |
| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every battery (Batteries tab) | ✓ | ✓ (and undo) | ✓ |
| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every published | – | ✓ | – (Not allowed) |
| Notified body | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – | – (Not allowed) |
| Commission | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – | – (Not allowed) |
| Repair | ✓ | ✓ | – | #43, #45–#48 | – | – | every published | – | – | – (Not allowed) |
| Recycling | ✓ | ✓ | – | #43, #45, #48 | – | – | every published | – | – | – (Not allowed) |
| Treatment | ✓ | ✓ | – | #48 | – | – | every published | – | – | – (Not allowed) |

Scanning a released ID whose passport isn't published yet shows "Not published yet" to everyone, with no battery data; a voided ID shows "Not available".

**Battery page actions** (team and the battery's customer admins, on an active passport):

| Action | Team | Own customer admin |
|---|---|---|
| Live values, Accident reports ("Add a report"), Upload a file | ✓ | ✓ |
| Change status: Repaired, placed on the market again, Remanufactured, Repurposed | ✓ | ✓ |
| Change status: Waste (a customer admin is asked a second question, "Yes, it is waste") | ✓ | ✓ |
| Change status: Undo the waste, with a reason (the battery goes back to the status of the version before) | ✓ | – |
| Change status: Correct the values | ✓ | – |
| Add a note | ✓ | ✓ |
| Accident reports: Withdraw…, with a reason and a confirm step | ✓ | – |
| Hand over… / Accept or Decline on "Waiting for you" | moves directly, or cancels a waiting one | ✓ |
| Mark as invalid or for deletion… (reason, then a confirm step), and Undo | ✓ | – |
| Edit product data, create and publish batteries | ✓ | – |
| Release, export and void battery IDs; Change its battery ID… on a draft | ✓ | – |

**Demo accounts on a battery that isn't demo:** a battery is inside the demo only when it, its batch, model and operator are demo and its cluster is a demo cluster or none. On any other battery, every demo account (customer admin, customer user, market surveillance and the rest) gets the visitor's page and no actions. This includes a demo-numbered battery the team made on real product data (allowed, to try things out). A demo token gets, for anything outside the demo, the same answer as for an ID that doesn't exist.

**Reset demo data** stops and changes nothing when a real account, battery or API token uses one of the demo's clusters; the red message names the cluster. Demo accounts that a demo admin added or made never stop it. The reset signs out every open demo session: log in again afterwards.

**Account actions** (Users pages; "Own customer admin" is a customer admin acting on people in their own clusters):

| Action | Team | Own customer admin |
|---|---|---|
| Add a new address | ✓ | ✓ (into their cluster) |
| Add an address that already has an account | – (refused: "change it from its row") | adds the membership only; the name and password stay as they are |
| Change a name or password | ✓ | only people who are with their customers alone, have no role across the app and were made by a customer admin of theirs; anyone else: "You can't change this person's account here. Ask the Scania team." |
| Give an account access across the app | ✓, with a new password in the same step | – |
| Lower or delete your own account, or the last team account | – | – |
| Use the fallback admin's address (outside Production only; in Production there is no fallback admin) | ✓ | – |
| Remove from a cluster (the x, which asks first) | ✓ | their customers |
| A membership's role | customer user or customer admin, nothing else | the same |

A new password has at least 12 characters. Deleting an account, lowering it, removing it from a cluster or changing its password ends its open sessions on their next page. E-mail addresses are the same whatever their capitals.

**Written authorisation** (a tick per cluster, on the Clusters tab, opened row, "Written authorisation"; the row's tag says "Authorised 3 Nov 2026" or "Not authorised"):

| Cluster | A customer admin or token |
|---|---|
| Authorised | changes passport data as in the table above |
| Not authorised (new, or from before the update) | reads everything; the battery page says so and offers no windows (Change status and Hand over are disabled); a post sent anyway changes nothing and gives the same line; a read-and-write token gets 403 with the line; the upload section isn't offered |
| Either | declining or cancelling a hand-over works; accepting needs both clusters authorised |

The team and market surveillance never need it. The reset ticks the demo clusters; "Take the authorisation away" is kept through a reset.

**The model's three questions** (the model's page: "Three questions about model …"): commercial warranty, cycling use, cadmium or lead above the limit; each is Yes or No, and "Not answered yet" until answered. A new model, and a copy of one, starts unanswered, and so does every model from before the update. While any is unanswered:
- the model and its batteries say "Questions to answer" (never "Complete");
- its drafts can't be published, and its published batteries get no status change (repair, Waste and the rest) and no new version; the Status section says "No status change can be made now: …" first, naming what is missing and who adds it (a customer admin: "until you add #83 … under Live values"; values only the team can type: "Ask the Scania team"). "Change status" isn't offered. Notes, live values, accident reports, marks and hand-overs still work;
- a "yes" makes values needed (warranty #19; cycling #71, #72, #74, #82 to #85; cadmium or lead #24); a "no" says "Not needed: this model isn't made for …". Saving a "yes" that a published battery lacks the values for asks first ("Save the answers anyway").
The rated capacity (#57) is needed before publishing. The warranty is a whole number of years (1 to 100) or months (1 to 1200), typed as a number and a unit, or as the official form (P3Y6M shows as 42 months); 0, a unit other than years or months, and other alphabets' digits are refused with "The warranty must be a whole number of years (1 to 100) or months (1 to 1200).". A battery published without a warranty gets one, once, with Change status, "Correct the values" (the team only); after that it is fixed.

**Saved changes that wait** (a published battery whose saved data differs from its public passport because something is missing): the battery's overview says "Saved changes are waiting: the public passport still shows version N" and lists what is missing; the team's battery list says "Saved changes are waiting on N published batteries: …" (a search never hides it). Adding what is missing publishes the change by itself ("Version N of the passport is published, with the saved changes that were waiting"); "Publish the waiting changes" (team) does it when nothing is in the way; every saving message says which passports got a new version and which kept theirs, and why. "Check all passports now" (battery list, team only) and an hourly check, which first runs a minute after the app starts, compare every published battery with its passport, put the marks right, archive extra active versions, and never publish; the line reads "Checked N published batteries: X now marked as waiting, Y no longer, Z old versions archived." or "…: nothing to put right.". A press during a run says "A check is already running…".

**The signature.** Each passport version is signed when published. The pages and the APIs say "Verified" only when the data matches its signature and the key is one the site knows. "Not verified" gives the reason: "The data doesn't match its signature.", "Signed with a key this site doesn't know.", "Not signed." or "The signature can't be read." (a damaged or missing proof never gives an error page). On the team's battery page only, under the unknown-key reason, a line says to list the earlier public key in `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS`. The issuer is `did:web:` plus the website of the battery IDs, and `/.well-known/did.json` (no log-in) lists the site's public keys.

**The public part as data** (no log-in, no token): `/dpp/{ID}.json`, `/dpp/{ID}/v2.json`, or the address with `Accept: application/json` (a browser's Accept, `application/json, */*` and `application/ld+json` get the page). It holds exactly what the public page shows (no #13, no battery status, no restricted values) and a `proof`, which is `null` when no signature holds for the data as served (for an older passport, after a key change until the earlier key is trusted, or after the site's address changed). The proof can be checked from outside as the Help says. Answers are never cached. A passport that isn't published answers 404 with `{"error": …}`. The four data routes have a read limit by caller's address (429 over it) that doesn't touch the page.

**A removed passport.** The team removes the passport of a recycled battery (marked for deletion first): "Remove the passport…", the window lists the earlier passports that go too, and asks for the battery's ID (either form, any capitals; a serial number or pack ID is refused: "Not removed: type the battery's ID exactly as it is shown, to confirm."). Afterwards every address (public page, detailed page, versions, the four data routes, `/api/passports/{id}` and `/verify`, the accident page, the external API) answers **410** with "This battery's passport no longer exists. It was removed after the battery was recycled or exported."; the battery is gone from the list, its ID can't be created or released again, and the audit log has one entry. A removal that stopped half way shows "Finish removing" on the battery's page, and the check finishes it. "Reset demo data" brings a removed demo battery back.

**The EU registry** (team only, a published real battery, outside the demo): an "EU registry" section says "Not recorded yet" until a day (not in the future) and a registration ID are recorded; the battery list says "N published batteries are not yet in the EU registry" with a link that narrows the list and "Download them as CSV". It is not passport data: not on the public page, the data, or any other role's pages.

**Product data from the demo** (team, on a test site with the demo on; the live site has no demo family, decision 82): on a demo family "Copy as real product data" (one copy at a time; after the confirm the button reads "Working…"; a copy that fails is undone) makes a real family with its models (with their three answers), batches, documents and sites; a demo site or operator has "Copy as real". The demo stays as it is. A real battery ID on the copy can be created but its first publication is blocked while the passport holds the demo's example values or files (the family, batch, site and operator pages list them; a test ID isn't blocked). "Release new IDs" has a "Test IDs" tick: IDs with TEST in them, for any model (the model is ignored), not counted, usable on demo or real product data, gone when the demo data is reset or cleared. A real battery can't belong to a demo customer; the pickers say "(demo customer: test IDs only)".

**Whole addresses.** The passport ID and the battery ID are shown as their whole address, website included, on the public and detailed pages, the team's and customer admin's battery pages and the versions list (lists keep the short form). Selecting the text and copying it gives the address whole, with no line break. Check at 1280 and 375 px: no sideways scroll, nothing cut.

**API** (`/api/external/v1`, a token in the `Authorization` header):

| Token | Reads | Writes |
|---|---|---|
| A cluster's "Read" token | that cluster's batteries, as its customer user would | nothing |
| A cluster's "Read and write live values" token | the same | the live values (#63, #83, #87, E1–E4) and uploads of live values, for that cluster's batteries |
| The team's "Full access" token | every battery | creating batteries (from a released, available serial number), publishing, live values, uploads |
| The demo data's "Demo team (full access)" token | every demo battery; a battery that isn't demo is answered as an unknown ID | creating demo batteries from the demo spares, publishing them; nothing on a battery that isn't demo |
| Any token | — | never a status change or repair (made on the battery's page only), and never releasing battery IDs |
| A cluster token of a cluster that isn't authorised | reads | nothing: 403 "The Scania team hasn't recorded your authorisation to update passport data yet." |
| Any token, a removed battery | — | 410 "This battery's passport no longer exists. It was removed after the battery was recycled or exported." (reads and writes) |

The battery, versions and passports answers carry `signatureVerified` and `signatureReason` (`null` when verified). An upload answer has, for each published battery, a `passport` object: `outcome` "new version", "nothing changed", "held back" (with what blocks) or "not published", and `version`.

`/api/passports` (the list of all passports) is for the team only.

## The go-live version: live site, no demo data

For the live site and the owner's PC (no demo). Log in with the team's own account; the tester notes which account and which test IDs were used. Trial batteries are **test IDs on the real product families** (decision 82), never demo batteries.

| # | Scenario | Expected |
|---|---|---|
| G1 | A new, empty database with `FIRST_TEAM_ACCOUNT_EMAIL` set: start the app, press "Forgot password" on the log-in page with that address | The text says a temporary password was sent if an account exists; it arrives by e-mail, logs in to the "set a new password" step; after choosing 12 or more characters the person opens the team pages. No password or reset link is in the app's log. A wrong guess before that is refused. |
| G2 | Start again with the setting still set | One team account; the log says the setting is ignored because a team account exists. |
| G3 | Ten wrong passwords for one account within 15 minutes from one network, then the right one from the same network, then from another network (a phone on mobile data) | The same network is refused with "Invalid email or password. Try again later." (the same words as for a wrong password); the other network logs in with the right password; another account still logs in from the first network; "Forgot password" (works while locked), a password set by the team, or the Account page's new password clears the lock at once; after 15 minutes it ends by itself. |
| G3a | While the lock of G3 is on, press "Forgot password" for the locked account and type the temporary password on the log-in page | It logs in to the "set a new password" step although the lock is on; after choosing a password the account logs in normally from every network. |
| G3b | On the live site, with a test team account: ten wrong passwords from one network (the office), the right one from the same network, then the right one from another network (a phone's own connection). Afterwards "Forgot password" for the test account | The office is refused with "Invalid email or password. Try again later."; the phone logs in. If the phone is refused too, the proxy isn't passing the client's address (`TRUSTED_PROXY_NETWORKS`, `TRUSTED_PROXY_HOP_LIMIT`): put it right before the site is used. "Forgot password" clears the count. |
| G4 | Battery families, "Load product data from Excel", **Download the empty workbook**; fill it (or use the owner's Core workbook), choose it with its files, **Check** | The check lists the family, models, batches, sites and operators with their values and units; any problem is named by sheet, row and column; nothing is saved (the product data pages show the same counts). |
| G5 | A workbook with a number typed as "4,7", an "about 220", a "yes" in small letters in a yes-or-no cell, and a model named on the documents sheet that the Models sheet lacks | Four problem lines, each naming sheet, column and model, in words. Load is switched off. |
| G6 | Fix them, **Load**; open the family, a model, a batch, the site and the operator | Everything of the workbook is there; the family shows its state; no value is the demo's example. One `dpp.product-data-loaded` line is in the audit log. |
| G6a | After the load of G6, open each model's page; then check a workbook with a different format for one model | The model page shows the format from the workbook (for example `VPC8C05s-#####`) and a later model did not take the first model's format. The check of the changed workbook shows the old and the new format and how many batteries of the model don't fit; `none` in the cell removes the format. |
| G6b | A workbook with two columns of one heading (a model copied as another name, keeping the old heading), then one with a typed value in a column without a heading | One problem line each: the first names both columns' letters, and on Batches adds that a workbook has one batch per model; the second says "column L has values but no heading in row 1". Nothing is read from those columns. |
| G6c | Check a workbook where a later model's recycled-content share is empty; then write `none` in it | The first check marks the value "taken from Core800"; with `none` the later model has none. An empty later batch column says every value, the year and the site included, is taken from the first model's batch. |
| G6d | With a published battery on the family, check a workbook that changes its model's mass and replaces a document | The block "Published passports change" says how many published batteries are reached and which parts reach them: each gets **one** new version, made after the whole load is saved, with all of the changes. Load gives the battery that one version (version 2, not one per change): its public passport holds the new mass and the document, and its "Saved changes are waiting" line, if it had one, is gone. |
| G6e | A load that stopped part way (a power cut or a closed window; testers can't make one on purpose, so only if one happened): look at the battery before and after | After a stopped load the battery stays on its version, with "Saved changes are waiting". Loading the workbook again publishes it (every load also publishes the waiting changes of the published batteries its workbook reaches, and the check names them: "N more published batteries have changes waiting …"); so does "Publish the waiting changes" on its page. Either way it gets one new version. The hourly check only marks waiting passports; it never publishes. |
| G7 | Load the same workbook again; then change one cell and check again | "Nothing to load" with Load switched off; then exactly that one change is shown ("Battery mass 470 → 455") and loading says "1 record changed". |
| G8 | A workbook naming a document file you don't send | The check lists it under "Documents still to come"; the load saves everything else; a later load with the file adds it. |
| G9 | Release `VPC8C05s-00001` for the model with serial number format `VPC8C05s-#####`; then `VPC8C05S-00001`, `VPC7C05-00001` and `VPC8C05-00001` | The first is released and shown with its small letter; the second is refused as the same serial number; the third and fourth are refused naming the format. Nothing is released by a refused call. |
| G10 | Create a battery from a released small-letter serial and publish it; open its address typed in capitals, in small letters and as typed | The same passport every time. The page and the QR code show `…/25S/UN778474481-VPC8C05s-00001`; the data's signature checks ("Verified"). |
| G11 | Release a serial that, ignoring dashes and capitals, is the item of another battery's ID (for example `VBC-1-R1` next to `VBC-1R1`) | Refused, naming the other battery. An ID released earlier that another battery's key already has stays Available but can't be used: the refusal says to void it with a reason on the Battery IDs page. A live battery is still served next to a removed one whose ID differs only in dashes. |
| G12 | Release test IDs ("Test IDs", marked TEST), make a battery of a real model with one, upload its readings, publish; open it as a visitor | It publishes; the page carries the "Test passport" tag and no word "demo"; it isn't counted among the real IDs. "Clear demo data" would remove it: don't press it unless you want that. |
| G13 | As market surveillance or another reader of restricted data (on a test site: `demo.msa@example.test`): open a battery's detail page, then open it again | The use notice shows next to the restricted data. The team's audit log has one "Restricted data opened" line for that reader, battery and day; the second opening adds none. The team and the battery's own customer admin see no notice and leave no line. Open the Registry list as the same reader: the notice shows above the list, and the audit log gets one "registry list" line for that reader and day. Open the accident reports first and the detail page after: each page has its own line. |
| G14 | A customer admin or a team member creates a cluster token | Only "Read" and "Read + write" are offered for a cluster. An older token with more rights is listed with what it can still do, and the team can replace it. A "Read + write" call that changes a published battery gives it a new version by itself. |
| G15 | Open the home page, a public passport, the log-in page and a team page; look at the network and console in the browser | Pages are styled and work; every request goes to the site's own address (Bootstrap comes from `/lib/bootstrap/`); the console shows no policy message; every response carries a content security policy, `X-Content-Type-Options: nosniff`, a referrer policy and `X-Frame-Options`. |
| G16 | Log in, restart the app, reload a page | Still logged in; a form opened before the restart still saves. |
| G16a | On a test site and on the PC (Development) with one database: log in on the PC, copy nothing, then open the test site in the same browser | Logged out on the test site: the two environments' log-ins don't work on each other. In `dataProtectionKeys` every key document has a `cipher` field and no readable key. |
| G16b | On a team page, enter a measured time "now" in the live-values card in a browser whose time zone differs from the server's, then send `measuredAt` without an offset and with one (`+01:00`) to the API | The form says "Read in your time zone (UTC+…)" and saves "now" (not refused as in the future); without an offset the API reads the time as UTC, with an offset as written. |
| G17 | Not logged in, home page: search for the first three or more letters or digits of a published battery's serial number (on a test site `demo-c`; on the live site the start of a real serial) | "No passport is exactly …. Did you mean:" lists at most three serial numbers that start with what you typed, each with its model; each link opens that battery's public page. Capitals, dashes and spaces don't matter (`democ7m` finds DEMO-C7M-0001). Fewer than three letters or digits, or a piece from the middle of a serial (`c7m`), offers nothing: "No published battery passport was found for …". A visitor never gets a "Type more" line, and part of a battery ID or passport ID offers nothing. |
| G17a | Log in as a customer's user, a reader such as market surveillance, or the team, and search the home page for part of a battery ID, a passport ID or a serial number (a piece from the middle, such as `c7m`) | The "Did you mean" list also offers the batteries the Registry lists for that reader and finds by part of any battery ID, passport ID or serial number, at most three; "Type more of the number to see the rest." shows when more match. A battery the reader's Registry doesn't list is found only by the start of its serial number, as for a visitor. The links open the detail page. |
| G17b | Log in as each kind of reader (team, customer admin, customer user, a reader such as market surveillance); also open the log-in address while logged in | The team lands on "To do"; every other reader lands on the Registry; a logged-in reader who opens the log-in address goes to the same place. A customer's admin sees their customer ID at the top of the Registry and of Admin. |
| G17c | Open an address that doesn't exist (`/no-such-page`), then `/api/no-such-call` | The first shows the "Page not found" page with the home search (status 404); the second is a plain 404 with no page; an error inside an API call answers `{"error": …}` with status 500, and a page error shows "Something went wrong" without details. |
| G17d | Search the home page many times in a minute from one address (only where the rate limit is on and you may; not on the live site) | Past the limit the page says "Too many searches from your address. Try again in a minute."; the API and the `.json` addresses answer `{"error": "Too many requests."}` with 429. A search that finds nothing uses two of the allowance. |

## Acceptance checklist

- [ ] On a test site: "Reset demo data" finishes and shows the tokens once. On the live site and the owner's PC the card says the demo is switched off and has no Reset button.
- [ ] Go-live version, live site: scenarios G1 to G17d hold, with test IDs on the real families.
- [ ] Every row of the scenario matrix holds, for every reader.
- [ ] The two badges show on every passport page with the right status. The Battery badge never shows to the public.
- [ ] Battery IDs: a range releases and opens the release's page with every ID Available; its "Export to Excel" opens with that release's IDs, one row per ID and its QR address.
- [ ] The Battery IDs page opens on Releases, with the real IDs' counts at the top; All IDs shows test IDs only with "Show test IDs" ticked, and voided IDs only with "Show voided" ticked (or the status Voided chosen); Releases shows a release whose IDs are all voided only with it ticked, and its own page lists every ID; Void… is in a row's ⋯ menu.
- [ ] A release with a wrong count, a repeated or already released serial number, or one with a space or a double dash lists every reason and releases nothing.
- [ ] A serial number with a small letter (VPC8C05s-00001) is released and shown as typed, in its battery ID and passport ID too; the same serial number in other letters (VPC8C05S-00001) is refused as the same serial number; its pages, data address, `/verify` and the API answer with the ID typed in capitals, in small letters or mixed, and always give the ID in its one form.
- [ ] A model with a "Serial number format" (VPC8C05s-#####) refuses a serial number that doesn't fit, naming the format: at release for that model, when creating a battery of it, at "Change its battery ID…" and when a draft's serial number is corrected. A model without one takes any serial number; test IDs aren't checked.
- [ ] Create battery picks a released serial number (no typing); the draft shows its battery ID and QR code at once; the ID turns Used.
- [ ] "Change its battery ID…" on a draft takes another available ID and gives the old one back; a voided ID is never offered again.
- [ ] Scanning a released, unpublished ID shows "Not published yet"; a voided one "Not available".
- [ ] With one of the three ID settings missing, the Battery IDs page shows a red notice naming it; nothing can be released, creating a battery from a released real ID is refused, and the demo data still works.
- [ ] DEMO-C7M-0002 lists what blocks publication; filling it in and pressing Publish gives a signed version 1 with a QR code, and its ID turns Published.
- [ ] A repair on DEMO-C7M-0001 sets re-used, adds to the repair history and makes a new version.
- [ ] Hand-over: demo-fleet's DEMO-CORE-0001 waits on demo-north's Registry; Accept moves it, and demo-fleet then sees it as the public does.
- [ ] Market surveillance marks a passport invalid from the Purchaser card's button, after a confirm step; the public page says so; only the team undoes it.
- [ ] A customer admin's Waste asks a second question; only the team undoes it ("Undo the waste", with a reason), and the battery goes back to the status of the version before.
- [ ] The team withdraws an accident report with a reason: its file is gone, the audit log has who, when, why and the file's name, and the cluster's history shows one line.
- [ ] "Add user" with an address that already has an account adds the membership only, and its name and password stay as they were. The account page asks for the current password to change the password or the e-mail.
- [ ] A real battery ID can't be created on demo product data, and the refusal says which of the batch, model and operator is demo and the way out; a demo account sees the visitor's page on a battery that isn't demo.
- [ ] A new model says "Not answered yet" for each question, its batteries can't be published, and nothing says "Complete" for it; answering Yes to cycling use lists #82 and the other cycling values as needed, No removes them from the list.
- [ ] A published battery of an unanswered model: the Status section says "No status change can be made now" with who adds what; live values, notes and marks still work.
- [ ] A missing value holds a saved change back: "Saved changes are waiting" on the battery and the list; adding the value publishes it by itself and the message says so; "Check all passports now" gives its line and publishes nothing.
- [ ] #57 is listed as needed; the warranty takes 8 years or 18 months and refuses 0, "P99999999999Y" and a unit of X without an error page; "Correct the values" adds a missing warranty once.
- [ ] A new battery created with the battery system's export for its serial number has its state of charge and cycles, says the temperature is missing, and (for an export from another serial) creates nothing; a production date of 2000-01 in the export isn't taken.
- [ ] Every "Verified" is Verified under the site's key; with another key it says "Not verified: signed with a key this site doesn't know", with the earlier key listed it is Verified again; `/.well-known/did.json` lists the keys; a passport with its proof damaged says "Not verified" and the page still opens.
- [ ] `/dpp/{ID}.json` and the Accept form give the public part and a proof that checks from outside, with no #13 and no battery status; the page and the data agree.
- [ ] The notified body's detail page has a "Shared" part (materials, dismantling manuals, part numbers, spare parts, safety measures) and "Authorities", and no purchaser part; the public data is the same as before.
- [ ] A cluster that isn't authorised: its admin reads and can change nothing, with the line; its token gets 403; after the team's tick everything works; a hand-over is accepted only when both clusters are authorised.
- [ ] The team records a battery's EU registration; the list's count and CSV follow; nothing of it shows to anyone else.
- [ ] The team removes the passport of a recycled battery after typing its ID: every address answers 410 with the sentence, its earlier passports go too, the ID can't be used again, and "Check all passports now" has nothing to put right.
- [ ] "Copy as real product data" gives a real family; a real ID on it is accepted and its first publication is blocked until the examples are replaced; a test ID publishes at once, isn't counted and goes with the reset.
- [ ] The passport and battery IDs show as whole addresses, and selecting one copies it whole.
- [ ] A file that can't be saved says why and what to do in the card's first message (for example a repair file on a draft: "Publish the battery first, or take the repair out of the file to save its readings.").
- [ ] Uploading the help's sample files on DEMO-C7M-0001 shows the check before saving, then saves.
- [ ] The API answers each token as in the API table, and refuses what it should with a plain message.
- [ ] The public and detail pages print on A4 with every tab open (browser print).
- [ ] Every page works at a phone's width (375 px) and at 150 % Windows scaling, with every element fully visible.

## Known limitations

- **Fallback admin:** it has no password in the app. Outside Production it works only when the person who runs the site has set `DEMO_ADMIN_PASSWORD`; in Production the app doesn't start with it switched on. Test with the team's own account.
- **Real IDs need the settings:** without `DPP_ID_AGENCY`, `DPP_ID_COMPANY` and `DPP_ID_BASE_URL`, the Battery IDs page shows a red notice naming the missing ones, and nothing is released or created from a released ID. The demo data still works, with test IDs (`urn:x-battery-pass:TEST-…`), as on a developer's PC.
- **Pack temperature:** the battery system's export has no pack temperature yet; each export's notes say so, and it is typed on the battery. The export team has also been asked for the modules' serial numbers and the cycle definition.
- **After an update:** every model is "not answered" until the team answers its three questions, and no cluster is authorised until the team ticks it. Neither is a fault. The first passport check runs a minute after the app starts.
- **Sign-in keys:** the first start of this version makes new sign-in keys, so everyone is logged out once; changing the signing key does it again. The keys are kept in the database encrypted with a secret made from the signing key, and each environment (the live site, a developer's PC) has its own, even on one database: a cookie from the PC is refused on the live site.
- **A model's serial number format comes with the Excel load** (the check shows old and new format and how many batteries don't fit; `none` removes it; a later model never takes the first model's). It can also be set on the model's page.
- **No way back:** a database written by this version (a model answered, a passport published, a copy made) can't be opened by the earlier version.
- **Removing a passport can't be undone.** Test it on a demo battery on a test site only; the reset brings that one back. On the live site never remove a real battery's passport to try it; use a test battery.
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
