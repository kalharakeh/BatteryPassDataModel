# Passport Admin And Registry Manual Test Flow

Use this checklist to manually verify the 32 passport, cluster, registry, editable-field, API, QR, and telemetry changes from the May 2026 update.

## 1. Start The App

1. Open a terminal in the worktree.
2. Run:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT="Development"
   dotnet run --project web/BatteryPassWeb.csproj --urls "http://localhost:5099" --no-launch-profile
   ```

3. Open `http://localhost:5099`.
4. Keep this terminal running while testing.

Expected result: the public search page opens and shows a sample battery search option.

## 2. Test Accounts

Use these seeded accounts after the reset step:

| Role | Email | Password |
| --- | --- | --- |
| Global admin | `admin@example.test` | `Password123!` |
| Demo local admin | `demo.admin@example.test` | `Password123!` |
| Demo cluster user | `demo.user@example.test` | `Password123!` |
| North customer user | `customer_001_001@customer.org` | `12345` |

## 3. Reset To Known Demo Data

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=products` or `/admin/help`.
3. Press **Reset battery-family passports**.
4. Confirm the success message says the reset seeded batteries and passport snapshots.
5. Open `/help`.
6. Copy the current generated sample Battery ID and latest Passport ID shown on the page.

Expected result: the app has eight demo batteries and nine passport snapshots, including one Demo API battery with one historical passport and one latest passport.

Covers points: 1, 20, 23, 24, 27, 28.

## 4. Public Sample Battery And QR

1. Log out.
2. Open `/`.
3. Click **Search sample battery**.
4. Confirm the page opens the public Summary report without asking for login.
5. Confirm the summary header shows:
   - Battery serial and cluster
   - Latest passport status
   - Passport ID and Battery ID with copy buttons
   - QR code
6. Click the QR download action.
7. Confirm the downloaded file is a `.png` image, not a text file.
8. Scan or inspect the QR payload.
9. Confirm it points to the latest public Summary report for the Battery ID.

Expected result: public users can open the sample summary and download a working QR image.

Covers points: 2, 3, 4.

## 5. Summary Report Visual Checks

1. Stay on the public Summary report.
2. Check desktop width and a narrow/mobile width.
3. Confirm the top identity area adapts without text overlap.
4. Confirm **Original Power** has the same heading weight/size as the other summary headings.
5. Check the carbon footprint chart.
6. Confirm each carbon segment has a distinct color and a table legend with values.
7. Check recycled content.
8. Confirm recycled content has color indicators and a legend/table with values.

Expected result: summary content is readable on small screens and the charts explain their colors with values.

Covers points: 4, 5, 6.

## 6. Cluster Creation And Duplicate Cluster IDs

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=clusters`.
3. Confirm the create button says **Create Cluster**.
4. Try to create a new cluster using an existing Cluster ID, such as `demo-cluster`, with a different name.
5. Save the form.

Expected result: the app blocks the duplicate Cluster ID and does not overwrite the existing cluster name.

Covers points: 8, 9.

## 7. Users, Roles, Password Fields, And Forgot Password

1. Open `/admin/clusters?tab=users`.
2. Confirm user rows show global admin status clearly as yes/no.
3. Open a user row/details area.
4. Confirm cluster memberships and membership roles are shown separately from global admin status.
5. Add or edit a user password.
6. Confirm Password and Confirm Password have the same type of field and reveal behavior.
7. Confirm **Add cluster** and **Membership Role** dropdowns have consistent dropdown indicators.
8. Log out.
9. Open `/login`.
10. Use **Forgot password?** with a test email.
11. Confirm the neutral success message appears.

Expected result: roles are explicit, password fields are consistent, dropdowns look standardized, and forgot password stores a placeholder reset request without requiring real email delivery.

Covers points: 10, 11, 12, 13.

## 8. Local Cluster User Management

1. Log in as `demo.admin@example.test`.
2. Open `/cluster-admin/users`.
3. Compare the layout with `/admin/clusters?tab=users` from the global admin view.
4. Confirm it has the same dense table style and row actions.
5. Confirm it only shows users and memberships for clusters the local admin manages.
6. Create or add a test user to the Demo API cluster.
7. Confirm the button and form wording use create/add language, not update language.

Expected result: cluster user management looks and behaves like the global users page, but scoped to the local admin's clusters.

Covers point: 14.

