# Battery Pass Demonstrator Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working vertical slice of the Battery Pass demonstrator clone: a Next.js app under `web/` with MongoDB Atlas connectivity, schema-aligned passport storage, seeded demo users/passports, demo auth, public viewer pages, and an admin section editor shell.

**Architecture:** Keep the existing repository as the canonical data-model source and add a new `web/` Next.js TypeScript app beside it. The app wraps canonical Battery Pass aspect payloads in an application-level passport document, derives reference-style view models for UI display, and centralizes MongoDB, schema validation, auth, and demo data behind small server-side modules.

**Tech Stack:** Next.js App Router, TypeScript, Tailwind CSS, MongoDB Atlas, MongoDB Node driver, Node test runner, React Testing Library, Playwright, Node `crypto` Ed25519 helpers for the signing foundation.

---

## Scope Split

The approved design covers several subsystems: viewer, registry, admin forms, MongoDB/GridFS, DID/VC signing, QR scanning, auth, validation, and testing. This plan implements the foundation vertical slice that the remaining subsystems depend on:

- Next.js app scaffold
- MongoDB connection and typed collections
- schema registry that references the repo schemas
- deterministic JSON canonicalization and signing primitives
- seeded demo users and a schema-shaped sample passport
- demo login/logout and role guard
- public landing, registry, overview, and summary pages
- admin passport list and section editor shell

Separate implementation plans should extend this foundation for GridFS uploads, full DID/VC proof flows, QR image/live camera scanning, and exhaustive schema-driven form coverage.

## File Structure

Create the app in `web/` so the model repository remains clean.

```text
web/
  .env.example
  package.json
  next.config.ts
  tsconfig.json
  src/
    app/
      globals.css
      layout.tsx
      page.tsx
      registry/page.tsx
      [passportId]/page.tsx
      [passportId]/summary/page.tsx
      admin/page.tsx
      admin/passports/page.tsx
      admin/passports/[passportId]/edit/page.tsx
      login/page.tsx
      api/auth/login/route.ts
      api/auth/logout/route.ts
      api/passports/route.ts
      api/passports/[passportId]/route.ts
    components/
      app-shell.tsx
      auth/login-form.tsx
      passport/passport-card.tsx
      passport/section-nav.tsx
      passport/verification-badge.tsx
      registry/registry-table.tsx
      admin/section-editor.tsx
      theme/theme-provider.tsx
      theme/theme-toggle.tsx
      ui/button.tsx
      ui/card.tsx
      ui/input.tsx
      ui/status-pill.tsx
    lib/
      auth/session.ts
      auth/users.ts
      crypto/canonicalize.ts
      crypto/signing.ts
      db/client.ts
      db/collections.ts
      db/passports.ts
      schemas/registry.ts
      seed/sample-passport.ts
      seed/seed.ts
      validation/validate-aspect.ts
      view-model/passport-view-model.ts
    types/
      passport.ts
  tests/
    canonicalize.test.ts
    signing.test.ts
    schema-registry.test.ts
    passport-view-model.test.ts
```

## Task 1: Scaffold The Next.js App

**Files:**
- Create: `web/`
- Create: `web/package.json`
- Create: `web/.env.example`
- Create: `web/src/app/globals.css`
- Modify: `web/src/app/layout.tsx`
- Create: `web/src/app/page.tsx`

- [ ] **Step 1: Create the app scaffold**

Run:

```powershell
npx create-next-app@latest web --ts --eslint --tailwind --app --src-dir --import-alias "@/*" --use-npm
```

Expected:

```text
Success! Created web
```

- [ ] **Step 2: Install runtime and test dependencies**

Run:

```powershell
Set-Location web
npm install mongodb bcryptjs lucide-react
npm install -D @types/bcryptjs @testing-library/react @testing-library/jest-dom @playwright/test
Set-Location ..
```

Expected: `package-lock.json` updates and no installation errors.

- [ ] **Step 3: Replace `web/.env.example`**

```text
MONGODB_URI=replace-with-your-mongodb-atlas-connection-string
MONGODB_DB=battery_pass_demo
APP_URL=http://localhost:3000
SESSION_SECRET=replace-with-at-least-32-random-characters
DEMO_ADMIN_EMAIL=admin@example.test
DEMO_ADMIN_PASSWORD=Password123!
DID_ISSUER_DID=did:web:local.battery.pass:issuer
DID_ISSUER_PRIVATE_KEY=
```

- [ ] **Step 4: Replace `web/src/app/globals.css`**

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

:root {
  color-scheme: light;
  --background: 248 250 252;
  --foreground: 15 23 42;
  --muted: 100 116 139;
  --panel: 255 255 255;
  --border: 226 232 240;
  --accent: 5 150 105;
}

.dark {
  color-scheme: dark;
  --background: 15 23 42;
  --foreground: 241 245 249;
  --muted: 148 163 184;
  --panel: 30 41 59;
  --border: 51 65 85;
  --accent: 16 185 129;
}

body {
  min-height: 100vh;
  background: rgb(var(--background));
  color: rgb(var(--foreground));
}

a {
  color: inherit;
}
```

- [ ] **Step 5: Replace `web/src/app/layout.tsx` with a minimal layout**

```tsx
import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Battery Passport - Viewer",
  description: "Internal Battery Pass demonstrator clone",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
```

- [ ] **Step 6: Replace `web/src/app/page.tsx`**

```tsx
import Link from "next/link";
import { Search } from "lucide-react";

export default function LandingPage() {
  return (
    <main className="mx-auto flex min-h-[70vh] max-w-3xl flex-col justify-center px-4 py-12">
      <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-semibold tracking-normal">Battery Passport Viewer</h1>
          <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">
            Enter a battery passport DID to open the public demonstrator view.
          </p>
        </div>
        <form action="/registry" className="flex flex-col gap-3 sm:flex-row">
          <input
            name="q"
            className="min-h-11 flex-1 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-900"
            placeholder="did:web:local.battery.pass:sample"
          />
          <button className="inline-flex min-h-11 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800">
            <Search className="h-4 w-4" />
            Search
          </button>
        </form>
        <div className="flex gap-3 text-sm">
          <Link className="text-emerald-700 dark:text-emerald-300" href="/registry">
            Open registry
          </Link>
          <Link className="text-emerald-700 dark:text-emerald-300" href="/login">
            Admin login
          </Link>
        </div>
      </div>
    </main>
  );
}
```

- [ ] **Step 7: Run lint**

Run:

```powershell
Set-Location web
npm run lint
Set-Location ..
```

Expected: lint passes.

- [ ] **Step 8: Commit**

```powershell
git add web
git commit -m "feat: scaffold Battery Pass web app"
```

## Task 2: Add Theme And Shell Components

**Files:**
- Create: `web/src/components/app-shell.tsx`
- Create: `web/src/components/theme/theme-provider.tsx`
- Create: `web/src/components/theme/theme-toggle.tsx`
- Create: `web/src/components/ui/button.tsx`
- Create: `web/src/components/ui/card.tsx`
- Create: `web/src/components/ui/input.tsx`
- Create: `web/src/components/ui/status-pill.tsx`
- Modify: `web/src/app/layout.tsx`

- [ ] **Step 1: Create `web/src/components/theme/theme-provider.tsx`**

```tsx
"use client";

