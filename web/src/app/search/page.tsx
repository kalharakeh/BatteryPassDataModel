import Link from "next/link";
import { redirect } from "next/navigation";
import { listPassports } from "@/lib/db/passports";
import { publicSearchResult } from "@/lib/registry/search-routing";

export const dynamic = "force-dynamic";

export default async function PublicSearchPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const params = await searchParams;
  const query = params.q ?? "";
  const passports = await listPassports(query);
  const searchResult = publicSearchResult(query, passports);

  if (searchResult.state === "summary") {
    redirect(searchResult.href);
  }

  return (
    <main className="mx-auto flex min-h-[70vh] max-w-2xl flex-col justify-center px-4 py-12">
      <h1 className="text-2xl font-semibold">Search battery passport</h1>
      <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Enter a full battery passport ID to open its summary report.</p>
      {searchResult.state === "notFound" ? (
        <p className="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100">
          No battery passport ID was found for <span className="font-medium">{query}</span>. Check the ID and try again.
        </p>
      ) : null}
      <form action="/search" className="mt-6 flex flex-col gap-3 sm:flex-row">
        <input
          name="q"
          defaultValue={query}
          className="min-h-11 flex-1 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-900"
          placeholder="did:web:acme.battery.pass:..."
        />
        <button type="submit" className="inline-flex min-h-11 items-center justify-center rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800">
          Search
        </button>
      </form>
      <Link href="/" className="mt-4 text-sm text-emerald-700 dark:text-emerald-300">
        Back to home
      </Link>
    </main>
  );
}
