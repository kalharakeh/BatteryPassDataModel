import type { Filter } from "mongodb";
import { passportCollection } from "./collections";
import type { BatteryPassport } from "../../types/passport";

export async function listPassports(query = "") {
  const collection = await passportCollection();
  const filter: Filter<BatteryPassport> = query
    ? {
        $or: [
          { passportId: { $regex: query, $options: "i" } },
          { "registryInfo.registryId": { $regex: query, $options: "i" } },
        ],
      }
    : {};

  return collection
    .find(filter, { projection: { _id: 0 } })
    .sort({ "registryInfo.updatedAt": -1 })
    .limit(50)
    .toArray() as Promise<BatteryPassport[]>;
}

export async function getPassport(passportId: string) {
  const collection = await passportCollection();
  return collection.findOne({ passportId }, { projection: { _id: 0 } }) as Promise<BatteryPassport | null>;
}

export async function upsertPassport(passport: BatteryPassport) {
  const collection = await passportCollection();
  await collection.updateOne({ passportId: passport.passportId }, { $set: passport }, { upsert: true });
  return passport;
}

export async function archivePassport(passportId: string) {
  const collection = await passportCollection();
  await collection.updateOne(
    { passportId },
    { $set: { "registryInfo.status": "archived", "registryInfo.updatedAt": new Date().toISOString() } },
  );
}
