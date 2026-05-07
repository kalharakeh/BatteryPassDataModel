# Phase 6B Evidence Pack And Document Verification Design

Date: 2026-05-07

## Goal

Phase 6B makes supporting documents part of the trust story instead of passive uploads. The app already stores document hashes in GridFS metadata, records document references on the passport, protects private downloads, and signs document references in canonical passport snapshots. The missing piece is a clear evidence view that tells admins whether required supporting files are present, protected, and still match the latest signed revision.

The phase answers one practical question:

> Are this battery's supporting documents complete, access-controlled, and consistent with the latest trusted passport signature?

## Current Context

The app already includes:

- secure document upload and download routes,
- GridFS file metadata with `passportId`, `documentKey`, `visibility`, and `sha256`,
- document references saved back to the passport,
- dirty-state marking when a supporting document changes,
- access-control checks for restricted downloads,
- audit events for denied downloads,
- canonical signed snapshots that include document `url`, `fileId`, `sha256`, `contentType`, and `visibility`,
- conformance/readiness pages for admin trust diagnostics,
- MongoDB-backed required/optional settings for data completion.

Phase 6B builds on these seams instead of introducing document parsing or a separate compliance engine.

## Scope

Phase 6B covers:

- an evidence-pack service that evaluates current document references,
- comparison between current document hashes and the latest signed revision's document hashes,
- evidence status counts and guidance on the admin conformance page,
- upload/replacement audit events for supporting documents,
- validation/readiness integration for required supporting evidence,
- tests and documentation for document evidence workflows.

## Non-Goals

- No PDF/image content parsing in this phase.
- No legal certification of document contents.
- No virus scanning, DLP scanning, retention policy, or production storage migration.
- No public exposure of private document diagnostics.
- No weakening of schema validation or data-completion requirements.
- No separate QR/document access page.

## Design Principles

1. **Evidence is signed by hash.** A document is trusted when the current passport reference hash matches the hash captured in the latest signed revision.
2. **Uploads are mutable, revisions are immutable.** Replacing a document updates the current passport and makes it dirty until the passport is signed again.
3. **Access status is explicit.** Admins can see whether a document is public or restricted; public users only get what access control allows.
4. **Required means required.** Missing required document evidence blocks readiness when the saved data-completion policy marks that document as required.
5. **No fake document validation.** The app verifies reference integrity and access control, not the legal meaning of a PDF or certificate.
6. **Conformance stays privileged.** Full evidence diagnostics remain visible only to general admins or cluster admins for the battery's cluster.

## Evidence Status Model

Add a small set of evidence statuses:

- `missingRequired`: required evidence has no current document reference.
- `missingOptional`: optional evidence has no current document reference.
- `uploadedUnsigned`: a document exists, but no signed revision contains this document hash yet.
- `verified`: current document hash matches the latest signed revision.
- `changedSinceSigning`: current document hash differs from the latest signed revision.
- `externalLinkOnly`: the reference has a URL but no stored file/hash.
- `missingFileReference`: the reference points to a file that cannot be checked.
- `accessRestricted`: document is private/restricted and requires authorization.
- `accessPublic`: document is public.

Status wording in the UI should be user-facing. Internal enum names can be concise, but the admin page should explain the fix:

- upload the missing file,
- replace the broken reference,
- sign the passport again,
- review visibility settings,
- open the document access rules.

## Architecture

### `PassportEvidenceService`

Add a focused read-only service that evaluates document evidence for one passport.

Inputs:

- current passport `BsonDocument`,
- latest signed revision snapshot, when available,
- data-completion policy/requirements,
- optional file metadata lookup results for stored documents.

Outputs:

- `EvidencePackResult`,
- one `EvidenceItemResult` per known document field,
- summary counts for missing required, uploaded unsigned, changed since signing, verified, public, and restricted documents.

The service should not mutate passports. It only explains document evidence state.

### Latest Revision Lookup

Add or reuse an audit/revision helper that returns the latest signed passport revision for a passport ID.

The evidence service compares:

- current path: `app.documents.<documentKey>.sha256`,
- signed path: latest signed revision snapshot `app.documents.<documentKey>.sha256`.

If there is no latest signed revision, uploaded documents are shown as uploaded but unsigned.

### Evidence View Models

Extend conformance models with:

- `EvidencePackViewModel`,
- `EvidenceItemViewModel`,
- evidence counts,
- document label,
- canonical document path,
- required/optional state,
- visibility,
- file ID or URL,
- current hash,
- signed hash,
- status label,
- recommended action.

