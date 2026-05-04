"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { requireRole } from "@/lib/auth/session";
import { getPassportForAdmin, archivePassport, upsertPassport } from "@/lib/db/passports";
import { uploadBufferToGridFs } from "@/lib/db/files";
import { buildPassportFromFormData } from "@/lib/admin/passport-form";
import { sampleDocumentLabels } from "@/lib/seed/sample-passport";
import type { PassportDocumentKey } from "@/types/passport";

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

async function uploadFileFromForm(formData: FormData, key: string, metadata: Record<string, string>) {
  const value = formData.get(key);
  if (!(value instanceof File) || value.size === 0) return undefined;

  const contentType = value.type || "application/octet-stream";
  if (contentType !== "application/pdf" && !contentType.startsWith("image/")) {
    throw new Error("Only PDF and image uploads are supported");
  }

  return uploadBufferToGridFs({
    buffer: Buffer.from(await value.arrayBuffer()),
    filename: value.name || key,
    contentType,
    metadata: {
      ...metadata,
      uploadedAt: new Date().toISOString(),
      source: "admin-passport-form",
    },
  });
}

export async function savePassportAction(formData: FormData) {
  await requireRole("admin");
  const passportId = text(formData, "passportId");
  if (!passportId) {
    throw new Error("passportId is required");
  }

  const existingPassport = await getPassportForAdmin(passportId);
  const documentEntries = await Promise.all(
    (Object.keys(sampleDocumentLabels) as PassportDocumentKey[]).map(async (key) => {
      const uploaded = await uploadFileFromForm(formData, `document_${key}`, {
        passportId,
        documentKey: key,
      });
      return [key, uploaded ? { url: uploaded.url, fileId: uploaded.fileId } : undefined] as const;
    }),
  );
  const documentUrls = Object.fromEntries(documentEntries.filter(([, value]) => value)) as Parameters<
    typeof buildPassportFromFormData
  >[1]["documentUrls"];

  const uploadedImage = await uploadFileFromForm(formData, "batteryImage", {
    passportId,
    documentKey: "batteryImage",
    publicAccess: "summaryImage",
  });

  const passport = buildPassportFromFormData(formData, {
    existingPassport,
    documentUrls,
    batteryImageUrl: uploadedImage?.url,
    batteryImageFileId: uploadedImage?.fileId,
  });

  await upsertPassport(passport);
  revalidatePath("/");
  revalidatePath("/registry");
  revalidatePath(`/${encodeURIComponent(passport.passportId)}`);
  redirect(`/admin/passports/${encodeURIComponent(passport.passportId)}/edit`);
}

export async function archivePassportAction(formData: FormData) {
  await requireRole("admin");
  const passportId = text(formData, "passportId");
  if (!passportId) {
    throw new Error("passportId is required");
  }
  await archivePassport(passportId);
  revalidatePath("/registry");
  revalidatePath("/admin/passports");
  redirect("/admin/passports");
}
