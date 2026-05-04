import Image from "next/image";
import Link from "next/link";
import { FileText } from "lucide-react";
import { DonutChart } from "@/components/charts/donut-chart";
import { PieChart } from "@/components/charts/pie-chart";
import { Card } from "@/components/ui/card";
import { VerificationBadge } from "@/components/passport/verification-badge";
import type { toPassportViewModel } from "@/lib/view-model/passport-view-model";

type PassportViewModel = ReturnType<typeof toPassportViewModel>;

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 text-sm font-medium">{value}</dd>
    </div>
  );
}

export function PassportSummaryReport({
  viewModel,
  detailAccessNotice,
}: {
  viewModel: PassportViewModel;
  detailAccessNotice?: string;
}) {
  const originalPower = viewModel.performance.ratedMaximumPower || 380;

  return (
    <div className="space-y-6">
      <Card>
        <div className="grid gap-6 lg:grid-cols-[1fr_320px] lg:items-start">
          <div>
            <div className="flex flex-wrap items-center gap-3">
              <VerificationBadge valid={viewModel.isValid} />
              <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                {viewModel.batteryStatus}
              </span>
            </div>
            <p className="mt-4 break-all text-sm font-medium text-slate-700 dark:text-slate-200">{viewModel.passportId}</p>
            <h1 className="mt-4 text-2xl font-semibold">{viewModel.modelNumber}</h1>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Cluster: {viewModel.clusterLabel}</p>
            {detailAccessNotice ? (
              <p className="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100">
                {detailAccessNotice}
              </p>
            ) : null}
            <Link
              className="mt-4 inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800"
              href={`/${encodeURIComponent(viewModel.passportId)}`}
            >
              <FileText className="h-4 w-4" />
              Detailed report
            </Link>
          </div>
          <div>
            <div className="overflow-hidden rounded-lg border border-slate-200 bg-slate-100 dark:border-slate-800 dark:bg-slate-950">
              <Image
                src={viewModel.batteryImageUrl}
                alt={viewModel.batteryImageAlt}
                width={640}
                height={480}
                className="aspect-[4/3] w-full object-cover"
                unoptimized
              />
            </div>
            <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">{viewModel.batteryImageAlt}</p>
          </div>
        </div>
        <dl className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Model Number" value={viewModel.modelNumber} />
          <Field label="Serial Number" value={viewModel.serialNumber} />
          <Field label="Category" value={viewModel.category} />
          <Field label="Status" value={viewModel.batteryStatus} />
          <Field label="Weight" value={viewModel.weightLabel} />
          <Field label="Manufactured date" value={viewModel.manufacturedDate} />
          <Field label="Manufactured by" value={viewModel.manufacturerName} />
          <Field label="Cluster" value={viewModel.clusterLabel} />
        </dl>
      </Card>

      <section className="rounded-lg border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
        <span className="inline-block bg-blue-700 px-1.5 py-0.5 text-lg font-semibold uppercase text-white">Summary report</span>
        <div className="mt-6 grid gap-8 lg:grid-cols-[220px_360px_1fr]">
          <div>
            <p className="text-sm font-semibold text-slate-500 dark:text-slate-300">Original power</p>
            <p className="mt-3 text-3xl font-semibold">{originalPower.toFixed(0)} kW</p>
          </div>
          <PieChart title="Carbon footprint" segments={viewModel.carbonFootprintStages.segments} size={300} legendColumns="grid-cols-1" />
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2 text-base font-semibold text-slate-500 dark:text-slate-300">
              <span>Recycled content share</span>
              <span className="rounded-full bg-orange-600 px-3 py-1 text-sm font-semibold text-white">
                {viewModel.circularity.recycledContentShareVerification === "verified" ? "Verified" : "Unverified"}
              </span>
            </div>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {viewModel.recycledContent.map((chart) => (
                <DonutChart key={chart.material} chart={chart} size={190} />
              ))}
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
