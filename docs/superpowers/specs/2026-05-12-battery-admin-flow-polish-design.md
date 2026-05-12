# Battery Admin Flow Polish Design

## Goal

Polish the battery-first admin workflow after the Battery ID / Passport ID redesign so admins manage battery records directly, create passport snapshots intentionally, and inspect passport history on a dedicated admin page.

## Decisions

- Use the battery list as the canonical admin management surface.
- Keep `/admin/clusters?tab=batteries` as the canonical URL.
- Keep `/admin/clusters?tab=passports` as a compatibility alias that opens the same Batteries tab.
- Normal non-telemetry edits go to the battery record, not to existing passport snapshots.
- Existing passport snapshots remain snapshot/trust records. They are used for summary, detailed report, conformance, sign/publish, audit, revisions, archive, and unarchive.
- API-created passport snapshots start as Draft and require explicit validate, sign, and publish.

## Admin Battery List

The Batteries tab shows one compact row per battery. The Passport column displays only the number, such as `1` or `2`, without the word `passport`.

The table should be wide enough for the 70-character Battery ID to stay on one line. When the viewport cannot fit the table, the table should scroll horizontally instead of wrapping Battery IDs or breaking the row layout.

The battery list no longer embeds passport-history rows under each battery. Row actions are icon buttons:

- Passport history: opens `/admin/batteries/{batteryId}/passports`
- Edit battery: opens `/admin/batteries/{batteryId}/edit`
- Latest conformance: opens latest passport conformance when a latest passport exists
- Create passport: creates a new snapshot for that battery

If the battery record has changed after the latest passport snapshot was created, the row shows a persistent `New passport needed` indicator. This indicator remains visible until a new passport snapshot is created from the battery.

## Battery Edit Flow

Add a dedicated battery edit route:

- `GET /admin/batteries/{batteryId}/edit`
- `POST /admin/batteries/{batteryId}/save`

The edit page edits the MongoDB battery document. It does not mutate existing passport snapshots.

The following identity fields are editable during initial battery creation and read-only after save:

- Battery ID
- Battery Family
- Battery serial number
- Manufactured by

Battery Model defaults to the latest model of the selected Battery Family when creating a battery. When the Battery Family selector changes during creation, the Battery Model selector should update to that family's latest model.

After saving battery edits, redirect to `/admin/clusters?tab=batteries` with a status message that the battery was saved and a new passport snapshot should be created to expose the updated data.

## Admin Passport History

Add a dedicated admin passport-history route:

- `GET /admin/batteries/{batteryId}/passports`

This page includes:

- Back to Batteries button linking to `/admin/clusters?tab=batteries`
- Battery identity header with Battery ID, serial number, Battery Family, Battery Model, and cluster label
- Create passport action
- One wide table row per passport

Passport history row actions are icon-only controls with hover/title text:

- Summary report
- Detailed report
- Conformance
- Audit trail
- Archive or Unarchive

The history table uses enough width to keep each Passport ID and its controls on one row, with horizontal scrolling on small screens.

## Passport Report Header Polish

In summary and detailed report pages, the bold identity line under Passport ID and Battery ID should show the Battery serial number instead of Battery Family.

When viewing a historical passport, the `Open latest passport` button becomes an icon-only action with hover/title text. The action should visually read as a history/latest navigation control, not as a long text button.

Battery ID remains visible in summary and detailed reports.

## Registry Access Bug

Public users remain unable to open `/registry`.

Signed-in regular users should see signed or published batteries for clusters they belong to. The observed state where `north.user@example.test` sees `No signed or published batteries are currently available to your role.` is a bug. After reset, that user should see the North Operations cluster's signed/published batteries.

## API Help And Reset Data

Product template reset creates a stable API demo setup for one example cluster battery.

The reset flow must create fixed demo token values for the QA/demo environment:

- Read token
- Read-write token
- Validate/sign/publish token

The external API help page displays:

- Sample Battery ID
- Sample Passport ID
- Sample read token
- Sample read-write token
- Sample validate/sign/publish token

The sample battery should belong to an example cluster so API examples cover scoped token behavior. The help page should provide all values needed to test read, write, telemetry, create passport, validate, sign, and publish flows without manually creating extra tokens.

## Data Model Notes

Battery documents need a durable way to know whether a new passport snapshot is required. The implementation can store either:

- battery `updatedAt` compared with latest passport `snapshot.createdAt`, or
- an explicit pending snapshot flag such as `app.snapshot.newPassportRequired`.

The preferred implementation is explicit state on the battery because it is easy to query and display. Creating a passport snapshot clears the pending indicator for that battery.

## Testing

Automated tests should cover:

- `tab=passports` resolves to the same battery list as `tab=batteries`, while redirects and links use `tab=batteries`.
- Battery list shows numeric passport count and no embedded history rows.
- Admin history page contains Back to Batteries and icon actions for summary, detailed report, conformance, audit, and archive/unarchive.
- Battery edit route updates battery documents, returns to the battery list, and sets the persistent new-passport-needed indicator.
- Creating a passport snapshot clears the new-passport-needed indicator.
- Created battery defaults Battery Model to the selected family's latest model.
- Locked identity fields are read-only after save.
- Summary/detail headers show Battery serial number in the top identity position.
- Historical latest-passport action is icon-only.
- `north.user@example.test` can see signed/published North cluster batteries in `/registry`.
- Reset creates fixed sample API tokens and `/help` displays the read, read-write, and sign/publish token values.
