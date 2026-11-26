# Official BatteryPass-Ready schemas (v2.0.1)

These two files are copies of the BatteryPass-Ready JSON schemas for Other industrial batteries above 2 kWh (decision 21 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`), with one local deviation (below).

| File | SHA-256 as published | SHA-256 in the app (with the deviation) |
|---|---|---|
| `Other_Industrial_Above_2kWh_2.0.1.json` | `31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab` | `3011fdc36ea8a5cf3f8a9e105a5878bf617748e01e22ecab6d20019746e0d33d` |
| `Other_Industrial_Above_2kWh_Feb27_2.0.1.json` | `aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5` | `81cb73db5e2dd3478f4e8339f1550940a1bfb841795a449c32a2728dc425e5f3` |

- **Source:** https://git.gefeg.com/bp/BatteryPassDataModel, folder `JSONSchemaFiles/v2.0.1`, commit `64761a79bc287f2e0eb371837c1d02e4eacb9c37` (23 Sep 2026, "v2.0.1 updated").
- **Copied:** 28 Oct 2026.
- **Licence:** CC BY 4.0 (https://creativecommons.org/licenses/by/4.0), as stated in the repository's README.
- **Copyright:** © 2026 BatteryPass-Ready Consortium; maintained by acatech on the consortium's behalf.

## Local deviation: internal resistance in decimal ohms (finding D06, decision 94)

In `$defs/ohmInteger`, the three values `ohmValueForBatteryModule`, `ohmValueForBatteryPack` and `ohmValueForBatteryCell` have `"type": "number"` in place of `"type": "integer"`. Nothing else differs.

- **Why:** #78, the initial internal resistance, is a few milliohms for a cell or a pack. In whole ohms it can only be entered as 0 (finding D06). The EU's battery format 1.0.0 (`internalCellResistance`, `internalPackResistance`) takes decimals in ohms, and the EU view (package R2) sends them so.
- **Effect:** a whole number stays valid; a decimal is now valid too. The definition keeps its name, so the rest of the schema is untouched.
- **Raised with BatteryPass-Ready:** in the programme's first letter to them (finding D06). When a release takes decimals, replace these files and drop this section.

## Local deviation: keys the schema doesn't define (decisions 28 and 94)

The passports the app makes carry values the v2.0.1 schema doesn't define, under keys of their own in the schema's sections. The schema files are unchanged for this: their sections don't forbid other keys (no `additionalProperties: false`), so the checks pass.

- **E1 to E4** (decision 28): the times spent in extreme temperatures, whole minutes, in `performanceAndDurability`.
- **E5 to E20** (decision 94, package R1): the values the EU's battery format 1.0.0 requires, under the EU's names.
  - **Plain texts:** `manufacturingCity`, `manufacturingRegion`, `manufacturingCountry` (ISO 3166 alpha-3) and `batchNumber`.
  - **Language maps** such as `{"en-GB": "…"}`: `notInUseTemperatureRangeTest`, and the ten end-of-life texts in `circularityAndResourceEfficiency`.
  - **Words:** `chemicalSymbol` ("Cadmium (Cd)", "Lead (Pb)"), as #42 carries names. The records keep the EU's addresses, and the EU view (package R2) sends them.
- **If a later BatteryPass-Ready release defines one of these keys** with another meaning or type, the app's key must move before that release is taken in.

The app embeds both files (see `web/BatteryPassWeb.csproj`) and checks passports and single values against them (`OfficialSchemas`). Don't edit them beyond the deviation above: `OfficialSchemasTests` compares their SHA-256 with the table above, and checks that undoing the deviation gives the published file byte for byte. `.gitattributes` stops git from changing their line endings. To move to a newer version, add the new files beside these and update this note and the tests.
