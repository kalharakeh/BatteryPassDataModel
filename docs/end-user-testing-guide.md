# Battery Pass Demonstrator - End User Testing Guide

Product Template is the internal implementation name for Battery Family. In the UI and tester workflows, Product Template and Battery Family mean the same stored template concept.

For the focused May 2026 passport admin, registry, editable-field, API, QR, and telemetry acceptance flow, use `docs/passport-admin-registry-manual-test-flow.md`.



## 1. What this app is and how it is built

- App type: ASP.NET Core MVC web app (`web/BatteryPassWeb.csproj`, target `net10.0`).
- Backend storage: MongoDB collections for batteries, passport snapshots, Battery families, Battery Models, Battery Model software parameters, users, clusters, memberships, API tokens, sign tokens, telemetry.
- Authentication: Cookie login (`/login`) with roles.
- Main code areas:
  - Controllers: `web/Controllers/*`
  - Data/services: `web/Services/*`
  - UI pages: `web/Views/*`
  - Static files/css: `web/wwwroot/*`

## 2. Web pages and structure

### 2.1 Global layout

- Shared shell: `web/Views/Shared/_Layout.cshtml`
- Header navigation is role-aware:
  - General admin: sees `Admin`
  - Cluster admin: sees `Cluster admin`
  - Authenticated users: see `Help`
  - Guests: see `Login`

### 2.2 Main routes

- Public/entry:
  - `/` home page
  - `/search?q=<batteryId, passportId, or text>`
  - `/{batteryId}` battery-level passport history
  - `/{batteryId}/latest` latest passport for a battery
  - `/{passportId}/summary` summary report for a specific passport snapshot
- Authenticated detail:
  - `/{passportId}` detailed report (requires cluster access)
- Authenticated registry:
  - `/registry` battery registry list/search (scope depends on role)
- Login/logout:
  - `/login`
  - `/login/logout` (POST)
- Admin:
  - `/admin` -> redirects to `/admin/clusters?tab=batteries`
  - `/admin/clusters?tab=batteries` Batteries dense table with generated Battery IDs, Battery Family, Battery Model, passport status, and inline actions.
  - `/admin/clusters?tab=battery` Battery cluster assignments dense table with one row per battery and assignment-only inline actions.
  - `/admin/clusters?tab=clusters` Registered clusters dense table.
  - `/admin/clusters?tab=users` Users dense table with separate Username and Email columns, cluster membership count, and inline user action drawer.
  - `/admin/clusters?tab=local-editable-fields` Local editable fields dense policy table. Sections are collapsed by default.
  - `/admin/clusters?tab=api-token-management` API Token Management dense console.
  - `/admin/clusters?tab=products` Battery families.
  - `/admin/products/{productId}` Battery family editor for shared product data, Battery Models, software parameters, and per-product-version required/optional parameters.
  - `/admin/help` admin workflow help dense reference console.
- Cluster admin:
  - `/cluster-admin/passports`
  - `/cluster-admin/users`
  - `/cluster-admin/secrets`
- Help:
  - `/help` (external API dense endpoint reference + request workbench)

## 3. Roles, permissions, and who can do what

## 3.1 General admin (global admin)

- Can access all admin pages (`/admin/*`).
- Can create, edit, publish/archive all passports.
- Can create and manage clusters.
- Can assign batteries to clusters from the Battery cluster assignments dense table.
- Can create users and assign multiple cluster memberships, with a separate role per membership.
- Can create/manage API tokens and sign tokens from API Token Management.
- Can set Local editable fields globally for local admins.
- Can open detailed report for any battery, including unassigned batteries.

## 3.2 Cluster admin (local admin)

- Can access `/cluster-admin/*`.
- Scope limited to clusters where membership role is `clusterAdmin`.
- Can edit local fields for batteries in managed clusters. The exact list is set globally by the general admin in **Local editable fields**:
  - Facility ID
  - Battery image URL
  - State of charge
  - Remaining capacity
  - Remaining energy
  - Full cycles
- Can manage users and memberships within managed clusters.
- Can manage sign tokens for batteries in managed clusters.
- Cannot modify global admin users from cluster user management.
- Cannot perform global admin operations (full passport editing, global cluster management, global token management).

