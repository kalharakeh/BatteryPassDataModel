import { Card } from "@/components/ui/card";
import { aspectSchemas } from "@/lib/schemas/registry";
import type { BatteryPassport } from "@/types/passport";

export function SectionEditor({ passport }: { passport: BatteryPassport }) {
  return (
    <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
      <nav className="space-y-2">
        {Object.values(aspectSchemas).map((schema) => (
          <a
            key={schema.key}
            href={`#${schema.key}`}
            className="block rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-800 dark:bg-slate-900"
          >
            {schema.title}
          </a>
        ))}
      </nav>
      <div className="space-y-4">
        {Object.values(aspectSchemas).map((schema) => (
          <Card key={schema.key} className="scroll-mt-24">
            <div id={schema.key}>
              <h2 className="font-medium">{schema.title}</h2>
              <p className="mt-1 text-xs text-slate-500">Schema version {schema.version}</p>
              <pre className="mt-4 max-h-80 overflow-auto rounded-md bg-slate-950 p-4 text-xs text-slate-100">
                {JSON.stringify(passport.aspects[schema.key]?.payload ?? {}, null, 2)}
              </pre>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
