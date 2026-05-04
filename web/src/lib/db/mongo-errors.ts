export function isMongoUnavailable(error: unknown) {
  return error instanceof Error && (error.name === "MongoServerSelectionError" || error.message.includes("tlsv1 alert internal error"));
}
