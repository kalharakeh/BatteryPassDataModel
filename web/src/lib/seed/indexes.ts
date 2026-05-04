import type { Collection, Db, Document } from "mongodb";

export const passportSearchTextIndexName = "passport_registry_search_text";

type IndexableCollection = Pick<Collection<Document>, "createIndex" | "dropIndex" | "indexes">;

function isTextIndex(index: Document) {
  const key = index.key as Record<string, unknown> | undefined;
  return key?._fts === "text";
}

export async function ensurePassportIndexes(collection: IndexableCollection) {
  await collection.createIndex({ passportId: 1 }, { unique: true });
  await collection.createIndex({ "registryInfo.registryId": 1 }, { unique: true });
  await collection.createIndex({ clusterId: 1 });

  const existingIndexes = await collection.indexes();
  const legacyTextIndexes = existingIndexes.filter((index) => isTextIndex(index) && index.name !== passportSearchTextIndexName);
  for (const index of legacyTextIndexes) {
    if (typeof index.name === "string") {
      await collection.dropIndex(index.name);
    }
  }

  await collection.createIndex(
    {
      passportId: "text",
      "registryInfo.registryId": "text",
      "app.display.modelNumber": "text",
      "app.display.serialNumber": "text",
      "app.display.manufacturerName": "text",
    },
    { name: passportSearchTextIndexName },
  );
}

export async function ensureClusterIndexes(db: Pick<Db, "collection">) {
  await db.collection("clusters").createIndex({ clusterId: 1 }, { unique: true });
  await db.collection("clusterMemberships").createIndex({ email: 1, clusterId: 1 }, { unique: true });
  await db.collection("clusterMemberships").createIndex({ clusterId: 1, role: 1 });
}
