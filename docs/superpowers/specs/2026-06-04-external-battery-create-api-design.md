# External Battery Create API Design

Date: 2026-06-04
Status: Approved direction, pending implementation plan

## Context

The external API already supports reading existing batteries, writing telemetry and operations data, changing Battery Model and software version, and creating a passport snapshot for an existing Battery ID.

The admin UI can create battery records through `POST /admin/batteries/create`. That flow generates the Battery ID from Battery Family and serial number, rejects duplicate serial numbers, requires a cluster, and creates the battery record without creating the first passport automatically.

There is no equivalent external API endpoint for creating the battery record itself. The current audit system is also passport-centered: audit events and signed revisions are keyed by Passport ID. Admin-created batteries do not currently get a dedicated battery-created audit event, and the battery builder leaves `updatedBy` as `product-template-reset` unless the caller overwrites it.

## Goals

- Add an external API endpoint to create a battery record.
- Keep serial number uniqueness the same as the admin flow: a serial number may not already exist on any battery.
- Validate submitted family, model, software version, cluster, and identity data before insertion.
- Return the generated Battery ID.
- Keep passport creation as a separate lifecycle action through the existing `POST /api/external/v1/batteries/{batteryId}/passports` endpoint.
- Record who created the battery for both API-created and admin-created batteries.
- Add a battery-level audit trail so creation can be traced before the first passport exists.

## Non-Goals

- Do not auto-create, validate, sign, or publish a passport when the battery is created.
- Do not change the existing Passport ID generation rules.
- Do not make Passport ID a caller-supplied field during battery creation.
- Do not relax cluster scoping for API tokens.

## Endpoint

Add:

```http
POST /api/external/v1/batteries
Authorization: Basic <token>
Content-Type: application/json
```

The endpoint requires write-capable token access. `readWrite` and full lifecycle tokens can create batteries; read-only and sign-only tokens cannot.

The token must be allowed to access the submitted `clusterId`. Global tokens can create in any cluster. Cluster-scoped tokens can create only in their assigned clusters. The API create endpoint requires a non-empty cluster, matching the admin battery creation flow.

## Request

Use a small, explicit JSON contract instead of accepting a full battery/passport document.

Required fields:

```json
{
  "batteryFamily": "Compact 7M",
  "batteryModel": "2.0",
  "softwareVersion": "4.0",
  "serialNumber": "SN-001",
  "clusterId": "cluster-north-operations"
}
```

Accepted aliases:

- `productId` may be used instead of `batteryFamily`.
- `productVersion` may be used instead of `batteryModel`.
- `name` may be used instead of `displayName`.

Optional creation fields:

```json
{
  "modelNumber": "compact-7m-SN-001",
  "displayName": "Compact 7M SN-001",
  "facilityId": "FAC-001",
  "manufacturingDate": "2026-06-04"
}
```

The first implementation should not accept arbitrary battery or passport document overrides. Keep creation limited to the required fields and optional creation fields listed above. Broader write support should remain in the existing targeted update endpoints.

## Validation

Validation runs before insert:

- Reject missing or blank `serialNumber`.
- Reject any existing battery with the same serial number, case-insensitive, regardless of Battery Family.
- Resolve Battery Family from `productId` or `batteryFamily`.
- Reject unknown Battery Family.
- Resolve Battery Model from `batteryModel` or `productVersion`.
- Reject Battery Model values that do not belong to the selected Battery Family.
- Require `softwareVersion`.
- Reject software versions that are not defined for the selected Battery Family and Battery Model.
- Require `clusterId`.
- Reject unknown cluster IDs.
- Reject clusters outside the token scope.
- Reject generated Battery ID collisions.
- Handle MongoDB duplicate-key errors as `409 Conflict` so concurrent duplicate creates return a clean API error.

## Creation Flow

The endpoint uses the same domain path as the admin flow:

1. Normalize request values.
2. Resolve the product template and product version.
3. Generate the Battery ID with `BatteryIdService.CreateBatteryId(product.ProductName, serialNumber)`.
4. Build the battery document with `ProductTemplatePassportBuilder.BuildBatteryFromTemplate`.
5. Apply the requested software version metadata for the selected model.
6. Apply allowed optional creation fields.
7. Set cluster and attribution fields.
8. Insert the battery through `BatteryRepository.CreateBatteryAsync`.
9. Append a battery-created audit event.
10. Return `201 Created` with the generated Battery ID.

