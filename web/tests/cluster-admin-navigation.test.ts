import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("cluster admin navigation", () => {
  it("uses a dashboard with links for passport and user management", () => {
    const source = readFileSync("src/app/cluster-admin/page.tsx", "utf8");

    assert.match(source, /Manage passports/);
    assert.match(source, /Manage users/);
    assert.match(source, /href="\/cluster-admin\/passports"/);
    assert.match(source, /href="\/cluster-admin\/users"/);
  });

  it("keeps passport management on its own cluster-admin page", () => {
    const source = readFileSync("src/app/cluster-admin/passports/page.tsx", "utf8");

    assert.match(source, /Edit local fields/);
    assert.match(source, /listClusterAdminPassports/);
  });

  it("has a cluster-admin users page and restricted membership actions", () => {
    const page = readFileSync("src/app/cluster-admin/users/page.tsx", "utf8");
    const actions = readFileSync("src/app/cluster-admin/users/actions.ts", "utf8");

    assert.match(page, /Add or update user/);
    assert.match(page, /deleteClusterAdminUserMembershipAction/);
    assert.match(actions, /requireClusterAdminForCluster/);
    assert.doesNotMatch(actions, /requireRole\("admin"\)/);
  });
});
