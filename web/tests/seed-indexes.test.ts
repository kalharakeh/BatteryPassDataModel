import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { ensurePassportIndexes } from "../src/lib/seed/indexes";

describe("ensurePassportIndexes", () => {
  it("drops the legacy text index before creating the expanded registry search index", async () => {
    const calls: string[] = [];
    const collection = {
      async createIndex(keys: Record<string, unknown>, options?: { name?: string }) {
        calls.push(`create:${options?.name ?? Object.keys(keys).join(",")}`);
      },
      async indexes() {
        return [
          {
            name: "passportId_text_registryInfo.registryId_text",
            key: { _fts: "text", _ftsx: 1 },
          },
        ];
      },
      async dropIndex(name: string) {
        calls.push(`drop:${name}`);
      },
    };

    await ensurePassportIndexes(collection);

    assert.ok(calls.includes("drop:passportId_text_registryInfo.registryId_text"));
    assert.ok(calls.includes("create:passport_registry_search_text"));
  });
});
