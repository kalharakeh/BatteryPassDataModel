# Battery Pass Demonstrator Clone Design

Date: 2026-04-30

## Goal

Build a faithful local/internal clone of the Battery Pass demonstrator web app, using this repository's Battery Pass data models as the canonical data contract. The app will run locally, use MongoDB Atlas Free as the online database, and include public viewer pages, registry, demo auth, admin forms, GridFS document storage, QR scanning, and DID/VC-style signing and verification.

The reference experience is:

- `https://thebatterypass.io/did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976`
- `https://thebatterypass.io/did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976/summary`

The clone should match the reference layout and behavior closely, including light and dark mode, while using project-owned branding/assets or temporary neutral demo assets.

## Approved Scope

- Faithful demonstrator clone first, not a production platform.
- Local/internal app runtime.
- Online database: MongoDB Atlas Free.
- Full-stack Next.js application with App Router.
- Demo auth with seeded users and role-based access.
- Admin create/edit/delete through section-by-section forms.
- Real cryptographic signing and verification with DID/VC-style proof data.
- Hybrid DID design: local-compatible now, strict public `did:web` ready later.
- GridFS uploads for reports, manuals, certificates, declarations, and supporting files.
- Manual passport lookup, QR image upload scanning, and live camera QR scanning.

## Out Of Scope For The First Build

- Public production deployment.
- External Cognito/OIDC login.
- Paid storage services such as S3/Azure Blob.
- Legal/compliance certification of the signing trust framework.
- Pixel-perfect copying of protected logos or brand assets without permission.

## Architecture

Use a single Next.js TypeScript app.

Main responsibilities:

- Public UI: landing, passport overview, summary/details, registry.
- Admin UI: dashboard, registry management, passport editor, uploads, signing.
- API routes/server actions: CRUD, validation, search, upload/download, signing, verification, DID document serving.
- Data access: MongoDB Atlas through the official MongoDB driver.
- Files: MongoDB GridFS bucket.
- Auth: local demo session cookies and seeded users.

Suggested route structure:

- `/`
- `/registry`
- `/[passportId]`
- `/[passportId]/summary`
- `/admin`
- `/admin/passports`
- `/admin/passports/new`
- `/admin/passports/[id]/edit`
- `/admin/users`
- `/admin/verification`
- `/api/passports`
- `/api/passport-instances/[passportId]`
- `/api/files`
- `/api/sign`
- `/api/verify`
- `/api/did/[...id]`

## Canonical Battery Pass Data

The app-level document is a wrapper. The stored aspect payloads must remain aligned with the repository's generated Battery Pass schemas.

Canonical schemas:

- General Product Information: `BatteryPass/io.BatteryPass.GeneralProductInformation/1.2.0/gen/GeneralProductInformation-schema.json`
- Carbon Footprint: `BatteryPass/io.BatteryPass.CarbonFootprint/1.2.0/gen/CarbonFootprintForBatteries-schema.json`
- Circularity: `BatteryPass/io.BatteryPass.Circularity/1.2.0/gen/Circularity-schema.json`
- Material Composition: `BatteryPass/io.BatteryPass.MaterialComposition/1.2.0/gen/MaterialComposition-schema.json`
- Performance and Durability: `BatteryPass/io.BatteryPass.Performance/1.2.1/gen/PerformanceAndDurability.schema`
- Labels and Certification: `BatteryPass/io.BatteryPass.Labels/1.2.0/gen/Labeling-schema.json`
- Supply Chain Due Diligence: `BatteryPass/io.BatteryPass.SupplyChainDueDiligence/1.2.0/gen/SupplyChainDueDiligence-schema.json`

The UI can expose friendly reference-style section names, but persistence should keep the aspect payloads keyed by canonical aspect identity.

Example MongoDB passport shape:

```json
{
  "passportId": "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976",
  "schemaVersions": {
    "generalProductInformation": "1.2.0",
    "carbonFootprintForBatteries": "1.2.0",
    "circularity": "1.2.0",
    "materialComposition": "1.2.0",
    "performanceAndDurability": "1.2.1",
    "labeling": "1.2.0",
    "supplyChainDueDiligence": "1.2.0"
  },
  "registryInfo": {
    "registryId": "uuid",
    "status": "draft",
    "createdAt": "ISO timestamp",
    "updatedAt": "ISO timestamp",
    "revisionPassportId": "",
    "latestPassportRevision": ""
  },
  "aspects": {
    "generalProductInformation": {
      "payload": {},
      "visibility": "public",
      "verification": {}
    },
    "carbonFootprintForBatteries": {
      "payload": {},
      "visibility": "public",
      "verification": {}
    }
  },
  "hiddenProperties": [],
  "instanceOnlyFields": [],
  "validation": {
    "hash": "",
    "signature": "",
    "proof": {},
    "isValid": false,
    "signedAt": null
  },
  "dataSource": {
    "name": "MongoDB Atlas",
    "instanceUrl": "/api/passport-instances/:passportId"
  }
}
```

The reference-style view model (`general`, `carbonFootprint`, `circularity`, etc.) should be derived from the canonical aspect payloads through mapper functions.

## Database Collections

- `passports`: current passport records and canonical aspect payloads.
- `passportRevisions`: immutable snapshots created on publish/sign or manual revision.
- `users`: seeded demo users with roles.
- `sessions`: demo login sessions.
- `didDocuments`: local-compatible DID documents and public key material.
- `verificationKeys`: metadata for signing keys; private key material stays in local secrets for the demo.
- `auditEvents`: create, edit, delete, upload, sign, verify, login events.
- `passportFiles.files` and `passportFiles.chunks`: GridFS file storage.

Indexes:

- unique `passportId`
- unique `registryInfo.registryId`
- text/search index for passport ID, model, serial, operator/manufacturer display fields
- `registryInfo.status`
- `updatedAt`

## Validation

Use the repo schemas as the validation source. Because the generated schemas use JSON Schema draft-04 and SAMM/OpenAPI-style structures, the implementation should centralize validation behind a schema service rather than spreading direct Ajv calls through the app.

Validation flow:

1. Admin edits a section.
2. The section form posts a canonical aspect payload.
3. The schema service validates the payload against the matching repo schema.
4. Field-level errors are returned to the admin UI.
5. Invalid payloads cannot be published or signed.

The schema service should also expose metadata needed by forms: required fields, enum values, nested object/array structure, and descriptions where available.

## Authentication And Roles

Use demo auth, not external identity providers.

Seeded roles:

- `public`: unauthenticated users.
- `viewer`: can view privileged sections after login.
- `admin`: can manage registry records, users, files, and passport data.
- `issuer`: can sign passport payloads.
- `verifier`: can verify and mark proof status.

Seeded users should use hashed passwords even though this is demo auth. Sessions should be cookie based.

Privileged fields and sections use `visibility` plus `hiddenProperties` rules. Public users see a locked state and login action.

## DID/VC-Style Signing

Use real cryptographic signatures with DID/VC-style proof metadata.

First version:

- Generate or load one or more local issuer/verifier DID key pairs.
- Store public keys in local-compatible DID documents in MongoDB.
- Store private keys in `.env.local` or a local uncommitted secrets file.
- Canonicalize JSON payloads before signing.
- Sign complete passport snapshots and optionally individual sections.
- Verify signatures in public and admin views.

Proof object shape:

```json
{
  "type": "DataIntegrityProof",
  "cryptosuite": "eddsa-jcs-2022",
  "created": "ISO timestamp",
  "verificationMethod": "did:web:local.battery.pass:issuer#acme-key-1",
  "proofPurpose": "assertionMethod",
  "proofValue": "base64url signature"
}
```

The first implementation does not need to be a full accredited VC ecosystem. It must, however, use real canonicalization, real asymmetric signatures, DID-shaped identifiers, resolvable local DID documents, and a clean path to strict public `did:web`.

## File Storage

Use MongoDB GridFS.

Each file includes metadata:

- `passportId`
- `aspect`
- `fieldPath`
- `documentType`
- `contentType`
- `sha256`
- `uploadedBy`
- `uploadedAt`
- `visibility`

Admin forms should support upload, replace, delete, preview/download, and linking uploaded files into canonical aspect payload fields where the model expects document URLs or resources.

