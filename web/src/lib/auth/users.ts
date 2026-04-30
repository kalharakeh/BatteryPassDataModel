import bcrypt from "bcryptjs";
import { userCollection } from "../db/collections";

export async function authenticateUser(email: string, password: string) {
  const users = await userCollection();
  const user = await users.findOne({ email });
  if (!user) return null;
  const valid = await bcrypt.compare(password, user.passwordHash);
  if (!valid) return null;
  return { email: user.email, name: user.name, roles: user.roles };
}
