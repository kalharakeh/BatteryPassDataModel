import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, it } from "node:test";

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    return statSync(path).isDirectory() ? sourceFiles(path) : path.endsWith(".tsx") ? [path] : [];
  });
}

describe("server action forms", () => {
  it("do not manually specify method or encType on function-action forms", () => {
    const offenders = sourceFiles("src")
      .flatMap((path) => {
        const source = readFileSync(path, "utf8");
        return [...source.matchAll(/<form\b(?=[^>]*\baction=\{[A-Za-z_$][\w$]*Action\})(?=[^>]*\b(?:encType|method)=)[^>]*>/gs)].map(
          (match) => `${path}: ${match[0]}`,
        );
      });

    assert.deepEqual(offenders, []);
  });
});
