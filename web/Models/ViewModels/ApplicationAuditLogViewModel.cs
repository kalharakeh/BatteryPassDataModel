using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Services;
using MongoDB.Bson;

namespace BatteryPassWeb.Models.ViewModels;

/// <summary>The audit log page (plan 2d-1): the events in plain English, by day.</summary>
public sealed class ApplicationAuditLogViewModel
{
    public ApplicationAuditFilter Filter { get; init; } = new();
    public IReadOnlyList<BsonDocument> Events { get; init; } = [];

    /// <summary>The events in plain English, by day, newest first.</summary>
    public IReadOnlyList<AuditDay> Days { get; init; } = [];

    /// <summary>How many events the page shows; "Show 50 more" asks for 50 more.</summary>
    public int Show { get; init; } = 50;

    public bool HasMore { get; init; }

    public TimeZoneInfo Zone { get; init; } = TimeZoneInfo.Local;
}
