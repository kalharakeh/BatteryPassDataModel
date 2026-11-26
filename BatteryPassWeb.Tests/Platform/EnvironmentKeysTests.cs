using System.Security.Cryptography;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// PL round two, point 2: a sign-in cookie or a form token made by the app in Development (the owner's PC, with the
/// fallback admin and the demo accounts) is not valid on the live site, even on the same database with the same signing
/// key, and the other way round. Each environment has its own keys and its own application name.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class EnvironmentKeysTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("Development", "Production", KeyRingTesting.CookiePurpose)]
    [InlineData("Development", "Production", KeyRingTesting.FormTokenPurpose)]
    [InlineData("Production", "Development", KeyRingTesting.CookiePurpose)]
    [InlineData("Staging", "Production", KeyRingTesting.CookiePurpose)]
    public void ACookieFromOneEnvironment_ShouldNotBeValidInAnother_OnOneDatabaseWithOneSigningKey(string madeIn, string readIn, string purpose)
    {
        var context = mongo.NewContext();
        using var maker = KeyRingTesting.Instance(context, madeIn);
        var ticket = KeyRingTesting.Protector(maker, purpose).Protect("admin@example.test");

        using var reader = KeyRingTesting.Instance(context, readIn);

        Assert.ThrowsAny<CryptographicException>(() => KeyRingTesting.Protector(reader, purpose).Unprotect(ticket));
        // control: the environment that made it still reads it
        using var sameEnvironment = KeyRingTesting.Instance(context, madeIn);
        Assert.Equal("admin@example.test", KeyRingTesting.Protector(sameEnvironment, purpose).Unprotect(ticket));
    }

    [Fact]
    public async Task EachEnvironment_ShouldKeepItsOwnKeys()
    {
        var context = mongo.NewContext();
        using var development = KeyRingTesting.Instance(context, "Development");
        using var production = KeyRingTesting.Instance(context, "Production");

        KeyRingTesting.Protector(development).Protect("x");
        KeyRingTesting.Protector(production).Protect("y");

        var keys = await context.Database!.GetCollection<BsonDocument>(MongoXmlRepository.CollectionName)
            .Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();
        Assert.Equal(["Development", "Production"], keys.Select(key => key["environment"].AsString).Order());
    }

    [Theory]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    [InlineData(" Production ")]
    public void TheEnvironmentsName_ShouldNotDependOnItsCapitals(string written)
    {
        var context = mongo.NewContext();
        using var site = KeyRingTesting.Instance(context, "Production");
        var ticket = KeyRingTesting.Protector(site).Protect("still signed in");

        using var sameSite = KeyRingTesting.Instance(context, written);

        Assert.Equal("still signed in", KeyRingTesting.Protector(sameSite).Unprotect(ticket));
        Assert.Throws<InvalidOperationException>(() => new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddBatteryPassDataProtection(written, null));
    }

    [Fact]
    public void TheApplicationName_ShouldNameTheEnvironment()
    {
        Assert.Equal("BatteryPass-Production", DataProtectionSetup.ApplicationName("Production"));
        Assert.Equal("BatteryPass-Development", DataProtectionSetup.ApplicationName("Development"));
    }
}
