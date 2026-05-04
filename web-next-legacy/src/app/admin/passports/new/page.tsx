import { randomUUID } from "node:crypto";
import { SectionEditor } from "@/components/admin/section-editor";
import { requireRole } from "@/lib/auth/session";
import { samplePassport } from "@/lib/seed/sample-passport";
import type { BatteryPassport } from "@/types/passport";

export const dynamic = "force-dynamic";

function createDraftPassport() {
  const passport = JSON.parse(JSON.stringify(samplePassport)) as BatteryPassport;
  const now = new Date().toISOString();
  passport.passportId = `did:web:acme.battery.pass:${randomUUID()}`;
  passport.registryInfo.registryId = randomUUID();
  passport.registryInfo.status = "draft";
  passport.registryInfo.createdAt = now;
  passport.registryInfo.updatedAt = now;
  passport.validation.isValid = false;
  passport.validation.signedAt = null;
  return passport;
}

export default async function NewPassportPage() {
  await requireRole("admin");
  const passport = createDraftPassport();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Create passport</h1>
      <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Start from the sample structure and enter a new passport ID and section data.</p>
      <div className="mt-6">
        <SectionEditor passport={passport} mode="new" />
      </div>
    </main>
  );
}
