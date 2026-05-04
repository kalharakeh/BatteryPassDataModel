import { notFound } from "next/navigation";
import { ClusterPassportEditor } from "@/components/cluster-admin/cluster-passport-editor";
import { requireClusterAdminAccess } from "@/lib/auth/cluster-guards";
import { getPassportForAdmin } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export const dynamic = "force-dynamic";

export default async function ClusterAdminEditPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassportForAdmin(decodeURIComponent(passportId));
  if (!passport) notFound();
  await requireClusterAdminAccess(passport);
  const viewModel = toPassportViewModel(passport);

  return (
    <main className="mx-auto max-w-5xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Edit local battery data</h1>
      <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{passport.passportId}</p>
      <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">
        {viewModel.modelNumber} in {passport.clusterId}
      </p>
      <div className="mt-6">
        <ClusterPassportEditor passport={passport} />
      </div>
    </main>
  );
}
