import path from "node:path";
import { fileURLToPath } from "node:url";
import type { NextConfig } from "next";

const appDir = path.dirname(fileURLToPath(import.meta.url));

const nextConfig: NextConfig = {
  output: "standalone",
  experimental: {
    // Next 16 enables Turbopack's persistent dev cache by default; keep local dev from growing .next on C:.
    turbopackFileSystemCacheForDev: false,
  },
  turbopack: {
    root: appDir,
  },
};

export default nextConfig;
