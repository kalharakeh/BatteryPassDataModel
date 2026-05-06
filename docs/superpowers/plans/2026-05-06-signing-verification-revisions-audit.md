# Signing Verification Revisions Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add real cryptographic signing, verification diagnostics, immutable revision snapshots, audit events, and sign/publish routes for Battery Pass trust state.

**Architecture:** Build a deterministic canonical passport snapshot from signed-core fields only, hash it, sign it with a local ECDSA P-256 demo key, and verify it by recomputing the snapshot. Store signed snapshots in `passportRevisions`, append meaningful actions to `auditEvents`, and expose sign/publish/verify actions through admin and API routes.

**Tech Stack:** ASP.NET Core MVC, C# 13 / .NET 10, MongoDB `BsonDocument`, built-in `System.Security.Cryptography.ECDsa`, xUnit.

---

## File Structure

- Create `web/Services/Base64Url.cs`: shared base64url encoding/decoding for proof values and key material.
- Create `web/Services/DemoSigningKeyService.cs`: loads ECDSA P-256 private key from `BATTERY_PASS_SIGNING_PRIVATE_KEY_BASE64` or generates a per-user local demo key outside the repo.
- Create `web/Services/CanonicalPassportSnapshotService.cs`: builds the signed passport core and canonicalizes BSON to stable JSON.
- Create `web/Services/PassportTrustService.cs`: hashes, signs, and verifies canonical snapshots.
- Create `web/Services/AuditRevisionService.cs`: builds and writes immutable revision documents plus append-only audit events.
- Modify `web/Services/PassportPublishPolicyService.cs`: distinguish admin draft invalidation from API trust-claim sanitization so dirty signed passports keep proof diagnostics.
- Modify `web/Services/PassportRepository.cs`: add methods to persist signature trust state, publish status, and validation summary together.
- Modify `web/Models/Trust/TrustModels.cs`: add signing, verification, and revision result models.
- Modify `web/Models/ViewModels/PassportViewModel.cs`: expose latest hash, revision, signed date, issuer, verification method, and proof status.
- Modify `web/Services/PassportViewModelFactory.cs`: map proof metadata into view models.
- Modify `web/Models/ViewModels/ConformanceViewModel.cs`: add verification result and revision/audit links.
- Modify `web/Controllers/AdminController.cs`: add sign, publish, audit, and revision routes and call trust/audit services.
- Modify `web/Controllers/PassportsApiController.cs`: add sign, publish, and verify API routes.
- Modify `web/Controllers/PassportController.cs`: optionally compute public verification state before rendering public pages.
- Modify `web/Views/Admin/Conformance.cshtml`: add sign/publish actions and proof diagnostics.
- Create `web/Views/Admin/Audit.cshtml`: show append-only audit events for a passport.
- Create `web/Views/Admin/Revisions.cshtml`: show immutable revision history for a passport.
- Modify `web/Views/Passport/Summary.cshtml` and `web/Views/Passport/Detail.cshtml`: show public verification panel.
- Add tests in `BatteryPassWeb.Tests/CanonicalPassportSnapshotServiceTests.cs`, `PassportTrustServiceTests.cs`, `AuditRevisionServiceTests.cs`, and `SigningWorkflowLayoutTests.cs`.

## Task 1: Canonical Snapshot And Hashing

**Files:**
- Create: `web/Services/CanonicalPassportSnapshotService.cs`
- Create: `web/Services/Base64Url.cs`
- Test: `BatteryPassWeb.Tests/CanonicalPassportSnapshotServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving canonical snapshots include `passportId`, `registryInfo.registryId`, schema versions, aspect payloads, and app document references while excluding `_id`, `clusterId`, `trust`, `validation`, `app.operations`, live telemetry, and chart caches. Add a second test proving canonical JSON is stable when BSON document field order changes.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter CanonicalPassportSnapshotServiceTests`

Expected: build fails because `CanonicalPassportSnapshotService` does not exist.

- [ ] **Step 3: Implement snapshot builder**

Implement `BuildSnapshot(BsonDocument passport)`, `Canonicalize(BsonValue value)`, and `Sha256Hex(string canonicalJson)`. Sort object keys ordinally, preserve array order, and write JSON with `Utf8JsonWriter`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter CanonicalPassportSnapshotServiceTests`

Expected: all canonical snapshot tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Services/CanonicalPassportSnapshotService.cs web/Services/Base64Url.cs BatteryPassWeb.Tests/CanonicalPassportSnapshotServiceTests.cs && git commit -m "feat: add canonical passport snapshots"`

## Task 2: Signing And Verification Service

