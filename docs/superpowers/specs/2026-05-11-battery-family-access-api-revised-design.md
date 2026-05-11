# Battery Family, Access, And API Revised Design

## Supersedes

This spec supersedes:

- `docs/superpowers/specs/2026-05-11-passport-admin-ux-guardrails-design.md`
- `docs/superpowers/plans/2026-05-11-passport-admin-ux-guardrails.md`

The revised implementation plan should combine the earlier admin/passport guardrails with the taxonomy, API token, signing, and access-level changes described here.

## Glossary

- Battery Family is the user-facing name for the existing internal Product Template concept.
- Product Template may remain in internal code names for now. In implementation discussions, Product Template and Battery Family mean the same internal concept unless explicitly stated otherwise.
- Battery version is the user-facing name for the existing product/battery version concept.
- Battery serial number is the user-facing name for the existing serial number field.
- Battery status is the physical/general `general/status` battery parameter from `generalProductInformation`.
- Passport status is the lifecycle state: Draft, Signed, Published, or Archived.

## Goals

- Rename Product/Product templates to Battery Family in the user-facing UI and documentation.
- Rename Product/battery version to Battery version.
- Remove Battery Name and Model Number from user-facing identity flows.
- Make Battery Family, Battery version, and Battery serial number the primary battery identity fields.
- Make software version, release date, and latest update ordinary Battery version parameters, not defining variants.
- Remove battery secrets completely.
- Add cluster-scoped API sign tokens and API validate/sign endpoints.
- Preserve the first explicit publish flow while auto-publishing later successful signs for passports that were published once before.
- Implement the revised access levels and visibility rules.
- Keep and integrate the earlier admin guardrails: clear Passport status/Battery status labels, archive confirmation/unarchive, safer cluster deletion, account self-service, and normal-user registry action icons.

## Battery Family Taxonomy

The user-facing product hierarchy is:

1. Battery Family
2. Battery version

Battery Family display values remain:

- Compact 7M
- Compact 13M
- Core

Each Battery Family can have several Battery versions. Battery Family is not editable by external API after a passport is created. Battery version can be changed by API within the passport's current Battery Family.

Internal classes, services, collections, and paths may keep `ProductTemplate`, `Product`, and `productId` naming for this implementation to avoid a broad code rename. The UI, help text, tester documentation, and admin labels should use Battery Family.

## Identity Fields

User-facing pages, forms, and registry/admin tables should remove Battery Name/display name and Model Number. The primary user-facing identity fields become:

- Battery Family
- Battery version
- Battery serial number
- Passport ID where needed for technical traceability

Existing stored display/model fields can remain in data for backward compatibility, but they should not drive user-facing identity.

## Software Parameters

Software version, release date, and latest update are no longer defining variants. They become ordinary parameters stored on the Battery version.

The implementation should remove:

- separate software-version lists as a hierarchy under Battery versions
- software-version push workflows
- software-version classification/filtering as a variant concept
- external API software parameter updates as a standalone endpoint or write behavior

Battery version push remains. When a Battery version is pushed or changed, its software parameters are applied along with the rest of the Battery version template values.

## Battery Version Changes

Admin UI:

- Admins can edit Battery Families and Battery versions.
- Push to batteries is available at Battery version level.
- A push updates matching batteries safely, preserving battery-specific identity and operational fields.
- Affected passports require validation/signing after push.

External API:

- A Read + write token can change the Battery version only within the current Battery Family.
- A Read + write token cannot change Battery Family.
- Changing Battery version applies the Battery version template safely.
- The response must state that validation/signing is required.
- Telemetry updates remain operational and do not require signing.

Preserved fields during Battery version changes include:

- passport ID
- cluster assignment
- Battery Family
- Battery serial number
- facility
- manufacturing date
- operational telemetry and telemetry history

## Passport And Battery Status

The UI should use two explicit labels:

- Battery status: the general/status battery parameter.
- Passport status: Draft, Signed, Published, or Archived.

The previous requirement to show the passport lifecycle in the hero still applies, but it should use the label Passport status. Battery status remains in the fields and should not be used as the top hero lifecycle badge.

The original image filename or image alt helper text should not appear as prominent text above the passport identity. The image can remain visual.

## Publish Lifecycle

First-time publish flow:

1. Validate
2. Sign
3. Explicit Publish

After the first explicit publish, the passport is considered published before forever. Store this as a durable historical marker. Later successful signing in the app or API automatically publishes the passport again, even if edits had moved the current lifecycle back to Draft or Signed.

Archived passports remain hidden outside admin workflows.

## API Tokens

Existing API tokens remain the credential system. One token can be linked to multiple clusters.

Token modes:

- Read
- Read + write
- Sign

Sign tokens:

- are scoped to linked clusters
- can call only API validate/sign endpoints
- cannot call general read/write endpoints

