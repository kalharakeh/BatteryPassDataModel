import assert from "node:assert/strict";
import { after, describe, it } from "node:test";
import { deleteStoredFile, getStoredFileMetadata, uploadBufferToGridFs } from "../src/lib/db/files";
import { closeMongoClient } from "../src/lib/db/client";
import { loadAppEnv } from "../src/lib/env/load-app-env";

loadAppEnv();
process.env.MONGODB_SERVER_SELECTION_TIMEOUT_MS ??= "5000";

function isMongoUnavailable(error: unknown) {
  return error instanceof Error && (error.name === "MongoServerSelectionError" || error.message.includes("tlsv1 alert internal error"));
}

describe("GridFS file storage", () => {
  let fileId = "";

  after(async () => {
    if (fileId) {
      await deleteStoredFile(fileId);
    }
    await closeMongoClient();
  });

  it("stores PDFs with content type metadata", { skip: !process.env.MONGODB_URI }, async (context) => {
    let saved: Awaited<ReturnType<typeof uploadBufferToGridFs>>;
    try {
      saved = await uploadBufferToGridFs({
        buffer: Buffer.from("%PDF-1.4\n%%EOF\n", "utf8"),
        filename: "test-demo.pdf",
        contentType: "application/pdf",
        metadata: { testRun: "file-storage.test" },
      });
      fileId = saved.fileId;
    } catch (error) {
      if (isMongoUnavailable(error)) {
        context.skip(`MongoDB unavailable for GridFS integration test: ${(error as Error).name}`);
        return;
      }
      throw error;
    }

    const metadata = await getStoredFileMetadata(fileId);

    assert.equal(metadata?.filename, "test-demo.pdf");
    assert.equal(metadata?.contentType, "application/pdf");
    assert.equal(saved.url, `/api/files/${fileId}`);
  });
});