## 3.3 Cluster user (normal user / member)

- Can access `/registry` and see only batteries linked to their cluster memberships.
- Can open detailed report only for batteries in clusters they are assigned to.
- Cannot access `/admin` or `/cluster-admin`.
- Can use summary pages; detail page access is checked against cluster membership.

## 4. Clusters, batteries, and user membership model

### 4.1 How batteries are grouped

- Each battery has `clusterId`. Passport snapshots inherit the battery cluster at creation time.
- Cluster assignment is managed by general admin in:
  - `/admin/clusters?tab=battery`
- The Battery cluster assignments page uses one row per battery. Battery ID, Battery Family, Battery Model, serial, and assignment state are separated into readable columns, and the only inline action is the assignment save/change control needed by that page.
- If `clusterId` is missing:
  - It is "unassigned"
  - Regular cluster users cannot open detailed report
  - Global admin still can
- `/registry` visibility:
  - General admin can search all batteries (including archived entries).
  - Non-admin users see only batteries from their assigned clusters.

### 4.2 Where clusters are managed

- Global cluster list and create/update/delete:
  - `/admin/clusters?tab=clusters`
- The Registered clusters page uses a compact dense table so cluster ID, display name, access status, linked batteries, linked users, and inline icon actions can be scanned without opening separate cards.

### 4.3 Where users are managed

- Global user management:
  - `/admin/clusters?tab=users`
- Local cluster-scoped user management:
  - `/cluster-admin/users`
- The global users page shows one row per user with separate Username and Email columns. The Cluster memberships column shows the membership count only.
- User profile, password change, global access, and multiple cluster memberships are handled from the inline action drawer. Password changes require typing the password twice and include a reveal icon. Account email is read-only in that drawer.

### 4.4 User data model

- `users` collection: `email`, `name`, `roles`, `passwordHash`.
- `clusterMemberships` collection: `email`, `clusterId`, `role` (`member` or `clusterAdmin`).
- Effective access:
  - System role `admin` gives global admin rights.
  - Membership role `clusterAdmin` gives local admin rights per cluster.
  - Membership role `member` gives read/detail access per cluster.
- A user can have multiple cluster memberships. Cluster membership roles can differ by cluster, so the users dense table does not show one global "System role" column for cluster access.

### 4.5 Seeded demo accounts and sample matrix

See `docs/sample-cluster-test-accounts.md` for provided test users and their linked batteries/clusters.

Battery family reset restores eight batteries and nine passport snapshots: one unassigned demonstrator, one Demo API battery with one historical passport and one latest passport, and six clustered customer/fleet batteries. The `/help` page shows the current generated Demo API Battery ID, latest Passport ID, and fixed QA tokens. The North customer test account is `customer_001_001@customer.org` with password `12345`.

### 4.6 Admin dense table layout checks

- Batteries, Battery cluster assignments, Registered clusters, Users, Local editable fields, API Token Management, `/help`, and `/admin/help` use the same compact dense table design language.
- The users table keeps the row compact by showing only the membership count. Open the inline row drawer to view, remove, or add multiple cluster memberships.
- Local editable fields sections are collapsed by default. Expand only the section being tested.
- API help and admin help detail controls open an inline row panel directly under the selected endpoint or workflow row, not at the bottom of the table.

## 5. What the Battery Pass contains and where to find it

Detailed report tabs are in `/{passportId}` and grouped into:

1. General
2. Software
3. Material composition
4. Performance
5. Compliance
6. Supply chain
7. Circularity
8. Carbon Footprint

The public summary shows the software version parameter in the battery fact grid. It still hides internal validation, conformance, proof, and trust diagnostics from public users.

### 5.1 General

- Display identity data:
  - Passport ID, model, serial, category, status, weight, manufactured date, manufacturer, facility ID, cluster label.
- Source paths (document level):
  - `app.display.*`
  - `aspects.generalProductInformation.payload.*`
  - `registryInfo.*`

### 5.2 Software

- Shows current software information:
  - Product name
  - Product ID
  - Battery Model
  - software version parameter
  - Release date
  - Latest update
