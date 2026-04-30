import { NextResponse } from "next/server";
import { createSession } from "@/lib/auth/session";
import { authenticateUser } from "@/lib/auth/users";

export async function POST(request: Request) {
  const formData = await request.formData();
  const email = String(formData.get("email") ?? "");
  const password = String(formData.get("password") ?? "");
  const user = await authenticateUser(email, password);

  if (!user) {
    return NextResponse.redirect(new URL("/login?error=invalid", request.url), { status: 303 });
  }

  await createSession(user.email, user.roles);
  return NextResponse.redirect(new URL("/admin", request.url), { status: 303 });
}
