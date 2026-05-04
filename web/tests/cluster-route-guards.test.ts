import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

describe("cluster route guards", () => {
  it("keeps summary reports public", () => {
    const source = readFileSync("src/app/[passportId]/summary/page.tsx", "utf8");

    assert.doesNotMatch(source, /requirePassportDetailAccess|requireRole\(/);
  });

  it("requires cluster access before rendering detail reports", () => {
    const source = readFileSync("src/app/[passportId]/page.tsx", "utf8");

    assert.match(source, /requirePassportDetailAccess/);
    assert.ok(source.indexOf("await requirePassportDetailAccess(passport") > source.indexOf("const passport = await getPassport("));
    assert.match(source, /nextPath/);
  });

  it("keeps no-match public searches on the search page", () => {
    const source = readFileSync("src/app/search/page.tsx", "utf8");

    assert.doesNotMatch(source, /notFound\(/);
    assert.match(source, /No battery passport ID was found/);
  });

  it("uses cluster-aware registry loading", () => {
    const source = readFileSync("src/app/registry/page.tsx", "utf8");

    assert.match(source, /listPassportsForRegistryAccess/);
    assert.doesNotMatch(source, /requireRole\("admin"\)/);
  });

  it("protects file downloads through cluster-aware file access", () => {
    const source = readFileSync("src/app/api/files/[fileId]/route.ts", "utf8");

    assert.match(source, /requireFileDownloadAccess/);
  });
});
