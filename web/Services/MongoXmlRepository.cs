using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

/// <summary>
/// Where the keys of ASP.NET Core's data protection are kept (finding S09): the app's own MongoDB database. They protect
/// the sign-in cookie, the form tokens and the battery messages between two pages. Made anew at each start they signed
/// everybody out and broke every open form; kept here, a restart, a new deployment and a second instance all read the same
/// keys. This is the framework's own key ring on a store of our own: a small class, no package for it.
///
/// The keys are secrets, and they are stored encrypted with a secret made from the passport signing key (PL round two):
/// someone who can only read the database can't use them. Each document says which signing key it was encrypted for
/// ("cipher") and in which environment it was made; an app reads only the keys encrypted for its own signing key. In
/// Production an unencrypted key is never used, however old: an expired key still opens the cookies made with it. Nothing
/// reads this collection except this class, and nothing here writes a key anywhere else.
/// </summary>
public sealed class MongoXmlRepository(MongoContext mongoContext, string environment, string? cipherId, bool production) : IXmlRepository
{
    public const string CollectionName = "dataProtectionKeys";

    private IMongoCollection<BsonDocument> Keys =>
        (mongoContext.Database ?? throw new InvalidOperationException("The sign-in keys are kept in the database, and there is no database connection."))
            .GetCollection<BsonDocument>(CollectionName);

    public IReadOnlyCollection<XElement> GetAllElements() =>
        Keys.Find(ForThisApp()).ToList()
            .Select(Element)
            .OfType<XElement>()
            .Where(element => !production || !IsUnencryptedKey(element))
            .ToList();

    public void StoreElement(XElement element, string friendlyName)
    {
        // The same key written twice (two instances, or a retry) is still one document.
        var id = string.IsNullOrWhiteSpace(friendlyName) ? Guid.NewGuid().ToString("N") : friendlyName;
        var update = Builders<BsonDocument>.Update
            .SetOnInsert("xml", element.ToString(SaveOptions.DisableFormatting))
            .SetOnInsert("environment", environment)
            .SetOnInsert("createdAt", DateTime.UtcNow.ToString("O"));
        if (cipherId is not null)
        {
            update = update.SetOnInsert("cipher", cipherId);
        }

        Keys.UpdateOne(Builders<BsonDocument>.Filter.Eq("_id", id), update, new UpdateOptions { IsUpsert = true });
    }

    /// <summary>How many stored documents hold a key that is not encrypted: an earlier version's, or a developer's PC's.</summary>
    public int UnencryptedKeyCount() =>
        Keys.Find(Builders<BsonDocument>.Filter.Empty).ToList()
            .Select(Element)
            .Count(element => element is not null && IsUnencryptedKey(element));

    /// <summary>A stored document's key, or null when the document is damaged: one bad document must not stop the site.</summary>
    private static XElement? Element(BsonDocument document)
    {
        if (!document.TryGetValue("xml", out var xml) || !xml.IsString)
        {
            return null;
        }

        try
        {
            return XElement.Parse(xml.AsString);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// The keys of this environment encrypted for this signing key (PL round two, points 1 and 2): the owner's PC in
    /// Development and the live site on one database never share keys.
    /// </summary>
    private FilterDefinition<BsonDocument> ForThisApp() =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("environment", environment),
            cipherId is null
                ? Builders<BsonDocument>.Filter.Exists("cipher", false)
                : Builders<BsonDocument>.Filter.Eq("cipher", cipherId));

    /// <summary>A key whose secret part is not wrapped by <see cref="SigningKeyXmlEncryptor"/>. A revocation holds no secret.</summary>
    private static bool IsUnencryptedKey(XElement element) =>
        element.Name.LocalName == "key"
        && !element.Descendants().Any(child => child.Name.LocalName == SigningKeyXmlEncryptor.ElementName);
}

public static class DataProtectionSetup
{
    public const string NotEncryptedWarning =
        "The sign-in keys are stored in the database unencrypted, because " + DemoSigningKeyService.PrivateKeySetting
        + " isn't set. This is only allowed outside Production.";

    /// <summary>
    /// The name every cookie and form token is protected under, with the environment in it (PL round two, point 2): a cookie
    /// made on a developer's PC is not a cookie on the live site, even with the same keys.
    /// </summary>
    public static string ApplicationName(string environment) => $"BatteryPass-{environment}";

    /// <summary>
    /// The environment's name as the keys are filed under it: the host treats "production" and "Production" as one
    /// environment, so they must share one name here too, or a change of capitals would sign everyone out.
    /// </summary>
    public static string CanonicalEnvironment(string environmentName)
    {
        var trimmed = (environmentName ?? string.Empty).Trim();
        foreach (var known in new[] { "Production", "Development", "Staging" })
        {
            if (trimmed.Equals(known, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return trimmed;
    }

    /// <summary>A short, one-way name of the secret: which signing key a stored sign-in key belongs to. Not the secret.</summary>
    public static string CipherId(byte[] secret) =>
        Convert.ToHexString(SHA256.HashData([.. "BatteryPass sign-in key ring id "u8, .. secret]))[..16].ToLowerInvariant();

    /// <summary>
    /// Data protection with its keys in the database, encrypted with the secret from the signing key. Production must have
    /// the secret. The application name is fixed per environment, so that every instance and every deployment folder shares
    /// one set of keys.
    /// </summary>
    public static IServiceCollection AddBatteryPassDataProtection(this IServiceCollection services, string environmentName, byte[]? secret)
    {
        var environment = CanonicalEnvironment(environmentName);
        var production = environment == "Production";
        if (production && secret is null)
        {
            throw new InvalidOperationException(
                $"In Production the sign-in keys are encrypted with a key made from {DemoSigningKeyService.PrivateKeySetting}, and it isn't set or can't be read.");
        }

        var cipher = secret is null ? null : new KeyRingCipher(secret);
        if (cipher is not null)
        {
            services.AddSingleton(cipher);
        }

        services.AddDataProtection().SetApplicationName(ApplicationName(environment));
        services.AddOptions<KeyManagementOptions>().Configure<MongoContext>((options, mongoContext) =>
        {
            options.XmlRepository = new MongoXmlRepository(mongoContext, environment, cipher?.Id, production);
            if (cipher is not null)
            {
                options.XmlEncryptor = new SigningKeyXmlEncryptor(cipher);
            }
        });
        return services;
    }
}
