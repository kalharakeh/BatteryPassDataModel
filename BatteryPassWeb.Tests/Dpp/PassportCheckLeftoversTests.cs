using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// The closing review's small points on the check of all published batteries (decision 65), finished in package C2:
/// a battery published between the check's two reads, a failed audit write, the button's token, and a press during a run.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PassportCheckLeftoversTests(MongoFixture mongo)
{
    [Fact]
    public async Task ReconcileAsync_ShouldPassABatteryBy_WhenItsFirstPublicationLandsBetweenTheTwoReads()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var packs = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Packs);
        var versions = rig.Context.Database!.GetCollection<BsonDocument>(DppStore.Versions);
        var copy = (await packs.Find(Builders<BsonDocument>.Filter.Eq("_id", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        copy.Remove("ItemKey"); // a copy has no key of its own yet, as a pack from before S1 round two
        copy["_id"] = "pack-late";
        copy["Values"]["batterySerialNumber"]["Value"] = "LATE";
        copy["Values"]["uniqueProductIdentifier"]["Value"] = "urn:test:late-battery";
        await packs.InsertOneAsync(copy); // a draft when the check starts
        var version = (await versions.Find(Builders<BsonDocument>.Filter.Eq("PackId", rig.PackId)).SingleAsync()).DeepClone().AsBsonDocument;
        version["_id"] = "version-late";
        version["PackId"] = "pack-late";
        version["DigitalProductPassportId"] = "urn:test:late";
        var logger = new CheckLogger
        {
            // Right after the check has read every version's number and status, the draft's first version is published.
            OnDebug = message =>
            {
                if (message.Contains("read the versions", StringComparison.Ordinal))
                {
                    versions.InsertOne(version);
                }
            },
        };

        var result = await Service(rig, logger).ReconcileAsync("check-test");

        Assert.Equal((1, 0), (result.Checked, result.Failed)); // the late one is passed by, not counted as a failure
        Assert.Empty(logger.Errors);
        Assert.Null((await rig.Store.GetAsync<BatteryPack>("pack-late"))!.ChangesWaitingSinceUtc);
    }

    [Fact]
    public async Task ReconcileAsync_ShouldLogAFailedAuditWrite_AndStillAnswer()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow); // a false marker: the check clears it
        var logger = new CheckLogger();
        await RefuseAuditAsync(rig);

        var result = await Service(rig, logger).ReconcileAsync("check-test");

        Assert.Equal(1, result.Cleared); // the change is made...
        Assert.Null((await rig.Store.GetAsync<BatteryPack>(rig.PackId))!.ChangesWaitingSinceUtc);
        Assert.Contains(logger.Errors, error => error.Contains("couldn't write its audit entry", StringComparison.Ordinal)); // ...and the failed entry is logged
    }

    [Fact]
    public async Task CheckAll_ShouldRunOnTheAppsStoppingToken_NotTheRequests()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo).WithClustersAsync();
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow);
        var controller = BatteriesControllerTests.Controller(rig, new Dictionary<string, string>());
        using var closedTab = new CancellationTokenSource();
        await closedTab.CancelAsync();
        controller.HttpContext.RequestAborted = closedTab.Token; // the tab is closed while the check runs

        var result = Assert.IsType<RedirectResult>(await controller.CheckAll(rig.Publication, new NotStopping()));

        Assert.Equal("/admin/batteries", result.Url);
        Assert.Equal("Checked 1 published battery: 0 now marked as waiting, 1 no longer, 0 old versions archived.", controller.TempData[ProductDataController.StatusKey]);
    }

    [Fact]
    public async Task ReconcileAsync_ShouldLogAPress_WhenACheckIsAlreadyRunning()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        await rig.Store.MarkChangesWaitingAsync([rig.PackId], DateTime.UtcNow);
        PublicationService? service = null;
        PassportCheckResult? second = null;
        var logger = new CheckLogger();
        logger.OnDebug = message =>
        {
            if (second is null && message.Contains("read the versions", StringComparison.Ordinal))
            {
                second = service!.ReconcileAsync("team@example.com").GetAwaiter().GetResult(); // pressed during the run
            }
        };
        service = Service(rig, logger);

        await service.ReconcileAsync("check-test");

        Assert.True(second!.AlreadyRunning);
        Assert.Equal("A check is already running; if it puts anything right, the audit log will say so.", second.Words);
        Assert.Contains(logger.Information, line => line.Contains("team@example.com", StringComparison.Ordinal) && line.Contains("already running", StringComparison.Ordinal));
    }

    /// <summary>The database refuses every audit entry.</summary>
    private static async Task RefuseAuditAsync(PublicationRig rig)
    {
        var database = rig.Context.Database!;
        var names = await (await database.ListCollectionNamesAsync()).ToListAsync();
        if (!names.Contains("applicationAuditEvents"))
        {
            await database.CreateCollectionAsync("applicationAuditEvents");
        }

        await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["collMod"] = "applicationAuditEvents",
            ["validator"] = new BsonDocument("never", new BsonDocument("$exists", true)),
            ["validationLevel"] = "strict",
            ["validationAction"] = "error",
        });
    }

    private static PublicationService Service(PublicationRig rig, ILogger<PublicationService> logger) => new(
        rig.Store,
        TestDemo.For(rig),
        new PublicationChecks(DppStoreTesting.Schemas),
        new PassportSigner(SigningTesting.Keys),
        new TestIdentifierScheme(),
        new ApplicationAuditService(rig.Context),
        rig.Clock,
        logger: logger);

    /// <summary>The app's lifetime, not stopping.</summary>
    internal sealed class NotStopping : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }

    /// <summary>Keeps what is logged; can act on a debug line, to stage a crossing at a known point of the check.</summary>
    private sealed class CheckLogger : ILogger<PublicationService>
    {
        public List<string> Errors { get; } = [];

        public List<string> Information { get; } = [];

        public Action<string>? OnDebug { get; set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);
            switch (logLevel)
            {
                case >= LogLevel.Error:
                    Errors.Add(line);
                    break;
                case LogLevel.Information:
                    Information.Add(line);
                    break;
                case LogLevel.Debug:
                    OnDebug?.Invoke(line);
                    break;
            }
        }
    }
}
