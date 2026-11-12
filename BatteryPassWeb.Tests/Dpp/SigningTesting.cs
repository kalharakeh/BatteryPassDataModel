using System.Security.Cryptography;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests.Dpp;

internal static class SigningTesting
{
    /// <summary>A throwaway signing key, so tests never create the app's local key file.</summary>
    public static readonly DemoSigningKeyService Keys = new(ECDsa.Create(ECCurve.NamedCurves.nistP256));
}