import { createContext, useContext, useEffect, useMemo, useState } from "react";

type Theme = "light" | "dark";

type ThemeContextValue = {
  theme: Theme;
  setTheme: (theme: Theme) => void;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = useState<Theme>("light");

  useEffect(() => {
    const saved = window.localStorage.getItem("theme") as Theme | null;
    const initial = saved === "dark" || saved === "light" ? saved : "light";
    setThemeState(initial);
    document.documentElement.classList.toggle("dark", initial === "dark");
  }, []);

  const setTheme = (nextTheme: Theme) => {
    setThemeState(nextTheme);
    window.localStorage.setItem("theme", nextTheme);
    document.documentElement.classList.toggle("dark", nextTheme === "dark");
  };

  const value = useMemo(() => ({ theme, setTheme }), [theme]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error("useTheme must be used within ThemeProvider");
  }
  return context;
}
```

- [ ] **Step 2: Create `web/src/components/theme/theme-toggle.tsx`**

```tsx
"use client";

import { Moon, Sun } from "lucide-react";
import { useTheme } from "./theme-provider";

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const isDark = theme === "dark";

  return (
    <button
      type="button"
      aria-label="Toggle theme"
      className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-slate-300 bg-white text-slate-800 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:hover:bg-slate-800"
      onClick={() => setTheme(isDark ? "light" : "dark")}
    >
      {isDark ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
    </button>
  );
}
```

- [ ] **Step 3: Create `web/src/components/app-shell.tsx`**

```tsx
import Link from "next/link";
import { BatteryCharging } from "lucide-react";
import { ThemeToggle } from "@/components/theme/theme-toggle";

export function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white/90 backdrop-blur dark:border-slate-800 dark:bg-slate-950/90">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
          <Link href="/" className="flex items-center gap-2 font-semibold">
            <BatteryCharging className="h-6 w-6 text-emerald-700 dark:text-emerald-300" />
            <span>Battery Passport</span>
          </Link>
          <nav className="flex items-center gap-4 text-sm">
            <Link href="/registry" className="text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300">
              Registry
            </Link>
            <Link href="/admin" className="text-slate-700 hover:text-emerald-700 dark:text-slate-200 dark:hover:text-emerald-300">
              Admin
            </Link>
            <ThemeToggle />
          </nav>
        </div>
      </header>
      {children}
      <footer className="border-t border-slate-200 px-4 py-6 text-center text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
        Internal demonstrator. Sample values are for demonstration purposes only.
      </footer>
    </div>
  );
}
```

- [ ] **Step 4: Create `web/src/components/ui/button.tsx`**

```tsx
import type { ButtonHTMLAttributes } from "react";

export function Button({ className = "", ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={`inline-flex min-h-10 items-center justify-center gap-2 rounded-md bg-emerald-700 px-4 text-sm font-medium text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:opacity-60 ${className}`}
      {...props}
    />
  );
}
```

- [ ] **Step 5: Create `web/src/components/ui/card.tsx`**

```tsx
export function Card({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <section className={`rounded-lg border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900 ${className}`}>
      {children}
    </section>
  );
}
```

- [ ] **Step 6: Create `web/src/components/ui/input.tsx`**

```tsx
import type { InputHTMLAttributes } from "react";

export function Input({ className = "", ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`min-h-10 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none focus:border-emerald-600 dark:border-slate-700 dark:bg-slate-950 ${className}`}
      {...props}
    />
  );
}
```

- [ ] **Step 7: Create `web/src/components/ui/status-pill.tsx`**

```tsx
const styles = {
  verified: "bg-emerald-700 text-white dark:bg-emerald-800",
  partial: "bg-amber-500 text-white dark:bg-amber-600",
  unverified: "bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-100",
  invalid: "bg-red-600 text-white dark:bg-red-700",
};

export function StatusPill({ status, label }: { status: keyof typeof styles; label: string }) {
  return <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[status]}`}>{label}</span>;
}
```

- [ ] **Step 8: Run lint and commit**

Before linting, replace `web/src/app/layout.tsx` so the shell and theme wrap every page:

```tsx
import type { Metadata } from "next";
import "./globals.css";
import { AppShell } from "@/components/app-shell";
import { ThemeProvider } from "@/components/theme/theme-provider";

export const metadata: Metadata = {
  title: "Battery Passport - Viewer",
  description: "Internal Battery Pass demonstrator clone",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <ThemeProvider>
          <AppShell>{children}</AppShell>
        </ThemeProvider>
      </body>
    </html>
  );
}
```

Run:

```powershell
Set-Location web
npm run lint
Set-Location ..
git add web/src/components web/src/app
git commit -m "feat: add Battery Pass shell and theme"
```

Expected: lint passes and commit succeeds.

## Task 3: Define Domain Types And Schema Registry

**Files:**
- Create: `web/src/types/passport.ts`
- Create: `web/src/lib/schemas/registry.ts`
- Create: `web/tests/schema-registry.test.ts`
- Modify: `web/package.json`

- [ ] **Step 1: Add a Node test script to `web/package.json`**

Modify the `scripts` object to include:

```json
{
  "test": "node --test --import tsx"
}
```

Install `tsx`:

```powershell
Set-Location web
npm install -D tsx
Set-Location ..
```

- [ ] **Step 2: Create `web/src/types/passport.ts`**