Read + write tokens:

- can write operational data that is already supported, such as telemetry
- can change Battery version within the current Battery Family
- cannot update software parameters directly
- cannot sign

Token deletion should mean deactivation, preserving token records for audit/history.

Global admins can create, regenerate, deactivate, and scope all token modes. Local admins can create, regenerate, and deactivate all token modes for clusters they administer.

## Battery Secrets Removal

Battery secrets are no longer needed. The app should:

- remove battery secret UI
- remove battery secret routes/actions
- remove battery secret validation from external API authorization
- stop seeding or generating battery secrets
- delete or drop existing `batterySecrets` data during initialization/reset/cleanup

API tokens linked to clusters are the only external API credential mechanism.

## API Validate And Sign

Add external API endpoints for validate and sign.

Recommended endpoints:

- `POST /api/external/v1/batteries/{passportId}/validate`
- `POST /api/external/v1/batteries/{passportId}/sign`

Both endpoints require a valid Sign token scoped to the passport's cluster. Validate should run the same readiness/validation logic as the app. Sign should perform the same signing operation as the app.

Sign behavior:

- If validation/signing fails, return a structured error with blockers.
- If signing succeeds and the passport was never published before, status becomes Signed and first-time Publish remains app-only.
- If signing succeeds and the passport was published before, the passport auto-publishes.

## Access Levels

Roles and visibility:

- Normal User: cluster-scoped. Can see only published passports in assigned clusters.
- Notified Body: global read-only. Can see signed or published passports in all clusters. Cannot see Trust & conformance.
- Market Surveillance Authorities: global read-only. Can see signed or published passports in all clusters. Can see Trust & conformance.
- Commission: global read-only. Can see draft, signed, and published passports in all clusters. Can see Trust & conformance.
- Person with Legitimate Interest: global read-only. Can see signed or published passports in all clusters. Cannot see Trust & conformance.
- Local Admin: same cluster-admin behavior as now.
- Global Admin: same global-admin behavior as now.

Archived passports remain hidden for all non-admin registry/report workflows.

Role labels should use the exact display labels above. Internal role keys can be normalized to stable machine names.

## Registry And Report Actions

Normal User registry rows should show two icon-only actions per battery:

- Summary report
- Detailed report

The icons must have accessible labels and hover/title text:

- `Summary report`
- `Detailed report`

The same clear distinction can be used for other roles where both views are available.

## Admin Guardrails Kept From Previous Design

Administration passport list:

- compact icon actions for Edit, Conformance, Archive, and Unarchive
- confirmation before archiving
- unarchive action visible for archived rows
- Archived rows visible in admin listing

Cluster deletion:

- confirmation before delete
- normal delete blocked when linked passports or memberships exist
- force delete requires explicit confirmation phrase and clears linked passport cluster assignments plus cluster memberships

User/account management:

- local admins cannot change their own cluster role or remove their own membership
- normal users can update their own email, display name, and password through Account
- changing email updates user record, memberships, and refreshes session

Header identity:

- show customer display name and role under the email/name row
- role display should use strongest role label

## Documentation Requirements

Documentation must explain:

- Battery Family is the user-facing name for internal Product Template.
- Battery version is the version level under Battery Family.
- Software version is only a parameter on Battery version.
- Battery secrets have been removed.
- API tokens can be scoped to multiple clusters.
- Sign tokens are used only for API validate/sign.
- The publish lifecycle and auto-publish-after-first-publish rule.
- The new access roles and visibility rules.

## Testing Strategy

Tests should cover:

- Battery Family/Battery version labels in admin, registry, passport, and help views.
- Model Number and Battery Name removed from user-facing identity views.
- Battery serial number label.
- Software version no longer acting as a hierarchy or push target.
- Battery version push dirties passports and requires validation/signing.
- API Battery version change preserves identity/telemetry and requires signing.
- Battery secrets removed from UI, routes, API auth, initialization, and reset.
- token modes include Sign and token deactivation.
- local-admin token creation/deactivation scoped to administered clusters.
- API validate/sign with Sign token.
- first-time sign does not publish automatically, but later sign auto-publishes after first explicit publish.
- role visibility for Normal User, Notified Body, Market Surveillance Authorities, Commission, Person with Legitimate Interest, Local Admin, and Global Admin.
- Trust & conformance visibility only for Market Surveillance Authorities, Commission, Local Admin, and Global Admin.
- icon-only registry actions with hover/accessibility text.
- earlier admin guardrails for archive, unarchive, cluster deletion, and account self-service.

## Non-Goals

- Renaming all internal `ProductTemplate*` code and MongoDB collection names.
- Allowing external API to change Battery Family.
- Allowing Sign tokens to read or write general API data.
- Reintroducing battery secrets.
- Showing archived passports outside admin workflows.
