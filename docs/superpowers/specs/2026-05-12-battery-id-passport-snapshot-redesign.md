# Battery ID And Passport Snapshot Redesign

## Supersedes And Updates

This spec updates the current battery family/API design and supersedes conflicting parts of:

- `docs/superpowers/specs/2026-05-11-battery-family-access-api-revised-design.md`

The earlier role, access, admin guardrail, cluster, account, token-management, and battery secret removal requirements still apply unless this spec explicitly changes them.

## Glossary

- Battery is the physical managed asset. The app manages batteries first, not passports first.
- Battery ID is a 70-character opaque deterministic identifier for a battery.
- Passport ID is a 70-character opaque deterministic identifier for one passport snapshot.
- Battery Family is the product-family level, for example Compact 7M, Compact 13M, or Core.
- Battery Model replaces the previous user-facing label Battery version.
- Product Template may remain an internal implementation name for Battery Family and Battery Model data.
- Passport snapshot is immutable non-telemetry data copied from a battery at a point in time.
- Latest public passport is the newest passport for a battery that is visible to the current public access rules.
- Admin latest passport is the newest passport for a battery by creation time, including drafts.

## Goals

- Introduce first-class battery management.
- Generate and store separate Battery IDs and Passport IDs.
- Make Battery ID the stable physical-battery identity and QR-code target.
- Make passports immutable snapshots of non-telemetry battery data.
- Store telemetry by Battery ID and expose it only through the latest passport view.
- Replace user-facing Battery version wording with Battery Model.
- Change external API write/read flows to use Battery ID for battery operations and Passport ID for trust operations.
- Reset/reseed demo MongoDB data into a clean battery-centered shape.

## Non-Goals

- Migrating every existing demo passport into the new shape. The demo reset will recreate data.
- Exposing raw battery records directly to public users.
- Renaming all internal ProductTemplate code in this phase.
- Keeping telemetry writes by Passport ID.
- Keeping anonymous registry browsing.

## ID Generation

Both generated IDs are opaque, deterministic, exactly 70 characters, and use only Base64URL characters: `A-Z`, `a-z`, `0-9`, `_`, and `-`.

ID generation uses an app-level `ID_GENERATION_SECRET` and HMAC. The recommended implementation is HMAC-SHA512 with context strings, Base64URL encoding without padding, then truncation to 70 characters.

Battery ID input:

- normalized Battery Family
- normalized Battery serial number

Passport ID input:

- UTC creation timestamp including time
- Battery ID
- normalized Battery Model

Battery Family and Battery serial number define the Battery ID and are locked after battery creation. Battery Model can change later and affects future Passport IDs, not existing ones.

Production must refuse ID creation when `ID_GENERATION_SECRET` is missing. Local development uses a generated `.env.local` value. The secret is never sent to the browser. Live ID preview is served through a server endpoint that accepts the visible inputs and returns the computed ID.

Passport ID final generation uses the server-assigned UTC snapshot timestamp. Any Passport ID shown before submit is a server-generated preview. The saved Passport ID returned after creation is authoritative.

## MongoDB Shape

Add a `batteries` collection. A battery document stores:

- `batteryId`
- locked identity inputs: Battery Family and Battery serial number
- current mutable Battery Model
- cluster/customer assignment
- manufacturer, facility, client/general data
- current non-telemetry static battery data
- current documents and references
- operational fields
- created/updated metadata

Keep `passports` as the passport snapshot collection. A passport document stores:

- `passportId`
- `batteryId`
- copied non-telemetry battery data at snapshot time
- lifecycle/trust state
- snapshot created timestamp
- `isLatestForBattery`
- `supersededAt`
- `supersededByPassportId`

`batteryTelemetry` is keyed by `batteryId`. Existing `passportId` telemetry writes are no longer accepted. Indexes should enforce unique Battery ID and Passport ID, support passport lookup by Battery ID and creation time, and support telemetry lookup by Battery ID and measured time.

## Snapshot Rules

Battery data is mutable. Passport non-telemetry data is immutable after creation.

Creating a passport snapshots the battery's current non-telemetry data, including identity display fields, Battery Family, Battery Model, serial number, manufacturer, documents, static technical data, material/carbon/compliance data, and other current battery fields.

Telemetry is not snapshotted. The latest passport overlays live and historical telemetry from `batteryTelemetry` by Battery ID. Historical passports do not show live telemetry. They should either hide the telemetry view or show a notice that live telemetry is available from the latest passport.

When a new passport is created, the previous latest passport is not archived. It keeps its lifecycle status and is marked as superseded/historical.

## Public Routes And Search

Public/direct routes stay root-relative:

- `/<batteryId>` opens a battery-level view listing all linked passports as rows.
- `/<batteryId>/latest` opens the latest public passport for the battery.
- `/<passportId>` opens the exact passport snapshot.

The router resolves Battery ID first, then Passport ID. Unique indexes make collisions invalid.

The front page search accepts Battery ID or Passport ID:

- Battery ID redirects to `/<batteryId>`.
- Passport ID redirects to `/<passportId>`.

The physical QR code points to `/<batteryId>/latest` so it remains stable for the battery lifetime.

Anonymous users cannot browse the registry. They can access known direct URLs only when the target passport is visible under access policy. The battery-level public page shows linked passport rows and icon actions only for passports/actions the current user may open.

Historical passport pages show a Historical passport indicator, linked Battery ID, and a link to the latest passport. Latest passport pages indicate when earlier passports exist.

## Signed-In Registry

