using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class ExternalApiSecurityServiceTests
{
    private const string EncryptionKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void CreateToken_ShouldReturn32Characters()
    {
        var service = new ExternalApiSecurityService(EncryptionKey);

        var token = service.CreateToken();

        Assert.Equal(32, token.Length);
        Assert.Matches("^[A-Za-z0-9]+$", token);
    }

    [Fact]
    public void HashAndVerify_ShouldValidateMatchingValue()
    {
        var service = new ExternalApiSecurityService(EncryptionKey);

        var hash = service.HashSecret("abc123");

        Assert.True(service.VerifySecret("abc123", hash));
        Assert.False(service.VerifySecret("different", hash));
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldRoundTripValue()
    {
        var service = new ExternalApiSecurityService(EncryptionKey);

        var encrypted = service.Encrypt("token-value-1");
        var decrypted = service.Decrypt(encrypted);

        Assert.Equal("token-value-1", decrypted);
    }
}