- Source paths:
  - Battery Model: `app.product.productVersion`
  - Battery Family baseline: `app.product.softwareVersion`, `app.product.softwareReleaseDate`, `app.product.softwareLatestUpdate`
  - The software-version endpoint updates only software versions defined on the selected Battery Model and copies release date/latest update metadata from the Battery Family baseline.
  - Display rule: summary and General tab software parameters show the selected Battery Model values from the Battery Family baseline.

### 5.3 Material composition

- Pie chart + material table:
  - Nickel, Copper, Aluminium, Graphite, Manganese, Cobalt, Lithium, Electrolyte/separators.
- Source paths:
  - `aspects.materialComposition.payload.batteryMaterials`
  - optional chart cache in `app.charts.materialComposition`

### 5.4 Performance

- Static technical values:
  - Rated energy/capacity/power, nominal voltage, expected life/cycles.
- Dynamic battery condition values:
  - State of charge, remaining capacity, remaining energy, full cycles.
- Operations + live telemetry:
  - Active flag, location, contact person, latest telemetry.
- Telemetry history charts for last week.
- Source paths:
  - `aspects.performanceAndDurability.payload.*`
  - `app.operations.*`

### 5.5 Compliance

- Document links:
  - Conformity assessment
  - EU declaration of conformity
- Source paths:
  - `app.documents.*`
  - `aspects.labeling.payload.*`

### 5.6 Supply chain

- Supply chain index and document links:
  - Sustainability report
  - Due diligence report
  - Third-party audit
  - Taxonomy report
- Source paths:
  - `aspects.supplyChainDueDiligence.payload.*`
  - `app.documents.*`

### 5.7 Circularity

- Text fields:
  - Separate collection
  - Waste prevention
  - Recycled content share verification state
- Recycled-content donuts by material (pre/post/primary shares).
- Source paths:
  - `aspects.circularity.payload.*`
  - `app.notes.circularity.*`
  - optional chart cache in `app.charts.recycledContent`

### 5.8 Carbon footprint

- Verified carbon amount and performance class.
- Lifecycle stage split:
  - Raw material extraction, main production, distribution, recycling.
- CO2 study reference link.
- Source paths:
  - `aspects.carbonFootprintForBatteries.payload.*`
  - `app.documents.co2StudyReference`
  - optional chart cache in `app.charts.carbonFootprint`

## 6. Edit scope by role (important for testing)

### 6.1 General admin full edit flow

- Battery edit route: `/admin/batteries/{batteryId}/edit`
- Can edit all major sections:
  - General, material composition, performance, compliance, supply chain, circularity, carbon footprint
- Can create new batteries from the batteries admin page.
- Can create a new passport snapshot for an existing battery after battery data changes. Existing passport snapshots are not mutated.

### 6.2 Cluster admin local edit form

- Route: `/cluster-admin/passports/{passportId}/edit`
- Can edit only local operational fields listed in section 3.2.

## 7. External API HTTP requests (detailed)

Base path:

- `/api/external/v1`

### 7.1 Authentication model

Each request is checked in this order:

1. API token in `Authorization` header
2. Cluster scope check (token scope vs battery cluster)
3. Sign token check (only if active for that battery)

Headers:

- `Authorization: Basic <base64(token:)>`
- Compatibility mode also accepted: `Authorization: Basic <token>`
- Required for validate/sign: `Authorization: Basic <sign-token>`

### 7.2 API tokens ("api keys")

Managed by:

- General admin in `/admin/clusters?tab=api-token-management` under **API Token Management**

Token properties:

- `accessMode`: `read` or `readWrite`
- `clusterIds`: linked cluster scope
- `globalAccess`: all clusters
- `allowUnassigned`: permits unassigned batteries
- `isActive`

Important:

