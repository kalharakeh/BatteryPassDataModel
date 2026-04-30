import { notFound } from "next/navigation";
import { PassportCard } from "@/components/passport/passport-card";
import { SectionNav } from "@/components/passport/section-nav";
import { getPassport } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export const dynamic = "force-dynamic";

export default async function PassportPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) notFound();
  const viewModel = toPassportViewModel(passport);

  return (
    <main className="mx-auto max-w-6xl space-y-6 px-4 py-10">
      <PassportCard passport={viewModel} />
      <SectionNav sections={viewModel.sections} />
    </main>
  );
}
