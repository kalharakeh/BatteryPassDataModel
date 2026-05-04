import { NextResponse } from "next/server";
import { requireRole } from "@/lib/auth/session";
import { uploadBufferToGridFs } from "@/lib/db/files";

export async function POST(request: Request) {
  await requireRole("admin");
  const formData = await request.formData();
  const file = formData.get("file");

  if (!(file instanceof File) || file.size === 0) {
    return NextResponse.json({ error: "file is required" }, { status: 400 });
  }

  const contentType = file.type || "application/octet-stream";
  if (contentType !== "application/pdf" && !contentType.startsWith("image/")) {
    return NextResponse.json({ error: "Only PDF and image uploads are supported" }, { status: 400 });
  }

  const saved = await uploadBufferToGridFs({
    buffer: Buffer.from(await file.arrayBuffer()),
    filename: file.name || "passport-upload",
    contentType,
    metadata: {
      uploadedAt: new Date().toISOString(),
      source: "admin-upload",
    },
  });

  return NextResponse.json(saved, { status: 201 });
}
