# QA Tester Pack

Product Template is the internal implementation name for Battery Family. In the UI and tester workflows, Product Template and Battery Family mean the same stored template concept.

This is the starting point for someone testing the Battery Pass app without prior project context. Use it together with:

- `docs/end-user-testing-guide.md` for detailed reference.
- `docs/sample-cluster-test-accounts.md` for seeded accounts and battery/passport access.
- `/help` inside the running app for the current generated API demo Battery ID, latest Passport ID, fixed tokens, examples, and request workbench.
- `/admin/help` inside the running app for the admin workflow dense reference console.

## Quick start

1. Open the app link provided by the project owner in a browser.
2. Log in as the general admin:
   - Email: `admin@example.test`
   - Password: `Password123!`
3. Open `/admin/clusters?tab=products`.
4. Press **Reset battery-family passports** to restore the known test data.
5. Open `/help` and copy the sample Battery ID and sample latest Passport ID.
6. Use that generated sample Battery ID as the main happy-path API and search test battery.
7. Legacy compatibility check: searching `did:web:acme.battery.pass:sample-customer-north-001` should resolve to the generated Demo API battery.
8. Keep `/help`, `/admin/help`, and this file open while testing.

Expected baseline after reset:

- Eight batteries and nine passport snapshots exist: one unassigned demonstrator, one Demo API battery with two passports, and six clustered customer/fleet batteries.
- The Demo API battery is linked to `demo-cluster`, has one historical passport and one latest passport, and is the sample used by `/help`.
- Battery families exist for Compact 7M, Compact 13M, and Core.
- Each Battery Family has Battery Models, and each Battery Model has its own software parameters.
- Published sample passports are signed, clean, and QR-ready unless a test intentionally changes them.

## Tester accounts

Shared seeded password: `Password123!`

| Purpose | Account | What to verify |
| --- | --- | --- |
| General admin | `admin@example.test` | Full admin workflow, Battery families, validation, signing, publishing, document evidence, API tokens, sign tokens. |
| Demo API user | `demo.user@example.test` | Can view the Demo API battery in registry and detailed report. |
| Demo API local admin | `demo.admin@example.test` | Can use cluster admin tools for `demo-cluster` and local battery fields. |
| North customer | `customer_001_001@customer.org` / `12345` | Can open North Operations Cluster batteries such as CP7M-NORTH-001. |
| North normal user | `north.user@example.test` | Can view North cluster battery details only. |
| North local admin | `north.admin@example.test` | Can use cluster admin tools for North cluster and local battery fields. |
| South normal user | `south.user@example.test` | Can view South cluster battery details only. |
| Fleet normal user | `fleet.user@example.test` | Can view Fleet cluster battery details only. |

Main battery IDs:

| Purpose | How to get it |
| --- | --- |
| Main happy path | Copy the sample Battery ID from `/help` after reset. |
| Latest passport for API signing/publishing tests | Copy the sample latest Passport ID from `/help` after reset. |
| Historical passport check | Open the Demo API battery history from `/admin/clusters?tab=batteries`; it should show one historical passport and one latest passport. |
| Legacy alias check | Search `did:web:acme.battery.pass:sample-customer-north-001`; it should resolve to the generated Demo API battery. |
| North, South, Fleet access checks | Copy generated Battery IDs from `/admin/clusters?tab=batteries` and use the cluster/user mapping in `docs/sample-cluster-test-accounts.md`. |

## Scenario matrix

