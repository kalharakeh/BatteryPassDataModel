import { MongoClient } from "mongodb";

const globalForMongo = globalThis as typeof globalThis & {
  mongoClientPromise?: Promise<MongoClient>;
};

export function getMongoClientPromise() {
  const uri = process.env.MONGODB_URI;
  if (!uri) {
    throw new Error("MONGODB_URI is required");
  }

  const promise = globalForMongo.mongoClientPromise ?? new MongoClient(uri).connect();
  if (process.env.NODE_ENV !== "production") {
    globalForMongo.mongoClientPromise = promise;
  }

  return promise;
}
