# DPP V3 Compliance Rebuild — Target Design

**Date:** 28 Oct 2026 · **Status:** draft for the product owner's review · **Builds on:** [key decisions](2026-10-28-dpp-v3-compliance-rebuild-decisions.md) 1–22, approved 28 Oct 2026

- **Compliance reference ("the file"):** `DPP_Internal_2026_BatteryPass-Ready_DataAttributeLongList_v2.0_August version_V3.xlsx`, tab "Data attribute longlist_v2.0", 73 attributes.
- **Technical reference ("the schema"):** BatteryPass-Ready JSON schema v2.0.1 for Other industrial batteries, `JSONSchemaFiles/v2.0.1/Other_Industrial_Above_2kWh_2.0.1.json` in the public BatteryPass-Ready GitLab (bp/BatteryPassDataModel), read 28 Oct 2026.
- **Coverage target:** every row of `DPP_V3_vs_App_comparison_2026-10-28_v2.xlsx` (Appendix C).

## How to read this

- **Section 1** is a one-page summary of what changes for each kind of user. Start there.
- **Sections 2–12** are the design: principles, building blocks, data, access, editing, flows, uploads, documents, identifiers, validation and pages.
- **Section 13** lists what is kept and removed, **section 14** the six build steps.
- **Section 15** lists the choices this design makes where the file and the 21 decisions don't decide. These are the places to push back. **Section 16** lists the open items.
- **Appendix A** is the attribute catalogue (73 rows), **Appendix B** the schema notes, **Appendix C** how each row of the comparison workbook is covered.

This is the umbrella design for the six build steps of section 14. Each step gets its own implementation plan, written and reviewed separately; step 2 is planned in four parts (2a–2d). The earlier 10-task plan `docs/superpowers/plans/2026-10-28-dpp-v3-high-priority-access-fixes.md` is replaced: its access rules move into step 3.

Terms used:

- **Team:** Scania's own administrators of the app (today's `admin` role).
- **Battery's cluster:** the cluster the battery belongs to, which is the purchaser. Its cluster users see purchaser data; its cluster admins edit it (role rule, 28 Oct).
- **Pack:** one battery, the subject of one passport (decision 16).
- **MSA:** market surveillance authorities.
- **LI roles:** the three legitimate-interest roles of decision 6. **LI-repair** covers repair, repurposing and remanufacturing operators. **LI-recycling** covers recyclers and battery treatment operators. **LI-treatment** covers authorised treatment facilities and WEEE treatment facilities.
- **Live set:** the attributes that change in place without a new passport version: #63 state of charge, #83 full cycles, #87 temperature, #92 accident information, and the four extreme-temperature times E1–E4 (decision 28).
- **E1–E4:** four live values added beyond the file by decision 28, from the Polarion dynamic attribute list (DIN DKE 99100, 6.7.7.5–6.7.7.8). Appendix A lists them after the file's attributes.

## 1. What changes for each user

| Who | Sees | Edits or does |
|---|---|---|
| Anyone, no login | The public summary: the 54 public attributes, including the passport header (#1–#6) and the public document links. Not the battery status, not the cluster. | Nothing |
| Cluster user of the battery's cluster | Public data, the 10 purchaser attributes (status, state of charge, cycles, temperature, accidents, fades, lifetime), the 5 shared attributes (#43, #45–48) and the repair history | Nothing |
| Cluster admin of the battery's cluster | Same as a cluster user | Purchaser data: the live set, and status changes (including repairs) with re-stated fades and lifetime. Hands a battery over to another cluster. Uploads JSON for the cluster's batteries. |
| Team | Everything | Everything else: families, models, sites, batches, operators, pack creation, documents, publication. Can also do what cluster admins do. Sets "marked-for-deletion"; can set "invalid". |
| Notified body | Public data and the 4 authorities attributes (#7, #12, #13, #29) | Nothing |
| Market surveillance | Public, purchaser (10), shared (5) and authorities (4) data | Can mark a passport "invalid" |
| Commission | Public, shared (5) and authorities (4) data. Not purchaser data: the file doesn't list the Commission there. | Nothing |
| LI-repair | Public data and #43, #45, #46, #47, #48 | Nothing |
| LI-recycling | Public data and #43, #45, #48 | Nothing |
| LI-treatment | Public data and #48 | Nothing |
| Battery system (BMS feed with a cluster API token) | — | Live values #63, #83, #87 and E1–E4 for that cluster's packs |

Biggest differences from today's app:

1. **Who sees what:** battery status leaves the public summary; test reports (#29) become authorities-only; purchaser data is shown only to the battery's cluster and market surveillance.
2. **Data model:** the app stores the 73 attributes of the v2.0 model instead of the v1.2 aspects. The 34 extra app parameters and all demo values go (Appendix C).
3. **Levels:** batch (model × calendar year × manufacturing site) and manufacturing site are added beside model and pack.
4. **Versions:** every published change to static data makes a new passport version with a new passport ID; the old version is archived and stays retrievable.
5. **Lifecycle:** a battery placed on the market again after re-use, remanufacturing or repurposing gets a new linked passport; a repair marks it "re-used" on the same passport; waste leads to a new version and, after recycling, to "marked-for-deletion".
6. **Hand-over:** cluster admins can hand a battery over to another cluster, which must accept.
7. **One way in:** forms, JSON upload and the battery-system feed all pass the same checks.

## 2. Scope and principles

**In scope:** everything needed to hold, show, edit, version and export the 73 attributes for Other industrial batteries above 2 kWh, with the file's access rules and the 21 decisions.

**Out of scope:**

- The 19 attributes removed in V3, and other battery categories (decision 4).
- The connection to the EU passport registry, which doesn't exist yet. The app keeps what the registry will need (#5, #6, #7).
- Choosing the identifier scheme (decision 5, open item O-1). The design makes it a setting.
- Page layout. A mock-up step comes before the page work (section 12).

**Principles:**

1. **The file is the spec.** An attribute exists in the app only if it is in the file, and its audience, level and update rule come from the file. The schema adds the technical detail the file leaves open: property names and value shapes (Appendix B). Where the two conflict, the file wins and the conflict is listed in Appendix B (so far only #17, decision 18).
2. **One catalogue.** The 73 attributes are defined once (Appendix A). Storage, forms, pages, access checks, uploads, the API, validation and the compliance test all read that definition. No page or service keeps its own list of fields.
3. **One save path.** Every change (form, JSON upload, API feed) goes through the same save pipeline (section 6.2).
4. **One visibility filter.** Every output (public page, detail page, API, JSON export, document download) is cut by the same filter: the reader's role and relation to the battery against each attribute's audience (section 5).
5. **One passport per pack** (decision 16). Systems and modules are not stored.

## 3. Building blocks

Each block has one job and a small interface, so it can be built and tested on its own.

| Block | Job | Used by | Depends on |
|---|---|---|---|
| Attribute catalogue | The 73 definitions of Appendix A as code: key, schema path, level, audience, editors, update rule, value shape, unit, requirement, Feb-2027 flag | All blocks below | — |
| Data store | The records of section 4, in MongoDB | Save pipeline, assembler | Catalogue |
| Save pipeline | Checks and applies one change set (section 6.2) | Forms, upload page, external API | Catalogue, roles, store, versioning, validation |
| Versioning and lifecycle | New versions, archiving, DPP status (#2), successors, transfers | Save pipeline, flows | Store, signing, identifier service |
| Passport assembler | Builds the schema JSON for one pack and version from its model, batch, site, operator and pack values. Values are stored in the schema's own form, units included; the save pipeline adds units to bare values. | Pages, API, signing, validation | Catalogue, store |
| Visibility filter | Removes what the reader may not see from an assembled passport | Pages, API, exports, documents | Catalogue, roles |
| Validation | Value checks; publication completeness; schema validation | Save pipeline, publication | Catalogue, copies of the schemas |
| Signing | Signs a version's static core (section 4.3) and verifies it | Publication, pages | Today's ECDSA trust services |
| Ingest adapters | Turn an upload or feed message into a change set: native format and BMS2_ACO | Upload page, external API | Catalogue |
| Documents | Public links and restricted files, with access-checked download | Pages, assembler | Visibility filter |
| Identifier service | Issues #5 and #6, including a new #6 for a successor, by the configured scheme | Pack creation, versioning | Settings |
| Compliance test | Checks the catalogue against a checked-in extract of the file and against the schema | Test run | Catalogue |

## 4. Data model

### 4.1 Records

| Record | Holds | Notes |
|---|---|---|
| Family | Display name; one picture, shown on the public summary (decision 20) | Groups models. Category #20 is fixed to "industrial/non-stationary battery" (decision 4). Name and picture are not passport data. |
| Model | The 32 model attributes, plus the starting values of the 5 re-stated ones (#59, #69, #74, #79, #81) | Belongs to a family |
| Manufacturing site | #12, #15, #16 | Shared by every batch made there |
| Batch | The 16 batch attributes: #10, #25, #28–36, #49–53 | One per model × calendar year × site (decision 3, from the file's level column) |
| Operator | #7 and #14 of one economic operator | Entered once, picked per pack. A repurposing operator is added here (7.5). |
| Pack | #6, #11, #13, #17, #19; links to operator and batch (the batch gives model and site); cluster; #22; the pack's re-stated values once given; the live set; the repair history (7.9); links to predecessor and successor | One per battery. A successor link is either a pack in the app or an external reference: the operator's details (#7, #14) and the successor passport's identifier or web address (7.5). |
| Passport version | #1–#5 and the signed snapshot of the static core | One per published version; found by its #5 |
| Document | One link or one uploaded file, for one document attribute of one model, batch or pack. #92 can have several accident reports per pack. | Section 9 |
| Transfer | Pack, from and to cluster, state, who, when | Section 7.8 |
| Upload log | One entry per upload or feed message: sender, time, result per key | Section 8 |

A pack's value for an attribute comes from its level. Model, batch and site values are shared by every pack linked to them. For the 5 re-stated attributes, a pack shows the model value until it has its own re-stated value, which then wins.

Every stored value carries **when the information was generated** (measurement time for live values, entry time otherwise), its **source** (form, upload or feed) and **who** sent it. The file asks for a per-attribute time stamp (#4, DIN note). The schema has no field for it, so it is kept in the store and shown on the detail page, not put in the passport JSON.

### 4.2 Versions

- **Draft.** A pack's passport starts as a draft. A draft has no DPP status, is not public and is visible to the team only (J-6).
- **Publication** makes version 1: a new #5, DPP status "active", #4 set to the publication time, and the static core signed (4.3).
- **Static change** to a published pack, meaning any change outside the live set, including a status change: a new version is published with a new #5. The previous version becomes "archived" and stays retrievable by its own #5. The first #5 always resolves to version 1, as the file requires (#5).
- **Live update:** changes the current version in place and sets #4. No new version; the file allows this exception for dynamic data (#5).
- **Model, batch or site change** after publication: the editor lists the packs with an active passport that it affects and asks for confirmation. On save, each of them gets a new version. Inactive, invalid and marked-for-deletion passports are left as they are. Uploading a document to such a model or batch asks too: the upload goes through only once the team ticks "Publish new versions".
- **Transfer** between clusters: no new version, because the cluster is not passport data.

#4 changes only when something is made available to readers: at publication and on live updates, not on draft edits. The file defines it as the time the update was "first made available to third parties".

### 4.3 What is signed

The signature covers the **static core**: every attribute except #2 (DPP status), #4 (last update) and the live set. Those change without a new version: #2 becomes "archived" when a newer version appears, and #4 changes on every live update. Signing them would break older signatures.

Today's signing (ECDSA over a canonical snapshot, `PassportTrustService`) stays; only the snapshot's content changes.

## 5. Access

### 5.1 Audiences, from the file

| Audience | Attributes | Who sees it (the team always sees everything) |
|---|---|---|
| Public | 54 | Everyone, no login |
| Purchaser | 10: #22, #59, #63, #69, #74, #79, #81, #83, #87, #92, plus E1–E4 (decision 28) | The battery's cluster and market surveillance |
| Shared | 5: #43, #45, #46, #47, #48 | The battery's cluster, market surveillance, the Commission, and the LI roles the file names for that attribute |
| Authorities | 4: #7, #12, #13, #29 | Notified bodies, market surveillance and the Commission |

LI roles per shared attribute (file, decision 6): #43 and #45 for LI-repair and LI-recycling; #46 and #47 for LI-repair; #48 for all three.

### 5.2 Role matrix

✓ = sees it, – = doesn't.

| Role | Public | Purchaser | #43 | #45 | #46 | #47 | #48 | Authorities |
|---|---|---|---|---|---|---|---|---|
| Anonymous | ✓ | – | – | – | – | – | – | – |
| Cluster user or admin, battery's cluster | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | – |
| Cluster user or admin, any other cluster | ✓ | – | – | – | – | – | – | – |
| Team | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Notified body | ✓ | – | – | – | – | – | – | ✓ |
| Market surveillance | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Commission | ✓ | – | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| LI-repair | ✓ | – | ✓ | ✓ | ✓ | ✓ | ✓ | – |
| LI-recycling | ✓ | – | ✓ | ✓ | – | – | ✓ | – |
| LI-treatment | ✓ | – | – | – | – | – | ✓ | – |

Rules:

- **Battery's cluster is checked per pack.** A cluster member sees purchaser and shared data only for packs of their own cluster. After a transfer, the previous cluster loses it (decision 14).
- **App-wide roles.** The three LI roles (replacing today's single `legitimateInterest` role), notified body, market surveillance and Commission are given by the team to named accounts. The app doesn't check an LI account's interest per battery; the team checks it when granting the role (J-7).
- **The file wins over the schema.** The schema carries its own, coarser read rights (for example "authorities"). Where they differ, the file's wording is used: for example, purchaser data goes to market surveillance only, not to notified bodies or the Commission.
- **All versions alike.** Archived, inactive, invalid and marked-for-deletion versions are filtered the same way as active ones.
- **Repair history** (7.9) isn't passport data. It is shown with the battery status, to the same readers.
- **API tokens.** A cluster token reads like a cluster user of its clusters and writes only the measured live values #63, #83, #87 and E1–E4 (J-10). A team token has the team's rights and is issued only for Scania's own systems. `/api/passports`, the list of all passports, is team-only.
- **Documents follow their attribute.** A restricted file is served only through an access-checked link, never a public storage address.

## 6. Editing

### 6.1 Who edits what

| What | Who | When |
|---|---|---|
| Family (name and picture), model (32 attributes and the 5 starting values), site, batch, operator records | Team | Any time; after publication with the confirmation of 4.2 |
| Shared attributes #43, #45–48, kept on the model | Team only (decision 10) | As model data |
| Pack creation: #6 (issued by the app), operator (#7, #14), #11, #13, #17, #19, batch, cluster | Team | At creation; #13 again if the service provider changes |
| Battery status #22 with the re-stated #59, #69, #74, #79, #81 | Team and the battery's cluster admins | Only through the status change (7.4) |
| Repair history (not passport data) | Team and the battery's cluster admins | Through the status change (7.9) |
| #63, #83, #87, E1–E4 | Battery-system feed, team, battery's cluster admins | Any time |
| #92 accident information | Team and the battery's cluster admins | Any time, by form with a file upload |
| A pack's cluster | Team directly; the battery's cluster admins by transfer (7.8) | Any time |
| DPP status #2 | The app; the team ("invalid", "marked-for-deletion"); market surveillance ("invalid") | 7.6, 7.7 |

Cluster admins act here as third parties authorised by the economic operator. The schema's own write rights for #22 and #87 name exactly that group.

### 6.2 The save pipeline

Every change set, whatever its source, runs these steps in order:

1. **Find the target:** the pack (or model, batch, site, operator). Not found: rejected.
2. **Known attribute?** A key that isn't in the catalogue is ignored and reported as "not in the attribute list". It is never stored.
3. **May this sender change it, for this pack?** If not: rejected and reported.
4. **Valid?** Type, unit, range and allowed values (Appendices A and B). Physical quantities can't be negative (#21, #57, #64–#67, #70, #78, #81, #82, #85; temperatures inside them can), an app rule kept from before V3. If not: rejected and reported.
5. **Empty never overwrites.** A missing or empty value leaves the stored value as it is. The BMS export sends many empty values. **The team's editors are the exception (decision 24):** a field the team empties on the page removes its value, unless a published passport built from the record needs it by February 2027 (then it is refused: change it instead). Uploads and feeds can't remove values.
6. **Rules across attributes:** a status change needs its re-stated values, and only the transitions of 7.4 are allowed.
7. **Apply:** the live set in place; anything else into the draft or, for a published pack, into a new version (4.2).
8. **Record:** an audit entry and an upload log entry with the result per key and, for a saved or removed value, the value it replaced.

A change set is all-or-nothing per pack: if step 6 fails, nothing is saved for that pack. Keys rejected in steps 2–4 are reported but don't block the rest (J-12).

**Preview:** the upload page and the API accept a preview flag. The pipeline then runs steps 1–6 and returns the report without saving anything.

## 7. Flows

### 7.1 Create and publish

1. The team creates the pack: picks the batch (which gives model and site), the operator and the cluster, and enters #11, #13, #17 and #19. The app issues #6.
2. The draft is checked against the publication rules (section 11). Missing Feb-2027 data blocks publication; data required later gives a warning. The mandatory live values (#63, #87) are needed too: if the feed hasn't sent them yet, the team enters the end-of-line values. #83 is conditional and doesn't block; #92 needs nothing extra (section 9).
3. On publication: #5 is issued, the DPP status becomes "active", #4 is set, the static core is signed and the passport is shown to each audience.

### 7.2 Static update

Any change outside the live set makes a new version (4.2), after the same publication checks.

### 7.3 Live update

From the feed, an upload or the form. Changes the current version in place and sets #4. Each value keeps its own measurement time. The signature is unaffected.

### 7.4 Status change (#22)

- Started by the team or the battery's cluster admins, on the pack page or by upload, for a pack whose passport is published and active.
- Asks for the new status and the re-stated #59, #69, #79, #81, plus #74 where the model has it. They are pre-filled with the pack's current values (decision 15).
- Capacity fade (#59) can be suggested from a measured remaining capacity: fade = 1 − remaining capacity / rated capacity (#57). The remaining capacity itself is not stored, because it isn't in the file.
- Allowed transitions: "original" is set only at creation. From "original", "re-used", "remanufactured" or "repurposed", the status can go to "re-used", "remanufactured", "repurposed" or "waste". "Waste" is final.
- A repair is recorded with the same form (7.9). "Re-used" without a repair means the battery is placed on the market again after preparation for re-use.
- Result: a repair or "waste" makes a new version of the same passport (7.9, 7.6). "Remanufactured", "repurposed", or "re-used" for placing on the market again lead to a successor passport (7.5).

### 7.5 Re-use, remanufacture, repurpose (decisions 13 and 19)

The file says a new passport is issued when such a battery is placed on the market again (#22), and the old passport becomes "inactive" once it is linked to the new one (#2). A repair is not one of these operations (7.9).

1. The status change (form or upload) records the new status and the re-stated values. The old passport gets a last version with them and stays "active" for now (J-13).
2. The pack page then asks who places the battery on the market again (decision 13 records that operator):
   - **Scania:** the team creates the successor pack as a draft, with a new #6 (J-2), Scania's operator record (#7, #14), the predecessor's batch link, the re-stated values as a start and a link back to the predecessor. It is completed and published like any new pack (7.1). On publication, the old passport becomes "inactive" and the two passports link to each other.
   - **Another operator:** the team or the battery's cluster admin enters that operator's details (#7, #14) and, once known, the successor passport's identifier or web address. The old passport then becomes "inactive" and links to it.

### 7.6 Waste and deletion

- Status "waste": a new version; the DPP status stays "active".
- After recycling or export, the team sets the DPP status "marked-for-deletion" (file, #2). The passport stays readable until it is deleted under a retention rule, which is outside this design.

### 7.7 Invalid

Market surveillance or the team marks a passport "invalid", with a reason. The public page shows that it is invalid. Only the team can undo it (J-8).

### 7.8 Transfer to another cluster (decisions 9 and 14)

1. A cluster admin of the battery's cluster, or the team, asks for a transfer to a named cluster.
2. The receiving cluster's admins accept or decline. The sender can cancel while it's pending.
3. On acceptance the pack moves. The previous cluster loses its purchaser and shared data. No new passport version.
4. The team can also move a pack directly, for example to fix a mistake. The move is audited.

### 7.9 Repair (decision 19)

The file's DIN note on #22 asks for a repair history and for a repair to be recorded as "repaired" under the status "re-used". The regulation text asks for a new passport only when the battery is placed on the market again, which a repair doesn't do.

- The team or the battery's cluster admins record a repair through the status change (7.4), with "repair" as the reason. An upload can do the same (8.2).
- The repair adds a "repaired" entry to the pack's repair history: date, what was repaired, who recorded it. The history is shown with the battery status, to the same readers (5.2). The schema has no field for it, so it isn't in the passport JSON.
- A battery with status "original" becomes "re-used". A battery that is already "re-used", "remanufactured" or "repurposed" keeps its status (J-15).
- Like any status change, a repair asks for the re-stated values (pre-filled) and publishes a new version of the same passport.

## 8. Live data and uploads

### 8.1 Three ways in, one pipeline

- **Form** on the pack page, for the team and the battery's cluster admins.
- **Upload page:** one JSON file, many packs, with a preview before saving. The file can carry static per-pack values or live values and status changes (decision 8). Step 3 of the pipeline decides what each sender may change: static per-pack values are team-only.
- **External API:** the battery-system feed posts the native JSON or the BMS export with a cluster token to one upload endpoint (`POST api/external/v1/uploads`, with the same preview flag).

### 8.2 Native upload format

```json
{
  "format": "dpp-v3-upload/1",
  "batteries": [
    {
      "batterySerialNumber": "VPC8C05-6788",
      "measuredAt": "2026-10-27T10:02:59Z",
      "values": {
        "stateOfChargeSoC": 76,
        "numberOfFullChargingAndDischargingCycles": 412,
        "temperatureInformation": 23
      }
    }
  ]
}
```

- **Finding the pack:** by `uniqueProductIdentifier` (#6) or `batterySerialNumber` (#11). A serial number that matches more than one pack the sender may change is rejected.
- **Keys** are the schema's property names, which are unique across its sections (Appendix B).
- **Values:** the schema's own JSON form is always accepted. For attributes that are a single value with a fixed unit (percentages, °C, Ah, kg, years, counts, status), a bare value is also accepted and the app adds the unit. For #79, a bare number is the pack value.
- **Time:** `measuredAt` is the generation time of every value in the entry. Without it, the upload time is used.
- **Status change:** `batteryStatus` goes in the same entry as the re-stated values. For "re-used", "remanufactured" and "repurposed", the next operator is then named on the pack page (7.5).
- **Repair:** the entry carries `"reason": "repair"` and a `"repairDescription"` next to `measuredAt`, plus the re-stated values. The app sets the status as 7.9 says.

### 8.3 The BMS export (mapping `BMS2_ACO`, version 01.32.07; decision 17)

Sample: `battery-passport_VPC8C05-6788_2026-10-27T10-02-59.928Z.json`.

| Export field | Use |
|---|---|
| `serialNumber` | Finds the pack (#11) |
| `exportedAt` | Generation time of the values |
| `stateOfCharge.raw` | #63 |
| `equivalentChargeAndDischargeCycles` | #83. One full charge plus one full discharge counts as one cycle (the export team confirms, 8.5). |
| A new field with the latest measured pack temperature in °C (8.5) | #87 |
| `timeSpentInExtremeHighTemperature.raw` | E1, seconds converted to whole minutes (decision 28) |
| `timeSpentInExtremeLowTemperature.raw` | E2, the same |
| `timeSpentChargingInExtremeTemperature.raw` | E3 and E4, in whole minutes. Polarion says it is always zero, because charging in extreme temperatures is prevented. A value above zero can't be split into above and below: the upload reports it and leaves E3 and E4 unchanged (8.5, item 7). |
| `productionDate` | #17 as year and month (decision 18), accepted from the team only |
| `remainingCapacityAh` | Suggests #59 at the next status change; not stored |
| Everything else: energy and Ah totals, the other cycle fields, histograms, deep-discharge events, service date, CAN payload, IP address, exporter, parent and child devices | Ignored: not in the file. The exporter's name and e-mail are not stored. |

In the sample, every field the passport needs is empty; only energy and Ah totals are filled. The request to the export team is in 8.5.

### 8.4 What goes away

Today's telemetry collection and its data go in step 3 (Appendix C, X-21). The external API's `POST /telemetry` becomes the write of the live values (#63, #83, #87, E1–E4) and its telemetry history the live-value history, both through the new model (step 3c). `PATCH /operations`, `PATCH /battery-model` and `PATCH /software-version` are removed in step 3, when the new pages and API replace the old ones: their data isn't in the file (Appendix C, X-03, X-05 and X-22 to X-24). The bulk upload endpoint arrives in step 5.

### 8.5 Request to the export team (decision 17)

1. Fill `stateOfCharge` (in %, 0–100), `equivalentChargeAndDischargeCycles` and `productionDate`.
2. Confirm that `equivalentChargeAndDischargeCycles` adds up partial cycles and counts one full charge plus one full discharge as one cycle.
3. Add one field with the latest measured pack temperature in °C, and say which sensors it comes from (for example the average of the cell sensors). The file asks for the sensor basis to be defined.
4. Give a measurement time per value, or confirm that all values are read at `exportedAt`.
5. Optional: fill `remainingCapacityAh`. The app uses it only to suggest the capacity fade at a status change.
6. Leave out what the passport doesn't need: the IP address, the CAN payload, and the exporter's name and e-mail.
7. Fill `timeSpentInExtremeHighTemperature`, `timeSpentInExtremeLowTemperature` and `timeSpentChargingInExtremeTemperature` (decision 28). Optional: split the charging time into above and below the boundary, as DIN DKE 99100 does. Ideally the boundaries match the idle-state temperature range the passport publishes (#88, #89).

## 9. Documents

The schema has 15 document-type attributes. 14 of them hold exactly one document each, as a schema `RelatedResource`: a web address and content type (required), plus language and title (optional). #36 is a plain web link.

| Kind | Attributes | How |
|---|---|---|
| Public links | #23, #24, #25, #28, #36, #38, #54, #55, #56 | Link to the company website, stored with content type, language and title |
| Restricted uploads | #29, #45, #46, #47, #48, #92 | File uploaded to the app, served only to the attribute's audience through an access-checked link |

Each attribute holds one document per owner (model, batch or pack).

#92 is the exception: its link points to the pack's accident page in the app, which is access-checked like the attribute. The page lists the accident reports uploaded by the team or the battery's cluster admins, or says that none were reported. So #92 is filled from publication on, and a new report doesn't change the link; it counts as a live update and sets #4 (J-14).

## 10. Identifiers

- #6 (pack), #7 (operator) and #12 (manufacturer) are EN 18219 identifiers; #5 identifies a passport version. The schema stores all four as URIs.
- The scheme (EN 18219 with an ISO/IEC 15459 issuing agency) is chosen later (decision 5, open item O-1). Until then the identifier service issues test IDs marked `TEST-`, and every page of a passport with a test ID shows a banner.
- The QR code on the battery carries #6 and opens the public summary.
- A successor passport gets a new #6 (J-2).

## 11. Validation and testing

**Validation:**

- **On save, per value:** type, unit, range (for example 0–100 %), allowed values, web address and identifier form.
- **At publication, completeness:** every attribute marked "Feb 27: yes" must be present, except conditional ones. The attributes required later (#25, #30–36, #38, #49–52, #57) give a warning while missing.
- **Conditional attributes** (C in Appendix A: #19, #24, #49–52, #71, #72, #74, #82–85) never block publication. The editor shows the file's condition for each (J-9).
- **At publication, schema check:** the assembled passport is also checked against the schema's Feb-2027 variant (`Other_Industrial_Above_2kWh_Feb27_2.0.1.json`) and the full schema (`Other_Industrial_Above_2kWh_2.0.1.json`). The catalogue decides what must be present; today the Feb-2027 variant requires exactly the catalogue's Feb-2027 set (Appendix B.3). A shape error in either schema blocks publication, including a part missing inside a value. The schemas' own "attribute missing" errors are left to the catalogue, because `SchemaCoverageTests` keeps the schemas' required lists equal to the catalogue's. Copies of both schema files are added to the repository in step 1 (decision 21), with a note crediting the BatteryPass-Ready Consortium under CC BY 4.0, the source address and the date.

**Tests** (xUnit, in the app's usual style of unit tests plus source-assertion tests):

- **Compliance test:** a checked-in extract of the file's 73 rows (number, name, audience, level, update rule, unit, format, requirement, Feb 27) is compared with the catalogue: same attributes, audience, level and update rule. When the file changes, a new extract shows what to change.
- **Schema test:** every catalogue key exists at its path in the schema, and every passport built from the test seed passes the schema.
- **Access test:** the role matrix of 5.2 as data, over all roles and all 73 attributes, for pages, API and documents.
- **Pipeline tests:** each step of 6.2, including empty values, unknown keys, rights and preview.
- **Flow tests:** versions, status transitions, successors, invalid, deletion and transfer.

## 12. Pages

- **Public summary** (no login; opened from the QR code): the 54 public attributes grouped by the schema's sections, the public document links, the DPP status and the family's picture (decision 20). An inactive passport links to its successor. No cluster name.
- **Detail page** (logged in): the public data plus one section per audience the reader may see (purchaser, shared, authorities). Each value shows when it was generated and where it came from. The purchaser section includes the repair history (7.9).
- **Team editors:** family (name and picture), model, site, batch, operator and pack; publication; versions; documents.
- **Cluster pages:** cluster users get a read-only list and detail of their packs: the Registry, rebuilt on the new model (decision 30), lists the batteries each signed-in reader may open. Cluster admins also get the live-value form, the status change, the accident page, transfer requests and the upload page.
- **Team editor rules (decisions 24–26, 29 Oct 2026):**
  - **Only what was edited on the page counts.** The page carries a stamp of what each field showed; a save changes only the fields edited there, so a page left open never undoes a newer value saved by a colleague, an upload or a feed. A field edited on both sides is refused, with the typed value kept, and saves on the next try. The record's name follows the same rule.
  - **Removing a value:** empty its field (decision 24). **Removing a record:** families, models, batches, sites and operators can be removed while nothing uses them; otherwise the page says why they stay (decision 25). **A new model** can start from a copy of any model's typed values (decision 26).
  - **Names** of families, of models within a family, of sites and of operators are unique, ignoring case.
  - **Unsaved edits** are never lost silently: leaving the page, Cancel edits and uploading a document (which reloads the page) ask first.
- **Layout:** the team editors reuse today's admin pages: their layout, styles and shared page parts (decision 23), with a calmer finish (decision 27): one card per thing, small outlined tags in sentence case instead of uppercase pills, orange only for "missing" and green only for "done", thin progress bars, messages as a slim line, one heading scale and gentle motion. After a save the editor returns to the section and place where Save was pressed and confirms it in the save bar. The finish lives in the team pages' own stylesheet; `site.css` is not changed. Only screens with no counterpart today (the batch page, sites and operators, the republish question and restricted documents) get a mock-up before the page work starts: `docs/mockups/dpp-v3-team-pages-new-screens.html`.

## 13. Kept and removed

**Kept** (app mechanics, not passport data): users, clusters, roles and login; API tokens; signing and trust, with the new snapshot content; the audit log; QR codes; help pages, rewritten for the new model. Also one picture per battery family on the public summary (decision 20).

**Removed** (in step 3, when the new pages and API replace the old ones):

- The v1.2 aspect models (`BatteryPass/io.BatteryPass.*`) and their sample payloads, `BatteryPassCanonicalDataCatalog`, and the demo completion and fallback services.
- Today's family templates, batteries and passports (reset, decision 1).
- The three field policies that the catalogue replaces: `EditableFieldPolicyService`, `LocalAdminEditableFieldPolicyService` and `DataCompletionPolicyService`.
- The app extras (Appendix C): 29 removed; 5 kept (4 app mechanics and the family picture).
- The telemetry and operations endpoints (8.4).

## 14. Build steps

| Step | Delivers |
|---|---|
| 1. Data foundation | The catalogue; schema copies; the new records (family, model, site, batch, operator, pack) and their database mapping; checks per value; the assembler; compliance and schema tests; a test seed. Built beside today's app: nothing is removed yet. |
| 2. Team editing and publication | The store's repositories, tested against a throwaway MongoDB (EphemeralMongo, decision 22), and the passport-version and document records; the save pipeline with the form as first entry point, turning bare values into the schema's form; team editors, including the family picture; the identifier service with test IDs; documents (links and uploads); publication checks; signing of the static core; versions and archiving |
| 3. Access, pages and switch-over | The 9 app roles: today's 7, with `legitimateInterest` split into the 3 LI roles (5.2 shows 10 kinds of reader, counting anonymous readers and other clusters); the visibility filter on pages, API, exports and documents; the public summary and the detail page; `/api/passports` team-only. Then the switch-over: remove the v1.2 model, the remaining old pages (the team pages were replaced in 2d) and API endpoints (including telemetry and operations), demo values and extras, and reset the data. |
| 4. Purchaser editing and lifecycle | Cluster-admin editing; the status change with re-stated values; repairs and the repair history; successor passports; waste, deletion and invalid; transfers |
| 5. Live data and uploads | The upload page with preview; the external API upload endpoint; the native and BMS2_ACO adapters, including E1–E4 (decision 28); the upload log |
| 6. Go-live readiness | The identifier scheme switch (once O-1 is decided); help pages and test pack rewritten; manual checks over the role matrix; the deployment to AWS (see "Where it runs") |

Each step ends with all tests passing and a working app. Appendix C shows which workbook rows each step closes.

Step 2 is planned in four parts (2d in two plans), each ending with a working app:

- **2a, store and save pipeline:** the repositories (tested against a throwaway MongoDB), turning bare values into the schema's form, who may change what, the save pipeline with preview and a change log, test identifiers and pack creation. Backend only; every pack is a draft.
- **2b, publication and versions:** the publication checks, passport IDs (#5), signing of the static core, versions and archiving.
- **2c, documents:** public links, and uploaded files behind access checks, including the audience rule of 5.2 and #92's accident reports. Backend only: the download route and the accident page arrive with step 3's roles.
- **2d, the team's editing pages:** on today's admin page layout, with a mock-up for the new screens only (section 12, decision 23). The new pages replace the old team pages in place, under the existing menu entries ("Battery families", "Batteries"); "Editable fields" goes and sites and operators get one permanent entry. No temporary menus, preview entries or duplicates (decision 23). 2d removes the admin actions, views and view models only the replaced pages used; the old services they shared with the cluster-admin pages and the API (`ProductTemplateService`, `EditableFieldPolicyService`, `DataCompletionPolicyService`, the battery and passport views those pages also render) stay until steps 3 and 4. Two plans:
  - **2d-1, product data:** the form built from the official schema for every attribute shape; the families page and family page (name, picture, models, the model editor), batches, and sites and operators; public document links and restricted uploads; the republish question when a change reaches published passports, also for uploads; the team editor rules of section 12 (decisions 24–26); and, from the parity check of 29 Oct 2026, non-negative quantities, the value before each change in the audit entry and the new record types in the audit log's filter. Replaces "Battery families" and removes "Editable fields".
  - **2d-2, batteries:** the batteries list, creating a battery, the battery page (its values, live values, cluster, publication checks, publishing, versions, accident reports). Replaces "Batteries". Its first task removes the old battery and passport admin actions, whose routes would otherwise clash with the new ones. It also restores what the old pages enforced and nothing in this design removes (parity check of 29 Oct 2026): the serial number, manufacturer and economic operator (#11, #17, #19) fixed once the passport is published, still correctable while a draft (6.1); a duplicate serial number refused at creation (step 4 exempts linked successor packs, 8.2); a battery's cluster must exist; deleting a cluster also counts its new packs; the batteries list search by passport ID, battery ID, serial number or cluster; removing a draft pack, e.g. one made in the wrong batch. Optional: the Conformance page checks stored files against their SHA-256; the "Battery cluster assignments" list folds into the batteries list.
  - **E1–E4 (decision 28):** their own small plan after 2d-2 and before step 5's BMS adapter: catalogue entries beyond the file, a form and checks without a schema entry, "E1"–"E4" labels, and the compliance test keeping the 73 file attributes exact. The battery page's live values are built from the catalogue, so E1–E4 appear there once added.
  The public pages and the API follow in step 3.

Step 3 is planned in four parts (agreed with the product owner on 29 Oct 2026), each ending with a working app. 3b and 3c use 3a's rule; 3d comes last because it removes what 3b and 3c replace:

- **3a, who sees what:** the 9 roles, with the Users pages offering the three LI roles in place of `legitimateInterest`; sign-ins and API tokens mapped to a `Reader`; the visibility filter that cuts an assembled passport for a reader with `AudienceRules.MaySee` (E1–E4 follow #87); and the two addresses the passports already link to: the access-checked download `/dpp/documents/{id}`, through `DocumentService.ReadAsync`, with `Content-Type` from the stored, byte-checked type, `Content-Disposition` and `X-Content-Type-Options: nosniff`; and the accident page `/dpp/packs/{packId}/accidents`, through `DocumentService.AccidentReportsAsync` (a new screen, so a mock-up first). The team's pages link the uploaded files and accident reports.
- **3b, public pages:** the public summary (no login, opened from the QR code, with the family's picture, decision 20) and the detail page (signed in: one section per audience the reader may see, each value with when and where it came from) replace today's passport pages in place, with the QR code and the home page's search. Today's look, with the new data underneath (decision 23).
- **3c, the API:** `/api/passports` becomes team-only, on the new model. The external API keeps doing what it did, rebuilt on the new model and the new access rules (product owner, 29 Oct 2026): clusters, a cluster's batteries, a battery's passport, its sections and values, creating batteries, the live values in place of telemetry, a battery's live-value history in place of the telemetry history, and publication. Each answers only what the token may see or change (J-10 for cluster tokens). The history is read from the change log, which keeps every saved value; 3c adds each value's measurement time to its entry. The endpoints whose data isn't in the file go: operations (location of use, contact person, active or not), the battery-model label and the software version (8.4; decisions 2 and 9; product owner, 29 Oct 2026). Step 5 adds the bulk upload endpoint and the BMS adapter.
  Planned in two parts (product owner, 29 Oct 2026), each ending with a working app: **3c-1, reading** (clusters, a cluster's batteries, a battery's passport, its versions, sections, values and readable values; `/api/passports` team-only; the help page's documentation and workbench for these) and **3c-2, writing** (creating batteries, the live values and their history, publication; the removed endpoints; the rest of the help page). Between the two, the old write endpoints keep working on the old data. Creating batteries and publishing through the API is for team (global) tokens only, as on the pages (design 6.1); a cluster token reads and writes its batteries' live values. The four sample tokens stay until 3d's reset.
- **Review batch (29 Oct 2026, decisions 30 and 31):** before 3d, on the same branch: the Registry rebuilt on the new model (it also replaces "Managed passports" for cluster admins), the team's battery page links, versions and draft QR, the live-value history on the detail page, conditions shown per attribute, deleting accounts, the "Not allowed" page, the module list per battery, E1–E4 in minutes, and the help pages and cluster token form tidied.
- **3d, switch-over:** remove the v1.2 model, the remaining old pages (the cluster admins' "Managed passports" and the views only they use), the old services, the demo values and the extras (section 13, Appendix C); the help pages already lost the old workflow in the review batch, and their rewrite stays in step 6. Then reset `battery_pass_v3`, asked again before it runs: the old data and the new test data (families, models, batches, sites, operators, batteries) go; clusters and API tokens stay. The product owner reviews the accounts and says which to keep; new accounts are set up with the new roles.
  Planned in two parts (product owner, 30 Oct 2026), each ending with a working app: **3d-1, the old app's code goes** (the cluster admins' old passport pages and the views only they use, the old services and repositories, the demo sample battery and the startup seeding of the old model, the old Next.js app `web-next-legacy`, and the tests of all this; no data is touched) and **3d-2, the reset** (the product owner reviews the accounts first; a script the product owner runs clears the old data, the new test data and the four sample tokens, after a dry run on a throwaway database; clusters, real API tokens and the audit log stay, and the log records the reset). The `BatteryPass` folder with the v1.2 aspect models stays in the repository as reference, with its README; the app no longer reads it (product owner, 30 Oct 2026, amending section 13).
- **3d-3, demo data** (product owner, 30 Oct 2026, decision 32), after 3d-2: a demo data set that shows the app's features, which the team can reset or clear from one "Demo data" card on the admin pages without touching real data. Every record it makes carries a demo mark; a demo record that real data uses stays and loses its mark. Planned in two parts if it grows long: the seed and clearing with their tests, then the card.
- **3e, a cleaner look** (product owner, 30 Oct 2026), after 3d-3: the admin and cluster admin pages (registered clusters, users, API tokens, the audit log, the cluster admins' users and tokens) and the help get a cleaner, friendlier look and layout, building on decision 27's finish, with mock-ups first for the product owner's approval. The help becomes one place that shows each reader only the parts for their role. Settled on 30 Oct 2026 (decision 33, which also folds in the demo-token restart fix) and planned in `docs/superpowers/plans/2026-10-30-dpp-v3-step3e-cleaner-look.md`.

**Where it runs (agreed 28 Oct 2026):** until the cut-over, the new version runs only on the product owner's PC, against the Atlas database `battery_pass_v3` (a launcher outside the repository sets `MONGODB_URI` and `MONGODB_DB` for its own process; any new configuration or launch profile must set `MONGODB_DB`, because the app falls back to `battery_pass_demo`). The live app on Elastic Beanstalk (branch `security/demo-api-hardening`, database `battery_pass_demo`) is not changed. Step 3's switch-over and reset run only against `battery_pass_v3`. Nothing in the rebuild touches the running app or its data. When all steps are done, the new version is deployed to AWS connected to `battery_pass_v3`, if that adds no cost; if it would, the old database is reset to match the new app instead.

## 15. Judgement calls

Choices this design makes where the file and the 21 decisions don't decide. Please check these first.

| Ref | Choice | Why |
|---|---|---|
| J-1 | The live set changes in place; everything else makes a new version (4.2) | The file allows exceptions for dynamic data (#5); a new version per state-of-charge reading would bury the real history |
| J-2 | A successor passport gets a new #6 | The repurposed or remanufactured battery is placed on the market again, possibly by another operator; the file asks for a new passport linked to the old one |
| J-3 | The signature leaves out #2, #4 and the live set (4.3) | They change without a new version; signing them would break older signatures |
| J-4 | #4 is set at publication and on live updates, not on draft edits | The file defines it as the time an update was first made available to third parties |
| J-5 | Value metadata (generated at, source, who) is stored and shown, but not put in the passport JSON | The file asks for it; the schema has no field for it |
| J-6 | Drafts are visible to the team only | A draft isn't on the market. Today market surveillance and the Commission also see drafts. |
| J-7 | LI roles are app-wide; the team checks the legitimate interest when granting the role | The app has no way to check an interest per battery |
| J-8 | Only the team can undo "invalid" | Keeps a market surveillance finding from being reversed by mistake |
| J-9 | Conditional attributes never block publication; the editor shows their condition | Avoids extra yes/no switches that aren't in the file |
| J-10 | Cluster API tokens write only #63, #83, #87 and E1–E4 | Machines feed live data; status changes and accident reports need a person |
| J-11 | Capacity fade can be suggested from a measured remaining capacity, which isn't stored | Helps the status change without adding an attribute |
| J-12 | A change set is all-or-nothing per pack; keys rejected for type, rights or validity are reported but don't block the rest | One bad key in a feed shouldn't drop good live values |
| J-13 | The old passport stays "active", with its new status, until the successor is published or linked | The file makes it "inactive" once linked to the new passport |
| J-14 | #92 links to the pack's accident page in the app, which lists uploaded reports or says none were reported | The schema allows one document; accidents can happen more than once, and the attribute is mandatory from publication |
| J-15 | A repair turns "original" into "re-used"; a battery already "re-used", "remanufactured" or "repurposed" keeps its status and only gains a history entry | Keeps the record that a battery was remanufactured or repurposed; the DIN note is written for original batteries |

## 16. Open items

| Ref | Item | Proposal |
|---|---|---|
| O-1 | Identifier scheme (decision 5) | Choose the issuing agency and register before go-live. Test IDs until then. |

Decided on 28 Oct 2026 (decisions 17–21 in the decision record):

- **O-2, BMS mapping:** #83 from `equivalentChargeAndDischargeCycles`, #87 from the latest measured pack temperature. The request to the export team is in 8.5.
- **O-3, manufacturing date (#17):** year and month (YYYY-MM), as the file says. BatteryPass-Ready should be told that their schema's format is a full date.
- **O-4, repairs:** recorded as "repaired" under status "re-used" on the same passport, with a repair history (7.9). A new passport only when the battery is placed on the market again.
- **O-5, battery picture:** one picture per battery family, set by the team, shown on the public summary. Not passport data.
- **O-6, schema copies:** the two v2.0.1 Other industrial schema files go into the repository in step 1, credited under CC BY 4.0.

## Appendix A — Attribute catalogue

One row per attribute of the file. Generated from the file and the schema; the compliance test (section 11) keeps the code in step with it.

- **Level:** *Passport (system)* is set by the app per version. *Pack*, *Batch* (model × calendar year × manufacturing site), *Site* and *Model* as in section 4. *Model; re-stated per pack* means the model value until a status change re-states it for the pack.
- **Who sees it:** Public; *Purchaser + MSA* (the battery's cluster and market surveillance); *Shared* (the battery's cluster, market surveillance, the Commission and the LI roles named in brackets); *Authorities* (notified bodies, market surveillance, the Commission). The team sees everything. Section 5.2 has the full matrix.
- **Changes:** *Fixed* means set once; a correction makes a new version. *Live (in place)* means no new version.
- **Format (unit):** the value's shape as the schema defines it; the app adds units. *Document link* is a web address and content type, with optional language and title.
- **OI:** the requirement for Other industrial batteries above 2 kWh, from the file: **M** = mandatory under the Battery Regulation; **(M)** = mandatory under ESPR and the JTC-24 standards; **C** = conditional.
- **Feb 27:** mandatory from February 2027 (file).
- **v2.0.1 field:** the path in the schema.

| # | Attribute | Level | Who sees it | Who edits it | Changes | Format (unit) | OI | Feb 27 | v2.0.1 field |
|---|---|---|---|---|---|---|---|---|---|
| 1 | DPP Schema version | Passport (system) | Public | System (constant per schema) | Fixed | Text | (M) | yes | `dppHeader.dppSchemaVersion` |
| 2 | DPP Status | Passport (system) | Public | System lifecycle; market surveillance or team may set 'invalid'; team sets 'marked-for-deletion' | Lifecycle | Status (active / archived / inactive / invalid / marked-for-deletion) | (M) | yes | `dppHeader.dppStatus` |
| 3 | DPP Granularity | Passport (system) | Public | System (always 'item') | Fixed | Fixed value: item | (M) | yes | `dppHeader.granularity` |
| 4 | Date-time of latest update of DPP | Passport (system) | Public | System (on publication and on live updates) | On every published update and live update | Timestamp (UTC, ISO 8601) | (M) | yes | `dppHeader.lastUpdated` |
| 5 | Unique battery passport identifier / unique DPP identifier | Passport (system) | Public | System (new ID per version) | New per version | ID, as a URI | M | yes | `dppHeader.digitalProductPassportId` |
| 6 | Unique battery identifier / unique product identifier | Pack | Public | System at creation (identifier scheme) | Fixed | ID (EN 18219), as a URI | M | yes | `dppHeader.uniqueProductIdentifier` |
| 7 | Unique economic operator identifier | Pack | Authorities | Team at creation | Fixed | ID (EN 18219), as a URI | (M) | yes | `dppHeader.economicOperatorId` |
| 10 | Battery model identifier | Batch | Public | Team (batch) | Fixed | Text | M | yes | `identifiersAndProductData.batteryModelIdentifier` |
| 11 | Battery serial number | Pack | Public | Team at creation | Fixed | Text | M | yes | `identifiersAndProductData.batterySerialNumber` |
| 12 | Unique manufacturer identifier | Site | Authorities | Team (site) | Fixed | ID (EN 18219), as a URI | M | yes | `identifiersAndProductData.uniqueManufacturerIdentifier` |
| 13 | Third party service provider | Pack | Authorities | Team at creation; team again if the provider changes | If provider changes | Text | (M) | yes | `identifiersAndProductData.thirdPartyServiceProvider` |
| 14 | Economic operator information | Pack | Public | Team at creation | Fixed | Operator details (name, trade name, postal address; web and e-mail optional) | M | yes | `identifiersAndProductData.economicOperatorInformation` |
| 15 | Manufacturer information | Site | Public | Team (site) | Fixed | Operator details (name, trade name, postal address; web and e-mail optional) | M | yes | `identifiersAndProductData.manufacturerInformation` |
| 16 | Manufacturing place | Site | Public | Team (site) | Fixed | Text | M | yes | `identifiersAndProductData.manufacturingPlace` |
| 17 | Manufacturing date | Pack | Public | Team at creation | Fixed | Date (YYYY-MM; decision 18) | M | yes | `identifiersAndProductData.manufacturingDate` |
| 19 | Warranty period of the battery | Pack | Public | Team at creation | Fixed | Period (ISO 8601, e.g. P3Y6M) | C | yes | `identifiersAndProductData.warrantyPeriod` |
| 20 | Battery category | Model | Public | Team (model) | Fixed | Fixed value: industrial/non-stationary battery | M | yes | `identifiersAndProductData.batteryCategory` |
| 21 | Battery mass | Model | Public | Team (model) | Fixed | Decimal (kg) | M | yes | `identifiersAndProductData.batteryMass` |
| 22 | Battery status | Pack | Purchaser + MSA | Team + battery's cluster admins (status-change flow) | On status change | Status (original / re-used / remanufactured / repurposed / waste) | M | yes | `identifiersAndProductData.batteryStatus` |
| 23 | Separate collection symbol | Model | Public | Team (model) | Fixed | Document link | M | yes | `symbolsLabelsAndDocumentationOfConformity.separateCollectionSymbol` |
| 24 | Symbols for cadmium and lead | Model | Public | Team (model) | Fixed | Document link | C | yes | `symbolsLabelsAndDocumentationOfConformity.symbolsForCadmiumAndLead` |
| 25 | Carbon footprint label | Batch | Public | Team (batch) | Fixed | Document link | M | no | `symbolsLabelsAndDocumentationOfConformity.carbonFootprintLabel` |
| 26 | Extinguishing agent | Model | Public | Team (model) | Fixed | Fire class + extinguishing agent (texts) | M | yes | `symbolsLabelsAndDocumentationOfConformity.extinguishingAgent` |
| 27 | Meaning of labels and symbols | Model | Public | Team (model) | Fixed | Text | M | yes | `symbolsLabelsAndDocumentationOfConformity.meaningOfLabelsAndSymbols` |
| 28 | EU declaration of conformity | Batch | Public | Team (batch) | Fixed | Document link | M | yes | `symbolsLabelsAndDocumentationOfConformity.euDeclarationOfConformity` |
| 29 | Results of test reports proving compliance | Batch | Authorities | Team (batch) | Fixed | Document link | M | yes | `symbolsLabelsAndDocumentationOfConformity.resultsOfTestReportsProvingCompliance` |
| 30 | Battery carbon footprint per Functional Unit | Batch | Public | Team (batch) | Fixed | Decimal (kgCO2eq/kWh) | M | no | `batteryCarbonFootprint.batteryCarbonFootprintPerFunctionalUnit` |
| 31 | Contribution of raw material acquisition and pre-processing lifecycle stage | Batch | Public | Team (batch) | Fixed | Decimal (kgCO2eq/kWh) | M | no | `batteryCarbonFootprint.contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage` |
| 32 | Contribution of main product production lifecycle stage | Batch | Public | Team (batch) | Fixed | Decimal (kgCO2eq/kWh) | M | no | `batteryCarbonFootprint.contributionOfMainProductProductionLifecycleStage` |
| 33 | Contribution of distribution lifecycle stage | Batch | Public | Team (batch) | Fixed | Decimal (kgCO2eq/kWh) | M | no | `batteryCarbonFootprint.contributionOfDistributionLifecycleStage` |
| 34 | Contribution of end of life and recycling lifecycle stage | Batch | Public | Team (batch) | Fixed | Decimal (kgCO2eq/kWh) | M | no | `batteryCarbonFootprint.contributionOfEndOfLifeAndRecyclingLifecycleStage` |
| 35 | Carbon footprint performance class | Batch | Public | Team (batch) | Fixed | Text | M | no | `batteryCarbonFootprint.carbonFootprintPerformanceClass` |
| 36 | Web link to public carbon footprint study | Batch | Public | Team (batch) | Fixed | Web link (URL) | M | no | `batteryCarbonFootprint.webLinkToPublicCarbonFootprintStudy` |
| 38 | Information of due diligence report | Model | Public | Team (model) | Fixed | Document link | M | no | `supplyChainDueDiligence.informationOfDueDiligenceReport` |
| 41 | Battery chemistry | Model | Public | Team (model) | Fixed | Chemistry code (e.g. Li-ion NMC) + optional text | M | yes | `batteryMaterialsAndComposition.batteryChemistry` |
| 42 | Critical raw materials | Model | Public | Team (model) | Fixed | List of texts | M | yes | `batteryMaterialsAndComposition.criticalRawMaterials` |
| 43 | Materials used in cathode, anode and electrolyte | Model | Shared (repair, recycling) | Team (model) | Fixed | List of texts | M | yes | `batteryMaterialsAndComposition.materialsUsedInCathodeAnodeAndElectrolyte` |
| 44 | Hazardous substances and their impact | Model | Public | Team (model) | Fixed | List of substances (name, CAS/EC no., concentration, hazard and precautionary phrases, location) | M | yes | `batteryMaterialsAndComposition.hazardousSubstancesAndTheirImpact` |
| 45 | Dismantling information: Manuals for the removal and the disassembly of the battery pack | Model | Shared (repair, recycling) | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.dismantlingInformation` |
| 46 | Part numbers for components | Model | Shared (repair) | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.partNumbersForComponents` |
| 47 | Information on sources of spare parts | Model | Shared (repair) | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.informationOnSourcesOfSpareParts` |
| 48 | Safety measures | Model | Shared (repair, recycling, treatment) | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.safetyMeasures` |
| 49 | Nickel recycled content share | Batch | Public | Team (batch) | Fixed | Integer (%) | C | no | `circularityAndResourceEfficiency.nickelRecycledContentShare` |
| 50 | Cobalt recycled content share | Batch | Public | Team (batch) | Fixed | Integer (%) | C | no | `circularityAndResourceEfficiency.cobaltRecycledContentShare` |
| 51 | Lithium recycled content share | Batch | Public | Team (batch) | Fixed | Integer (%) | C | no | `circularityAndResourceEfficiency.lithiumRecycledContentShare` |
| 52 | Lead recycled content share | Batch | Public | Team (batch) | Fixed | Integer (%) | C | no | `circularityAndResourceEfficiency.leadRecycledContentShare` |
| 53 | Renewable content share | Batch | Public | Team (batch) | Fixed | Integer (%) | M | yes | `circularityAndResourceEfficiency.renewableContentShare` |
| 54 | Information on the role of end-users in contributing to waste prevention | Model | Public | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.informationOnTheRoleOfEndUsersInContributingToWastePrevention` |
| 55 | Information on the role of end-users in contributing to the separate collection of waste batteries | Model | Public | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries` |
| 56 | Information on battery collection, preparation for second life and on treatment at end of life | Model | Public | Team (model) | Fixed | Document link | M | yes | `circularityAndResourceEfficiency.informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife` |
| 57 | Rated capacity | Model | Public | Team (model) | Fixed | Integer (Ah) | M | no | `performanceAndDurability.ratedCapacity` |
| 59 | Capacity fade | Model; re-stated per pack | Purchaser + MSA | Team (model); team + battery's cluster admins re-state it per pack | Re-stated on status change | Integer (%) | M | yes | `performanceAndDurability.capacityFade` |
| 63 | State of Charge (SoC) | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer (%) | M | yes | `performanceAndDurability.stateOfChargeSoC` |
| 64 | Minimum voltage | Model | Public | Team (model) | Fixed | Decimal (V) + optional temperature range (°C) | M | yes | `performanceAndDurability.minimumVoltage` |
| 65 | Maximum voltage | Model | Public | Team (model) | Fixed | Decimal (V) + optional temperature range (°C) | M | yes | `performanceAndDurability.maximumVoltage` |
| 66 | Nominal voltage | Model | Public | Team (model) | Fixed | Decimal (V) + optional temperature range (°C) | M | yes | `performanceAndDurability.nominalVoltage` |
| 67 | Original power capability | Model | Public | Team (model) | Fixed | Two decimals (W at 80 % and at 20 % SoC) + optional temperature range | M | yes | `performanceAndDurability.originalPowerCapability` |
| 69 | Power fade | Model; re-stated per pack | Purchaser + MSA | Team (model); team + battery's cluster admins re-state it per pack | Re-stated on status change | Integer (%) | M | yes | `performanceAndDurability.powerFade` |
| 70 | Maximum permitted battery power | Model | Public | Team (model) | Fixed | Decimal (W) + optional temperature range (°C) | M | yes | `performanceAndDurability.maximumPermittedBatteryPower` |
| 71 | Initial round trip energy efficiency | Model | Public | Team (model) | Fixed | Integer (%) | C | yes | `performanceAndDurability.initialRoundTripEnergyEfficiency` |
| 72 | Round trip energy efficiency at 50% of cycle life | Model | Public | Team (model) | Fixed | Integer (%) | C | yes | `performanceAndDurability.roundTripEnergyEfficiencyAt50PerCentOfCycleLife` |
| 74 | Energy round trip efficiency fade | Model; re-stated per pack | Purchaser + MSA | Team (model); team + battery's cluster admins re-state it per pack | Re-stated on status change | Integer (%) | C | yes | `performanceAndDurability.energyRoundTripEfficiencyFade` |
| 78 | Initial internal resistance of battery cell and pack (module recommended) | Model | Public | Team (model) | Fixed | Integers (Ohm) for pack and cell; module optional | M | yes | `performanceAndDurability.initialInternalResistanceOfBatteryCellAndPackModuleRecommended` |
| 79 | Internal resistance increase of pack (cell and module recommended) | Model; re-stated per pack | Purchaser + MSA | Team (model); team + battery's cluster admins re-state it per pack | Re-stated on status change | Integers (%) for pack; cell and module optional | M | yes | `performanceAndDurability.internalResistanceIncreaseOfPackCellAndModuleRecommended` |
| 81 | Expected lifetime in calendar years | Model; re-stated per pack | Purchaser + MSA | Team (model); team + battery's cluster admins re-state it per pack | Re-stated on status change | Decimal (years) | M | yes | `performanceAndDurability.expectedLifetimeInCalendarYears` |
| 82 | Expected lifetime - Number of charge-discharge cycles | Model | Public | Team (model) | Fixed | Integer | C | yes | `performanceAndDurability.expectedLifetimeNumberOfChargeDischargeCycles` |
| 83 | Number of full charging and discharging cycles | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer | C | yes | `performanceAndDurability.numberOfFullChargingAndDischargingCycles` |
| 84 | Cycle-life reference test | Model | Public | Team (model) | Fixed | Text | C | yes | `performanceAndDurability.cycleLifeReferenceTest` |
| 85 | C-rate of relevant cycle-life test | Model | Public | Team (model) | Fixed | Decimal (A/Ah) | C | yes | `performanceAndDurability.cRateOfRelevantCycleLifeTest` |
| 87 | Temperature information | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer (°C) | M | yes | `performanceAndDurability.temperatureInformation` |
| 88 | Temperature range idle state, lower boundary | Model | Public | Team (model) | Fixed | Integer (°C) | M | yes | `performanceAndDurability.temperatureRangeIdleStateLowerBoundary` |
| 89 | Temperature range idle state, upper boundary | Model | Public | Team (model) | Fixed | Integer (°C) | M | yes | `performanceAndDurability.temperatureRangeIdleStateUpperBoundary` |
| 92 | Information on accidents | Pack | Purchaser + MSA | Team + battery's cluster admins (form, with file upload) | Live (in place) | Document link | M | yes | `performanceAndDurability.informationOnAccidents` |

**Added beyond the file (decision 28).** From the Polarion dynamic attribute list (DIN DKE 99100). Not in the V3 file or the v2.0.1 schema; the schema allows extra fields, so the passport still validates. They never block publication.

| # | Attribute | Level | Who sees it | Who enters it | Changes | Format (unit) | DIN DKE | Feb 27 | Passport property |
|---|---|---|---|---|---|---|---|---|---|
| E1 | Time spent in extreme temperatures above boundary | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer (min) | 6.7.7.5 | — | `performanceAndDurability.timeSpentInExtremeTemperaturesAboveBoundary` |
| E2 | Time spent in extreme temperatures below boundary | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer (min) | 6.7.7.6 | — | `performanceAndDurability.timeSpentInExtremeTemperaturesBelowBoundary` |
| E3 | Time spent charging during extreme temperatures above boundary | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer (min) | 6.7.7.7 | — | `performanceAndDurability.timeSpentChargingDuringExtremeTemperaturesAboveBoundary` |
| E4 | Time spent charging during extreme temperatures below boundary | Pack | Purchaser + MSA | Battery-system feed (API/upload) + team + battery's cluster admins | Live (in place) | Integer (min) | 6.7.7.8 | — | `performanceAndDurability.timeSpentChargingDuringExtremeTemperaturesBelowBoundary` |

## Appendix B — Schema notes

Checked against `Other_Industrial_Above_2kWh_2.0.1.json` on 28 Oct 2026.

**B.1 Names.** Six technical names in the file's "Technical references" tab differ from the schema. The app uses the schema's names:

| # | File's tab | Schema |
|---|---|---|
| 33 | `ContributionOfDistributionLifecycleStage` | `contributionOfDistributionLifecycleStage` |
| 34 | `ContributionOfEndOfLifeAndRecyclingLifecycleStage` | `contributionOfEndOfLifeAndRecyclingLifecycleStage` |
| 43 | `materialsUsedinCathodeAnodeAndElectrolyte` | `materialsUsedInCathodeAnodeAndElectrolyte` |
| 63 | `stateOfCharge` | `stateOfChargeSoC` |
| 79 | `internalResistanceIncreaseOfPackCell AndModuleRecommended` (stray space) | `internalResistanceIncreaseOfPackCellAndModuleRecommended` |
| 82 | `expectedLifetimeNumberofChargeDischargeCycles` | `expectedLifetimeNumberOfChargeDischargeCycles` |

Property names are unique across the schema's sections, so uploads can use them without the section name.

**B.2 Value shapes.** Where the schema is more precise than the file's format column, Appendix A follows the schema:

- **Identifiers** #5, #6, #7 and #12 are URIs.
- **Manufacturing date** #17: the file says year and month (YYYY-MM); the schema's format is a full date. The app follows the file (decision 18). Format checks are only annotations by default in JSON Schema 2020-12, so validation passes.
- **Operator details** #14 and #15: name, registered trade name and postal address are required; web and e-mail addresses are optional.
- **Fixed values:** #3 is always "item"; #20 is always "industrial/non-stationary battery".
- **Status values** — #2: active, archived, inactive, invalid, marked-for-deletion. #22: original, re-used, remanufactured, repurposed, waste.
- **#26** is a fire class plus an extinguishing agent; **#41** is a chemistry code from a fixed list (Li-ion LCO, LFP, LMO, NCA, NMC; Li-metal; Na-ion; Ni-Cd; Ni-MH; Pb) plus optional text; **#44** is a list of substances, each with name, CAS or EC number, concentration, hazard and precautionary phrases, and an optional location.
- **Voltages and power:** #64–#66 and #70 can carry the temperature range they were measured in; #67 has two values, at 80 % and at 20 % state of charge.
- **Pack, cell, module:** #78 needs pack and cell values (module optional); #79 needs the pack value (cell and module optional).
- **Temperature** #87 is one whole number in °C, not a histogram.
- **Documents:** 14 attributes are one `RelatedResource` each; #36 is a plain web address.
- **Units** are fixed strings per value (for example "%", "°C", "kgCO2-eq/kWh"). The file says units are always given; the app adds them.

**B.3 Required fields.** The full schema's required lists match the file's M and (M) marks for Other industrial; the conditional attributes (C) are optional. The Feb-2027 variant (`Other_Industrial_Above_2kWh_Feb27_2.0.1.json`) requires the same fields minus every attribute marked "Feb 27: no": it has no carbon footprint or due diligence section, and doesn't require #25 or #57. So the catalogue's publication gate and the Feb-2027 schema agree.

**B.4 Schema properties not in the file.** These ten are optional in the Other industrial schema and the app doesn't emit them: `dppHeader.contentSpecificationIds`, `dppHeader.facilityId`, `absoluteBatteryCarbonFootprint`, `dateOfPuttingTheBatteryIntoService`, `remainingCapacity`, `remainingPowerCapability`, `numberOfDeepDischargeEvents`, `numberOfOverchargeEvents`, `thirdPartyAssurancesOfRecognisedSchemes`, `supplyChainIndices`.

## Appendix C — Comparison workbook coverage

How each row of `DPP_V3_vs_App_comparison_2026-10-28_v2.xlsx` is covered. Generated from the workbook.

**Tab 'Attribute comparison' (73 rows):** every attribute is in Appendix A and built in steps 1–5.

**Tab 'Decisions' (73 rows):** every row is settled by adopting the file, in the way this design describes (for example, site data is linked to each battery rather than copied). The three rows of your own rules follow decisions 9, 10, 11 and 15.

| What differs | Rows | Step | Section |
|---|---|---|---|
| Who edits (your rule) | D-09 (#10) | 4 | 6.1, 7.8 (decision 9) |
| Who edits (your rule) | D-35 (#43) | 2 | 6.1 (decision 10) |
| Who edits (your rule) | D-43 (#59) | 4 | 7.4 (decisions 11, 15) |
| Who can see it | D-01 (#22), D-03 (#29), D-04 (#63), D-05 (#83), D-06 (#4), D-11 (#16), D-14 (#28), D-17 (#30), D-28 (#35), D-30 (#36), D-32 (#38), D-33 (#42), D-34 (#43), D-40 (#54), D-41 (#55), D-42 (#57), D-45 (#66), D-47 (#70), D-48 (#81), D-50 (#82) | 3 | 5 |
| Family/model vs battery level | D-07 (#10), D-10 (#15), D-12 (#16), D-15 (#28), D-16 (#29), D-18 (#30), D-20 (#31), D-22 (#32), D-24 (#33), D-26 (#34), D-29 (#35), D-31 (#36), D-36 (#49), D-37 (#50), D-38 (#51), D-39 (#52) | 1 | 4.1 |
| Set at creation / editable / dynamic | D-02 (#22), D-49 (#81) | 4 | 7.4 |
| Set at creation / editable / dynamic | D-08 (#10), D-13 (#16) | 2 | 6.1 |
| Set at creation / editable / dynamic | D-44 (#63), D-51 (#83) | 5 | 8 |
| Content / format | D-19 (#30), D-21 (#31), D-23 (#32), D-25 (#33), D-27 (#34), D-46 (#67), D-55 (#10), D-56 (#15), D-57 (#16), D-58 (#17), D-59 (#22), D-61 (#35), D-63 (#42), D-64 (#43), D-65 (#49), D-66 (#50), D-67 (#51), D-68 (#52), D-72 (#70) | 1 | App. A, B |
| Content / format | D-52 (#2), D-53 (#4) | 2 | 4.2 |
| Content / format | D-54 (#6) | 2 | 10 |
| Content / format | D-60 (#28), D-62 (#36), D-69 (#54), D-70 (#55) | 2 | 9 |
| Content / format | D-71 (#63), D-73 (#83) | 5 | 8 |

**Tab 'App extras' (34 rows):**

| Ref | App parameter | Outcome | Step | Section |
|---|---|---|---|---|
| X-01 | Cluster | Kept as app mechanics; hand-over flow | 4 | 7.8 |
| X-02 | Battery Family | Kept as the family grouping | 1 | 4.1 |
| X-03 | Battery Model as a version label ('1.0', '2.0') | Removed; not in the file | 3 | 13 |
| X-04 | Model number, display name | Removed; not in the file | 3 | 13 |
| X-05 | Software version | Removed (decision 9) | 3 | 13 |
| X-06 | Software release date, software latest update | Removed; not in the file | 3 | 13 |
| X-07 | Battery image | Kept: one picture per family, set by the team (decision 20) | 2 | 12 |
| X-08 | Module count, product description | Kept as app data, not passport data: a module list per battery and a description per model (decision 31) | 3 | 4.1, 12 |
| X-09 | Rated energy (kWh) | Removed; not in the file | 3 | 13 |
| X-10 | Remaining capacity (%) | Removed; only used to suggest #59 at a status change | 3 | 7.4 |
| X-11 | Remaining energy (kWh) | Removed; not in the file | 3 | 13 |
| X-12 | Material masses x8 (kg) with component and CAS | Removed; not in the file | 3 | 13 |
| X-13 | Supply chain index | Removed; not in the file | 3 | 13 |
| X-14 | Sustainability report (document) | Removed; not in the file | 3 | 13 |
| X-15 | Third-party audit (document) | Removed; not in the file | 3 | 13 |
| X-16 | Taxonomy report (document) | Removed; not in the file | 3 | 13 |
| X-17 | Recycled content: post-consumer share x4 | Removed; not in the file | 3 | 13 |
| X-18 | Recycled content: primary-material share x4 | Removed; not in the file | 3 | 13 |
| X-19 | Recycled content share verification | Removed; not in the file | 3 | 13 |
| X-20 | Absolute carbon footprint | Removed; schema property not in V3 | 3 | App. B |
| X-21 | Telemetry: current consumption, charge level, voltage, current (+ timestamp) | Replaced by the live values and their history (decision 29); the old telemetry data goes | 3 | 8.4 |
| X-22 | Operational status (active / not active) | Removed with the telemetry and operations endpoints | 3 | 8.4 |
| X-23 | Location of use (site, address, city, country, latitude, longitude) | Removed with the telemetry and operations endpoints | 3 | 8.4 |
| X-24 | Contact person (name, e-mail, phone) | Removed with the telemetry and operations endpoints | 3 | 8.4 |
| X-25 | Passport workflow states (Draft / Signed / Published) and 'Latest / Historical passport' badge | Replaced by the DPP status (#2) and drafts | 2 | 4.2 |
| X-26 | Trust & conformance (signature proof, hash, issuer, revision) | Kept; signs the static core | 2 | 4.3 |
| X-27 | Date of putting into service | Removed; schema property not in V3 | 3 | App. B |
| X-28 | State of certified energy (SOCE), capacity threshold for exhaustion | Removed; not in the file | 3 | 13 |
| X-29 | Self-discharge: initial, current, evolution | Removed; not in the file | 3 | 13 |
| X-30 | Remaining power capability, remaining round-trip efficiency | Removed; not in the file | 3 | 13 |
| X-31 | Energy throughput, capacity throughput | Removed; not in the file | 3 | 13 |
| X-32 | Time in extreme temperatures (4 values) | Kept as E1–E4, live values beyond the file (decision 28) | 3, 5 | Appendix A |
| X-33 | Power capability ratio, generic C-rate | Removed; not in the file | 3 | 13 |
| X-34 | Duplicates under batteryCondition (powerFade, roundTripEfficiencyFade, roundTripEfficiencyat50PerCentCycleLife, remainingEnergy.remainingEnergyalue) | Removed; not in the file | 3 | 13 |

**Tab 'Design impacts' (17 rows):**

| Ref | Topic | Section |
|---|---|---|
| I-01 | Four audiences, not two | 5 |
| I-02 | Purchaser data belongs to the battery's cluster (your rule, 28 Oct) | 5.2 |
| I-03 | Cluster admins edit purchaser data (your rule) | 6.1 |
| I-04 | Batch level for carbon footprint, recycled content and conformity documents | 4.1 |
| I-05 | Manufacturer data belongs to the manufacturing site | 4.1 |
| I-06 | DPP header (EN 18223) is a new public block | 4.2, App. A (#1–#5) |
| I-07 | Identifier formats | 10 |
| I-08 | Battery status drives passport versions | 7.4–7.6 |
| I-09 | The dynamic data set and where it comes from | 8 |
| I-10 | 'Static' values that are re-stated on events | 7.4 |
| I-11 | Data model v2.0 replaces v1.2 | 2, 13 |
| I-12 | V3 was cut for 'Other Industrial >2 kWh' | 2 |
| I-13 | Phasing | 11 |
| I-14 | The public summary becomes the main regulated view | 12 |
| I-15 | Documents and links | 9 |
| I-16 | Placeholder and demo values | 13 |
| I-17 | Units and formats | App. A, B |

**Tab 'Removed in V3' (19 rows):** they stay out (decision 4). Needed again only if a product is placed on the market in another battery category.
