import type { BatteryPassport } from "@/types/passport";

type ClusterAdminUpdateOptions = {
  now?: string;
  batteryImageUrl?: string;
  batteryImageFileId?: string;
};

function clonePassport(passport: BatteryPassport) {
  return JSON.parse(JSON.stringify(passport)) as BatteryPassport;
}

function text(formData: FormData, key: string, fallback = "") {
  const value = formData.get(key);
  return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

function numberInRange(formData: FormData, key: string, fallback: number, label: string, min: number, max?: number) {
  const raw = text(formData, key, String(fallback));
  const parsed = Number(raw);
  if (!Number.isFinite(parsed) || parsed < min || (max != null && parsed > max)) {
    throw new RangeError(max == null ? `${label} must be ${min} or greater` : `${label} must be between ${min} and ${max}`);
  }
  return parsed;
}

function currentConditionValue(
  condition: Record<string, Record<string, unknown>>,
  key: string,
  valueKey: string,
  fallback: number,
) {
  const value = Number(condition[key]?.[valueKey] ?? fallback);
  return Number.isFinite(value) ? value : fallback;
}

export function buildClusterAdminPassportUpdate(
  existingPassport: BatteryPassport,
  formData: FormData,
  options: ClusterAdminUpdateOptions = {},
) {
  const now = options.now ?? new Date().toISOString();
  const passport = clonePassport(existingPassport);
  const app = passport.app;
  const general = passport.aspects.generalProductInformation?.payload ?? {};
  const performance = passport.aspects.performanceAndDurability?.payload ?? {};
  const condition = (performance.batteryCondition ?? {}) as Record<string, Record<string, unknown>>;

  const facilityId = text(formData, "facilityId", app.display.facilityId);
  const stateOfCharge = numberInRange(
    formData,
    "stateOfCharge",
    currentConditionValue(condition, "stateOfCharge", "stateOfChargeValue", 0),
    "State of charge",
    0,
    100,
  );
  const remainingCapacity = numberInRange(
    formData,
    "remainingCapacity",
    currentConditionValue(condition, "remainingCapacity", "remainingCapacityValue", 0),
    "Remaining capacity",
    0,
    100,
  );
  const remainingEnergy = numberInRange(
    formData,
    "remainingEnergy",
    currentConditionValue(condition, "remainingEnergy", "remainingEnergyValue", 0),
    "Remaining energy",
    0,
  );
  const fullCycles = Math.trunc(
    numberInRange(
      formData,
      "fullCycles",
      currentConditionValue(condition, "numberOfFullCycles", "numberOfFullCyclesValue", 0),
      "Full cycles",
      0,
    ),
  );

  app.display.facilityId = facilityId;
  app.media.batteryImageUrl = options.batteryImageUrl || app.media.batteryImageUrl;
  app.media.batteryImageFileId = options.batteryImageFileId || app.media.batteryImageFileId;
  app.media.batteryImageAlt = `Industrial EV battery pack for sample passport ${app.display.modelNumber}`;

  general.manufacturingPlace = {
    ...((general.manufacturingPlace ?? {}) as Record<string, unknown>),
    streetAddress: facilityId,
  };
  const manufacturerInformation = (general.manufacturerInformation ?? {}) as Record<string, unknown>;
  general.manufacturerInformation = {
    ...manufacturerInformation,
    postalAddress: {
      ...((manufacturerInformation.postalAddress ?? {}) as Record<string, unknown>),
      streetAddress: facilityId,
    },
  };

  condition.stateOfCharge = { ...condition.stateOfCharge, stateOfChargeValue: stateOfCharge, lastUpdate: now };
  condition.remainingCapacity = { ...condition.remainingCapacity, remainingCapacityValue: remainingCapacity, lastUpdate: now };
  condition.remainingEnergy = { ...condition.remainingEnergy, remainingEnergyValue: remainingEnergy, lastUpdate: now };
  condition.numberOfFullCycles = { ...condition.numberOfFullCycles, numberOfFullCyclesValue: fullCycles, lastUpdate: now };
  performance.batteryCondition = condition;

  passport.aspects.generalProductInformation = {
    ...(passport.aspects.generalProductInformation ?? { visibility: "public", verification: { state: "verified" as const } }),
    payload: general,
  };
  passport.aspects.performanceAndDurability = {
    ...(passport.aspects.performanceAndDurability ?? { visibility: "public", verification: { state: "verified" as const } }),
    payload: performance,
  };
  passport.registryInfo.updatedAt = now;

  return passport;
}
