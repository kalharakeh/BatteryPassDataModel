using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class PassportStatusLabelTests
{
    [Fact]
    public void PassportRepositoryStatusLabel_ShouldShowAwaitingSignOffForDirtyState()
    {
        var document = new BsonDocument
        {
            ["registryInfo"] = new BsonDocument { ["status"] = "draft" },
            ["trust"] = new BsonDocument
            {
                ["state"] = "dirty",
                ["isDirty"] = true
            }
        };

        Assert.Equal("Awaiting sign-off", PassportRepository.BuildPassportStatusLabel(document));
    }
}
