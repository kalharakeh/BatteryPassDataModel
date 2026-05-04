import Link from "next/link";
import { Settings2 } from "lucide-react";
import { Card } from "@/components/ui/card";
import { listClusterAdminPassports } from "@/lib/auth/cluster-guards";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export const dynamic = "force-dynamic";

export default async function ClusterAdminPassportsPage() {
  const passports = await listClusterAdminPassports();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">Managed passports</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
            Update Facility ID, battery image, state of charge, remaining capacity, remaining energy, and full cycles.
          </p>
        </div>
        <Link
          href="/cluster-admin"
          className="inline-flex min-h-10 items-center justify-center rounded-md border border-slate-300 px-4 text-sm font-medium hover:border-emerald-600 dark:border-slate-700"
        >
          Cluster admin
        </Link>
      </div>
      <div className="mt-6 grid gap-4">
        {passports.length === 0 ? (
          <Card>
            <p className="text-sm text-slate-600 dark:text-slate-300">No cluster-admin battery assignments found for this account.</p>
          </Card>
        ) : (
          passports.map((passport) => {
            const viewModel = toPassportViewModel(passport);
            return (
              <Card key={passport.passportId}>
                <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
                  <div>
                    <p className="text-xs uppercase text-slate-500 dark:text-slate-400">{passport.clusterId}</p>
                    <h2 className="mt-1 text-lg font-semibold">{viewModel.modelNumber}</h2>
                    <p className="mt-1 break-all text-sm text-slate-600 dark:text-slate-300">{passport.passportId}</p>
                  </div>
                  <Link
                    href={`/cluster-admin/passports/${encodeURIComponent(passport.passportId)}/edit`}
                    className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800"
                  >
                    <Settings2 className="h-4 w-4" />
                    Edit local fields
                  </Link>
                </div>
              </Card>
            );
          })
        )}
      </div>
    </main>
  );
}