## 9. Editable Fields Policy

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=local-editable-fields`.
3. Confirm the page is labeled **Editable fields**.
4. Expand a section.
5. Confirm each parameter row has three checkboxes:
   - Editable at creation
   - Editable after creation
   - Editable by local admin
6. Try to allow **after creation** while creation is disabled.
7. Try to allow **local admin** while after creation is disabled.
8. Save the policy.
9. Log in as `demo.admin@example.test`.
10. Edit a Demo API battery.
11. Confirm only fields allowed by the policy are editable.
12. Change a local-admin-editable signed field and save.

Expected result: policy changes affect local admins, dependency rules are enforced, and local admin edits to signed battery data can require a new passport.

Covers points: 15, 30, 31.

## 10. Battery Creation Rules

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=batteries`.
3. Start creating a new battery.
4. Confirm cluster assignment is required.
5. Confirm creation allows only:
   - Battery Family
   - Battery Model
   - Battery serial number
   - Manufactured date
   - Facility ID
   - Manufactured by
   - Software version
   - Cluster
6. Create the battery.
7. Open the created battery.
8. Confirm inherited fields were copied from the Battery Family and Battery Model.

Expected result: no battery can be created without a cluster, and non-editable parameters come from the selected family/model baseline.

Covers points: 1, 27, 30.

## 11. Battery Edit Rules And New Passport Needed

1. Open a latest seeded battery as global admin.
2. Edit only allowed after-creation fields:
   - Battery Model
   - Facility ID
   - Software version
   - Cluster
3. Save one real change.
4. Confirm the battery list shows a new passport is needed.
5. Create a new passport for that battery.
6. Confirm the new-passport-needed indicator clears once the draft passport exists.
7. Leave the passport as draft.
8. Confirm the UI asks for validate/sign/publish next, not another new passport.
9. Go back to the battery and change the editable value back to match the latest passport.
10. Save again.

Expected result: new passport needed is based on differences from the latest passport, and draft passports do not also show "new passport needed."

Covers points: 1, 22, 27, 29.

## 12. Passport History Permissions

1. Log in as `admin@example.test`.
2. Open the Demo API battery that has historical and latest passports.
3. Confirm the global admin can see history and edit historical passports.
4. Log out and log in as `demo.admin@example.test`.
5. Open the same battery.
6. Confirm the local admin can see passport history.
7. Confirm historical passports are read-only for the local admin.
8. Confirm the latest passport can be handled according to the local admin's permissions.

Expected result: local admins can inspect history but only global admins can edit historical passports.

Covers point: 16.

## 13. Managed Passport Search And Status

1. Log in as `admin@example.test`.
2. Open `/admin/passports`.
3. Search by Passport ID.
4. Search by Battery ID.
5. Search by battery serial number.
6. Confirm the first column is labeled as Passport ID when it contains Passport IDs.
7. Confirm each row shows passport status.

Expected result: both Battery ID and Passport ID search work, labels are correct, and status is visible.

Covers points: 17, 18.

## 14. Registry Search

1. Log in as `admin@example.test`.
2. Open `/registry`.
3. Search by Battery ID.
4. Confirm opening the battery shows the full passport list for that battery.
5. Search by battery serial number.
6. Confirm it behaves the same as Battery ID search.
7. Search by Passport ID.
8. Confirm it opens that exact passport.
9. Search by cluster name or ID, such as `demo-cluster` or `Demo API Cluster`.
10. Confirm it lists batteries associated with that cluster.
11. Log out and log in as `demo.admin@example.test`.
12. Search by cluster name or ID.

Expected result: cluster search works only for global admins. Non-global users get a clear message instead of cluster-wide results.

Covers point: 19.

