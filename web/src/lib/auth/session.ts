import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import { createHmac, timingSafeEqual } from "node:crypto";

const cookieName = "battery-pass-demo-session";

function secret() {
  const value = process.env.SESSION_SECRET;
  if (!value || value.length < 32) {
    throw new Error("SESSION_SECRET must be at least 32 characters");
  }
  return value;
}

function signValue(value: string) {
  return createHmac("sha256", secret()).update(value).digest("base64url");
}

export async function createSession(email: string, roles: string[]) {
  const payload = Buffer.from(JSON.stringify({ email, roles, iat: Date.now() }), "utf8").toString("base64url");
  const signature = signValue(payload);
  const cookieStore = await cookies();
  cookieStore.set(cookieName, `${payload}.${signature}`, { httpOnly: true, sameSite: "lax", path: "/" });
}

export async function clearSession() {
  const cookieStore = await cookies();
  cookieStore.delete(cookieName);
}

export async function getSession() {
  const cookieStore = await cookies();
  const raw = cookieStore.get(cookieName)?.value;
  if (!raw) return null;
  const [payload, signature] = raw.split(".");
  if (!payload || !signature) return null;
  const expected = signValue(payload);
  if (Buffer.byteLength(signature) !== Buffer.byteLength(expected)) return null;
  if (!timingSafeEqual(Buffer.from(signature), Buffer.from(expected))) return null;
  return JSON.parse(Buffer.from(payload, "base64url").toString("utf8")) as { email: string; roles: string[]; iat: number };
}

export async function requireRole(role: string) {
  const session = await getSession();
  if (!session?.roles.includes(role)) {
    redirect("/login");
  }
  return session;
}
