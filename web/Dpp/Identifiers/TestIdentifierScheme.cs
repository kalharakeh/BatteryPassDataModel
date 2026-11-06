using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>The battery ID (#6) a battery gets when it is created, or why it can't get one.</summary>
public sealed record NewIdentifier(string? Identifier, string? Problem)
{
    public static NewIdentifier Refused(string problem) => new(null, problem);
}

/// <summary>
/// Issues the battery ID (#6) and the passport IDs (#5). The scheme is chosen by settings (O-1, step 6d): an ISO/IEC 15459
/// item ID under issuing agency UN (DUNS) made from the battery's serial number, as a web address, or, without those
/// settings, test IDs. A battery gets its #6 when it is created (the owner, 5 Nov 2026).
/// </summary>
public interface IIdentifierScheme
{
    /// <summary>The battery ID (#6) <paramref name="pack"/> gets, or why it can't get one.</summary>
    Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default);

    /// <summary>
    /// The battery ID a serial number makes, checked on its own (its letters and digits, its length, TEST). Whether another
    /// battery has it is the caller's check.
    /// </summary>
    NewIdentifier ItemFor(string serialNumber, bool demo);

    /// <summary>A new passport ID (#5) for version <paramref name="version"/> of the battery <paramref name="packIdentifier"/>.</summary>
    string NewPassportIdentifier(string packIdentifier, int version);

    /// <summary>True for identifiers issued for testing; pages show a "Test passport" tag for these (design 10).</summary>
    bool IsTestIdentifier(string identifier);
}

/// <summary>
/// Test identifiers: a URN in a private namespace, marked TEST-, so they pass the absolute-URI check, can never be
/// mistaken for a registered identifier, and can be recognised later (plan judgement call). Used while the identifier
/// settings aren't given.
/// </summary>
public sealed class TestIdentifierScheme : IIdentifierScheme
{
    public const string Prefix = "urn:x-battery-pass:TEST-";

    public Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NewIdentifier($"{Prefix}{Guid.NewGuid():N}", null));

    public NewIdentifier ItemFor(string serialNumber, bool demo) =>
        IdentifierForms.Clean(serialNumber).Length == 0 ? NewIdentifier.Refused(DunsIdentifierScheme.NoLettersOrDigits) : new NewIdentifier($"{Prefix}{Guid.NewGuid():N}", null);

    public string NewPassportIdentifier(string packIdentifier, int version) => $"{Prefix}DPP-{Guid.NewGuid():N}";

    public bool IsTestIdentifier(string identifier) => IdentifierForms.IsTest(identifier);
}