```ts
export const aspectKeys = [
  "generalProductInformation",
  "carbonFootprintForBatteries",
  "circularity",
  "materialComposition",
  "performanceAndDurability",
  "labeling",
  "supplyChainDueDiligence",
] as const;

export type AspectKey = (typeof aspectKeys)[number];

export type Visibility = "public" | "privileged";
export type RegistryStatus = "draft" | "published" | "archived";
export type VerificationState = "verified" | "partial" | "unverified" | "invalid";

export type AspectRecord = {
  payload: Record<string, unknown>;
  visibility: Visibility;
  verification: {
    state: VerificationState;
    issuer?: string;
    signedAt?: string;
    hash?: string;
    signature?: string;
  };
};

export type BatteryPassport = {
  passportId: string;
  schemaVersions: Record<AspectKey, string>;
  registryInfo: {
    registryId: string;
    status: RegistryStatus;
    createdAt: string;
    updatedAt: string;
    revisionPassportId: string;
    latestPassportRevision: string;
  };
  aspects: Partial<Record<AspectKey, AspectRecord>>;
  hiddenProperties: string[];
  instanceOnlyFields: string[];
  validation: {
    hash: string;
    signature: string;
    proof: Record<string, unknown>;
    isValid: boolean;
    signedAt: string | null;
  };
  dataSource: {
    name: string;
    instanceUrl: string;
  };
};
```

- [ ] **Step 3: Create `web/src/lib/schemas/registry.ts`**

```ts
import path from "node:path";
import type { AspectKey } from "@/types/passport";

export type AspectSchemaInfo = {
  key: AspectKey;
  title: string;
  version: string;
  schemaPath: string;
};

const repoRoot = path.resolve(process.cwd(), "..");

export const aspectSchemas: Record<AspectKey, AspectSchemaInfo> = {
  generalProductInformation: {
    key: "generalProductInformation",
    title: "General Product Information",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.GeneralProductInformation/1.2.0/gen/GeneralProductInformation-schema.json"),
  },
  carbonFootprintForBatteries: {
    key: "carbonFootprintForBatteries",
    title: "Carbon Footprint",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.CarbonFootprint/1.2.0/gen/CarbonFootprintForBatteries-schema.json"),
  },
  circularity: {
    key: "circularity",
    title: "Circularity",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.Circularity/1.2.0/gen/Circularity-schema.json"),
  },
  materialComposition: {
    key: "materialComposition",
    title: "Material Composition",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.MaterialComposition/1.2.0/gen/MaterialComposition-schema.json"),
  },
  performanceAndDurability: {
    key: "performanceAndDurability",
    title: "Performance and Durability",
    version: "1.2.1",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.Performance/1.2.1/gen/PerformanceAndDurability.schema"),
  },
  labeling: {
    key: "labeling",
    title: "Labels and Certification",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.Labels/1.2.0/gen/Labeling-schema.json"),
  },
  supplyChainDueDiligence: {
    key: "supplyChainDueDiligence",
    title: "Supply Chain Due Diligence",
    version: "1.2.0",
    schemaPath: path.join(repoRoot, "BatteryPass/io.BatteryPass.SupplyChainDueDiligence/1.2.0/gen/SupplyChainDueDiligence-schema.json"),
  },
};

export function getAspectSchemaInfo(key: AspectKey) {
  return aspectSchemas[key];
}
```

- [ ] **Step 4: Create `web/tests/schema-registry.test.ts`**

```ts
import assert from "node:assert/strict";
import fs from "node:fs";
import { describe, it } from "node:test";
import { aspectSchemas } from "../src/lib/schemas/registry";

describe("aspectSchemas", () => {
  it("points every canonical aspect at an existing repo schema", () => {
    for (const schema of Object.values(aspectSchemas)) {
      assert.equal(fs.existsSync(schema.schemaPath), true, `${schema.key} schema missing at ${schema.schemaPath}`);
      assert.match(schema.version, /^1\.2\.[01]$/);
    }
  });
});
```

- [ ] **Step 5: Run test and commit**

Run:

```powershell
Set-Location web
npm test -- tests/schema-registry.test.ts
Set-Location ..
git add web
git commit -m "feat: register Battery Pass schemas"
```

Expected: test passes.

## Task 4: Add Canonicalization And Signing Primitives

**Files:**
- Create: `web/src/lib/crypto/canonicalize.ts`
- Create: `web/src/lib/crypto/signing.ts`
- Create: `web/tests/canonicalize.test.ts`
- Create: `web/tests/signing.test.ts`

- [ ] **Step 1: Create `web/src/lib/crypto/canonicalize.ts`**

```ts
export function canonicalizeJson(value: unknown): string {
  if (value === null || typeof value !== "object") {
    if (typeof value === "number" && !Number.isFinite(value)) {
      throw new Error("Cannot canonicalize non-finite numbers");
    }
    return JSON.stringify(value);
  }

  if (Array.isArray(value)) {
    return `[${value.map((item) => canonicalizeJson(item)).join(",")}]`;
  }

  const entries = Object.entries(value as Record<string, unknown>)
    .filter(([, entryValue]) => entryValue !== undefined)
    .sort(([left], [right]) => left.localeCompare(right));

  return `{${entries
    .map(([key, entryValue]) => `${JSON.stringify(key)}:${canonicalizeJson(entryValue)}`)
    .join(",")}}`;
}
```

- [ ] **Step 2: Create `web/src/lib/crypto/signing.ts`**

```ts
import { createHash, generateKeyPairSync, sign, verify } from "node:crypto";
import { canonicalizeJson } from "./canonicalize";

export type SigningKeyPair = {
  publicKeyPem: string;
  privateKeyPem: string;
};

export function generateEd25519KeyPair(): SigningKeyPair {
  const { publicKey, privateKey } = generateKeyPairSync("ed25519");
  return {
    publicKeyPem: publicKey.export({ type: "spki", format: "pem" }).toString(),
    privateKeyPem: privateKey.export({ type: "pkcs8", format: "pem" }).toString(),
  };
}

export function hashPayload(payload: unknown): string {
  return createHash("sha256").update(canonicalizeJson(payload)).digest("base64url");
}

export function signPayload(payload: unknown, privateKeyPem: string): string {
  return sign(null, Buffer.from(canonicalizeJson(payload)), privateKeyPem).toString("base64url");
}

export function verifyPayloadSignature(payload: unknown, signature: string, publicKeyPem: string): boolean {
  return verify(null, Buffer.from(canonicalizeJson(payload)), publicKeyPem, Buffer.from(signature, "base64url"));
}
```

- [ ] **Step 3: Create `web/tests/canonicalize.test.ts`**

