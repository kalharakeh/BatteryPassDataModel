import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { renderToStaticMarkup } from "react-dom/server";
import { PassportDetailReport } from "../src/components/passport/passport-detail-report";
import { samplePassport } from "../src/lib/seed/sample-passport";
import { toPassportViewModel } from "../src/lib/view-model/passport-view-model";

describe("PassportDetailReport", () => {
  it("renders one selected tab panel instead of all section content", () => {
    const viewModel = toPassportViewModel(samplePassport);
    const html = renderToStaticMarkup(<PassportDetailReport passport={samplePassport} viewModel={viewModel} />);

    assert.match(html, /Generic information about the battery/);
    assert.doesNotMatch(html, /Material composition of the battery/);
    assert.doesNotMatch(html, /Battery performance information/);
    assert.match(html, /aria-selected="true"/);
  });
});
