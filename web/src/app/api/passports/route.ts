import { NextResponse } from "next/server";
import { listPassports, upsertPassport } from "@/lib/db/passports";
import type { BatteryPassport } from "@/types/passport";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const passports = await listPassports(url.searchParams.get("q") ?? "");
  return NextResponse.json({ passports });
}

export async function POST(request: Request) {
  const passport = (await request.json()) as BatteryPassport;
  if (!passport.passportId) {
    return NextResponse.json({ error: "passportId is required" }, { status: 400 });
  }
  const saved = await upsertPassport(passport);
  return NextResponse.json({ passport: saved }, { status: 201 });
}
