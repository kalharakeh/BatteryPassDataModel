import { MongoClient } from "mongodb";

const globalForMongo = globalThis as typeof globalThis & {
  mongoClientPromise?: Promise<MongoClient>;
};

export function getMongoClientPromise() {
  const uri = process.env.MONGODB_URI;
  if (!uri) {
    throw new Error("MONGODB_URI is required");
  }

  const serverSelectionTimeoutMS = Number(process.env.MONGODB_SERVER_SELECTION_TIMEOUT_MS);
  const clientOptions =
    Number.isFinite(serverSelectionTimeoutMS) && serverSelectionTimeoutMS > 0 ? { serverSelectionTimeoutMS } : undefined;
  const promise = globalForMongo.mongoClientPromise ?? new MongoClient(uri, clientOptions).connect();
  if (process.env.NODE_ENV !== "production") {
    globalForMongo.mongoClientPromise = promise;
  }

  return promise;
}

export async function closeMongoClient() {
  const client = await globalForMongo.mongoClientPromise?.catch(() => null);
  globalForMongo.mongoClientPromise = undefined;
  await client?.close();
}
