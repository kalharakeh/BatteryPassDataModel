using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>A clock a test can move forward.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>The seed pack in a fresh store, with publication wired to the save pipeline as in the app.</summary>
internal sealed record PublicationRig(
    DppStore Store,
    MongoContext Context,
    SeedPassport Seed,
    PublicationService Publication,
    SavePipeline Pipeline,
    MutableTimeProvider Clock)
{
    public string PackId => Seed.Sources.Pack.Id;
}

internal static class PublicationTesting
{
    public static readonly ChangeSender Team = ChangeSender.Team("team@example.com");

    public static async Task<PublicationRig> RigAsync(MongoFixture mongo)
    {
        var (store, context, seed) = await DppStoreTesting.SeededStoreAsync(mongo);
        var clock = new MutableTimeProvider(DppStoreTesting.Now);
        var publication = new PublicationService(
            store,
            new PublicationChecks(DppStoreTesting.Schemas),
            new PassportSigner(SigningTesting.Keys),
            new TestIdentifierScheme(),
            new ApplicationAuditService(context),
            clock);
        var pipeline = SavePipelineTesting.Pipeline(store, context, clock, publication);
        return new PublicationRig(store, context, seed, publication, pipeline, clock);
    }

    /// <summary>The rig with the seed pack published once (version 1).</summary>
    public static async Task<PublicationRig> PublishedRigAsync(MongoFixture mongo)
    {
        var rig = await RigAsync(mongo);
        var result = await rig.Publication.PublishAsync(Team, rig.PackId);
        Assert.Null(result.Problem);
        return rig;
    }
}
