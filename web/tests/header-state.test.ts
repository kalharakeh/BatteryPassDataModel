import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { buildHeaderState } from "../src/lib/auth/header-state";
import type { Cluster, ClusterMembership } from "../src/types/passport";

const clusters: Cluster[] = [
  { clusterId: "cluster-north-operations", name: "North Operations Cluster", createdAt: "now", updatedAt: "now" },
];

const memberships: ClusterMembership[] = [
  { email: "north.admin@example.test", clusterId: "cluster-north-operations", role: "clusterAdmin", createdAt: "now", updatedAt: "now" },
];

describe("buildHeaderState", () => {
  it("shows user email and cluster names for logged in users", () => {
    const state = buildHeaderState({ email: "north.admin@example.test", roles: ["viewer"], iat: 0 }, memberships, clusters);

    assert.equal(state.identityLabel, "north.admin@example.test");
    assert.deepEqual(state.clusterNames, ["North Operations Cluster"]);
  });

  it("shows cluster-admin links only for local admins", () => {
    const state = buildHeaderState({ email: "north.admin@example.test", roles: ["viewer"], iat: 0 }, memberships, clusters);

    assert.deepEqual(
      state.links.map((link) => link.href),
      ["/cluster-admin"],
    );
  });

  it("shows only the admin dashboard link for general admins", () => {
    const state = buildHeaderState({ email: "admin@example.test", roles: ["admin", "viewer"], iat: 0 }, [], clusters);

    assert.deepEqual(
      state.links.map((link) => link.href),
      ["/admin"],
    );
  });
});
