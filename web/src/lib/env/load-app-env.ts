import { loadEnvConfig } from "@next/env";

export function loadAppEnv(projectDir = process.cwd()) {
  loadEnvConfig(projectDir);
}
