# Sample Cluster Test Accounts

Shared seeded password: `Password123!`

## General Admin

| Purpose | Email | Access |
| --- | --- | --- |
| General admin | `admin@example.test` | Full registry, full passport edits, cluster assignment, user membership assignment |

## Battery Access Matrix

| Battery ID | Cluster | Cluster ID | Connected normal user | Connected local admin | Password | General admin |
| --- | --- | --- | --- | --- | --- | --- |
| `did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976` | Default Demonstrator Cluster | `cluster-default-demonstrator` | None seeded | None seeded | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-customer-north-001` | North Operations Cluster | `cluster-north-operations` | `north.user@example.test` | `north.admin@example.test` | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-customer-south-001` | South Operations Cluster | `cluster-south-operations` | `south.user@example.test` | `south.admin@example.test` | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-end-user-fleet-001` | Fleet Operations Cluster | `cluster-fleet-operations` | `fleet.user@example.test` | `fleet.admin@example.test` | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-end-user-storage-001` | Storage Operations Cluster | `cluster-storage-operations` | `storage.user@example.test` | `storage.admin@example.test` | `Password123!` | `admin@example.test` |

Normal users can open the detailed report for their own cluster battery only. Local admin users can also open `/cluster-admin` and edit Facility ID, Battery image, State of charge %, Remaining capacity %, Remaining energy kWh, and Full cycles for their own cluster battery.

## Phase 6A demo scenarios

Use these allow-listed passports after pressing **Reset demo scenarios** on `/admin/help`.

| Scenario | Passport ID | Cluster | Admin account | Expected state |
|---|---|---|---|---|
| Published trusted | `did:web:acme.battery.pass:demo-published-trusted-001` | North Operations Cluster | `admin@example.test` or `north.admin@example.test` | Published, signed, clean, public, QR-ready |
| Draft incomplete | `did:web:acme.battery.pass:demo-draft-incomplete-001` | North Operations Cluster | `admin@example.test` | Missing required data, blocked from signing |
| Ready to sign | `did:web:acme.battery.pass:demo-ready-to-sign-001` | North Operations Cluster | `admin@example.test` | Complete and validated, unsigned |
| Signed unpublished | `did:web:acme.battery.pass:demo-signed-unpublished-001` | North Operations Cluster | `admin@example.test` | Signed and clean, not public until published |
| Dirty after edit | `did:web:acme.battery.pass:demo-dirty-after-edit-001` | North Operations Cluster | `admin@example.test` | Signed core changed after proof |
| Invalid signature | `did:web:acme.battery.pass:demo-invalid-signature-001` | North Operations Cluster | `admin@example.test` | Proof diagnostics fail verification |
| Restricted document access | `did:web:acme.battery.pass:demo-restricted-document-001` | North Operations Cluster | `admin@example.test` or `north.admin@example.test` | Published trusted with restricted evidence metadata |
