import { createHash, generateKeyPairSync, sign, verify } from "node:crypto";
import { canonicalizeJson } from "./canonicalize";

export type SigningKeyPair = {
  publicKeyPem: string;
  privateKeyPem: string;
};

export function generateEd25519KeyPair(): SigningKeyPair {
  const { publicKey, privateKey } = generateKeyPairSync("ed25519");
  return {
    publicKeyPem: publicKey.export({ type: "spki", format: "pem" }).toString(),
    privateKeyPem: privateKey.export({ type: "pkcs8", format: "pem" }).toString(),
  };
}

export function hashPayload(payload: unknown): string {
  return createHash("sha256").update(canonicalizeJson(payload)).digest("base64url");
}

export function signPayload(payload: unknown, privateKeyPem: string): string {
  return sign(null, Buffer.from(canonicalizeJson(payload)), privateKeyPem).toString("base64url");
}

export function verifyPayloadSignature(payload: unknown, signature: string, publicKeyPem: string): boolean {
  return verify(null, Buffer.from(canonicalizeJson(payload)), publicKeyPem, Buffer.from(signature, "base64url"));
}
