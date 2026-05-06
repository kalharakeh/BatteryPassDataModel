using BatteryPassWeb.Models.Trust;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PublicVisibilityPolicyTests
{
    [Fact]
    public void PublicVisibility_ShouldRequirePublishedCleanSignedCurrentProof()
    {
        var service = new PassportPublishPolicyService();

        Assert.True(service.IsPubliclyVisible(ReadyPassport()));
        Assert.False(service.IsPubliclyVisible(ReadyPassport(status: "draft")));
        Assert.False(service.IsPubliclyVisible(ReadyPassport(status: "published", trustState: TrustState.Unvalidated)));
        Assert.False(service.IsPubliclyVisible(ReadyPassport(status: "published", isDirty: true)));
        Assert.False(service.IsPubliclyVisible(ReadyPassport(status: "published", latestHash: "different-hash")));
        Assert.False(service.IsPubliclyVisible(ReadyPassport(status: "published", proofValue: string.Empty)));
    }

    private static BsonDocument ReadyPassport(
        string status = "published",
        string trustState = TrustState.Signed,
        bool isDirty = false,
        string validationHash = "hash-123",
        string latestHash = "hash-123",
        string proofValue = "proof-123")
    {
        return new BsonDocument
        {
            ["registryInfo"] = new BsonDocument
            {
                ["status"] = status
            },
            ["validation"] = new BsonDocument
            {
                ["isValid"] = true,
                ["hash"] = validationHash
            },
            ["trust"] = new BsonDocument
            {
                ["state"] = trustState,
                ["isDirty"] = isDirty,
                ["latestHash"] = latestHash,
                ["latestProof"] = new BsonDocument
                {
                    ["proofValue"] = proofValue
                }
            }
        };
    }
}
