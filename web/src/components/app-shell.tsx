import Link from "next/link";
import { BatteryCharging } from "lucide-react";
import { ThemeToggle } from "@/components/theme/theme-toggle";

export function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white/90 backdrop-blur dark:border-slate-800 dark:bg-slate-950/90">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
          <Link href="/" className="flex items-center gap-2 font-semibold">
            <BatteryCharging className="h-6 w-6 text-emerald-700 dark:text-emerald-300" />
            <span>Battery Passport</span>
          </Link>
          <nav className="flex items-center gap-4 text-sm">
            <Link
              href="/registry"
              className="text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300"
            >
              Registry
            </Link>
            <Link
              href="/admin"
              className="text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300"
            >
              Admin
            </Link>
            <ThemeToggle />
          </nav>
        </div>
      </header>
      {children}
      <footer className="border-t border-slate-200 px-4 py-6 text-center text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
        Internal demonstrator. Sample values are for demonstration purposes only.
      </footer>
    </div>
  );
}