| Area | Scenario | What should happen |
| --- | --- | --- |
| Public search | Search for a published Passport ID from `/`. | The public summary opens. |
| Battery ID search | Search for a generated Battery ID from `/`. | The battery-level history page opens with linked passports and actions. |
| Latest route | Open `/{batteryId}/latest`. | The latest passport summary opens. |
| Legacy sample search | Search `did:web:acme.battery.pass:sample-customer-north-001`. | It resolves to the generated Demo API battery/latest passport. |
| Public unpublished search | Search for an unpublished or draft passport as a guest. | The public search should not expose it. |
| QR | Click/download the QR from the summary and scan/upload it from `/`. | It resolves to the latest passport for the Battery ID. |
| Summary page | Open `/{passportId}/summary`. | Shows Battery ID, Passport ID, battery facts, software version parameter, QR, charts, and no internal conformance/proof diagnostics. |
| Detailed report | Open `/{passportId}` while logged in with access. | Shows General, General tab software parameters, Material, Performance, Compliance, Supply chain, Circularity, Carbon Footprint, and admin-only Trust tab when allowed. |
| General tab software parameters | Patch Battery Model through `/help` workbench or cURL. | API response says a new passport, validation, signing, and publishing are required; after that workflow, Summary and General tab show the Battery Model software parameters. |
| Battery families | Edit a Battery Model, save it, then push that saved Battery Model to matching batteries. Software metadata is part of the saved Battery Model. | Template-owned fields update, manual overrides are preserved, and changed signed core data requires a new signed passport snapshot. |
| Admin create battery | Create a new battery from the batteries admin page. | Battery ID is generated from Battery Family and serial number; Passport ID is not entered during battery creation. |
| Admin create passport | Create a passport for an existing battery. | A new snapshot Passport ID is generated from Battery ID, Battery Model, and timestamp. |
| Admin dense tables | Open `/admin/clusters?tab=batteries`, `/admin/clusters?tab=battery`, `/admin/clusters?tab=clusters`, `/admin/clusters?tab=users`, `/admin/clusters?tab=local-editable-fields`, and `/admin/clusters?tab=api-token-management`. | Pages use compact dense table layouts, separated columns, and inline icon actions. Local editable field sections are collapsed by default. |
| Battery cluster assignments | Open `/admin/clusters?tab=battery` and change one battery cluster assignment. | One row per battery is shown, Battery ID and Battery Family are separate columns, and only assignment controls appear inline. |
| User multi-cluster memberships | Open `/admin/clusters?tab=users`, expand a user action drawer, and add a second cluster membership with a different role. | The table row still shows one user, Username and Email are separate, Cluster memberships shows the membership count, and the drawer handles multiple cluster memberships plus password confirmation/reveal. |
| API Token Management | Open `/admin/clusters?tab=api-token-management`. | API tokens and sign tokens are managed on one dense page. Token ID has its own copy column, Token Value is still the secret used for API calls, Scope shows a scope count with cluster-name details, and unused generated tokens can be cleaned up. |
| API help and admin help | Open `/help` and `/admin/help`, then expand a row detail. | The detail panel opens as an inline row directly under the selected endpoint or workflow row, not at the bottom of the table. |
| Validate, sign, publish | Open conformance, validate, sign, publish. | Signing requires zero blockers; publishing requires a current valid proof. |
| Dirty recovery | Edit signed battery data such as weight, save, then create a new passport, validate, sign, and publish again. | Old passport remains historical; the new latest passport exposes the changed battery snapshot. |
| Document evidence | Upload or replace required document evidence. | Hash is stored, evidence becomes part of validation/signature flow, changed evidence requires a new signature. |
| Access control | Try restricted document download as the wrong user or guest. | Access is denied with 403. Authorized admins/cluster users can access permitted files. |
| API auth | Call external API with read token, read-write token, bad token, and wrong cluster scope. | Correct responses: read succeeds, write requires read-write, bad token is 401, wrong scope is 403. |
| API telemetry | Post telemetry to a Battery ID. | Latest passport detail charts update and passport trust state does not become dirty. |
| API model validation | Patch Battery Model to an unknown model. | API returns `400 Bad Request` with allowed models. |
| API passport creation | POST `/api/external/v1/batteries/{batteryId}/passports` with the fixed sign token. | Response returns `201 Created` and the new Passport ID. |

## Acceptance checklist