```ts
import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { canonicalizeJson } from "../src/lib/crypto/canonicalize";

describe("canonicalizeJson", () => {
  it("sorts object keys recursively", () => {
    const left = { b: 1, a: { d: true, c: ["x", { z: 2, y: 1 }] } };
    const right = { a: { c: ["x", { y: 1, z: 2 }], d: true }, b: 1 };

    assert.equal(canonicalizeJson(left), canonicalizeJson(right));
    assert.equal(canonicalizeJson(left), '{"a":{"c":["x",{"y":1,"z":2}],"d":true},"b":1}');
  });

  it("rejects non-finite numbers", () => {
    assert.throws(() => canonicalizeJson({ value: Number.NaN }), /non-finite/);
  });
});
```

- [ ] **Step 4: Create `web/tests/signing.test.ts`**

```ts
import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { generateEd25519KeyPair, hashPayload, signPayload, verifyPayloadSignature } from "../src/lib/crypto/signing";

describe("signing", () => {
  it("signs and verifies canonicalized payloads", () => {
    const keys = generateEd25519KeyPair();
    const payload = { passportId: "did:web:local.battery.pass:sample", aspect: { b: 2, a: 1 } };

    const signature = signPayload(payload, keys.privateKeyPem);

    assert.equal(hashPayload(payload).length > 20, true);
    assert.equal(verifyPayloadSignature({ passportId: "did:web:local.battery.pass:sample", aspect: { a: 1, b: 2 } }, signature, keys.publicKeyPem), true);
    assert.equal(verifyPayloadSignature({ passportId: "did:web:local.battery.pass:sample", aspect: { a: 1, b: 3 } }, signature, keys.publicKeyPem), false);
  });
});
```

- [ ] **Step 5: Run tests and commit**

```powershell
Set-Location web
npm test -- tests/canonicalize.test.ts tests/signing.test.ts
Set-Location ..
git add web/src/lib/crypto web/tests
git commit -m "feat: add passport signing primitives"
```

Expected: both tests pass.

## Task 5: Add MongoDB Client And Passport Repository

**Files:**
- Create: `web/src/lib/db/client.ts`
- Create: `web/src/lib/db/collections.ts`
- Create: `web/src/lib/db/passports.ts`
- Create: `web/src/lib/seed/sample-passport.ts`

- [ ] **Step 1: Create `web/src/lib/db/client.ts`**

```ts
import { MongoClient } from "mongodb";

const uri = process.env.MONGODB_URI;

if (!uri) {
  throw new Error("MONGODB_URI is required");
}

const globalForMongo = globalThis as typeof globalThis & {
  mongoClientPromise?: Promise<MongoClient>;
};

export const mongoClientPromise =
  globalForMongo.mongoClientPromise ??
  new MongoClient(uri).connect();

if (process.env.NODE_ENV !== "production") {
  globalForMongo.mongoClientPromise = mongoClientPromise;
}
```

- [ ] **Step 2: Create `web/src/lib/db/collections.ts`**

```ts
import type { Collection } from "mongodb";
import { mongoClientPromise } from "./client";
import type { BatteryPassport } from "@/types/passport";

export type DemoUser = {
  email: string;
  passwordHash: string;
  name: string;
  roles: string[];
};

export async function getDatabase() {
  const client = await mongoClientPromise;
  return client.db(process.env.MONGODB_DB ?? "battery_pass_demo");
}

export async function passportCollection(): Promise<Collection<BatteryPassport>> {
  return (await getDatabase()).collection<BatteryPassport>("passports");
}

export async function userCollection(): Promise<Collection<DemoUser>> {
  return (await getDatabase()).collection<DemoUser>("users");
}
```

- [ ] **Step 3: Create `web/src/lib/db/passports.ts`**

```ts
import { passportCollection } from "./collections";
import type { BatteryPassport } from "@/types/passport";

export async function listPassports(query = "") {
  const collection = await passportCollection();
  const filter = query
    ? {
        $or: [
          { passportId: { $regex: query, $options: "i" } },
          { "registryInfo.registryId": { $regex: query, $options: "i" } },
        ],
      }
    : {};

  return collection.find(filter).sort({ "registryInfo.updatedAt": -1 }).limit(50).toArray();
}

export async function getPassport(passportId: string) {
  const collection = await passportCollection();
  return collection.findOne({ passportId });
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
```

- [ ] **Step 4: Create `web/src/lib/seed/sample-passport.ts`**

```ts
import type { BatteryPassport } from "@/types/passport";

const now = "2024-09-05T08:03:42.000Z";

export const samplePassport: BatteryPassport = {
  passportId: "did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976",
  schemaVersions: {
    generalProductInformation: "1.2.0",
    carbonFootprintForBatteries: "1.2.0",
    circularity: "1.2.0",
    materialComposition: "1.2.0",
    performanceAndDurability: "1.2.1",
    labeling: "1.2.0",
    supplyChainDueDiligence: "1.2.0",
  },
  registryInfo: {
    registryId: "886a9b6b-1fa2-434a-ade0-b724e8dd7656",
    status: "published",
    createdAt: now,
    updatedAt: now,
    revisionPassportId: "",
    latestPassportRevision: "",
  },
  aspects: {
    generalProductInformation: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:local.battery.pass:issuer", signedAt: now },
      payload: {
        productIdentifier: "EV-BAT095",
        batteryPassportIdentifier: "urn:local:0226151e949cd0678ef3162431e28976",
        batteryCategory: "ev",
        manufacturerInformation: {
          contactName: "Exide Batteries Auditor",
          identifier: "exide-batteries",
          postalAddress: { addressCountry: "Germany", postalCode: "10724", streetAddress: "ACME Street 1" },
          webAddress: "https://exide-batteries.example",
        },
        manufacturingDate: "2023-09-05T18:58:41.000Z",
        batteryStatus: "Original",
        batteryMass: 499,
        manufacturingPlace: { addressCountry: "Germany", postalCode: "10724", streetAddress: "ACME Street 1" },
        operatorInformation: {
          contactName: "ACME Batteries Auditor",
          identifier: "acme-batteries",
          postalAddress: { addressCountry: "Germany", postalCode: "10724", streetAddress: "ACME Street 1" },
          webAddress: "https://acme-batteries.example",
        },
        puttingIntoService: "2024-01-10T00:00:00.000Z",
        warrentyPeriod: "P8Y",
      },
    },
    carbonFootprintForBatteries: {
      visibility: "public",
      verification: { state: "verified", issuer: "did:web:local.battery.pass:issuer", signedAt: now },
      payload: {
        batteryCarbonFootprint: 137,
        carbonFootprintPerLifecycleStage: [
          { lifecycleStage: "rawMaterialExtraction", carbonFootprint: 89 },
          { lifecycleStage: "mainProduction", carbonFootprint: 30 },
          { lifecycleStage: "distribution", carbonFootprint: 10 },
          { lifecycleStage: "recycling", carbonFootprint: 8 },
        ],
        carbonFootprintPerformanceClass: "B",
        carbonFootprintStudy: "https://exide-batteries.example/studies/90288",
      },
    },
    supplyChainDueDiligence: {
      visibility: "privileged",
      verification: { state: "partial" },
      payload: {
        supplyChainDueDiligenceReport: "https://exide-batteries.example/sdd-report.pdf",
        thirdPartyAussurances: "https://exide-batteries.example/audit-certificate.vc",
        supplyChainIndicies: 82,
      },
    },
  },
  hiddenProperties: ["supplyChainDueDiligence"],
  instanceOnlyFields: ["generalProductInformation.payload.batteryStatus"],
  validation: {
    hash: "",
    signature: "",
    proof: {},
    isValid: false,
    signedAt: null,
  },
  dataSource: {
    name: "MongoDB Atlas",
    instanceUrl: "/api/passport-instances/did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976",
  },
};
```

