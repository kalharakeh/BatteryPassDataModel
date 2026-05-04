import { Save, Upload } from "lucide-react";
import { saveClusterPassportAction } from "@/app/cluster-admin/passports/actions";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";
import type { BatteryPassport } from "@/types/passport";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-1.5 text-sm font-medium">
      <span>{label}</span>
      {children}
    </label>
  );
}

function NumberField({ label, name, defaultValue }: { label: string; name: string; defaultValue: number }) {
  return (
    <Field label={label}>
      <Input name={name} type="number" step="any" defaultValue={defaultValue} />
    </Field>
  );
}

export function ClusterPassportEditor({ passport }: { passport: BatteryPassport }) {
  const viewModel = toPassportViewModel(passport);

  return (
    <Card>
      <form action={saveClusterPassportAction} className="space-y-6">
        <input type="hidden" name="passportId" value={passport.passportId} />
        <div>
          <p className="text-xs uppercase text-slate-500 dark:text-slate-400">Cluster</p>
          <p className="mt-1 text-sm font-medium">{passport.clusterId ?? "Unassigned"}</p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Facility ID">
            <Input name="facilityId" defaultValue={viewModel.facilityId} />
          </Field>
          <NumberField label="State of charge %" name="stateOfCharge" defaultValue={viewModel.performance.stateOfCharge} />
          <NumberField label="Remaining capacity %" name="remainingCapacity" defaultValue={viewModel.performance.remainingCapacity} />
          <NumberField label="Remaining energy kWh" name="remainingEnergy" defaultValue={viewModel.performance.remainingEnergy} />
          <NumberField label="Full cycles" name="fullCycles" defaultValue={viewModel.performance.cycles} />
          <Field label="Battery image">
            <span className="text-xs font-normal text-slate-500">Current: {viewModel.batteryImageUrl}</span>
            <Input name="batteryImage" type="file" accept="image/*" />
          </Field>
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4 text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
          <span className="inline-flex items-center gap-2">
            <Upload className="h-4 w-4" />
            Local admins can update only operational condition fields and public battery images.
          </span>
          <Button type="submit">
            <Save className="h-4 w-4" />
            Save local updates
          </Button>
        </div>
      </form>
    </Card>
  );
}
