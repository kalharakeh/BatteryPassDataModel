import Link from "next/link";
import { Plus } from "lucide-react";
import { archivePassportAction } from "./actions";
import { requireRole } from "@/lib/auth/session";
import { listPassports } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export const dynamic = "force-dynamic";

export default async function AdminPassportsPage() {
  await requireRole("admin");
  const passports = await listPassports("", { includeArchived: true });

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Manage passports</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Open a passport to edit schema-aligned sections.</p>
        </div>
        <Link
          href="/admin/passports/new"
          className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800"
        >
          <Plus className="h-4 w-4" />
          New passport
        </Link>
      </div>
      <div className="mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <table className="w-full min-w-[900px] text-left text-sm">
          <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
            <tr>
              <th className="px-4 py-3">Passport ID</th>
              <th className="px-4 py-3">Model</th>
              <th className="px-4 py-3">Serial</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3">Updated</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {passports.map((passport) => {
              const viewModel = toPassportViewModel(passport);
              return (
                <tr key={passport.passportId} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                  <td className="max-w-sm truncate px-4 py-3">{passport.passportId}</td>
                  <td className="px-4 py-3">{viewModel.modelNumber}</td>
                  <td className="px-4 py-3">{viewModel.serialNumber}</td>
                  <td className="px-4 py-3">{viewModel.status}</td>
                  <td className="px-4 py-3">{passport.registryInfo.updatedAt.slice(0, 10)}</td>
                  <td className="flex items-center gap-3 px-4 py-3">
                    <Link className="text-emerald-700 dark:text-emerald-300" href={`/admin/passports/${encodeURIComponent(passport.passportId)}/edit`}>
                      Edit
                    </Link>
                    <form action={archivePassportAction}>
                      <input type="hidden" name="passportId" value={passport.passportId} />
                      <button type="submit" className="text-orange-700 dark:text-orange-300">
                        Archive
                      </button>
                    </form>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </main>
  );
}
