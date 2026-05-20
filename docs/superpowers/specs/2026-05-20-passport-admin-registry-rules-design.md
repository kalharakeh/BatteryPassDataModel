# Passport Admin Registry Rules Design

## Goal

Resolve the 32 reviewed findings as one coherent change set for passport state, battery editing, API behavior, registry search, report polish, and admin UX. The design keeps passport snapshots trustworthy while allowing batteries to carry current operational values such as cluster assignment, editable product metadata, and telemetry.

## Approved Implementation Phases

1. Core data and status rules.
2. APIs and template-driven editing.
3. Admin, registry, report, and UX polish.

The implementation plan should preserve this order so state rules are settled before API and UI surfaces depend on them.

## Key Decisions

- A battery cannot be created without a cluster.
- New passports inherit the battery record's current cluster assignment.
- Cluster assignment can be edited after creation and is passport-affecting.
- The visible `dirty` state is renamed to `Awaiting sign-off`; the internal technical state can remain `dirty`.
- `New passport needed` is derived from editable-field differences against the latest passport snapshot, not treated as a permanent flag.
- `Draft` and `New passport needed` are mutually exclusive. Creating the new draft passport clears `New passport needed`; the draft then needs validation, signing, and publishing.
- Telemetry is live operational data. It updates reports immediately and never causes validation, signing, publishing, dirty/awaiting-sign-off, or new-passport-needed states.
- Local/cluster admins can view passport history read-only for in-scope batteries, but only global admins can edit historical passports.
- Local/cluster admins can create, validate, sign, and publish only latest/current passports for batteries in clusters they administer.
- Registry cluster search is global-admin-only. Non-global users who search for a cluster name or cluster ID should see a clear access message.

## Phase 1: Core Data And Status Rules

Battery documents are the current operational source for cluster assignment and allowed post-creation edits. Passport documents remain versioned snapshots of passport-governed data.

Battery creation requires:

- Cluster
- Battery family
- Battery model
- Battery serial number
- Manufactured date
- Facility ID
- Manufactured by
- Software version

At creation, all other model-linked parameters are inherited from the selected battery family/model template. After creation, the default editable fields are:

- Battery Model
- Facility ID
- Software Version
- Cluster assignment

These defaults become the seed policy for the global Editable Fields configuration, not unchangeable hardcoded behavior.

The new-passport-needed calculation compares only passport-affecting fields that are editable after creation against the latest passport snapshot. If a value differs, the battery needs a new passport. If the value is changed back to match the latest passport, the indicator clears automatically. Telemetry fields are excluded from this comparison.

When a new passport is created from a battery, the passport receives the battery's current cluster assignment and current passport-governed values. The battery no longer shows `New passport needed` for those values unless they change again.

## Phase 2: APIs And Template-Driven Editing

Battery Model updates through the app or API must validate that the requested model exists for the battery's family. Invalid values are rejected before they can create broken validation states. Valid model changes copy all linked family/model parameters onto the battery and then run the same latest-passport diff logic.

Software Version updates through the app or API must validate the requested version against the allowed software data for the selected family/model. Valid software changes copy software release date and software latest update automatically. Release date and latest update cannot be edited directly on the battery.

When family/model template data is pushed to matching batteries, the service compares pushed values to each battery's current values. Only batteries with actual differences are updated, and only those batteries become new-passport-needed.

External API changes:

- Cluster-scoped read tokens authorize by the battery's current cluster assignment.
- A cluster token can read batteries in its cluster and receives `403` only for out-of-scope batteries.
- Add and document `/api/external/v1/batteries/{batteryId}/passports`.
- Include `/api/external/v1/batteries/{batteryId}/passports` in the API help endpoint dropdown.
- Rename the token capability label to `create, validate, sign and publish passport`.
- Ensure that token scope actually allows create, validate, sign, and publish passport workflow actions.
- Add a software-version update API path that uses the shared software validation and metadata-copying logic.
- Define a telemetry write template supporting one or many variables and one or many timestamped points.
- Update API help with clear examples for sending one point, multiple points for one variable, and multiple variables with multiple points.
- Telemetry reads/graphs sort accepted points by timestamp and ignore or flag malformed points without breaking the detailed report.

## Phase 3: Admin, Registry, Reports, And UX

### Summary And Detail Reports

- QR scan resolves by Battery ID to the latest public summary report.
- QR download returns a QR image file, not text.
- The Summary report header uses three columns on desktop:
  - Battery serial, cluster, and latest-passport status.
  - Compact Passport ID and Battery ID with copy controls.
  - QR code.
- The header collapses cleanly on smaller screens and lower resolutions.
- `Original Power` matches other section headings in size, weight, spacing, and boldness.
- Carbon footprint and recycled content charts use distinct colors and table-style legends with values.

### Admin Wording And Validation

- Show `Awaiting sign-off` instead of `dirty` in user-facing UI.
- Change Registered Clusters action text from `Save Cluster` to `Create Cluster`.
- Creating a cluster with an existing Cluster ID is blocked with validation and never overwrites the existing cluster.
- User role displays become explicit: `Global admin: Yes/No` plus cluster memberships and roles.
- Password and Confirm Password fields use consistent password reveal controls everywhere.
- All admin and cluster-admin select controls use the same visible dropdown styling and chevron treatment.

### Forgot Password Placeholder

Add a front-page/login `Forgot password?` flow:

- User enters email.
- The app shows the neutral success message `If an account exists, reset instructions will be sent when email delivery is configured.`
- The app stores a reset request in internal audit/reset-request storage for admin/debug review.
- No outbound email is sent until email delivery is configured.

### Users Pages

The cluster-admin Users page should mirror `/admin/clusters?tab=users` in layout, controls, table behavior, and UX. It is scoped to users who share at least one cluster administered by the logged-in cluster admin.

### Managed Passport

- Rename the first column from Battery ID to Passport ID.
- Search matches Passport ID, Battery ID, and battery serial number.
- Add a status column using user-facing labels such as Draft, Published, and Awaiting sign-off.

### Registry Search

Registry search supports:

- Battery ID
- Battery serial number
- Passport ID
- Cluster name, for global admins only
- Cluster ID, for global admins only

Searching by battery ID or serial number opens the latest public passport for non-admin users. For global and local admins, it shows the battery's passport list/history. Local admins can view history read-only; global admins can edit historical passports.

Searching by passport ID opens that passport directly, subject to visibility and authorization.

Searching by cluster name or cluster ID displays batteries associated with that cluster only for global admins. Non-global users receive a message explaining that cluster search requires global admin access.

### API Help And Tokens

- Rename `validate and sign` token wording to `create, validate, sign and publish passport`.
- Keep sample/default API tokens aligned with the new label.
- Add telemetry payload examples and the missing battery passports endpoint to the help UI.

### Responsive Requirements

All touched pages must be checked at smaller widths and lower resolutions. Tables, IDs, QR blocks, buttons, dropdowns, legends, and status pills must wrap, collapse, or scroll without clipping, overlap, or unreadable text.

## Editable Fields Configuration

Rename Local Editable Fields to Editable Fields. The configuration is global across all clusters.

Each parameter row has three checkmarks:

- Editable at creation
- Editable after creation
- Editable by local admin

Dependency rules:

- Checking `Editable by local admin` automatically checks `Editable after creation` and `Editable at creation`.
- Checking `Editable after creation` automatically checks `Editable at creation`.
- Unchecking `Editable at creation` automatically unchecks the other two.
- Unchecking `Editable after creation` automatically unchecks `Editable by local admin`.

Telemetry fields are always writable operational data and do not affect passport status.

Default seed configuration:

- Editable at creation: Cluster, Battery Family, Battery Model, Battery serial number, Manufactured date, Facility ID, Manufactured by, Software Version.
- Editable after creation: Battery Model, Facility ID, Software Version, Cluster assignment.
- Editable by local admin: the same fields allowed after creation by default.

## Testing Strategy

Automated tests should cover:

- Battery creation rejects missing cluster.
- New passport creation copies the battery's current cluster.
- Cluster edits can trigger and clear new-passport-needed through latest-passport diffing.
- Draft passports do not also show new-passport-needed.
- User-facing dirty labels render as Awaiting sign-off.
- Telemetry writes never affect passport readiness or trust state.
- Battery Model and Software Version API writes reject invalid values and copy linked metadata for valid values.
- Product template push marks only changed batteries as needing a new passport.
- Cluster-scoped API tokens authorize by current battery cluster.
- The new battery passports API endpoint appears in help and enforces scope.
- Telemetry payload parsing accepts valid single and multi-point payloads and handles malformed points safely.
- Global admins can search clusters in the registry; non-global users see the global-admin-required message.
- Managed Passport searches Passport ID, Battery ID, and serial number and shows status.
- Local admins can view passport history read-only and cannot edit historical passports.
- Cluster-admin Users mirrors the global Users tab layout while enforcing scope.
- QR scanning resolves latest public summary by Battery ID and QR download returns an image.
- Report charts render distinct colors and value legends.
- Responsive checks cover the affected report, registry, managed passport, users, clusters, and editable-fields pages.

## Out Of Scope

- Actual outbound forgot-password email delivery.
- Per-cluster editable-field policy. The approved policy is global.
- Letting telemetry affect passport validation, signing, publishing, dirty, or new-passport-needed state.
