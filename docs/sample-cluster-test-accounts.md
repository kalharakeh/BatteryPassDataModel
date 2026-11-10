# Demo Accounts, Clusters and Batteries

What "Reset demo data" makes (decision 32). The team presses it at the end of the Clusters tab; "Clear demo data" removes it all again and keeps real data. Every record it makes carries a demo mark.

**Passwords:** every demo account signs in with the one password set on the server (`DEMO_USERS_PASSWORD`). The project owner gives it to testers. It is never written here or shown in the app. Without that setting, the demo data comes without accounts, and the Demo data card says so.

**Tokens:** the reset shows each token's value once, in its result window. Copy the values then. They are never stored readable and never written here.

## Clusters

| Cluster ID | Name | Batteries |
|---|---|---|
| `demo-north` | Demo North Operations | DEMO-C7M-0001, DEMO-C7M-0002, DEMO-C7M-0003 |
| `demo-south` | Demo South Operations | DEMO-C13M-0001 (and its two new passports), DEMO-C13M-0002 |
| `demo-fleet` | Demo Fleet Operations | DEMO-CORE-0001 (waiting to go to demo-north), DEMO-CORE-0002, DEMO-CORE-0003 |

## Accounts

| Account | Role | Cluster |
|---|---|---|
| `demo.north.admin@example.test` | Cluster admin | demo-north |
| `demo.north.user@example.test` | Cluster user | demo-north |
| `demo.south.admin@example.test` | Cluster admin | demo-south |
| `demo.south.user@example.test` | Cluster user | demo-south |
| `demo.fleet.admin@example.test` | Cluster admin | demo-fleet |
| `demo.fleet.user@example.test` | Cluster user | demo-fleet |
| `demo.msa@example.test` | Market surveillance | — |
| `demo.notified-body@example.test` | Notified body | — |
| `demo.commission@example.test` | Commission | — |
| `demo.repair@example.test` | Repair, repurposing and remanufacturing (legitimate interest) | — |
| `demo.recycling@example.test` | Recycling and battery treatment (legitimate interest) | — |
| `demo.treatment@example.test` | Authorised treatment and WEEE facilities (legitimate interest) | — |

The team's account is not demo data: the project owner gives testers a team account if they test the team's pages.

## Tokens

| Token | Access | Used for |
|---|---|---|
| Each demo cluster, "Read" | That cluster's batteries, read only | API reads |
| Each demo cluster, "Read and write live values" | The same, plus live values and uploads of live values | The battery system's feed |
| "Demo team (full access)" | Every battery; creating and publishing | The team's own systems |

## Batteries

| Battery | Family and model | Cluster | State | Shows |
|---|---|---|---|---|
| DEMO-C7M-0001 | Compact, Compact 7M | demo-north | Published, several versions; battery re-used (repaired) | The repair history, an accident report, a week of live values, 7 modules, an export from the battery system and a file from its cluster admin under "Upload a file" |
| DEMO-C7M-0002 | Compact, Compact 7M | demo-north | Draft, ready to publish | "Ready to publish" and the "Publish passport" action |
| DEMO-C7M-0003 | Compact, Compact 7M | demo-north | Published, then marked invalid by `demo.msa` | The invalid notice; the reason "Demo: wrong carbon footprint figures", seen only by the team and market surveillance |
| DEMO-C13M-0001 | Compact, Compact 13M | demo-south | Repurposed, then remanufactured: three linked passports, the two older inactive, the newest active | The passports' chain and their notices |
| DEMO-C13M-0002 | Compact, Compact 13M | demo-south | Draft, 1 value missing | "Not ready to publish yet" and what the passport still needs |
| DEMO-CORE-0001 | Core, Core | demo-fleet | Published; an accident report; handed over to demo-north, waiting | "Waiting for you" on demo-north's Registry; Accept or Decline |
| DEMO-CORE-0002 | Core, Core | demo-fleet | Draft, 1 value missing | "Not ready to publish yet" and what the passport still needs |
| DEMO-CORE-0003 | Core, Core | demo-fleet | Waste, marked for deletion | The marked-for-deletion notice |

Drafts are seen by the team only. The batteries come from the "Demo" release on the Battery IDs tab, which also holds four spare IDs, DEMO-SPARE-0001 to DEMO-SPARE-0004. With the three ID settings their IDs are marked TEST after the DUNS (`UN778474481-TEST-DEMO-C7M-0001`); without them they start with `urn:x-battery-pass:TEST-`. The pages show a "Test passport" tag either way.

## Sample files

On `/help`, signed in as the team or a cluster admin, the "Upload a file" part offers two downloads for DEMO-C7M-0001:
- an upload file, with live values and a repair;
- a battery system export (mapping BMS2_ACO), with made-up values.
