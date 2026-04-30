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