- [ ] **Step 5: Commit**

```powershell
git add web/src/lib/db web/src/lib/seed/sample-passport.ts
git commit -m "feat: add MongoDB passport repository"
```

## Task 6: Add Seed Script

**Files:**
- Create: `web/src/lib/seed/seed.ts`
- Modify: `web/package.json`

- [ ] **Step 1: Create `web/src/lib/seed/seed.ts`**

```ts
import bcrypt from "bcryptjs";
import { passportCollection, userCollection } from "../db/collections";
import { samplePassport } from "./sample-passport";

async function seed() {
  const passports = await passportCollection();
  const users = await userCollection();

  await passports.createIndex({ passportId: 1 }, { unique: true });
  await passports.createIndex({ "registryInfo.registryId": 1 }, { unique: true });
  await passports.createIndex({ passportId: "text", "registryInfo.registryId": "text" });

  await passports.updateOne({ passportId: samplePassport.passportId }, { $set: samplePassport }, { upsert: true });

  const password = process.env.DEMO_ADMIN_PASSWORD ?? "Password123!";
  const passwordHash = await bcrypt.hash(password, 10);

  await users.updateOne(
    { email: process.env.DEMO_ADMIN_EMAIL ?? "admin@example.test" },
    {
      $set: {
        email: process.env.DEMO_ADMIN_EMAIL ?? "admin@example.test",
        passwordHash,
        name: "Demo Admin",
        roles: ["admin", "viewer", "issuer", "verifier"],
      },
    },
    { upsert: true },
  );

  console.log(`Seeded passport ${samplePassport.passportId}`);
  console.log(`Seeded admin user ${process.env.DEMO_ADMIN_EMAIL ?? "admin@example.test"}`);
}

seed()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  });
```

- [ ] **Step 2: Add seed script to `web/package.json`**

```json
{
  "seed": "tsx src/lib/seed/seed.ts"
}
```

- [ ] **Step 3: Run type/lint checks**

```powershell
Set-Location web
npm run lint
Set-Location ..
```

Expected: lint passes. Do not run `npm run seed` until `.env.local` contains a valid MongoDB Atlas connection string.

- [ ] **Step 4: Commit**

```powershell
git add web/package.json web/package-lock.json web/src/lib/seed
git commit -m "feat: add demo seed script"
```

## Task 7: Add Demo Auth

**Files:**
- Create: `web/src/lib/auth/session.ts`
- Create: `web/src/lib/auth/users.ts`
- Create: `web/src/app/api/auth/login/route.ts`
- Create: `web/src/app/api/auth/logout/route.ts`
- Create: `web/src/app/login/page.tsx`
- Create: `web/src/components/auth/login-form.tsx`

- [ ] **Step 1: Create `web/src/lib/auth/session.ts`**

```ts
import { redirect } from "next/navigation";
import { cookies } from "next/headers";
import { createHmac, timingSafeEqual } from "node:crypto";

const cookieName = "battery-pass-demo-session";

function secret() {
  const value = process.env.SESSION_SECRET;
  if (!value || value.length < 32) {
    throw new Error("SESSION_SECRET must be at least 32 characters");
  }
  return value;
}

function signValue(value: string) {
  return createHmac("sha256", secret()).update(value).digest("base64url");
}

export async function createSession(email: string, roles: string[]) {
  const payload = Buffer.from(JSON.stringify({ email, roles, iat: Date.now() }), "utf8").toString("base64url");
  const signature = signValue(payload);
  const cookieStore = await cookies();
  cookieStore.set(cookieName, `${payload}.${signature}`, { httpOnly: true, sameSite: "lax", path: "/" });
}

export async function clearSession() {
  const cookieStore = await cookies();
  cookieStore.delete(cookieName);
}

export async function getSession() {
  const cookieStore = await cookies();
  const raw = cookieStore.get(cookieName)?.value;
  if (!raw) return null;
  const [payload, signature] = raw.split(".");
  if (!payload || !signature) return null;
  const expected = signValue(payload);
  if (Buffer.byteLength(signature) !== Buffer.byteLength(expected)) return null;
  if (!timingSafeEqual(Buffer.from(signature), Buffer.from(expected))) return null;
  return JSON.parse(Buffer.from(payload, "base64url").toString("utf8")) as { email: string; roles: string[]; iat: number };
}

export async function requireRole(role: string) {
  const session = await getSession();
  if (!session?.roles.includes(role)) {
    redirect("/login");
  }
  return session;
}
```

- [ ] **Step 2: Create `web/src/lib/auth/users.ts`**

```ts
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
```

- [ ] **Step 3: Create `web/src/app/api/auth/login/route.ts`**

```ts
import { NextResponse } from "next/server";
import { createSession } from "@/lib/auth/session";
import { authenticateUser } from "@/lib/auth/users";

export async function POST(request: Request) {
  const formData = await request.formData();
  const email = String(formData.get("email") ?? "");
  const password = String(formData.get("password") ?? "");
  const user = await authenticateUser(email, password);

  if (!user) {
    return NextResponse.redirect(new URL("/login?error=invalid", request.url), { status: 303 });
  }

  await createSession(user.email, user.roles);
  return NextResponse.redirect(new URL("/admin", request.url), { status: 303 });
}
```

- [ ] **Step 4: Create `web/src/app/api/auth/logout/route.ts`**

