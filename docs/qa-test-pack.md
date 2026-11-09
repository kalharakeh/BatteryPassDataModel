# QA Tester Pack

The starting point for anyone testing the battery passport app without knowing the project. It covers the new app (DPP V3): one passport per battery, built from the V3 attribute list, with a reader's role deciding what they see.

Keep these open while testing:

- `docs/end-user-testing-guide.md`: the step-by-step script per role.
- `docs/sample-cluster-test-accounts.md`: the demo accounts, clusters and batteries.
- `/help` in the app: each role's own help, including "Statuses" (the two badges).
- `/help/api` in the app: every API call, with a workbench to try one.

## Quick start

1. Open the app link provided by the project owner in a browser.
2. **The known start:** a team member signs in, opens Clusters, and at the end of the Clusters tab presses **Reset demo data**.
   - This makes the demo clusters, accounts, batteries and API tokens again.
   - The tokens' values are shown once, in the window that follows. Copy them then: they are never shown again and never written here.
3. **Passwords:** the demo accounts share one password, which the project owner gives you. It is set on the server (`DEMO_USERS_PASSWORD`) and never written in this pack or in the app.
4. **One browser:** test in one browser window. Sign out and in as each account in turn: some faults only show when the same browser changes accounts.
5. **Every element fully visible:** on every page and in every window, scroll each choice, field and button into view and check it is fully shown, not under a bar or cut. Reaching the last button isn't enough.

## Tester accounts

| Who | Account | Cluster | What to check |
|---|---|---|---|
| Team | the team's own account (from the project owner) | all | Product data, releasing battery IDs, creating and publishing batteries, every battery page, marking passports, demo data, tokens |
| Cluster admin | `demo.north.admin@example.test` | demo-north | Their batteries' pages: live values, accident reports, status changes and repairs, hand-overs, uploads; their users and tokens. Their Registry also shows DEMO-CORE-0001 waiting to be accepted. |
| Cluster user | `demo.north.user@example.test` | demo-north | Their batteries in the Registry and on the detail page, read only |
| Other cluster admin | `demo.south.admin@example.test`, `demo.fleet.admin@example.test` | demo-south, demo-fleet | They see demo-north's batteries as the public does; demo-north's admin's "Waiting for you" from demo-fleet |
| Other cluster users | `demo.south.user@example.test`, `demo.fleet.user@example.test` | demo-south, demo-fleet | As above, read only |
| Market surveillance | `demo.msa@example.test` | — | Every published passport, every section, "Mark as invalid…" |
| Notified body | `demo.notified-body@example.test` | — | Public and authorities' values |
| Commission | `demo.commission@example.test` | — | Public, shared and authorities' values; no purchaser values |
| Repair, repurposing, remanufacturing | `demo.repair@example.test` | — | Public and the shared values #43, #45–#48 |
| Recycling, battery treatment | `demo.recycling@example.test` | — | Public and #43, #45, #48 |
| Authorised treatment, WEEE | `demo.treatment@example.test` | — | Public and #48 |
| Anyone | not signed in | — | The public page only |

## Demo batteries

| Battery | Cluster | What it shows |
|---|---|---|
| DEMO-C7M-0001 | demo-north | Published, several versions, repaired (status re-used, with its repair history), an accident report, live values with a week of history, 7 modules, two earlier uploads |
| DEMO-C7M-0002 | demo-north | A draft with missing values: the publication checks |
| DEMO-C7M-0003 | demo-north | Marked invalid by market surveillance ("Demo: wrong carbon footprint figures") |
| DEMO-C13M-0001 | demo-south | Repurposed, then remanufactured: three linked passports, the two older inactive |
| DEMO-C13M-0002 | demo-south | A draft |
| DEMO-CORE-0001 | demo-fleet | Published, an accident report, handed over to demo-north and waiting |
| DEMO-CORE-0002 | demo-fleet | A draft |
| DEMO-CORE-0003 | demo-fleet | Waste, marked for deletion |

The demo batteries come from a release with the reference "Demo", on the Battery IDs tab, with four spare IDs (DEMO-SPARE-0001 to DEMO-SPARE-0004) for trying the create picker and "Change its battery ID…". With the three ID settings set (`DPP_ID_AGENCY`, `DPP_ID_COMPANY`, `DPP_ID_BASE_URL`), their IDs are marked TEST after the DUNS, such as `UN778474481-TEST-DEMO-C7M-0001`. Without them they are test IDs starting with `urn:x-battery-pass:TEST-`. Either way the pages show a "Test passport" tag.

## Scenario matrix

What each reader should get. ✓ = allowed or shown, – = not shown or refused. "Own" means the battery is in the reader's cluster.

| Reader | Public page | Detail page | Purchaser tab | Shared tab | Authorities tab | Live values, Modules tabs | Registry lists | Battery page (manage) | Mark as invalid | Battery IDs tab |
|---|---|---|---|---|---|---|---|---|---|---|
| Anyone, not signed in | ✓ | – (sign-in) | – | – | – | – | – | – | – | – (sign-in) |
| Cluster user, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | – | – | – (Not allowed) |
| Cluster admin, own battery | ✓ | ✓ | ✓ | ✓ | – | ✓ | own cluster's | ✓ | – | – (Not allowed) |
| Cluster user or admin, other cluster's battery | ✓ | public only | – | – | – | – | own cluster's | – (refused) | – | – (Not allowed) |
| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every battery (Batteries tab) | ✓ | ✓ (and undo) | ✓ |
| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | every published | – | ✓ | – (Not allowed) |
| Notified body | ✓ | ✓ | – | – | ✓ | – | every published | – | – | – (Not allowed) |
| Commission | ✓ | ✓ | – | ✓ | ✓ | – | every published | – | – | – (Not allowed) |
| Repair | ✓ | ✓ | – | #43, #45–#48 | – | – | every published | – | – | – (Not allowed) |
| Recycling | ✓ | ✓ | – | #43, #45, #48 | – | – | every published | – | – | – (Not allowed) |
| Treatment | ✓ | ✓ | – | #48 | – | – | every published | – | – | – (Not allowed) |

