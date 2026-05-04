import { Archive, Save, Upload } from "lucide-react";
import { archivePassportAction, savePassportAction } from "@/app/admin/passports/actions";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { sampleDocumentLabels } from "@/lib/seed/sample-passport";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";
import type { BatteryPassport, PassportDocumentKey } from "@/types/passport";

type SectionEditorProps = {
  passport: BatteryPassport;
  mode: "new" | "edit";
};

const materialFields = [
  ["materialNickel", "Nickel"],
  ["materialCopper", "Copper"],
  ["materialAluminium", "Aluminium"],
  ["materialGraphite", "Graphite"],
  ["materialManganese", "Manganese"],
  ["materialCobalt", "Cobalt"],
  ["materialLithium", "Lithium"],
  ["materialElectrolyte", "Electrolyte and separators"],
] as const;

const carbonFields = [
  ["carbonRawMaterial", "raw material extraction"],
  ["carbonMainProduction", "main production"],
  ["carbonDistribution", "distribution"],
  ["carbonRecycling", "recycling"],
] as const;

const recycledFields = [
  ["recycledNickel", "Nickel"],
  ["recycledCobalt", "Cobalt"],
  ["recycledLithium", "Lithium"],
  ["recycledLead", "Lead"],
] as const;

function chartValue(segments: Array<{ label: string; value: number }>, label: string) {
  return segments.find((segment) => segment.label === label)?.value ?? 0;
}

function recycledValue(charts: Array<{ material: string; preConsumerShare: number; postConsumerShare: number; primaryMaterialShare: number }>, material: string) {
  return charts.find((chart) => chart.material === material) ?? { preConsumerShare: 0, postConsumerShare: 0, primaryMaterialShare: 0 };
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-1.5 text-sm font-medium">
      <span>{label}</span>
      {children}
    </label>
  );
}

function TextField({
  label,
  name,
  defaultValue,
  type = "text",
  readOnly = false,
}: {
  label: string;
  name: string;
  defaultValue: string | number;
  type?: string;
  readOnly?: boolean;
}) {
  return (
    <Field label={label}>
      <Input
        name={name}
        type={type}
        step={type === "number" ? "any" : undefined}
        defaultValue={defaultValue}
        readOnly={readOnly}
        className={readOnly ? "bg-slate-100 dark:bg-slate-900" : ""}
      />
    </Field>
  );
}

function FormSection({ id, title, children }: { id: string; title: string; children: React.ReactNode }) {
  return (
    <section id={id} className="scroll-mt-24 border-b border-slate-200 pb-6 last:border-0 dark:border-slate-800">
      <h2 className="text-lg font-semibold">{title}</h2>
      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{children}</div>
    </section>
  );
}

