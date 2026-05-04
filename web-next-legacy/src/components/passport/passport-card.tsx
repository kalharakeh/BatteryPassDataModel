import Link from "next/link";
import Image from "next/image";
import { FileText, ListTree } from "lucide-react";
import { Card } from "@/components/ui/card";
import { VerificationBadge } from "./verification-badge";

type PassportCardProps = {
  passport: {
    passportId: string;
    modelNumber: string;
    serialNumber: string;
    category: string;
    batteryStatus: string;
    weightLabel: string;
    carbonFootprint: string;
    manufacturedDate: string;
    manufacturerName: string;
    clusterLabel: string;
    batteryImageUrl: string;
    batteryImageAlt: string;
    isValid: boolean;
  };
};

export function PassportCard({ passport }: PassportCardProps) {
  return (
    <Card>
      <div className="grid gap-6 lg:grid-cols-[1fr_320px] lg:items-start">
        <div>
          <div className="flex flex-wrap items-center gap-3">
            <VerificationBadge valid={passport.isValid} />
            <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-600 dark:bg-slate-800 dark:text-slate-300">
              {passport.batteryStatus}
            </span>
          </div>
          <p className="mt-4 break-all text-sm font-medium text-slate-700 dark:text-slate-200">{passport.passportId}</p>
          <h1 className="mt-4 text-2xl font-semibold">{passport.modelNumber || "Battery Passport"}</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Cluster: {passport.clusterLabel}</p>
          <div className="mt-4 flex flex-wrap gap-3">
            <Link
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-center text-sm font-medium text-white hover:bg-emerald-800"
              href={`/${encodeURIComponent(passport.passportId)}/summary`}
            >
              <FileText className="h-4 w-4" />
              Summary report
            </Link>
            <Link
              className="inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-slate-300 px-4 text-sm font-medium hover:border-emerald-600 dark:border-slate-700"
              href={`/${encodeURIComponent(passport.passportId)}#detail-tabs`}
            >
              <ListTree className="h-4 w-4" />
              Detailed report
            </Link>
          </div>
        </div>
        <div className="overflow-hidden rounded-lg border border-slate-200 bg-slate-100 dark:border-slate-800 dark:bg-slate-950">
          <Image
            src={passport.batteryImageUrl}
            alt={passport.batteryImageAlt}
            width={640}
            height={480}
            className="aspect-[4/3] w-full object-cover"
            unoptimized
          />
        </div>
      </div>
      <dl className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <dt className="text-xs uppercase text-slate-500">Model Number</dt>
          <dd className="mt-1 font-medium">{passport.modelNumber}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Serial Number</dt>
          <dd className="mt-1 font-medium">{passport.serialNumber}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Category</dt>
          <dd className="mt-1 font-medium">{passport.category}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Status</dt>
          <dd className="mt-1 font-medium">{passport.batteryStatus}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Weight</dt>
          <dd className="mt-1 font-medium">{passport.weightLabel}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Manufactured date</dt>
          <dd className="mt-1 font-medium">{passport.manufacturedDate}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Manufactured by</dt>
          <dd className="mt-1 font-medium">{passport.manufacturerName}</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Cluster</dt>
          <dd className="mt-1 font-medium">{passport.clusterLabel}</dd>
        </div>
      </dl>
    </Card>
  );
}
