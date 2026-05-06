# Battery Pass Trust Upgrade Design

Date: 2026-05-06

## Goal

Upgrade the ASP.NET Core MVC Battery Pass demonstrator from a useful passport and operations portal into a trust-centered demonstrator that can show whether a battery passport is valid, signed, traceable, and understandable to public users, administrators, and external API integrators.

The approved delivery approach is **Trust Spine First**:

1. Add validation, trust state, and dirty tracking.
2. Add signing, verification, revisions, and audit.
3. Add secure file access and document hashing.
4. Add QR lookup and QR generation.
5. Add conformance dashboard and admin guidance.
6. Expand tests and sample data.

## Current App Context

The app is an ASP.NET Core MVC application in `web/` with MongoDB storage. It already includes:

- public home/search and passport summary pages,
- authenticated detailed passport views,
- global admin and cluster admin pages,
- MongoDB passport, cluster, user, token, secret, and telemetry repositories,
- token and battery-secret protected external HTTP API,
- GridFS upload/download endpoints,
- telemetry history charts,
- demo cookie authentication and role-based access.

The main gap is that "verified" currently behaves like a display/status flag rather than a proof-backed trust state. The upgrade makes validation, signing, revisions, audit, and file permissions explicit.

## Approved Scope

- Schema validation for canonical BatteryPass aspect payloads.
- Practical business validation for app-specific readiness.
- Passport dirty tracking for signed canonical data.
- Real cryptographic signing and verification for passport snapshots.
- Immutable signed/published revisions.
- Append-only audit events.
- File visibility and passport/cluster access checks.
- Document file hashing for signed supporting documents.
- QR image upload scanning, live camera scanning, and QR generation.
- Admin conformance dashboard.
- Admin form guidance, validation results, and public verification panels.
- Tests for trust, API, file, QR, and UI flows.

## Out Of Scope

- Accredited legal compliance certification.
- A full production trust registry.
- External OIDC/Cognito login.
- Production-grade key management service integration.
- Treating external telemetry as part of the signed passport declaration.

## Core Design Principle: Two Data Lanes

The app must separate the signed passport declaration from mutable operational data.

### Signed Passport Core

The signed passport core contains administrator-maintained canonical data:

- `passportId`
- `schemaVersions`
- stable `registryInfo` identity/status fields
- canonical BatteryPass aspect payloads
- signed supporting document references and file hashes
- visibility rules for signed content

Admin edits to signed core data make the current passport working copy dirty until it is validated and signed again.

### Live Operational Layer

The live operational layer contains mutable operational data:

- telemetry history,
- latest live telemetry,
- external HTTP operations writes,
- live location/use fields updated through the external API,
- contact person updated through the external API,
- cluster-scoped operational fields,
- UI chart caches and other derived display helpers.

External HTTP requests must **not** make the passport dirty. They update live operational views and audit history only.

## Trust Lifecycle

Canonical passport workflow:

1. Admin edits signed core fields.
2. Passport becomes dirty if it had an existing trusted signature.
3. Admin validates the passport.
4. If there are no blocking errors, the passport can be signed.
5. Signing creates a proof and immutable revision snapshot.
6. Published views display the latest trust state and proof metadata.

External HTTP workflow:

1. External client sends token-authenticated request.
2. App checks token scope and active battery secret.
3. App writes telemetry or live operational data.
4. App writes audit event.
5. Passport signed state remains unchanged.

## Trust State Model

Each passport should expose trust state separate from registry status.

Registry lifecycle:

- `draft`
- `published`
- `archived`

Trust lifecycle:

- `unvalidated`: no recent validation result.
- `invalid`: blocking validation errors exist.
- `valid`: latest validation has no blocking errors but no current signature.
- `signed`: current signed core matches latest proof.
- `dirty`: signed core changed after the latest trusted signature.
- `signatureInvalid`: proof verification failed.

The current passport document stores lightweight trust summary fields and pointers. Immutable details live in `passportRevisions` and `auditEvents`.

## Backend Units

### SchemaRegistryService

Responsibilities:

- Map app aspect keys to repository schema files.
- Load schema content from `BatteryPass/io.BatteryPass.*`.
- Expose schema version and source path.
- Expose field metadata where available for admin guidance.
- Report missing or unusable schemas as validation warnings unless the section is mandatory for publish.

### PassportValidationService

Responsibilities:

- Validate canonical aspect payloads against available schemas.
- Run business checks that schemas do not fully express.
- Return section-level results with blocking errors, warnings, and passed checks.
- Persist the latest validation summary when requested.

Blocking examples:

- missing `passportId`,
- missing or duplicate `registryInfo.registryId`,
- invalid JSON/object shape for a canonical aspect,
- impossible numeric values such as negative mass or percentages outside expected ranges,
- document references that point to inaccessible or missing required files,
- invalid registry status transition,
- malformed public summary URL or QR payload.

Warning examples:

- recommended fields are missing,
- optional documents are missing,
- schema metadata is unavailable,
- telemetry is stale,
- battery image is missing or fallback,
- a section is valid but thin for a polished demo.

### PassportTrustService

Responsibilities:

- Build the signed snapshot from the signed passport core only.
- Exclude live operational data and volatile database fields.
- Canonicalize JSON deterministically.
- Compute SHA-256 hash.
- Sign with Ed25519.
- Verify by recomputing the canonical snapshot and checking public key material.
- Produce DID/VC-style proof metadata for display.

Proof shape:

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

The first implementation may use local demo keys from environment variables or local uncommitted secrets. Private keys must not be committed.

### AuditRevisionService

Responsibilities:

- Create immutable revision snapshots on signing/publish.
- Append audit events for meaningful actions.
- Provide revision history and audit history to admin and verification views.

Audit examples:

- admin edit saved,
- cluster-admin operational edit saved,
- validation run,
- signing succeeded or failed,
- passport published,
- passport archived,
- file uploaded,
- file download denied,
- external telemetry written,
- external operations patched,
- QR generated or scanned if useful for diagnostics.

## MongoDB Collections

Existing collections remain in use. New or expanded collections:

### `passports`

Mutable working copy. Add or normalize:

- `trust.state`
- `trust.isDirty`
- `trust.lastValidatedAt`
- `trust.lastSignedAt`
- `trust.latestRevisionId`
- `trust.latestHash`
- `trust.latestProof`
- `trust.validationSummary`

### `passportRevisions`

Immutable signed/published snapshots:

- `revisionId`
- `passportId`
- `revisionNumber`
- `snapshot`
- `snapshotHash`
- `proof`
- `schemaVersions`
- `signedBy`
- `signedAt`
- `publishedAt`
- `createdAt`

### `auditEvents`

Append-only events:

- `eventId`
- `passportId`
- `eventType`
- `actor`
- `actorRole`
- `source`
- `message`
- `metadata`
- `createdAt`

### `didDocuments` / `verificationKeys`

If not already present, add local-compatible DID document and verification key metadata collections:

- public key material,
- verification method ID,
- issuer DID,
- active/revoked state,
- created/updated timestamps.

Private keys stay outside committed source and outside MongoDB unless explicitly demo-only and encrypted.

## Validation And Publish Policy

Save draft:

- always allowed for global admins;
- local cluster admins may save only allowed operational/local fields.

Validate:

- allowed for admins;
- returns blocking errors, warnings, and passed checks;
- never discards draft edits.

Sign:

- allowed only when the latest validation has zero blocking errors;
- warnings do not block signing;
- creates or updates current proof and writes an immutable revision.

Publish:

- allowed only with zero blocking errors and a current valid signature;
- sets registry status to `published`;
- points the current passport to the latest revision.

Warnings are allowed because this is a demonstrator and may contain partial sample data. Blocking errors exist to prevent fake verified states.

## Dirty Tracking Rules

Dirty when:

- a global admin changes signed core fields after signing,
- a signed supporting document reference changes,
- a signed supporting document file hash changes,
- schema version mapping for signed payload changes.

Not dirty when:

- external HTTP telemetry is written,
- external HTTP operations are patched,
- telemetry history changes,
- latest live telemetry changes,
- cluster assignment changes,
- API token or battery secret changes,
- audit events are appended,
- UI cache/chart fields are regenerated.

Cluster-admin local edits should be classified by field. Local edits to live operational fields do not dirty the signed core. If a local field is promoted into the signed snapshot in a later version, that promotion must be explicit.

## File Access And Document Hashing

GridFS files should include metadata:

- `passportId`
- `documentKey`
- `aspect`
- `fieldPath`
- `visibility`: `public` or `restricted`
- `linkedToSignedSnapshot`
- `sha256`
- `uploadedBy`
- `uploadedAt`
- `contentType`

Download rules:

- public files can be downloaded by public users,
- restricted files require login and passport/cluster access,
- admins can access all files,
- denied downloads return `403` and write an audit event,
- missing files return `404`.

Signed document references should include file ID and SHA-256 hash in the signed snapshot when they support a signed declaration.

## QR Design

QR features:

- generate QR for each passport public summary URL,
- optionally include DID payload where useful,
- show QR preview/download in admin passport pages,
- add QR image upload scanning to the public home/search page,
- add live camera scanning where browser permissions allow,
- keep manual DID search visible as fallback.

QR error handling:

- invalid QR payload shows retry and manual search,
- unknown passport shows not-found page,
- camera permission denial falls back to upload/manual search.

