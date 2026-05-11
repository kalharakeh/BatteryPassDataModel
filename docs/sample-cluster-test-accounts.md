# Sample Cluster Test Accounts

Shared seeded password: `Password123!`

North customer password exception: `customer_001_001@customer.org` uses `12345`.

## General Admin

| Purpose | Email | Access |
| --- | --- | --- |
| General admin | `admin@example.test` | Full registry, full passport edits, product templates, Local editable fields, API Token Management, cluster assignment, user membership assignment |

## Cluster Users

| Cluster | Normal users | Local admin |
| --- | --- | --- |
| North Operations Cluster | `north.user@example.test`, `customer_001_001@customer.org` | `north.admin@example.test` |
| South Operations Cluster | `south.user@example.test` | `south.admin@example.test` |
| Fleet Operations Cluster | `fleet.user@example.test` | `fleet.admin@example.test` |

## Battery Access Matrix

| Battery ID | Model | Product template | Product/battery version | Software | Cluster | Cluster ID | Connected normal user | Connected local admin | Password | General admin |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976` | `CP7M-DEMO-001` | Compact 7M | `1.0` | `2.0` | Unassigned demonstrator | None | None seeded | None seeded | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-customer-north-001` | `CP7M-NORTH-001` | Compact 7M | `1.0` | `2.0` | North Operations Cluster | `cluster-north-operations` | `customer_001_001@customer.org`, `north.user@example.test` | `north.admin@example.test` | `12345` for customer, otherwise `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-customer-north-002` | `CP7M-NORTH-002` | Compact 7M | `2.0` | `4.0` | North Operations Cluster | `cluster-north-operations` | `customer_001_001@customer.org`, `north.user@example.test` | `north.admin@example.test` | `12345` for customer, otherwise `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-customer-south-001` | `CP13M-SOUTH-001` | Compact 13M | `1.0` | `2.0` | South Operations Cluster | `cluster-south-operations` | `south.user@example.test` | `south.admin@example.test` | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-customer-south-002` | `CP13M-SOUTH-002` | Compact 13M | `2.0` | `4.0` | South Operations Cluster | `cluster-south-operations` | `south.user@example.test` | `south.admin@example.test` | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-end-user-fleet-001` | `CORE-FLEET-001` | Core | `1.0` | `2.0` | Fleet Operations Cluster | `cluster-fleet-operations` | `fleet.user@example.test` | `fleet.admin@example.test` | `Password123!` | `admin@example.test` |
| `did:web:acme.battery.pass:sample-end-user-fleet-002` | `CORE-FLEET-002` | Core | `2.0` | `4.0` | Fleet Operations Cluster | `cluster-fleet-operations` | `fleet.user@example.test` | `fleet.admin@example.test` | `Password123!` | `admin@example.test` |

Normal users can open the detailed report for their own cluster battery only. The customer account `customer_001_001@customer.org` can open North batteries including `CP7M-NORTH-001`; it should not open South, Fleet, or unassigned detailed reports.

Local admin users can open `/cluster-admin` and edit only the fields selected by the general admin under **Local editable fields**. The default editable set is Facility ID, Battery image, State of charge %, Remaining capacity %, Remaining energy kWh, and Full cycles.

## Product Template Reset

Press **Reset product-template passports** on `/admin/clusters?tab=products` or `/admin/help` to recreate one unassigned demonstrator plus six clustered customer batteries from the MongoDB product templates. The reset removes older sample/demo passports so the acceptance test set stays small and predictable.
