import Link from "next/link";
import { BatteryCharging, UsersRound } from "lucide-react";
import { Card } from "@/components/ui/card";
import { requireClusterAdminSession } from "@/lib/auth/cluster-guards";
import { listPassports } from "@/lib/db/passports";
import { listClusterMemberships } from "@/lib/db/clusters";

export const dynamic = "force-dynamic";

export default async function ClusterAdminPage() {
  const { administeredClusterIds } = await requireClusterAdminSession();
  const administeredClusters = new Set(administeredClusterIds);
  const [passports, memberships] = await Promise.all([listPassports(), listClusterMemberships()]);
  const passportCount = passports.filter((passport) => passport.clusterId && administeredClusters.has(passport.clusterId)).length;
  const userCount = new Set(
    memberships.filter((membership) => administeredClusters.has(membership.clusterId)).map((membership) => membership.email),
  ).size;

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Cluster admin</h1>
      <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
        Manage local battery data and users for the clusters you administer.
      </p>

      <div className="mt-6 grid gap-4 md:grid-cols-3">
        <Card>
          <p className="text-sm text-slate-500">Administered clusters</p>
          <p className="mt-2 text-3xl font-semibold">{administeredClusterIds.length}</p>
        </Card>
        <Card>
          <p className="text-sm text-slate-500">Managed passports</p>
          <p className="mt-2 text-3xl font-semibold">{passportCount}</p>
        </Card>
        <Card>
          <p className="text-sm text-slate-500">Connected users</p>
          <p className="mt-2 text-3xl font-semibold">{userCount}</p>
        </Card>
      </div>

      <div className="mt-6 flex flex-wrap gap-3">
        <Link
          className="inline-flex min-h-10 items-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800"
          href="/cluster-admin/passports"
        >
          <BatteryCharging className="h-4 w-4" />
          Manage passports
        </Link>
        <Link
          className="inline-flex min-h-10 items-center gap-2 rounded-md border border-slate-300 px-4 text-sm font-medium hover:border-emerald-600 dark:border-slate-700"
          href="/cluster-admin/users"
        >
          <UsersRound className="h-4 w-4" />
          Manage users
        </Link>
      </div>
    </main>
  );
}
