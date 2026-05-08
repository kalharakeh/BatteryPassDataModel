# QA Tester Pack

This is the starting point for someone testing the Battery Pass app without prior project context. Use it together with:

- `docs/end-user-testing-guide.md` for detailed reference.
- `docs/sample-cluster-test-accounts.md` for seeded accounts and passport IDs.
- `/help` inside the running app for external API examples and the request workbench.
- `/admin/help` inside the running app for the admin workflow.

## Quick start

1. Open the app link provided by the project owner in a browser.
2. Log in as the general admin:
   - Email: `admin@example.test`
   - Password: `Password123!`
3. Open `/admin/clusters?tab=products`.
4. Press **Reset product-template passports** to restore the known test data.
5. Open `/admin/clusters?tab=passports`.
6. Use `did:web:acme.battery.pass:sample-customer-north-001` as the main happy-path test passport.
7. Keep `/help`, `/admin/help`, and this file open while testing.

Expected baseline after reset:

- Four passport records exist.
- Product templates exist for Compact 7M, Compact 13M, and Core.
- Each product has software versions `1.0`, `2.0`, and `3.0`.
- The known passports are published, signed, clean, and QR-ready unless a test intentionally changes them.

## Tester accounts

Shared seeded password: `Password123!`

| Purpose | Account | What to verify |
| --- | --- | --- |
| General admin | `admin@example.test` | Full admin workflow, product templates, validation, signing, publishing, document evidence, API tokens, battery secrets. |
| North normal user | `north.user@example.test` | Can view North cluster battery details only. |
| North local admin | `north.admin@example.test` | Can use cluster admin tools for North cluster and local battery fields. |
| South normal user | `south.user@example.test` | Can view South cluster battery details only. |
| Fleet normal user | `fleet.user@example.test` | Can view Fleet cluster battery details only. |

Main battery IDs:

| Purpose | Passport ID |
| --- | --- |
| Main happy path | `did:web:acme.battery.pass:sample-customer-north-001` |
| Compact 13M comparison | `did:web:acme.battery.pass:sample-customer-south-001` |
| Core product comparison | `did:web:acme.battery.pass:sample-end-user-fleet-001` |
| API/testing preserved ID | `did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976` |

## Scenario matrix

| Area | Scenario | What should happen |
| --- | --- | --- |
| Public search | Search for a published passport from `/`. | The public summary opens. |
| Public unpublished search | Search for an unpublished or draft passport as a guest. | The public search should not expose it. |
| QR | Click/download the QR from the summary and scan/upload it from `/`. | It resolves back to the summary. |
| Summary page | Open `/{passportId}/summary`. | Shows battery facts, current software version, QR, charts, and no internal conformance/proof diagnostics. |
| Detailed report | Open `/{passportId}` while logged in with access. | Shows General, Software tab, Material, Performance, Compliance, Supply chain, Circularity, Carbon Footprint, and admin-only Trust tab when allowed. |
| Software tab | Patch software through `/help` workbench or cURL. | Summary and Software tab show the current installed software. Passport remains clean. |
| Product templates | Edit a template and push a software version to matching batteries. | Template-owned fields update, manual overrides are preserved, and changed signed core data becomes dirty. |
| Admin create | Create a new passport from `/admin/passports/new`. | Product and software are selected first; battery-specific fields stay editable/blank for admin entry. |
| Validate, sign, publish | Open conformance, validate, sign, publish. | Signing requires zero blockers; publishing requires a current valid proof. |
| Dirty recovery | Edit signed core data such as weight, save, then validate, sign, publish again. | Passport becomes dirty after edit and clean after re-signing/publishing. |
| Document evidence | Upload or replace required document evidence. | Hash is stored, evidence becomes part of validation/signature flow, changed evidence requires a new signature. |
| Access control | Try restricted document download as the wrong user or guest. | Access is denied with 403. Authorized admins/cluster users can access permitted files. |
| API auth | Call external API with read token, read-write token, bad token, and wrong cluster scope. | Correct responses: read succeeds, write requires read-write, bad token is 401, wrong scope is 403. |
| API telemetry | Post telemetry. | Detail charts update and passport trust state does not become dirty. |
| API software validation | Patch software to an unknown version. | API returns `400 Bad Request` with allowed versions. |

## Acceptance checklist

Mark each item pass/fail during a formal test run.

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Reset product-template passports restores exactly the four known passports. |  |  |
| Public search opens a published passport summary. |  |  |
| Public summary shows current software version and no trust/conformance diagnostics. |  |  |
| QR download/scan opens the same summary. |  |  |
| Detailed report Software tab shows product, product ID, version, release date, and latest update. |  |  |
| General admin can validate, sign, and publish a passport. |  |  |
| Editing signed core data makes the passport dirty. |  |  |
| Re-validating, signing, and publishing returns the passport to clean. |  |  |
| API telemetry update does not dirty the passport. |  |  |
| API software update to an allowed version does not dirty the passport. |  |  |
| API software update to an unknown version returns 400. |  |  |
| Product template push preserves manual overrides. |  |  |
| Required/optional fields are managed from product templates, not a global data requirements page. |  |  |
| Required document evidence stores a SHA-256 hash. |  |  |
| Replacing signed evidence requires validation/signing again. |  |  |
| Restricted document access is denied to unauthorized users. |  |  |
| North user cannot open South or Fleet detailed reports. |  |  |
| Local cluster admin can use cluster-scoped tools but not global admin tools. |  |  |

## Suggested test order

1. Reset data from `/admin/clusters?tab=products`.
2. Verify public summary, QR, and detailed report tabs.
3. Verify role-based access with normal users and local admins.
4. Verify admin trust workflow: validate, sign, publish.
5. Verify dirty recovery after a core edit.
6. Verify document evidence upload, hash, signature, and access control.
7. Verify API reads, telemetry writes, operations patch, and software patch.
8. Verify product template edit/push behavior.
9. File bugs using the template below.

## Known limitations

- This is still a demonstrator, not the final production deployment.
- Product templates and seeded passports are realistic enough for testing but are not official production master data.
- The reset action intentionally deletes older sample/demo passports and restores the four known test passports.
- External API writes are limited to telemetry, operations metadata, and current installed software version.
- API telemetry and API software updates are operational data and should not dirty the signed passport.
- Signing verifies the canonical passport core and referenced evidence hashes; it does not parse PDF contents.
- Camera QR scanning depends on browser/device permissions. Image upload scanning is the fallback.
- Some browser, OS, and camera permission failures are expected; manual DID search should still work.

## Bug report template

Use this format for every issue.

```text
Title:

Environment:
- App URL:
- Browser:
- Logged-in account:
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
