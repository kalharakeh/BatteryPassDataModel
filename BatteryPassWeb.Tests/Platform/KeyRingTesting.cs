using System.Security.Cryptography;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryPassWeb.Tests.Platform;

/// <summary>An app's data protection on a database, as Program.cs sets it up, for the key store's tests.</summary>
internal static class KeyRingTesting
{
    public const string CookiePurpose = "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler";
    public const string FormTokenPurpose = "Microsoft.AspNetCore.Antiforgery.AntiforgeryToken.v1";

    /// <summary>A signing key as the setting holds it: base64 PKCS#8, P-256.</summary>
    public static string NewSigningKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Convert.ToBase64String(key.ExportPkcs8PrivateKey());
    }

    public static readonly string SigningKey = NewSigningKey();

    public static ServiceProvider Instance(MongoContext context, string environment = "Production", string? signingKey = null, bool noSigningKey = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddBatteryPassDataProtection(environment, noSigningKey ? null : DemoSigningKeyService.DataProtectionSecret(signingKey ?? SigningKey));
        return services.BuildServiceProvider();
    }

    public static IDataProtector Protector(ServiceProvider instance, string purpose = CookiePurpose) =>
        instance.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose);
}
