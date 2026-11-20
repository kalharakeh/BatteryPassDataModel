namespace V3Reset;

/// <summary>What the reset does with a collection of battery_pass_v3 (design 14, 3d-2).</summary>
public enum CollectionFate
{
    /// <summary>The new model's test data: emptied; the app keeps the collections and their indexes.</summary>
    NewTestData,

    /// <summary>The v1.2 app's data, which nothing reads since 3d-1: dropped.</summary>
    OldData,

    /// <summary>Short-lived records: emptied.</summary>
    Cleared,

    /// <summary>Kept; the decisions file says which accounts, clusters and tokens go.</summary>
    Reviewed,

    /// <summary>Kept as they are.</summary>
    Kept,

    /// <summary>A collection the app doesn't know: listed and left alone.</summary>
    Unknown,
}

/// <summary>The collections the reset knows, by what it does with them.</summary>
public static class ResetCollections
{
    /// <summary>The only database the tool works on.</summary>
    public const string Database = "battery_pass_v3";

    /// <summary>The live app's database, refused by name.</summary>
    public const string LiveDatabase = "battery_pass_demo";

    private static readonly Dictionary<string, CollectionFate> Fates = new(StringComparer.Ordinal)
    {
        ["dpp_families"] = CollectionFate.NewTestData,
        ["dpp_models"] = CollectionFate.NewTestData,
        ["dpp_sites"] = CollectionFate.NewTestData,
        ["dpp_batches"] = CollectionFate.NewTestData,
        ["dpp_operators"] = CollectionFate.NewTestData,
        ["dpp_packs"] = CollectionFate.NewTestData,
        ["dpp_passport_versions"] = CollectionFate.NewTestData,
        ["dpp_transfers"] = CollectionFate.NewTestData,
        ["dpp_uploads"] = CollectionFate.NewTestData,
        ["dpp_upload_checks"] = CollectionFate.NewTestData,
        ["dpp_battery_ids"] = CollectionFate.NewTestData,
        ["dpp_removed_passports"] = CollectionFate.NewTestData, // finding R08: the markers of removed passports go with the test data
        ["dpp_documents"] = CollectionFate.NewTestData,
        ["dpp_files.files"] = CollectionFate.NewTestData,
        ["dpp_files.chunks"] = CollectionFate.NewTestData,
        ["passports"] = CollectionFate.OldData,
        ["batteries"] = CollectionFate.OldData,
        ["passportRevisions"] = CollectionFate.OldData,
        ["batteryProductTemplates"] = CollectionFate.OldData,
        ["batteryProductTemplateVersions"] = CollectionFate.OldData,
        ["batteryProductTemplatePushRuns"] = CollectionFate.OldData,
        ["batteryProductCompletionPolicies"] = CollectionFate.OldData,
        ["dataCompletionPolicies"] = CollectionFate.OldData,
        ["editableFieldPolicies"] = CollectionFate.OldData,
        ["batteryTelemetry"] = CollectionFate.OldData,
        ["passportFiles.files"] = CollectionFate.OldData,
        ["passportFiles.chunks"] = CollectionFate.OldData,
        ["passwordResetRequests"] = CollectionFate.Cleared,
        ["signInAttempts"] = CollectionFate.Cleared, // finding W4: the count of wrong passwords and the locks
        ["restrictedReads"] = CollectionFate.Cleared, // finding R11: the keys that keep one log entry per reader and day
        ["users"] = CollectionFate.Reviewed,
        ["clusterMemberships"] = CollectionFate.Reviewed,
        ["clusters"] = CollectionFate.Reviewed,
        ["apiTokens"] = CollectionFate.Reviewed,
        ["applicationSettings"] = CollectionFate.Kept,
        ["applicationAuditEvents"] = CollectionFate.Kept,
        ["auditEvents"] = CollectionFate.Kept,
        ["batteryAuditEvents"] = CollectionFate.Kept,
        ["dataProtectionKeys"] = CollectionFate.Kept, // finding S09: the keys of every sign-in and open form; a reset never touches them
    };

    public static CollectionFate FateOf(string collection) =>
        Fates.TryGetValue(collection, out var fate) ? fate : CollectionFate.Unknown;

    public static string Describe(CollectionFate fate) => fate switch
    {
        CollectionFate.NewTestData => "emptied (new test data)",
        CollectionFate.OldData => "removed (the old app's data)",
        CollectionFate.Cleared => "emptied (short-lived)",
        CollectionFate.Reviewed => "kept, as your decisions say",
        CollectionFate.Kept => "kept",
        _ => "left alone (not known to the app)",
    };
}
