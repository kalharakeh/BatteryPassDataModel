import type { BatteryPassport } from "@/types/passport";

function normalized(value: string) {
  return value.trim().toLowerCase();
}

export function summaryRedirectForSearch(query: string, passports: BatteryPassport[]) {
  const search = normalized(query);
  if (!search || passports.length !== 1) return null;

  const passport = passports[0];
  const exactIds = [
    passport.passportId,
    passport.registryInfo.registryId,
    passport.app?.display.serialNumber,
    passport.aspects.generalProductInformation?.payload.batteryPassportIdentifier,
  ]
    .filter((value): value is string => typeof value === "string")
    .map(normalized);

  return exactIds.includes(search) ? `/${encodeURIComponent(passport.passportId)}/summary` : null;
}

export function publicSearchResult(query: string, passports: BatteryPassport[]) {
  const href = summaryRedirectForSearch(query, passports);
  if (href) {
    return { state: "summary" as const, href };
  }
  if (query.trim()) {
    return { state: "notFound" as const };
  }
  return { state: "empty" as const };
}
