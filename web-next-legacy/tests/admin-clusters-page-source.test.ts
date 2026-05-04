import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("admin clusters page source", () => {
  it("has three cluster administration tabs", () => {
    const source = readFileSync("src/app/admin/clusters/page.tsx", "utf8");

    assert.match(source, /Battery cluster assignments/);
    assert.match(source, /Registered clusters/);
    assert.match(source, /Users/);
  });

  it("does not expose cluster categories", () => {
    const source = readFileSync("src/app/admin/clusters/page.tsx", "utf8");
    const actions = readFileSync("src/app/admin/clusters/actions.ts", "utf8");

    assert.doesNotMatch(source, /name="kind"/);
    assert.doesNotMatch(source, /Cluster kind/);
    assert.doesNotMatch(source, />Customer</);
    assert.doesNotMatch(source, />End user</);
    assert.doesNotMatch(source, />Sample</);
    assert.doesNotMatch(actions, /ClusterKind/);
    assert.doesNotMatch(actions, /clusterKind/);
  });

  it("supports registered cluster and user membership row management", () => {
    const source = readFileSync("src/app/admin/clusters/page.tsx", "utf8");
    const actions = readFileSync("src/app/admin/clusters/actions.ts", "utf8");

    assert.match(source, /membershipsByUser/);
    assert.match(source, /updateClusterAction/);
    assert.match(source, /deleteClusterAction/);
    assert.match(source, /deleteUserMembershipAction/);
    assert.match(actions, /export async function updateClusterAction/);
    assert.match(actions, /export async function deleteClusterAction/);
    assert.match(actions, /export async function deleteUserMembershipAction/);
  });

  it("uses high-contrast selected tab colors", () => {
    const source = readFileSync("src/app/admin/clusters/page.tsx", "utf8");

    assert.match(source, /bg-emerald-100/);
    assert.match(source, /text-emerald-950/);
    assert.doesNotMatch(source, /bg-slate-950 text-white dark:bg-slate-100 dark:text-slate-950/);
  });
});
