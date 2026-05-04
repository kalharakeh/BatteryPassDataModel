import bcrypt from "bcryptjs";
import { userCollection } from "../db/collections";
import { authenticateDemoAdminOnMongoUnavailable } from "./demo-admin";

export async function authenticateUser(email: string, password: string) {
  try {
    const users = await userCollection();
    const user = await users.findOne({ email });
    if (!user) return null;
    const valid = await bcrypt.compare(password, user.passwordHash);
    if (!valid) return null;
    return { email: user.email, name: user.name, roles: user.roles };
  } catch (error) {
    return authenticateDemoAdminOnMongoUnavailable(error, email, password);
  }
}
