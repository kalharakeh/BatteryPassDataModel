# Battery Pass Demonstrator - End User Testing Guide



## 1. What this app is and how it is built

- App type: ASP.NET Core MVC web app (`web/BatteryPassWeb.csproj`, target `net10.0`).
- Backend storage: MongoDB collections for passports, product templates, product/battery versions, product-version software versions, users, clusters, memberships, API tokens, battery secrets, telemetry.
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
  - `/search?q=<passportId or text>`
  - `/{passportId}/summary` summary report
- Authenticated detail:
  - `/{passportId}` detailed report (requires cluster access)
- Authenticated registry:
  - `/registry` battery registry list/search (scope depends on role)
- Login/logout:
  - `/login`
  - `/login/logout` (POST)
- Admin:
  - `/admin` -> redirects to `/admin/clusters?tab=passports`
  - `/admin/clusters?tab=passports|battery|clusters|users|local-editable-fields|api-token-management|products`
  - `/admin/products/{productId}` product template editor for shared product data, product/battery versions, nested software versions, and per-product-version required/optional parameters.
- Cluster admin:
  - `/cluster-admin/passports`
  - `/cluster-admin/users`
  - `/cluster-admin/secrets`
- Help:
  - `/help` (external API docs + request workbench)

## 3. Roles, permissions, and who can do what

## 3.1 General admin (global admin)

- Can access all admin pages (`/admin/*`).
- Can create, edit, publish/archive all passports.
- Can create and manage clusters.
- Can assign batteries to clusters.
- Can create users and assign memberships.
- Can create/manage API tokens and battery secrets from API Token Management.
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
- Can manage battery secrets for batteries in managed clusters.
- Cannot modify global admin users from cluster user management.
- Cannot perform global admin operations (full passport editing, global cluster management, global token management).

## 3.3 Cluster user (normal user / member)

- Can access `/registry` and see only batteries linked to their cluster memberships.
- Can open detailed report only for batteries in clusters they are assigned to.
- Cannot access `/admin` or `/cluster-admin`.
- Can use summary pages; detail page access is checked against cluster membership.

## 4. Clusters, batteries, and user membership model

### 4.1 How batteries are grouped

- Each battery passport can have `clusterId`.
- Cluster assignment is managed by general admin in:
  - `/admin/clusters?tab=battery`
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

### 4.3 Where users are managed

- Global user management:
  - `/admin/clusters?tab=users`
- Local cluster-scoped user management:
  - `/cluster-admin/users`

### 4.4 User data model

- `users` collection: `email`, `name`, `roles`, `passwordHash`.
- `clusterMemberships` collection: `email`, `clusterId`, `role` (`member` or `clusterAdmin`).
- Effective access:
  - System role `admin` gives global admin rights.
  - Membership role `clusterAdmin` gives local admin rights per cluster.
  - Membership role `member` gives read/detail access per cluster.

### 4.5 Seeded demo accounts and sample matrix

See `docs/sample-cluster-test-accounts.md` for provided test users and their linked batteries/clusters.

Product template reset restores one unassigned demonstrator plus six clustered customer batteries. The North customer test account is `customer_001_001@customer.org` with password `12345`.

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

The public summary shows the current software version in the battery fact grid. It still hides internal validation, conformance, proof, and trust diagnostics from public users.

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
  - Product/battery version
  - Current software version
  - Release date
  - Latest update
- Source paths:
  - Product/battery version: `app.product.productVersion`
  - Product-template baseline: `app.product.softwareVersion`, `app.product.softwareReleaseDate`, `app.product.softwareLatestUpdate`
  - Current installed software from API updates: `app.operations.softwareVersion`, `app.operations.softwareReleaseDate`, `app.operations.softwareLatestUpdate`
- Display rule:
  - If current operational software exists, the summary and Software tab show it.
  - Otherwise they fall back to the selected product-template baseline.

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

### 6.1 General admin full edit form

- Route: `/admin/passports/{passportId}/edit`
- Can edit all major sections:
  - General, material composition, performance, compliance, supply chain, circularity, carbon footprint
- Can create new passport at `/admin/passports/new`

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
3. Battery secret check (only if active for that battery)

Headers:

- `Authorization: Basic <base64(token:)>`
- Compatibility mode also accepted: `Authorization: Basic <token>`
- Optional when active: `X-Battery-Secret: <secret>`

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

### 7.3 Battery secrets

Managed by:

- General admin: `/admin/clusters?tab=api-token-management` under **API Token Management**
- Cluster admin (own clusters): `/cluster-admin/secrets`

Behavior:

