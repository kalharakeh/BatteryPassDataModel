# Passport Admin And Registry Manual Test Flow

Use this checklist to manually verify the original 32 passport/admin/registry/API findings plus the later follow-up fixes for report layout, QR downloads, shared tables, editable fields, software versions, and responsive behavior.

## 1. Start The App

1. Open a terminal in this worktree.
2. Run:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT="Development"
   dotnet run --project web/BatteryPassWeb.csproj --urls "http://localhost:5099" --no-launch-profile
   ```

3. Open `http://localhost:5099`.
4. Keep the terminal running while testing.

Expected result: the public search page opens and the app connects to the configured development MongoDB. If startup says `Database is not connected`, check that `web/.env.local` or `web/appsettings.Development.json` contains the local MongoDB settings.

## 2. Test Accounts

Use these accounts after the reset step.

| Role | Email | Password |
| --- | --- | --- |
| Global admin | `admin@example.test` | `Password123!` |
| Demo local admin | `demo.admin@example.test` | `Password123!` |
| Demo cluster user | `demo.user@example.test` | `Password123!` |
| North customer user | `customer_001_001@customer.org` | `12345` |

## 3. Full Reset To Canonical Demo Data

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=products` or `/admin/help`.
3. Press **Reset battery-family passports**.
4. Confirm the success message appears.
5. Open `/help`.
6. Copy the current sample Battery ID and latest Passport ID.
7. Open `/admin/clusters?tab=clusters`, `/admin/clusters?tab=users`, `/admin/clusters?tab=batteries`, and `/admin/clusters?tab=api-token-management`.

Expected result: reset clears stale demo state and reseeds canonical users, clusters, memberships, API tokens, editable-field policy, battery families/models/software versions, batteries, passports, and telemetry/supporting data. There should be no duplicate clusters and no unassigned batteries.

Covers original points: 1, 20, 23, 24, 27, 28, 30, 31. Covers follow-up reset clarification.

## 4. Public Sample Search And QR

1. Log out.
2. Open `/`.
3. Click **Search sample battery**.
4. Confirm it opens the public Summary report without login and does not 404.
5. Confirm the URL resolves to the latest public summary for that battery.
6. Confirm the QR image is visible in the Summary report header.
7. Click the QR image/download action.
8. Confirm the response/download is a `.png` image, not a text file.
9. Scan the QR code or inspect its payload.

Expected result: anonymous users can open the sample passport summary. QR scan opens the latest public Summary report URL for the Battery ID, and the QR download returns `image/png`.

Covers original points: 2, 3, 4. Covers follow-up fixes: public QR image/download and sample search 404.

## 5. Summary And Detailed Report Header Layout

Run this at wide desktop, medium desktop/tablet, and narrow mobile widths.

1. Open the public Summary report from step 4.
2. Confirm the top header uses three fixed identity panels at wide width:
   - Battery serial, cluster, latest/historical status
   - Passport ID and Battery ID with copy buttons
   - QR code
3. Confirm long Passport ID and Battery ID values are truncated inside the identifier panel and do not overlap the QR panel.
4. Confirm the QR panel border is fully visible and the QR code stays inside it.
5. Confirm the battery image stays in its media area and does not spill outside the page.
6. Narrow the browser to medium width and confirm the panels adapt without overlap.
7. Narrow to mobile width and confirm panels stack cleanly.
8. Open the detailed report for the same passport as an authorized user.
9. Confirm the detailed report uses the same fixed identity header pattern.

Expected result: Summary and Detail use the same responsive header design. IDs, QR, image, and panel borders remain readable at wide, medium, and narrow widths.

Covers original points: 4, 6. Covers follow-up fixes: header overlap, clipped borders, detail header parity, and mid-width responsive breakage.

## 6. Summary And Detail Chart Checks

1. On Summary, check **Original Power**.
2. Confirm it has the same heading weight and visual hierarchy as the other summary headings.
3. Check the carbon footprint pie chart.
4. Confirm it has multiple colored sections, not one solid circle.
5. Confirm the carbon footprint table legend shows colors and values.
6. Check recycled content cards.
7. Confirm each card shows color sections and a value legend.
8. Open the detailed report and repeat the carbon footprint and recycled-content checks.
9. Open the detailed report **Performance** tab.
10. Confirm all telemetry charts have matching panel widths and bounded heights.
11. Confirm the Performance tab does not keep expanding vertically or crash.

Expected result: carbon and recycled charts are colored with legends and values. Performance graphs are neat, equal-width where expected, and stable.

Covers original points: 5, 6, 25. Covers follow-up fixes: conic-gradient colors, recycled legend layout, and telemetry chart sizing.

## 7. Cluster Creation And Duplicate Cluster ID Handling

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=clusters`.
3. Confirm the create button says **Create Cluster**.
4. Enter a new cluster name and an existing Cluster ID, such as `demo-cluster`.
5. Save.

