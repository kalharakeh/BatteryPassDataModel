import type { BatteryPassport } from "../../types/passport";

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
