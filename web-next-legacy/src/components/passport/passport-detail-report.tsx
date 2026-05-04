"use client";

import { useState } from "react";
import Link from "next/link";
import { ExternalLink } from "lucide-react";
import { DonutChart } from "@/components/charts/donut-chart";
import { PieChart } from "@/components/charts/pie-chart";
import type { toPassportViewModel } from "@/lib/view-model/passport-view-model";
import type { BatteryPassport, PassportDocumentLink } from "@/types/passport";

type PassportViewModel = ReturnType<typeof toPassportViewModel>;

type DetailReportProps = {
  passport: BatteryPassport;
  viewModel: PassportViewModel;
};

function sectionId(section: string) {
  return section.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-sm font-medium">{value || "Not provided"}</dd>
    </div>
  );
}

function Metric({ label, value, unit = "" }: { label: string; value: number | string; unit?: string }) {
  return (
    <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
      <p className="text-xs uppercase text-slate-500 dark:text-slate-400">{label}</p>
      <p className="mt-1 text-lg font-semibold">
        {value}
        {unit ? <span className="ml-1 text-sm font-medium text-slate-500">{unit}</span> : null}
      </p>
    </div>
  );
}

function DocumentRow({ document }: { document: PassportDocumentLink }) {
  return (
    <a
      href={document.url}
      target="_blank"
      rel="noreferrer"
      className="flex min-h-11 items-center justify-between gap-3 rounded-md border border-slate-200 px-3 text-sm hover:border-emerald-600 dark:border-slate-800"
    >
      <span>{document.label}</span>
      <span className="inline-flex items-center gap-1 text-emerald-700 dark:text-emerald-300">
        External link
        <ExternalLink className="h-4 w-4" />
      </span>
    </a>
  );
}

function GeneralPanel({ viewModel }: { viewModel: PassportViewModel }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">General</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Generic information about the battery</p>
      <dl className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Field label="Name" value={viewModel.name} />
        <Field label="Manufactured date" value={viewModel.manufacturedDate} />
        <Field label="Facility ID" value={viewModel.facilityId} />
        <Field label="Manufactured by" value={viewModel.manufacturerName} />
        <Field label="Category" value={viewModel.category} />
        <Field label="Status" value={viewModel.batteryStatus} />
        <Field label="Weight" value={viewModel.weightLabel} />
        <Field label="Verified" value={viewModel.verificationState === "verified" ? "Verified" : "Unverified"} />
      </dl>
    </section>
  );
}

