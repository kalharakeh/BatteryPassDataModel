import { notFound } from "next/navigation";
import { PassportDetailReport } from "@/components/passport/passport-detail-report";
import { PassportCard } from "@/components/passport/passport-card";
import { requirePassportDetailAccess } from "@/lib/auth/cluster-guards";
import { listClusters } from "@/lib/db/clusters";
import { getPassport } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export const dynamic = "force-dynamic";

export default async function PassportPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const decodedPassportId = decodeURIComponent(passportId);
  const passport = await getPassport(decodedPassportId);
  if (!passport) notFound();
  const nextPath = `/${encodeURIComponent(decodedPassportId)}`;
  await requirePassportDetailAccess(passport, {
    nextPath,
    unauthorizedRedirectPath: `${nextPath}/summary?access=wrong-cluster`,
  });
  const clusters = await listClusters();
  const viewModel = toPassportViewModel(passport, { clusters });

  return (
    <main className="mx-auto max-w-6xl space-y-6 px-4 py-10">
      <PassportCard passport={viewModel} />
      <PassportDetailReport passport={passport} viewModel={viewModel} />
    </main>
  );
}
