import { notFound } from "next/navigation";
import { SectionEditor } from "@/components/admin/section-editor";
import { requireRole } from "@/lib/auth/session";
import { getPassportForAdmin } from "@/lib/db/passports";

export const dynamic = "force-dynamic";

export default async function EditPassportPage({ params }: { params: Promise<{ passportId: string }> }) {
  await requireRole("admin");
  const { passportId } = await params;
  const passport = await getPassportForAdmin(decodeURIComponent(passportId));
  if (!passport) notFound();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Edit passport</h1>
      <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{passport.passportId}</p>
      <div className="mt-6">
        <SectionEditor passport={passport} mode="edit" />
      </div>
    </main>
  );
}