Expected result: duplicate Cluster ID is blocked. A clear message appears, the existing cluster name is not overwritten, and the name/ID you typed remain in the form instead of being cleared.

Covers original points: 8, 9. Covers follow-up fix: duplicate cluster feedback and preserved inputs.

## 8. User Access, Membership Roles, And Row Persistence

1. Open `/admin/clusters?tab=users`.
2. Start creating a user.
3. Confirm the **Access** dropdown defaults to **Cluster Member**, not **Global Admin**.
4. Confirm the Access options are:
   - Global Admin
   - Cluster Member
5. Expand an existing user row.
6. Add a cluster membership.
7. Confirm the row remains expanded after save.
8. Change a membership role.
9. Confirm the row remains expanded after save.
10. Confirm membership role options include:
    - Cluster Admin
    - Normal User
    - Notified Body
    - Market Surveillance Authorities
    - Commission
    - Person with Legitimate Interest
11. Check **Add cluster** and **Membership Role** fields.
12. Confirm dropdowns have consistent indicators and styling.

Expected result: global access and cluster memberships are separate concepts. User row edits do not collapse the row or reset the page position.

Covers original points: 10, 13. Covers follow-up fixes: Access naming/default, complete role list, and row persistence.

## 9. Password Fields And Forgot Password

1. In `/admin/clusters?tab=users`, edit or create a user.
2. Type into Password and Confirm Password.
3. Confirm both fields have the same reveal behavior.
4. Confirm only the app's outside reveal button is visible; there is no extra native inline eye button inside the field.
5. Log out.
6. Open `/login`.
7. Use **Forgot password?** with a test email.

Expected result: password fields are consistent. Forgot password shows a neutral placeholder success and stores/logs the reset request internally for admins/debugging without requiring real outbound email.

Covers original points: 11, 12. Covers follow-up password reveal fix.

## 10. Local Cluster User Management

1. Log in as `demo.admin@example.test`.
2. Open `/cluster-admin/users`.
3. Compare it with `/admin/clusters?tab=users` from the global admin view.
4. Confirm it uses the same layout, row drawer behavior, controls, and wording.
5. Confirm it only shows users and memberships for clusters the local admin manages.
6. Add or update a cluster membership.
7. Confirm the edited row remains expanded after save.

Expected result: local cluster user management matches the global users UX, scoped to the local admin's clusters.

Covers original point: 14. Covers follow-up row persistence and shared local/global UX.

## 11. Editable Fields Policy

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=local-editable-fields`.
3. Confirm the page is labeled **Editable fields**.
4. Confirm the **Cluster admin** text does not overlap the toggle/control.
5. Expand a parameter section.
6. Confirm each parameter row has three checkboxes:
   - Editable at creation
   - Editable after creation
   - Editable by local admin
7. Try to allow **Editable after creation** while **Editable at creation** is disabled.
8. Try to allow **Editable by local admin** while **Editable after creation** is disabled.
9. Save the policy.

Expected result: dependency rules are enforced, the row layout does not overlap, and the policy restricts everyone, including global admins. Global admins configure the policy; they do not bypass locked battery fields.

Covers original points: 15, 30. Covers follow-up fix: policy row overlap and global restriction clarification.

## 12. Locked And Editable Battery Fields

1. As global admin, open `/admin/clusters?tab=local-editable-fields`.
2. Temporarily allow an after-creation field such as a material mass field.
3. Open a matching battery edit page.
4. Confirm allowed fields are unlocked.
5. Confirm locked fields remain visible but disabled/gray and cannot be edited.
6. Change the allowed field to a distinctive value, for example changing Nickel mass from `51.63` to `5163`.
7. Save.
8. Reopen the battery.
9. Confirm the exact value you saved is still displayed and was not scaled or overwritten by the battery family/model baseline.
10. Change the value back to match the latest passport snapshot.
11. Save and return to the battery table.

Expected result: editable values persist exactly. New-passport-needed is calculated by comparing current battery data against the latest passport snapshot, so it appears after a real difference and clears when reverted to the latest passport value.

Covers original points: 29, 30, 31. Covers follow-up fix: editable material mass persistence and latest-passport comparison.

## 13. Battery Creation Rules

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=batteries`.
3. Start creating a new battery.
4. Confirm the Cluster field is present and marked required.
5. Try to save without a cluster.
6. Confirm validation says the battery cluster is required and keeps the form data you typed.
7. Confirm creation allows only:
   - Battery Family
   - Battery Model
   - Battery serial number
   - Manufactured date
   - Facility ID
   - Manufactured by
   - Software version
   - Cluster
