import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { describe, it } from "node:test";

const packageJson = JSON.parse(
  fs.readFileSync(path.join(process.cwd(), "package.json"), "utf8"),
) as { scripts?: Record<string, string> };

type NextConfigLike = {
  experimental?: {
    turbopackFileSystemCacheForDev?: boolean;
  };
  turbopack?: {
    root?: string;
  };
};

function unwrapConfig(config: NextConfigLike | { default: NextConfigLike }) {
  return "default" in config ? config.default : config;
}

async function loadNextConfig() {
  const configModule = await import("../next.config");
  return unwrapConfig(configModule.default as NextConfigLike | { default: NextConfigLike });
}

describe("dev server safety", () => {
  it("uses webpack for npm run dev to avoid Turbopack cache growth on C:", () => {
    assert.match(packageJson.scripts?.dev ?? "", /\bnext\s+dev\b.*--webpack(?:\s|$)/);
  });

  it("disables the Turbopack filesystem cache for direct next dev runs", async () => {
    const nextConfig = await loadNextConfig();

    assert.equal(nextConfig.experimental?.turbopackFileSystemCacheForDev, false);
  });

  it("pins Turbopack's project root to the web app directory", async () => {
    const nextConfig = await loadNextConfig();

    assert.equal(path.resolve(nextConfig.turbopack?.root ?? ""), process.cwd());
  });
});
