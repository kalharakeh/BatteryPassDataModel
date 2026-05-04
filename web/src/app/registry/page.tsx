import Link from "next/link";
import { Search } from "lucide-react";
import { redirect } from "next/navigation";
import { RegistryTable } from "@/components/registry/registry-table";
import { listPassportsForRegistryAccess } from "@/lib/auth/cluster-guards";
import { summaryRedirectForSearch } from "@/lib/registry/search-routing";
import { samplePassportId } from "@/lib/seed/sample-passport";

export const dynamic = "force-dynamic";

export default async function RegistryPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const params = await searchParams;
  const passports = await listPassportsForRegistryAccess(params.q ?? "");
  const summaryRedirect = summaryRedirectForSearch(params.q ?? "", passports);
  if (summaryRedirect) {
    redirect(summaryRedirect);
  }

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Passport Registry</h1>
      <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Search and open registered battery passports.</p>
      <form action="/registry" className="mt-6 flex flex-col gap-3 sm:flex-row">
        <input
          name="q"
          defaultValue={params.q ?? ""}
          className="min-h-11 flex-1 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-900"
          placeholder="Search by passport ID, model, serial, or manufacturer"
        />
        <button
          type="submit"
          className="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800"
        >
          <Search className="h-4 w-4" />
          Search
        </button>
        <Link
          href={`/registry?q=${encodeURIComponent(samplePassportId)}`}
          className="inline-flex min-h-11 items-center justify-center rounded-md border border-slate-300 px-4 text-sm font-medium hover:border-emerald-600 dark:border-slate-700"
        >
          Sample ID
        </Link>
      </form>
      <div className="mt-6">
        <RegistryTable passports={passports} />
      </div>
    </main>
  );
}
