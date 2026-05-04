import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { loginRedirectDestination } from "../src/lib/auth/login-redirect";

describe("loginRedirectDestination", () => {
  it("returns the requested detail report path after successful login", () => {
    const destination = loginRedirectDestination({ roles: ["viewer"] }, "/did%3Aweb%3Aacme.battery.pass%3Asample");

    assert.equal(destination, "/did%3Aweb%3Aacme.battery.pass%3Asample");
  });

  it("rejects external next URLs and keeps the role-based fallback", () => {
    assert.equal(loginRedirectDestination({ roles: ["viewer"] }, "https://example.test/phish"), "/");
    assert.equal(loginRedirectDestination({ roles: ["admin"] }, ""), "/admin");
  });
});
