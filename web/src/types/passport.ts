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
export type ClusterMembershipRole = "member" | "clusterAdmin";

export type Cluster = {
  clusterId: string;
  name: string;
  createdAt: string;
  updatedAt: string;
};

export type ClusterMembership = {
  email: string;
  clusterId: string;
  role: ClusterMembershipRole;
  createdAt: string;
  updatedAt: string;
};

export type ChartSegment = {
  label: string;
  value: number;
  unit?: string;
  color?: string;
};

export type RecycledContentChart = {
  material: string;
  preConsumerShare: number;
  postConsumerShare: number;
  primaryMaterialShare: number;
};

export type PassportDocumentKey =
  | "conformityAssessment"
  | "euDeclarationOfConformity"
  | "sustainabilityReport"
  | "dueDiligenceReport"
  | "thirdPartyAudit"
  | "taxonomyReport"
  | "co2StudyReference";

export type PassportDocumentLink = {
  label: string;
  url: string;
  fileId?: string;
  contentType?: string;
};

export type PassportAppMetadata = {
  display: {
    name: string;
    modelNumber: string;
    serialNumber: string;
    facilityId: string;
    manufacturerName: string;
  };
  media: {
    batteryImageUrl: string;
    batteryImageFileId?: string;
    batteryImageAlt: string;
  };
  documents: Record<PassportDocumentKey, PassportDocumentLink>;
  charts: {
    materialComposition: ChartSegment[];
    carbonFootprint: ChartSegment[];
    recycledContent: RecycledContentChart[];
  };
  notes: {
    circularity: {
      separateCollection: string;
      wastePrevention: string;
      recycledContentShareVerification: VerificationState;
    };
  };
};

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
  clusterId?: string;
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
  app: PassportAppMetadata;
};
