# Battery Pass Guided Readiness Polish Design

Date: 2026-05-07

## Goal

Phase 5A turns the already-working trust workflow into a clear admin experience. The app can validate, sign, publish, track dirty state, protect files, and scan/generate QR codes; the next gap is that admins still have to infer the right next action from many panels, validation rows, and status fields.

The goal is to make each passport answer one practical question:

> What do I need to do next to make this battery complete, trusted, and publishable?

## Current Context

The app already includes:

- real schema and business-rule validation,
- validation/publish policy gates,
- signing, proof metadata, revisions, and audit events,
- dirty-state tracking for canonical admin edits,
- external HTTP telemetry/operations writes that do not dirty the signed core,
- secure document access checks,
- QR generation and QR scan/search fallback,
- data requirement toggles stored in MongoDB,
- an admin conformance page,
- an admin help page,
- admin edit form sections for the main Battery Pass data.

Recent bug fixes also separated manufacturer serial number from Battery Pass identifier so identifier values such as `urn:local:...` do not leak into the serial-number field.

## Scope

Phase 5A covers guided readiness polish only:

- central readiness decision service,
- cleaner conformance dashboard,
- improved admin edit form guidance,
- simplified admin help page,
- end-user testing guide updates,
- regression/layout tests for the guided workflow.

Phase 5A does not add a separate wizard, does not weaken validation, and does not expose conformance details on the public summary page.

## Non-Goals

- No new public validation/conformance section on the summary page.
- No broad redesign of the whole app shell.
- No changes to cryptographic proof semantics.
- No changes to external API trust-dirty rules.
- No replacement of the existing data requirements tab.
- No disabled buttons with "Action blocked" messages. Unavailable actions stay hidden; the page explains the next available action instead.

## Design Principles

1. **One next action.** Admins should see the next available action before seeing diagnostics.
2. **Progressive detail.** Show readiness and blockers first; keep proof hashes, warnings, and raw validation rows collapsible.
3. **No fake readiness.** Full required data remains required. Blocking errors still block signing.
4. **Keep the signed core distinct.** Live HTTP telemetry and operational values remain outside signed readiness.
5. **Privileged trust evidence.** Trust & conformance details remain visible only to general admins or cluster admins for the battery's cluster.
6. **Respect existing pages.** Improve conformance, edit, and help pages rather than creating a new wizard.

## Architecture

### `PassportReadinessService`

Add a focused service that converts the existing trust, validation, verification, and registry state into one decision object.

Inputs:

- passport `BsonDocument`,
- latest `TrustValidationSummary`,
- `PassportPublishDecision` from `PassportPublishPolicyService`,
- `PassportVerificationResult` from `PassportTrustService`,
- data completion status from `DataCompletionPolicyService` where needed.

Output model:

- `StateKey`: `draft`, `incomplete`, `readyToSign`, `signedClean`, `readyToPublish`, `publishedTrusted`, `dirtyNeedsResign`, `invalidSignature`, `serviceError`.
- `StateLabel`: human-readable state.
- `Severity`: `neutral`, `warning`, `blocked`, `ready`, `trusted`.
- `NextActionKey`: `validate`, `completeData`, `editRequiredData`, `sign`, `publish`, `reviewDiagnostics`, `none`.
- `NextActionLabel`: button/action text.
- `NextActionDescription`: short explanation.
- `CanValidate`, `CanCompleteDemoData`, `CanSign`, `CanPublish`.
- `PrimaryReason`: one sentence explaining why this state exists.
- `BlockerCount`, `WarningCount`.
- `TrustIsDirty`, `HasCurrentProof`, `IsPublished`.

The service does not write to MongoDB. It only explains readiness.

### `ConformanceViewModel`

Extend the existing conformance model with:

- `Readiness`,
- grouped blockers by section,
- grouped warnings by section,
- advanced verification diagnostics,
- section summary counts.

The controller remains responsible for loading the document, validation summary, publish decision, and verification result. The readiness service only assembles the decision.

### Admin Field Guidance

Keep guidance data lightweight and practical. Add an in-code catalog or view model mapping for visible admin form fields:

- form field name,
- friendly label,
- canonical data path,
- section,
- short description,
- required/optional state from MongoDB data completion policy,
- signed-core vs operational classification.

This catalog should reuse existing data requirements where possible. If a field is required by the saved MongoDB policy, the edit form marks it as required. Optional fields are marked as optional. Operational fields get a note that they do not dirty signed passport state when changed through external HTTP requests.

## Conformance Page Design

The conformance page becomes a guided readiness page.

Top area:

- passport ID and model,
- readiness state badge,
- one primary next action,
- short explanation of why that action is next,
- quick links: edit passport, admin help, audit trail, revision history.

Readiness strip:

- blocking errors,
- warnings,
- signed proof state,
- dirty/clean state,
- publish visibility.

Main content:

- **Next action card:** shows only the next valid action button. Validate is always available. Complete demo data appears only when missing required demo-fillable data exists. Sign appears only when signing is allowed. Publish appears only when publishing is allowed.
- **Blocking data to fix:** grouped by section with field path, friendly label, what to do, and edit link to that admin section where possible.
- **Completion checklist:** section cards showing complete/incomplete state and required fields still missing.
- **Advanced diagnostics:** warnings, proof/hash details, raw validation table, and verification messages in collapsed sections.

The page should not show unavailable buttons as disabled blocked controls.

## Admin Edit Form Design

The admin edit page remains the main place to fill data, but it should guide the user better.

Per section:

- section header shows required fields remaining,
- fields show `Required` or `Optional`,
- fields include a short description,
- fields with known blocking validation issues get inline error text,
- section-level link opens the conformance page filtered or anchored to that section,
- save button copy explains that draft save is allowed and may make signed core dirty.

For signed-core fields:

- show a subtle note: "Changing this after signing makes the passport dirty until it is validated and signed again."

For operational/live fields:

- show a subtle note: "External HTTP updates to this live operational data do not dirty the passport signature."

## Admin Help Page Design

The help page should become shorter and more task-driven.

Keep:

- create battery ID,
- first-time completion flow,
- validate/sign/publish flow,
- dirty recovery flow,
- status dictionary,
- link to data requirements.

Reduce or move:

- the very long parameter-by-parameter list should be summarized because the data requirements tab is now the authoritative place for required/optional settings.
- detailed field lists should link to the data requirements tab and conformance blockers instead of duplicating everything.

Add:

- "First-time passport" checklist,
- "After edit / dirty recovery" checklist,
- "What public users can see" note,
- "What admins can see" note,
- reminder that Trust & conformance is privileged.

## Data Flow

1. Admin opens conformance.
2. Controller loads passport, data requirements, validation summary, publish decision, and verification result.
3. `PassportReadinessService` computes readiness.
4. Conformance page renders the readiness state and only valid next actions.
5. Admin edits data from the edit page.
6. Save remains allowed.
7. Signed core edits mark the passport dirty.
8. Admin validates again.
9. If blocking errors are zero, sign appears.
10. If current proof is valid and clean, publish appears.

## Error Handling

- Validation failures stay non-destructive and keep draft edits.
- Service/MongoDB signing or publishing failures show a service error and do not claim trust state changes.
- Signature mismatch shows invalid signature and hash diagnostics only in the advanced diagnostics area.
- Missing schemas remain visible as warnings unless current policy treats the field/section as required for publish readiness.
- QR scan failure remains a retry/manual DID fallback.
- File denial remains `403` with audit event.

## Testing

Add unit tests for `PassportReadinessService`:

- incomplete data -> next action is complete or edit required data,
- zero blockers without current proof -> ready to sign,
- signed clean but unpublished -> ready to publish,
- signed clean and published -> published trusted,
- dirty signed passport -> dirty needs re-sign,
- invalid signature -> review diagnostics,
- warnings do not block signing,
- published but dirty is not published trusted.

Add layout/source tests:

- conformance view renders readiness state and one next-action area,
- conformance view does not render disabled blocked buttons,
- advanced diagnostics are collapsible,
- edit form renders required/optional markers and guidance classes,
- help page includes first-time and dirty recovery checklists,
- public summary does not render conformance/validation details.

Update `docs/end-user-testing-guide.md`:

- add Phase 5A guided readiness verification,
- include exact admin testing path for first-time passport and dirty recovery,
- include expected next actions at each state.

## Rollout

1. Add readiness service and tests.
2. Wire readiness into `AdminController.Conformance`.
3. Simplify conformance page layout around readiness.
4. Add admin edit field guidance.
5. Simplify admin help page.
6. Update testing guide.
7. Run targeted tests, full test suite, and web build.

## Acceptance Criteria

- Admin can open conformance and immediately understand the passport state.
- Only currently available workflow action buttons are shown.
- Blocking issues are grouped by section with clear correction guidance.
- Admin edit form clearly shows required and optional fields.
- Help page explains first-time creation and dirty recovery without overwhelming duplication.
- Public summary remains free of validation/conformance details.
- Trust & conformance evidence remains privileged.
- Full test suite and web build pass.