- Secret is linked to a single `passportId` (and cluster metadata).
- If secret is active, API requests for that battery require `X-Battery-Secret`.
- If no secret exists or secret inactive, secret header is not required.

### 7.4 What can be done

Read:

- `GET /batteries/{passportId}`
- `GET /batteries/{passportId}/section/{sectionName}`
- `GET /batteries/{passportId}/values?path=...`
- `GET /batteries/{passportId}/paths?section=...&includeContainers=true|false`
- `GET /batteries/{passportId}/telemetry/history?hours=1..168`

Write (requires `readWrite` token):

- `POST /batteries/{passportId}/telemetry`
- `PATCH /batteries/{passportId}/operations`
- `PATCH /batteries/{passportId}/software`

Software update behavior:

- Body: `{ "softwareVersion": "2.0" }`
- The requested version must exist in the MongoDB software list for the battery's selected product/battery version.
- If the version is not found, the API returns `400 Bad Request` and lists the allowed versions.
- The API writes current installed software under `app.operations.*`, so it does not dirty the signed passport.
- The product-template baseline stays under `app.product.*` and remains part of the signed passport core.
- The public summary and detailed Software tab show the current software version, release date, and latest update from `app.operations.*` when present, otherwise from `app.product.*`.

### 7.5 What cannot be done (external API)

- Cannot create or delete passports.
- Cannot edit full passport sections/aspects.
- Cannot write telemetry with read-only token.
- Cannot access batteries outside token cluster scope.
- Cannot bypass active battery secret.
- Cannot query telemetry history with `hours` outside `1..168`.
- Cannot set software to a version that is not defined for the battery product/battery version.

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

### 8.1 cURL - read full battery

```bash
curl -X GET "http://localhost:5186/api/external/v1/batteries/did:web:acme.battery.pass:sample-customer-north-001" \
  -H "Authorization: Basic U0FNUExFQkFUVEVSWVBBU1NQT1JUUkVBRFRPS04wMDE6"
```

### 8.2 cURL - write telemetry

```bash
curl -X POST "http://localhost:5186/api/external/v1/batteries/did:web:acme.battery.pass:sample-customer-north-001/telemetry" \
  -H "Authorization: Basic U0FNUExFQkFUVEVSWVBBU1NQT1JUV1JJVEVUT0swMDE6" \
  -H "Content-Type: application/json" \
  -d "{\"points\":[{\"currentConsumptionKwh\":154.6,\"currentChargeLevelPct\":82.1,\"currentVoltageV\":401.7,\"currentCurrentA\":49.2}]}"
```

### 8.3 cURL - patch operations

```bash
curl -X PATCH "http://localhost:5186/api/external/v1/batteries/did:web:acme.battery.pass:sample-customer-north-001/operations" \
  -H "Authorization: Basic U0FNUExFQkFUVEVSWVBBU1NQT1JUV1JJVEVUT0swMDE6" \
  -H "Content-Type: application/json" \
  -d "{\"isActive\":true,\"locationOfUse\":{\"siteName\":\"Factory 4\",\"city\":\"Berlin\",\"country\":\"DE\"},\"contactPerson\":{\"name\":\"Anna Becker\",\"email\":\"anna@example.test\"}}"
```

### 8.4 cURL - patch current software version

```bash
curl -X PATCH "http://localhost:5186/api/external/v1/batteries/did:web:acme.battery.pass:sample-customer-north-001/software" \
  -H "Authorization: Basic U0FNUExFQkFUVEVSWVBBU1NQT1JUV1JJVEVUT0swMDE6" \
  -H "Content-Type: application/json" \
  -d "{\"softwareVersion\":\"2.0\"}"
```

### 8.5 JavaScript (fetch)

```js
const token = "YOUR_TOKEN_VALUE";
const auth = "Basic " + btoa(token + ":");

const response = await fetch(
  "http://localhost:5186/api/external/v1/batteries/did:web:acme.battery.pass:sample-customer-north-001/values?path=ratedEnergy&path=nickelMass",
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

### 8.6 Python (requests)

```python
import base64
import requests

token = "YOUR_TOKEN_VALUE"
auth = "Basic " + base64.b64encode(f"{token}:".encode("utf-8")).decode("utf-8")

url = "http://localhost:5186/api/external/v1/batteries/did:web:acme.battery.pass:sample-customer-north-001/telemetry/history?hours=24"
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

