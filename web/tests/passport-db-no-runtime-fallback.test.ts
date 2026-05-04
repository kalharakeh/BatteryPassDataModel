import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("passport database reads", () => {
  it("do not serve bundled sample passport data when MongoDB is unavailable", () => {
    const source = readFileSync("src/lib/db/passports.ts", "utf8");

    assert.doesNotMatch(source, /samplePassport/);
    assert.doesNotMatch(source, /developmentReadFallback/);
  });
});
