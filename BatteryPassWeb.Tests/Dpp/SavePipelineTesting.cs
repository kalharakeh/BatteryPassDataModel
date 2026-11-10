using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The save pipeline around a test store; the fixed test clock unless a clock is given.</summary>
internal static class SavePipelineTesting
{
    public static SavePipeline Pipeline(DppStore store, MongoContext context, params IPackChangeListener[] listeners) =>
        Pipeline(store, context, new FixedTimeProvider(DppStoreTesting.Now), listeners);

    public static SavePipeline Pipeline(DppStore store, MongoContext context, TimeProvider clock, params IPackChangeListener[] listeners) => new(
        store,
        TestDemo.For(store, context),
        new AttributeValueNormalizer(DppStoreTesting.Schemas),
        new AttributeValueValidator(DppStoreTesting.Schemas),
        new ApplicationAuditService(context),
        clock,
        DocumentTesting.Links,
        listeners);
}
