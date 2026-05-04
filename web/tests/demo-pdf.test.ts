import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { createDemoPdfBuffer } from "../src/lib/files/demo-pdf";

describe("createDemoPdfBuffer", () => {
  it("creates a PDF document buffer with supplied title text", () => {
    const buffer = createDemoPdfBuffer("EU Declaration", ["Battery passport demo document"]);
    const text = buffer.toString("latin1");

    assert.equal(text.startsWith("%PDF-1.4"), true);
    assert.match(text, /EU Declaration/);
    assert.match(text, /Battery passport demo document/);
    assert.match(text, /%%EOF/);
  });
});