Scanning a released ID whose passport isn't published yet shows "Not published yet" to everyone, with no battery data; a voided ID shows "Not available".

**Battery page actions** (team and the battery's cluster admins, on an active passport):

| Action | Team | Own cluster admin |
|---|---|---|
| Live values, Accident reports ("Add a report"), Upload a file | ✓ | ✓ |
| Change status: Repaired, placed on the market again, Remanufactured, Repurposed, Waste | ✓ | ✓ |
| Change status: Correct the values | ✓ | – |
| Add a note | ✓ | ✓ |
| Hand over… / Accept or Decline on "Waiting for you" | moves directly, or cancels a waiting one | ✓ |
| Mark as invalid or for deletion…, and Undo | ✓ | – |
| Edit product data, create and publish batteries | ✓ | – |
| Release, export and void battery IDs; Change its battery ID… on a draft | ✓ | – |

**API** (`/api/external/v1`, a token in the `Authorization` header):

| Token | Reads | Writes |
|---|---|---|
| A cluster's "Read" token | that cluster's batteries, as its cluster user would | nothing |
| A cluster's "Read and write live values" token | the same | the live values (#63, #83, #87, E1–E4) and uploads of live values, for that cluster's batteries |
| The team's "Full access" token | every battery | creating batteries (from a released, available serial number), publishing, live values, uploads |
| Any token | — | never a status change or repair (made on the battery's page only), and never releasing battery IDs |

`/api/passports` (the list of all passports) is for the team only.

## Acceptance checklist

- [ ] "Reset demo data" finishes and shows the tokens once.
- [ ] Every row of the scenario matrix holds, for every reader.
- [ ] The two badges show on every passport page with the right status. The Battery badge never shows to the public.
- [ ] Battery IDs: a range releases and opens the release's page with every ID Available; its "Export to Excel" opens with that release's IDs, one row per ID and its QR address.
- [ ] The Battery IDs page opens on Releases, with the real IDs' counts at the top; All IDs shows demo IDs only with "Show demo IDs" ticked, and voided IDs only with "Show voided" ticked (or the status Voided chosen); Releases shows a release whose IDs are all voided only with it ticked, and its own page lists every ID; Void… is in a row's ⋯ menu.
- [ ] A release with a wrong count, a repeated or already released serial number, or one with lower case or a double dash lists every reason and releases nothing.
- [ ] Create battery picks a released serial number (no typing); the draft shows its battery ID and QR code at once; the ID turns Used.
- [ ] "Change its battery ID…" on a draft takes another available ID and gives the old one back; a voided ID is never offered again.
- [ ] Scanning a released, unpublished ID shows "Not published yet"; a voided one "Not available".
- [ ] With one of the three ID settings missing, the Battery IDs page shows a red notice naming it; nothing can be released, creating a battery from a released real ID is refused, and the demo data still works.
- [ ] DEMO-C7M-0002 lists what blocks publication; filling it in and pressing Publish gives a signed version 1 with a QR code, and its ID turns Published.
- [ ] A repair on DEMO-C7M-0001 sets re-used, adds to the repair history and makes a new version.
- [ ] Hand-over: demo-fleet's DEMO-CORE-0001 waits on demo-north's Registry; Accept moves it, and demo-fleet then sees it as the public does.
- [ ] Market surveillance marks a passport invalid; the public page says so; only the team undoes it.
- [ ] Uploading the help's sample files on DEMO-C7M-0001 shows the check before saving, then saves.
- [ ] The API answers each token as in the API table, and refuses what it should with a plain message.
- [ ] The public and detail pages print on A4 with every tab open (browser print).
- [ ] Every page works at a phone's width (375 px) and at 150 % Windows scaling, with every element fully visible.

## Known limitations

- **Real IDs need the settings:** without `DPP_ID_AGENCY`, `DPP_ID_COMPANY` and `DPP_ID_BASE_URL`, the Battery IDs page shows a red notice naming the missing ones, and nothing is released or created from a released ID. The demo data still works, with test IDs (`urn:x-battery-pass:TEST-…`), as on a developer's PC.
- **Pack temperature:** the battery system's export has no pack temperature yet; each export's notes say so.
- **Hand-overs:** no e-mail; the receiver sees them on their Registry. A waiting hand-over isn't cancelled by itself when the passport stops being active; accepting is then refused with a reason.
- **Signatures:** each version is signed when published; live values and accident reports change it in place without a new signature, by design.

## Bug report template

```text
Title:
Account (role) used:
Battery (serial number) and page address:
Screen width and browser:
Steps:
1.
2.
3.
Expected:
What happened:
Screenshot or file:
```

## Improvement suggestion template

```text
Page:
Who would use it (role):
What is hard today:
Suggestion:
```
