import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { afterEach, describe, it } from "node:test";
import { loadAppEnv } from "../src/lib/env/load-app-env";

const envKey = "BATTERY_PASS_TEST_ENV";
const previousValue = process.env[envKey];

afterEach(() => {
  if (previousValue === undefined) {
    delete process.env[envKey];
  } else {
    process.env[envKey] = previousValue;
  }
});

describe("loadAppEnv", () => {
  it("loads values from a Next.js .env.local file for standalone scripts", () => {
    const projectDir = fs.mkdtempSync(path.join(os.tmpdir(), "battery-pass-env-"));
    fs.writeFileSync(path.join(projectDir, ".env.local"), `${envKey}=loaded-from-local\n`);

    delete process.env[envKey];
    loadAppEnv(projectDir);

    assert.equal(process.env[envKey], "loaded-from-local");
  });
});
