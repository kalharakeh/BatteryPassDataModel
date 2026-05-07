namespace BatteryPassWeb.Models.Demo;

public static class DemoScenarioKey
{
    public const string PublishedTrusted = "publishedTrusted";
    public const string DraftIncomplete = "draftIncomplete";
    public const string ReadyToSign = "readyToSign";
    public const string SignedUnpublished = "signedUnpublished";
    public const string DirtyAfterEdit = "dirtyAfterEdit";
    public const string InvalidSignature = "invalidSignature";
    public const string RestrictedDocument = "restrictedDocument";
}

public sealed record DemoScenarioDefinition(
    string Key,
    string PassportId,
    string Label,
    string ExpectedState,
    string ClusterId);

public sealed record DemoScenarioResetResult(
    int ResetCount,
    IReadOnlyList<string> PassportIds,
    string ResetAt);

public static class DemoScenarioCatalog
{
    public const string DefaultClusterId = "cluster-north-operations";
    public const string PublishedTrustedPassportId = "did:web:acme.battery.pass:demo-published-trusted-001";
    public const string DraftIncompletePassportId = "did:web:acme.battery.pass:demo-draft-incomplete-001";
    public const string ReadyToSignPassportId = "did:web:acme.battery.pass:demo-ready-to-sign-001";
    public const string SignedUnpublishedPassportId = "did:web:acme.battery.pass:demo-signed-unpublished-001";
    public const string DirtyAfterEditPassportId = "did:web:acme.battery.pass:demo-dirty-after-edit-001";
    public const string InvalidSignaturePassportId = "did:web:acme.battery.pass:demo-invalid-signature-001";
    public const string RestrictedDocumentPassportId = "did:web:acme.battery.pass:demo-restricted-document-001";

    public static IReadOnlyList<DemoScenarioDefinition> All { get; } =
    [
        new(DemoScenarioKey.PublishedTrusted, PublishedTrustedPassportId, "Published trusted", "Published, signed, clean, public, QR-ready", DefaultClusterId),
        new(DemoScenarioKey.DraftIncomplete, DraftIncompletePassportId, "Draft incomplete", "Missing required data, blocked from signing", DefaultClusterId),
        new(DemoScenarioKey.ReadyToSign, ReadyToSignPassportId, "Ready to sign", "Complete and validated, unsigned", DefaultClusterId),
        new(DemoScenarioKey.SignedUnpublished, SignedUnpublishedPassportId, "Signed unpublished", "Signed and clean, not public yet", DefaultClusterId),
        new(DemoScenarioKey.DirtyAfterEdit, DirtyAfterEditPassportId, "Dirty after edit", "Signed core changed after proof", DefaultClusterId),
        new(DemoScenarioKey.InvalidSignature, InvalidSignaturePassportId, "Invalid signature", "Proof diagnostics fail verification", DefaultClusterId),
        new(DemoScenarioKey.RestrictedDocument, RestrictedDocumentPassportId, "Restricted document access", "Published trusted with restricted evidence metadata", DefaultClusterId)
    ];

    public static bool IsKnownPassportId(string passportId)
    {
        return All.Any(scenario => scenario.PassportId.Equals(passportId, StringComparison.OrdinalIgnoreCase));
    }
}
