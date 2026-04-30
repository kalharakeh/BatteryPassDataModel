import Link from "next/link";
import { Search } from "lucide-react";

export default function LandingPage() {
  return (
    <main className="mx-auto flex min-h-[70vh] max-w-3xl flex-col justify-center px-4 py-12">
      <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-semibold tracking-normal">Battery Passport Viewer</h1>
          <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
            Enter a battery passport DID to open the public demonstrator view.
          </p>
        </div>
        <form action="/registry" className="flex flex-col gap-3 sm:flex-row">
          <input
            name="q"
            className="min-h-11 flex-1 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-900"
            placeholder="did:web:local.battery.pass:sample"
          />
          <button
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800"
            type="submit"
          >
            <Search className="h-4 w-4" />
            Search
          </button>
        </form>
        <div className="flex gap-3 text-sm">
          <Link className="text-emerald-700 dark:text-emerald-300" href="/registry">
            Open registry
          </Link>
          <Link className="text-emerald-700 dark:text-emerald-300" href="/login">
            Admin login
          </Link>
        </div>
      </div>
    </main>
  );
}
