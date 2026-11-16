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
3. **Passwords:** the demo accounts share one password, which the project owner gives you. It is set on the server (`DEMO_USERS_PASSWORD`) and never written in this pack or in the app. The team's own account has its own password. A test site's fallback admin works only if the person who runs the site has set a password for it. Where the demo is switched off (`ENABLE_DEMO_DATA`), there is no Reset demo data and the demo accounts can't sign in: ask the project owner for a site where it is on.
4. **One browser:** test in one browser window. Sign out and in as each account in turn: some faults only show when the same browser changes accounts.
5. **Every element fully visible:** on every page and in every window, scroll each choice, field and button into view and check it is fully shown, not under a bar or cut. Reaching the last button isn't enough.

## Tester accounts

| Who | Account | Cluster | What to check |
|---|---|---|---|
| Team | the team's own account (from the project owner) | all | Product data (the model's three questions, "Copy as real product data"), releasing battery IDs (test IDs too), creating and publishing batteries, every battery page, marking and removing passports, the EU registry record, "Check all passports now", each cluster's written authorisation, demo data, tokens |
| Cluster admin | `demo.north.admin@example.test` | demo-north | Their batteries' pages: live values, accident reports, status changes and repairs, Waste (which asks a second question), hand-overs, uploads; their users and tokens (adding an address that already has an account only adds it to their cluster). All of it works only while the Scania team has recorded the cluster's written authorisation (below). Their Registry also shows DEMO-CORE-0001 waiting to be accepted. |
| Cluster user | `demo.north.user@example.test` | demo-north | Their batteries in the Registry and on the detail page, read only. On the account page the role reads "Cluster member". |
| Other cluster admin | `demo.south.admin@example.test`, `demo.fleet.admin@example.test` | demo-south, demo-fleet | They see demo-north's batteries as the public does; demo-north's admin's "Waiting for you" from demo-fleet |
| Other cluster users | `demo.south.user@example.test`, `demo.fleet.user@example.test` | demo-south, demo-fleet | As above, read only |
| Market surveillance | `demo.msa@example.test` | — | Every published passport, every section, "Mark as invalid…" (a button in the Purchaser card, then a confirm step) |
| Notified body | `demo.notified-body@example.test` | — | Public, shared (#43, #45 to #48) and authorities' values |
| Commission | `demo.commission@example.test` | — | Public, shared and authorities' values; no purchaser values |
| Repair, repurposing, remanufacturing | `demo.repair@example.test` | — | Public and the shared values #43, #45–#48 |
| Recycling, battery treatment | `demo.recycling@example.test` | — | Public and #43, #45, #48 |
| Authorised treatment, WEEE | `demo.treatment@example.test` | — | Public and #48 |
| Anyone | not signed in | — | The public page only; the login page says "No account yet? Ask your contact at Scania." |

Every signed-in account has an Account page (top bar) with "Your name", "Your e-mail", "Your role" and, to change the password or the e-mail, "Current password". A new password has at least 12 characters.

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

What each reader should get. ✓ = allowed or shown, – = not shown or refused. "Own" means the battery is in the reader's cluster.

| Reader | Public page | Detail page | Purchaser tab | Shared tab | Authorities tab | Live values, Modules tabs | Registry lists | Battery page (manage) | Mark as invalid | Battery IDs tab |
|---|---|---|---|---|---|---|---|---|---|---|
| Anyone, not signed in | ✓ | – (sign-in) | – | – | – | – | – | – | – | – (sign-in) |
| Cluster user, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | – | – | – (Not allowed) |
| Cluster admin, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | ✓ | – | – (Not allowed) |
| Cluster user or admin, other cluster's battery | ✓ | public only | – | – | – | – | own cluster's | – (refused) | – | – (Not allowed) |
| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every battery (Batteries tab) | ✓ | ✓ (and undo) | ✓ |
| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every published | – | ✓ | – (Not allowed) |
| Notified body | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – | – (Not allowed) |
| Commission | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – | – (Not allowed) |
| Repair | ✓ | ✓ | – | #43, #45–#48 | – | – | every published | – | – | – (Not allowed) |
| Recycling | ✓ | ✓ | – | #43, #45, #48 | – | – | every published | – | – | – (Not allowed) |
| Treatment | ✓ | ✓ | – | #48 | – | – | every published | – | – | – (Not allowed) |

Scanning a released ID whose passport isn't published yet shows "Not published yet" to everyone, with no battery data; a voided ID shows "Not available".

**Battery page actions** (team and the battery's cluster admins, on an active passport):

| Action | Team | Own cluster admin |
|---|---|---|
| Live values, Accident reports ("Add a report"), Upload a file | ✓ | ✓ |
| Change status: Repaired, placed on the market again, Remanufactured, Repurposed | ✓ | ✓ |
| Change status: Waste (a cluster admin is asked a second question, "Yes, it is waste") | ✓ | ✓ |
| Change status: Undo the waste, with a reason (the battery goes back to the status of the version before) | ✓ | – |
| Change status: Correct the values | ✓ | – |
| Add a note | ✓ | ✓ |
| Accident reports: Withdraw…, with a reason and a confirm step | ✓ | – |
| Hand over… / Accept or Decline on "Waiting for you" | moves directly, or cancels a waiting one | ✓ |
| Mark as invalid or for deletion… (reason, then a confirm step), and Undo | ✓ | – |
| Edit product data, create and publish batteries | ✓ | – |
| Release, export and void battery IDs; Change its battery ID… on a draft | ✓ | – |

**Demo accounts on a battery that isn't demo:** a battery is inside the demo only when it, its batch, model and operator are demo and its cluster is a demo cluster or none. On any other battery, every demo account (cluster admin, cluster user, market surveillance and the rest) gets the visitor's page and no actions. This includes a demo-numbered battery the team made on real product data (allowed, to try things out). A demo token gets, for anything outside the demo, the same answer as for an ID that doesn't exist.

**Reset demo data** stops and changes nothing when a real account, battery or API token uses one of the demo's clusters; the red message names the cluster. Demo accounts that a demo admin added or made never stop it. The reset signs out every open demo session: sign in again afterwards.

**Account actions** (Users pages; "Own cluster admin" is a cluster admin acting on people in their own clusters):

| Action | Team | Own cluster admin |
|---|---|---|
| Add a new address | ✓ | ✓ (into their cluster) |
| Add an address that already has an account | – (refused: "change it from its row") | adds the membership only; the name and password stay as they are |
| Change a name or password | ✓ | only people who are in their clusters alone, have no role across the app and were made by a cluster admin of theirs; anyone else: "You can't change this person's account here. Ask the Scania team." |
| Give an account access across the app | ✓, with a new password in the same step | – |
| Lower or delete your own account, or the last team account | – | – |
| Use the fallback admin's address (outside Production only; in Production there is no fallback admin) | ✓ | – |
| Remove from a cluster (the x, which asks first) | ✓ | their clusters |
| A membership's role | cluster user or cluster admin, nothing else | the same |

A new password has at least 12 characters. Deleting an account, lowering it, removing it from a cluster or changing its password ends its open sessions on their next page. E-mail addresses are the same whatever their capitals.

**Written authorisation** (a tick per cluster, on the Clusters tab, opened row, "Written authorisation"; the row's tag says "Authorised 3 Nov 2026" or "Not authorised"):

| Cluster | A cluster admin or token |
|---|---|
| Authorised | changes passport data as in the table above |
| Not authorised (new, or from before the update) | reads everything; the battery page says so and offers no windows (Change status and Hand over are disabled); a post sent anyway changes nothing and gives the same line; a read-and-write token gets 403 with the line; the upload section isn't offered |
| Either | declining or cancelling a hand-over works; accepting needs both clusters authorised |

The team and market surveillance never need it. The reset ticks the demo clusters; "Take the authorisation away" is kept through a reset.

**The model's three questions** (the model's page: "Three questions about model …"): commercial warranty, cycling use, cadmium or lead above the limit; each is Yes or No, and "Not answered yet" until answered. A new model, and a copy of one, starts unanswered, and so does every model from before the update. While any is unanswered:
- the model and its batteries say "Questions to answer" (never "Complete");
- its drafts can't be published, and its published batteries get no status change (repair, Waste and the rest) and no new version; the Status section says "No status change can be made now: …" first, naming what is missing and who adds it (a cluster admin: "until you add #83 … under Live values"; values only the team can type: "Ask the Scania team"). "Change status" isn't offered. Notes, live values, accident reports, marks and hand-overs still work;
- a "yes" makes values needed (warranty #19; cycling #71, #72, #74, #82 to #85; cadmium or lead #24); a "no" says "Not needed: this model isn't made for …". Saving a "yes" that a published battery lacks the values for asks first ("Save the answers anyway").
The rated capacity (#57) is needed before publishing. The warranty is a whole number of years (1 to 100) or months (1 to 1200), typed as a number and a unit, or as the official form (P3Y6M shows as 42 months); 0, a unit other than years or months, and other alphabets' digits are refused with "The warranty must be a whole number of years (1 to 100) or months (1 to 1200).". A battery published without a warranty gets one, once, with Change status, "Correct the values" (the team only); after that it is fixed.

**Saved changes that wait** (a published battery whose saved data differs from its public passport because something is missing): the battery's overview says "Saved changes are waiting: the public passport still shows version N" and lists what is missing; the team's battery list says "Saved changes are waiting on N published batteries: …" (a search never hides it). Adding what is missing publishes the change by itself ("Version N of the passport is published, with the saved changes that were waiting"); "Publish the waiting changes" (team) does it when nothing is in the way; every saving message says which passports got a new version and which kept theirs, and why. "Check all passports now" (battery list, team only) and an hourly check, which first runs a minute after the app starts, compare every published battery with its passport, put the marks right, archive extra active versions, and never publish; the line reads "Checked N published batteries: X now marked as waiting, Y no longer, Z old versions archived." or "…: nothing to put right.". A press during a run says "A check is already running…".

**The signature.** Each passport version is signed when published. The pages and the APIs say "Verified" only when the data matches its signature and the key is one the site knows. "Not verified" gives the reason: "The data doesn't match its signature.", "Signed with a key this site doesn't know.", "Not signed." or "The signature can't be read." (a damaged or missing proof never gives an error page). On the team's battery page only, under the unknown-key reason, a line says to list the earlier public key in `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS`. The issuer is `did:web:` plus the website of the battery IDs, and `/.well-known/did.json` (no sign-in) lists the site's public keys.

**The public part as data** (no sign-in, no token): `/dpp/{ID}.json`, `/dpp/{ID}/v2.json`, or the address with `Accept: application/json` (a browser's Accept, `application/json, */*` and `application/ld+json` get the page). It holds exactly what the public page shows (no #13, no battery status, no restricted values) and a `proof`, which is `null` when no signature holds for the data as served (for an older passport, after a key change until the earlier key is trusted, or after the site's address changed). The proof can be checked from outside as the Help says. Answers are never cached. A passport that isn't published answers 404 with `{"error": …}`. The four data routes have a read limit by caller's address (429 over it) that doesn't touch the page.

**A removed passport.** The team removes the passport of a recycled battery (marked for deletion first): "Remove the passport…", the window lists the earlier passports that go too, and asks for the battery's ID (either form, any capitals; a serial number or pack ID is refused: "Not removed: type the battery's ID exactly as it is shown, to confirm."). Afterwards every address (public page, detailed page, versions, the four data routes, `/api/passports/{id}` and `/verify`, the accident page, the external API) answers **410** with "This battery's passport no longer exists. It was removed after the battery was recycled or exported."; the battery is gone from the list, its ID can't be created or released again, and the audit log has one entry. A removal that stopped half way shows "Finish removing" on the battery's page, and the check finishes it. "Reset demo data" brings a removed demo battery back.

**The EU registry** (team only, a published real battery, outside the demo): an "EU registry" section says "Not recorded yet" until a day (not in the future) and a registration ID are recorded; the battery list says "N published batteries are not yet in the EU registry" with a link that narrows the list and "Download them as CSV". It is not passport data: not on the public page, the data, or any other role's pages.

**Product data from the demo** (team): on a demo family "Copy as real product data" (one copy at a time; after the confirm the button reads "Working…"; a copy that fails is undone) makes a real family with its models (with their three answers), batches, documents and sites; a demo site or operator has "Copy as real". The demo stays as it is. A real battery ID on the copy can be created but its first publication is blocked while the passport holds the demo's example values or files (the family, batch, site and operator pages list them; a test ID isn't blocked). "Release new IDs" has a "Test IDs" tick: IDs with TEST in them, for any model (the model is ignored), not counted, usable on demo or real product data, gone when the demo data is reset or cleared. A real battery can't belong to a demo customer; the pickers say "(demo customer: test IDs only)".

**Whole addresses.** The passport ID and the battery ID are shown as their whole address, website included, on the public and detailed pages, the team's and cluster admin's battery pages and the versions list (lists keep the short form). Selecting the text and copying it gives the address whole, with no line break. Check at 1280 and 375 px: no sideways scroll, nothing cut.

**API** (`/api/external/v1`, a token in the `Authorization` header):

| Token | Reads | Writes |
|---|---|---|
| A cluster's "Read" token | that cluster's batteries, as its cluster user would | nothing |
| A cluster's "Read and write live values" token | the same | the live values (#63, #83, #87, E1–E4) and uploads of live values, for that cluster's batteries |
| The team's "Full access" token | every battery | creating batteries (from a released, available serial number), publishing, live values, uploads |
| The demo data's "Demo team (full access)" token | every demo battery; a battery that isn't demo is answered as an unknown ID | creating demo batteries from the demo spares, publishing them; nothing on a battery that isn't demo |
| Any token | — | never a status change or repair (made on the battery's page only), and never releasing battery IDs |
| A cluster token of a cluster that isn't authorised | reads | nothing: 403 "The Scania team hasn't recorded your authorisation to update passport data yet." |
| Any token, a removed battery | — | 410 "This battery's passport no longer exists. It was removed after the battery was recycled or exported." (reads and writes) |

The battery, versions and passports answers carry `signatureVerified` and `signatureReason` (`null` when verified). An upload answer has, for each published battery, a `passport` object: `outcome` "new version", "nothing changed", "held back" (with what blocks) or "not published", and `version`.

`/api/passports` (the list of all passports) is for the team only.

## Acceptance checklist

- [ ] "Reset demo data" finishes and shows the tokens once.
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
- [ ] A cluster admin's Waste asks a second question; only the team undoes it ("Undo the waste", with a reason), and the battery goes back to the status of the version before.
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
- **No way back:** a database written by this version (a model answered, a passport published, a copy made) can't be opened by the earlier version.
- **Removing a passport can't be undone.** Test it on a demo battery only; the reset brings that one back.
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