- The app stores hash/encrypted values.
- The plain token value is shown only at create/regenerate time.
- Passing token ID instead of token value is rejected.
- The Token ID column is a copyable record identifier for admins. It is not the secret credential used in `Authorization`.
- API calls must use the Token Value shown at creation/regeneration time or one of the fixed QA Token Values from `/help`.
- The token table keeps Token, Token ID, Permission, Scope, Status, Last used, and inline action buttons in separate columns.
- The Scope column shows a scope count. Opening the scope details lists cluster names only; cluster IDs are not repeated in the table display.
- The create-token linked cluster selector shows cluster names only. Cluster IDs remain part of stored token scope data for admin/audit behavior.
- Use the cleanup action in API Token Management to remove generated unused demo tokens when the table becomes noisy.

### 7.3 sign tokens

Managed by:

- General admin: `/admin/clusters?tab=api-token-management` under **API Token Management**
- Cluster admin (own clusters): `/cluster-admin/secrets`

Behavior:

- Secret is linked to cluster-scoped trust actions for passport IDs.
- Sign tokens are required for `/validate` and `/sign`; read/write tokens cannot sign.
- If no secret exists or secret inactive, secret header is not required.

### 7.4 What can be done

Read:

- `GET /batteries/{batteryId}`
- `GET /batteries/{batteryId}/section/{sectionName}`
- `GET /batteries/{batteryId}/values?path=...`
- `GET /batteries/{batteryId}/paths?section=...&includeContainers=true|false`
- `GET /batteries/{batteryId}/telemetry/history?hours=1..168`

Write (requires `readWrite` token):

- `POST /batteries/{batteryId}/telemetry`
- `PATCH /batteries/{batteryId}/operations`
- `PATCH /batteries/{batteryId}/battery-model`

Trust actions (requires sign token):

- `POST /batteries/{batteryId}/passports`
- `POST /passports/{passportId}/validate`
- `POST /passports/{passportId}/sign`
- `POST /passports/{passportId}/publish`

Software update behavior:

- Body: `{ "batteryModel": "2.0" }`
- The requested model must exist in the MongoDB Battery Model list for the battery's current Battery Family.
- If the model is not found, the API returns `400 Bad Request` and lists the allowed models.
- The API writes the selected Battery Model and its software parameters to the battery record, then reports that a new passport snapshot, validation, signing, and publishing are required.
- The Battery Family baseline stays under `app.product.*` and remains part of the signed passport core.
- The public summary and detailed General tab show the software version parameter, release date, and latest update from `app.product.*`.

### 7.5 What cannot be done (external API)

- Cannot create new batteries.
- Cannot delete passports.
- Can create a new passport snapshot only for an existing battery with a valid sign token.
- Cannot edit full passport sections/aspects.
- Cannot write telemetry with read-only token.
- Cannot access batteries outside token cluster scope.
- Cannot bypass active Sign token.
- Cannot query telemetry history with `hours` outside `1..168`.
- Cannot set a Battery Model that is not defined for the battery Battery Family.

### 7.6 Sections and value aliases

`section` names:

- `general`
- `materialComposition`
- `performance`
- `circularity`
- `supplyChain`
- `carbonFootprint`
- `compliance`
- `operations`
- `display`
- `full`

Useful aliases for `/values`:

- `ratedEnergy`
- `ratedCapacity`
- `ratedMaximumPower`
- `nominalVoltage`
- `nickelMass`
- `currentConsumptionKwh`
- `currentChargeLevelPct`
- `currentVoltageV`
- `currentCurrentA`
- `softwareVersion`
- `softwareReleaseDate`
- `softwareLatestUpdate`
- `locationOfUse`
- `contactPerson`
- `isActive`

## 8. External API examples

After reset, open `/help` and copy the current generated sample Battery ID and latest Passport ID. The fixed demo tokens are:

- Read token: `SAMPLEBATTERYPASSPORTREADTOKN001`
- Read-write token: `SAMPLEBATTERYPASSPORTWRITETOK001`
- Passport Lifecycle token: `SAMPLEBATTERYPASSPORTSIGNTOK001`

The `/help` API help page uses a dense endpoint table. Open an endpoint detail with the row control; the detail panel appears inline under the row being inspected while the request workbench stays available for live calls.

The examples below use shell variables so the generated IDs can change safely after reset.

