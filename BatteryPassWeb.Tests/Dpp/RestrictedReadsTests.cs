using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Finding R11: a reader who opens restricted data is told in one plain line who may use it and for what, and the app logs
/// the first opening of a battery's restricted data (or of a restricted document) per reader and day, without any value. The
/// team's own reads and a cluster admin's reads of their own batteries are not logged.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class RestrictedReadsTests(MongoFixture mongo)
{
    private static readonly string[] Sections = ["purchaser", "authorities"];

    private static async Task<(RestrictedReads Reads, MutableTimeProvider Clock, IMongoDatabase Database)> StartAsync(MongoFixture mongo)
    {
        var context = mongo.NewContext();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero));
        var reads = new RestrictedReads(context, new ApplicationAuditService(context), clock);
        await reads.EnsureIndexesAsync();
        return (reads, clock, context.Database!);
    }

    private static async Task<List<BsonDocument>> EntriesAsync(IMongoDatabase database) =>
        await database.GetCollection<BsonDocument>("applicationAuditEvents").Find(Builders<BsonDocument>.Filter.Eq("eventType", RestrictedReads.EventType)).ToListAsync();

    [Fact]
    public async Task TheFirstOpening_ShouldBeLoggedWithWhoRoleBatterySectionsAndTime_AndNoValue()
    {
        var (reads, _, database) = await StartAsync(mongo);

        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections));

        var entry = Assert.Single(await EntriesAsync(database));
        Assert.Equal("msa@example.test", entry["actor"].AsString);
        Assert.Equal("Market Surveillance Authorities", entry["actorRole"].AsString);
        Assert.Equal("pack-1", entry["entityId"].AsString);
        Assert.Equal("restricted", entry["category"].AsString);
        Assert.Equal(Sections, entry["metadata"]["sections"].AsBsonArray.Select(section => section.AsString));
        Assert.Equal("SN-0001", entry["metadata"]["battery"].AsString);
        Assert.Contains("SN-0001", entry["message"].AsString, StringComparison.Ordinal);
        Assert.True(entry.Contains("createdAt"));
        Assert.DoesNotContain("value", entry["metadata"].AsBsonDocument.Names.Select(name => name.ToLowerInvariant()));
    }

    [Fact]
    public async Task OpeningItAgainTheSameDay_ShouldAddNothing_AndTheNextDayAddsOne()
    {
        var (reads, clock, database) = await StartAsync(mongo);
        await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections);

        Assert.False(await reads.NoteBatteryAsync("MSA@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections));
        clock.Now = clock.Now.AddHours(10); // still the same UTC day
        Assert.False(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections));
        Assert.Single(await EntriesAsync(database));

        clock.Now = clock.Now.AddHours(10); // the next day
        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections));
        Assert.Equal(2, (await EntriesAsync(database)).Count);
    }

    [Fact]
    public async Task AnotherReaderOrAnotherBattery_ShouldBeLoggedSeparately()
    {
        var (reads, _, database) = await StartAsync(mongo);

        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections));
        Assert.True(await reads.NoteBatteryAsync("nb@example.test", "Notified Body", "pack-1", "SN-0001", Sections));
        Assert.True(await reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-2", "SN-0002", Sections));

        Assert.Equal(3, (await EntriesAsync(database)).Count);
    }

    [Fact]
    public async Task TwoOpeningsAtOnce_ShouldMakeOneEntry()
    {
        var (reads, _, database) = await StartAsync(mongo);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            reads.NoteBatteryAsync("msa@example.test", "Market Surveillance Authorities", "pack-1", "SN-0001", Sections)));

        Assert.Single(results, true);
        Assert.Single(await EntriesAsync(database));
    }

    [Fact]
    public async Task ARestrictedDocument_ShouldBeLoggedOncePerReaderAndDay_WithItsName()
    {
        var (reads, _, database) = await StartAsync(mongo);

        Assert.True(await reads.NoteDocumentAsync("nb@example.test", "Notified Body", "doc-1", "conformity.pdf", "pack-1"));
        Assert.False(await reads.NoteDocumentAsync("nb@example.test", "Notified Body", "doc-1", "conformity.pdf", "pack-1"));

        var entry = Assert.Single(await EntriesAsync(database));
        Assert.Equal("dpp-document", entry["entityType"].AsString);
        Assert.Equal("doc-1", entry["entityId"].AsString);
        Assert.Equal("conformity.pdf", entry["metadata"]["document"].AsString);
    }

    [Fact]
    public void TheTeamsOwnReadsAndAClusterAdminsOwnBatteries_ShouldNotBeLogged()
    {
        Assert.True(RestrictedReads.IsOwnData(ChangeSender.Team("team@example.test"), "north"));
        Assert.True(RestrictedReads.IsOwnData(ChangeSender.ClusterAdmin("admin@example.test", "north"), "North"));
        Assert.True(RestrictedReads.IsOwnData(ChangeSender.ClusterAdmin("admin@example.test", "north"), "north"));
        Assert.False(RestrictedReads.IsOwnData(ChangeSender.ClusterAdmin("admin@example.test", "north"), "south"));
        Assert.False(RestrictedReads.IsOwnData(ChangeSender.ClusterAdmin("admin@example.test", "north"), null));
        Assert.False(RestrictedReads.IsOwnData(null, "north")); // a market surveillance account, a notified body, a plain cluster member
    }

    [Fact]
    public void TheNotice_ShouldBeOneTextInOnePlace_NamingTheReadersRole()
    {
        var line = RestrictedReads.Notice("Notified Body");

        Assert.Equal("This data is shared with you under the Battery Regulation for your role as Notified Body; don't pass it on or keep it longer than you need it.", line);
        var views = Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(DppTestFiles.RepoFile("web", "Views", "Shared", "_Layout.cshtml")))!, "*.cshtml", SearchOption.AllDirectories);
        Assert.DoesNotContain(views, view => File.ReadAllText(view).Contains("don't pass it on", StringComparison.Ordinal));
        Assert.Contains("RestrictedReads.Notice(", File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Shared", "_RestrictedNotice.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public void TheNotice_ShouldBeInThePartialThatRendersARestrictedSection_NotInTheTabStrip()
    {
        var tabs = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Passport", "_PassportTabs.cshtml"));
        var strip = tabs[tabs.IndexOf("<ul class=\"nav nav-tabs", StringComparison.Ordinal)..tabs.IndexOf("</ul>", StringComparison.Ordinal)];

        Assert.DoesNotContain("_RestrictedNotice", strip, StringComparison.Ordinal);
        Assert.Equal(3, tabs.Split("_RestrictedNotice", StringSplitOptions.None).Length - 1); // live values, modules and each audience section
    }

    [Fact]
    public void TheTeamsAuditLog_ShouldHaveAFilterForRestrictedReads()
    {
        var page = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Admin", "AuditLog.cshtml"));

        Assert.Contains("@Option(\"restricted\", \"Restricted data opened\", filter.Category)", page, StringComparison.Ordinal);
    }
}
