# Phase 6A End-to-End Demo Hardening And Acceptance Pack Design

Date: 2026-05-07

## Goal

Phase 6A makes the trust demonstrator reliable for a first-time demo. The app already has schema validation, signing, revisions, audit events, secure documents, QR lookup, conformance guidance, and dirty-state tracking. The remaining risk is not feature absence; it is demo drift.

The phase answers one operational question:

> Can we reset the app to known demo states and prove the full trust workflow works from start to finish?

## Current Context

The app already includes:

- seeded sample passports and accounts,
- `ExternalApiInitializer` for sample external API data,
- `DemoRequiredDataCompletionService` for filling schema-required demo data,
- validation, signing, publish, audit, revision, QR, document access, readiness, and guidance tests,
- an end-user testing guide with Phase 5A guided-readiness checks.

Phase 6A builds on these pieces instead of adding a separate browser automation stack.

## Scope

Phase 6A covers:

- a small catalog of official demo passport scenarios,
- safe reset tooling for known demo passports only,
- acceptance tests for trust workflow contracts,
- documentation with exact passport IDs, accounts, steps, and expected outcomes,
- light UI/admin affordances only if needed to make reset and testing understandable.

## Non-Goals

- No Playwright/Selenium browser test stack in this phase.
- No production data migration system.
- No legal certification workflow.
- No weakening of required-data validation.
- No reset operation that can overwrite arbitrary user-created passports.
- No new visual redesign beyond small clarity fixes found during hardening.

## Demo Scenario Catalog

The reset flow should prepare known demo passports for these states:

1. **Published trusted**: complete, validated, signed, clean, published, public-searchable, QR-resolvable.
2. **Draft incomplete**: missing required data, validation blocked, cannot sign.
3. **Ready to sign**: complete and validated with zero blockers, unsigned, sign action available.
4. **Signed unpublished**: complete, signed, clean, not public-searchable until published.
5. **Dirty after edit**: previously signed/published passport with a signed-core edit that requires revalidation/resigning.
6. **Invalid signature**: proof/hash diagnostics show signature failure without exposing secrets.
7. **Restricted document access**: at least one restricted supporting file denies public download and allows authorized access.

The catalog should use stable IDs and be documented in `docs/sample-cluster-test-accounts.md`.

## Safe Demo Reset

Add a deliberate reset operation that only touches known demo IDs.

Preferred shape:

- an admin-only service such as `DemoScenarioResetService`,
- a controller action under admin routes, or a script/command if that fits the existing app better,
- allow-list of scenario passport IDs,
- clear audit event for every reset passport,
- reset should also restore trust fields, registry state, document visibility metadata, and scenario labels.

Safety rules:

- reset refuses unknown passport IDs,
- reset never uses a broad collection wipe,
- reset does not print or store private signing key material,
- reset should be idempotent so running it twice produces the same demo state,
- reset should not mark HTTP telemetry as signed-core data.

## Acceptance Test Pack

Add tests that prove workflow contracts at service/controller/source level.

Required checks:

- incomplete passport cannot sign,
- complete validated passport can sign,
- signed clean passport can publish,
- published trusted passport is publicly visible,
- draft, unsigned, dirty, or invalid-signature passports are not publicly visible,
- QR payload resolves to public summary route for published trusted passports,
- external HTTP telemetry and operations writes do not dirty trust state,
- admin signed-core edit dirties the passport after signing,
- restricted file download without access returns `403` and records an audit event,
- demo reset catalog contains every documented scenario and refuses unknown IDs.

The tests should prefer existing service boundaries and source/layout assertions where full integration would require MongoDB. MongoDB-backed behavior can be covered with repository/service contract tests where the project already has suitable seams.

## Manual Demo Script

Update `docs/end-user-testing-guide.md` with one exact script:

1. Reset demo scenarios.
2. Login as global admin.
3. Open the draft incomplete passport and confirm blockers.
4. Complete required demo data if the scenario is intended to move forward.
5. Validate, sign, and publish.
6. Search publicly for the published trusted passport.
7. Download or scan its QR code and confirm it opens the public summary.
8. Edit a signed-core field, save, and confirm dirty state.
9. Validate, sign, and publish again.
10. Confirm external HTTP telemetry update does not dirty the passport.
11. Confirm restricted document access behavior for public and authorized users.

Each step should include:

- account to use,
- route to open,
- passport ID,
- action to click,
- expected state/message.

## Error Handling

- Reset unavailable because MongoDB is down: show/service-return a clear error and leave existing demo state unchanged.
- Reset blocked for unknown ID: return a safe validation error, not a partial reset.
- Signing failure during reset: do not mark scenario as published trusted.
- Missing demo file: scenario should show missing evidence clearly or recreate metadata without claiming the file exists.
- External API token missing: guide should point to the API token admin tab and sample token setup.

## Testing Strategy

Use test-driven changes.

Test layers:

- `DemoScenarioResetServiceTests` for catalog, allow-list, and state construction.
- `TrustWorkflowAcceptanceTests` for validate/sign/publish/public-visibility decisions.
- `ExternalApiTrustAcceptanceTests` for non-dirty HTTP write guarantees.
- `DocumentAccessAcceptanceTests` for restricted file denial and audit behavior.
- `QrAcceptanceTests` for QR payload/summary route expectations.
- guide/source tests to ensure docs mention exact scenario IDs and expected outcomes.

## Rollout

1. Add the demo scenario catalog and tests.
2. Add safe reset service and admin/script entry point.
3. Add acceptance tests for trust, QR, external API, and document access.
4. Update sample account matrix and end-user testing guide.
5. Run targeted tests, then the web build.
6. Run the reset manually on the demo database only after code verification.

## Acceptance Criteria

- The app has documented demo passport IDs for all Phase 6A scenarios.
- Reset can restore known demo scenarios without touching arbitrary passports.
- The published trusted scenario can be found publicly and opened by QR.
- The incomplete scenario cannot be signed.
- The dirty scenario clearly requires validation/signing again.
- External HTTP writes do not dirty a signed passport.
- Restricted files are denied to unauthorized users.
- The testing guide lets a new tester verify the full workflow without guessing.
