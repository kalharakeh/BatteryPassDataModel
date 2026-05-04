import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { buildClusterAdminPassportUpdate } from "../src/lib/admin/cluster-passport-form";
import { samplePassport } from "../src/lib/seed/sample-passport";
import type { BatteryPassport } from "../src/types/passport";

function cloneSample(): BatteryPassport {
  return JSON.parse(JSON.stringify({ ...samplePassport, clusterId: "sample-cluster-a" })) as BatteryPassport;
}

describe("buildClusterAdminPassportUpdate", () => {
  it("updates only the local-admin battery fields", () => {
    const form = new FormData();
    form.set("facilityId", "Local Facility 42");
    form.set("stateOfCharge", "66");
    form.set("remainingCapacity", "88.5");
    form.set("remainingEnergy", "79.2");
    form.set("fullCycles", "650");
    form.set("modelNumber", "SHOULD-NOT-CHANGE");

    const updated = buildClusterAdminPassportUpdate(cloneSample(), form, {
      now: "2026-05-04T10:00:00.000Z",
      batteryImageUrl: "/api/files/new-image",
      batteryImageFileId: "new-image",
    });
    const condition = updated.aspects.performanceAndDurability?.payload.batteryCondition;
    const general = updated.aspects.generalProductInformation?.payload;

    assert.equal(updated.app.display.facilityId, "Local Facility 42");
    assert.equal((general?.manufacturingPlace as Record<string, unknown>).streetAddress, "Local Facility 42");
    assert.equal(updated.app.media.batteryImageUrl, "/api/files/new-image");
    assert.equal(updated.app.display.modelNumber, samplePassport.app.display.modelNumber);
    assert.equal(condition?.stateOfCharge?.stateOfChargeValue, 66);
    assert.equal(condition?.remainingCapacity?.remainingCapacityValue, 88.5);
    assert.equal(condition?.remainingEnergy?.remainingEnergyValue, 79.2);
    assert.equal(condition?.numberOfFullCycles?.numberOfFullCyclesValue, 650);
  });

  it("rejects out-of-range local-admin numeric values", () => {
    const form = new FormData();
    form.set("stateOfCharge", "140");
    form.set("remainingCapacity", "88.5");
    form.set("remainingEnergy", "79.2");
    form.set("fullCycles", "650");

    assert.throws(
      () => buildClusterAdminPassportUpdate(cloneSample(), form, { now: "2026-05-04T10:00:00.000Z" }),
      /State of charge must be between 0 and 100/,
    );
  });
});
