namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>Issues unique product identifiers (#6). The real EN 18219 scheme is chosen before go-live (decision 5, O-1).</summary>
public interface IIdentifierScheme
{
    string NewPackIdentifier();

    /// <summary>True for identifiers issued for testing; pages show a banner for these (design 10).</summary>
    bool IsTestIdentifier(string identifier);
}

/// <summary>
/// Test identifiers: a URN in a private namespace, marked TEST-, so they pass the absolute-URI check, can never be
/// mistaken for a registered identifier, and can be recognised later (plan judgement call).
/// </summary>
public sealed class TestIdentifierScheme : IIdentifierScheme
{
    public const string Prefix = "urn:x-battery-pass:TEST-";

    public string NewPackIdentifier() => $"{Prefix}{Guid.NewGuid():N}";

    public bool IsTestIdentifier(string identifier) =>
        identifier?.StartsWith(Prefix, StringComparison.Ordinal) == true;
}
