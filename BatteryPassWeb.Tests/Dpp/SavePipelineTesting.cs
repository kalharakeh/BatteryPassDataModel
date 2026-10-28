using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The save pipeline around a test store, with the fixed test clock.</summary>
internal static class SavePipelineTesting
{
    public static SavePipeline Pipeline(DppStore store, MongoContext context) => new(
        store,
        new AttributeValueNormalizer(DppStoreTesting.Schemas),
        new AttributeValueValidator(DppStoreTesting.Schemas),
        new ApplicationAuditService(context),
        new FixedTimeProvider(DppStoreTesting.Now));
}
