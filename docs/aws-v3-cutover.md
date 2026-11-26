# Moving the live site to the new app (DPP V3) — Cut-over Runbook

**What this does:** the one Elastic Beanstalk environment behind `https://www.sib-scania-eubp.eu/` (`battery-pass-env`, `eu-north-1`) changes from the old app to the new one.
- **Old app:** branch `security/demo-api-hardening`, database `battery_pass_demo`.
- **New app:** the tag `new-app-v3.7` (the go-live version `new-app-v3.5`, then `new-app-v3.6` and `new-app-v3.7`, on top of `new-app-v3.4`), database `battery_pass_v3`.

The old app stays restorable (section 7). Nothing runs side by side.

**Who:** the product owner runs every step in AWS, Atlas and the registrar. The app's sessions write and check this runbook; they never touch AWS, Beanstalk, Atlas or `battery_pass_demo`.

**Cost:** none added. It is the same environment, load balancer, NAT gateway, instance, certificate and domain (design, "Where it runs": the no-extra-cost path, confirmed by the owner on 4 Nov 2026, "Kept is enough"). The one possible cost is a larger instance, if the new app needs more memory (section 6, check 8). That would be the owner's choice, and nothing here changes it.

**This runbook is for the go-live version, brought up to `new-app-v3.7`** (wave 2, the platform package, the hardening package, the serial number package and the Excel loader on top of `new-app-v3.4`). Where it says "the live database", that is `battery_pass_v3`, the database the owner enters real data into. It holds real product data and no demo data (decision 82).

**Old addresses stop working, by choice.** The old app's pages and links (its `/dpp/…` passports, QR codes and API) go away at the cut-over. No old QR code or link was ever shared (the owner: "No, demo only"), so there are no redirects.

## 1. What is already in place (checked 4 Nov 2026, read-only)