```bash
sampleBatteryId="<copy the Sample Battery ID from /help>"
samplePassportId="<copy the Sample latest Passport ID from /help>"
readToken="SAMPLEBATTERYPASSPORTREADTOKN001"
writeToken="SAMPLEBATTERYPASSPORTWRITETOK001"
signToken="SAMPLEBATTERYPASSPORTSIGNTOK001"
```

### 8.1 cURL - read full battery

```bash
curl -X GET "http://localhost:5186/api/external/v1/batteries/$sampleBatteryId" \
  -H "Authorization: Basic $readToken"
```

### 8.2 cURL - write telemetry

```bash
curl -X POST "http://localhost:5186/api/external/v1/batteries/$sampleBatteryId/telemetry" \
  -H "Authorization: Basic $writeToken" \
  -H "Content-Type: application/json" \
  -d "{\"points\":[{\"currentConsumptionKwh\":154.6,\"currentChargeLevelPct\":82.1,\"currentVoltageV\":401.7,\"currentCurrentA\":49.2}]}"
```

### 8.3 cURL - patch operations

```bash
curl -X PATCH "http://localhost:5186/api/external/v1/batteries/$sampleBatteryId/operations" \
  -H "Authorization: Basic $writeToken" \
  -H "Content-Type: application/json" \
  -d "{\"isActive\":true,\"locationOfUse\":{\"siteName\":\"Factory 4\",\"city\":\"Berlin\",\"country\":\"DE\"},\"contactPerson\":{\"name\":\"Anna Becker\",\"email\":\"anna@example.test\"}}"
```

### 8.4 cURL - patch software version parameter

```bash
curl -X PATCH "http://localhost:5186/api/external/v1/batteries/$sampleBatteryId/battery-model" \
  -H "Authorization: Basic $writeToken" \
  -H "Content-Type: application/json" \
  -d "{\"batteryModel\":\"2.0\"}"
```

### 8.5 cURL - create, validate, sign, and publish a new passport

```bash
curl -X POST "http://localhost:5186/api/external/v1/batteries/$sampleBatteryId/passports" \
  -H "Authorization: Basic $signToken"

curl -X POST "http://localhost:5186/api/external/v1/passports/$samplePassportId/validate" \
  -H "Authorization: Basic $signToken"

curl -X POST "http://localhost:5186/api/external/v1/passports/$samplePassportId/sign" \
  -H "Authorization: Basic $signToken"

curl -X POST "http://localhost:5186/api/external/v1/passports/$samplePassportId/publish" \
  -H "Authorization: Basic $signToken"
```

### 8.6 JavaScript (fetch)

```js
const token = "YOUR_TOKEN_VALUE";
const sampleBatteryId = "COPY_SAMPLE_BATTERY_ID_FROM_HELP";
const auth = "Basic " + btoa(token + ":");

const response = await fetch(
  `http://localhost:5186/api/external/v1/batteries/${sampleBatteryId}/values?path=ratedEnergy&path=nickelMass`,
  {
    method: "GET",
    headers: {
      Authorization: auth,
      Accept: "application/json",
    },
  }
);

const data = await response.json();
console.log(data);
```

### 8.7 Python (requests)

```python
import base64
import requests

token = "YOUR_TOKEN_VALUE"
sample_battery_id = "COPY_SAMPLE_BATTERY_ID_FROM_HELP"
auth = "Basic " + base64.b64encode(f"{token}:".encode("utf-8")).decode("utf-8")