Mark each item pass/fail during a formal test run.

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Reset battery-family passports restores eight batteries and nine passport snapshots. |  |  |
| `/help` shows the generated Demo API Battery ID, latest Passport ID, and fixed demo tokens. |  |  |
| Public search opens a published passport summary. |  |  |
| Battery ID search opens the battery-level passport history page. |  |  |
| Public summary shows linked Battery ID, software version parameter, and no trust/conformance diagnostics. |  |  |
| QR download/scan opens the latest passport for the same Battery ID. |  |  |
| Detailed report General tab software parameters shows product, product ID, version, release date, and latest update. |  |  |
| General admin can validate, sign, and publish a passport. |  |  |
| Editing signed battery data requires a new passport snapshot. |  |  |
| Re-validating, signing, and publishing returns the latest passport to clean. |  |  |
| API telemetry update does not dirty the passport. |  |  |
| Battery Model API change to an allowed model requires a new passport, validation, signing, and publishing. |  |  |
| Battery Model API change to an unknown model returns 400. |  |  |
| Battery family push preserves manual overrides. |  |  |
| Required/optional fields are managed from Battery families, not a global data requirements page. |  |  |
| Admin dense table pages use separated columns, inline icon actions, and no card-heavy layout. |  |  |
| Battery cluster assignments page shows one row per battery with assignment-only controls. |  |  |
| Users page shows Username and Email separately, a membership count, and supports multiple cluster memberships from the inline drawer. |  |  |
| User password change requires confirmation and includes a reveal icon. |  |  |
| Local editable fields sections are collapsed by default. |  |  |
| API Token Management has a separate Token ID copy column and Scope count/details without cluster IDs in the visible table scope text. |  |  |
| `/help` and `/admin/help` detail controls open inline row panels under the selected row. |  |  |
| Required document evidence stores a SHA-256 hash. |  |  |
| Replacing signed evidence requires validation/signing again. |  |  |
| Restricted document access is denied to unauthorized users. |  |  |
| North user cannot open South, Fleet, Demo API, or unassigned detailed reports. |  |  |
| Local cluster admin can use cluster-scoped tools but not global admin tools. |  |  |

## Suggested test order

1. Reset data from `/admin/clusters?tab=products`.
2. Verify `/help` sample IDs, fixed tokens, API request workbench, and Demo API battery history.
3. Verify admin dense tables, user multi-cluster membership behavior, API Token Management scope display, and inline row help details.
4. Verify public summary, QR, Battery ID search, Passport ID search, and detailed report tabs.
5. Verify role-based access with normal users and local admins.
6. Verify admin trust workflow: validate, sign, publish.
7. Verify dirty recovery after a battery data edit and new passport snapshot.
8. Verify document evidence upload, hash, signature, and access control.
9. Verify API reads, telemetry writes, operations patch, passport creation, and battery-model patch.
10. Verify Battery family edit/push behavior.
11. File bugs using the template below.

## Known limitations

- This is still a demonstrator, not the final production deployment.
- Battery families and seeded batteries are realistic enough for testing but are not official production master data.
- The reset action intentionally deletes older sample/demo data and restores eight batteries with nine passport snapshots.
- External API writes are limited to telemetry, operations metadata, Battery Model changes, and passport snapshot creation for an existing battery.
- API telemetry does not require signing; Battery Model API changes require a new passport, validation, signing, and publishing.
- Signing verifies the canonical passport core and referenced evidence hashes; it does not parse PDF contents.
- Camera QR scanning depends on browser/device permissions. Image upload scanning is the fallback.
- Some browser, OS, and camera permission failures are expected; manual ID search should still work.

## Bug report template

Use this format for every issue.

```text
Title:

Environment:
- App URL:
- Browser:
- Logged-in account:
- Battery ID:
- Passport ID:
- Time/date:

Steps to reproduce:
1.
2.
3.

Expected result:

Actual result:

Screenshots or files:

Console/network errors:

Does it reproduce after reset? Yes/No

Severity:
- Blocker / Major / Minor / Polish

Notes or improvement suggestion:
```

## Improvement suggestion template

```text
Area:

Current experience:

Suggested improvement:

Why it helps:

Risk or tradeoff:
```