## Conformance Dashboard

Add an admin conformance view for each passport.

Recommended route:

- `/admin/passports/{passportId}/conformance`

Dashboard sections:

- overall readiness state,
- blocking errors count,
- warnings count,
- section-by-section validation cards,
- signature/proof status,
- dirty state,
- latest revision details,
- signed document status,
- QR status,
- next recommended actions.

Actions:

- validate,
- sign,
- publish,
- open public summary,
- download QR,
- open audit history,
- open revision history.

## Admin Guidance

Admin forms should show:

- required/optional markers,
- field descriptions from schema/docs where available,
- inline validation errors,
- warnings that do not block saving,
- section badges for valid/warning/error states,
- explanation when a save makes a passport dirty,
- clear distinction between signed core fields and live operational fields.

## Public Verification Panel

Public summary and authenticated detail pages should show:

- verified, unverified, dirty, or invalid signature state,
- issuer DID,
- verification method,
- hash fingerprint,
- signed date,
- revision number,
- validation warning summary where appropriate,
- explanation when live telemetry is newer than the signed passport snapshot.

## API And Route Additions

Recommended route additions:

- `POST /api/passports/{passportId}/validate`
- `POST /api/passports/{passportId}/sign`
- `POST /api/passports/{passportId}/publish`
- `GET /api/passports/{passportId}/verify`
- `GET /api/passports/{passportId}/qr`
- `GET /admin/passports/{passportId}/conformance`
- `GET /admin/passports/{passportId}/audit`
- `GET /admin/passports/{passportId}/revisions`

Existing external API endpoints continue to operate. Their telemetry and operations writes must append audit events but must not dirty signed passport state.

## Error Handling

- Validation failures keep draft edits and return field-level errors.
- Signature mismatch shows invalid signature diagnostics without exposing secrets or private key material.
- Missing schema produces warning unless a mandatory publish check depends on it.
- File download denial returns `403` and writes an audit event.
- QR scan failure returns retry/manual fallback.
- MongoDB unavailable returns a service error and avoids changing trust state.
- Signing is transactional at the logical service level: if revision write fails, trust state must not claim a successful signature.

## Testing

Minimum coverage:

- schema registry loads expected schema mappings,
- validation result mapping returns blocking errors and warnings,
- publish gate blocks signing on blocking errors,
- warnings do not block signing,
- admin canonical edits dirty signed passport state,
- external HTTP telemetry writes do not dirty signed passport state,
- external HTTP operations writes do not dirty signed passport state,
- signing and verification round trip,
- tampered signed snapshot fails verification,
- revision snapshot is immutable by service behavior,
- audit events are written for edits, validation, signing, publishing, uploads, denied downloads, and external writes,
- file visibility checks allow/deny correctly,
- QR generation payload resolves to public summary,
- QR parser handles URL and DID payloads,
- public verification panel renders trust states,
- admin conformance dashboard renders blocking errors, warnings, actions, and revision/proof state.

## Implementation Phases

### Phase 1: Trust State And Validation

- Add trust state fields and dirty tracking helpers.
- Add schema registry service.
- Add validation service and result models.
- Add validate endpoint and admin conformance skeleton.
- Ensure external HTTP writes do not dirty trust state.

### Phase 2: Signing, Verification, Revisions, Audit

- Add canonical snapshot builder.
- Add signing and verification service.
- Add DID/key metadata support.
- Add revision collection writes.
- Add audit event collection writes.
- Add sign, publish, verify endpoints.

### Phase 3: Secure Files

- Add file metadata hashing.
- Enforce public/restricted download checks.
- Link document hashes into signed snapshots.
- Add audit events for uploads and denied downloads.

### Phase 4: QR

- Add QR generation endpoint.
- Add QR preview/download in admin.
- Add QR upload and camera scanning to home/search.
- Add QR parser tests.

### Phase 5: Dashboard And Guidance Polish

- Complete conformance dashboard.
- Add inline admin validation guidance.
- Add public verification panel.
- Refine copy and sample data.

### Phase 6: Test Hardening

- Add unit and integration coverage for trust, file, API, QR, and UI flows.
- Run full test suite and fix regressions.

## Acceptance Criteria

- A passport cannot be signed or published with blocking validation errors.
- A passport can be signed and verified with real cryptographic proof.
- Editing signed canonical data makes the passport dirty.
- External HTTP telemetry and operations writes do not make the passport dirty.
- Published passports have immutable revision snapshots.
- Admin and external operations create audit events.
- Restricted files cannot be downloaded without appropriate access.
- Signed supporting files can be checked by stored hash.
- QR lookup works through image upload and camera scan where available.
- Admin users can see conformance status and next actions.
- Public users can understand verification state without reading raw JSON.
