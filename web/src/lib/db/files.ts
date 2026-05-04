import { GridFSBucket, ObjectId, type Document } from "mongodb";
import { Readable } from "node:stream";
import { getDatabase } from "./collections";

export type UploadBufferInput = {
  buffer: Buffer;
  filename: string;
  contentType: string;
  metadata?: Document;
};

export async function fileBucket() {
  return new GridFSBucket(await getDatabase(), { bucketName: "passportFiles" });
}

export async function uploadBufferToGridFs(input: UploadBufferInput) {
  const bucket = await fileBucket();
  const metadata = { ...input.metadata, contentType: input.contentType };
  const upload = bucket.openUploadStream(input.filename, {
    metadata,
  });

  await new Promise<void>((resolve, reject) => {
    Readable.from(input.buffer).pipe(upload).on("error", reject).on("finish", () => resolve());
  });

  const db = await getDatabase();
  await db.collection("passportFiles.files").updateOne(
    { _id: upload.id },
    {
      $set: {
        contentType: input.contentType,
        metadata,
      },
    },
  );

  const fileId = upload.id.toString();
  return { fileId, url: `/api/files/${fileId}` };
}

export async function getStoredFileMetadata(fileId: string) {
  if (!ObjectId.isValid(fileId)) return null;
  const db = await getDatabase();
  return db.collection("passportFiles.files").findOne({ _id: new ObjectId(fileId) });
}

export async function getStoredFileBySeedKey(seedKey: string) {
  const db = await getDatabase();
  return db.collection("passportFiles.files").findOne({ "metadata.seedKey": seedKey });
}

export async function deleteStoredFile(fileId: string) {
  if (!ObjectId.isValid(fileId)) return;
  const bucket = await fileBucket();
  await bucket.delete(new ObjectId(fileId)).catch((error: Error & { code?: string }) => {
    if (error.message.includes("FileNotFound")) return;
    throw error;
  });
}

export async function openStoredFileDownload(fileId: string) {
  if (!ObjectId.isValid(fileId)) return null;
  const metadata = await getStoredFileMetadata(fileId);
  if (!metadata) return null;
  const bucket = await fileBucket();
  return {
    metadata,
    stream: bucket.openDownloadStream(new ObjectId(fileId)),
  };
}
