# Real Schema Validation And Publish Policy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace friendly validation flags with real schema validation and policy gates so draft saves are always safe but publish/verified status cannot be faked.

**Architecture:** Add a focused in-repo JSON schema validator for the generated BatteryPass schemas, integrate it into `PassportValidationService`, and add a publish policy service that centralizes sign/publish readiness. Controllers and views then consume the policy instead of trusting form or API status fields.

**Tech Stack:** ASP.NET Core MVC, C# 13 / .NET 10, MongoDB `BsonDocument`, `System.Text.Json`, xUnit.

---

## File Structure

- Create `web/Services/JsonSchemaValidationService.cs`: validates `BsonDocument` payloads against local schema files with `$ref`, `allOf`, `type`, `properties`, `required`, `enum`, `pattern`, numeric bounds, and array `items`.
- Create `web/Services/PassportPublishPolicyService.cs`: answers whether a validation summary can sign, whether a passport can publish, and normalizes blocked publish requests.
- Modify `web/Services/PassportValidationService.cs`: call the schema validator for each present aspect payload and return blocking schema errors, warnings, and passed checks.
- Modify `web/Program.cs`: register the new services.
- Modify `web/Controllers/AdminController.cs`: save drafts without setting fake `validation.isValid`, block direct published status, and keep draft edits.
- Modify `web/Controllers/PassportsApiController.cs`: sanitize incoming passport documents so direct API create/update cannot publish or mark verified.
- Modify `web/Views/Admin/EditPassport.cshtml`: remove direct `Published` selection and guide admins to validate/sign/publish flow.
- Modify `web/Views/Admin/Conformance.cshtml`: show policy readiness: save allowed, can sign, can publish, and why publish is blocked.
- Test `BatteryPassWeb.Tests/JsonSchemaValidationServiceTests.cs`: schema validator red/green coverage.
- Test `BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs`: policy red/green coverage.
- Modify `BatteryPassWeb.Tests/PassportValidationServiceTests.cs`: prove official schemas produce blocking errors and warnings do not block signing.
- Test `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`: source/layout checks for no fake verified/published shortcuts.

## Task 1: JSON Schema Validator

**Files:**
- Create: `web/Services/JsonSchemaValidationService.cs`
- Test: `BatteryPassWeb.Tests/JsonSchemaValidationServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create tests for a temporary schema with `required`, nested object `required`, enum, minimum, array item validation, and local `$ref`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter JsonSchemaValidationServiceTests`

Expected: build fails because `JsonSchemaValidationService` does not exist.

- [ ] **Step 3: Implement minimal validator**

Implement recursive JSON schema validation against `BsonValue` and return `TrustValidationIssue` instances. Missing/unreadable schemas return a warning because demonstrator schemas may be absent.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter JsonSchemaValidationServiceTests`

Expected: all schema validator tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Services/JsonSchemaValidationService.cs BatteryPassWeb.Tests/JsonSchemaValidationServiceTests.cs && git commit -m "feat: add json schema validation service"`

## Task 2: Passport Validation Integration

**Files:**
- Modify: `web/Services/PassportValidationService.cs`
- Modify: `web/Program.cs`
- Modify: `BatteryPassWeb.Tests/PassportValidationServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Update tests so an incomplete official aspect payload returns blocking schema errors, while a passport with only warnings and no blocking errors still has `CanSign == true`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportValidationServiceTests`

Expected: incomplete official payload currently passes because only payload presence is checked.

- [ ] **Step 3: Integrate validator**

Inject `JsonSchemaValidationService` into `PassportValidationService`, validate each present aspect payload against its schema, and keep missing schema as a warning.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportValidationServiceTests`

Expected: validation tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Services/PassportValidationService.cs web/Program.cs BatteryPassWeb.Tests/PassportValidationServiceTests.cs && git commit -m "feat: validate passports against battery pass schemas"`

## Task 3: Publish Policy

**Files:**
- Create: `web/Services/PassportPublishPolicyService.cs`
- Test: `BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Test that blocking errors prevent signing, warnings do not prevent signing, publish is blocked without a current proof, and publish is allowed only when validation is clean and current proof metadata is present.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportPublishPolicyServiceTests`

Expected: build fails because `PassportPublishPolicyService` does not exist.

- [ ] **Step 3: Implement policy service**

Implement `CanSign`, `CanPublish`, `NormalizeRegistryStatus`, and `SanitizeTrustClaimsForDraftSave`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter PassportPublishPolicyServiceTests`

Expected: all policy tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Services/PassportPublishPolicyService.cs BatteryPassWeb.Tests/PassportPublishPolicyServiceTests.cs && git commit -m "feat: add passport publish policy service"`

## Task 4: Controller And View Gates

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/PassportsApiController.cs`
- Modify: `web/Views/Admin/EditPassport.cshtml`
- Modify: `web/Views/Admin/Conformance.cshtml`
- Test: `BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs`

- [ ] **Step 1: Write failing tests**

Add source/layout tests that prove fake `validation["isValid"] = true` is gone, admin form no longer exposes direct `published` selection, API calls sanitize trust claims, and conformance explains the publish/sign policy.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter ValidationPolicyLayoutTests`

Expected: tests fail because direct published selection and fake validation flags still exist.

- [ ] **Step 3: Implement gates**

Inject `PassportPublishPolicyService`, force draft/archived-only direct status edits, sanitize proof fields on draft saves, and block API direct publish with `400`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryPassWeb.Tests\BatteryPassWeb.Tests.csproj --filter ValidationPolicyLayoutTests`

Expected: policy layout tests pass.

- [ ] **Step 5: Commit**

Run: `git add web/Controllers/AdminController.cs web/Controllers/PassportsApiController.cs web/Views/Admin/EditPassport.cshtml web/Views/Admin/Conformance.cshtml BatteryPassWeb.Tests/ValidationPolicyLayoutTests.cs && git commit -m "feat: enforce validation publish policy"`

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