Signed-in registry users see one row per battery, not one row per passport. Each row shows:

- Battery ID
- Battery Family
- Battery Model
- Battery serial number
- passport count, for example `3 passports`
- latest passport status
- updated date
- icon actions for latest summary and detailed reports, respecting access level

Anonymous users do not have access to the open registry.

## Admin Workflow

Admin and cluster-admin management shifts from passports to batteries.

The admin battery table shows one row per battery with embedded passport history. Columns include Battery ID, Battery Family, Battery Model, serial number, cluster/customer, passport count, latest passport status, and updated date.

Each battery row exposes actions to:

- edit the battery
- create a passport snapshot
- expand/view linked passport history

The embedded passport history shows Passport ID, created timestamp, status, latest/historical marker, and summary/detail/conformance/audit actions.

Create battery flow:

- select Battery Family
- enter Battery serial number
- preview generated Battery ID live through a server endpoint
- create the battery
- lock Battery Family and serial number after creation
- guide admin to create the first passport

Create passport flow:

- select an existing battery
- preview generated Passport ID from server timestamp, Battery ID, and Battery Model
- snapshot current battery data
- mark previous latest passport as superseded
- create the new passport as draft

Admin latest includes drafts. Public latest resolves to the newest publicly visible passport until a newer draft is signed/published.

## External API

Canonical battery API routes use Battery ID:

- `GET /api/external/v1/batteries/{batteryId}`
- `GET /api/external/v1/batteries/{batteryId}/section/{sectionName}`
- `GET /api/external/v1/batteries/{batteryId}/values`
- `GET /api/external/v1/batteries/{batteryId}/paths`
- `POST /api/external/v1/batteries/{batteryId}/telemetry`
- `GET /api/external/v1/batteries/{batteryId}/telemetry/history`
- `PATCH /api/external/v1/batteries/{batteryId}/operations`
- `PATCH /api/external/v1/batteries/{batteryId}/battery-model`
- `POST /api/external/v1/batteries/{batteryId}/passports`

Battery read endpoints return data as exposed through the latest accessible passport. They do not expose raw battery documents.

Telemetry writes append to `batteryTelemetry` by Battery ID and update the live telemetry overlay used by the latest passport. If a Passport ID is supplied to a telemetry route, the API returns a clear error explaining that telemetry is keyed by Battery ID.

Non-telemetry API writes update the mutable battery source record and do not mutate existing passport snapshots. Responses state that a new passport should be created and signed/published to expose the updated snapshot.

`POST /api/external/v1/batteries/{batteryId}/passports` creates a new passport snapshot and returns the generated `passportId`.

Trust operations use Passport ID:

- `POST /api/external/v1/passports/{passportId}/validate`
- `POST /api/external/v1/passports/{passportId}/sign`
- `POST /api/external/v1/passports/{passportId}/publish`

The external Sign token mode becomes a Validate + sign + publish capability. First-time publishing is allowed through the external API.

## QR Codes

QR code generation uses Battery ID and produces a payload for:

- `/<batteryId>/latest`

The QR SVG filename should be derived from Battery ID. Passport pages may display or download the stable battery QR, not a passport-specific QR.

## Reset And Seed Data

The reset flow wipes and recreates the demo domain data in the new battery-centered shape:

- `batteries`
- `passports`
- `batteryTelemetry`
- seeded audit/revision ledger entries
- product-template/family/model data as needed

Existing demo passports are discarded during reset. Seeded batteries should include multiple passports for at least one battery so latest/historical behavior is visible.

## User-Facing Naming

User-facing text should use:

- Battery Family
- Battery Model
- Battery serial number
- Battery ID
- Passport ID
- Battery status for the physical/general battery status
- Passport status for Draft, Signed, Published, or Archived

Battery version should be removed from user-facing UI, docs, API help, and tests. Internal code may retain product/version terms where changing them would create unnecessary churn, but public APIs should use `battery-model`.

Passport summary, detail, registry, admin rows, and API responses include Battery ID and Passport ID where relevant.

## Testing Strategy

Tests should cover:

- Battery ID and Passport ID are exactly 70 Base64URL characters.
- ID generation is deterministic with the same secret and inputs.
- ID generation requires `ID_GENERATION_SECRET` outside development.
- Battery ID changes when Battery Family or serial number changes before creation.
- Passport ID changes when timestamp or Battery Model changes.
- Battery Family and serial number are locked after battery creation.
- Battery creation live preview is server-driven.
- Passport creation snapshots current non-telemetry battery data.
- Existing passport snapshots are not mutated by later battery edits.
- Previous passport is superseded but not archived when a new one is created.
- Latest passport overlays telemetry by Battery ID.
- Historical passports do not show live telemetry and link to latest.
- Public routes resolve `/<batteryId>`, `/<batteryId>/latest`, and `/<passportId>`.
- Front-page search redirects Battery ID to `/<batteryId>` and Passport ID to `/<passportId>`.
- QR payload points to `/<batteryId>/latest`.
- Anonymous users cannot browse the registry.
- Signed-in registry shows battery rows with passport counts and role-aware actions.
- External API reads through latest passport data.
- External API writes update battery source data and require new passport creation for non-telemetry changes.
- `POST /api/external/v1/batteries/{batteryId}/passports` returns `passportId`.
- Trust API validates, signs, and publishes by Passport ID.
- Telemetry API rejects Passport IDs with a clear Battery ID error.
- Reset recreates batteries, passports, products/families/models, and telemetry in the new shape.