**Files:**
- Create: `web/Services/DemoSigningKeyService.cs`
- Create: `web/Services/PassportTrustService.cs`
- Modify: `web/Models/Trust/TrustModels.cs`
- Test: `BatteryPassWeb.Tests/PassportTrustServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving `Sign` returns a proof with `DataIntegrityProof`, `ecdsa-p256-sha256-jcs-2026`, hash, verification method, public key JWK, and proof value. Add tests proving `Verify` passes for the signed passport and fails after canonical aspect data is tampered.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportTrustServiceTests`

Expected: build fails because `PassportTrustService` does not exist.

- [ ] **Step 3: Implement signing and verification**

Use ECDSA P-256 and SHA-256 over the canonical JSON bytes. Store proof metadata without private key material. Support injected test keys and environment/local demo keys for app usage.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportTrustServiceTests`

Expected: signing and verification tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Services/DemoSigningKeyService.cs web/Services/PassportTrustService.cs web/Models/Trust/TrustModels.cs BatteryPassWeb.Tests/PassportTrustServiceTests.cs && git commit -m "feat: add passport signing verification service"`

## Task 3: Audit And Revision Service

**Files:**
- Create: `web/Services/AuditRevisionService.cs`
- Modify: `web/Services/PassportRepository.cs`
- Test: `BatteryPassWeb.Tests/AuditRevisionServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving revision documents deep-clone snapshots, include revision metadata, include proof and hash, and are not affected when the original snapshot is modified later. Add tests proving audit event documents include event ID, actor, source, message, metadata, and created date.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter AuditRevisionServiceTests`

Expected: build fails because `AuditRevisionService` does not exist.

- [ ] **Step 3: Implement audit/revision persistence helpers**

Build revision and audit documents in pure methods and add Mongo write/list/update methods for `passportRevisions` and `auditEvents`. Add repository methods for signature persistence and publish status updates.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter AuditRevisionServiceTests`

Expected: audit/revision tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Services/AuditRevisionService.cs web/Services/PassportRepository.cs BatteryPassWeb.Tests/AuditRevisionServiceTests.cs && git commit -m "feat: add passport revisions audit events"`

## Task 4: Sign Publish Verify Routes And UI

**Files:**
- Modify: `web/Program.cs`
- Modify: `web/Services/PassportPublishPolicyService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/PassportsApiController.cs`
- Modify: `web/Controllers/PassportController.cs`
- Modify: `web/Models/ViewModels/ConformanceViewModel.cs`
- Modify: `web/Models/ViewModels/PassportViewModel.cs`
- Modify: `web/Services/PassportViewModelFactory.cs`
- Modify: `web/Views/Admin/Conformance.cshtml`
- Create: `web/Views/Admin/Audit.cshtml`
- Create: `web/Views/Admin/Revisions.cshtml`
- Modify: `web/Views/Passport/Summary.cshtml`
- Modify: `web/Views/Passport/Detail.cshtml`
- Test: `BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs`

- [ ] **Step 1: Write failing tests**

Add source/layout tests that assert DI registration, admin sign/publish/audit/revision routes, API sign/publish/verify routes, conformance sign/publish buttons, and public proof metadata rendering exist.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter SigningWorkflowLayoutTests`

Expected: tests fail because routes and proof UI do not exist.

- [ ] **Step 3: Implement routes and UI**

Add admin and API workflows that validate first, block signing on blocking errors, write revision before updating current trust state, publish only when verification passes, append audit events, and render proof diagnostics.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter SigningWorkflowLayoutTests`

Expected: signing workflow layout tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Program.cs web/Services/PassportPublishPolicyService.cs web/Controllers/AdminController.cs web/Controllers/PassportsApiController.cs web/Controllers/PassportController.cs web/Models/ViewModels/ConformanceViewModel.cs web/Models/ViewModels/PassportViewModel.cs web/Services/PassportViewModelFactory.cs web/Views/Admin/Conformance.cshtml web/Views/Admin/Audit.cshtml web/Views/Admin/Revisions.cshtml web/Views/Passport/Summary.cshtml web/Views/Passport/Detail.cshtml BatteryPassWeb.Tests/SigningWorkflowLayoutTests.cs && git commit -m "feat: expose passport signing workflow"`

## Task 5: Full Verification

**Files:** all changed files.

- [ ] **Step 1: Run full tests**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj`

Expected: all tests pass.

- [ ] **Step 2: Run web build**

Run: `dotnet build web\BatteryPassWeb.csproj`

Expected: build succeeds.

- [ ] **Step 3: Inspect git status**

Run: `git status --short`

Expected: clean after commits.
