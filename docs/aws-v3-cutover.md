# Moving the live site to the new app (DPP V3) — Cut-over Runbook

**What this does:** the one Elastic Beanstalk environment behind `https://www.sib-scania-eubp.eu/` (`battery-pass-env`, `eu-north-1`) changes from the old app to the new one.
- **Old app:** branch `security/demo-api-hardening`, database `battery_pass_demo`.
- **New app:** branch `feature/dpp-v3-step2d-team-pages`, database `battery_pass_v3`.

The old app stays restorable (section 7). Nothing runs side by side.

**Who:** the product owner runs every step in AWS, Atlas and the registrar. The app's sessions write and check this runbook; they never touch AWS, Beanstalk, Atlas or `battery_pass_demo`.

**Cost:** none added. It is the same environment, load balancer, NAT gateway, instance, certificate and domain (design, "Where it runs": the no-extra-cost path, confirmed by the owner on 4 Nov 2026, "Kept is enough"). The one possible cost is a larger instance, if the new app needs more memory (section 6, check 8). That would be the owner's choice, and nothing here changes it.

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
   - the tag `new-app-v3` on the new branch's last commit, pushed to `origin`. The managing session does this after step 6.
5. **The bundle:** check out `new-app-v3` and run `powershell -ExecutionPolicy Bypass -File .\scripts\publish-elastic-beanstalk.ps1`. Keep the zip it prints (`artifacts\releases\BatteryPassWeb-elastic-beanstalk-<time>.zip`). The script leaves out `.env*` files.
6. **Upload it as a new application version, without deploying:**
   - Beanstalk, Application `battery-pass`, Application versions, Upload, label `new-app-v3`.
   - Check that the application's version lifecycle setting won't delete the old app's version. Note the old version's label (Environment, Running version).
