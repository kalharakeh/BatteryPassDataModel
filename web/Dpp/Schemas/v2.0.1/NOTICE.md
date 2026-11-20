# Official BatteryPass-Ready schemas (v2.0.1)

These two files are unchanged copies of the BatteryPass-Ready JSON schemas for Other industrial batteries above 2 kWh (decision 21 in `docs/superpowers/specs/2026-10-28-dpp-v3-compliance-rebuild-decisions.md`).

| File | SHA-256 |
|---|---|
| `Other_Industrial_Above_2kWh_2.0.1.json` | `31c0c4a5bdbbf2dc0d4908cfb94db1a0da1a65d7b41d545a04029a94c97000ab` |
| `Other_Industrial_Above_2kWh_Feb27_2.0.1.json` | `aa2ef3f30bd967023946032e338c02ca82f03bc8d26b0a110f0edeafbe4604f5` |

- **Source:** https://git.gefeg.com/bp/BatteryPassDataModel, folder `JSONSchemaFiles/v2.0.1`, commit `64761a79bc287f2e0eb371837c1d02e4eacb9c37` (23 Sep 2026, "v2.0.1 updated").
- **Copied:** 28 Oct 2026.
- **Licence:** CC BY 4.0 (https://creativecommons.org/licenses/by/4.0), as stated in the repository's README.
- **Copyright:** © 2026 BatteryPass-Ready Consortium; maintained by acatech on the consortium's behalf.

The app embeds both files (see `web/BatteryPassWeb.csproj`) and checks passports and single values against them (`OfficialSchemas`). Don't edit them: `OfficialSchemasTests` compares their SHA-256 with the table above, and `.gitattributes` stops git from changing their line endings. To move to a newer version, add the new files beside these and update this note and the tests.
