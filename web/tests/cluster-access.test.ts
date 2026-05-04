import assert from "node:assert/strict";
import { describe, it } from "node:test";
import {
  administeredClusterIdsForSession,
  canAdministerCluster,
  canOpenPassportDetail,
  filterMembershipsForClusterAdmin,
  filterPassportsForRegistryAccess,
} from "../src/lib/auth/cluster-access";
import { samplePassport } from "../src/lib/seed/sample-passport";
import type { BatteryPassport, ClusterMembership } from "../src/types/passport";

const clusterA = "sample-cluster-a";
const clusterB = "sample-cluster-b";

function passportInCluster(clusterId: string): BatteryPassport {
  return {
    ...samplePassport,
    passportId: `did:web:acme.battery.pass:${clusterId}`,
    clusterId,
  };
}

describe("cluster access decisions", () => {
  it("allows a normal user to open a detail report for a battery in the same cluster", () => {
    const session = { email: "member-a@example.test", roles: ["viewer"], iat: 0 };
    const memberships: ClusterMembership[] = [
      { email: session.email, clusterId: clusterA, role: "member", createdAt: "now", updatedAt: "now" },
    ];

    assert.equal(canOpenPassportDetail(session, passportInCluster(clusterA), memberships), true);
  });

  it("blocks a normal user from opening a detail report for another cluster", () => {
    const session = { email: "member-a@example.test", roles: ["viewer"], iat: 0 };
    const memberships: ClusterMembership[] = [
      { email: session.email, clusterId: clusterA, role: "member", createdAt: "now", updatedAt: "now" },
    ];

    assert.equal(canOpenPassportDetail(session, passportInCluster(clusterB), memberships), false);
  });

  it("lets cluster admins administer and list only their administered clusters", () => {
    const session = { email: "admin-a@example.test", roles: ["viewer"], iat: 0 };
    const memberships: ClusterMembership[] = [
      { email: session.email, clusterId: clusterA, role: "clusterAdmin", createdAt: "now", updatedAt: "now" },
      { email: session.email, clusterId: clusterB, role: "member", createdAt: "now", updatedAt: "now" },
    ];
    const visible = filterPassportsForRegistryAccess(session, [passportInCluster(clusterA), passportInCluster(clusterB)], memberships);

    assert.equal(canAdministerCluster(session, clusterA, memberships), true);
    assert.equal(canAdministerCluster(session, clusterB, memberships), false);
    assert.deepEqual(
      visible.map((passport) => passport.clusterId),
      [clusterA],
    );
  });

  it("scopes local-admin user management to administered clusters", () => {
    const session = { email: "admin-a@example.test", roles: ["viewer"], iat: 0 };
    const memberships: ClusterMembership[] = [
      { email: session.email, clusterId: clusterA, role: "clusterAdmin", createdAt: "now", updatedAt: "now" },
      { email: session.email, clusterId: clusterB, role: "member", createdAt: "now", updatedAt: "now" },
      { email: "member-a@example.test", clusterId: clusterA, role: "member", createdAt: "now", updatedAt: "now" },
      { email: "member-b@example.test", clusterId: clusterB, role: "member", createdAt: "now", updatedAt: "now" },
    ];

    assert.deepEqual(administeredClusterIdsForSession(session, memberships), [clusterA]);
    assert.deepEqual(
      filterMembershipsForClusterAdmin(session, memberships).map((membership) => [membership.email, membership.clusterId]),
      [
        [session.email, clusterA],
        ["member-a@example.test", clusterA],
      ],
    );
  });

  it("lets a general admin bypass cluster limits and access legacy unclustered batteries", () => {
    const session = { email: "admin@example.test", roles: ["admin", "viewer"], iat: 0 };
    const legacy = { ...samplePassport, passportId: "did:web:acme.battery.pass:legacy", clusterId: undefined };

    assert.equal(canOpenPassportDetail(session, passportInCluster(clusterB), []), true);
    assert.equal(canOpenPassportDetail(session, legacy, []), true);
    assert.equal(filterPassportsForRegistryAccess(session, [passportInCluster(clusterA), legacy], []).length, 2);
  });
});