## 15. API Token Access

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=api-token-management`.
3. Create or use a cluster-scoped read token for `demo-cluster`.
4. Open `/help`.
5. Use the API workbench with the cluster read token.
6. Read the sample Demo API battery.
7. Repeat with a global token.

Expected result: both the valid cluster token and global token can read the allowed battery. The cluster token should not return 403 for a battery in its allowed cluster.

Covers point: 20.

## 16. API Battery Model Updates

1. Open `/help`.
2. Use the API workbench with a read-write token.
3. Send a PATCH/update that changes the sample battery to an allowed Battery Model.
4. Confirm the Battery Model changes.
5. Confirm linked Battery Model fields are also updated from the Battery Family/Model baseline.
6. Confirm the battery requires a new passport when the update differs from the latest passport.
7. Try to PATCH a non-existing Battery Model value.

Expected result: allowed Battery Models update the full linked data; unknown models are rejected instead of partially writing invalid data.

Covers points: 21, 27, 29.

## 17. API Software Version Updates

1. Open `/help`.
2. Use the API workbench with a read-write token.
3. Send a software-version update for the sample battery using a version defined on that battery's current Battery Model.
4. Confirm software version changes.
5. Confirm release date and latest update date are copied automatically from the Battery Family/Model software data.
6. Confirm a new passport is required when the value differs from the latest passport.
7. Try a software version that is not defined for that Battery Model.

Expected result: software version can be changed through app/API only to defined values, and release/latest-update metadata follows automatically.

Covers points: 26, 27, 29.

## 18. Create, Validate, Sign, And Publish Token

1. Open `/help`.
2. Confirm the sign token is labeled **create, validate, sign and publish passport**.
3. Use the sample sign token preset in the API workbench.
4. Create a passport for a changed sample battery.
5. Validate it.
6. Sign it.
7. Publish it.

Expected result: read/write token is not enough for the full trust workflow; the create/validate/sign/publish token can complete it for its allowed cluster.

Covers point: 24.

## 19. Missing Passport Endpoint In API Help

1. Open `/help`.
2. Open the endpoint/template dropdown in the API workbench.
3. Confirm `/api/external/v1/batteries/{batteryId}/passports` appears.
4. Select it and run the sample request using the sample Battery ID.

Expected result: the endpoint is visible in the dropdown and returns the passports for the battery.

Covers point: 23.

## 20. Battery Family/Model Push To Batteries

1. Log in as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Open a Battery Family and Battery Model.
4. Change a shared parameter.
5. Save the Battery Model.
6. Push the saved Battery Model to matching batteries.
7. Confirm changed matching batteries are updated and require a new passport.
8. Push again without changing anything.

Expected result: the first push marks changed batteries as requiring new passports; the second no-op push does not.

Covers point: 28.

## 21. Local Admin Passport Workflow

1. Log in as `demo.admin@example.test`.
2. Edit an allowed field on a Demo API battery.
3. Save.
4. Confirm a new passport is required if the edit differs from the latest passport.
5. Create a new passport.
6. Validate it.
7. Sign it.
8. Publish it.

Expected result: local admins can complete the passport lifecycle for batteries in their managed clusters.

Covers point: 31.

## 22. Telemetry Template And Malformed Points

1. Open `/help`.
2. Select the telemetry write example.
3. Send one valid telemetry point for one variable.
4. Send multiple valid points for one variable.
5. Send multiple variables with multiple timestamped points.
6. Send a mixed payload with one malformed point and at least one valid point.
7. Open the detailed report Performance tab for the same battery.

Expected result: valid telemetry appears in sorted time order, malformed points are ignored or flagged by the API, and the Performance graph does not break.

Covers point: 25.

## 23. Telemetry Does Not Affect Passport Trust State

1. Pick a published clean passport.
2. Record its visible status.
3. Send telemetry from `/help` or the API.
4. Reopen the battery and passport status.
5. Check the admin list and conformance/status area.

Expected result: telemetry does not make the battery require a new passport, does not make the passport awaiting sign-off, and does not require validation, signing, or publishing.

Covers point: 32.

## 24. Responsive UI Smoke Test

Run this on desktop and a narrow browser width:

1. `/`
2. `/login`
3. `/registry`
4. `/admin/clusters?tab=batteries`
5. `/admin/clusters?tab=clusters`
6. `/admin/clusters?tab=users`
7. `/admin/clusters?tab=local-editable-fields`
8. `/cluster-admin/users`
9. `/help`
10. Public Summary report
11. Detailed report Performance tab

Expected result: text does not overlap, tables scroll where needed, copy buttons stay usable, select controls look consistent, and IDs wrap/truncate cleanly.

Covers points: 4, 5, 10, 13, 14.

## 25. Final Pass/Fail Notes

For each failed step, record:

- URL
- Logged-in user
- Battery ID or Passport ID
- What you expected
- What happened instead
- Screenshot if visual
- API request/response if API-related

This makes each bug report reproducible from the same seeded demo state.
