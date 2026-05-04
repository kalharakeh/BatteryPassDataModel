import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { buildPassportFromFormData } from "../src/lib/admin/passport-form";
import { samplePassportId } from "../src/lib/seed/sample-passport";

describe("buildPassportFromFormData", () => {
  it("maps form-only admin fields into canonical aspect payloads and app metadata", () => {
    const form = new FormData();
    form.set("passportId", "did:web:acme.battery.pass:new");
    form.set("name", "EV-BAT100");
    form.set("modelNumber", "M-100");
    form.set("serialNumber", "SER-100");
    form.set("category", "EV");
    form.set("batteryStatus", "Original");
    form.set("batteryMass", "510.5");
    form.set("manufacturingDate", "2024-01-15");
    form.set("facilityId", "Berlin");
    form.set("manufacturerName", "Scania Industrial Batteries");
    form.set("carbonFootprint", "140");
    form.set("performanceClass", "A");
    form.set("stateOfCharge", "77");
    form.set("remainingCapacity", "93");

    const passport = buildPassportFromFormData(form, { existingPassport: null, now: "2026-04-30T00:00:00.000Z" });

    assert.equal(passport.passportId, "did:web:acme.battery.pass:new");
    assert.equal(passport.aspects.generalProductInformation?.payload.productIdentifier, "M-100");
    assert.equal(passport.app.display.serialNumber, "SER-100");
    assert.equal(passport.app.display.name, "EV-BAT100");
    assert.equal(passport.aspects.carbonFootprintForBatteries?.payload.batteryCarbonFootprint, 140);
    assert.equal(
      passport.aspects.performanceAndDurability?.payload.batteryCondition?.stateOfCharge?.stateOfChargeValue,
      77,
    );
  });

  it("preserves existing document and image metadata when fields are not replaced", () => {
    const form = new FormData();
    form.set("passportId", samplePassportId);
    form.set("name", "EV-BAT095");
    form.set("modelNumber", "M-41698615");
    form.set("serialNumber", "992356610548948");
    form.set("category", "EV");
    form.set("batteryStatus", "Original");
    form.set("batteryMass", "499");
    form.set("manufacturingDate", "2023-09-05");
    form.set("facilityId", "Berlin");
    form.set("manufacturerName", "Scania Industrial Batteries");
    form.set("carbonFootprint", "137");
    form.set("performanceClass", "B");

    const existing = buildPassportFromFormData(form, { existingPassport: null, now: "2026-04-30T00:00:00.000Z" });
    existing.app.media.batteryImageUrl = "/sample-battery.png";
    existing.app.documents.euDeclarationOfConformity.url = "/api/files/abc";

    const updated = buildPassportFromFormData(form, { existingPassport: existing, now: "2026-04-30T00:01:00.000Z" });

    assert.equal(updated.app.media.batteryImageUrl, "/sample-battery.png");
    assert.equal(updated.app.documents.euDeclarationOfConformity.url, "/api/files/abc");
  });
});
