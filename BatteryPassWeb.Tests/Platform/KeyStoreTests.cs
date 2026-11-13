using BatteryPassWeb.Configuration;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using V3Reset;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// Finding S09: the keys that protect sign-in cookies and form tokens live in the app's MongoDB database, so a restart or a
/// second instance reads the same keys and nobody is signed out. The keys are secrets: the reset tool and "Clear demo data"
/// leave them alone and nothing shows them.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class KeyStoreTests(MongoFixture mongo)
{
    private const string Purpose = "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler";

    private static ServiceProvider Instance(MongoContext context)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddBatteryPassDataProtection();
        return services.BuildServiceProvider();
    }

    private static IDataProtector Protector(ServiceProvider instance) =>
        instance.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

    [Fact]
    public async Task ASignIn_ShouldStillWork_AfterTheAppRestarts()
    {
        var context = mongo.NewContext();
        string ticket;
        using (var before = Instance(context))
        {
            ticket = Protector(before).Protect("the signed-in person");
        }

        using var after = Instance(context);

        Assert.Equal("the signed-in person", Protector(after).Unprotect(ticket));
        Assert.True(await context.Database!.GetCollection<BsonDocument>(MongoXmlRepository.CollectionName).CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty) >= 1);
    }

    [Fact]
    public async Task TwoInstances_ShouldReadEachOthersTickets_EvenWhenBothMadeAKeyAtTheSameMoment()
    {
        var context = mongo.NewContext();
        using var one = Instance(context);
        using var two = Instance(context);
        var gate = new ManualResetEventSlim();

        var made = new[] { one, two }.Select(instance => Task.Run(() =>
        {
            gate.Wait();
            return Protector(instance).Protect("hello");
        })).ToArray();
        gate.Set();
        var tickets = await Task.WhenAll(made);

        Assert.Equal("hello", Protector(one).Unprotect(tickets[1]));
        Assert.Equal("hello", Protector(two).Unprotect(tickets[0]));
    }

    [Fact]
    public void ADatabaseWithoutTheKeys_ShouldNotReadAnotherDatabasesTickets()
    {
        using var first = Instance(mongo.NewContext());
        using var second = Instance(mongo.NewContext());
        var ticket = Protector(first).Protect("secret");

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => Protector(second).Unprotect(ticket));
    }

    [Fact]
    public async Task TheStore_ShouldKeepAnElementOnceWhateverTimesItIsWritten()
    {
        var context = mongo.NewContext();
        var store = new MongoXmlRepository(context);
        var element = System.Xml.Linq.XElement.Parse("<key id=\"k1\"><value>v</value></key>");

        store.StoreElement(element, "key-k1");
        store.StoreElement(element, "key-k1");
        store.StoreElement(System.Xml.Linq.XElement.Parse("<revocation/>"), string.Empty);

        Assert.Equal(2, store.GetAllElements().Count);
        Assert.Contains(store.GetAllElements(), found => found.Attribute("id")?.Value == "k1");
        Assert.Equal(2, await context.Database!.GetCollection<BsonDocument>(MongoXmlRepository.CollectionName).CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
    }

    [Fact]
    public void TheKeyCollection_ShouldBeLeftAloneByTheResetTool()
    {
        Assert.Equal(CollectionFate.Kept, ResetCollections.FateOf(MongoXmlRepository.CollectionName));
    }

    [Fact]
    public async Task ClearDemoData_ShouldLeaveTheKeysAlone()
    {
        using var rig = await Demo.DemoRig.StartAsync(mongo);
        await rig.Demo.SeedAsync(Demo.DemoRig.Who);
        var keys = rig.Database.GetCollection<BsonDocument>(MongoXmlRepository.CollectionName);
        await keys.InsertOneAsync(new BsonDocument { ["_id"] = "key-test", ["xml"] = "<key/>" });

        await rig.Demo.ClearAsync(Demo.DemoRig.Who);
        await rig.Demo.ResetAsync(Demo.DemoRig.Who);

        Assert.Equal(1, await keys.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
    }

    [Fact]
    public void ProgramCs_ShouldUseTheStoreForTheKeys()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("AddBatteryPassDataProtection()", program, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStore_ShouldNeverWriteAKeyToALog()
    {
        var source = File.ReadAllText(DppTestFiles.RepoFile("web", "Services", "MongoXmlRepository.cs"));

        Assert.DoesNotContain("Log", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console", source, StringComparison.Ordinal);
    }
}
