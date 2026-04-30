import Link from "next/link";
import { RegistryTable } from "@/components/registry/registry-table";
import { requireRole } from "@/lib/auth/session";
import { listPassports } from "@/lib/db/passports";

export default async function AdminPassportsPage() {
  await requireRole("admin");
  const passports = await listPassports();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Manage passports</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Open a passport to edit schema-aligned sections.</p>
        </div>
      </div>
      <div className="mt-6">
        <RegistryTable passports={passports} />
      </div>
      <div className="mt-4 space-y-2">
        {passports.map((passport) => (
          <Link
            key={passport.passportId}
            className="block text-sm text-emerald-700 dark:text-emerald-300"
            href={`/admin/passports/${encodeURIComponent(passport.passportId)}/edit`}
          >
            Edit {passport.passportId}
          </Link>
        ))}
      </div>
    </main>
  );
}
