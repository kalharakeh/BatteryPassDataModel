import { RegistryTable } from "@/components/registry/registry-table";
import { listPassports } from "@/lib/db/passports";

export default async function RegistryPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const params = await searchParams;
  const passports = await listPassports(params.q ?? "");

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Passport Registry</h1>
      <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Search and open registered battery passports.</p>
      <div className="mt-6">
        <RegistryTable passports={passports} />
      </div>
    </main>
  );
}
