using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Editing;

/// <summary>
/// Finding R08 (the manager's decision): a recycled battery's passport ceases to exist. For a battery marked for deletion,
/// the team removes the passport after typing the battery's ID: its versions, values (live values and their history
/// included), accident reports and their files, hand-overs and kept upload checks go. What stays is one audit entry (who,
/// when, the battery ID) and a marker for its IDs, so the ID is never used again and every address of the passport
/// answers "This battery was recycled. Its passport no longer exists." (410). Nothing removes a passport by itself.
/// </summary>
public sealed class PassportRemoval(DppStore store, DemoBoundary demo, DocumentStore documents, ApplicationAuditService audit, TimeProvider clock)
{
    public const string RemovedEvent = "dpp.passport-removed";

    /// <summary>What every address of a removed passport answers.</summary>
    public const string Gone = "This battery was recycled. Its passport no longer exists.";

    public const string OnlyTheTeam = "only the team removes a passport";
    public const string NotMarked = "only a passport marked for deletion after recycling can be removed";
    public const string TypeTheId = "type the battery's ID exactly as it is shown, to confirm";
    public const string NoBattery = "the battery was not found";

    /// <summary>The marker of a removed passport answering to this ID (a battery ID in either form, a passport ID, the pack); null when none.</summary>
    public Task<RemovedPassport?> FindAsync(string? id, CancellationToken cancellationToken = default) => store.FindRemovedAsync(id, cancellationToken);

    /// <summary>Removes the passport. Null when done; otherwise why not, as a refusal ("Not removed: …").</summary>
    public async Task<string?> RemoveAsync(ChangeSender sender, string packId, string? typedId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (sender.Kind != SenderKind.Team)
        {
            return OnlyTheTeam;
        }

        if (await store.GetAsync<BatteryPack>(packId, cancellationToken) is not { } pack || !await demo.ReachesAsync(sender, pack, cancellationToken))
        {
            return NoBattery;
        }

        var versions = await store.ListVersionsAsync(pack.Id, cancellationToken);
        if (versions.MaxBy(version => version.Number) is not { DppStatus: DppStatuses.MarkedForDeletion })
        {
            return NotMarked;
        }

        var batteryId = pack.Values.GetValueOrDefault("uniqueProductIdentifier")?.Value?.GetValue<string>() ?? string.Empty;
        var typed = typedId?.Trim() ?? string.Empty;
        if (batteryId.Length == 0 || typed.Length == 0
            || !(typed.Equals(batteryId, StringComparison.OrdinalIgnoreCase) || typed.Equals(IdentifierForms.Short(batteryId), StringComparison.OrdinalIgnoreCase)))
        {
            return TypeTheId;
        }

        // The marker first: from here on every address answers 410, also if a later step fails (the rest is put right by
        // removing again, which finds the pack, or leaves records nothing can reach).
        var none = CancellationToken.None;
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = new[] { pack.Id, batteryId, IdentifierForms.Short(batteryId) }
            .Concat(versions.SelectMany(version => new[] { version.DigitalProductPassportId, IdentifierForms.Short(version.DigitalProductPassportId) }))
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        await store.AddRemovedAsync(new RemovedPassport { Id = pack.Id, BatteryId = batteryId, Ids = ids, RemovedAtUtc = now, RemovedBy = sender.Who, IsDemo = pack.IsDemo }, none);

        // The pack next: everything that saves or publishes finds the battery by it, so nothing can write a version after.
        await store.DeleteAsync<BatteryPack>(pack.Id, none);
        await store.DeletePassportDataAsync(pack.Id, none); // versions, hand-overs, upload checks, the values' change history
        await documents.DeleteOwnedAsync(TargetKind.Pack, pack.Id, none); // accident reports and their files

        await audit.AppendApplicationAuditEventAsync(
            RemovedEvent,
            sender.Who,
            sender.Kind.ToString(),
            "form",
            $"Passport of battery {IdentifierForms.Short(batteryId)} removed after recycling.",
            new BsonDocument { ["batteryId"] = batteryId },
            entityType: "dpp-pack",
            entityId: pack.Id,
            batteryId: batteryId,
            cancellationToken: none);
        return null;
    }
}
