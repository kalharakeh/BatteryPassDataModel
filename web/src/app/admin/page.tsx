import Link from "next/link";
import { Card } from "@/components/ui/card";
import { requireRole } from "@/lib/auth/session";
import { listPassports } from "@/lib/db/passports";

export const dynamic = "force-dynamic";

export default async function AdminPage() {
  await requireRole("admin");
  const passports = await listPassports();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Admin dashboard</h1>
      <div className="mt-6 grid gap-4 md:grid-cols-3">
        <Card>
          <p className="text-sm text-slate-500">Passports</p>
          <p className="mt-2 text-3xl font-semibold">{passports.length}</p>
        </Card>
        <Card>
          <p className="text-sm text-slate-500">Published</p>
          <p className="mt-2 text-3xl font-semibold">
            {passports.filter((passport) => passport.registryInfo.status === "published").length}
          </p>
        </Card>
        <Card>
          <p className="text-sm text-slate-500">Draft</p>
          <p className="mt-2 text-3xl font-semibold">
            {passports.filter((passport) => passport.registryInfo.status === "draft").length}
          </p>
        </Card>
      </div>
      <Link className="mt-6 inline-flex rounded-md bg-emerald-700 px-4 py-2 text-sm font-medium text-white" href="/admin/passports">
        Manage passports
      </Link>
    </main>
  );
}