```ts
import { NextResponse } from "next/server";
import { clearSession } from "@/lib/auth/session";

export async function POST(request: Request) {
  await clearSession();
  return NextResponse.redirect(new URL("/", request.url), { status: 303 });
}
```

- [ ] **Step 5: Create `web/src/components/auth/login-form.tsx`**

```tsx
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function LoginForm({ error }: { error?: string }) {
  return (
    <form action="/api/auth/login" method="post" className="space-y-4">
      {error ? <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-200">Invalid email or password.</p> : null}
      <label className="block text-sm">
        <span className="mb-1 block font-medium">Email / Username</span>
        <Input name="email" type="email" required placeholder="admin@example.test" className="w-full" />
      </label>
      <label className="block text-sm">
        <span className="mb-1 block font-medium">Password</span>
        <Input name="password" type="password" required placeholder="Password123!" className="w-full" />
      </label>
      <Button type="submit" className="w-full">Verify</Button>
    </form>
  );
}
```

- [ ] **Step 6: Create `web/src/app/login/page.tsx`**

```tsx
import { LoginForm } from "@/components/auth/login-form";
import { Card } from "@/components/ui/card";

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const params = await searchParams;
  return (
    <main className="mx-auto flex min-h-[70vh] max-w-md items-center px-4 py-12">
      <Card className="w-full">
        <h1 className="text-xl font-semibold">Authenticate for privileged access</h1>
        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Sign in with demo credentials to view admin and privileged sections.</p>
        <div className="mt-6">
          <LoginForm error={params.error} />
        </div>
      </Card>
    </main>
  );
}
```

- [ ] **Step 7: Run lint and commit**

```powershell
Set-Location web
npm run lint
Set-Location ..
git add web/src/lib/auth web/src/app/api/auth web/src/app/login web/src/components/auth
git commit -m "feat: add demo authentication"
```

## Task 8: Add Passport View Model And Public Pages

**Files:**
- Create: `web/src/lib/view-model/passport-view-model.ts`
- Create: `web/tests/passport-view-model.test.ts`
- Create: `web/src/components/passport/verification-badge.tsx`
- Create: `web/src/components/passport/passport-card.tsx`
- Create: `web/src/components/passport/section-nav.tsx`
- Create: `web/src/components/registry/registry-table.tsx`
- Create: `web/src/app/registry/page.tsx`
- Create: `web/src/app/[passportId]/page.tsx`
- Create: `web/src/app/[passportId]/summary/page.tsx`

- [ ] **Step 1: Create `web/src/lib/view-model/passport-view-model.ts`**

```ts
import type { BatteryPassport } from "@/types/passport";

function valueAt(payload: Record<string, unknown>, key: string) {
  return payload[key] == null ? "" : String(payload[key]);
}

export function toPassportViewModel(passport: BatteryPassport) {
  const general = passport.aspects.generalProductInformation?.payload ?? {};
  const carbon = passport.aspects.carbonFootprintForBatteries?.payload ?? {};

  return {
    passportId: passport.passportId,
    registryId: passport.registryInfo.registryId,
    status: passport.registryInfo.status,
    modelNumber: valueAt(general, "productIdentifier"),
    serialNumber: valueAt(general, "batteryPassportIdentifier"),
    category: valueAt(general, "batteryCategory").toUpperCase(),
    batteryStatus: valueAt(general, "batteryStatus").toUpperCase(),
    weight: valueAt(general, "batteryMass"),
    carbonFootprint: valueAt(carbon, "batteryCarbonFootprint"),
    isValid: passport.validation.isValid,
    verificationState: passport.validation.isValid ? "verified" : "unverified",
    sections: [
      "General",
      "Material composition",
      "Performance",
      "Carbon footprint",
      "Circularity",
      "Compliance",
      "Supply chain",
    ],
  };
}
```

- [ ] **Step 2: Create `web/tests/passport-view-model.test.ts`**

```ts
import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { samplePassport } from "../src/lib/seed/sample-passport";
import { toPassportViewModel } from "../src/lib/view-model/passport-view-model";

describe("toPassportViewModel", () => {
  it("maps canonical aspect payloads into reference-style display fields", () => {
    const vm = toPassportViewModel(samplePassport);

    assert.equal(vm.modelNumber, "EV-BAT095");
    assert.equal(vm.category, "EV");
    assert.equal(vm.carbonFootprint, "137");
    assert.equal(vm.sections.includes("Supply chain"), true);
  });
});
```

- [ ] **Step 3: Create passport UI components**

Create `web/src/components/passport/verification-badge.tsx`:

```tsx
import { ShieldCheck } from "lucide-react";
import { StatusPill } from "@/components/ui/status-pill";

export function VerificationBadge({ valid }: { valid: boolean }) {
  return (
    <div className="flex items-center gap-2">
      <ShieldCheck className="h-5 w-5 text-emerald-700 dark:text-emerald-300" />
      <StatusPill status={valid ? "verified" : "unverified"} label={valid ? "Verified" : "Unverified"} />
    </div>
  );
}
```

Create `web/src/components/passport/passport-card.tsx`:

```tsx
import Link from "next/link";
import { Card } from "@/components/ui/card";
import { VerificationBadge } from "./verification-badge";

type PassportCardProps = {
  passport: {
    passportId: string;
    modelNumber: string;
    serialNumber: string;
    category: string;
    batteryStatus: string;
    weight: string;
    carbonFootprint: string;
    isValid: boolean;
  };
};

export function PassportCard({ passport }: PassportCardProps) {
  return (
    <Card>
      <div className="flex flex-col gap-6 md:flex-row md:items-start md:justify-between">
        <div>
          <VerificationBadge valid={passport.isValid} />
          <h1 className="mt-4 text-2xl font-semibold">{passport.modelNumber || "Battery Passport"}</h1>
          <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{passport.passportId}</p>
        </div>
        <Link className="rounded-md bg-emerald-700 px-4 py-2 text-center text-sm font-medium text-white hover:bg-emerald-800" href={`/${encodeURIComponent(passport.passportId)}/summary`}>
          Summary report
        </Link>
      </div>
      <dl className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div><dt className="text-xs uppercase text-slate-500">Category</dt><dd className="mt-1 font-medium">{passport.category}</dd></div>
        <div><dt className="text-xs uppercase text-slate-500">Status</dt><dd className="mt-1 font-medium">{passport.batteryStatus}</dd></div>
        <div><dt className="text-xs uppercase text-slate-500">Weight</dt><dd className="mt-1 font-medium">{passport.weight} kg</dd></div>
        <div><dt className="text-xs uppercase text-slate-500">Carbon footprint</dt><dd className="mt-1 font-medium">{passport.carbonFootprint} kg CO2e/kWh</dd></div>
      </dl>
    </Card>
  );
}
```

