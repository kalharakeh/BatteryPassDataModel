import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { renderToStaticMarkup } from "react-dom/server";
import { PassportSummaryReport } from "../src/components/passport/passport-summary-report";
import { samplePassport } from "../src/lib/seed/sample-passport";
import { toPassportViewModel } from "../src/lib/view-model/passport-view-model";

describe("PassportSummaryReport", () => {
  it("renders compact overview fields and summary charts without material composition", () => {
    const viewModel = toPassportViewModel(samplePassport, {
      clusters: [{ clusterId: "cluster-default-demonstrator", name: "Default Demonstrator Cluster", createdAt: "now", updatedAt: "now" }],
    });
    const html = renderToStaticMarkup(
      <PassportSummaryReport
        viewModel={viewModel}
        detailAccessNotice="Sign in with a user connected to Default Demonstrator Cluster to open the detailed report."
      />,
    );

    assert.match(html, /Verified/);
    assert.match(html, /Original/);
    assert.match(html, /Default Demonstrator Cluster/);
    assert.match(html, /Sign in with a user connected to Default Demonstrator Cluster/);
    assert.match(html, /Detailed report/);
    assert.match(html, /Original power/);
    assert.match(html, /Carbon footprint/);
    assert.match(html, /Recycled content share/);
    assert.doesNotMatch(html, /Material composition/);
  });
});
