# Passport Admin Test Feedback Fix Design

## Goal

Fix the issues found during manual testing after the passport admin and registry rules work. The fixes should keep the existing approved rules, but tighten the implementation around shared battery tables, report responsiveness, editable-field enforcement, reset behavior, software-version management, and local-admin/global-admin consistency.

## Clarified Decisions

- Global account access has only two choices: `Global Admin` and `Cluster Member`.
- `Notified Body`, `Market Surveillance Authorities`, `Commission`, and `Person with Legitimate Interest` are cluster membership roles only.
- For now, only `Cluster Admin` has cluster-scoped battery edit and passport workflow rights. Other cluster roles are read-only.
- `/registry`, `/admin/clusters?tab=batteries`, `/cluster-admin/passports`, and `/admin/passports` keep their URLs and page shells, but render the same shared battery table/search/actions component.
- Local cluster admins see only managed-cluster batteries in shared battery pages. They can view passport history read-only and can edit/create workflow items only where their cluster-admin role permits it.
- Editable-field policy restricts everyone, including global admins. Global admins can configure the policy and edit historical passport snapshots, but they do not bypass locked battery fields.
- Locked battery fields stay visible as disabled/gray read-only fields and are also protected server-side.
- Telemetry and operational fields are always editable and never affect passport dirty/awaiting-sign-off, validation, signing, publishing, or new-passport-needed state. This includes location/contact/isActive operational fields.
- New-passport-needed is always calculated by comparing current battery data against the latest passport snapshot. The same rule applies after direct battery edits and after family/model template pushes.
- Allowed software versions belong to each Battery Family + Battery Model combination. New battery creation defaults to the highest software version string.
- The reset battery-family passports action becomes a full canonical reset, including users, clusters, memberships, tokens, batteries, passports, telemetry/supporting data, product templates, editable-field policy, and software-version defaults.

## Report And Chart Fixes

Summary and detail pages should use one shared responsive report header pattern.

Desktop layout keeps the fixed identity/header presentation with separate areas for:

- Battery serial, cluster, and latest/historical status.
- Compact Passport ID and Battery ID with copy controls.
- QR code and media.

On smaller screens the identity cards stack cleanly. The QR code should sit near the battery image when that saves vertical space, and only stack fully on very narrow screens. IDs, QR, and image panels must not overlap, clip borders, or spill outside their containers.

Carbon footprint charts in summary and detail must assign distinct colors to lifecycle-stage segments and show matching value legends. Recycled-content cards must use responsive grid sizing so cards do not stack awkwardly when two can fit.

Telemetry history charts in the detail Performance tab must render inside fixed-height chart panels. Data should be sorted and sanitized before charting so malformed or sparse telemetry cannot cause vertical runaway or page crashes.

## Shared Battery Table

Build one shared battery-table view model and partial for:

- `/registry`
- `/admin/clusters?tab=batteries`
- `/cluster-admin/passports`
- `/admin/passports`

The shared table is the source of truth for:

- Battery ID with trimming/copy behavior.
- Battery family/model/serial.
- Cluster label.
- Passport count/history access.
- Latest passport status.
- New-passport-needed indicator.
- Updated date.
- Role-aware actions.

Search behavior:

- Battery ID and battery serial open battery history for global admins.
- Battery ID and battery serial keep the current local-admin behavior for cluster admins.
- Anonymous sample search from `/` opens the latest public summary report.
- Passport ID opens that passport directly, subject to visibility.
- Cluster name/ID search is global-admin-only; other roles see a clear access message.

History pages need a return-source parameter so history opened from `/registry` goes back to `/registry`, not `/admin/clusters?tab=batteries`.

## Users, Roles, Clusters, And Tokens

The Users tab should rename `Global access` to `Access` with options:

- `Global Admin`
- `Cluster Member`

Cluster membership role dropdowns should support:

- `Cluster Admin`
- `Normal User`
- `Notified Body`
- `Market Surveillance Authorities`
- `Commission`
- `Person with Legitimate Interest`

Cluster IDs must be normalized and duplicate creation must be blocked both in controller/service validation and by a MongoDB uniqueness constraint. The full reset clears duplicate test data and recreates canonical clusters.

Global-admin and cluster-admin API token pages should share the same UX and layout, with local-admin token lists/actions scoped to managed clusters. Token create/regenerate should show a modal popup:

- `Copy` copies the token and keeps the popup open.
- `Okay` copies the token and closes the popup.

## Battery Create/Edit And Editable Fields

Battery creation must include a required cluster selector and preserve form state when validation fails. Creation allows only:

- Battery Family
- Battery Model
- Battery serial number
- Manufactured date
- Facility ID
- Manufactured by
- Software version
- Cluster

All other battery parameters are inherited from the selected Battery Family + Battery Model and rendered locked.

Battery edit follows the editable-field policy. Allowed fields unlock and persist the submitted value. Locked fields render disabled/gray and cannot be changed through forged form submission. The labels `Battery mass` and `Weight` should be made consistent, with `Battery mass` preferred for admin/editable-field surfaces.

When an allowed passport-affecting field is saved, the system recalculates new-passport-needed by comparing the current battery document against the latest passport snapshot. If the value matches the latest passport again, the indicator clears.

## Battery Family Software Versions

The Battery Family editor should expose software versions as rows under the selected Battery Model:

`Software version | Release date | Latest update | Actions`

Admins can add and remove rows. Battery create/edit forms use a dropdown of allowed versions for the selected Battery Family + Battery Model. Selecting a software version copies its release date and latest update date automatically. The API software-version update path uses the same allowed-list validation and metadata copy logic.

## Full Reset

The reset battery-family passports button should clear and reseed the complete demo/test state:

- Users and cluster memberships.
- Clusters.
- API tokens.
- Product templates and per-model software-version lists.
- Editable-field policy.
- Batteries.
- Passports and passport audit/supporting data.
- Telemetry/supporting demo data.

The reset should leave the database in a canonical state that satisfies the current rules, including no unassigned batteries and no duplicate clusters.

## Testing Strategy

Automated coverage should include:

- Anonymous sample search opens latest public summary.
- Shared battery table returns consistent status/new-passport-needed data across registry/admin/cluster-admin/passports URLs.
- Global-admin battery search opens admin history; local-admin search keeps local behavior.
- Local admins can view history read-only and cannot edit historical passports.
- Duplicate Cluster ID creation is rejected and unique indexes are present.
- Full reset clears stale test data and recreates canonical users, clusters, tokens, batteries, passports, policies, and software versions.
- Editable-field policy locks visible fields and blocks server-side changes for locked fields.
- Allowed battery mass edits persist and recalculate new-passport-needed against the latest passport.
- Battery creation requires cluster and only allows the approved creation fields.
- Per-model software-version rows can be added and selected; invalid API software versions are rejected.
- Token modal copy/okay behavior works on global and cluster-admin token pages.
- Report headers, IDs, QR, battery image, recycled content, carbon charts, and telemetry charts behave at desktop and narrow widths.

Manual browser verification should cover summary, detail Performance tab, registry, admin batteries, admin passports, cluster-admin passports, users, editable fields, battery creation/editing, battery family software versions, reset, and token management.

## Out Of Scope

- Changing detailed permissions for read-only authority roles beyond the current read-only cluster membership behavior.
- Outbound forgot-password email delivery.
- Making telemetry or operational fields affect passport trust/readiness state.