7. **The settings file** for the switch: `cutover-settings.json` (section 4). Keep it outside the repository, and delete it after the switch.
8. **Demo data on the live site** (the owner's choice):
   - The demo accounts sign in with `DEMO_USERS_PASSWORD`. On a public site, that must be a strong value of its own, never the PC's.
   - Leave it out to have no demo accounts.
9. **A rehearsal first** (decision 79): a second Beanstalk environment with the same bundle and the same settings, on a copy of the database (`MONGODB_DB` set to the copy's name), under its own address. Run the checks of section 6 there, and scan a QR code with a phone. Its `APP_BASE_URL` and `DPP_ID_BASE_URL` are the rehearsal's own address, so nothing it signs is mistaken for the live site's. Delete the environment and the copy afterwards.
10. **A back-up of `battery_pass_v3`** just before the switch (an Atlas snapshot or a `mongodump`): the earlier version can't open the database once the new one has written to it (decision 62).

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
| `DEMO_USERS_PASSWORD` | the owner's choice (section 2.8) | Demo accounts sign in only while `ENABLE_DEMO_DATA` is on (see below). |
| `ENABLE_DEMO_DATA` | not set (off in Production) | Switches the demo data, demo accounts and demo tokens on. Off: there is no "Reset demo data", demo accounts can't sign in and demo tokens are refused; "Clear demo data" stays. Set it to `true` only on a test site, or for the days the demo is wanted. |
| `ENABLE_DEMO_ADMIN_FALLBACK` | `false` or not set | never a fallback admin on a public site. **In Production the app refuses to start if it is on.** |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | not set | There is no default password. The fallback admin works only outside Production, and only when a password is set here. Don't set them on the live site. |
| `ENABLE_RATE_LIMITING` | not set (on) | |
| `FILE_UPLOAD_RATE_LIMIT_PER_WINDOW` | not set (10 per rate-limit window) | Limits file uploads. It now also limits the team's and cluster admins' file actions (accident reports, "Check the file"). With `ENABLE_RATE_LIMITING=false` nothing is limited. |
| `TRUSTED_PROXY_NETWORKS`, `TRUSTED_PROXY_HOP_LIMIT` | not set | The defaults trust the load balancer's private ranges. |
| `POWER_AUTOMATE_RESET_WEBHOOK_URL`, `…_SECRET`, `PASSWORD_RESET_APP_NAME` | same, if password reset e-mails are used | |
| `ID_GENERATION_SECRET` | same | Only the old app reads it. Keeping it makes a roll-back simpler. |
| `DPP_ID_AGENCY` | `UN` (new) | The battery IDs' issuing agency: DUNS (decision 38). |
| `DPP_ID_COMPANY` | `778474481` (new) | Scania Industrial Batteries AB's DUNS. |
| `DPP_ID_BASE_URL` | `https://www.sib-scania-eubp.eu` (new) | The start of every battery ID and passport ID, e.g. `https://www.sib-scania-eubp.eu/25S/UN778474481-…`, and the site the passports' signatures name. In Production it must be a whole `http://` or `https://` address and the same site as `APP_BASE_URL`. If it is later moved to another host, every earlier passport's public data says `"proof": null` until that passport gets a new version (listing the earlier key doesn't bring it back). Without all three, the Battery IDs page shows a red notice naming the missing ones, and every real release and create is refused (check 9). |

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
  { "Namespace": "aws:elasticbeanstalk:application:environment", "OptionName": "DEMO_USERS_PASSWORD", "Value": "<only if demo accounts are wanted>" }
]
```

Leave out the `DEMO_USERS_PASSWORD` line if no demo accounts are wanted.

## 5. The day (the owner, in a quiet hour)

1. **Save the old configuration:** Beanstalk, `battery-pass-env`, Actions, Save configuration, name `old-app-final`.
2. **The PC:** close the PC's app. From now on the live site writes to `battery_pass_v3`, and only one app should write there. To keep trying things on the PC afterwards, give the launcher a database of its own (a copy of `battery_pass_v3` under another name, in `MONGODB_DB`); don't start the PC's app against `battery_pass_v3` again: it runs in development mode, with the demo data switched on.
3. **The switch** (AWS CLI, signed in to the account that holds the environment):

   ```powershell
   aws elasticbeanstalk update-environment --region eu-north-1 --environment-name battery-pass-env --version-label new-app-v3 --option-settings file://cutover-settings.json
   ```

4. Wait until the environment's health is Green and its running version is `new-app-v3` (a few minutes; the site is down meanwhile).
5. **Delete `cutover-settings.json`.**
6. Run the checks (section 6).

## 6. Checks after the switch

1. `https://www.sib-scania-eubp.eu/health` answers 200.
2. `https://www.sib-scania-eubp.eu/` shows the new home page. `http://www…` and the address without www lead to it.
3. Sign in with the team's account (accounts in `battery_pass_v3` keep their passwords).
4. **Signatures:**
   - Open a published passport's detail page (Registry, then a battery, "View more about this passport"). The hero card's "Signature" says **Verified**.
   - That shows the passport wasn't changed since it was signed and that it was signed with a key this site knows: the PC's key, which is the site's key now. A passport whose key the site doesn't know says "Not verified: signed with a key this site doesn't know".
   - **That the live site signs with the PC's key**, check in Atlas (read-only):
     - Before the switch, in Data Explorer, `battery_pass_v3`, `dpp_passport_versions`, note the field `Proof.PublicKeyX` of one version published on the PC.
     - After the switch, publish one change on the live site (e.g. a draft demo battery, or "Reset demo data" in check 6).
     - The newest version's `Proof.PublicKeyX` must be the same value. If it differs, `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64` isn't the PC's key.
   - The pages don't show the key itself; their "Key" line is its name.
   - **The key's address:** `https://www.sib-scania-eubp.eu/.well-known/did.json` must reach the app (no sign-in). It lists the site's public keys, and anyone checks a passport's public data against it. If the load balancer or a rule in front of the site sends `/.well-known/` elsewhere, fix that.
5. **QR code:** scanning a passport's QR code opens `https://www.sib-scania-eubp.eu/dpp/…`.
6. **Links made on the PC:**
   - A battery's accident page link (#92) and its restricted documents' links were saved with the PC's address (`http://localhost:5186/…`) when they were made. Batteries made on the live site get the live address.
   - `battery_pass_v3` holds demo data only (the owner, 4 Nov 2026). The live site runs without demo data (the owner, 11 Nov 2026): leave `ENABLE_DEMO_DATA` unset and press "Clear demo data" (Clusters tab, end) once after the switch, so the old demo batteries and accounts are gone. To show the demo for a few days, set `ENABLE_DEMO_DATA` to `true`, press "Reset demo data", and clear it again afterwards. It stops and changes nothing if a real account, battery or API token uses one of the demo's clusters (it names the cluster); demo accounts a demo admin added never stop it: every demo battery is made again with the live address. The demo tokens are new, and shown once.
   - Before that, a battery's signature check (check 4) can still use a version published on the PC. After it, every demo version is published on the live site.
7. **API:** a cluster token reads its batteries at `https://www.sib-scania-eubp.eu/api/external/v1/…` (the API help page's examples).
8. **Load:** after a day, check the instance's CPU and memory in Beanstalk's Monitoring. If the instance is short of memory, a larger instance type is a cost question for the owner.
9. **The ID settings** (on the day): signed in as the team, open the Battery IDs tab (`/admin/battery-ids`). The page shows **no red notice**. A red notice names the missing `DPP_ID_*` settings: add them in Beanstalk (Configuration, Environment properties) and check again.
10. **A demo battery's ID** (after check 6's "Reset demo data", if the demo is on): on Battery IDs, All IDs, tick "Show test IDs". A demo ID reads `UN778474481-TEST-…`, and `https://www.sib-scania-eubp.eu/25S/<that ID>` opens its battery's public summary once it is published, or "Not published yet" while it isn't.
11. **The first passport check:** a minute after the app starts, it compares every published battery with its passport (the audit log gets an entry `dpp.passports-checked` if it changed anything). The team can press "Check all passports now" on the battery list.
12. **Before real use** (the team, once): open each model's page and answer its three questions (commercial warranty, cycling use, cadmium or lead above the limit): until then nothing of that model can be published and its published batteries get no status change. Open Clusters and tick each customer's written authorisation (with its date): until then its admins and tokens only read.
13. **The removed-passport collection:** `dpp_removed_passports` is made with its index when the app starts; nothing to do. It is empty until the team removes a recycled battery's passport.

## 7. Restore points, and rolling back to the old app

**Kept:**
- the branch `security/demo-api-hardening`, untouched, with the tags `old-app-final` and `app-v1.2-before-dpp-v3` at 8c8bfe7;
- the old application version in Beanstalk (its label, noted in section 2.6);
- the saved configuration `old-app-final`;
- the database `battery_pass_demo`, untouched;
- **a back-up of `battery_pass_v3`**: take one before every later update of the new app. An earlier version of the app can't open a database a later one has written to once a model is answered, a passport is published or a copy of product data is made (it answers 500). Only the back-up goes back;
- the new app: the branch `feature/dpp-v3-step2d-team-pages` on `origin`, its tag `new-app-v3`, its application version and the database `battery_pass_v3`.

**Roll back** (one command again, for the same reason as section 4):

```powershell
aws elasticbeanstalk update-environment --region eu-north-1 --environment-name battery-pass-env --template-name old-app-final --version-label <the old version's label>
```

- The old app comes back with its old settings and `battery_pass_demo`, as before.
- `battery_pass_v3` keeps everything the new app wrote.
- Going forward again is section 5, step 3.

## Not in this runbook

- Running both apps at once: not wanted (the owner, 4 Nov 2026).
- Moving the database or Atlas cluster.
- E-mail.
- Registering the identifier scheme: the DUNS `778474481` is the company's own, and the IDs are on its domain (section 2.1 checks the domain's owner). The ID settings are in section 3.
