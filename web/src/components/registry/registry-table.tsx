import Link from "next/link";
import type { BatteryPassport } from "@/types/passport";

export function RegistryTable({ passports }: { passports: BatteryPassport[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <table className="w-full min-w-[760px] text-left text-sm">
        <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
          <tr>
            <th className="px-4 py-3">Registry ID</th>
            <th className="px-4 py-3">Passport ID</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Updated</th>
            <th className="px-4 py-3">Actions</th>
          </tr>
        </thead>
        <tbody>
          {passports.map((passport) => (
            <tr key={passport.passportId} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
              <td className="px-4 py-3">{passport.registryInfo.registryId}</td>
              <td className="max-w-sm truncate px-4 py-3">{passport.passportId}</td>
              <td className="px-4 py-3">{passport.registryInfo.status}</td>
              <td className="px-4 py-3">{passport.registryInfo.updatedAt}</td>
              <td className="px-4 py-3">
                <Link className="text-emerald-700 dark:text-emerald-300" href={`/${encodeURIComponent(passport.passportId)}`}>
                  View
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