8. Confirm all other battery parameters are visible as disabled/gray inherited fields.
9. Try creating a battery using an existing battery serial number.
10. Confirm validation says the serial number already exists.
11. Create a valid new battery.
12. Reopen it and confirm inherited fields came from the selected Battery Family + Battery Model and software version.

Expected result: no battery can be created without a cluster, duplicate serials are blocked, form state is preserved on validation errors, and non-creation fields are inherited and locked.

Covers original points: 1, 27, 30. Covers follow-up battery creation fixes.

## 14. Battery Edit Rules And New Passport Needed

1. Open a seeded latest battery as global admin.
2. Edit only allowed after-creation fields, such as:
   - Battery Model
   - Facility ID
   - Software version
   - Cluster
   - Any additional field currently enabled by the editable-field policy
3. Save one real change that differs from the latest passport.
4. Confirm the shared battery table shows **New passport needed** in the Status column.
5. Edit the same value back to match the latest passport snapshot.
6. Save.
7. Confirm **New passport needed** clears.
8. Confirm operational fields such as active flag, location, and contact are always editable and do not affect passport status.

Expected result: new-passport-needed is based on current battery data versus latest passport data, not simply on the act of saving or on differences from the family/model template.

Covers original points: 22, 27, 29, 32. Covers follow-up status-column and revert behavior.

## 15. Passport Creation Is Snapshot-Only

1. Use a battery that shows **New passport needed**.
2. Click the action to create a new passport.
3. Confirm you are not taken to a form where all passport/battery fields can be edited.
4. Confirm a new passport ID is created from the current battery data snapshot.
5. Confirm the previous latest passport becomes historical.
6. Confirm the new passport is latest and Draft or awaiting sign-off.
7. Return to the shared battery table.
8. Confirm Draft and **New passport needed** are not shown together for the same latest passport.
9. Continue through validate, sign, and publish.

Expected result: creating a passport freezes a snapshot. Editing battery data happens before passport creation; validate/sign/publish happens afterward.

Covers original points: 1, 22, 24, 31. Covers follow-up fix: snapshot-only creation and no Draft plus new-passport-needed conflict.

## 16. Passport History Permissions And Return Links

1. Log in as `admin@example.test`.
2. Open `/registry`.
3. Search for a battery with multiple passports.
4. Open its passport history.
5. Confirm the back button returns to `/registry`.
6. Confirm global admin can see and edit historical passports.
7. Log out and log in as `demo.admin@example.test`.
8. Open the same battery through the local admin flow.
9. Confirm local admin can see passport history read-only.
10. Confirm local admin cannot edit historical passports.

Expected result: history visibility and edit permissions are role-aware, and history opened from registry returns to registry.

Covers original point: 16. Covers follow-up fixes: registry return URL and local read-only history.

## 17. Shared Registry And Admin Battery Table

Run these checks in both `/registry` and `/admin/clusters?tab=batteries`.

1. Confirm both pages keep their URL/page shell but render the same shared search/table layout.
2. Confirm columns are compact and ordered:
   - Battery serial number
   - Battery ID
   - Family
   - Model
   - Cluster
   - Passports
   - Status
   - Updated
   - Actions
3. Confirm Battery ID is truncated with a copy button.
4. Confirm `Model` is used instead of `Battery Model`.
5. Confirm `Status` is used instead of `Latest passport status`.
6. Confirm **New passport needed** appears in the Status cell, not as an extra separate column or badge that makes the table too wide.
7. Search by battery serial number.
8. Confirm the table filters to matching rows.
9. Search by Battery ID.
10. Confirm the table filters to the matching row.
11. Search by Passport ID.
12. Confirm the table shows the battery row that owns that passport.
13. Search by cluster name or cluster ID as global admin.
14. Confirm the table shows batteries associated with that cluster.
15. Log in as a non-global user and try cluster search.
16. Confirm they see a clear message instead of cluster-wide results.

Expected result: `/registry` and `/admin/clusters?tab=batteries` show the same source of truth and the same compact UX, with role-aware actions and search scope.