function MaterialPanel({
  viewModel,
  chemistry,
  materials,
}: {
  viewModel: PassportViewModel;
  chemistry: Record<string, unknown>;
  materials: Array<Record<string, unknown>>;
}) {
  return (
    <section>
      <h2 className="text-xl font-semibold">Material composition</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Material composition of the battery</p>
      <div className="mt-6 grid gap-8 lg:grid-cols-[320px_1fr]">
        <PieChart title="Battery material composition" segments={viewModel.materialComposition.segments} size={300} />
        <div className="overflow-x-auto">
          <dl className="mb-4 grid gap-4 sm:grid-cols-2">
            <Field label="Chemistry" value={`${chemistry.shortName ?? "NMC"} - ${chemistry.clearName ?? ""}`} />
            <Field label="Declared total" value={`${viewModel.materialComposition.total.toFixed(1)} kg`} />
          </dl>
          <table className="w-full min-w-[540px] text-left text-sm">
            <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
              <tr>
                <th className="py-2 pr-4">Material</th>
                <th className="py-2 pr-4">Mass</th>
                <th className="py-2 pr-4">Component</th>
                <th className="py-2 pr-4">Critical</th>
              </tr>
            </thead>
            <tbody>
              {materials.map((material) => {
                const location = (material.batteryMaterialLocation ?? {}) as Record<string, unknown>;
                return (
                  <tr key={String(material.batteryMaterialName)} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
                    <td className="py-2 pr-4 font-medium">{String(material.batteryMaterialName ?? "")}</td>
                    <td className="py-2 pr-4">{String(material.batteryMaterialMass ?? "")} kg</td>
                    <td className="py-2 pr-4">{String(location.componentName ?? "")}</td>
                    <td className="py-2 pr-4">{material.isCriticalRawMaterial ? "Yes" : "No"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

function PerformancePanel({ viewModel }: { viewModel: PassportViewModel }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">Performance</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Battery performance information</p>
      <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Metric label="Rated energy" value={viewModel.performance.ratedEnergy} unit="kWh" />
        <Metric label="Rated capacity" value={viewModel.performance.ratedCapacity} unit="Ah" />
        <Metric label="Maximum power" value={viewModel.performance.ratedMaximumPower} unit="kW" />
        <Metric label="Nominal voltage" value={viewModel.performance.nominalVoltage} unit="V" />
        <Metric label="State of charge" value={viewModel.performance.stateOfCharge} unit="%" />
        <Metric label="Remaining capacity" value={viewModel.performance.remainingCapacity} unit="%" />
        <Metric label="Remaining energy" value={viewModel.performance.remainingEnergy} unit="kWh" />
        <Metric label="Full cycles" value={viewModel.performance.cycles} />
      </div>
    </section>
  );
}

function CompliancePanel({ viewModel }: { viewModel: PassportViewModel }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">Compliance</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Regulatory compliance of the battery</p>
      <div className="mt-5 grid gap-3 md:grid-cols-2">
        <DocumentRow document={viewModel.documents.conformityAssessment} />
        <DocumentRow document={viewModel.documents.euDeclarationOfConformity} />
      </div>
    </section>
  );
}

function SupplyChainPanel({ viewModel, supplyChain }: { viewModel: PassportViewModel; supplyChain: Record<string, unknown> }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">Supply chain</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Supply chain information</p>
      <div className="mt-5 grid gap-4 lg:grid-cols-[180px_1fr]">
        <Metric label="Supply chain index" value={Number(supplyChain.supplyChainIndicies ?? 0)} />
        <div className="grid gap-3 md:grid-cols-2">
          <DocumentRow document={viewModel.documents.sustainabilityReport} />
          <DocumentRow document={viewModel.documents.dueDiligenceReport} />
          <DocumentRow document={viewModel.documents.thirdPartyAudit} />
          <DocumentRow document={viewModel.documents.taxonomyReport} />
        </div>
      </div>
    </section>
  );
}

function CircularityPanel({ viewModel }: { viewModel: PassportViewModel }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">Circularity</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Circularity of the battery</p>
      <dl className="mt-5 grid gap-4 lg:grid-cols-3">
        <Field label="Separate collection" value={viewModel.circularity.separateCollection} />
        <Field label="End of life information: Waste prevention" value={viewModel.circularity.wastePrevention} />
        <Field
          label="Recycled content share"
          value={
            <span className="rounded-full bg-orange-600 px-3 py-1 text-xs font-semibold text-white">
              {viewModel.circularity.recycledContentShareVerification === "verified" ? "Verified" : "Unverified"}
            </span>
          }
        />
      </dl>
      <div className="mt-6 grid gap-6 md:grid-cols-2 xl:grid-cols-4">
        {viewModel.recycledContent.map((chart) => (
          <DonutChart key={chart.material} chart={chart} />
        ))}
      </div>
    </section>
  );
}

function CarbonPanel({ viewModel }: { viewModel: PassportViewModel }) {
  return (
    <section>
      <h2 className="text-xl font-semibold">Carbon Footprint</h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">CO2 footprint generated by the battery</p>
      <div className="mt-5 grid gap-8 lg:grid-cols-[320px_1fr]">
        <PieChart title="Carbon footprint" segments={viewModel.carbonFootprintStages.segments} size={300} legendColumns="grid-cols-1" />
        <div className="space-y-5">
          <dl className="grid gap-4 sm:grid-cols-2">
            <Field label="Amount" value={`Verified ${viewModel.carbonFootprintLabel}`} />
            <Field label="Performance class" value={viewModel.performanceClass} />
          </dl>
          <DocumentRow document={viewModel.documents.co2StudyReference} />
          <Link href={`/${encodeURIComponent(viewModel.passportId)}/summary`} className="inline-flex text-sm font-medium text-emerald-700 dark:text-emerald-300">
            Open summary report
          </Link>
        </div>
      </div>
    </section>
  );
}

export function PassportDetailReport({ passport, viewModel }: DetailReportProps) {
  const [activeSection, setActiveSection] = useState(viewModel.sections[0]);
  const materialPayload = passport.aspects.materialComposition?.payload ?? {};
  const chemistry = (materialPayload.batteryChemistry ?? {}) as Record<string, unknown>;
  const materials = Array.isArray(materialPayload.batteryMaterials)
    ? (materialPayload.batteryMaterials as Array<Record<string, unknown>>)
    : [];
  const supplyChain = passport.aspects.supplyChainDueDiligence?.payload ?? {};

  let activePanel: React.ReactNode;
  switch (activeSection) {
    case "Material composition":
      activePanel = <MaterialPanel viewModel={viewModel} chemistry={chemistry} materials={materials} />;
      break;
    case "Performance":
      activePanel = <PerformancePanel viewModel={viewModel} />;
      break;
    case "Compliance":
      activePanel = <CompliancePanel viewModel={viewModel} />;
      break;
    case "Supply chain":
      activePanel = <SupplyChainPanel viewModel={viewModel} supplyChain={supplyChain} />;
      break;
    case "Circularity":
      activePanel = <CircularityPanel viewModel={viewModel} />;
      break;
    case "Carbon Footprint":
      activePanel = <CarbonPanel viewModel={viewModel} />;
      break;
    case "General":
    default:
      activePanel = <GeneralPanel viewModel={viewModel} />;
      break;
  }

  return (
    <div id="detail-tabs" className="space-y-6 scroll-mt-24">
      <div
        className="flex gap-2 overflow-x-auto border-b border-slate-200 pb-2 dark:border-slate-800"
        role="tablist"
        aria-label="Passport sections"
      >
        {viewModel.sections.map((section) => {
          const selected = activeSection === section;
          return (
            <button
              key={section}
              id={`${sectionId(section)}-tab`}
              type="button"
              role="tab"
              aria-selected={selected}
              aria-controls={`${sectionId(section)}-panel`}
              onClick={() => setActiveSection(section)}
              className={`shrink-0 rounded-md px-3 py-2 text-sm font-medium ${
                selected
                  ? "bg-emerald-700 text-white"
                  : "text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-900"
              }`}
            >
              {section}
            </button>
          );
        })}
      </div>

      <div
        id={`${sectionId(activeSection)}-panel`}
        role="tabpanel"
        aria-labelledby={`${sectionId(activeSection)}-tab`}
        className="rounded-lg border border-slate-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900"
      >
        {activePanel}
      </div>
    </div>
  );
}
