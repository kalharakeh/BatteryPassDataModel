import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { describe, it } from "node:test";
import Ajv from "ajv";
import draft04 from "ajv/lib/refs/json-schema-draft-04.json";
import { aspectSchemas } from "../src/lib/schemas/registry";
import { samplePassport } from "../src/lib/seed/sample-passport";

function readSchema(filePath: string) {
  const buffer = fs.readFileSync(filePath);
  const text = buffer[0] === 0xff && buffer[1] === 0xfe ? buffer.toString("utf16le") : buffer.toString("utf8");
  return JSON.parse(text.replace(/^\uFEFF/, ""));
}

describe("rich sample passport schema alignment", () => {
  it("validates every seeded canonical aspect payload against the repo Battery Passport schemas", () => {
    const repoRoot = path.resolve(process.cwd(), "..");
    const ajv = new Ajv({ schemaId: "auto", allErrors: true, unknownFormats: "ignore" });
    ajv.addMetaSchema(draft04);

    for (const [aspectKey, schemaInfo] of Object.entries(aspectSchemas)) {
      const schema = readSchema(path.join(repoRoot, schemaInfo.schemaPath));
      const validate = ajv.compile(schema);
      const payload = samplePassport.aspects[aspectKey as keyof typeof samplePassport.aspects]?.payload;
      const valid = validate(payload);
      assert.equal(valid, true, `${aspectKey} failed schema validation: ${JSON.stringify(validate.errors, null, 2)}`);
    }
  });
});
