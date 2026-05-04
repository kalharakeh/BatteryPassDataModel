import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { canonicalizeJson } from "../src/lib/crypto/canonicalize";

describe("canonicalizeJson", () => {
  it("sorts object keys recursively", () => {
    const left = { b: 1, a: { d: true, c: ["x", { z: 2, y: 1 }] } };
    const right = { a: { c: ["x", { y: 1, z: 2 }], d: true }, b: 1 };

    assert.equal(canonicalizeJson(left), canonicalizeJson(right));
    assert.equal(canonicalizeJson(left), '{"a":{"c":["x",{"y":1,"z":2}],"d":true},"b":1}');
  });

  it("rejects non-finite numbers", () => {
    assert.throws(() => canonicalizeJson({ value: Number.NaN }), /non-finite/);
  });
});
