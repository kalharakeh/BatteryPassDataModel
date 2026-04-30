import type { Collection } from "mongodb";
import { getMongoClientPromise } from "./client";
import type { BatteryPassport } from "../../types/passport";

export type DemoUser = {
  email: string;
  passwordHash: string;
  name: string;
  roles: string[];
};

export async function getDatabase() {
  const client = await getMongoClientPromise();
  return client.db(process.env.MONGODB_DB ?? "battery_pass_demo");
}

export async function passportCollection(): Promise<Collection<BatteryPassport>> {
  return (await getDatabase()).collection<BatteryPassport>("passports");
}

export async function userCollection(): Promise<Collection<DemoUser>> {
  return (await getDatabase()).collection<DemoUser>("users");
}
