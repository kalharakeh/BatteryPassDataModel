using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using static BatteryPassWeb.Tests.Dpp.SmallLetterSerialTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 80 at release, creation and swap: a serial number with a small letter is released and kept as typed, its ID
/// carries it the same way, and the same serial number in other letters can't be released, created or swapped to. The database
/// itself keeps two releases at once from sharing one.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SmallLetterSerialReleaseTests(MongoFixture mongo)
{
    [Fact]
    public async Task ARangeWithASmallLetter_ShouldBeReleasedAndListedAsTyped_WithTheSmallLetterInItsIds()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;

        var result = await pool.ReleaseAsync(Who, new ReleaseRequest("PO Core800s", null, [new IdRange("VPC8C05s-00001", "VPC8C05s-00003", "3")], null));

        Assert.Equal(3, result.Released);
        var items = await pool.ListAsync(new ReleasedIdFilter(Reference: result.ReleaseId));
        Assert.Equal(["VPC8C05s-00001", "VPC8C05s-00002", "VPC8C05s-00003"], items.Select(item => item.SerialNumber));
        Assert.Equal($"{Base}/25S/{Item}", items[0].Identifier);
        Assert.All(items, item => Assert.Matches("/25S/UN778474481-VPC8C05s-0000[1-3]$", item.Identifier));
        Assert.Equal("VPC8C05S00001", items[0].Id); // the key: letters and digits, in capitals
        var logged = (await rig.All("applicationAuditEvents", Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("entityId", result.ReleaseId))).Single()["message"].AsString;
        Assert.Contains("VPC8C05s-00001 … VPC8C05s-00003", logged, StringComparison.Ordinal); // the audit log names them as released
    }

    [Theory]
    [InlineData("VPC8C05S-00002")]
    [InlineData("vpc8c05s-00002")]
    [InlineData("VPC8C05S00002")]
    public async Task TheSameSerialInOtherLetters_ShouldNotBeReleasedAgain_AndTheRefusalSaysItIsTheSame(string again)
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", "VPC8C05s-00001", "VPC8C05s-00002", "VPC8C05s-00003"));

        var result = await pool.ReleaseAsync(Who, List("PO again", again));

        Assert.False(result.Done);
        Assert.Equal("already released as VPC8C05s-00002, which is the same serial number (Available, PO Core800s)", Assert.Single(result.Problems).Reason);
        Assert.Equal(3, await rig.Count(DppStore.BatteryIds, Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("IsDemo", false)));
    }

    [Fact]
    public async Task TheSameSpellingAgain_ShouldBeRefusedInTheOldWords()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", "VPC8C05s-00001"));

        var result = await pool.ReleaseAsync(Who, List("PO again", "VPC8C05s-00001"));

        Assert.Equal("already released (Available, PO Core800s)", Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public async Task BothSpellingsInOneRelease_ShouldBeRefused_AsTheSameSerialNumber()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;

        var result = await pool.ReleaseAsync(Who, List("PO twice", "VPC8C05s-00001", "VPC8C05S-00001"));

        Assert.Equal(0, result.Released);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(("VPC8C05S-00001", "it is in this release twice, also as VPC8C05s-00001, which is the same serial number"), (problem.What, problem.Reason));
    }

    [Fact]
    public async Task TwoReleasesAtOnce_ShouldLeaveOneId_BecauseTheDatabaseKeepsTheKeyUnique()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;

        // The pool's own check, twenty times: whichever release comes second is refused.
        for (var round = 0; round < 20; round++)
        {
            var small = $"VPC8C05s-1{round:0000}";
            var capital = small.ToUpperInvariant();
            var both = await Task.WhenAll(
                Task.Run(() => pool.ReleaseAsync(Who, List("PO small", small))),
                Task.Run(() => pool.ReleaseAsync(Who, List("PO capital", capital))));

            Assert.Single(both, result => result.Done);
            Assert.Equal(1, await rig.Count(DppStore.BatteryIds, Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", IdentifierForms.Clean(small))));
        }

        // And without that check: the store itself refuses the second spelling, whole (the _id is the key).
        ReleasedBatteryId Record(string serial, string release) => new()
        {
            Id = IdentifierForms.Clean(serial),
            SerialNumber = serial,
            Identifier = $"{Base}/25S/UN778474481-{serial}",
            ReleaseId = release,
            Reference = release,
            Status = ReleasedIdStatuses.Available,
            ReleasedBy = Who,
            ReleasedAtUtc = DateTime.UtcNow,
        };
        Assert.True(await rig.Store.InsertReleaseAsync([Record("VPC8C05s-20001", "first")]));
        Assert.False(await rig.Store.InsertReleaseAsync([Record("VPC8C05s-20002", "second"), Record("VPC8C05S-20001", "second")]));
        Assert.Equal("VPC8C05s-20001", (await pool.GetAsync("vpc8c05s-20001"))!.SerialNumber);
        Assert.Null(await pool.GetAsync("VPC8C05s-20002")); // all or nothing
    }

    [Theory]
    [MemberData(nameof(SmallLetterSerialTesting.Serials), MemberType = typeof(SmallLetterSerialTesting))]
    [InlineData("VPC8C05S00001")] // without its dash
    public async Task ABatteryMadeWithAnySpelling_ShouldTakeTheReleasedId_AndKeepTheSerialAsReleased(string typed)
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", Serial));
        var creation = rig.Services.GetRequiredService<PackCreationService>();

        var created = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, typed));

        Assert.Null(created.Problem);
        Assert.Equal(Serial, Text(created.Pack!, "batterySerialNumber")); // as released, small s included
        Assert.Equal($"{Base}/25S/{Item}", Text(created.Pack!, "uniqueProductIdentifier"));
        Assert.Equal((ReleasedIdStatuses.Used, created.Pack!.Id), ((await pool.GetAsync(typed))!.Status, (await pool.GetAsync(typed))!.PackId));

        // Once it exists, no spelling of it makes a second battery.
        foreach (var again in SerialSpellings.Append("VPC8C05S00001"))
        {
            var second = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, again));
            Assert.Null(second.Pack);
            Assert.Contains(second.Keys, key => key.Key == "batterySerialNumber" && key.Message == SavePipeline.SerialNumberTaken);
        }

        Assert.Single(await rig.Store.FindPacksBySerialNumberAsync(Serial));
    }

    [Fact]
    public async Task ABatteryMadeWithoutThePool_ShouldNotShareASerialNumberInOtherLetters()
    {
        var (rig, _, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var first = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC8C05s-00050") with { FromPool = false });
        Assert.Equal($"{Base}/25S/UN778474481-VPC8C05s-00050", Text(first.Pack!, "uniqueProductIdentifier"));

        foreach (var again in new[] { "VPC8C05S-00050", "vpc8c05s-00050", "VPC8C05S00050" })
        {
            var second = await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, again) with { FromPool = false });
            Assert.Null(second.Pack);
            Assert.Contains(second.Keys, key => key.Key == "batterySerialNumber" && key.Message == SavePipeline.SerialNumberTaken);
        }

        // The ID scheme's own check, should the serial check ever be passed: an ID that differs only in capitals is taken.
        var scheme = rig.Services.GetRequiredService<IIdentifierScheme>();
        var other = new BatteryPack { Id = "pack-other", BatchId = first.Pack!.BatchId, OperatorId = first.Pack.OperatorId };
        other.Values["batterySerialNumber"] = new BatteryPassWeb.Dpp.Values.AttributeValue { Value = System.Text.Json.Nodes.JsonValue.Create("VPC8C05S-00050"), GeneratedAtUtc = DateTime.UtcNow, Source = "form" };
        var made = await scheme.NewPackIdentifierAsync(other);
        Assert.Equal((null, DunsIdentifierScheme.Taken("UN778474481-VPC8C05s-00050", "VPC8C05s-00050")), (made.Identifier, made.Problem));
    }

    [Fact]
    public async Task TwoBatteriesMadeAtOnceWithTheTwoSpellings_ShouldLeaveOneBattery_BecauseTheIdIsPickedInOneStep()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        var creation = rig.Services.GetRequiredService<PackCreationService>();

        for (var round = 0; round < 8; round++)
        {
            var small = $"VPC8C05s-3{round:0000}";
            await pool.ReleaseAsync(Who, List($"PO {round}", small));
            var both = await Task.WhenAll(
                Task.Run(() => creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, small))),
                Task.Run(() => creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, small.ToUpperInvariant()))));

            // In the app every battery takes its ID from the released ones, whose key (letters and digits) is unique in
            // the database and is picked in one step: whichever comes second finds it taken.
            Assert.Single(both, result => result.Pack is not null);
            Assert.Single(await rig.Store.FindPacksBySerialNumberAsync(small));
            Assert.Equal($"{Base}/25S/UN778474481-{small}", Text((await rig.Store.FindPacksBySerialNumberAsync(small.ToUpperInvariant())).Single(), "uniqueProductIdentifier"));
        }
    }

    [Fact]
    public async Task ADraft_ShouldSwapToAReleasedSerialTypedInAnyLetters_AndNotToOneInUse()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", "VPC8C05s-00001", "VPC8C05s-00002", "VPC8C05s-00003"));
        var creation = rig.Services.GetRequiredService<PackCreationService>();
        var draft = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC8C05s-00001"))).Pack!;
        var other = (await creation.CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC8C05s-00003"))).Pack!;

        Assert.Null(await pool.SwapAsync(Who, draft.Id, "vpc8c05S-00002"));

        var swapped = (await rig.Store.GetAsync<BatteryPack>(draft.Id))!;
        Assert.Equal("VPC8C05s-00002", Text(swapped, "batterySerialNumber"));
        Assert.Equal($"{Base}/25S/UN778474481-VPC8C05s-00002", Text(swapped, "uniqueProductIdentifier"));
        Assert.Equal(ReleasedIdStatuses.Available, (await pool.GetAsync("VPC8C05S-00001"))!.Status);
        Assert.Equal("that battery ID isn't available for this battery's model any more; pick another", await pool.SwapAsync(Who, draft.Id, "VPC8C05S-00003")); // the other battery's, in capitals
        Assert.Equal("VPC8C05s-00003", Text((await rig.Store.GetAsync<BatteryPack>(other.Id))!, "batterySerialNumber"));
        var line = (await rig.All("applicationAuditEvents", Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("eventType", BatteryIdPool.SwappedEvent))).Single()["message"].AsString;
        Assert.Equal("Draft battery VPC8C05s-00001 now has battery ID VPC8C05s-00002; VPC8C05s-00001 is available again", line);
    }

    [Fact]
    public async Task AnIdInAnyLetters_ShouldBeVoided_AndStayVoidedForEverySpelling()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", Serial));

        Assert.Null(await pool.VoidAsync(Who, "VPC8C05S-00001", "Label damaged"));

        Assert.Equal(ReleasedIdStatuses.Voided, (await pool.GetAsync(Serial))!.Status);
        Assert.Equal(PackCreationService.NotAvailable, (await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "vpc8c05s-00001"))).Problem);
        Assert.Equal("already released as VPC8C05s-00001, which is the same serial number (Voided, PO Core800s)", Assert.Single((await pool.ReleaseAsync(Who, List("PO again", "VPC8C05S-00001"))).Problems).Reason);
    }

    [Fact]
    public async Task ATestId_ShouldKeepTheSerialAsTyped_InItsIdToo_AndItsTestMarkInCapitals()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;

        var result = await pool.ReleaseAsync(Who, new ReleaseRequest("Trial", null, [], "vpc8c05s-90001", IsDemo: true));

        var item = Assert.Single(await pool.ListAsync(new ReleasedIdFilter(Reference: result.ReleaseId)));
        Assert.Equal(("vpc8c05s-90001", $"{Base}/25S/UN778474481-TEST-vpc8c05s-90001"), (item.SerialNumber, item.Identifier));
        Assert.Equal(DunsIdentifierScheme.StartsWithTest, Assert.Single((await pool.ReleaseAsync(Who, List("PO real", "test-90002"))).Problems).Reason);
    }

    [Fact]
    public async Task ASerialWithoutASmallLetter_ShouldBeReleasedExactlyAsBefore()
    {
        var (rig, pool, c7) = await SeededAsync(mongo);
        using var __ = rig;

        var result = await pool.ReleaseAsync(Who, List("PO Core700", "VPC7C05-00001", "VBC-92921"));

        var items = (await pool.ListAsync(new ReleasedIdFilter(Reference: result.ReleaseId))).ToDictionary(item => item.SerialNumber, item => (item.Id, item.Identifier));
        Assert.Equal(("VPC7C0500001", $"{Base}/25S/UN778474481-VPC7C05-00001"), items["VPC7C05-00001"]);
        Assert.Equal(("VBC92921", $"{Base}/25S/UN778474481-VBC-92921"), items["VBC-92921"]);
        var created = await rig.Services.GetRequiredService<PackCreationService>().CreateAsync(ChangeSender.Team(Who), NewBattery(c7, "VPC7C05-00001"));
        Assert.Equal("VPC7C05-00001", Text(created.Pack!, "batterySerialNumber"));
        Assert.Equal("already released (Used, PO Core700)", Assert.Single((await pool.ReleaseAsync(Who, List("PO again", "VPC7C05-00001"))).Problems).Reason);
    }

    [Fact]
    public async Task TheListsSearch_ShouldFindAReleasedId_HoweverItIsTyped()
    {
        var (rig, pool, _) = await SeededAsync(mongo);
        using var __ = rig;
        await pool.ReleaseAsync(Who, List("PO Core800s", Serial, "VPC7C05-00001"));

        foreach (var query in new[] { "VPC8C05s", "vpc8c05s-0", "VPC8C05S00001", "8c05S" })
        {
            Assert.Equal([Serial], (await pool.ListAsync(new ReleasedIdFilter(Query: query))).Select(item => item.SerialNumber));
        }
    }
}
