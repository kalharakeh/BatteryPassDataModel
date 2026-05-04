import { notFound } from "next/navigation";
import { PassportSummaryReport } from "@/components/passport/passport-summary-report";
import { canOpenPassportDetail, isGeneralAdmin } from "@/lib/auth/cluster-access";
import { getSession } from "@/lib/auth/session";
import { getClusterMembershipsForUser, listClusters } from "@/lib/db/clusters";
import { getPassport } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export const dynamic = "force-dynamic";

export default async function PassportSummaryPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) notFound();
  const [clusters, session] = await Promise.all([listClusters(), getSession()]);
  const memberships = session ? await getClusterMembershipsForUser(session.email).catch(() => []) : [];
  const viewModel = toPassportViewModel(passport, { clusters });
  const canOpenDetail = canOpenPassportDetail(session, passport, memberships);
  const detailAccessNotice = canOpenDetail
    ? undefined
    : passport.clusterId
      ? session
        ? `Current user ${session.email} is not connected to ${viewModel.clusterLabel}. Sign in with a user connected to ${viewModel.clusterLabel} to open the detailed report.`
        : `Sign in with a user connected to ${viewModel.clusterLabel} to open the detailed report.`
      : isGeneralAdmin(session)
        ? undefined
        : "This battery is not assigned to a cluster. Sign in as the general admin to open the detailed report.";

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <PassportSummaryReport viewModel={viewModel} detailAccessNotice={detailAccessNotice} />
    </main>
  );
}
