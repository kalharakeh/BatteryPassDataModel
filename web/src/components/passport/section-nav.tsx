export function SectionNav({ sections }: { sections: string[] }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {sections.map((section) => (
        <section key={section} className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
          <h2 className="font-medium">{section}</h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">View details and verification metadata.</p>
        </section>
      ))}
    </div>
  );
}
