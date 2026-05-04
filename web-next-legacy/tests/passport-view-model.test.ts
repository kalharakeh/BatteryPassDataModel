import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { defaultSampleCluster } from "../src/lib/seed/sample-clusters";
import { samplePassport } from "../src/lib/seed/sample-passport";
import { toPassportViewModel } from "../src/lib/view-model/passport-view-model";

describe("toPassportViewModel", () => {
  it("maps canonical aspect payloads into reference-style display fields", () => {
    const vm = toPassportViewModel(samplePassport);

    assert.equal(vm.name, "EV-BAT095");
    assert.equal(vm.modelNumber, "M-41698615");
    assert.equal(vm.serialNumber, "992356610548948");
    assert.equal(vm.category, "EV");
    assert.equal(vm.carbonFootprint, "137");
    assert.equal(vm.sections.includes("Supply chain"), true);
    assert.equal(vm.materialComposition.segments.length > 4, true);
    assert.equal(vm.carbonFootprintStages.total, 137);
    assert.equal(vm.recycledContent.length, 4);
    assert.equal(vm.documents.euDeclarationOfConformity.label, "EU declaration of conformity ID");
  });

  it("includes cluster names when cluster metadata is supplied", () => {
    const vm = toPassportViewModel(samplePassport, { clusters: [defaultSampleCluster] });

    assert.equal(vm.clusterId, "cluster-default-demonstrator");
    assert.deepEqual(vm.clusterNames, ["Default Demonstrator Cluster"]);
    assert.equal(vm.clusterLabel, "Default Demonstrator Cluster");
  });
});
