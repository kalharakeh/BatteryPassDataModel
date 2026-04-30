import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { samplePassport } from "../src/lib/seed/sample-passport";
import { toPassportViewModel } from "../src/lib/view-model/passport-view-model";

describe("toPassportViewModel", () => {
  it("maps canonical aspect payloads into reference-style display fields", () => {
    const vm = toPassportViewModel(samplePassport);

    assert.equal(vm.modelNumber, "EV-BAT095");
    assert.equal(vm.category, "EV");
    assert.equal(vm.carbonFootprint, "137");
    assert.equal(vm.sections.includes("Supply chain"), true);
  });
});
