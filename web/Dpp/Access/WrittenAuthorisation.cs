using System.Globalization;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>A cluster's written authorisation to update passport data: the date it was given, who recorded it and when.</summary>
public sealed record ClusterAuthorisation(DateOnly On, string By, DateTime? RecordedAtUtc);

/// <summary>
/// Finding R20 (the owner's decision, 10 Nov 2026): the operator authorises a cluster in writing before its admins and
/// tokens change passport data (status, re-stated values, live values, accident reports, hand-overs, uploads). The team
/// records it on the cluster. Without it they read as before and change nothing. The one rule; <see cref="DemoBoundary"/>
/// reads the cluster for it, and every way to change a battery asks there.
/// </summary>
public static class WrittenAuthorisation
{
    /// <summary>The cluster record's field that holds the authorisation; a cluster without it isn't authorised.</summary>
    public const string Field = "writtenAuthorisation";

    /// <summary>The one line a cluster admin or a token of an unauthorised cluster meets on its pages and from the API.</summary>
    public const string Missing = "The Scania team hasn't recorded your authorisation to update passport data yet.";

    /// <summary>The same line as a refusal, in the services' words ("Not asked: the Scania team hasn't …").</summary>
    public const string Refusal = "the Scania team hasn't recorded your authorisation to update passport data yet";

    /// <summary>A hand-over waiting for an answer whose sending cluster lost its authorisation meanwhile (round two, 3).</summary>
    public const string GiverMissing = "The sending cluster's authorisation to update passport data isn't recorded any more, so the battery can't move yet.";

    public const string GiverRefusal = "the sending cluster's authorisation to update passport data isn't recorded any more, so the battery can't move yet";

    /// <summary>Whether this sender needs its battery's cluster to be authorised: cluster admins and battery system tokens.</summary>
    public static bool Needed(ChangeSender sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.Kind is SenderKind.ClusterAdmin or SenderKind.BatterySystemFeed;
    }

    /// <summary>
    /// The cluster's authorisation, or null. A cluster with no record, or a record from before this change, has none:
    /// the team records it (the owner's decision: new clusters start without it).
    /// </summary>
    public static ClusterAuthorisation? Of(BsonDocument? cluster)
    {
        if (cluster?.GetValue(Field, BsonNull.Value) is not BsonDocument record
            || !DateOnly.TryParseExact(BsonHelpers.GetString(record, "on"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var on))
        {
            return null;
        }

        return new ClusterAuthorisation(on, BsonHelpers.GetString(record, "by"), BsonHelpers.GetDateTime(record, "at"));
    }

    /// <summary>Null when the sender may change a battery of this cluster; otherwise <see cref="Refusal"/>.</summary>
    public static string? Problem(ChangeSender sender, BsonDocument? batteryCluster) =>
        !Needed(sender) || Of(batteryCluster) is not null ? null : Refusal;
}
