using System.Reflection;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiInitializerTests
{
    [Fact]
    public void BuildFallbackSampleDocument_ShouldUseScaniaManufacturer()
    {
        var method = typeof(ExternalApiInitializer).GetMethod(
            "BuildFallbackSampleDocument",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var document = Assert.IsType<BsonDocument>(method.Invoke(null, []));

        Assert.Equal(
            "Scania Industrial Batteries",
            document["app"]["display"]["manufacturerName"].AsString);
    }
}
