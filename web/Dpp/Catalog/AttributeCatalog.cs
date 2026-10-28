using A = BatteryPassWeb.Dpp.Catalog.Audience;
using C = BatteryPassWeb.Dpp.Catalog.ChangeRule;
using E = BatteryPassWeb.Dpp.Catalog.AttributeEditors;
using G = BatteryPassWeb.Dpp.Catalog.LegitimateInterestGroups;
using L = BatteryPassWeb.Dpp.Catalog.AttributeLevel;
using R = BatteryPassWeb.Dpp.Catalog.Requirement;

namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>
/// The 73 attributes of the V3 file (design Appendix A) with their v2.0.1 schema names.
/// LongListComplianceTests keeps this list in step with the file, SchemaCoverageTests with the schema.
/// </summary>
public static class AttributeCatalog
{
    private const string Hdr = AttributeDefinition.HeaderSection;
    private const string Ident = "identifiersAndProductData";
    private const string Carbon = "batteryCarbonFootprint";
    private const string Materials = "batteryMaterialsAndComposition";
    private const string Circularity = "circularityAndResourceEfficiency";
    private const string Performance = "performanceAndDurability";
    private const string DueDiligence = "supplyChainDueDiligence";
    private const string Symbols = "symbolsLabelsAndDocumentationOfConformity";

    /// <summary>Purchaser data: the team and the battery's cluster admins (role rule, decision 9).</summary>
    private const E PurchaserEditors = E.Team | E.BatteryClusterAdmins;

    /// <summary>Live values: also the battery-system feed (decision 8, J-10).</summary>
    private const E Live = E.BatterySystemFeed | E.Team | E.BatteryClusterAdmins;

