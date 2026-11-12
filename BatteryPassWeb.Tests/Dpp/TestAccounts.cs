using System.Security.Claims;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// A sign-in for a controller test, with the account record the app would have behind it: a session whose account is
/// gone has no roles (finding R03, point 6a).
/// </summary>
internal static class TestAccounts
{
    public static ClaimsPrincipal SignedIn(MongoContext context, string email, string role)
    {
        context.Database!.GetCollection<BsonDocument>("users").UpdateOne(
            Builders<BsonDocument>.Filter.Eq("email", ClusterRepository.NormalizeEmail(email)),
            Builders<BsonDocument>.Update.SetOnInsert("name", email).SetOnInsert("roles", new BsonArray { role }),
            new UpdateOptions { IsUpsert = true });
        return new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, role)], "test"));
    }
}