| What | State |
|---|---|
| `www.sib-scania-eubp.eu` | DNS CNAME to `battery-pass-env.eba-2a34ep5k.eu-north-1.elasticbeanstalk.com` (the live environment) |
| `https://www.sib-scania-eubp.eu/` | 200; the load balancer's HTTPS certificate covers it |
| `http://www.sib-scania-eubp.eu/` | 307 to `https://www.sib-scania-eubp.eu/` (the app's own redirect) |
| `http://` and `https://sib-scania-eubp.eu/` (no www) | 301 to `https://www.sib-scania-eubp.eu/`, with a valid certificate. This is the registrar's forwarding (A records 3.33.251.168 and 15.197.225.128). |
| `/health` on www | 200 over HTTP and HTTPS (the load balancer's check) |
| Atlas | `battery_pass_v3` is on the same cluster as `battery_pass_demo`, so the NAT gateway's fixed IP is already on Atlas's Network Access list |
| .NET | Both apps target .NET 10, so the environment's platform stays as it is |
| Bundle | `scripts/publish-elastic-beanstalk.ps1` is the same on both branches |

DNS, HTTPS and the www redirect need no change.

## 2. Before the day (the owner)

1. **The domain belongs to Scania Industrial Batteries AB.** Check the registrar's record (still open, 4 Nov 2026).
2. **An Atlas database user for the live site:**
   - Atlas, Database Access, Add new user, e.g. `beanstalk-v3`, with `readWrite` on `battery_pass_v3` only. The PC's `kassem-v3-local` stays the PC's.
   - Build its connection string as the launcher's, with the new user. This is the new `MONGODB_URI`.
   - Never give it access to `battery_pass_demo`.
3. **The signing key** (the owner's own steps; nobody else reads or copies it):
   - Passports are signed with the key in `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64`. **In Production the app doesn't start without it** (or with a value that isn't a P-256 key in base64 PKCS#8; the message names the setting, never the value). Outside Production it can fall back to the one-line file `%LOCALAPPDATA%\BatteryPassDemo\signing-key-p256.pkcs8` on the machine that runs the app.
   - The passports in `battery_pass_v3` were signed on the PC. Each signature stores its own public key, and they show "Verified" on any site whose key is that same one (or lists it in `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS`, below): "Verified" now also means the key is one the site knows.
   - The live site still needs the same key, so that passports published there are signed by the same key as those published on the PC. One key is what a reader or authority can trust; a new key per machine would split the passports between keys.
   - Without the setting the app would have no key to sign with, and Production refuses to start.
   - Back up that one-line value (or the launcher's `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64`, if the launcher sets it) in a safe place, e.g. the company password manager.
   - Never put it in Git, a chat or a file in the repository.
4. **The restore points** (section 7):
   - the tag `old-app-final` at 8c8bfe7 (the old app; the tag `app-v1.2-before-dpp-v3` already marks the same commit);
   - the tag `new-app-v3.7` on the go-live commit, pushed to `origin`.
5. **The bundle:** check out `new-app-v3.7` and run `powershell -ExecutionPolicy Bypass -File .\scripts\publish-elastic-beanstalk.ps1`. Keep the zip it prints (`artifacts\releases\BatteryPassWeb-elastic-beanstalk-<time>.zip`). The script leaves out `.env*` files.
6. **Upload it as a new application version, without deploying:**
   - Beanstalk, Application `battery-pass`, Application versions, Upload, label `new-app-v3.7`.
   - Check that the application's version lifecycle setting won't delete the old app's version. Note the old version's label (Environment, Running version).
7. **The settings file** for the switch: `cutover-settings.json` (section 4). Keep it outside the repository, and delete it after the switch.
8. **No demo data on the live site** (decision 82):
   - `ENABLE_DEMO_DATA=false` goes into the settings (section 4), and `DEMO_USERS_PASSWORD` is left out. There are no demo accounts and no "Reset demo data".
   - The live database holds no demo data. If a demo battery or demo family is still in it, press "Clear demo data" (Clusters tab, end) once after the switch (check 6).
   - Trial batteries are made on the real product families with test IDs (check 10).
9. **A rehearsal first** (decision 79): a second Beanstalk environment with the same bundle and the same settings, on a copy of the database (`MONGODB_DB` set to the copy's name), under its own address. Run the checks of section 6 there, and scan a QR code with a phone. Its `APP_BASE_URL` and `DPP_ID_BASE_URL` are the rehearsal's own address, so nothing it signs is mistaken for the live site's. Delete the environment and the copy afterwards.
10. **A back-up of `battery_pass_v3`** just before the switch (an Atlas snapshot or a `mongodump`): the earlier version can't open the database once the new one has written to it (decision 62).
11. **The first team account**, only if the database has no team account (a new, empty database):
    - Put `FIRST_TEAM_ACCOUNT_EMAIL` (one address, the person who will run the team pages) into the settings before the first start.
    - The person then opens the sign-in page, presses "Forgot password", types that address and signs in with the temporary password that arrives by e-mail. That needs the reset mail to work (`POWER_AUTOMATE_RESET_WEBHOOK_URL` and its secret, section 3; see `power-automate-password-reset.md`). Without the mail, nobody can sign in.
    - There is no fallback admin in Production and no password is ever printed in the log.
    - If `battery_pass_v3` already holds a team account (the owner's own), the setting does nothing and the log says so. It can stay set.
12. **The proxy's body limit.** The Excel load (section 6.14) sends up to 100 MB in one post. A proxy in front of the app (nginx on Elastic Beanstalk) must let through bodies of **100 MB for `/admin/product-data-load`** (the page and its `/check`). Everywhere else it may keep the upload limit. For example: `location /admin/product-data-load { client_max_body_size 100m; }`. The app answers 413 over its own limit; a proxy that stops it earlier shows the reader a plain proxy error.

## 3. The settings (environment properties)

The new app reads these. "Same" means the live environment's current value stays.

| Setting | Value on the live site | Why |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (same) | |
| `MONGODB_URI` | the `beanstalk-v3` user's connection string (new) | access to `battery_pass_v3` only. The database must be MongoDB with collation support (the app finds an e-mail address whatever its capitals): Atlas has it; **Amazon DocumentDB does not**, so it can't be used. |
| `MONGODB_DB` | `battery_pass_v3` (new) | Since plan 6a-1 the app refuses to start in Production without it. |
| `APP_BASE_URL` | `https://www.sib-scania-eubp.eu/` (new) | QR codes, document links and the accident page are built from it. A trailing `/` is fine. In Production it must be a whole `http://` or `https://` address, and **the same site as `DPP_ID_BASE_URL`** (scheme, host and port, a trailing dot ignored), or the app doesn't start; the message says what differs. |
| `APP_URL` | remove, or the same as `APP_BASE_URL` | the old name; `APP_BASE_URL` wins |
| `REQUIRE_HTTPS_REDIRECTION` | `true` (same) | |
| `SESSION_SECRET` | same | |
| `EXTERNAL_API_ENCRYPTION_KEY` | same | Must be set, or the API tokens stop working. They are stored as salted hashes that don't use it, so existing tokens keep working whatever its value. |
| `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64` | the PC's key (section 2.3; new) | **Required in Production:** the app doesn't start without it. Passports keep their signatures. |
| `BATTERY_PASS_SIGNING_TRUSTED_PUBLIC_KEYS` | not set (optional) | Earlier public keys the site still vouches for: base64 SubjectPublicKeyInfo, separated by commas. **Before changing the signing key**, copy the current public key from the start-up log line "After a key change, add this public key to …" and put it here, or every earlier passport shows "Not verified: signed with a key this site doesn't know" and its public data says `"proof": null`. An entry that isn't a P-256 public key stops the app in Production. |
| `BATTERY_PASS_SIGNING_ISSUER`, `BATTERY_PASS_SIGNING_VERIFICATION_METHOD` | not used any more | Both settings are gone. The issuer is `did:web:` plus the host of `DPP_ID_BASE_URL` (`did:web:www.sib-scania-eubp.eu`); the key's ID is its fingerprint. |
| `DPP_PASSPORT_CHECK` | not set (on) | The check of all published passports, shortly after start and every hour (it puts the "changes are waiting" marks right and never publishes). `off` turns it off; leave it on in Production. |
| `DEMO_USERS_PASSWORD` | not set (decision 82) | Demo accounts sign in only while `ENABLE_DEMO_DATA` is on. The live site has none. |
| `ENABLE_DEMO_DATA` | `false` (new; decision 82) | Switches the demo data, demo accounts and demo tokens on. Off: there is no "Reset demo data", demo accounts can't sign in and demo tokens are refused; "Clear demo data" stays. Set it to `false` on the live site and on the owner's PC, so that "Reset demo data" can't bring the demo families back. It is off in Production when not set; say it anyway, so the PC's launcher and the site read alike. |
| `FIRST_TEAM_ACCOUNT_EMAIL` | the owner's choice, only for an empty database (new) | The address of the first team account. Optional and not secret. Used only when the database has no team account (counted the way the team pages count: a demo account doesn't count); otherwise the log says it is ignored. The address must be one "Forgot password" takes; one it refuses makes no account and the log says why. Nothing happens when it is empty. If the account it made has lost team access and no team account is left, the log says to delete that account (`users`, `_id` "first-team-account") and start again. See section 2.11. |
| `HEALTH_DIAGNOSTICS` | not set | **`true` stops the start in Production**. Leave it out. |
| `ENABLE_DEMO_ADMIN_FALLBACK` | `false` or not set | Never a fallback admin on a public site. **In Production the app refuses to start if it is on.** The first team account comes from `FIRST_TEAM_ACCOUNT_EMAIL`. |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | not set | There is no default password. The fallback admin works only outside Production, and only when a password is set here. Don't set them on the live site. |
| `ENABLE_RATE_LIMITING` | not set (on) | Turns all the rate limits off with `false`; leave it on. What a person or a caller sees over a limit is in section 6, check 18. |
| `RATE_LIMIT_WINDOW_SECONDS` | not set (60) | The length of the window every limit counts in. |
| `EXTERNAL_API_READ_RATE_LIMIT_PER_WINDOW` | not set (120 per window) | The public read limit, counted per client address: the passports' public data (`/dpp/…`, the `.json` addresses), the home search (`/search`) and the home page with a search text (`/?q=…`). The API's own read limit is counted apart, per token. **A search that finds nothing uses two of the allowance** (the search, then the home page it leads to). Do not set it low: an office of many people shares one address. |
| `FILE_UPLOAD_RATE_LIMIT_PER_WINDOW` | not set (10 per rate-limit window) | Limits file uploads. It also limits the team's and cluster admins' file actions (accident reports, "Check the file"). **It counts per signed-in person** (by e-mail address), so people behind one office address each have the whole allowance; a caller who isn't signed in is counted by address. With `ENABLE_RATE_LIMITING=false` nothing is limited. |
| `MAX_UPLOAD_BYTES` | not set (10 MB) | The biggest file one upload may carry. The pages and the 413 answer name it in megabytes ("10 MB"). The Excel load has its own, bigger limits (section 2.12). |
| `TRUSTED_PROXY_NETWORKS`, `TRUSTED_PROXY_HOP_LIMIT` | not set | The defaults trust the load balancer's private ranges. **They decide the client address the app sees**, read from the forwarded header (`X-Forwarded-For`) of a trusted proxy, and the sign-in lock counts wrong passwords per client address. If they are wrong, every request seems to come from the proxy's address and one person guessing locks the account for everyone. Check 17 in section 6 tests it. |
| `POWER_AUTOMATE_RESET_WEBHOOK_URL`, `…_SECRET`, `PASSWORD_RESET_APP_NAME` | same, if password reset e-mails are used | |
| `ID_GENERATION_SECRET` | same | Only the old app reads it. Keeping it makes a roll-back simpler. |
| `DPP_ID_AGENCY` | `UN` (new) | The battery IDs' issuing agency: DUNS (decision 38). |
| `DPP_ID_COMPANY` | `778474481` (new) | Scania Industrial Batteries AB's DUNS. |
| `DPP_ID_BASE_URL` | `https://www.sib-scania-eubp.eu` (new) | The start of every battery ID and passport ID, e.g. `https://www.sib-scania-eubp.eu/25S/UN778474481-…`, and the site the passports' signatures name. In Production it must be a whole `http://` or `https://` address and the same site as `APP_BASE_URL`. If it is later moved to another host, every earlier passport's public data says `"proof": null` until that passport gets a new version (listing the earlier key doesn't bring it back). Without all three, the Battery IDs page shows a red notice naming the missing ones, and every real release and create is refused (check 9). |

### What the app keeps in the database, and what a back-up, a reset and "Clear demo data" do with it

New collections in the go-live version. The app makes them (and their indexes) at the first start; nothing to do by hand.

| Collection | What it holds | Back-up | Reset tool (`tools/V3Reset`) | "Clear demo data" / "Reset demo data" |
|---|---|---|---|---|
| `dataProtectionKeys` | The keys that protect sign-in cookies and open forms, so that a restart, a new deployment and several instances share them. **Stored encrypted,** with a secret made from the signing key (`BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64`). **Each environment has its own keys** (the live site, a developer's PC), even on one database. | A copy of the database without the signing key carries no usable sign-in keys; keep the signing key out of back-ups. | Kept | Left alone |
| `signInAttempts` | Wrong passwords per account and per client address, for the sign-in lock (ten wrong in 15 minutes from one address lock that address out of that account for 15 minutes; fifty from all addresses together limit the account to addresses it signed in from before), and those known addresses per account (kept 90 days, the database removes the old ones). | Included; the counts matter for 15 minutes, the known addresses for 90 days. | Emptied (so every address is a stranger again until it signs in) | Left alone |
| `restrictedReads` | One key per reader, battery and day (and per document), so that a reader's first opening of restricted data each day is logged once. Entries expire after 3 days. | Included; nothing in it matters after a few days. | Emptied | Left alone |

- Deleting `dataProtectionKeys` signs everybody out once; new keys are made at the next start. The first start of the go-live version makes the first key, so **everybody is signed out once at this update**.
- **Changing the signing key signs everybody out once** (the old keys can't be read; new ones are made at the next start) and breaks any form that was open. Plan it for a quiet time.
- **The PC and the live site no longer share sign-ins,** even on one database: each environment has its own keys, so a cookie from the PC is refused on the live site and the other way round. Each signs in on its own.
- **Old unencrypted key documents** (from an earlier version, or from a PC without the signing key; the documents without a `cipher` field) are never used in Production. The start log counts them in one line ("N sign-in keys in the database are not encrypted … This site never uses them"); they can be deleted from `dataProtectionKeys`, nothing breaks. A damaged key document is skipped, with no error and no stop.
- **On a new database, start one instance first,** then add the second: the key then exists before the second starts. Two instances starting together on an empty collection may each make a key and use it for up to a day; nobody notices, because a cookie with an unknown key makes the app read the key ring again.
- **What the start log says about the keys:** with a signing key, nothing about encryption; on a PC without one, one warning line that the keys are not encrypted; in Production, the line about unencrypted documents only if there are some.
- **The unique index `item_key` on the batteries** (S1): at every start the app makes the indexes, then gives each old battery its key (its battery ID without dashes and capitals), in record-id order, with a write that never overwrites a key another instance or a swap has just set. Of two old batteries whose keys collide, the one first by record id keeps the key (not necessarily the older one); the other is left without one, and the log says: "Battery … has the same battery ID as battery …, apart from capitals and dashes … it was left without its key. Decide which of the two is right." The start never fails. Nothing to do by hand; if the log names a pair, the team decides which battery is right. A released ID whose key a battery already has stays "Available" in the list; the app refuses to use it and says so: void it on the Battery IDs page, with a reason, and use another. The first start with 2,000 old batteries takes a couple of seconds; later starts a few milliseconds.
- Old tokens made with "Read, write and sign" for a cluster stay and are listed with what they can still do. The team can replace them. Old memberships without an account stay.

## 4. Why the switch is one command, and the command

**The risk:** the live environment already sets `MONGODB_DB=battery_pass_demo`.
- If the new code is deployed first, the new app starts against `battery_pass_demo` and creates its collections and indexes there.
- If the settings are changed first, the old app restarts against `battery_pass_v3`.
- Either one writes into the wrong database. The console changes code and settings in two separate updates, so the switch is one `update-environment` call that carries both: the new version and the new settings, in one environment update.

`cutover-settings.json` (outside the repository; fill in the values, delete it after):

```json
[
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "MONGODB_URI", "Value": "<beanstalk-v3 connection string>" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "MONGODB_DB", "Value": "battery_pass_v3" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "APP_BASE_URL", "Value": "https://www.sib-scania-eubp.eu/" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "APP_URL", "Value": "https://www.sib-scania-eubp.eu/" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64", "Value": "<the key from section 2.3>" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "DPP_ID_AGENCY", "Value": "UN" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "DPP_ID_COMPANY", "Value": "778474481" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "DPP_ID_BASE_URL", "Value": "https://www.sib-scania-eubp.eu" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "ENABLE_DEMO_ADMIN_FALLBACK", "Value": "false" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "ENABLE_DEMO_DATA", "Value": "false" },
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "FIRST_TEAM_ACCOUNT_EMAIL", "Value": "<only for an empty database: the first team member's address>" }
]
```

Leave out the `FIRST_TEAM_ACCOUNT_EMAIL` line when `battery_pass_v3` already has a team account.

## 5. The day (the owner, in a quiet hour)

1. **Save the old configuration:** Beanstalk, `battery-pass-env`, Actions, Save configuration, name `old-app-final`.
2. **The PC:** close the PC's app. From now on the live site writes to `battery_pass_v3`, and only one app should write there. **Every instance on one database runs the same version:** the PC's app and the live site share `battery_pass_v3`, so update both to `new-app-v3.7` before the PC's app is started again. `new-app-v3.5` stores a battery ID key on each battery the first time it starts on a database; after that `new-app-v3.4` can't use that database (its battery list comes up empty with "Element 'ItemKey' does not match any field"). A back-up taken before the update is the only way back. To keep trying things on the PC afterwards, give the launcher a database of its own (a copy of `battery_pass_v3` under another name, in `MONGODB_DB`); don't start the PC's app against `battery_pass_v3` again. If it ever has to run against it, the launcher must set `ENABLE_DEMO_DATA=false` (decision 82), so that "Reset demo data" can't bring the demo families back into the real database.
3. **The switch** (AWS CLI, signed in to the account that holds the environment):

   ```powershell
   aws elasticbeanstalk update-environment --region eu-north-1 --environment-name battery-pass-env --version-label new-app-v3.7 --option-settings file://cutover-settings.json
   ```

4. Wait until the environment's health is Green and its running version is `new-app-v3.7` (a few minutes; the site is down meanwhile).
5. **Delete `cutover-settings.json`.**
6. Run the checks (section 6).

## 6. Checks after the switch

1. `https://www.sib-scania-eubp.eu/health` answers 200.
2. `https://www.sib-scania-eubp.eu/` shows the new home page. `http://www…` and the address without www lead to it.
3. Sign in with the team's account (accounts in `battery_pass_v3` keep their passwords). On a new database, use the first team account (section 2.11): "Forgot password", the temporary password, then a password of your own (at least 12 characters).
4. **Signatures:**
   - Open a published passport's detail page (Registry, then a battery, "View more about this passport"). The hero card's "Signature" says **Verified**.
   - That shows the passport wasn't changed since it was signed and that it was signed with a key this site knows: the PC's key, which is the site's key now. A passport whose key the site doesn't know says "Not verified: signed with a key this site doesn't know".
   - **That the live site signs with the PC's key**, check in Atlas (read-only):
     - Before the switch, in Data Explorer, `battery_pass_v3`, `dpp_passport_versions`, note the field `Proof.PublicKeyX` of one version published on the PC.
     - After the switch, publish one change on the live site (check 10's test battery).
     - The newest version's `Proof.PublicKeyX` must be the same value. If it differs, `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64` isn't the PC's key.
   - The pages don't show the key itself; their "Key" line is its name.
   - **The key's address:** `https://www.sib-scania-eubp.eu/.well-known/did.json` must reach the app (no sign-in). It lists the site's public keys, and anyone checks a passport's public data against it. If the load balancer or a rule in front of the site sends `/.well-known/` elsewhere, fix that.
5. **QR code:** scanning a passport's QR code opens `https://www.sib-scania-eubp.eu/dpp/…`.
6. **Links made on the PC:**
   - A battery's accident page link (#92) and its restricted documents' links were saved with the PC's address (`http://localhost:5186/…`) when they were made. Batteries made on the live site get the live address.
   - The live site runs without demo data (the owner, 11 and 13 Nov 2026; decision 82). `ENABLE_DEMO_DATA` is `false`. If the database still holds demo batteries or demo accounts, press "Clear demo data" (Clusters tab, end) once. The card then says "Demo data is switched off on this site" and has no "Reset demo data" button; "Clear demo data" stays. Its question also counts the test IDs and test batteries that go with it, so read it before you confirm.
   - Do not press "Reset demo data" anywhere on the live site.
7. **API:** a cluster token reads its batteries at `https://www.sib-scania-eubp.eu/api/external/v1/…` (the API help page's examples).
8. **Load:** after a day, check the instance's CPU and memory in Beanstalk's Monitoring. If the instance is short of memory, a larger instance type is a cost question for the owner.
9. **The ID settings** (on the day): signed in as the team, open the Battery IDs tab (`/admin/battery-ids`). The page shows **no red notice**. A red notice names the missing `DPP_ID_*` settings: add them in Beanstalk (Configuration, Environment properties) and check again.
10. **A test battery's ID** (on the real family, decision 67 and 82): sign in as the team. On Battery IDs, release a test ID (it contains `TEST`; it is never a real ID and isn't counted), make a battery with it on a real model, upload its readings and publish it. Its ID reads `UN778474481-TEST-…`, and `https://www.sib-scania-eubp.eu/25S/<that ID>` opens its public summary with a "Test passport" tag, or "Not published yet" while it isn't published. On Battery IDs, All IDs, tick "Show test IDs" to see it. "Clear demo data" removes test IDs and test batteries, so use it only when you want them gone. On a test site with the demo on, A demo ID reads `UN778474481-TEST-…` too.
11. **The first passport check:** a minute after the app starts, it compares every published battery with its passport (the audit log gets an entry `dpp.passports-checked` if it changed anything). The team can press "Check all passports now" on the battery list.
12. **Before real use** (the team, once): open each model's page and answer its three questions (commercial warranty, cycling use, cadmium or lead above the limit): until then nothing of that model can be published and its published batteries get no status change. Open Clusters and tick each customer's written authorisation (with its date): until then its admins and tokens only read.
13. **The removed-passport collection:** `dpp_removed_passports` is made with its index when the app starts; nothing to do. It is empty until the team removes a recycled battery's passport.
14. **Security headers.** Every response carries a content security policy, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` and `X-Frame-Options: SAMEORIGIN`: error pages, static files, the health check and redirects too. The policy lets the page load scripts and styles from the app itself only (no CDN: Bootstrap is served from `/lib/bootstrap/`), images from the app, `data:` and `blob:` (the home page's "scan a QR code from a picture"), and nothing from another site; each inline script carries a one-time nonce. After the switch: open the home page, the sign-in page, a public passport and a team page; the pages must show their styles and work (menus open, windows open). In the browser's developer tools (Network) no request goes to another host and the Console shows no policy message. A page that looks unstyled or has dead buttons means a proxy or rule in front of the site is changing headers or blocking `/lib/`: fix that first. (Ten `style=` attributes are still allowed inline; a later update removes them.) The third-party files under `web/wwwroot/lib` (Bootstrap) are checked out byte for byte, whatever the checkout's line-ending setting (`.gitattributes`); the bundle holds the same bytes. Nothing to do.
15. **The sign-in survives a restart:** sign in, restart the environment (or deploy the same version again), reload: you are still signed in. If you are signed out each time, the collection `dataProtectionKeys` isn't being written: check the `MONGODB_URI` user has `readWrite` and look in the log. On the live site the log has no warning about unencrypted keys (the keys are encrypted with the signing key). A line saying that sign-in keys in the database are not encrypted and are never used means old documents from an earlier version, which can be deleted.
16. **Loading product data from the Excel** (section 8, step 4) is the first real test of the proxy's body limit (section 2.12): load the workbook with all its files. A plain proxy error page (413) means the limit isn't 100 MB for `/admin/product-data-load`.
17. **The sign-in lock counts per client address:** with a test team account, type ten wrong passwords from one network (the office); the sign-in page refuses with "Invalid email or password. Try again later." Then sign in to the same account with the right password from another network (a phone's own mobile connection): it must work. If it is refused too, the proxy isn't passing the client's address (`TRUSTED_PROXY_NETWORKS`, `TRUSTED_PROXY_HOP_LIMIT`; the app reads `X-Forwarded-For`), every sign-in then counts as one sender, and one person guessing could lock the account for everyone: put it right before the site is used. Afterwards press "Forgot password" for the test account; that clears the count.
18. **Over a limit, and unknown addresses** (new in `new-app-v3.6`):
    - **A rate limit.** A person on a page gets a short sentence: "Too many searches from your address. Try again in a minute." on the home search, "Too many tries from your address. Try again in a minute." on other pages such as the log-in page. The API, the `.json` addresses and a request that asks for JSON get `{"error": "Too many requests."}` with status 429. A reader whose search finds nothing and who then searches again quickly may see the sentence earlier than expected: a failed search uses two of the public read allowance (section 3).
    - **An unknown address** shows the app's "Page not found" page with status 404. Under `/api` and for files (an address whose last part has a dot) the answer stays a plain 404. An address that exists but is asked the wrong way (for example opening `/login/logout` in the browser) answers 405, not "not found".
    - **An error inside an `/api` call** answers `{"error": "Something went wrong. Try again later."}` with status 500. On a page, an error shows "Something went wrong" with no detail.
    - To test it: open `https://www.sib-scania-eubp.eu/no-such-page` (the "not found" page, 404) and `https://www.sib-scania-eubp.eu/api/no-such-call` (a plain 404). Do not try to hit a rate limit on the live site; the limit is per address and the test would lock your own office out of the search for a minute.

## 7. Restore points, and rolling back to the old app

**Kept:**
- the branch `security/demo-api-hardening`, untouched, with the tags `old-app-final` and `app-v1.2-before-dpp-v3` at 8c8bfe7;
- the old application version in Beanstalk (its label, noted in section 2.6);
- the saved configuration `old-app-final`;
- the database `battery_pass_demo`, untouched;
- **a back-up of `battery_pass_v3`**: take one before every later update of the new app. An earlier version of the app can't open a database a later one has written to once a model is answered, a passport is published or a copy of product data is made (it answers 500). Only the back-up goes back;
- the new app: the tag `new-app-v3.7` on `origin` (and the tag `new-app-v3.4`, the last version before the go-live version), its application version and the database `battery_pass_v3`.

**Roll back** (one command again, for the same reason as section 4):

```powershell
aws elasticbeanstalk update-environment --region eu-north-1 --environment-name battery-pass-env --template-name old-app-final --version-label <the old version's label>
```

- The old app comes back with its old settings and `battery_pass_demo`, as before.
- `battery_pass_v3` keeps everything the new app wrote.
- **No way back to `new-app-v3.4` on the same database:** once `new-app-v3.5` or a later version has started on `battery_pass_v3`, `new-app-v3.4` can't use it (empty battery list, "Element 'ItemKey' does not match any field" and "'SerialFormat'" in its log). Only the back-up taken before the update goes back. Never run two versions on one database: the PC's app and the live site must run the same one.
- Going forward again is section 5, step 3.

## 8. The first day on the live site, in order

After the switch and the checks of section 6. The team does these, signed in as a team admin.

1. **Back-up** of `battery_pass_v3` just before the update (section 2.10): an Atlas snapshot or a `mongodump`. Note which one it is.
2. **Deploy** (section 5). Wait for Green; delete `cutover-settings.json`.
3. **The first team account**, if the database was empty (section 2.11): "Forgot password" with the address set in `FIRST_TEAM_ACCOUNT_EMAIL`, then sign in. Run checks 1 to 4 of section 6.
4. **Load the product data from the Excel.** Battery families, "Load product data from Excel". Choose the filled workbook and the documents and pictures it names, then press **Check**. Fix what it lists by sheet, row and column and check again; replace the demo's example values it names. A document you don't have yet is listed under "Documents still to come": it can follow in a later load. When the check shows no problem, press **Load**. It saves all of it or nothing, and says how many passports get a new version: **each published battery gets one new version for the whole load**, made after everything is saved. If a load stops part way, those batteries stay on their version with the changes waiting; load the workbook again (every load also publishes the waiting changes of the batteries it reaches) or press "Publish the waiting changes" on the battery's page. The hourly check only marks them; it never publishes. A second load of the same workbook says "Nothing to load".
5. **Answer nothing by hand that the Excel carries.** The family's and each model's values, the three questions of each model, the sites, the operators, the hazardous substances and the documents come from the workbook. The workbook carries each model's serial number format too (`VPC8C05-#####` for Core800, `VPC8C05s-#####` for Core800s, `VPC7C05-#####` for Core700): the check shows the old and the new format and how many batteries don't fit it. Open each model's page and look; the format can also be set there. A value that is wrong is corrected in the workbook and loaded again. **If the database has a family copied from the demo** ("Copy as real product data", before decision 82), the load matches the family, the site and the operator by name and updates them (they lose their demo marks), makes the three models and their batches, and leaves the copied model and its batch as they were, with their example values. Open the copied model's page afterwards and remove it there (the page offers it).
6. **Tick each customer's written authorisation** (Clusters tab, each cluster's row, with the date), if there are customers yet: until then their admins and tokens only read.
7. **Release IDs** (Battery IDs): a serial number may hold small letters (`VPC8C05s-00001`) and must fit the model's format. The same serial in other capitals is the same serial.
8. **One test battery** (check 10 of section 6): a test ID on a real family, readings uploaded, published, opened as a visitor. This is the dress rehearsal; "Clear demo data" removes it afterwards.
9. **One real battery:** a real ID, a real battery of a real model, its values typed by the team or loaded from the battery system's export; #13 (third party service provider) is typed for each battery, and without it the battery can't publish. Publish it, scan its QR code with a phone, and check the detail page's signature says Verified.

If something goes wrong after the first real battery, section 7 says what can be rolled back: only the back-up of step 1 undoes what the new version has written.

## Not in this runbook

- Running both apps at once: not wanted (the owner, 4 Nov 2026).
- Moving the database or Atlas cluster.
- E-mail.
- Registering the identifier scheme: the DUNS `778474481` is the company's own, and the IDs are on its domain (section 2.1 checks the domain's owner). The ID settings are in section 3.
