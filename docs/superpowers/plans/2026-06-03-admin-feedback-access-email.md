# Admin Feedback Access Email Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the admin feedback changes for role access, clean UI affordances, Power Automate password reset, external API discovery/lifecycle permissions, and reset-demo compatibility.

**Architecture:** Keep the existing ASP.NET MVC/MongoDB structure and extend current services instead of introducing a new workflow layer. Use source-level tests already common in `BatteryPassWeb.Tests`, plus focused service/model tests where behavior can be exercised without MongoDB.

**Tech Stack:** ASP.NET Core MVC, Razor views, MongoDB.Driver, BCrypt.Net, built-in SMTP client, xUnit.

---

### Task 1: Guardrail Tests

**Files:**
- Modify: `BatteryPassWeb.Tests/ForgotPasswordFlowTests.cs`
- Create: `BatteryPassWeb.Tests/AdminFeedbackImplementationTests.cs`
- Modify: existing access/API/UI source tests only if they need new assertions.

- [ ] Write tests that fail until password reset uses a real token flow, email sender service, reset GET/POST endpoints, and neutral messaging.
- [ ] Write tests that fail until global report roles are documented in `AccessControlService` as all-cluster read roles, with Commission and Market Surveillance Authority allowed to see drafts.
- [ ] Write tests that fail until Registered Clusters has a user overview dropdown and global Users says `Create User`.
- [ ] Write tests that fail until editable fields have separate cluster-admin visibility and edit switches.
- [ ] Write tests that fail until cluster admins have history and trust action UI routes.
- [ ] Write tests that fail until the external API has `GET /clusters`, `GET /clusters/{clusterId}/batteries`, and a lifecycle token mode.

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter AdminFeedbackImplementationTests`

Expected before implementation: failing assertions for missing code/text.

### Task 2: Access And UI

**Files:**
- Modify: `web/Services/AccessControlService.cs`
- Modify: `web/Services/BatteryTableService.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/wwwroot/css/site.css`

- [ ] Add all-cluster report read helper for Notified Body, Market Surveillance Authorities, Commission, and Person with Legitimate Interest.
- [ ] Let Commission and Market Surveillance Authorities see draft/unpublished passports across all clusters; keep Notified Body and Person with Legitimate Interest to signed/published passports.
- [ ] Show a cluster user overview dropdown in Registered Clusters using existing membership/user data.
- [ ] Rename the global user submit button from `Save user` to `Create User`.

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter AdminFeedbackImplementationTests`

### Task 3: Passport Lifecycle UX

**Files:**
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Models/ViewModels/BatteryViewModels.cs`
- Modify: `web/Models/ViewModels/EditPassportViewModel.cs`
- Modify: `web/Views/Admin/BatteryPassports.cshtml`
- Create: `web/Views/ClusterAdmin/BatteryPassports.cshtml`
- Modify: `web/Views/ClusterAdmin/EditPassport.cshtml`

- [ ] Only allow creating a new passport when no passport exists or `newPassportRequired` is true.
- [ ] Clear `newPassportRequired` after successful UI/API passport creation.
- [ ] Add cluster-admin history route and view.
- [ ] Add validate/sign/publish controls to the cluster-admin edit page and show Battery ID before Passport ID.

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter AdminFeedbackImplementationTests`

### Task 4: Editable Field Visibility

**Files:**
- Modify: `web/Services/EditableFieldPolicyService.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/EditPassport.cshtml`

- [ ] Extend `EditableFieldPermission` with `VisibleToClusterAdmin`.
- [ ] Persist and load the new flag while defaulting older stored policies to visible when locally editable.
- [ ] Use visibility to decide which cluster-admin fields render, and editability to decide whether rendered controls are writable.

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter AdminFeedbackImplementationTests`

### Task 5: Power Automate Password Reset

**Files:**
- Modify: `web/Configuration/BatteryPassOptions.cs`
- Modify: `web/Program.cs`
- Modify: `web/Services/AuthService.cs`
- Create: `web/Services/EmailSender.cs`
- Modify: `web/Controllers/LoginController.cs`
- Modify: `web/Models/ViewModels/LoginViewModel.cs`
- Create: `web/Models/ViewModels/ResetPasswordViewModel.cs`
- Modify: `web/Views/Login/Index.cshtml`
- Create: `web/Views/Login/ResetPassword.cshtml`
- Modify: `web/.env.example`

- [ ] Generate random reset tokens, store only token hashes and expiry metadata.
- [ ] Send reset links through a Power Automate HTTP-triggered flow.
- [ ] Keep forgot-password response neutral for account enumeration safety.
- [ ] Add reset-password GET/POST with password confirmation and one-time token consumption.

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj --filter ForgotPasswordFlowTests`

### Task 6: External API And Reset Data

**Files:**
- Modify: `web/Services/ExternalApiRepository.cs`
- Modify: `web/Services/ExternalApiInitializer.cs`
- Modify: `web/Services/ProductTemplateService.cs`
- Modify: `web/Controllers/ExternalApiController.cs`
- Modify: `web/Controllers/AdminController.cs`
- Modify: `web/Controllers/ClusterAdminController.cs`
- Modify: `web/Views/Admin/Clusters.cshtml`
- Modify: `web/Views/ClusterAdmin/ApiTokens.cshtml`
- Modify: `web/Views/Help/Index.cshtml`
- Modify: docs with Power Automate and API reset notes.

- [ ] Add lifecycle/read-write-sign token mode without changing read, read-write, or sign semantics.
- [ ] Add cluster list and cluster battery ID list endpoints scoped to token access.
- [ ] Seed lifecycle demo token plus all global read-role users during reset.
- [ ] Update API help and demo docs for the new endpoints/token.

Run: `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`

### Task 7: Verification And Commits

- [ ] Commit plan.
- [ ] Commit failing tests.
- [ ] Commit access/UI/lifecycle changes.
- [ ] Commit Power Automate password reset.
- [ ] Run `dotnet test BatteryPassWeb.Tests/BatteryPassWeb.Tests.csproj`.
- [ ] Run `dotnet build web/BatteryPassWeb.csproj`.
- [ ] Start the local app and perform a browser smoke check if MongoDB/dev startup succeeds.