The view model should avoid exposing private storage details beyond what admins need to diagnose the passport.

## Conformance Page Design

Add an **Evidence readiness** panel to the admin conformance page.

Top of panel:

- required evidence missing,
- changed since signing,
- uploaded unsigned,
- verified documents,
- restricted documents,
- public documents.

Document list:

- document label,
- required/optional chip,
- current evidence status chip,
- visibility chip,
- hash comparison state,
- next action text,
- upload/replace link where available.

The panel should use the same visual language as the Phase 5A conformance page: clean cards, readable status chips, progressive detail, and no blocked disabled buttons.

## Validation And Publish Policy Integration

Evidence affects readiness as follows:

- missing required evidence is blocking when the saved data-completion policy marks that document field required,
- missing optional evidence is informational,
- changed-since-signing evidence contributes to dirty state and requires re-signing before publish,
- uploaded unsigned evidence does not block draft save but prevents the passport from being treated as fully trusted,
- warnings do not override blocking evidence requirements.

The existing save-draft behavior remains unchanged.

## Audit And Revision Behavior

Add an audit event when a document is uploaded or replaced.

Recommended event names:

- `passport.file.uploaded`,
- `passport.file.replaced`,
- existing `passport.file.download.denied` remains unchanged.

Audit details should include:

- passport ID,
- document key,
- visibility,
- file ID,
- SHA-256 hash,
- actor,
- timestamp.

The audit entry must not include private signing keys or secret storage details.

Signed revisions remain the immutable record of which document hashes were trusted at signing time.

## Data Flow

1. Admin uploads or replaces a supporting document.
2. File route computes SHA-256 and stores file metadata.
3. Passport document reference is updated.
4. Passport signed core is marked dirty.
5. Upload/replacement audit event is written.
6. Admin opens conformance.
7. Controller loads the current passport, latest signed revision, validation summary, readiness, and evidence pack.
8. Evidence service compares current document hashes with latest signed revision hashes.
9. Conformance page shows document evidence readiness and the next required action.
10. Admin signs again when validation blockers are resolved.
11. New signed revision captures the updated document hashes.

## Error Handling

- Missing latest signed revision: show uploaded documents as unsigned, not invalid.
- Missing document hash: show a checkable evidence warning and prompt replacement.
- Missing GridFS file for a referenced file ID: show missing file reference and prevent false verified status.
- Access denied: return `403` and write the existing denied-download audit event.
- MongoDB unavailable: show a service error and do not change trust or evidence state.
- Hash mismatch: show changed-since-signing with current and signed hash diagnostics to admins only.

## Testing

Add tests for:

- required document missing creates a blocking evidence item,
- optional document missing does not block readiness,
- current hash equals signed hash creates verified evidence,
- current hash differs from signed hash creates changed-since-signing evidence,
- uploaded document without a signed revision is uploaded unsigned,
- external URL without hash is external-link-only,
- upload writes document hash, updates passport reference, dirties signed core, and records upload/replacement audit event,
- restricted download still returns `403` for unauthorized users and writes audit,
- conformance view renders evidence counts and document status chips,
- public summary does not expose privileged evidence diagnostics.

## Documentation

Update the admin help and end-user testing guide with:

- what evidence readiness means,
- how to upload or replace supporting documents,
- why replacing a document makes the passport dirty,
- how to sign again so the new document hash becomes trusted,
- how restricted and public documents behave,
- how to verify document evidence in the conformance page.

## Rollout

1. Add evidence result models and service tests.
2. Add latest signed revision lookup helper if needed.
3. Wire evidence evaluation into the admin conformance controller.
4. Render the evidence readiness panel.
5. Add upload/replacement audit events.
6. Integrate required evidence with readiness/validation messaging.
7. Update admin help and end-user testing guide.
8. Run targeted tests, full test suite, and web build.

## Acceptance Criteria

- Admins can see whether every required supporting document is missing, unsigned, changed, verified, public, or restricted.
- Replacing a document clearly makes the passport dirty until it is validated and signed again.
- Signed revisions remain the source of truth for trusted document hashes.
- Required missing evidence blocks readiness according to the saved MongoDB data-completion policy.
- Public users cannot see privileged evidence diagnostics or restricted files.
- Upload/replacement actions create audit events.
- Full test suite and web build pass.
