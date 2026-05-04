import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { generateEd25519KeyPair, hashPayload, signPayload, verifyPayloadSignature } from "../src/lib/crypto/signing";

describe("signing", () => {
  it("signs and verifies canonicalized payloads", () => {
    const keys = generateEd25519KeyPair();
    const payload = { passportId: "did:web:local.battery.pass:sample", aspect: { b: 2, a: 1 } };

    const signature = signPayload(payload, keys.privateKeyPem);

    assert.equal(hashPayload(payload).length > 20, true);
    assert.equal(
      verifyPayloadSignature(
        { passportId: "did:web:local.battery.pass:sample", aspect: { a: 1, b: 2 } },
        signature,
        keys.publicKeyPem,
      ),
      true,
    );
    assert.equal(
      verifyPayloadSignature(
        { passportId: "did:web:local.battery.pass:sample", aspect: { a: 1, b: 3 } },
        signature,
        keys.publicKeyPem,
      ),
      false,
    );
  });
});
