using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Publication;

/// <summary>A published passport as readers see it, with the version it comes from.</summary>
public sealed record PublishedPassport(PassportVersion Version, JsonObject Passport);

/// <summary>
/// Reads published passports (design 4.2): the active one per pack, or any version by its #5. Visibility filtering is
/// step 3's job; this returns the whole passport.
/// </summary>
public sealed class PassportReader(DppStore store)
{
    /// <summary>The pack's current passport: the active version's signed core, its #2 and #4, and the live values.</summary>
    public async Task<PublishedPassport?> CurrentAsync(string packId, CancellationToken cancellationToken = default)
    {
        var version = await store.GetActiveVersionAsync(packId, cancellationToken);
        return version is null ? null : await ComposeAsync(version, cancellationToken);
    }

    /// <summary>
    /// The version with this passport ID (#5). An archived version comes without live values (they aren't versioned),
    /// so it lacks the required live set; that is expected and must not be "fixed" with today's live values.
    /// </summary>
    public async Task<PublishedPassport?> VersionAsync(string passportId, CancellationToken cancellationToken = default)
    {
        var version = await store.GetVersionByPassportIdAsync(passportId, cancellationToken);
        return version is null ? null : await ComposeAsync(version, cancellationToken);
    }

    /// <summary>True when the stored core is exactly what was signed at publication.</summary>
    public static bool Verify(PassportVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return PassportSigner.Verify(version.StaticCore, version.Proof);
    }

    private async Task<PublishedPassport> ComposeAsync(PassportVersion version, CancellationToken cancellationToken)
    {
        var liveValues = IsNewest(version.DppStatus)
            ? (await store.GetAsync<BatteryPack>(version.PackId, cancellationToken))?.Values
            : null;
        return new PublishedPassport(version, PassportStaticCore.Compose(version.StaticCore, version.DppStatus, version.LastUpdatedUtc, liveValues));
    }

    /// <summary>
    /// The battery's newest passport carries its live values: the active one, or one a mark has frozen (invalid, marked
    /// for deletion), which keeps the last values it had (redesign R6). An older version of the same passport (archived)
    /// and a passport a newer one replaced (inactive) have none.
    /// </summary>
    private static bool IsNewest(string status) =>
        status is DppStatuses.Active or DppStatuses.Invalid or DppStatuses.MarkedForDeletion;
}
