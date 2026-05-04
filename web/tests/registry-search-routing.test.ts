import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { samplePassport, samplePassportId } from "../src/lib/seed/sample-passport";
import { publicSearchResult, summaryRedirectForSearch } from "../src/lib/registry/search-routing";

describe("summaryRedirectForSearch", () => {
  it("redirects a unique battery ID search directly to the summary report", () => {
    const redirectUrl = summaryRedirectForSearch(samplePassportId, [samplePassport]);

    assert.equal(redirectUrl, `/${encodeURIComponent(samplePassportId)}/summary`);
  });

  it("keeps broad searches on the registry result list", () => {
    const redirectUrl = summaryRedirectForSearch("Scania", [samplePassport]);

    assert.equal(redirectUrl, null);
  });

  it("returns a no-match state instead of requiring a 404 page", () => {
    const result = publicSearchResult("did:web:local.battery.pass:missing", []);

    assert.deepEqual(result, { state: "notFound" });
  });

  it("sends exact available IDs to the public summary regardless of cluster access", () => {
    const differentClusterPassport = { ...samplePassport, clusterId: "cluster-south-operations" };
    const result = publicSearchResult(differentClusterPassport.passportId, [differentClusterPassport]);

    assert.deepEqual(result, {
      state: "summary",
      href: `/${encodeURIComponent(differentClusterPassport.passportId)}/summary`,
    });
  });
});
