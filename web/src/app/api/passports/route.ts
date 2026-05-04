import { NextResponse } from "next/server";
import { listPassportsForRegistryAccess } from "@/lib/auth/cluster-guards";
import { requireRole } from "@/lib/auth/session";
import { upsertPassport } from "@/lib/db/passports";
import type { BatteryPassport } from "@/types/passport";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const passports = await listPassportsForRegistryAccess(url.searchParams.get("q") ?? "");
  return NextResponse.json({ passports });
}

export async function POST(request: Request) {
  await requireRole("admin");
  const passport = (await request.json()) as BatteryPassport;
  if (!passport.passportId) {
    return NextResponse.json({ error: "passportId is required" }, { status: 400 });
  }
  const saved = await upsertPassport(passport);
  return NextResponse.json({ passport: saved }, { status: 201 });
}
