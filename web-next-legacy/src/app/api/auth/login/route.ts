import { NextResponse } from "next/server";
import { loginRedirectDestination } from "@/lib/auth/login-redirect";
import { createSession } from "@/lib/auth/session";
import { authenticateUser } from "@/lib/auth/users";

export async function POST(request: Request) {
  const formData = await request.formData();
  const email = String(formData.get("email") ?? "");
  const password = String(formData.get("password") ?? "");
  const user = await authenticateUser(email, password);

  if (!user) {
    const failedLoginUrl = new URL("/login?error=invalid", request.url);
    const nextPath = new URL(request.url).searchParams.get("next") ?? String(formData.get("next") ?? "");
    if (nextPath) {
      failedLoginUrl.searchParams.set("next", nextPath);
    }
    return NextResponse.redirect(failedLoginUrl, { status: 303 });
  }

  await createSession(user.email, user.roles);
  const nextPath = new URL(request.url).searchParams.get("next") ?? String(formData.get("next") ?? "");
  const destination = loginRedirectDestination(user, nextPath);
  return NextResponse.redirect(new URL(destination, request.url), { status: 303 });
}
