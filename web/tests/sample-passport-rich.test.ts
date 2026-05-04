import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { samplePassport, samplePassportId, oldLocalSamplePassportId } from "../src/lib/seed/sample-passport";
import { aspectKeys } from "../src/types/passport";

describe("rich sample passport", () => {
  it("uses the ACME demo DID and archives the old local sample separately", () => {
    assert.equal(samplePassportId, "did:web:acme.battery.pass:0226151e-949c-d067-8ef3-162431e28976");
    assert.equal(samplePassport.passportId, samplePassportId);
    assert.equal(oldLocalSamplePassportId, "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976");
  });

  it("contains every requested Battery Passport section", () => {
    for (const key of aspectKeys) {
      assert.ok(samplePassport.aspects[key]?.payload, `${key} payload should be present`);
    }
  });

  it("contains the expected public overview values", () => {
    const general = samplePassport.aspects.generalProductInformation?.payload ?? {};

    assert.equal(general.productIdentifier, "M-41698615");
    assert.equal(samplePassport.app.display.name, "EV-BAT095");
    assert.equal(samplePassport.app.display.serialNumber, "992356610548948");
    assert.equal(samplePassport.app.display.facilityId, "Berlin");
    assert.equal(samplePassport.app.display.manufacturerName, "Scania Industrial Batteries");
    assert.equal(general.batteryMass, 499);
    assert.equal(general.batteryStatus, "Original");
  });

  it("links all seeded demo documents through app metadata", () => {
    assert.deepEqual(Object.keys(samplePassport.app.documents).sort(), [
      "co2StudyReference",
      "conformityAssessment",
      "dueDiligenceReport",
      "euDeclarationOfConformity",
      "sustainabilityReport",
      "taxonomyReport",
      "thirdPartyAudit",
    ]);
  });

  it("includes circularity recycled content chart data for nickel, cobalt, lithium, and lead", () => {
    assert.deepEqual(
      samplePassport.app.charts.recycledContent.map((item) => item.material),
      ["Nickel", "Cobalt", "Lithium", "Lead"],
    );
  });
});