1. Log in as global admin and verify all admin tabs.
2. Create a new cluster, assign one battery, assign one local admin and one normal user.
3. Log in as local admin and verify cluster-scoped limits, including that global admin users cannot be modified.
4. Log in as normal user and verify `/registry` shows only own cluster batteries and detail access works only for own cluster battery.
5. Open API Token Management, create an API token + battery secret, then call external API with and without secret.
6. Verify read token cannot write.
7. Verify out-of-scope token gets `403`.
8. Verify telemetry write appears in detail history charts.
9. Patch the current software version through `/api/external/v1/batteries/{passportId}/software` and confirm the public summary shows the current software version.
10. Open the detailed report and confirm the Software tab shows product, product ID, release date, and latest update.

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
7. Send telemetry or a software-version update through the external API and confirm the passport does not become dirty.

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
   - Expected software behavior: the public summary shows the current software version and the detailed report has a Software tab.
5. Edit a signed core field such as battery mass and save.
   - Expected next action: Sign passport after validation succeeds.
   - Expected state: Dirty: re-sign required.
6. Confirm warnings and proof/hash diagnostics are available under advanced diagnostics, not as the first thing an admin must parse.

### Product template baseline checklist

Use this checklist after the product-template migration to prove the app can restore and operate from production-like shared product baselines.

1. Login as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Confirm three product templates are listed: Compact 7M, Compact 13M, and Core.
4. Open each product template and confirm product/battery versions are listed newest-first and each version has nested software versions.
5. Confirm required and optional parameters are shown section by section and saved per product/battery version in MongoDB.
6. Press **Reset product-template passports** from the product templates tab or admin help.
7. Confirm one unassigned demonstrator plus six clustered customer batteries are restored:
   - `did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976`
   - `did:web:acme.battery.pass:sample-customer-north-001`
   - `did:web:acme.battery.pass:sample-customer-north-002`
   - `did:web:acme.battery.pass:sample-customer-south-001`
   - `did:web:acme.battery.pass:sample-customer-south-002`
   - `did:web:acme.battery.pass:sample-end-user-fleet-001`
   - `did:web:acme.battery.pass:sample-end-user-fleet-002`
8. Open `/admin/passports/new` and confirm Product, product/battery version, and Software version are chosen before the draft is created.
9. Edit a product/battery version value, save it, then push that saved product/battery version to matching batteries. Edit software metadata separately and use the software push only for saved software versions.
10. Confirm matching batteries preserve manual overrides, become dirty when template-owned signed data changes, and require validate, sign, and publish to return clean.

### Phase 6A end-to-end demo hardening checklist

Use this checklist after Phase 6A changes to prove the demo can be restored and verified from a known state.

1. Login as `admin@example.test`.
2. Open `/admin/clusters?tab=products`.
3. Press **Reset product-template passports**.
4. Confirm the success message says the product template passports were reset.
5. Open `/admin/passports/did%3Aweb%3Aacme.battery.pass%3Asample-customer-north-001/conformance`.
6. Expected state: Published, signed, clean, public, QR-ready.
7. Logout or use a public browser session.
8. Search for `did:web:acme.battery.pass:sample-customer-north-001` from `/`.
9. Expected result: the public summary opens.
10. Download or click the QR code from the summary page.
11. Expected result: the QR resolves back to the public summary URL.
12. Login again as `admin@example.test`.
13. Edit `did:web:acme.battery.pass:sample-customer-north-001`, change one signed core field such as weight, and save.
14. Open conformance and confirm the passport is dirty or requires a fresh signature.
15. Validate, sign, and publish the passport again.
16. Expected result: the passport returns to published, signed, and clean.
17. Use the external API workbench or curl to send a telemetry update to the same passport.
18. Expected result: External HTTP telemetry update does not dirty the passport.
19. Use the external API workbench or curl to patch software to an allowed version such as `2.0`.
20. Expected result: the summary and detailed Software tab show the current software value, and the passport remains clean.
21. Try restricted document access on a private evidence file.
22. Expected result: Restricted document download returns 403 for unauthorized users and remains accessible only to authorized admin/cluster users when a linked file exists.

### Phase 6B Evidence Readiness Check

Use an admin account and a passport from the demo catalog.

1. Open `/admin/clusters?tab=passports`.
2. Open the passport edit page.
3. Upload or replace one required supporting document.
4. Open the passport conformance page.
5. Confirm the Evidence readiness panel shows the file as uploaded unsigned or Changed since signing.
6. Validate the passport.
7. Sign the passport again.
8. Confirm the Evidence readiness panel shows the document as verified in the latest signed revision.
9. Publish when the readiness page shows the publish action.
10. Try a restricted document download from a public browser session and confirm it is denied.

Expected result: document hashes are visible to admins, changed evidence requires a new signature, and restricted files remain access-controlled.
