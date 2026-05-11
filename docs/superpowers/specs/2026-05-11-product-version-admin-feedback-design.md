# Product Version And Admin Feedback Design

Date: 2026-05-11

## Context

Tester feedback found regressions and missing workflow pieces around new passport creation, product template creation, software-version pushes, passport summary access messaging, user password entry, admin navigation, local-admin field permissions, and external credential management.

The largest design change is product versioning. Product templates currently model `Product -> Software versions`. The required target is `Product -> Product/Battery versions -> Software versions`, with all editable template data stored in MongoDB.

## Goals

- Make new passport creation work from the admin UI path.
- Make new product template creation work from empty fields and from an existing template base.
- Store product templates, product versions, software versions, required-field policy, local-admin editable-field policy, API tokens, battery secrets, and reset passports in MongoDB.
- Add product/battery versions distinct from software versions.
- Make product versions newest-first everywhere.
- Make software versions newest-first within each product version.
- Fix push-to-batteries for a selected product version/software version pair, including newly added software versions such as `4.0`.
- Seed one unassigned demonstrator battery plus six clustered customer batteries during template reset.
- Combine API tokens and battery secrets into one `API Token Management` page with two tabs.
- Add password reveal controls to add/update user forms.
- Fix passport summary access notice for users connected to the passport cluster.
- Keep admin navigation on one row after the credential-page merge.
- Move local-admin editable-field configuration to a separate global MongoDB-backed page.

## Non-Goals

- Do not replace MongoDB persistence with hard-coded runtime-only templates.
- Do not redesign the whole admin shell beyond the navigation and page changes needed for this feedback.
- Do not merge battery/product versions with software versions.
- Do not weaken signing, validation, or public visibility rules.

## Data Model

MongoDB becomes the source of truth for product template data.

Product-level data:

- `productId`
- `productName`
- `description`
- `imageUrl`
- `moduleCount`
- audit metadata such as `updatedAt` and `updatedBy`

Product/battery version data:

- `productId`
- `version`
- shared passport parameter set for that product version:
  - battery mass
  - rated energy, capacity, maximum power, nominal voltage
  - expected lifetime and expected cycles
  - supply-chain index
  - carbon footprint and performance class
  - material masses
  - carbon lifecycle stages
  - recycled-content values
  - template documents and evidence references
  - required-field policy for validation
- audit metadata

Software version data:

- `productId`
- `productVersion`
- `version`
- `releaseDate`
- `latestUpdate`
- audit metadata

Passport product metadata:

- `app.product.productId`
- `app.product.productName`
- `app.product.productVersion`
- `app.product.softwareVersion`
- `app.product.softwareReleaseDate`
- `app.product.softwareLatestUpdate`
- `app.product.templateHash`
- `app.templateBaseline`

Existing flat product-template documents are read as product version `1.0` until initialization/reset rewrites them into the nested model. This keeps older data usable during local development and migration.

## Admin Workflows

### Product Templates

The product templates overview remains product-oriented and lists product cards.

Opening a product shows product/battery versions newest-first. General admins can:

- add a product version
- edit a product version
- duplicate a version from an existing product/version/software base
- add or edit software versions within a product version
- push a selected product version/software version pair to matching batteries

Software versions are shown newest-first inside each product version. Adding a software version does not create three default software versions.

### New Product Template

Creating a product template starts with empty fields.

The page also offers optional cascading base controls:

1. existing product
2. product/battery version
3. software version

Selecting a base fills the form from MongoDB for that product/version/software combination. The user can then edit and save it as a new product template.

### New Passport

Creating a passport starts with battery identity fields empty.

The page offers cascading template controls:

1. product
2. product/battery version
3. software version

Defaults are the latest product/battery version for the selected product and latest software version for that product/battery version. Selecting values fills shared template fields while preserving empty identity fields until the admin provides them.

Saving creates a passport with the selected product version and software version metadata.

### Local Admin Editable Fields

Required/optional validation remains product-version-specific.

Local-admin editable fields are configured globally on a separate MongoDB-backed admin page. The cluster-admin edit form uses that global policy to decide which fields are editable.

### API Token Management

API tokens and battery secrets move into one admin page named `API Token Management`.

The page has two internal tabs:

- API tokens
- Battery secrets

The main admin navigation has one credential-related item so the tab row returns to one line.

Help/admin help explains:

- API token values are used by external API clients for authenticated read or read/write API access.
- Battery secrets are per-battery shared credentials used by batteries or telemetry clients when writing battery-specific data.
- Generated token/secret values are shown once and must be stored by the operator.

## Reset Seed Data

Product template reset replaces existing clustered template passports with:

- North cluster:
  - Compact 7M, product version `1.0`
  - Compact 7M, product version `2.0`
- South cluster:
  - Compact 13M, product version `1.0`
  - Compact 13M, product version `2.0`
- Fleet cluster:
  - Core, product version `1.0`
  - Core, product version `2.0`

The existing unassigned/default demonstrator battery remains, also created from the MongoDB template model.

Reset also ensures relevant demo users and memberships exist, including the customer user path needed for North summary/detail testing.

## Bug Fixes

### Passport Summary Access

If a signed-in non-admin user belongs to the passport cluster, the summary page must not show the "Sign in with a user connected to..." detail-access notice.

The `CP7M-NORTH-001` / `customer_001_001@customer.org` / `12345` scenario should allow detailed-report access when the reset/demo data includes that user and membership.

### Push To Batteries

Push targets matching batteries by:

- product ID
- product/battery version
- software version

Push compares each passport against its stored template baseline and updates only fields still matching that baseline. Manual battery-specific overrides remain preserved. Push records an audit event and does not crash when newly added software versions are pushed.

### User Password Reveal

Admin and local-admin add/update user forms render password inputs with an eye/reveal control so the operator can inspect what they typed.

### Current Version Field

Current version fields use the same compact field sizing as neighboring form fields.

### Admin Navigation

The former `API tokens` and `Battery secrets` top-level tabs become one `API Token Management` entry, restoring one-row navigation.

## Testing And Verification

Add failing tests first for:

- product-template catalog/model nesting
- product-version newest-first ordering
- software-version newest-first ordering within product versions
- new passport creation with selected product version and software version
- new product template creation from empty fields
- new product template creation from existing product/version/software base
- push-to-batteries for product version/software version pair, including a newly added software version
- reset seed data: one unassigned demonstrator plus six clustered customer batteries
- summary/detail access for a user connected to North Operations Cluster
- global local-admin editable-field policy
- password reveal controls on admin and cluster-admin user forms
- combined API Token Management navigation/page
- current-version field sizing

Run the existing .NET test suite after changes.

Run the app locally after implementation and verify the admin UI paths as closely as possible:

- create a new passport through the admin page path
- create a new product template through the admin page path
- add a software version and push it to matching batteries
- sign in as the North customer user and verify detail access behavior

If full browser automation is unavailable, document the automated controller/service coverage and any manual verification gap.

## Open Decisions Resolved

- Product versions are nested under products.
- Software versions are nested under product versions.
- Local-admin editable fields are global across all batteries.
- Reset keeps the unassigned demonstrator and creates six clustered customer batteries.
- The recommended implementation approach is one coherent product-version refactor rather than separate bugfix and versioning passes.
