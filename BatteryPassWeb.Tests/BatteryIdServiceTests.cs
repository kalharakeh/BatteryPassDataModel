using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class BatteryIdServiceTests
{
    private const string Secret = "local-development-id-generation-secret-32-bytes";

    [Fact]
    public void CreateBatteryId_ReturnsStableSeventyCharacterBase64Url()
    {
        var service = new BatteryIdService(Secret, allowMissingSecret: false);

        var first = service.CreateBatteryId("Compact 7M", "SN-001");
        var second = service.CreateBatteryId(" compact 7m ", " sn-001 ");

        Assert.Equal(first, second);
        Assert.Equal(70, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]{70}$", first);
    }

    [Fact]
    public void CreateBatteryId_ChangesWhenIdentityInputsChange()
    {
        var service = new BatteryIdService(Secret, allowMissingSecret: false);

        var original = service.CreateBatteryId("Compact 7M", "SN-001");

        Assert.NotEqual(original, service.CreateBatteryId("Core", "SN-001"));
        Assert.NotEqual(original, service.CreateBatteryId("Compact 7M", "SN-002"));
    }

    [Fact]
    public void CreatePassportId_UsesTimestampBatteryIdAndBatteryModel()
    {
        var service = new BatteryIdService(Secret, allowMissingSecret: false);
        var batteryId = service.CreateBatteryId("Compact 7M", "SN-001");
        var createdAt = DateTimeOffset.Parse("2026-05-12T10:15:30.1234567Z");

        var first = service.CreatePassportId(batteryId, "Model 2.0", createdAt);
        var second = service.CreatePassportId(batteryId, "Model 2.0", createdAt);

        Assert.Equal(first, second);
        Assert.Equal(70, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]{70}$", first);
        Assert.NotEqual(first, service.CreatePassportId(batteryId, "Model 3.0", createdAt));
        Assert.NotEqual(first, service.CreatePassportId(batteryId, "Model 2.0", createdAt.AddTicks(1)));
    }

    [Fact]
    public void Constructor_RejectsMissingSecretWhenFallbackNotAllowed()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new BatteryIdService(string.Empty, allowMissingSecret: false));

        Assert.Contains("ID_GENERATION_SECRET", exception.Message);
    }
}
