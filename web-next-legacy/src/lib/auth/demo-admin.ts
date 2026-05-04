import { isMongoUnavailable } from "../db/mongo-errors";

const demoRoles = ["admin", "viewer", "issuer", "verifier"];

export async function authenticateDemoAdminOnMongoUnavailable(error: unknown, email: string, password: string) {
  if (process.env.NODE_ENV === "production" || !isMongoUnavailable(error)) {
    throw error;
  }

  const demoEmail = process.env.DEMO_ADMIN_EMAIL ?? "admin@example.test";
  const demoPassword = process.env.DEMO_ADMIN_PASSWORD ?? "Password123!";

  if (email !== demoEmail || password !== demoPassword) {
    return null;
  }

  return {
    email: demoEmail,
    name: "Demo Admin",
    roles: demoRoles,
  };
}
