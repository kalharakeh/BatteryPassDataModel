# Battery Pass Demonstrator - End User Testing Guide



## 1. What this app is and how it is built

- App type: ASP.NET Core MVC web app (`web/BatteryPassWeb.csproj`, target `net10.0`).
- Backend storage: MongoDB collections for passports, users, clusters, memberships, API tokens, battery secrets, telemetry.
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
  - `/admin/clusters?tab=passports|battery|clusters|users|api-tokens|battery-secrets`
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
- Can create/manage all API tokens.
- Can create/manage all battery secrets.
- Can open detailed report for any battery, including unassigned batteries.

## 3.2 Cluster admin (local admin)

- Can access `/cluster-admin/*`.
- Scope limited to clusters where membership role is `clusterAdmin`.
- Can edit local fields for batteries in managed clusters:
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

## 5. What the Battery Pass contains and where to find it

Detailed report tabs are in `/{passportId}` and grouped into:

1. General
2. Material composition
3. Performance
4. Compliance
5. Supply chain
6. Circularity
7. Carbon Footprint

### 5.1 General

- Display identity data:
  - Passport ID, model, serial, category, status, weight, manufactured date, manufacturer, facility ID, cluster label.
- Source paths (document level):
  - `app.display.*`
  - `aspects.generalProductInformation.payload.*`
  - `registryInfo.*`

### 5.2 Material composition

- Pie chart + material table:
  - Nickel, Copper, Aluminium, Graphite, Manganese, Cobalt, Lithium, Electrolyte/separators.
- Source paths:
  - `aspects.materialComposition.payload.batteryMaterials`
  - optional chart cache in `app.charts.materialComposition`

### 5.3 Performance

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

### 5.4 Compliance

- Document links:
  - Conformity assessment
  - EU declaration of conformity
- Source paths:
  - `app.documents.*`
  - `aspects.labeling.payload.*`

### 5.5 Supply chain

- Supply chain index and document links:
  - Sustainability report
  - Due diligence report
  - Third-party audit
  - Taxonomy report
- Source paths:
  - `aspects.supplyChainDueDiligence.payload.*`
  - `app.documents.*`

### 5.6 Circularity

- Text fields:
  - Separate collection
  - Waste prevention
  - Recycled content share verification state
- Recycled-content donuts by material (pre/post/primary shares).
- Source paths:
  - `aspects.circularity.payload.*`
  - `app.notes.circularity.*`
  - optional chart cache in `app.charts.recycledContent`

### 5.7 Carbon footprint

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

- General admin in `/admin/clusters?tab=api-tokens`

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

- General admin: `/admin/clusters?tab=battery-secrets`
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

### 7.5 What cannot be done (external API)

- Cannot create or delete passports.
- Cannot edit full passport sections/aspects.
- Cannot write telemetry with read-only token.
- Cannot access batteries outside token cluster scope.
- Cannot bypass active battery secret.
- Cannot query telemetry history with `hours` outside `1..168`.

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

### 8.4 JavaScript (fetch)

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

### 8.5 Python (requests)

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
5. Create API token + battery secret, then call external API with and without secret.
6. Verify read token cannot write.
7. Verify out-of-scope token gets `403`.
8. Verify telemetry write appears in detail history charts.

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
5. Edit a signed core field such as battery mass and save.
   - Expected next action: Sign passport after validation succeeds.
   - Expected state: Dirty: re-sign required.
6. Confirm warnings and proof/hash diagnostics are available under advanced diagnostics, not as the first thing an admin must parse.
