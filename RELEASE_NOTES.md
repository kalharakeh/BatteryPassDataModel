# Changelog
All notable changes to the battery passport data model will be documented in this file.

## The go-live version: first account, no demo, Excel load, serial numbers, hardening (16 Nov 2026)

On top of `new-app-v3.4`: the platform, hardening, serial number and product data loading packages. Read "Before you update" first.

### Before you update

- **Back up the database.** Same as for `new-app-v3.4`: the earlier version can't open a database this version has written to [decision 62]. Note which back-up it is.
- **Nothing to do by hand for the battery ID key.** The start gives old batteries their key itself. If the log names two batteries with the same ID apart from capitals and dashes, the team decides which is right.
- **Everybody is signed out once** after the update: the sign-in keys now live in the database [decision 84].
- **What v3.4 asked still applies:** the three answers of each model and the tick of each customer's written authorisation (until then its admins and tokens only read).
- **New settings:** `FIRST_TEAM_ACCOUNT_EMAIL` (only for an empty database), and `ENABLE_DEMO_DATA=false` on the live site and on the owner's PC. `HEALTH_DIAGNOSTICS=true` now stops the start in Production. A proxy in front of the app must allow 100 MB for `/admin/product-data-load`. The cut-over runbook has them all.
- **A copy of the database now carries the live sign-in keys** (collection `dataProtectionKeys`, not encrypted). Keep back-ups as safe as the signing key.

### For the team

- **Load product data from Excel.** On Battery families, "Load product data from Excel" takes the filled workbook and its files, checks them (every problem by sheet, row and column, nothing saved), then loads all or nothing. A second load updates; a document you don't have yet is listed under "Documents still to come" [decision 83, 87].
- **Small letters in serial numbers.** `VPC8C05s-00001` and `VPC8C05S-00001` are the same serial number; it is kept as released, and the battery ID, the passport ID and the QR code carry the small letter. An address typed in other capitals opens the same passport [decision 80].
- **A serial number format per model** (for example `VPC8C05-#####`), set on the model's page. Releasing IDs, creating a battery and swapping an ID refuse a serial that doesn't fit and name the format [decision 81]. The workbook doesn't load the format yet.
- **One rule for battery IDs:** two batteries never have IDs that differ only in capitals or dashes, at release, creation, swap and new passports; the database enforces it. A draft's serial number is checked whenever its text changes [decision 86].
- **No demo families on the live site.** The demo is switched off, "Reset demo data" is gone there, and trial batteries are made with test IDs on the real families. "Clear demo data" stays and says what else goes (test IDs, test batteries) [decision 82].
- **The first team account** of an empty database is made from `FIRST_TEAM_ACCOUNT_EMAIL`; the person sets a password with "Forgot password" [decision 78, 84].
- **Sign-in lock:** ten wrong passwords in 15 minutes lock the address for 15 minutes; a new password unlocks it. Addresses with a line break or other odd characters are refused when set [decision 85].
- **Cluster tokens** are "Read" or "Read + write" only. Older tokens with more rights stay and are listed with what they can still do; "Replace" gives a new token and switches the old one off. "Read + write" saves republish a published battery, as the team's own save does. Deleting a cluster deletes its tokens, narrows tokens that had others and cancels waiting hand-overs [decision 85].
- **The audit log** has a filter "Restricted data opened" [decision 85].
- A "measured at" time more than five minutes ahead is refused everywhere.

### For cluster admins

- Cluster tokens: see above. A cluster must exist before people are added to it.

### For readers

