import bcrypt from "bcryptjs";
import { passportCollection, userCollection } from "../db/collections";
import { loadAppEnv } from "../env/load-app-env";
import { samplePassport } from "./sample-passport";

loadAppEnv();

async function seed() {
  const passports = await passportCollection();
  const users = await userCollection();

  await passports.createIndex({ passportId: 1 }, { unique: true });
  await passports.createIndex({ "registryInfo.registryId": 1 }, { unique: true });
  await passports.createIndex({ passportId: "text", "registryInfo.registryId": "text" });

  await passports.updateOne({ passportId: samplePassport.passportId }, { $set: samplePassport }, { upsert: true });

  const password = process.env.DEMO_ADMIN_PASSWORD ?? "Password123!";
  const passwordHash = await bcrypt.hash(password, 10);
  const email = process.env.DEMO_ADMIN_EMAIL ?? "admin@example.test";

  await users.updateOne(
    { email },
    {
      $set: {
        email,
        passwordHash,
        name: "Demo Admin",
        roles: ["admin", "viewer", "issuer", "verifier"],
      },
    },
    { upsert: true },
  );

  console.log(`Seeded passport ${samplePassport.passportId}`);
  console.log(`Seeded admin user ${email}`);
}

seed()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });
