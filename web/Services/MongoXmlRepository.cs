using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Services;

/// <summary>
/// Where the keys of ASP.NET Core's data protection are kept (finding S09): the app's own MongoDB database. They protect
/// the sign-in cookie, the form tokens and the battery messages between two pages. Made anew at each start they signed
/// everybody out and broke every open form; kept here, a restart, a new deployment and a second instance all read the same
/// keys. This is the framework's own key ring on a store of our own: a small class, no package for it.
///
/// The keys are secrets. Nothing reads this collection except this class, and nothing here writes them to a log. A copy of
/// the database carries live sign-in keys with it.
/// </summary>
public sealed class MongoXmlRepository(MongoContext mongoContext) : IXmlRepository
{
    public const string CollectionName = "dataProtectionKeys";

    private IMongoCollection<BsonDocument> Keys =>
        (mongoContext.Database ?? throw new InvalidOperationException("The sign-in keys are kept in the database, and there is no database connection."))
            .GetCollection<BsonDocument>(CollectionName);

    public IReadOnlyCollection<XElement> GetAllElements() =>
        Keys.Find(Builders<BsonDocument>.Filter.Empty).ToList()
            .Select(document => XElement.Parse(document["xml"].AsString))
            .ToList();

    public void StoreElement(XElement element, string friendlyName)
    {
        // The same key written twice (two instances, or a retry) is still one document.
        var id = string.IsNullOrWhiteSpace(friendlyName) ? Guid.NewGuid().ToString("N") : friendlyName;
        Keys.UpdateOne(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update
                .SetOnInsert("xml", element.ToString(SaveOptions.DisableFormatting))
                .SetOnInsert("createdAt", DateTime.UtcNow.ToString("O")),
            new UpdateOptions { IsUpsert = true });
    }
}

public static class DataProtectionSetup
{
    /// <summary>
    /// Data protection with its keys in the database. The application name is fixed, so that every instance and every
    /// deployment folder shares one set of keys.
    /// </summary>
    public static IServiceCollection AddBatteryPassDataProtection(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName("BatteryPass");
        services.AddOptions<KeyManagementOptions>().Configure<MongoContext>((options, mongoContext) =>
            options.XmlRepository = new MongoXmlRepository(mongoContext));
        return services;
    }
}
