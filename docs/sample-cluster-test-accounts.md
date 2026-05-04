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