## Public Features

Landing:

- Manual passport DID entry.
- QR image upload scanner.
- Live camera QR scanner.
- Sample passport shortcut for demo.

Registry:

- Searchable table.
- Passport ID, registry ID, economic operator, created/updated date, status, actions.

Passport overview:

- Match reference page structure.
- Verification badge.
- Passport ID, model, serial, category, status, weight.
- Manufacturer/operator summary.
- Illustrative battery image.
- Section cards and quick facts.
- Light/dark mode.

Summary/details:

- Match reference section navigation.
- Sections:
  - General
  - Material composition
  - Performance
  - Carbon footprint
  - Circularity
  - Compliance
  - Supply chain
- Locked states for privileged data.
- Verification panels showing issuer DID, method, hash, signature state, and signed date.

## Admin Features

Admin dashboard:

- Registry stats.
- Recent edits/uploads/signing events.
- Quick create/import actions.

Passport editor:

- Section-by-section forms.
- Save draft per section.
- Validate section.
- Publish/revise passport.
- Sign passport or selected sections.
- Verify current signatures.
- Upload/manage attached files.
- Delete or archive passport records.

Forms:

- Prefer schema-driven fields where practical.
- Use custom components for nested arrays/objects and document fields.
- Preserve canonical aspect payload names.
- Show field descriptions from schemas/docs when helpful.

## UI Direction

The UI should closely match the reference web pages:

- same page hierarchy and layout language,
- similar cards, badges, section navigation, dense technical tables/forms,
- light/dark mode toggle,
- green verification accents,
- neutral backgrounds,
- serious registry/admin feel,
- no marketing-style landing hero.

Use project-owned branding/assets or temporary neutral demo assets. Do not copy protected logos, names, or images unless the user provides rights to use them.

## Error Handling

- Unknown passport: show "passport does not exist" and return to search.
- Invalid schema payload: show field-level errors and block publish/sign.
- Missing privileged access: show locked state and login action.
- Upload failure: show retry/delete options and keep previous file reference intact.
- Signature mismatch: mark invalid and show hash/proof diagnostic metadata.
- QR scan failure: allow retry and manual fallback.
- MongoDB unavailable: show app-level service error and log details server-side.

## Testing

Minimum test coverage:

- schema validation service tests,
- canonicalization/sign/verify tests,
- API CRUD tests,
- GridFS upload/download tests,
- demo auth/role guard tests,
- admin form smoke tests,
- public viewer rendering tests,
- QR parsing utility tests.

Use Playwright for key browser flows once the UI exists:

- manual lookup,
- QR image lookup,
- login,
- create passport,
- edit section,
- upload file,
- sign passport,
- public verification display.

## Environment Variables

Expected local configuration:

```text
MONGODB_URI=
MONGODB_DB=battery_pass_demo
APP_URL=http://localhost:3000
SESSION_SECRET=
DEMO_SEED_PASSWORD=
DID_ISSUER_PRIVATE_KEY=
DID_ISSUER_DID=did:web:local.battery.pass:issuer
```

Private keys and secrets must not be committed.

## Acceptance Criteria

- The app runs locally and connects to MongoDB Atlas Free.
- Seed script creates demo users, DID documents, keys metadata, and sample passport records.
- Public pages match the reference structure and include light/dark mode.
- Registry, lookup, QR image scanning, and live camera scanning work.
- Admin can create, edit, delete/archive, upload files, and sign passports through forms.
- Stored aspect payloads match the repo Battery Pass data model schemas.
- Invalid aspect payloads cannot be published or signed.
- DID/VC-style signatures are real and verifiable.
- GridFS files can be uploaded, linked, downloaded, and deleted.
- Privileged data is hidden until demo login.

## Implementation Sequence

1. Scaffold Next.js app and base design system.
2. Add MongoDB connection, schema registry, and seed script.
3. Build demo auth and role guards.
4. Build passport data service and validation service.
5. Build public landing, registry, overview, and summary pages.
6. Build admin registry and section editor.
7. Add GridFS file upload/download.
8. Add DID document routes and signing/verification.
9. Add QR image and live camera scanning.
10. Add tests and sample data polish.
