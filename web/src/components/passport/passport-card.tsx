import Link from "next/link";
import { Card } from "@/components/ui/card";
import { VerificationBadge } from "./verification-badge";

type PassportCardProps = {
  passport: {
    passportId: string;
    modelNumber: string;
    serialNumber: string;
    category: string;
    batteryStatus: string;
    weight: string;
    carbonFootprint: string;
    isValid: boolean;
  };
};

export function PassportCard({ passport }: PassportCardProps) {
  return (
    <Card>
      <div className="flex flex-col gap-6 md:flex-row md:items-start md:justify-between">
        <div>
          <VerificationBadge valid={passport.isValid} />
          <h1 className="mt-4 text-2xl font-semibold">{passport.modelNumber || "Battery Passport"}</h1>
          <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{passport.passportId}</p>
        </div>
        <Link
          className="rounded-md bg-emerald-700 px-4 py-2 text-center text-sm font-medium text-white hover:bg-emerald-800"
          href={`/${encodeURIComponent(passport.passportId)}/summary`}
        >
          Summary report
        </Link>
      </div>
      <dl className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
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
          <dd className="mt-1 font-medium">{passport.weight} kg</dd>
        </div>
        <div>
          <dt className="text-xs uppercase text-slate-500">Carbon footprint</dt>
          <dd className="mt-1 font-medium">{passport.carbonFootprint} kg CO2e/kWh</dd>
        </div>
      </dl>
    </Card>
  );
}
