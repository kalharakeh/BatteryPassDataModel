import type { Filter } from "mongodb";
import { passportCollection } from "./collections";
import type { BatteryPassport } from "../../types/passport";

type ListPassportsOptions = {
  includeArchived?: boolean;
};

function publicStatusFilter(includeArchived = false): Filter<BatteryPassport> {
  return includeArchived ? {} : { "registryInfo.status": { $ne: "archived" } };
}

export async function listPassports(query = "", options: ListPassportsOptions = {}) {
  const collection = await passportCollection();
  const searchFilter: Filter<BatteryPassport> = query
    ? {
        $or: [
          { passportId: { $regex: query, $options: "i" } },
          { "registryInfo.registryId": { $regex: query, $options: "i" } },
          { "app.display.modelNumber": { $regex: query, $options: "i" } },
          { "app.display.serialNumber": { $regex: query, $options: "i" } },
          { "app.display.manufacturerName": { $regex: query, $options: "i" } },
          { clusterId: { $regex: query, $options: "i" } },
          { "aspects.generalProductInformation.payload.productIdentifier": { $regex: query, $options: "i" } },
          { "aspects.generalProductInformation.payload.batteryPassportIdentifier": { $regex: query, $options: "i" } },
        ],
      }
    : {};
  const filter: Filter<BatteryPassport> = { $and: [publicStatusFilter(options.includeArchived), searchFilter].filter(Boolean) };

  return collection
    .find(filter, { projection: { _id: 0 } })
    .sort({ "registryInfo.updatedAt": -1 })
    .limit(50)
    .toArray() as Promise<BatteryPassport[]>;
}

export async function getPassport(passportId: string) {
  const collection = await passportCollection();
  return collection.findOne({ passportId, "registryInfo.status": { $ne: "archived" } }, { projection: { _id: 0 } }) as Promise<BatteryPassport | null>;
}

export async function getPassportForAdmin(passportId: string) {
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

export async function updatePassportCluster(passportId: string, clusterId: string) {
  const collection = await passportCollection();
  await collection.updateOne({ passportId }, { $set: { clusterId, "registryInfo.updatedAt": new Date().toISOString() } });
}

export async function clearPassportCluster(clusterId: string) {
  const collection = await passportCollection();
  await collection.updateMany(
    { clusterId },
    { $unset: { clusterId: "" }, $set: { "registryInfo.updatedAt": new Date().toISOString() } },
  );
}
