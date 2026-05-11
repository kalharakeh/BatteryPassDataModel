# Passport Admin UX Guardrails Design

## Context

Recent review feedback identified confusing passport state labels, unclear registry actions, missing archive recovery, risky cluster deletion, and missing self-service account editing. The existing app already has separate admin, cluster-admin, registry, passport summary/detail, and authentication areas, so the design keeps those surfaces and adds targeted clarity and guardrails.

## Goals

- Show one user-facing passport lifecycle state: Draft, Signed, Published, or Archived.
- Keep battery condition separate and label it as Battery status.
- Make administration actions compact, recoverable, and understandable.
- Prevent accidental destructive cluster and membership changes.
- Let normal users manage their own profile details and password.
- Clarify normal-user registry actions.
- Show customer name and role in the top header identity area.

## Passport State Model

The UI should compute a display-only Passport state:

- Archived when `registryInfo.status` is `archived`.
- Published when `registryInfo.status` is `published`.
- Signed when the trust state is signed but the registry status is not published.
- Draft otherwise.

Battery status remains the physical or operational status from the passport data, such as in-use or active-related battery information. It must be labeled as Battery status wherever shown near Passport state.

## Passport Summary And Detail Views

The hero badge above the passport ID should show Passport state instead of Battery status. The summary/detail field grid should include both Passport state and Battery status with explicit labels.

The current top-level image filename or alt-text helper line is not needed above the passport identity area. The image can remain visual, and the data fields remain the source of truth.

## Administration Passport List

The passport table in administration should keep its current structure but make the Actions column narrower:

- Edit is a pen icon button with hover/title text `Edit`.
- Conformance is a compact badge or icon button with hover/title text `Conformance`.
- Archive is an archive/box icon button with hover/title text `Archive`.
- Archived rows show an Unarchive action instead of Archive.

Archive actions require confirmation before posting. Archived records remain visible in admin search/listing but stay inaccessible through normal summary/detail routes.

Model and Updated should have enough width and no forced wrapping where practical, so common values stay on one row.

## Cluster Deletion Guardrails

Deleting a cluster should require confirmation. A normal delete should be blocked when the cluster has linked passports or user memberships.

A separate force-delete path will handle clusters with linked data. It must require an explicit confirmation phrase and clearly state that linked passport cluster assignments and user memberships will also be removed. The force path clears passport cluster links and deletes cluster memberships.

This prevents an admin from deleting every cluster by accident and leaving the local admin workflow unusable.

## User And Role Management

Local admins may create or update users in clusters they administer, change other users' passwords, and elevate or downgrade other users between Normal user and Local admin within those clusters.

Local admins may not change their own cluster role or add themselves as a Normal user to a different cluster through the local admin UI. Global admins retain broader role-management authority from the administration page.

Role labels shown in the UI should use:

- Normal user for `member`.
- Local admin for `clusterAdmin`.
- Global admin for `admin`.

## Self-Service Account Page

Authenticated users need an account/profile page. It should allow them to update:

- Display name.
- Email address.
- Password.

Changing the email should update the user record and the user's cluster memberships, then refresh the current authentication session so the header and authorization checks use the new email.

## Registry Actions

For normal users in the Scania Passport Registry, rows should expose two clear actions:

- Summary opens `/{passportId}/summary`.
- Detailed report opens `/{passportId}`.

These labels replace the ambiguity of a single Summary action when both views exist.

## Header Identity

The authenticated identity block should show the customer display name and role under the email/name row. Example labels:

- `Anna Andersson / Normal User`
- `North Operations / Local Admin`
- `Demo Administrator / Global Admin`

When no display name is available, the email can remain the fallback name. When multiple roles exist, the strongest user-facing role should be shown in this order: Global admin, Local admin, Normal user.

## Testing Strategy

Add focused tests matching the current repository style:

- Passport view-model and view tests for Passport state and Battery status labels.
- Admin passport-list markup tests for icon actions, confirmation, and unarchive.
- Repository/controller tests or source-structure tests for unarchive and cluster deletion guardrails.
- User-management tests for local-admin self-protection and normal role labels.
- Account/profile tests for route, view, and session-refresh intent.
- Registry markup tests for Summary and Detailed report actions.
- Header layout tests for customer name and role display.

## Non-Goals

- Redesigning the full administration workspace.
- Changing public visibility rules for archived passports.
- Replacing the existing role model or cluster membership storage.
- Changing the signing or publishing workflow semantics beyond display-label clarity.
