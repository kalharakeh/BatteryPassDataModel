import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("registry admin access", () => {
  it("uses cluster-aware access before listing registry records", () => {
    const source = readFileSync("src/app/registry/page.tsx", "utf8");

    assert.match(source, /listPassportsForRegistryAccess/);
    assert.doesNotMatch(source, /requireRole\("admin"\)/);
  });
});
