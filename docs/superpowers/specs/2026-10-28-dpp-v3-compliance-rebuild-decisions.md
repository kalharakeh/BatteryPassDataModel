# DPP V3 Compliance Rebuild — Key Decisions

Decided by the product owner on 28 Oct 2026, after the comparison of the V3 attribute list with the app.

## Context

- **Goal:** cover every row of `DPP_V3_vs_App_comparison_2026-10-28_v2.xlsx` and comply with `DPP_Internal_2026_BatteryPass-Ready_DataAttributeLongList_v2.0_August version_V3.xlsx` (73 attributes, tab "Data attribute longlist_v2.0").
- **Freedom:** the passport app has no real users or data yet, so data can be reset and the model restructured; changes can be bundled freely.
- **Baseline:** branch `security/demo-api-hardening`, commit `8c8bfe7`, ASP.NET app in `web/`; 464 tests passing.

## Role rule

Where the file says "the person who has legally purchased the battery (or a third party on their behalf)", it means **the members of the cluster the battery belongs to**: cluster users **see** that data, cluster admins **edit** it.

Confirmed: market surveillance also sees purchaser data, because every purchaser row in the file lists it (decision 12). Shared legitimate-interest data (#43, #45–48) is seen by the cluster but maintained by Scania's team (decision 10).

## Decisions

| # | Topic | Decision | What it means for the build |
|---|---|---|---|
| 1 | Data model | Rebuild on the BatteryPass-Ready **v2.0** data model; reset batteries, passports and family templates; keep users, clusters and API tokens | Replace the v1.2 aspect models, their sample payloads and the demo fallbacks. Validate against the v2.0 JSON schema (repository linked from the file's Technical references tab). |
| 2 | Catalogue | The 73 V3 attributes become the app's **single attribute catalogue** | One entry per attribute: v2.0 technical name, level, audience, editor, static/dynamic and update rule, unit, format, requirement for Other industrial, mandatory from Feb 2027. Storage, pages, forms, access checks, APIs, validation and a compliance test read from it. Anything not in it is removed, except app mechanics (clusters, users, signatures/trust, audit). |
| 3 | Levels | Family → model → **batch** (model × calendar year × manufacturing site) → battery, plus **manufacturing-site** data | The batch holds carbon footprint, recycled and renewable content, DoC, test reports and the model identifier (16 rows). The site holds manufacturer identifier and information and the manufacturing place (#12, #15, #16). |
| 4 | Category | All products are **Other industrial >2 kWh** | V3 covers everything; the 19 rows removed in V3 stay out. Conditional rows still apply (#19, #24, #49–52, and the non-cycling exemptions on #71, #72, #74, #82–85). |
| 5 | Identifiers | Choose the EN 18219 / ISO/IEC 15459 scheme **later** | Build a configurable ID format with clearly marked test IDs; choose the scheme and register with its issuing agency before go-live. |
| 6 | Legitimate-interest roles | **Split into three** roles | Repair/repurposing/remanufacturing operators: #43, #45, #46, #47, #48. Recyclers and battery treatment operators: #43, #45, #48. Authorised treatment and WEEE facilities: #48. |
| 7 | Documents | Public documents as **links** to the company website; restricted documents **uploaded** to the app | Links are stored with content type, language and title (v2.0 keys); uploads are served only to the attribute's audience. |
| 8 | Live data | **BMS feed** through the external API, **manual entry** by cluster admins, and a **JSON upload tool** | The upload takes per-battery static parameters or live data. Only catalogue attributes are accepted, each only if the uploader may edit it; other keys are reported and ignored. The existing BMS export format (e.g. `battery-passport_VPC8C05-6788_….json`) should map in: state of charge, equivalent cycles and temperature (mapping: decision 17). Proposed: static per-battery parameters by Scania's team, live data by Scania's team and the battery's cluster admins. |
| 9 | Cluster admins' rights (workbook D-09) | Purchaser data, **plus handing their batteries over** to another cluster | Cluster admins edit only purchaser data and can transfer one of their batteries to another cluster (e.g. after a resale), through a transfer step so it doesn't simply vanish from their view. Battery model/batch and manufacturing site stay with Scania's team. Software version is removed (not in the file). Design proposal: the cluster is not passport data, so a transfer doesn't need a new passport. |
| 10 | Shared legitimate-interest data (workbook D-35) | **Scania's team** edits; the battery's cluster only sees it | Kept once per battery model in the family/model template (#43, #45–48), as the file defines them per model. |
| 11 | Fades and expected lifetime (workbook D-43) | Template value at placement on market; **Scania's team and the battery's cluster admins** re-state them for that battery when its status changes | Needs per-battery values for #59, #69, #74, #79 and #81 that override the template, enterable by hand or by JSON upload. |
| 12 | Market surveillance | Sees purchaser data, **as the file requires** | Confirms the access matrix: battery's cluster + market surveillance (+ Scania's team) for the 10 purchaser rows. |
| 13 | Repurpose / remanufacture / re-use | When the status becomes Repurposed, Remanufactured or Reused, a **new passport linked to the previous one** is issued and the previous passport becomes **"inactive"**; the economic operator responsible for the new passport is recorded (#7) | Follows #22 ("a new battery passport must be issued … when placed on the market again") and #2 (DPP status "inactive"). Waste is an ordinary new version; the passport is later "marked-for-deletion" after recycling or export. Refined by decision 19: the new passport comes when the battery is placed on the market again; a repair sets "re-used" on the same passport. |
| 14 | Battery transfer | The **receiving cluster accepts** before the transfer takes effect; afterwards the **previous cluster no longer sees** the battery's purchaser data | The file grants purchaser data to the current purchaser only. |
| 15 | Status change and fades | The status change (form or JSON upload) **also asks for the re-stated fades and expected lifetime** | Keeps #59, #69, #74, #79 and #81 in step with #22. |
| 16 | Passport level | **One passport per pack** (e.g. `VPC8C05-6788`); a system (e.g. `SYSTEM-001`) is only a grouping | The battery record is the pack. System and module identifiers in uploads are not stored (not in the file). |
| 17 | BMS export mapping (design O-2) | #83 from **`equivalentChargeAndDischargeCycles`**; #87 from the **latest measured pack temperature** | The export team is asked to fill state of charge, cycles and production date; confirm the cycle count (partial cycles added up, one full charge plus one full discharge = one cycle); add one pack-temperature field in °C and say which sensors it uses; give measurement times; and drop the IP address, CAN payload and exporter details. Remaining capacity is optional (it suggests #59). |
| 18 | Manufacturing date #17 (design O-3) | **Year and month** (YYYY-MM), as the file and the regulation say | The schema's format is a full date; format checks are annotations by default, so validation passes. Tell BatteryPass-Ready about the mismatch. |
| 19 | Repairs (design O-4) | Follow the file's DIN note: a repair is recorded as **"repaired" under status "re-used"** on the same passport, with a repair history; a **new passport only when the battery is placed on the market again** | Recorded by Scania's team or the battery's cluster admins through the status change (re-stated values asked as usual) and shown with the status to the same readers. The history isn't in the passport JSON (no schema field). Refines decision 13. |
| 20 | Battery picture (design O-5) | **Keep one picture per battery family**, set by Scania's team | Shown on the public summary. Not passport data and not in the JSON; cluster admins can no longer change it. |
| 21 | Schema copies (design O-6) | **Copy both v2.0.1 Other industrial schema files** into the repository in step 1 | `Other_Industrial_Above_2kWh_2.0.1.json` and `Other_Industrial_Above_2kWh_Feb27_2.0.1.json`, with a note giving the source address, the date, the CC BY 4.0 licence and © BatteryPass-Ready Consortium. Tests run offline. |
| 22 | Test packages | **JsonSchema.Net** checks values and passports against the official schemas (step 1); **EphemeralMongo** runs the repositories' tests against a throwaway MongoDB (from step 2, where the database code arrives) | EphemeralMongo starts a temporary mongod inside the test run; no Docker or local install. Its first run downloads a mongod binary (about 100 MB). |
| 23 | Team pages (step 2d) | **Reuse today's admin page layout**, styles and shared page parts; no new layout options | The family page, Edit battery, Conformance, Revision history and the batteries list keep their look with the new data underneath. Only screens with no counterpart today get a mock-up (batch page, sites and operators, republish question, restricted documents). The new pages replace the old ones in place under the existing menu entries; no temporary menus, preview entries or duplicates in the version being built. |
| 24 | Removing a value (step 2d) | The team's editors **may remove a value** by emptying its field; uploads and feeds keep "empty never overwrites" | A value a published passport needs by February 2027 can't be removed (change it instead). Removals are audited with the value they replaced and republish like changes. |
| 25 | Removing records (step 2d) | Families, models, batches, sites and operators **can be removed while nothing uses them** | Once a record has models, batches, batteries or passports, the page says why it stays. Removing deletes its values and uploaded documents, and is audited. The old family page could remove a model. |
| 26 | New model from a copy (step 2d) | Adding a model offers **"Start from: empty or a copy of a model"**, from any family | Copies the typed values, marked as entered by the team member at that moment; uploaded documents and batches aren't copied. The old family page had "Base on existing template". |
| 27 | Look of the team pages (step 2d) | Today's palette, fonts, cards, buttons and inputs, with a **calmer finish**: one card per thing, outlined sentence-case tags, orange only for "missing", green only for "done", thin progress bars, slim message lines, one heading scale, gentle motion | Amends decision 23. The team pages look a little more refined than the other admin tabs, which can get the same finish when step 3 rebuilds them. The shared `site.css` is not changed. |

Decisions 1–23 approved by the product owner on 28 Oct 2026; decisions 24–27 on 29 Oct 2026.

## Still open

- **Identifier scheme** (decision 5) — before go-live.

## Next

The target design is [2026-10-28-dpp-v3-compliance-rebuild-design.md](2026-10-28-dpp-v3-compliance-rebuild-design.md): the attribute catalogue, data structure, access matrix, editing rules, flows and JSON upload format. After the product owner's review, plan step 1 (data foundation).

The 10-task plan `docs/superpowers/plans/2026-10-28-dpp-v3-high-priority-access-fixes.md` was written for the current model; with the rebuild, its access rules move into the access step.