    public static IReadOnlyList<AttributeDefinition> All { get; } =
    [
        new(1, "DPP Schema version", Hdr, "dppSchemaVersion", L.PassportSystem, A.Public, G.None, E.System, C.Fixed, null, R.MandatoryEspr, true),
        new(2, "DPP Status", Hdr, "dppStatus", L.PassportSystem, A.Public, G.None, E.System | E.Team | E.MarketSurveillance, C.Lifecycle, null, R.MandatoryEspr, true),
        new(3, "DPP Granularity", Hdr, "granularity", L.PassportSystem, A.Public, G.None, E.System, C.Fixed, null, R.MandatoryEspr, true),
        new(4, "Date-time of latest update of DPP", Hdr, "lastUpdated", L.PassportSystem, A.Public, G.None, E.System, C.OnPublishedOrLiveUpdate, null, R.MandatoryEspr, true),
        new(5, "Unique battery passport identifier / unique DPP identifier", Hdr, "digitalProductPassportId", L.PassportSystem, A.Public, G.None, E.System, C.NewPerVersion, null, R.Mandatory, true),
        new(6, "Unique battery identifier / unique product identifier", Hdr, "uniqueProductIdentifier", L.Pack, A.Public, G.None, E.System, C.Fixed, null, R.Mandatory, true),
        new(7, "Unique economic operator identifier", Hdr, "economicOperatorId", L.Pack, A.Authorities, G.None, E.Team, C.Fixed, null, R.MandatoryEspr, true),
        new(10, "Battery model identifier", Ident, "batteryModelIdentifier", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(11, "Battery serial number", Ident, "batterySerialNumber", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(12, "Unique manufacturer identifier", Ident, "uniqueManufacturerIdentifier", L.Site, A.Authorities, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(13, "Third party service provider", Ident, "thirdPartyServiceProvider", L.Pack, A.Authorities, G.None, E.Team, C.IfProviderChanges, null, R.MandatoryEspr, true),
        new(14, "Economic operator information", Ident, "economicOperatorInformation", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(15, "Manufacturer information", Ident, "manufacturerInformation", L.Site, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(16, "Manufacturing place", Ident, "manufacturingPlace", L.Site, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(17, "Manufacturing date", Ident, "manufacturingDate", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(19, "Warranty period of the battery", Ident, "warrantyPeriod", L.Pack, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(20, "Battery category", Ident, "batteryCategory", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(21, "Battery mass", Ident, "batteryMass", L.Model, A.Public, G.None, E.Team, C.Fixed, "kg", R.Mandatory, true),
        new(22, "Battery status", Ident, "batteryStatus", L.Pack, A.Purchaser, G.None, PurchaserEditors, C.OnStatusChange, null, R.Mandatory, true),
        new(23, "Separate collection symbol", Symbols, "separateCollectionSymbol", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(24, "Symbols for cadmium and lead", Symbols, "symbolsForCadmiumAndLead", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(25, "Carbon footprint label", Symbols, "carbonFootprintLabel", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(26, "Extinguishing agent", Symbols, "extinguishingAgent", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(27, "Meaning of labels and symbols", Symbols, "meaningOfLabelsAndSymbols", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(28, "EU declaration of conformity", Symbols, "euDeclarationOfConformity", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(29, "Results of test reports proving compliance", Symbols, "resultsOfTestReportsProvingCompliance", L.Batch, A.Authorities, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(30, "Battery carbon footprint per Functional Unit", Carbon, "batteryCarbonFootprintPerFunctionalUnit", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(31, "Contribution of raw material acquisition and pre-processing lifecycle stage", Carbon, "contributionOfRawMaterialAcquisitionAndPreProcessingLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(32, "Contribution of main product production lifecycle stage", Carbon, "contributionOfMainProductProductionLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(33, "Contribution of distribution lifecycle stage", Carbon, "contributionOfDistributionLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(34, "Contribution of end of life and recycling lifecycle stage", Carbon, "contributionOfEndOfLifeAndRecyclingLifecycleStage", L.Batch, A.Public, G.None, E.Team, C.Fixed, "kgCO2eq/kWh", R.Mandatory, false),
        new(35, "Carbon footprint performance class", Carbon, "carbonFootprintPerformanceClass", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(36, "Web link to public carbon footprint study", Carbon, "webLinkToPublicCarbonFootprintStudy", L.Batch, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(38, "Information of due diligence report", DueDiligence, "informationOfDueDiligenceReport", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, false),
        new(41, "Battery chemistry", Materials, "batteryChemistry", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(42, "Critical raw materials", Materials, "criticalRawMaterials", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(43, "Materials used in cathode, anode and electrolyte", Materials, "materialsUsedInCathodeAnodeAndElectrolyte", L.Model, A.Shared, G.Repair | G.Recycling, E.Team, C.Fixed, null, R.Mandatory, true),
        new(44, "Hazardous substances and their impact", Materials, "hazardousSubstancesAndTheirImpact", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(45, "Dismantling information: Manuals for the removal and the disassembly of the battery pack", Circularity, "dismantlingInformation", L.Model, A.Shared, G.Repair | G.Recycling, E.Team, C.Fixed, null, R.Mandatory, true),
        new(46, "Part numbers for components", Circularity, "partNumbersForComponents", L.Model, A.Shared, G.Repair, E.Team, C.Fixed, null, R.Mandatory, true),
        new(47, "Information on sources of spare parts", Circularity, "informationOnSourcesOfSpareParts", L.Model, A.Shared, G.Repair, E.Team, C.Fixed, null, R.Mandatory, true),
        new(48, "Safety measures", Circularity, "safetyMeasures", L.Model, A.Shared, G.Repair | G.Recycling | G.Treatment, E.Team, C.Fixed, null, R.Mandatory, true),
        new(49, "Nickel recycled content share", Circularity, "nickelRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(50, "Cobalt recycled content share", Circularity, "cobaltRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(51, "Lithium recycled content share", Circularity, "lithiumRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(52, "Lead recycled content share", Circularity, "leadRecycledContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, false),
        new(53, "Renewable content share", Circularity, "renewableContentShare", L.Batch, A.Public, G.None, E.Team, C.Fixed, "%", R.Mandatory, true),
        new(54, "Information on the role of end-users in contributing to waste prevention", Circularity, "informationOnTheRoleOfEndUsersInContributingToWastePrevention", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(55, "Information on the role of end-users in contributing to the separate collection of waste batteries", Circularity, "informationOnTheRoleOfEndUsersInContributingToTheSeparateCollectionOfWasteBatteries", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(56, "Information on battery collection, preparation for second life and on treatment at end of life", Circularity, "informationOnBatteryCollectionPreparationForSecondLifeAndOnTreatmentAtEndOfLife", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Mandatory, true),
        new(57, "Rated capacity", Performance, "ratedCapacity", L.Model, A.Public, G.None, E.Team, C.Fixed, "Ah", R.Mandatory, false),
        new(59, "Capacity fade", Performance, "capacityFade", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Mandatory, true),
        new(63, "State of Charge (SoC)", Performance, "stateOfChargeSoC", L.Pack, A.Purchaser, G.None, Live, C.Live, "%", R.Mandatory, true),
        new(64, "Minimum voltage", Performance, "minimumVoltage", L.Model, A.Public, G.None, E.Team, C.Fixed, "V", R.Mandatory, true),
        new(65, "Maximum voltage", Performance, "maximumVoltage", L.Model, A.Public, G.None, E.Team, C.Fixed, "V", R.Mandatory, true),
        new(66, "Nominal voltage", Performance, "nominalVoltage", L.Model, A.Public, G.None, E.Team, C.Fixed, "V", R.Mandatory, true),
        new(67, "Original power capability", Performance, "originalPowerCapability", L.Model, A.Public, G.None, E.Team, C.Fixed, "W", R.Mandatory, true),
        new(69, "Power fade", Performance, "powerFade", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Mandatory, true),
        new(70, "Maximum permitted battery power", Performance, "maximumPermittedBatteryPower", L.Model, A.Public, G.None, E.Team, C.Fixed, "W", R.Mandatory, true),
        new(71, "Initial round trip energy efficiency", Performance, "initialRoundTripEnergyEfficiency", L.Model, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, true),
        new(72, "Round trip energy efficiency at 50% of cycle life", Performance, "roundTripEnergyEfficiencyAt50PerCentOfCycleLife", L.Model, A.Public, G.None, E.Team, C.Fixed, "%", R.Conditional, true),
        new(74, "Energy round trip efficiency fade", Performance, "energyRoundTripEfficiencyFade", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Conditional, true),
        new(78, "Initial internal resistance of battery cell and pack (module recommended)", Performance, "initialInternalResistanceOfBatteryCellAndPackModuleRecommended", L.Model, A.Public, G.None, E.Team, C.Fixed, "Ohm", R.Mandatory, true),
        new(79, "Internal resistance increase of pack (cell and module recommended)", Performance, "internalResistanceIncreaseOfPackCellAndModuleRecommended", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "%", R.Mandatory, true),
        new(81, "Expected lifetime in calendar years", Performance, "expectedLifetimeInCalendarYears", L.ModelRestatedPerPack, A.Purchaser, G.None, PurchaserEditors, C.RestatedOnStatusChange, "years", R.Mandatory, true),
        new(82, "Expected lifetime - Number of charge-discharge cycles", Performance, "expectedLifetimeNumberOfChargeDischargeCycles", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(83, "Number of full charging and discharging cycles", Performance, "numberOfFullChargingAndDischargingCycles", L.Pack, A.Purchaser, G.None, Live, C.Live, null, R.Conditional, true),
        new(84, "Cycle-life reference test", Performance, "cycleLifeReferenceTest", L.Model, A.Public, G.None, E.Team, C.Fixed, null, R.Conditional, true),
        new(85, "C-rate of relevant cycle-life test", Performance, "cRateOfRelevantCycleLifeTest", L.Model, A.Public, G.None, E.Team, C.Fixed, "A/Ah", R.Conditional, true),
        new(87, "Temperature information", Performance, "temperatureInformation", L.Pack, A.Purchaser, G.None, Live, C.Live, "°C", R.Mandatory, true),
        new(88, "Temperature range idle state, lower boundary", Performance, "temperatureRangeIdleStateLowerBoundary", L.Model, A.Public, G.None, E.Team, C.Fixed, "°C", R.Mandatory, true),
        new(89, "Temperature range idle state, upper boundary", Performance, "temperatureRangeIdleStateUpperBoundary", L.Model, A.Public, G.None, E.Team, C.Fixed, "°C", R.Mandatory, true),
        new(92, "Information on accidents", Performance, "informationOnAccidents", L.Pack, A.Purchaser, G.None, PurchaserEditors, C.Live, null, R.Mandatory, true),
    ];

    private static readonly Dictionary<int, AttributeDefinition> ByNumberIndex = All.ToDictionary(a => a.Number);
    private static readonly Dictionary<string, AttributeDefinition> ByPropertyIndex = All.ToDictionary(a => a.Property, StringComparer.Ordinal);

    public static AttributeDefinition ByNumber(int number) =>
        ByNumberIndex.TryGetValue(number, out var attribute)
            ? attribute
            : throw new KeyNotFoundException($"The catalogue has no attribute #{number}.");

    public static AttributeDefinition? FindByProperty(string property) => ByPropertyIndex.GetValueOrDefault(property);
}
