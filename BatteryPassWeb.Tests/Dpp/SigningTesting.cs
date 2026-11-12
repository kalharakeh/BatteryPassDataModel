using System.Security.Cryptography;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

internal static class SigningTesting
{
    /// <summary>A throwaway signing key, so tests never create the app's local key file.</summary>
    public static readonly DemoSigningKeyService Keys = new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    /// <summary>A passport reader that trusts <see cref="Keys"/>, as the app trusts its own key.</summary>
    public static PassportReader Passports(DppStore store) => new(store, new PassportSigner(Keys));
}
