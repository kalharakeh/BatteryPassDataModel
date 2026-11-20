# DPP V3 Step 4: Purchaser Editing and Lifecycle — Wrap-up

Step 4 is built in five plans, each run in its own session and each ending with a working app. This note says what
there is now, what was left open on purpose, and what step 5 should know. Decision 34 holds the product owner's answers;
the mock-up approved on 31 Oct 2026 is `docs/mockups/dpp-v3-step4-purchaser-and-lifecycle.html`.

## What is built

| Part | Plan | What it adds |
|---|---|---|
| 4a | `2026-10-31-dpp-v3-step4a-cluster-admin-battery-page.md` | The cluster admin's battery page, `/cluster-admin/batteries/{id}`, opened by "Manage" on their Registry rows and the detail page: live values and accident reports to edit, modules and versions to read. Only admin memberships give editing rights. |
| 4b | `2026-11-01-dpp-v3-step4b-status-change-and-repairs.md` | The status change (#22) asked as "What happened?", with the re-stated fades and lifetime (#59, #69, #74, #79, #81) and a fade suggestion; repairs with a history that is never changed; waste; the team's "Correct the values" and "Add a note to the history". |
| 4c-1 | `2026-11-02-dpp-v3-step4c1-new-linked-passport.md` | Re-use, remanufacture and repurpose place the battery on the market again: Scania's new linked passport (a new #6) or another operator's link. The old passport becomes inactive and frozen; notices link the two. |
| 4c-2 | `2026-11-02-dpp-v3-step4c2-invalid-and-deletion.md` | "Invalid" (team or market surveillance, with a reason seen only by them) and "marked for deletion" (team, waste only), both undone by the team only; they stay in the Registry with their status. |
| 4d | `2026-11-02-dpp-v3-step4d-hand-overs.md` | Hand-overs between clusters: the sender names the receiving cluster by its ID and asks; the receiver accepts or declines on its Registry; the sender or the team cancels; the team keeps its direct move. |

The demo data shows each part: a repaired battery (DEMO-C7M-0001), a waste one marked for deletion (DEMO-CORE-0003), a
repurposed one with its new passport (DEMO-C13M-0001), an invalid one (DEMO-C7M-0003) and a waiting hand-over
(DEMO-CORE-0001, demo-fleet to demo-north). The suite went from 2098 to 2233 tests.

New app data, all outside the passport: the pack's repairs, successor link and mark, and the hand-overs in the new
collection `dpp_transfers` (made with its indexes at the app's first start; the reset tool treats it as test data).

## Open follow-ups

- **Cluster IDs in hand-overs:** `HandOverService` compares the battery's cluster with the sender's clusters without
  `ClusterRepository.NormalizeClusterId`, unlike `ClusterBatteryPages`. Harmless while stored IDs are normalised (they
  are today); to be tidied in step 5 (the managing session, 2 Nov 2026).
- **A hand-over waiting on a passport that stops being active** (a successor's passport, an invalid mark) isn't
  cancelled by itself: Accept is refused with a plain reason, Decline and Cancel still work.
- **Clearing the demo data** removes the demo batteries' hand-overs. A real battery handed over to a demo cluster would
  be left waiting with no one to answer; its sender or the team can cancel it.
- **No e-mail** tells a receiver that a hand-over waits (Q13); it shows on their Registry only.
- **The successor's warranty (#19)** is the team's to type before publishing; the successor carries the last measured
  live values, since #63 and #87 are needed to publish.

## What step 5 should know

- **Status changes by upload belong to step 5's upload** (decision 34). They must go through `StatusChangeService`,
  which checks the transition and saves the status and the re-stated values together; the save pipeline refuses #22 and
  the re-stated values on their own.
- **Cluster API tokens stay live-values-only** (J-10). The BMS adapter writes live values in place, without a new
  version.
- **A passport that is no longer active takes no changes** (`DppStatuses.IsNoLongerActive`): inactive, invalid and
  marked for deletion. An upload aimed at one must be refused, and a serial-number lookup skips inactive passports.
- **Senders:** team, cluster admin, battery-system feed and market surveillance (`SenderKind`). A battery-system feed
  changes live values only; market surveillance changes no value.
- **Testing:** run builds and tests with `--disable-build-servers` and `MSBUILDDISABLENODEREUSE=1
  DOTNET_CLI_USE_MSBUILD_SERVER=0`; whole suites with `--blame-hang --blame-hang-timeout 10m`. Long runs under load
  were slowdowns, not stuck tests. A clean clone checks out `site.css` with CRLF line endings and fails two
  `ApplicationAuditCoverageTests`; the worktree's copy passes them.
