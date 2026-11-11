using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round seven, 3: one battery's failed write is logged and the check goes on; the counts are what changed; the audit
/// entry is written for what was done, also when the run ends early because the app is stopping.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportCheckFailuresTests(MongoFixture mongo)
{
    [Fact]
    public async Task AFailedWrite_ShouldBeLogged_AndTheRunShouldGoOn()
    {
        var (rig, copyId) = await TwoDifferingAsync();
        var logger = new ListLogger(onError: null);
        await RefuseMarkingAsync(rig, rig.PackId);
        PassportCheckResult result;
        try
        {
            result = await Service(rig, logger).ReconcileAsync("check-test");
        }
        finally
        {
            await AllowAllAsync(rig);
        }

        Assert.Equal((2, 1, 1), (result.Checked, result.Marked, result.Failed)); // the other battery is still marked
        Assert.NotNull((await rig.Store.GetAsync<BatteryPack>(copyId))!.ChangesWaitingSinceUtc);
        Assert.Single(logger.Errors);
        var entry = await AuditAsync(rig);
        Assert.Equal(1, entry["metadata"]["marked"].AsInt32);
        Assert.Equal(1, entry["metadata"]["failed"].AsInt32);
    }

    [Fact]
    public async Task ARunThatStopsEarly_ShouldLeave_AndLogWhatItDid()
    {
        var (rig, copyId) = await TwoDifferingAsync();
        using var stopping = new CancellationTokenSource();
        var logger = new ListLogger(onError: stopping.Cancel); // the app stops while the first battery fails
        await RefuseMarkingAsync(rig, rig.PackId);
        PassportCheckResult result;
        try
        {
            result = await Service(rig, logger).ReconcileAsync("check-test", stopping.Token);
        }
        finally
        {
            await AllowAllAsync(rig);
        }

        Assert.True(result.StoppedEarly);
        Assert.Equal((0, 1), (result.Marked, result.Failed));
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(copyId))!.ChangesWaitingSinceUtc); // never reached
        var entry = await AuditAsync(rig);
        Assert.True(entry["metadata"]["stoppedEarly"].AsBoolean);
        Assert.Contains("stopped early", entry["message"].AsString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACheckDuringARun_ShouldSayOneIsAlreadyRunning()
    {
        var (rig, _) = await TwoDifferingAsync();
        PublicationService? service = null;
        PassportCheckResult? second = null;
        var logger = new ListLogger(onError: () => second = service!.ReconcileAsync("check-test-2").GetAwaiter().GetResult()); // pressed during the run
        service = Service(rig, logger);
        await RefuseMarkingAsync(rig, rig.PackId);
        PassportCheckResult first;
        try
        {
            first = await service.ReconcileAsync("check-test");
        }
        finally
        {
            await AllowAllAsync(rig);
        }

        Assert.True(second!.AlreadyRunning);
        Assert.Equal("A check is already running; its result will be in the audit log.", second.Words);
        Assert.False(first.AlreadyRunning);
        Assert.Equal(1, first.Marked);
        Assert.False((await service.ReconcileAsync("check-test-3")).AlreadyRunning); // free again afterwards
    }

    /// <summary>The seed battery published, a second published copy, and a model value written straight into the store.</summary>
    private async Task<(PublicationRig Rig, string CopyId)> TwoDifferingAsync()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var copy = (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        copy["_id"] = "pack-second";
        copy["Values"]["batterySerialNumber"]["Value"] = "SECOND";
        copy["Values"]["uniqueProductIdentifier"]["Value"] = "urn:test:second-battery";
        await packs.InsertOneAsync(copy);
        var version = (await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        version["_id"] = "version-second";
        version["PackId"] = "pack-second";
        version["DigitalProductPassportId"] = "urn:test:second";
        await versions.InsertOneAsync(version);
        await rig.Store.SetValuesAsync<BatteryModel>("model-test", new Dictionary<string, AttributeValue>
        {
            ["batteryMass"] = new() { Value = new JsonObject { ["gramKilogramUnit"] = "kg", ["gramKilogramValue"] = 513 }, GeneratedAtUtc = DateTime.UtcNow, Source = "form" },
        });
        return (rig, "pack-second");
    }

    /// <summary>The database refuses a marker on this battery: its write fails.</summary>
    private static Task RefuseMarkingAsync(PublicationRig rig, string packId) =>
        rig.Context.Database!.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["collMod"] = DppStore.Packs,
            ["validator"] = new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("_id", new BsonDocument("$ne", packId)),
                new BsonDocument(nameof(BatteryPack.ChangesWaitingSinceUtc), new BsonDocument("$exists", false)),
            }),
            ["validationLevel"] = "strict",
            ["validationAction"] = "error",
        });

    private static Task AllowAllAsync(PublicationRig rig) =>
        rig.Context.Database!.RunCommandAsync<BsonDocument>(new BsonDocument { ["collMod"] = DppStore.Packs, ["validator"] = new BsonDocument() });

    private static Task<BsonDocument> AuditAsync(PublicationRig rig) =>
        rig.Context.Database!.GetCollection<BsonDocument>("applicationAuditEvents")
            .Find(Builders<BsonDocument>.Filter.Eq("eventType", PublicationService.CheckedEvent)).SingleAsync();

    private static PublicationService Service(PublicationRig rig, ILogger<PublicationService> logger) => new(
        rig.Store,
        TestDemo.For(rig),
        new PublicationChecks(DppStoreTesting.Schemas),
        new PassportSigner(SigningTesting.Keys),
        new TestIdentifierScheme(),
        new ApplicationAuditService(rig.Context),
        rig.Clock,
        logger: logger);

    /// <summary>Keeps the errors logged; can run an action on the first one (to stop the app half way).</summary>
    private sealed class ListLogger(Action? onError) : ILogger<PublicationService>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
                onError?.Invoke();
            }
        }
    }
}
