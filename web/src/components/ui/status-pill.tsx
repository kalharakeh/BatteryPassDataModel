const styles = {
  verified: "bg-emerald-700 text-white dark:bg-emerald-800",
  partial: "bg-amber-500 text-white dark:bg-amber-600",
  unverified: "bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-100",
  invalid: "bg-red-600 text-white dark:bg-red-700",
};

export function StatusPill({ status, label }: { status: keyof typeof styles; label: string }) {
  return <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[status]}`}>{label}</span>;
}
