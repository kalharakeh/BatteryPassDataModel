# V3Reset: the reset of battery_pass_v3

A one-off tool for step 3d-2 of the DPP V3 rebuild (design section 14). It clears the old app's data, the new model's
test data and the four sample API tokens from the Atlas database `battery_pass_v3`, and applies the product owner's
decisions about accounts, clusters and tokens. It works only on `battery_pass_v3` and refuses `battery_pass_demo`,
the live app's database, by name.

| Collections | What the reset does |
|---|---|
| `dpp_families`, `dpp_models`, `dpp_sites`, `dpp_batches`, `dpp_operators`, `dpp_packs`, `dpp_passport_versions`, `dpp_documents`, `dpp_files` | emptied: the new model's test data |
| `passports`, `batteries`, `passportRevisions`, the product templates and policies, `batteryTelemetry`, `passportFiles` | removed: the old app's data |
| `passwordResetRequests` | emptied |
| `users`, `clusterMemberships`, `clusters`, `apiTokens` | kept, except what the decisions file marks delete; the sample tokens always go |
| `applicationSettings`, `applicationAuditEvents`, `auditEvents`, `batteryAuditEvents` | kept; the audit log gets one "data.reset" entry with the counts |
| anything else | left alone and listed |

## Steps

Close the Battery Pass app first, and run these from the repository root in PowerShell. The launcher asks for the
Atlas password each time; use the same Atlas user and cluster host as `run-batterypass-v3-local.ps1`.

1. **Review**: `.\tools\V3Reset\v3-reset.ps1 inventory -User <Atlas user> -ClusterHost <cluster host>`
   It only reads. It writes `battery-pass-v3-review-<date>` in your user folder, with `inventory.txt` (what is in
   the database and what the reset would do) and `decisions.csv`.
2. **Decide**: open `decisions.csv` (Excel or a phone). Each row is an account, a cluster or a token; the first
   column says `keep` or `delete`, already filled in. Change `keep` to `delete` for what should go. For an account,
   the `role` column can take a new role: `admin`, `member`, `notifiedBody`, `marketSurveillanceAuthority`,
   `commission`, `liRepair`, `liRecycling`, `liTreatment`, or several joined with `+`.
3. **Dry run**: `.\tools\V3Reset\v3-reset.ps1 reset -User <Atlas user> -ClusterHost <cluster host> -Decisions <path to decisions.csv>`
   It shows what the reset would do and changes nothing. A mistake in the file stops it with the line to fix.
4. **Reset**: the same command with `-Run`. It shows the plan again and asks you to type `battery_pass_v3`; anything
   else stops it. It then saves a safety copy of everything it removes or changes in
   `battery-pass-v3-safety-copy-<date>` in your user folder, and only then makes the changes.
5. **Check**: start the app with `run-batterypass-v3-local.ps1`, sign in, and look at Batteries (empty), Users and
   the audit log (the "data.reset" entry). New accounts are made on the Users page. Delete the safety copy folder
   once you're satisfied: it holds removed accounts' password hashes.

If a reset stops halfway (a lost connection, say), run the inventory again and start from its new decisions file:
rows for what is already gone would stop the reset with "there is no such account".