Covers original points: 17, 18, 19. Covers follow-up fixes: compact columns, table search-as-filter, shared controls, status mismatch, and cluster search scope.

## 18. Admin Passports List

1. Log in as `admin@example.test`.
2. Open `/admin/passports`.
3. Confirm a Cluster column shows the cluster name.
4. Confirm Passport ID and Battery ID columns are compact, truncated where needed, and include copy controls.
5. Search by Passport ID.
6. Search by Battery ID.
7. Search by battery serial number.
8. Confirm each row shows passport status.

Expected result: admin passport search works for Passport ID, Battery ID, and serial number, with correct labels and cluster visibility.

Covers original points: 17, 18. Covers follow-up cluster-column request.

## 19. Cluster Admin Shared Pages

1. Log in as `demo.admin@example.test`.
2. Open `/cluster-admin/passports`.
3. Confirm it matches the shared battery table UX from `/admin/clusters?tab=batteries`, scoped to managed-cluster batteries.
4. Confirm local admin actions are limited to what they are allowed to do.
5. Open `/cluster-admin/secrets` or the local API token management page.
6. Compare it with `/admin/clusters?tab=api-token-management`.
7. Confirm the layout and token workflow match, but token data/actions are scoped to managed clusters.

Expected result: global-admin and local-admin shared pages look and behave the same, with permissions and data scope applied.

Covers original point: 14 and follow-up shared local/global UX.

## 20. API Token Management And Generated Token Modal

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=api-token-management`.
3. Create a cluster-scoped token.
4. Confirm the generated token appears in an app modal popup.
5. Click **Copy**.
6. Confirm the token is copied and the popup remains open.
7. Click **Okay**.
8. Confirm the token is copied and the popup closes.
9. Repeat in the local admin token page if available.

Expected result: generated tokens are shown once in a controlled modal with copy and okay behavior.

Covers follow-up token modal request.

## 21. API Token Access

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=api-token-management`.
3. Create or use a cluster-scoped read token for `demo-cluster`.
4. Open `/help`.
5. Use the API workbench with the cluster read token.
6. Read the sample Demo API battery.
7. Repeat with a global token.

Expected result: a valid cluster token can read a battery in its allowed cluster and does not return 403. A global token can read globally.

Covers original point: 20.

## 22. API Help Endpoint And Token Labels

1. Open `/help`.
2. Open the endpoint/template dropdown.
3. Confirm `/api/external/v1/batteries/{batteryId}/passports` is present.
4. Select it and run the sample request with the sample Battery ID.
5. Confirm the sign workflow token label says **Passport Lifecycle**.

Expected result: the passports endpoint is visible and the token label accurately describes the full passport lifecycle capability.

Covers original points: 23, 24.

## 23. API Battery Model Updates

1. Open `/help`.
2. Use a read-write token.
3. Send a PATCH/update that changes the sample battery to an allowed Battery Model for its Battery Family.
4. Confirm the Battery Model changes.
5. Confirm linked Battery Model fields are updated from the Battery Family/Model baseline.
6. Confirm the battery requires a new passport when the updated data differs from the latest passport.
7. Try to PATCH a non-existing Battery Model.

Expected result: allowed Battery Models update the full linked data. Unknown models are rejected instead of partially writing invalid battery data.

Covers original points: 21, 27, 29.

## 24. Battery Family Software Versions

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Open a Battery Family and Battery Model.
4. Confirm software versions are shown as rows with:
   - Software version
   - Release date
   - Latest update
   - Actions
5. Confirm saved rows are read-only.
6. Click **Add software version**.
7. Confirm the new pending row automatically uses the next highest version string for that family/model.
8. Confirm release date and latest update default to today's date.
9. Confirm the pending row has a save icon, not a remove icon.
10. Click the save icon.
11. Confirm the row becomes read-only and the remove icon appears.
12. Remove a saved software version if appropriate for the test data.

Expected result: software versions are managed per Battery Family + Battery Model, pending rows must be saved before they become part of the model, and saved versions are read-only/removable.

Covers original points: 16, 26, 27. Covers follow-up software-version UX.

## 25. API Software Version Updates

1. Open `/help`.
2. Use a read-write token.
3. Update a sample battery to a software version defined on that battery's current Battery Family + Battery Model.
4. Confirm software version changes.
5. Confirm release date and latest update date are copied automatically from the selected software version data.
6. Confirm a new passport is required only when the current battery data differs from the latest passport.
7. Try a software version that is not defined for that Battery Family + Battery Model.

Expected result: software version can be changed through app/API only to defined values, and release/latest-update metadata follows automatically.