Create `web/src/components/passport/section-nav.tsx`:

```tsx
export function SectionNav({ sections }: { sections: string[] }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {sections.map((section) => (
        <section key={section} className="rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
          <h2 className="font-medium">{section}</h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">View details and verification metadata.</p>
        </section>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Create `web/src/components/registry/registry-table.tsx`**

```tsx
import Link from "next/link";
import type { BatteryPassport } from "@/types/passport";

export function RegistryTable({ passports }: { passports: BatteryPassport[] }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <table className="w-full min-w-[760px] text-left text-sm">
        <thead className="border-b border-slate-200 text-xs uppercase text-slate-500 dark:border-slate-800">
          <tr>
            <th className="px-4 py-3">Registry ID</th>
            <th className="px-4 py-3">Passport ID</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Updated</th>
            <th className="px-4 py-3">Actions</th>
          </tr>
        </thead>
        <tbody>
          {passports.map((passport) => (
            <tr key={passport.passportId} className="border-b border-slate-100 last:border-0 dark:border-slate-800">
              <td className="px-4 py-3">{passport.registryInfo.registryId}</td>
              <td className="max-w-sm truncate px-4 py-3">{passport.passportId}</td>
              <td className="px-4 py-3">{passport.registryInfo.status}</td>
              <td className="px-4 py-3">{passport.registryInfo.updatedAt}</td>
              <td className="px-4 py-3">
                <Link className="text-emerald-700 dark:text-emerald-300" href={`/${encodeURIComponent(passport.passportId)}`}>View</Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

- [ ] **Step 5: Create public pages**

Create `web/src/app/registry/page.tsx`:

```tsx
import { RegistryTable } from "@/components/registry/registry-table";
import { listPassports } from "@/lib/db/passports";

export default async function RegistryPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const params = await searchParams;
  const passports = await listPassports(params.q ?? "");

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Passport Registry</h1>
      <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Search and open registered battery passports.</p>
      <div className="mt-6">
        <RegistryTable passports={passports} />
      </div>
    </main>
  );
}
```

Create `web/src/app/[passportId]/page.tsx`:

```tsx
import { notFound } from "next/navigation";
import { PassportCard } from "@/components/passport/passport-card";
import { SectionNav } from "@/components/passport/section-nav";
import { getPassport } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export default async function PassportPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) notFound();
  const viewModel = toPassportViewModel(passport);

  return (
    <main className="mx-auto max-w-6xl space-y-6 px-4 py-10">
      <PassportCard passport={viewModel} />
      <SectionNav sections={viewModel.sections} />
    </main>
  );
}
```

Create `web/src/app/[passportId]/summary/page.tsx`:

```tsx
import { notFound } from "next/navigation";
import { Card } from "@/components/ui/card";
import { getPassport } from "@/lib/db/passports";
import { toPassportViewModel } from "@/lib/view-model/passport-view-model";

export default async function PassportSummaryPage({ params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) notFound();
  const viewModel = toPassportViewModel(passport);

  return (
    <main className="mx-auto max-w-6xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Summary report</h1>
      <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{viewModel.passportId}</p>
      <div className="mt-6 grid gap-4 md:grid-cols-2">
        {viewModel.sections.map((section) => (
          <Card key={section}>
            <h2 className="font-medium">{section}</h2>
            <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Detailed schema-aligned values will appear in this section.</p>
          </Card>
        ))}
      </div>
    </main>
  );
}
```

- [ ] **Step 6: Run tests/lint and commit**

```powershell
Set-Location web
npm test -- tests/passport-view-model.test.ts
npm run lint
Set-Location ..
git add web
git commit -m "feat: add public passport viewer"
```

Expected: test and lint pass.

## Task 9: Add Passport API Routes

**Files:**
- Create: `web/src/app/api/passports/route.ts`
- Create: `web/src/app/api/passports/[passportId]/route.ts`

- [ ] **Step 1: Create `web/src/app/api/passports/route.ts`**

```ts
import { NextResponse } from "next/server";
import { listPassports, upsertPassport } from "@/lib/db/passports";
import type { BatteryPassport } from "@/types/passport";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const passports = await listPassports(url.searchParams.get("q") ?? "");
  return NextResponse.json({ passports });
}

export async function POST(request: Request) {
  const passport = (await request.json()) as BatteryPassport;
  if (!passport.passportId) {
    return NextResponse.json({ error: "passportId is required" }, { status: 400 });
  }
  const saved = await upsertPassport(passport);
  return NextResponse.json({ passport: saved }, { status: 201 });
}
```

- [ ] **Step 2: Create `web/src/app/api/passports/[passportId]/route.ts`**

```ts
import { NextResponse } from "next/server";
import { archivePassport, getPassport, upsertPassport } from "@/lib/db/passports";
import type { BatteryPassport } from "@/types/passport";

export async function GET(_request: Request, { params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) {
    return NextResponse.json({ error: "Passport does not exist" }, { status: 404 });
  }
  return NextResponse.json({ passport });
}

export async function PUT(request: Request, { params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  const passport = (await request.json()) as BatteryPassport;
  if (passport.passportId !== decodeURIComponent(passportId)) {
    return NextResponse.json({ error: "passportId mismatch" }, { status: 400 });
  }
  const saved = await upsertPassport(passport);
  return NextResponse.json({ passport: saved });
}

export async function DELETE(_request: Request, { params }: { params: Promise<{ passportId: string }> }) {
  const { passportId } = await params;
  await archivePassport(decodeURIComponent(passportId));
  return NextResponse.json({ archived: true });
}
```

- [ ] **Step 3: Run lint and commit**

```powershell
Set-Location web
npm run lint
Set-Location ..
git add web/src/app/api/passports
git commit -m "feat: add passport API routes"
```

Expected: lint passes.

## Task 10: Add Admin Dashboard And Section Editor Shell

**Files:**
- Create: `web/src/components/admin/section-editor.tsx`
- Create: `web/src/app/admin/page.tsx`
- Create: `web/src/app/admin/passports/page.tsx`
- Create: `web/src/app/admin/passports/[passportId]/edit/page.tsx`

- [ ] **Step 1: Create `web/src/components/admin/section-editor.tsx`**

```tsx
import { Card } from "@/components/ui/card";
import { aspectSchemas } from "@/lib/schemas/registry";
import type { BatteryPassport } from "@/types/passport";

export function SectionEditor({ passport }: { passport: BatteryPassport }) {
  return (
    <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
      <nav className="space-y-2">
        {Object.values(aspectSchemas).map((schema) => (
          <a key={schema.key} href={`#${schema.key}`} className="block rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-800 dark:bg-slate-900">
            {schema.title}
          </a>
        ))}
      </nav>
      <div className="space-y-4">
        {Object.values(aspectSchemas).map((schema) => (
          <Card key={schema.key} className="scroll-mt-24" >
            <div id={schema.key}>
              <h2 className="font-medium">{schema.title}</h2>
              <p className="mt-1 text-xs text-slate-500">Schema version {schema.version}</p>
              <pre className="mt-4 max-h-80 overflow-auto rounded-md bg-slate-950 p-4 text-xs text-slate-100">
                {JSON.stringify(passport.aspects[schema.key]?.payload ?? {}, null, 2)}
              </pre>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Create `web/src/app/admin/page.tsx`**

```tsx
import Link from "next/link";
import { Card } from "@/components/ui/card";
import { requireRole } from "@/lib/auth/session";
import { listPassports } from "@/lib/db/passports";

export default async function AdminPage() {
  await requireRole("admin");
  const passports = await listPassports();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Admin dashboard</h1>
      <div className="mt-6 grid gap-4 md:grid-cols-3">
        <Card><p className="text-sm text-slate-500">Passports</p><p className="mt-2 text-3xl font-semibold">{passports.length}</p></Card>
        <Card><p className="text-sm text-slate-500">Published</p><p className="mt-2 text-3xl font-semibold">{passports.filter((p) => p.registryInfo.status === "published").length}</p></Card>
        <Card><p className="text-sm text-slate-500">Draft</p><p className="mt-2 text-3xl font-semibold">{passports.filter((p) => p.registryInfo.status === "draft").length}</p></Card>
      </div>
      <Link className="mt-6 inline-flex rounded-md bg-emerald-700 px-4 py-2 text-sm font-medium text-white" href="/admin/passports">
        Manage passports
      </Link>
    </main>
  );
}
```

- [ ] **Step 3: Create `web/src/app/admin/passports/page.tsx`**

```tsx
import Link from "next/link";
import { RegistryTable } from "@/components/registry/registry-table";
import { requireRole } from "@/lib/auth/session";
import { listPassports } from "@/lib/db/passports";

export default async function AdminPassportsPage() {
  await requireRole("admin");
  const passports = await listPassports();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Manage passports</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Open a passport to edit schema-aligned sections.</p>
        </div>
      </div>
      <div className="mt-6">
        <RegistryTable passports={passports} />
      </div>
      <div className="mt-4 space-y-2">
        {passports.map((passport) => (
          <Link key={passport.passportId} className="block text-sm text-emerald-700 dark:text-emerald-300" href={`/admin/passports/${encodeURIComponent(passport.passportId)}/edit`}>
            Edit {passport.passportId}
          </Link>
        ))}
      </div>
    </main>
  );
}
```

- [ ] **Step 4: Create `web/src/app/admin/passports/[passportId]/edit/page.tsx`**

```tsx
import { notFound } from "next/navigation";
import { SectionEditor } from "@/components/admin/section-editor";
import { requireRole } from "@/lib/auth/session";
import { getPassport } from "@/lib/db/passports";

export default async function EditPassportPage({ params }: { params: Promise<{ passportId: string }> }) {
  await requireRole("admin");
  const { passportId } = await params;
  const passport = await getPassport(decodeURIComponent(passportId));
  if (!passport) notFound();

  return (
    <main className="mx-auto max-w-7xl px-4 py-10">
      <h1 className="text-2xl font-semibold">Edit passport</h1>
      <p className="mt-2 break-all text-sm text-slate-600 dark:text-slate-300">{passport.passportId}</p>
      <div className="mt-6">
        <SectionEditor passport={passport} />
      </div>
    </main>
  );
}
```

- [ ] **Step 5: Run lint and commit**

```powershell
Set-Location web
npm run lint
Set-Location ..
git add web/src/app/admin web/src/components/admin
git commit -m "feat: add admin passport editor shell"
```

Expected: lint passes.

## Task 11: Local Smoke Test

**Files:**
- Modify: no files unless a previous task produced a defect.

- [ ] **Step 1: Create `web/.env.local`**

Use the values from `web/.env.example` and replace `MONGODB_URI`, `SESSION_SECRET`, and demo credentials with real local/internal demo values.

- [ ] **Step 2: Seed MongoDB Atlas**

Run:

```powershell
Set-Location web
npm run seed
Set-Location ..
```

Expected:

```text
Seeded passport did:web:local.battery.pass:0226151e-949c-d067-8ef3-162431e28976
Seeded admin user admin@example.test
```

- [ ] **Step 3: Start the app**

Run:

```powershell
Set-Location web
npm run dev
```

Expected:

```text
Local: http://localhost:3000
```

- [ ] **Step 4: Manually verify public pages**

Open:

```text
http://localhost:3000
http://localhost:3000/registry
http://localhost:3000/did%3Aweb%3Alocal.battery.pass%3A0226151e-949c-d067-8ef3-162431e28976
http://localhost:3000/did%3Aweb%3Alocal.battery.pass%3A0226151e-949c-d067-8ef3-162431e28976/summary
```

Expected:

- landing page loads
- registry lists one seeded passport
- overview page shows EV-BAT095 and verification badge
- summary page shows seven Battery Pass sections
- theme toggle switches light and dark mode

- [ ] **Step 5: Manually verify admin pages**

Open:

```text
http://localhost:3000/login
```

Sign in with the seeded admin credentials, then open:

```text
http://localhost:3000/admin
http://localhost:3000/admin/passports
http://localhost:3000/admin/passports/did%3Aweb%3Alocal.battery.pass%3A0226151e-949c-d067-8ef3-162431e28976/edit
```

Expected:

- login succeeds
- admin dashboard shows registry counts
- passport management lists the seeded record
- editor displays canonical aspect payload JSON by section

- [ ] **Step 6: Commit smoke-test fixes**

If fixes were needed:

```powershell
git add web
git commit -m "fix: pass foundation smoke test"
```

If no fixes were needed, do not create an empty commit.
