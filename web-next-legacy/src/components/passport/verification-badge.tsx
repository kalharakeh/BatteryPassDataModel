import { ShieldCheck } from "lucide-react";
import { StatusPill } from "@/components/ui/status-pill";

export function VerificationBadge({ valid }: { valid: boolean }) {
  return (
    <div className="flex items-center gap-2">
      <ShieldCheck className="h-5 w-5 text-emerald-700 dark:text-emerald-300" />
      <StatusPill status={valid ? "verified" : "unverified"} label={valid ? "Verified" : "Unverified"} />
    </div>
  );
}