url = f"http://localhost:5186/api/external/v1/batteries/{sample_battery_id}/telemetry/history?hours=24"
resp = requests.get(url, headers={"Authorization": auth, "Accept": "application/json"}, timeout=30)
print(resp.status_code)
print(resp.json())
```

## 9. Internal APIs (for app/admin use)

- `POST /api/auth/login`, `POST /api/auth/logout`
- `GET/POST/PUT/DELETE /api/passports` (admin write required)
- `POST /api/files` (cluster admin or admin), `GET /api/files/{fileId}`

These are not the token-based external integration API; they use app login/cookie authorization.

## 10. Practical test checklist

1. Log in as global admin and verify all admin dense table tabs, including Batteries, Battery cluster assignments, Registered clusters, Users, Local editable fields collapsed by default, API Token Management, Battery families, and Help.
2. Create a new cluster, assign one battery from `/admin/clusters?tab=battery`, assign one local admin, and assign one normal user with multiple cluster memberships.
3. Log in as local admin and verify cluster-scoped limits, including that global admin users cannot be modified.
4. Log in as normal user and verify `/registry` shows only own cluster batteries and detail access works only for own cluster battery.
5. Open API Token Management, create read/write and Sign tokens, confirm Token ID and Token Value are distinct, confirm the scope count opens cluster-name details, then call external API with the scoped Token Value.
6. Verify read token cannot write.
7. Verify out-of-scope token gets `403`.
8. Verify telemetry write appears in detail history charts.
9. Patch Battery Model through `/api/external/v1/batteries/{batteryId}/battery-model` and confirm the response says a new passport, validation, signing, and publishing are required.
10. Open `/help` and `/admin/help`, expand a detail row, and confirm the inline row panel opens directly under the selected row.
11. Open the detailed report and confirm the General tab software parameters shows product, product ID, release date, and latest update.

## 11. Trust workflow hardening checklist

Use this checklist after Section 5 changes to verify the demonstrator is ready for a clean end-to-end demo.

### 11.1 Happy path: validation -> sign -> publish -> QR scan

1. Log in as a global admin.
2. Open `/admin/passports/{passportId}/edit` and save complete required passport data.
3. Open `/admin/passports/{passportId}/conformance`.
4. Click `Validate passport` and confirm blocking errors are zero.
5. Click `Sign passport` and confirm an immutable revision appears in revision history.
6. Click `Publish passport` and confirm the passport is publicly searchable.
7. Download or display the passport QR code.
8. Open `/`, click the QR icon in the main search field, upload or scan the QR code, and confirm the public summary opens.

### 11.2 Dirty recovery path: dirty -> validate -> sign -> publish

1. Edit a signed passport canonical field in the admin form.
2. Confirm the conformance page reports the passport as dirty or not currently publishable.
3. Click `Validate passport`.
4. Resolve any blocking errors shown by section and field path.
5. Click `Sign passport` to create a new revision and proof hash.
6. Click `Publish passport` again so the published state points to the latest signed revision.
7. Send telemetry through the external API and confirm the passport does not become dirty. Change Battery Model through the external API and confirm a new passport plus validation/signing is required.

### 11.3 Failure and access checks

1. MongoDB/service failure: temporarily use an invalid MongoDB URI in a local test environment and try to sign or publish. The UI/API should show a service error, and it must not claim a successful signed or published trust state.
2. Signature mismatch: edit signed canonical data after signing and confirm verification shows an invalid/dirty state with hash diagnostics, without exposing private-key details.
3. Validation failure: remove a required field, validate, and confirm draft edits remain saved while signing stays unavailable.
4. Restricted file access: upload a private document, try to download it without the right login/cluster access, and confirm the restricted document download returns 403 and writes an audit event.
5. QR scan fallback: test camera denial, unsupported native barcode detection, and SVG upload. Manual DID search should remain available.

### 11.4 Phase 5A guided readiness checklist

Use this checklist after the guided readiness polish to confirm the conformance page tells admins exactly what to do next.

1. Open an incomplete draft passport in `/admin/passports/{passportId}/conformance`.
   - Expected next action: Complete required data.
   - Expected state: Incomplete data.
2. Complete required data or manually fill the blocking fields, then validate.
   - Expected next action: Sign passport.
   - Expected state: Ready to sign.
3. Sign the passport.
   - Expected next action: Publish passport.
   - Expected state: Ready to publish.
4. Publish the passport.
   - Expected state: Published and trusted.
   - Expected public behavior: the summary is searchable and still does not show internal conformance diagnostics.
   - Expected software behavior: the public summary shows the software version parameter and the detailed report has a General tab software parameters.
5. Edit a signed core field such as battery mass and save.
   - Expected next action: Sign passport after validation succeeds.
   - Expected state: Dirty: re-sign required.
6. Confirm warnings and proof/hash diagnostics are available under advanced diagnostics, not as the first thing an admin must parse.

### Battery Family baseline checklist

Use this checklist after the Battery Family migration to prove the app can restore and operate from production-like shared product baselines.

1. Login as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Confirm three Battery families are listed: Compact 7M, Compact 13M, and Core.
4. Open each Battery family and confirm Battery Models are listed newest-first and each model has software parameters.
5. Confirm required and optional parameters are shown section by section and saved per Battery Model in MongoDB.
6. Press **Reset battery-family passports** from the Battery families tab or admin help.
7. Confirm eight batteries and nine passport snapshots are restored:
   - one unassigned Compact 7M demonstrator
   - one Demo API Compact 7M battery in `demo-cluster` with two passports, one historical and one latest
   - two North Operations Compact 7M batteries
   - two South Operations Compact 13M batteries
   - two Fleet Operations Core batteries
8. Open `/help` and confirm it shows the generated Demo API Battery ID, the latest Demo API Passport ID, and the fixed read, read-write, and sign tokens.
9. Create a battery from the batteries admin page and confirm the Battery ID is generated from Battery Family and serial number; Passport ID is not entered during battery creation.
10. Create a passport for that battery and confirm the Passport ID is generated from Battery ID, Battery Model, and timestamp.
11. Edit a Battery Model value, save it, then push that saved Battery Model to matching batteries. Software metadata is part of the saved Battery Model; push the Battery Model when shared parameters change.
12. Confirm matching batteries preserve manual overrides, and changed signed battery data requires a new passport snapshot followed by validate, sign, and publish to expose the updated snapshot.

### Phase 6A end-to-end demo hardening checklist

Use this checklist after Phase 6A changes to prove the demo can be restored and verified from a known state.

1. Login as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Press **Reset battery-family passports**.
4. Confirm the success message says the Battery family passports were reset.
5. Open `/help` and copy the generated sample Battery ID and sample latest Passport ID.
6. Open `/admin/passports/{sampleLatestPassportId}/conformance`.
7. Expected state: Published, signed, clean, public, QR-ready.
8. Logout or use a public browser session.
9. Search for the generated sample Battery ID from `/`.
10. Expected result: the battery-level passport history opens and shows one historical passport and one latest passport.
11. Open `/{sampleBatteryId}/latest`.
12. Expected result: the public summary for the latest passport opens.
13. Search for `did:web:acme.battery.pass:sample-customer-north-001` from `/`.
14. Expected result: the compatibility alias resolves to the generated Demo API battery/latest passport.
15. Download or click the QR code from the summary page.
16. Expected result: the QR resolves back to the latest passport for the same Battery ID.
17. Login again as `admin@example.test`.
18. Edit the Demo API battery, change one signed core field such as weight, and save.
19. Expected result: the battery list shows that a new passport should be created.
20. Create a new passport snapshot for that battery, then validate, sign, and publish it.
21. Expected result: the new passport becomes latest, and the old latest passport becomes historical.
22. Use the external API workbench or curl to send a telemetry update to the same Battery ID.
23. Expected result: External HTTP telemetry update does not dirty the passport.
24. Use the external API workbench or curl to patch Battery Model to an allowed model such as `2.0`.
25. Expected result: the API says a new passport snapshot, validation, signing, and publishing are required.
26. Try restricted document access on a private evidence file.
27. Expected result: Restricted document download returns 403 for unauthorized users and remains accessible only to authorized admin/cluster users when a linked file exists.

### Phase 6B Evidence Readiness Check

Use an admin account and a passport from the demo catalog.

1. Open `/admin/clusters?tab=batteries`.
2. Open the battery row action/history, then open the passport edit page for a demo passport.
3. Upload or replace one required supporting document.
4. Open the passport conformance page.
5. Confirm the Evidence readiness panel shows the file as uploaded unsigned or Changed since signing.
6. Validate the passport.
7. Sign the passport again.
8. Confirm the Evidence readiness panel shows the document as verified in the latest signed revision.
9. Publish when the readiness page shows the publish action.
10. Try a restricted document download from a public browser session and confirm it is denied.

Expected result: document hashes are visible to admins, changed evidence requires a new signature, and restricted files remain access-controlled.