- **A use notice** sits next to restricted data (shared with you for your role; don't pass it on or keep it longer than you need it). Your first opening of a battery's restricted data each day is logged by the team's audit log, without the values. The battery's own team and cluster admins see no notice [decision 85].
- A control character in an address is never a passport (it was an error page).

### Under the hood

- **Security headers** on every response (content security policy, nosniff, referrer policy, frame options); Bootstrap is served from the app, so no page loads a script or style from another site [decision 84].
- **Sign-in cookies and open forms survive a restart** (keys in the database).
- Health diagnostics can't be switched on in Production.
- New collections: `dataProtectionKeys`, `signInAttempts`, `restrictedReads`; a unique index `item_key` on the batteries.

## Wave 2: the values a passport must have, the signature, public data, authorisation and removal (11 to 13 Nov 2026)

Fixes from the review of the app on tag `new-app-v3.2`, on top of `new-app-v3.3.1`. The finding IDs are in brackets. Read "Before you update" first.

### Before you update

- **Back up the database.** The earlier version can't open a database this version has written to once a model is answered, a passport is published or a copy is made (it answers 500). Only the back-up goes back [decision 62].
- **Production needs its signing key and its two addresses.** The app doesn't start without `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64`, or when `APP_BASE_URL` or `DPP_ID_BASE_URL` isn't a whole `http://` or `https://` address, or when the two aren't the same site [R23, D18].
- **Every model is "not answered" after the update.** Until the team answers a model's three questions, its drafts can't be published and its published batteries get no status change and no new version [R01].
- **No cluster is authorised after the update.** The team ticks each customer's written authorisation; until then its admins and tokens read only [R20].
- `/.well-known/did.json` must reach the app. New settings: `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS` and `DPP_PASSPORT_CHECK`. The two `BATTERY_PASS_SIGNING_ISSUER` and `…_VERIFICATION_METHOD` settings are gone.

### For the team

- **Three questions per model** (commercial warranty, cycling use, cadmium or lead above the limit): a "yes" makes the matching values needed before publishing. This reverses the earlier rule that conditional values never block [R01].
- **The rated capacity (#57) is needed before publishing**, although the official list says "not by February 2027" [R22].
- **A status change needs a passport that can publish**, Waste included. The Status section says so before anything is typed, and names who can add what is missing [R01].
- **Saved changes that wait:** a published battery whose saved data differs from its public passport says "Saved changes are waiting", on its page and on the battery list. Adding what is missing publishes the change by itself; "Publish the waiting changes" does it when nothing is in the way. Every saving message says which passports got a new version and which kept theirs [R01].
- **"Check all passports now"**, and a check shortly after start and every hour: it compares every published battery with its passport, puts the waiting marks right and archives all but the newest active version. It never publishes [R01].
- **A new battery can take the battery system's export:** its state of charge and cycles become the first values, and the temperature is typed. The export's production date counts only when it can be true [R09].
- **The warranty** is typed as years (1 to 100) or months (1 to 1200), also in the official form (P3Y6M shows as 42 months); a missing one is added, once, with "Correct the values". Languages are shown by name [W05].
- **The signature's issuer is the passport's own web address** (`did:web:` plus the battery IDs' host). The site publishes its public keys at `/.well-known/did.json`. "Verified" also means the key is trusted; "Not verified" says why. Earlier public keys are listed in `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS` before a key is changed [R23].
- **Written authorisation:** a dated tick per cluster on the Clusters tab. Without it a cluster's admins and tokens read but change nothing, and a hand-over is accepted only when both clusters are authorised [R20].
- **The EU registry record:** a published real battery's page has an "EU registry" section for the day and the registration ID; the battery list says how many aren't recorded yet and gives them as a CSV [R02].
- **Removing a passport:** after a battery is recycled or exported, the team can remove its passport (with its earlier passports) by typing its ID. Everything is deleted except a marker for its IDs and one audit entry; a removal that stops half way is finished from the marker. The addresses answer 410 "This battery's passport no longer exists. It was removed after the battery was recycled or exported." The IDs are never used again [R08].
- Production refuses a passport address that isn't a whole http(s) address, and two addresses that are different sites [D18].

### For cluster admins

- A cluster changes passport data only once the team has recorded its written authorisation; until then the battery page says so and offers no forms. Declining or cancelling a hand-over still works; accepting needs both clusters' authorisation [R20].
- When a passport can't get a new version yet, the Status section says "No status change can be made now" and names what is missing, such as "until you add #83 … under Live values". Adding it publishes the waiting change [R01].
- The warranty is shown and typed as years or months [W05].

### For readers

- **The public part of a published passport is also given as data,** without a token: `/dpp/{ID}.json`, or the address with `Accept: application/json`, with its own signature over the public part. The Help says how to check it from outside [R07].
- **Notified bodies see the model's restricted data** (#43, #45 to #48), as the Regulation says [R05].
- A passport whose signature can't be read or is missing says "Not verified" and still opens. The APIs say why in `signatureReason` [R23].
- The API's upload answer says, for each published battery, what its passport got (`passport.outcome`: "new version", "nothing changed" or "held back"). A removed battery answers 410 on every route [R01, R08].

## new-app-v3.3.1: for the first real cycle (12 Nov 2026)

The owner's Monday package: real product data from the demo, test IDs, and the whole addresses.

### For the team

- **"Copy as real product data"** on a demo family makes a real copy of its models, batches and documents (and of the sites they use); "Copy as real" does the same for a demo site or operator. The demo data stays as it is. A copy runs to its end, one at a time, and undoes itself if it fails part way [decision 66].
- **Test IDs:** "Release new IDs" can release IDs that contain TEST. They are for any model, aren't counted, can be used on demo or real product data, and go when the demo data is reset or cleared [decision 67].
- **A real battery doesn't publish the demo's example values.** The copy keeps the demo's example texts and files until you replace them; the pages list them, and the first publication of a battery with a real ID is blocked while its passport holds one (a test ID isn't). A real battery can't belong to a demo customer [decision 70].
- **Two plainer messages:** a file that can't be saved says why and what to do, as the card's main message; a real ID on demo product data is refused in a sentence that names what is demo and the way out [decision 69].

### For cluster admins

- A real battery can't be handed to a demo customer, and the customer pickers mark demo customers [decision 70].

### For readers

- **The passport ID and the battery ID are shown as their whole address,** website included, on the public and detailed pages and the battery pages. Selecting the text and copying gives the address whole [decision 68].

## Wave 1: security fixes (10 Nov 2026)

Fixes from the review of the app on tag `new-app-v3.2`. The finding IDs are in brackets.

### For the team

- A cluster admin can no longer take over an account. "Add user" with an address that already has an account only adds it to their cluster; they change a name or password only for people who are in their clusters alone, have no role across the app and were made by a cluster admin of theirs [R19, S04, S08].
- The team's "Add user" refuses an address that has an account: change it from its row. Giving an account a role across the app needs a new password in the same step. Nobody can lower or delete their own account, and the last team account can't be lowered or deleted [R19, S04].
- A membership is a cluster user or a cluster admin, nothing else [S08].
- A new password has at least 12 characters. The account page asks for the current password before it changes the password or the e-mail [S06].
- An e-mail address is the same whatever its capitals [U28].
- Deleting an account, lowering it, removing it from a cluster or changing its password ends its open sessions on their next page [S02].
- The fallback admin has no password in the code any more. In Production the app doesn't start with it switched on; elsewhere it works only with a password set in the environment [R21].
- The team can withdraw an accident report with a reason. Its file is deleted; the audit log keeps who, when, why and the file's name [S07].
- The team can undo a waste, with a reason: the battery goes back to the status of the version before [R10].
- Demo data can be switched off with `ENABLE_DEMO_DATA` (off in Production unless set). A real battery ID can't be created or first published on demo product data, and an ID can only be swapped for one of the same kind [R03, U16].
- File actions (accident reports, "Check the file") share the upload rate limit [S07].
- Only http and https addresses become links [S01]. The accident link (#92) belongs to the app and can't be set by a file; "Check the file" shows a current value only to a sender who may see it [S03, S05].

### For cluster admins

- "Add user" with an address that already has an account only adds the person to your cluster; their name and password stay as they are. For people outside your clusters the page says "You can't change this person's account here. Ask the Scania team." [R19]
- The x beside a cluster asks first, then removes the person, and says so afterwards [U29].
- "Waste" asks a second question, because only the team can undo it [R10].
- The account page says "Your name", "Your e-mail" and "Current password", and a cluster user's role reads "Cluster member" [W04, S06].

### For readers

- Market surveillance marks a passport invalid with a button in the Purchaser card, then a confirm step [U11].
- A signed-in reader who opens the login address goes to their start page, and the login page says how to get an account [U31, U03].
- Demo accounts and demo tokens reach only demo batteries; on any other battery they see what a visitor sees [R03, U16].

## Battery ID and passport snapshots

- Added first-class Battery IDs and Passport IDs.
- Moved management, registry, QR, telemetry, and external battery API routes to Battery ID.
- Kept passports as immutable non-telemetry snapshots with historical/latest state.
- Renamed user-facing Battery version to Battery Model.

## [1.2.0] - 2025-10-23
 - Lincense consistently updated to Creative Commons Attribution 4.0 International (CC-BY-4.0) license. See the [LICENSE](https://creativecommons.org/licenses/by/4.0/legalcode) file for more details. 

## [1.2.0] - 2025-01-14
### Updated
- Updated battery passport data model according to: 
  - ### DIN DKE Spec 99100:2025-02 "Requirements for data attributes of the battery passport"
- Link to the DIN Spec: https://www.dinmedia.de/en/technical-rule/din-dke-spec-99100/385692321
  

## [1.0.0] - 2024-05-28
### New
- Initial battery passport data model




