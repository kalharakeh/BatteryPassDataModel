using System.Security.Cryptography;
using System.Xml.Linq;
using BatteryPassWeb.Services;
using BatteryPassWeb.Tests.Dpp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>
/// PL round two, point 1: the sign-in keys in the database are encrypted with a key derived from the passport signing key,
/// which is not in the database. Someone who can only read the database can't make a sign-in cookie from them, and in
/// Production an unencrypted key is never used, whatever its age.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class KeyEncryptionTests(MongoFixture mongo)
{
    private static IMongoCollection<BsonDocument> Keys(MongoContext context) =>
        context.Database!.GetCollection<BsonDocument>(MongoXmlRepository.CollectionName);

    [Fact]
    public void TheSecret_ShouldComeFromTheSigningKey_WhateverItsEncoding()
    {
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(KeyRingTesting.SigningKey), out _);
        var reExported = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        var encrypted = Convert.ToBase64String(key.ExportEncryptedPkcs8PrivateKey("x"u8, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));

        var secret = DemoSigningKeyService.DataProtectionSecret(KeyRingTesting.SigningKey);

        Assert.NotNull(secret);
        Assert.Equal(32, secret!.Length);
        Assert.Equal(secret, DemoSigningKeyService.DataProtectionSecret($"  {reExported} "));
        Assert.NotEqual(secret, DemoSigningKeyService.DataProtectionSecret(KeyRingTesting.NewSigningKey()));
        Assert.Null(DemoSigningKeyService.DataProtectionSecret(null));
        Assert.Null(DemoSigningKeyService.DataProtectionSecret("   "));
        Assert.Null(DemoSigningKeyService.DataProtectionSecret("not base64 at all"));
        Assert.Null(DemoSigningKeyService.DataProtectionSecret(encrypted));
        // The secret is not the private key itself.
        Assert.NotEqual(key.ExportParameters(true).D, secret);
    }

    [Fact]
    public async Task AKeyWrittenInProduction_ShouldNotBeReadableInTheStoredDocument()
    {
        var context = mongo.NewContext();
        using var instance = KeyRingTesting.Instance(context);
        KeyRingTesting.Protector(instance).Protect("hello");

        var documents = await Keys(context).Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();

        var stored = Assert.Single(documents);
        var xml = XElement.Parse(stored["xml"].AsString);
        // The framework puts the wrapped secret where the plain "masterKey" with its "value" would be.
        Assert.DoesNotContain(xml.Descendants(), element => element.Name.LocalName is "masterKey" or "value");
        var wrapped = Assert.Single(xml.Descendants(), element => element.Name.LocalName == "encryptedSecret");
        Assert.StartsWith(typeof(SigningKeyXmlDecryptor).FullName + ",", (string?)wrapped.Attribute("decryptorType"));
        Assert.Equal(DataProtectionSetup.CipherId(DemoSigningKeyService.DataProtectionSecret(KeyRingTesting.SigningKey)!), stored["cipher"].AsString);
        Assert.DoesNotContain("unencrypted", stored["xml"].AsString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TwoInstancesWithTheSameSigningKey_ShouldReadEachOthersCookies_AndAfterARestart()
    {
        var context = mongo.NewContext();
        using var one = KeyRingTesting.Instance(context);
        var ticket = KeyRingTesting.Protector(one).Protect("team member");

        using var two = KeyRingTesting.Instance(context);
        using var restarted = KeyRingTesting.Instance(context);

        Assert.Equal("team member", KeyRingTesting.Protector(two).Unprotect(ticket));
        Assert.Equal("team member", KeyRingTesting.Protector(restarted).Unprotect(ticket));
        Assert.Equal("back", KeyRingTesting.Protector(one).Unprotect(KeyRingTesting.Protector(two).Protect("back")));
    }

    [Fact]
    public void AnInstanceWithAnotherSigningKey_ShouldRefuseTheCookies()
    {
        var context = mongo.NewContext();
        using var site = KeyRingTesting.Instance(context);
        var ticket = KeyRingTesting.Protector(site).Protect("team member");

        using var other = KeyRingTesting.Instance(context, signingKey: KeyRingTesting.NewSigningKey());

        Assert.ThrowsAny<CryptographicException>(() => KeyRingTesting.Protector(other).Unprotect(ticket));
        // and it still works for itself, with a key of its own
        Assert.Equal("own", KeyRingTesting.Protector(other).Unprotect(KeyRingTesting.Protector(other).Protect("own")));
    }

    [Fact]
    public async Task ACookieFromACopiedUnencryptedKey_ShouldBeRefusedInProduction()
    {
        var context = mongo.NewContext();
        using var site = KeyRingTesting.Instance(context);
        var own = KeyRingTesting.Protector(site).Protect("the site's own");

        // Someone who read an unencrypted key out of the database (an earlier version's, or one a Development app without a
        // signing key wrote) builds data protection around it with the live site's own application name and makes a cookie.
        var copied = new ListXmlRepository();
        var forger = new ServiceCollection().AddLogging();
        forger.AddDataProtection().SetApplicationName(DataProtectionSetup.ApplicationName("Production"));
        forger.Configure<KeyManagementOptions>(options => options.XmlRepository = copied);
        using var forgerServices = forger.BuildServiceProvider();
        var forged = forgerServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(KeyRingTesting.CookiePurpose).Protect("team admin");
        var element = Assert.Single(copied.GetAllElements());
        // as it would be found, and dressed up as one of the site's own encrypted keys
        foreach (var cipher in new BsonValue?[] { null, DataProtectionSetup.CipherId(DemoSigningKeyService.DataProtectionSecret(KeyRingTesting.SigningKey)!) })
        {
            var document = new BsonDocument
            {
                ["_id"] = $"copied-{Guid.NewGuid():N}",
                ["xml"] = element.ToString(SaveOptions.DisableFormatting),
                ["environment"] = "Production",
            };
            if (cipher is not null)
            {
                document["cipher"] = cipher;
            }

            await Keys(context).InsertOneAsync(document);
        }

        using var restarted = KeyRingTesting.Instance(context);

        Assert.ThrowsAny<CryptographicException>(() => KeyRingTesting.Protector(restarted).Unprotect(forged));
        Assert.Equal("the site's own", KeyRingTesting.Protector(restarted).Unprotect(own));
        Assert.Equal(2, new MongoXmlRepository(context, "Production", null, production: true).UnencryptedKeyCount());
    }

    [Fact]
    public async Task Development_WithoutASigningKey_ShouldKeepItsKeysAsBefore()
    {
        var context = mongo.NewContext();
        using var instance = KeyRingTesting.Instance(context, "Development", noSigningKey: true);

        var ticket = KeyRingTesting.Protector(instance).Protect("dev");
        using var restarted = KeyRingTesting.Instance(context, "Development", noSigningKey: true);

        Assert.Equal("dev", KeyRingTesting.Protector(restarted).Unprotect(ticket));
        var stored = Assert.Single(await Keys(context).Find(Builders<BsonDocument>.Filter.Empty).ToListAsync());
        Assert.False(stored.Contains("cipher"));
    }

    [Fact]
    public void Production_ShouldNotSetUpTheKeysWithoutASigningKey()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddBatteryPassDataProtection("Production", null));
    }

    [Fact]
    public void TheDecryptor_ShouldBeMadeByTheFramework_FromItsStoredTypeName()
    {
        var type = typeof(SigningKeyXmlDecryptor);

        Assert.True(type.IsPublic);
        Assert.NotNull(type.GetConstructor([typeof(IServiceProvider)]));
    }

    [Fact]
    public void ProgramCs_ShouldGiveTheKeysTheEnvironmentAndTheSigningKey_AndSayWhenTheyAreNotEncrypted()
    {
        var program = File.ReadAllText(DppTestFiles.RepoFile("web", "Program.cs"));

        Assert.Contains("AddBatteryPassDataProtection(builder.Environment.EnvironmentName, DemoSigningKeyService.DataProtectionSecret(", program, StringComparison.Ordinal);
        Assert.Contains("DataProtectionSetup.NotEncryptedWarning", program, StringComparison.Ordinal);
        Assert.Contains("UnencryptedKeyCount()", program, StringComparison.Ordinal);
    }

    /// <summary>A key store in memory: what someone would build from keys they read out of the database.</summary>
    private sealed class ListXmlRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];

        public IReadOnlyCollection<XElement> GetAllElements() => _elements.ToList();

        public void StoreElement(XElement element, string friendlyName) => _elements.Add(new XElement(element));
    }
}
