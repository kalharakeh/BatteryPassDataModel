"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { requireClusterAdminAccess } from "@/lib/auth/cluster-guards";
import { buildClusterAdminPassportUpdate } from "@/lib/admin/cluster-passport-form";
import { uploadBufferToGridFs } from "@/lib/db/files";
import { getPassportForAdmin, upsertPassport } from "@/lib/db/passports";

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

async function uploadBatteryImage(formData: FormData, passportId: string) {
  const value = formData.get("batteryImage");
  if (!(value instanceof File) || value.size === 0) return undefined;
  const contentType = value.type || "application/octet-stream";
  if (!contentType.startsWith("image/")) {
    throw new Error("Only image uploads are supported for battery images");
  }

  return uploadBufferToGridFs({
    buffer: Buffer.from(await value.arrayBuffer()),
    filename: value.name || "battery-image",
    contentType,
    metadata: {
      passportId,
      documentKey: "batteryImage",
      publicAccess: "summaryImage",
      uploadedAt: new Date().toISOString(),
      source: "cluster-admin-passport-form",
    },
  });
}

export async function saveClusterPassportAction(formData: FormData) {
  const passportId = text(formData, "passportId");
  if (!passportId) {
    throw new Error("passportId is required");
  }

  const existingPassport = await getPassportForAdmin(passportId);
  if (!existingPassport) {
    throw new Error("Passport does not exist");
  }
  await requireClusterAdminAccess(existingPassport);

  const uploadedImage = await uploadBatteryImage(formData, passportId);
  const passport = buildClusterAdminPassportUpdate(existingPassport, formData, {
    batteryImageUrl: uploadedImage?.url,
    batteryImageFileId: uploadedImage?.fileId,
  });

  await upsertPassport(passport);
  revalidatePath("/registry");
  revalidatePath("/cluster-admin");
  revalidatePath(`/${encodeURIComponent(passport.passportId)}`);
  revalidatePath(`/${encodeURIComponent(passport.passportId)}/summary`);
  redirect(`/cluster-admin/passports/${encodeURIComponent(passport.passportId)}/edit`);
}
