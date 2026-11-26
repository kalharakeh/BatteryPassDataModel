# Demo Accounts, Clusters and Batteries

What "Reset demo data" makes (decision 32). The team presses it at the end of the Clusters tab; "Clear demo data" removes it all again and keeps real data. Every record it makes carries a demo mark.

**Passwords:** every demo account signs in with the one password set on the server (`DEMO_USERS_PASSWORD`). The project owner gives it to testers. It is never written here or shown in the app. Without that setting, the demo data comes without accounts, and the Demo data card says so.

**Tokens:** the reset shows each token's value once, in its result window. Copy the values then. They are never stored readable and never written here.

## Clusters

The team's pages (the Clusters tab) call them clusters. A customer's own people read "customer", and a cluster's ID is the **customer ID**: a customer's admin finds it at the top of their Registry and of their Admin page, and types it to hand a battery over.

| Cluster ID | Name | Batteries |
|---|---|---|
| `demo-north` | Demo North Operations | DEMO-C7M-0001, DEMO-C7M-0002, DEMO-C7M-0003 |
| `demo-south` | Demo South Operations | DEMO-C13M-0001 (and its two new passports), DEMO-C13M-0002 |
| `demo-fleet` | Demo Fleet Operations | DEMO-CORE-0001 (waiting to go to demo-north), DEMO-CORE-0002, DEMO-CORE-0003 |

## Accounts

The "Name" and "Role" are what the app shows for the account: the name beside the initials in the top bar, and the role in the account menu and on "Your account".

| Account | Name | Role | Customer (cluster) |
|---|---|---|---|
| `demo.north.admin@example.test` | Demo north admin | Customer admin | demo-north |
| `demo.north.user@example.test` | Demo north user | Customer user | demo-north |
| `demo.south.admin@example.test` | Demo south admin | Customer admin | demo-south |
| `demo.south.user@example.test` | Demo south user | Customer user | demo-south |
| `demo.fleet.admin@example.test` | Demo fleet admin | Customer admin | demo-fleet |
| `demo.fleet.user@example.test` | Demo fleet user | Customer user | demo-fleet |
| `demo.msa@example.test` | Demo market surveillance | Market Surveillance Authorities | — |
| `demo.notified-body@example.test` | Demo notified body | Notified Body | — |
| `demo.commission@example.test` | Demo Commission | Commission | — |
| `demo.repair@example.test` | Demo repair shop | Legitimate interest: repair, repurposing, remanufacturing | — |
| `demo.recycling@example.test` | Demo recycler | Legitimate interest: recycling and training | — |
| `demo.treatment@example.test` | Demo treatment facility | Legitimate interest: authorised or WEEE treatment | — |

After logging in, every one of these lands on the Registry (the team lands on "To do"). A customer's account also shows its customer in the account menu ("Customer: Demo North Operations").

The team's account is not demo data: the project owner gives testers a team account if they test the team's pages.

Demo accounts work only on demo batteries and demo clusters: on a battery that isn't demo (one made with a real battery ID or real product data, a demo-numbered battery on real product data included), every demo account sees what a visitor sees, and no action is offered. "Reset demo data" stops, and changes nothing, when a real account, battery or API token uses one of the demo's clusters, and names the cluster; demo accounts a demo admin added or made never stop it. They can log in only while the site has the demo on (`ENABLE_DEMO_DATA`). The demo accounts that belong to a cluster are made as if that cluster's admin had made them, so the customer admin may change their name and password; the accounts across the app are the team's.

## Tokens

| Token | Access | Used for |
|---|---|---|
| Each demo cluster, "Read" | That cluster's batteries, read only | API reads |
| Each demo cluster, "Read and write live values" | The same, plus live values and uploads of live values | The battery system's feed |
| "Demo team (full access)" | Every demo battery; creating demo batteries from the demo spares and publishing them. A battery that isn't demo is answered as an unknown ID | The team's own systems |

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

The demo models are answered (cycling use and cadmium or lead: yes for all three; the commercial warranty: yes for Compact 7M, no for Compact 13M and Core), so DEMO-C13M-0002 and DEMO-CORE-0002 each miss only #17. The three demo clusters carry the team's written authorisation, ticked by the reset ("demo-data"); if the team takes one away, the reset keeps it away. The notified body's account sees the shared values (materials, dismantling manuals, part numbers, spare parts, safety measures) as well as the authorities' values.

Drafts are seen by the team only. The batteries come from the "Demo" release on the Battery IDs tab, which also holds four spare IDs, DEMO-SPARE-0001 to DEMO-SPARE-0004. With the three ID settings their IDs are marked TEST after the DUNS (`UN778474481-TEST-DEMO-C7M-0001`); without them they start with `urn:x-battery-pass:TEST-`. The pages show a "Test passport" tag either way.

## Sample files

On `/help`, logged in as the team or a customer admin, the "Upload a file" part offers two downloads for DEMO-C7M-0001:
- an upload file, with live values and a repair;
- a battery system export (mapping BMS2_ACO), with made-up values.
