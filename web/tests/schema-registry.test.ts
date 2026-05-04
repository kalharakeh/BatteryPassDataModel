import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { describe, it } from "node:test";
import { aspectSchemas } from "../src/lib/schemas/registry";

describe("aspectSchemas", () => {
  it("points every canonical aspect at an existing repo schema", () => {
    const repoRoot = path.resolve(process.cwd(), "..");

    for (const schema of Object.values(aspectSchemas)) {
      const absoluteSchemaPath = path.join(repoRoot, schema.schemaPath);
      assert.equal(fs.existsSync(absoluteSchemaPath), true, `${schema.key} schema missing at ${absoluteSchemaPath}`);
      assert.match(schema.version, /^1\.2\.[01]$/);
    }
  });
});
