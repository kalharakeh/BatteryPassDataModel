import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { additionalSampleClusterSeeds, allSampleClusters, defaultSampleCluster } from "../src/lib/seed/sample-clusters";
import { samplePassport } from "../src/lib/seed/sample-passport";

function aspectPayload(passport: (typeof additionalSampleClusterSeeds)[number]["passport"], aspect: "generalProductInformation" | "carbonFootprintForBatteries" | "performanceAndDurability") {
  return passport.aspects[aspect]?.payload ?? {};
}

function generatedValueSet(
  picker: (passport: (typeof additionalSampleClusterSeeds)[number]["passport"]) => unknown,
) {
  return new Set(additionalSampleClusterSeeds.map((seed) => picker(seed.passport)));
}

describe("sample cluster seed data", () => {
  it("creates four extra cluster/battery/account groups with distinct cluster assignments", () => {
    assert.equal(additionalSampleClusterSeeds.length, 4);
    assert.equal(defaultSampleCluster.clusterId, "cluster-default-demonstrator");

    const clusterIds = new Set(additionalSampleClusterSeeds.map((seed) => seed.cluster.clusterId));
    const passportIds = new Set(additionalSampleClusterSeeds.map((seed) => seed.passport.passportId));
    const normalUsers = new Set(additionalSampleClusterSeeds.map((seed) => seed.normalUser.email));
    const adminUsers = new Set(additionalSampleClusterSeeds.map((seed) => seed.adminUser.email));

    assert.equal(clusterIds.size, 4);
    assert.equal(passportIds.size, 4);
    assert.equal(normalUsers.size, 4);
    assert.equal(adminUsers.size, 4);

    for (const seed of additionalSampleClusterSeeds) {
      assert.equal(seed.passport.clusterId, seed.cluster.clusterId);
      assert.deepEqual(
        seed.memberships.map((membership) => [membership.email, membership.clusterId, membership.role]),
        [
          [seed.normalUser.email, seed.cluster.clusterId, "member"],
          [seed.adminUser.email, seed.cluster.clusterId, "clusterAdmin"],
        ],
      );
    }
  });

  it("keeps clusters neutral with no category/kind field", () => {
    for (const cluster of allSampleClusters) {
      assert.equal("kind" in cluster, false);
      assert.ok(cluster.clusterId);
      assert.ok(cluster.name);
    }
  });

  it("assigns each seeded battery to its own distinct cluster", () => {
    const passports = [samplePassport, ...additionalSampleClusterSeeds.map((seed) => seed.passport)];
    const clusterIds = passports.map((passport) => passport.clusterId);

    assert.equal(clusterIds.length, 5);
    assert.equal(new Set(clusterIds).size, 5);
    for (const clusterId of clusterIds) {
      assert.ok(allSampleClusters.some((cluster) => cluster.clusterId === clusterId));
    }
  });

  it("uses visibly different sample battery data for each generated battery", () => {
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "generalProductInformation").batteryCategory).size, 4);
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "generalProductInformation").batteryMass).size, 4);
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "generalProductInformation").manufacturingDate).size, 4);
    assert.equal(generatedValueSet((passport) => aspectPayload(passport, "carbonFootprintForBatteries").batteryCarbonFootprint).size, 4);
    assert.equal(generatedValueSet((passport) => {
      const performance = aspectPayload(passport, "performanceAndDurability");
      const technical = (performance.batteryTechicalProperties ?? {}) as Record<string, unknown>;
      return technical.ratedEnergy;
    }).size, 4);
  });
});
