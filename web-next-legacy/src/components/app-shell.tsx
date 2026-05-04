import Link from "next/link";
import { BatteryCharging, LogOut } from "lucide-react";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { buildHeaderState } from "@/lib/auth/header-state";
import { getSession } from "@/lib/auth/session";
import { getClusterMembershipsForUser, listClusters } from "@/lib/db/clusters";

export async function AppShell({ children }: { children: React.ReactNode }) {
  const session = await getSession();
  let headerState = buildHeaderState(session, [], []);
  if (session) {
    const [memberships, clusters] = await Promise.all([
      getClusterMembershipsForUser(session.email).catch(() => []),
      listClusters().catch(() => []),
    ]);
    headerState = buildHeaderState(session, memberships, clusters);
  }

  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white/90 backdrop-blur dark:border-slate-800 dark:bg-slate-950/90">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
          <Link href="/" className="flex items-center gap-2 font-semibold">
            <BatteryCharging className="h-6 w-6 text-emerald-700 dark:text-emerald-300" />
            <span>Battery Passport</span>
          </Link>
          <nav className="flex items-center gap-4 text-sm">
            {headerState.links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300"
              >
                {link.label}
              </Link>
            ))}
            {session ? (
              <div className="hidden max-w-xs text-right text-xs text-slate-600 dark:text-slate-300 md:block">
                <p className="font-medium">{headerState.identityLabel}</p>
                <p className="truncate">{headerState.clusterNames.length ? headerState.clusterNames.join(", ") : "No cluster"}</p>
              </div>
            ) : null}
            {session ? (
              <form action="/api/auth/logout" method="post">
                <button
                  type="submit"
                  className="inline-flex items-center gap-1 text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300"
                >
                  <LogOut className="h-4 w-4" />
                  Logout
                </button>
              </form>
            ) : (
              <Link
                href="/login"
                className="text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300"
              >
                Login
              </Link>
            )}
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