Covers original points: 26, 27, 29.

## 26. Battery Family/Model Push Scope

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Open a specific Battery Family and Battery Model, for example `Compact 13M` model `2.0`.
4. Change a shared field such as Battery mass.
5. Save the Battery Model.
6. Push the saved Battery Model to matching batteries.
7. Confirm only batteries with the exact same Battery Family and Battery Model are updated.
8. Confirm batteries with the same family but a different model are not marked as requiring a new passport.
9. Push again without making any further change.

Expected result: push scope is exact family-model. Changed matching batteries are updated and compared against latest passports; no-op pushes do not create new passport requirements.

Covers original point: 28. Covers follow-up exact model-scope fix.

## 27. Create, Validate, Sign, And Publish Workflow

1. Use a battery that needs a new passport.
2. Use the UI or API token with **Passport Lifecycle** capability.
3. Create a new passport snapshot.
4. Validate it.
5. Sign it.
6. Publish it.
7. Confirm the latest passport status becomes Published.
8. Confirm the battery table no longer says **New passport needed** if current battery data matches the latest published passport.

Expected result: read/write token is not enough for the full trust workflow; the Passport Lifecycle capability can complete the lifecycle for allowed batteries.

Covers original points: 24, 31.

## 28. Telemetry Template And Malformed Points

1. Open `/help`.
2. Select the telemetry write example.
3. Send one valid telemetry point for one variable.
4. Send multiple valid points for one variable.
5. Send multiple variables with multiple timestamped points.
6. Send a mixed payload with one malformed point and at least one valid point.
7. Confirm the API ignores or flags malformed points without breaking the page.
8. Open the detailed report Performance tab for the same battery.

Expected result: valid telemetry appears in sorted timestamp order. Malformed points do not crash the API or Performance page.

Covers original point: 25.

## 29. Telemetry And Operational Fields Do Not Affect Passport Trust

1. Pick a clean published passport.
2. Record its visible status.
3. Send telemetry from `/help` or the API.
4. Edit operational fields such as active flag, location, or contact if available.
5. Reopen the battery and passport status.
6. Check the shared battery table.

Expected result: telemetry and operational fields are treated the same for trust purposes. They do not make the battery require a new passport, do not make a passport awaiting sign-off, and do not require validation, signing, or publishing.

Covers original point: 32 and follow-up telemetry/operational clarification.

## 30. Responsive Smoke Sweep

Run this at wide desktop, medium desktop/tablet, and narrow mobile widths.

1. `/`
2. `/login`
3. `/registry`
4. `/admin/clusters?tab=batteries`
5. `/admin/clusters?tab=clusters`
6. `/admin/clusters?tab=users`
7. `/admin/clusters?tab=local-editable-fields`
8. `/admin/clusters?tab=api-token-management`
9. `/admin/passports`
10. `/cluster-admin/users`
11. `/cluster-admin/passports`
12. `/help`
13. Public Summary report
14. Detailed report Performance tab

Expected result: text does not overlap, copy buttons stay usable, select controls look consistent, password reveal controls are not duplicated, tables minimize unnecessary horizontal scrolling, and IDs/QR/images remain contained.

Covers original points: 4, 5, 10, 13, 14, 17, 18, 19, 25. Covers follow-up responsive and compact-table fixes.

## 31. Final Pass/Fail Notes

For each failed step, record:

- URL
- Logged-in user
- Battery ID or Passport ID
- What you expected
- What happened instead
- Screenshot if visual
- API request/response if API-related

This makes each bug report reproducible from the same seeded demo state.

## 32. Quick Regression Route

If you only have time for a short smoke run after code changes, run these:

1. Full reset from `/admin/clusters?tab=products`.
2. Anonymous `/` -> **Search sample battery** -> Summary opens.
3. Summary QR image loads and QR download returns PNG.
4. Summary at wide, medium, and mobile widths.
5. `/registry` and `/admin/clusters?tab=batteries` shared table search by serial, Battery ID, Passport ID, and cluster.
6. Duplicate Cluster ID attempt preserves form values and shows a message.
7. User membership edit keeps the row open.
8. Battery create requires cluster and rejects duplicate serial.
9. Editable material mass persists exactly and new-passport-needed clears when reverted.
10. Create passport creates a snapshot only; Draft and new-passport-needed are not shown together.
11. Battery Family software version add -> save -> read-only/remove.
12. Product push updates only exact family-model matches.
13. Performance tab charts stay bounded.

Expected result: the highest-risk original and follow-up fixes still work together.
