import assert from "node:assert/strict";
import fs from "node:fs";
import { describe, it } from "node:test";
import { aspectSchemas } from "../src/lib/schemas/registry";

describe("aspectSchemas", () => {
  it("points every canonical aspect at an existing repo schema", () => {
    for (const schema of Object.values(aspectSchemas)) {
      assert.equal(fs.existsSync(schema.schemaPath), true, `${schema.key} schema missing at ${schema.schemaPath}`);
      assert.match(schema.version, /^1\.2\.[01]$/);
    }
  });
});
