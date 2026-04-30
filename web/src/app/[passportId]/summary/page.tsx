import { notFound } from "next/navigation";
import { Card } from "@/components/ui/card";
import { getPassport } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export default async function PassportSummaryPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) notFound();
  const viewModel = toPassportViewModel(passport);

  return (
    <main className="mx-auto max-w-6xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Summary report</h1>
      <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{viewModel.passportId}</p>
      <div className="mt-6 grid gap-4 md:grid-cols-2">
        {viewModel.sections.map((section) => (
          <Card key={section}>
            <h2 className="font-medium">{section}</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
              Detailed schema-aligned values will appear in this section.
            </p>
          </Card>
        ))}
      </div>
    </main>
  );
}
