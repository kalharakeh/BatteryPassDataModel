import { NextResponse } from "next/server";
import { Readable } from "node:stream";
import { requireFileDownloadAccess } from "@/lib/auth/cluster-guards";
import { requireRole } from "@/lib/auth/session";
import { deleteStoredFile, openStoredFileDownload } from "@/lib/db/files";

function headerValue(value: unknown) {
  return typeof value === "string" && value ? value : undefined;
}

export async function GET(_request: Request, { params }: { params: Promise<{ fileId: string }> }) {
  const { fileId } = await params;
  const storedFile = await openStoredFileDownload(fileId);
  if (!storedFile) {
    return NextResponse.json({ error: "File does not exist" }, { status: 404 });
  }
  await requireFileDownloadAccess(storedFile.metadata);

  const contentType =
    headerValue(storedFile.metadata.contentType) ??
    headerValue((storedFile.metadata.metadata as Record<string, unknown> | undefined)?.contentType) ??
    "application/octet-stream";
  const filename = headerValue(storedFile.metadata.filename) ?? "battery-passport-file";

  return new Response(Readable.toWeb(storedFile.stream) as BodyInit, {
    headers: {
      "Content-Type": contentType,
      "Content-Disposition": `inline; filename="${filename.replaceAll('"', "")}"`,
    },
  });
}

export async function DELETE(_request: Request, { params }: { params: Promise<{ fileId: string }> }) {
  await requireRole("admin");
  const { fileId } = await params;
  await deleteStoredFile(fileId);
  return NextResponse.json({ deleted: true });
}
