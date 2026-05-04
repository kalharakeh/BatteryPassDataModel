import assert from "node:assert/strict";
import { afterEach, beforeEach, describe, it } from "node:test";
import { authenticateDemoAdminOnMongoUnavailable } from "../src/lib/auth/demo-admin";

function mongoUnavailableError() {
  const error = new Error("tlsv1 alert internal error");
  error.name = "MongoServerSelectionError";
  return error;
}

describe("authenticateDemoAdminOnMongoUnavailable", () => {
  const originalEnv = {
    DEMO_ADMIN_EMAIL: process.env.DEMO_ADMIN_EMAIL,
    DEMO_ADMIN_PASSWORD: process.env.DEMO_ADMIN_PASSWORD,
    NODE_ENV: process.env.NODE_ENV,
  };

  beforeEach(() => {
    process.env.DEMO_ADMIN_EMAIL = "admin@example.test";
    process.env.DEMO_ADMIN_PASSWORD = "Password123!";
    process.env.NODE_ENV = "development";
  });

  afterEach(() => {
    process.env.DEMO_ADMIN_EMAIL = originalEnv.DEMO_ADMIN_EMAIL;
    process.env.DEMO_ADMIN_PASSWORD = originalEnv.DEMO_ADMIN_PASSWORD;
    process.env.NODE_ENV = originalEnv.NODE_ENV;
  });

  it("authenticates configured demo admin credentials when MongoDB is unavailable", async () => {
    const user = await authenticateDemoAdminOnMongoUnavailable(mongoUnavailableError(), "admin@example.test", "Password123!");

    assert.deepEqual(user, {
      email: "admin@example.test",
      name: "Demo Admin",
      roles: ["admin", "viewer", "issuer", "verifier"],
    });
  });

  it("rejects wrong demo admin credentials during MongoDB outage", async () => {
    const user = await authenticateDemoAdminOnMongoUnavailable(mongoUnavailableError(), "admin@example.test", "wrong");

    assert.equal(user, null);
  });
});
