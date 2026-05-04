import { NextResponse } from "next/server";
import { requirePassportDetailAccess } from "@/lib/auth/cluster-guards";
import { requireRole } from "@/lib/auth/session";
import { archivePassport, getPassport, upsertPassport } from "@/lib/db/passports";
import type { BatteryPassport } from "@/types/passport";

export async function GET(_request: Request, { params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) {
    return NextResponse.json({ error: "Passport does not exist" }, { status: 404 });
  }
  await requirePassportDetailAccess(passport);
  return NextResponse.json({ passport });
}

export async function PUT(request: Request, { params }: { params: Promise<{ passportId: string }> }) {
  await requireRole("admin");
  const { passportId } = await params;
  const passport = (await request.json()) as BatteryPassport;
  if (passport.passportId !== decodeURIComponent(passportId)) {
    return NextResponse.json({ error: "passportId mismatch" }, { status: 400 });
  }
  const saved = await upsertPassport(passport);
  return NextResponse.json({ passport: saved });
}

export async function DELETE(_request: Request, { params }: { params: Promise<{ passportId: string }> }) {
  await requireRole("admin");
  const { passportId } = await params;
  await archivePassport(decodeURIComponent(passportId));
  return NextResponse.json({ archived: true });
}