export function SectionEditor({ passport, mode }: SectionEditorProps) {
  const viewModel = toPassportViewModel(passport);
  const materialSegments = viewModel.materialComposition.segments;
  const carbonSegments = viewModel.carbonFootprintStages.segments;
  const supplyChain = passport.aspects.supplyChainDueDiligence?.payload ?? {};

  return (
    <div className="grid gap-6 lg:grid-cols-[240px_1fr]">
      <nav className="space-y-2 lg:sticky lg:top-20 lg:self-start">
        {viewModel.sections.map((section) => (
          <a
            key={section}
            href={`#admin-${section.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`}
            className="block rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-800 dark:bg-slate-900"
          >
            {section}
          </a>
        ))}
      </nav>

      <div className="space-y-4">
        <Card>
          <form action={savePassportAction} className="space-y-6">
            <FormSection id="admin-general" title="General">
              <TextField label="Passport ID" name="passportId" defaultValue={viewModel.passportId} readOnly={mode === "edit"} />
              <TextField label="Name" name="name" defaultValue={viewModel.name} />
              <TextField label="Model Number" name="modelNumber" defaultValue={viewModel.modelNumber} />
              <TextField label="Serial Number" name="serialNumber" defaultValue={viewModel.serialNumber} />
              <TextField label="Category" name="category" defaultValue={viewModel.category} />
              <TextField label="Status" name="batteryStatus" defaultValue={viewModel.batteryStatus} />
              <TextField label="Weight kg" name="batteryMass" type="number" defaultValue={viewModel.weight} />
              <TextField label="Manufactured date" name="manufacturingDate" type="date" defaultValue={viewModel.manufacturedDate} />
              <TextField label="Facility ID" name="facilityId" defaultValue={viewModel.facilityId} />
              <TextField label="Manufactured by" name="manufacturerName" defaultValue={viewModel.manufacturerName} />
              <Field label="Registry status">
                <select
                  name="status"
                  defaultValue={viewModel.status}
                  className="min-h-10 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-950"
                >
                  <option value="draft">Draft</option>
                  <option value="published">Published</option>
                  <option value="archived">Archived</option>
                </select>
              </Field>
              <Field label="Battery image">
                <span className="text-xs font-normal text-slate-500">Current: {viewModel.batteryImageUrl}</span>
                <Input name="batteryImage" type="file" accept="image/*" />
              </Field>
            </FormSection>

            <FormSection id="admin-material-composition" title="Material composition">
              {materialFields.map(([name, label]) => (
                <TextField key={name} label={`${label} kg`} name={name} type="number" defaultValue={chartValue(materialSegments, label)} />
              ))}
            </FormSection>

            <FormSection id="admin-performance" title="Performance">
              <TextField label="Rated energy kWh" name="ratedEnergy" type="number" defaultValue={viewModel.performance.ratedEnergy} />
              <TextField label="Rated capacity Ah" name="ratedCapacity" type="number" defaultValue={viewModel.performance.ratedCapacity} />
              <TextField label="Maximum power kW" name="ratedMaximumPower" type="number" defaultValue={viewModel.performance.ratedMaximumPower} />
              <TextField label="Nominal voltage V" name="nominalVoltage" type="number" defaultValue={viewModel.performance.nominalVoltage} />
              <TextField label="Expected lifetime years" name="expectedLifetime" type="number" defaultValue={viewModel.performance.expectedLifetime} />
              <TextField label="Expected cycles" name="expectedNumberOfCycles" type="number" defaultValue={viewModel.performance.expectedNumberOfCycles} />
              <TextField label="State of charge %" name="stateOfCharge" type="number" defaultValue={viewModel.performance.stateOfCharge} />
              <TextField label="Remaining capacity %" name="remainingCapacity" type="number" defaultValue={viewModel.performance.remainingCapacity} />
              <TextField label="Remaining energy kWh" name="remainingEnergy" type="number" defaultValue={viewModel.performance.remainingEnergy} />
              <TextField label="Full cycles" name="fullCycles" type="number" defaultValue={viewModel.performance.cycles} />
            </FormSection>

            <FormSection id="admin-compliance" title="Compliance">
              {(["conformityAssessment", "euDeclarationOfConformity"] as PassportDocumentKey[]).map((key) => (
                <Field key={key} label={sampleDocumentLabels[key]}>
                  <a className="break-all text-xs font-normal text-emerald-700 dark:text-emerald-300" href={viewModel.documents[key].url} target="_blank" rel="noreferrer">
                    Current external link
                  </a>
                  <Input name={`document_${key}`} type="file" accept="application/pdf" />
                </Field>
              ))}
            </FormSection>

            <FormSection id="admin-supply-chain" title="Supply chain">
              <TextField label="Supply chain index" name="supplyChainIndex" type="number" defaultValue={Number(supplyChain.supplyChainIndicies ?? 82)} />
              {(["sustainabilityReport", "dueDiligenceReport", "thirdPartyAudit", "taxonomyReport"] as PassportDocumentKey[]).map((key) => (
                <Field key={key} label={sampleDocumentLabels[key]}>
                  <a className="break-all text-xs font-normal text-emerald-700 dark:text-emerald-300" href={viewModel.documents[key].url} target="_blank" rel="noreferrer">
                    Current external link
                  </a>
                  <Input name={`document_${key}`} type="file" accept="application/pdf" />
                </Field>
              ))}
            </FormSection>

            <FormSection id="admin-circularity" title="Circularity">
              <Field label="Separate collection">
                <textarea
                  name="separateCollection"
                  defaultValue={viewModel.circularity.separateCollection}
                  className="min-h-24 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-950"
                />
              </Field>
              <Field label="Waste prevention">
                <textarea
                  name="wastePrevention"
                  defaultValue={viewModel.circularity.wastePrevention}
                  className="min-h-24 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-950"
                />
              </Field>
              <Field label="Recycled content share verification">
                <select
                  name="recycledContentShareVerification"
                  defaultValue={viewModel.circularity.recycledContentShareVerification}
                  className="min-h-10 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-950"
                >
                  <option value="verified">Verified</option>
                  <option value="partial">Partial</option>
                  <option value="unverified">Unverified</option>
                  <option value="invalid">Invalid</option>
                </select>
              </Field>
              {recycledFields.flatMap(([prefix, material]) => {
                const values = recycledValue(viewModel.recycledContent, material);
                return [
                  <TextField key={`${prefix}Pre`} label={`${material} pre consumer %`} name={`${prefix}Pre`} type="number" defaultValue={values.preConsumerShare} />,
                  <TextField key={`${prefix}Post`} label={`${material} post consumer %`} name={`${prefix}Post`} type="number" defaultValue={values.postConsumerShare} />,
                  <TextField key={`${prefix}Primary`} label={`${material} primary material %`} name={`${prefix}Primary`} type="number" defaultValue={values.primaryMaterialShare} />,
                ];
              })}
            </FormSection>

            <FormSection id="admin-carbon-footprint" title="Carbon Footprint">
              <TextField label="Amount gCO2e/kWh" name="carbonFootprint" type="number" defaultValue={viewModel.carbonFootprint} />
              <TextField label="Performance class" name="performanceClass" defaultValue={viewModel.performanceClass} />
              {carbonFields.map(([name, label]) => (
                <TextField key={name} label={`${label} gCO2e/kWh`} name={name} type="number" defaultValue={chartValue(carbonSegments, label)} />
              ))}
              <Field label={sampleDocumentLabels.co2StudyReference}>
                <a className="break-all text-xs font-normal text-emerald-700 dark:text-emerald-300" href={viewModel.documents.co2StudyReference.url} target="_blank" rel="noreferrer">
                  Current external link
                </a>
                <Input name="document_co2StudyReference" type="file" accept="application/pdf" />
              </Field>
            </FormSection>

            <div className="flex flex-wrap justify-end gap-3">
              <Button type="submit">
                <Save className="h-4 w-4" />
                Save passport
              </Button>
            </div>
          </form>
        </Card>

        {mode === "edit" ? (
          <Card className="border-orange-200 dark:border-orange-900">
            <form action={archivePassportAction} className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="font-semibold">Archive hidden from public search</h2>
                <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">This preserves data and files while removing the passport from public registry results.</p>
              </div>
              <input type="hidden" name="passportId" value={viewModel.passportId} />
              <Button type="submit" className="bg-orange-600 hover:bg-orange-700">
                <Archive className="h-4 w-4" />
                Archive
              </Button>
            </form>
          </Card>
        ) : null}

        <p className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
          <Upload className="h-4 w-4" />
          PDF and image uploads are stored in MongoDB GridFS and exposed through app-served file URLs.
        </p>
      </div>
    </div>
  );
}