Creation must not create a passport. The response should point clients to the existing passport snapshot endpoint when they are ready to create the first passport.

## Response

Success:

```json
{
  "success": true,
  "message": "Battery created successfully.",
  "data": {
    "batteryId": "did:web:acme.battery.pass:...",
    "batteryFamily": "Compact 7M",
    "batteryModel": "2.0",
    "softwareVersion": "4.0",
    "serialNumber": "SN-001",
    "clusterId": "cluster-north-operations",
    "createPassportPath": "/api/external/v1/batteries/{batteryId}/passports"
  }
}
```

Errors should follow the existing external API envelope:

- `400 Bad Request` for missing fields, malformed fields, unknown family/model/software, or invalid cluster IDs.
- `401 Unauthorized` for missing or invalid tokens.
- `403 Forbidden` for token scope mismatch.
- `409 Conflict` for duplicate serial number or generated Battery ID collision.
- `503 Service Unavailable` when MongoDB is unavailable.

## Attribution

Battery documents should record creation and update attribution.

For admin-created batteries:

```json
{
  "createdBy": "admin@example.com",
  "createdByType": "admin-ui",
  "updatedBy": "admin@example.com",
  "updatedByType": "admin-ui"
}
```

For API-created batteries:

```json
{
  "createdBy": "Token display name",
  "createdByType": "external-api",
  "createdByTokenId": "token-id",
  "updatedBy": "Token display name",
  "updatedByType": "external-api",
  "updatedByTokenId": "token-id"
}
```

Existing `createdAt` and `updatedAt` should remain ISO-8601 UTC strings.

## Battery Audit Trail

Add a battery-level audit trail separate from passport audit events.

Collection: `batteryAuditEvents`

Event shape:

```json
{
  "eventId": "...",
  "batteryId": "did:web:acme.battery.pass:...",
  "eventType": "battery.created",
  "actor": "admin@example.com or token display name",
  "actorType": "admin-ui or external-api",
  "actorTokenId": "token-id when external-api",
  "source": "admin-ui or external-api",
  "message": "Battery created.",
  "metadata": {
    "batteryFamily": "Compact 7M",
    "batteryModel": "2.0",
    "softwareVersion": "4.0",
    "serialNumber": "SN-001",
    "clusterId": "cluster-north-operations"
  },
  "createdAt": "2026-06-04T00:00:00.0000000Z"
}
```

The service should support appending events and listing events by Battery ID. The first implementation only needs `battery.created`, but the boundary should allow future battery update events.

## Admin Flow Alignment

Update the existing admin battery create flow so it:

- Sets `createdBy`, `createdByType`, `updatedBy`, and `updatedByType`.
- Appends the same `battery.created` event to `batteryAuditEvents`.
- Keeps the existing global serial-number uniqueness behavior.
- Keeps the existing user experience: battery creation redirects to the batteries table and does not create a passport automatically.

## Documentation And Workbench

Update `/help` and QA docs to include:

- `POST /api/external/v1/batteries`
- Required body fields
- Duplicate serial behavior
- The separate next step: `POST /api/external/v1/batteries/{batteryId}/passports`
- Token access requirements

Add an API workbench example with the read-write token and a clearly marked replace-before-run serial number such as `SN-API-REPLACE-ME`.

## Testing

Add tests covering:

- External API source contains `POST /api/external/v1/batteries`.
- Read-write and lifecycle tokens can create batteries; read-only and sign-only tokens cannot.
- Token cluster scope is enforced.
- Duplicate serial numbers return `409 Conflict`.
- Unknown Battery Family returns `400 Bad Request`.
- Unknown Battery Model for the selected family returns `400 Bad Request`.
- Unknown software version for the selected model returns `400 Bad Request`.
- Successful creation returns `batteryId`.
- Successful creation does not create a passport.
- Admin-created and API-created batteries set attribution fields.
- Admin-created and API-created batteries append `battery.created` events.
- `/help` documents the endpoint and keeps passport creation as a separate lifecycle step.
