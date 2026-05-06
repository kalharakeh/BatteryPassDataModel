using BatteryPassWeb.Models.Trust;
using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public sealed record PassportPublishDecision(
    bool CanSign,
    bool CanPublish,
    string PublishBlockReason);

public sealed class PassportPublishPolicyService
{
    public bool CanSign(TrustValidationSummary summary)
    {
        return summary.BlockingErrorCount == 0;
    }

    public PassportPublishDecision Evaluate(BsonDocument passport, TrustValidationSummary summary)
    {
        var canSign = CanSign(summary);
        if (!canSign)
        {
            return new PassportPublishDecision(
                CanSign: false,
                CanPublish: false,
                PublishBlockReason: "Resolve blocking validation errors before publishing.");
        }

        if (!HasCurrentValidSignature(passport))
        {
            return new PassportPublishDecision(
                CanSign: true,
                CanPublish: false,
                PublishBlockReason: "Publish requires a current valid signature proof.");
        }

        return new PassportPublishDecision(
            CanSign: true,
            CanPublish: true,
            PublishBlockReason: string.Empty);
    }

    public string NormalizeRegistryStatus(string requestedStatus, BsonDocument passport, TrustValidationSummary summary)
    {
        return requestedStatus.Trim().ToLowerInvariant() switch
        {
            "archived" => "archived",
            "published" => Evaluate(passport, summary).CanPublish ? "published" : "draft",
            _ => "draft"
        };
    }

    public void SanitizeTrustClaimsForDraftSave(BsonDocument passport)
    {
        var validation = EnsureDocument(passport, "validation");
        validation["isValid"] = false;
        validation["signedAt"] = BsonNull.Value;
        validation["hash"] = string.Empty;
        validation["signature"] = string.Empty;
        validation["proof"] = new BsonDocument();

        var trust = EnsureDocument(passport, "trust");
        trust["state"] = TrustState.Unvalidated;
        trust["isDirty"] = false;
        trust["latestHash"] = string.Empty;
        trust["latestProof"] = new BsonDocument();
        trust["lastSignedAt"] = BsonNull.Value;
    }

    public void InvalidateValidationClaimForDraftSave(BsonDocument passport)
    {
        var validation = EnsureDocument(passport, "validation");
        validation["isValid"] = false;
        validation["signedAt"] = BsonNull.Value;
        validation["hash"] = string.Empty;
        validation["signature"] = string.Empty;
        validation["proof"] = new BsonDocument();

        EnsureDocument(passport, "trust");
    }

    public bool HasCurrentValidSignature(BsonDocument passport)
    {
        if (!GetBoolean(passport, "validation", "isValid"))
        {
            return false;
        }

        var validationHash = BsonHelpers.GetString(passport, "validation", "hash");
        var latestHash = BsonHelpers.GetString(passport, "trust", "latestHash");
        if (string.IsNullOrWhiteSpace(validationHash)
            || string.IsNullOrWhiteSpace(latestHash)
            || !string.Equals(validationHash, latestHash, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(BsonHelpers.GetString(passport, "trust", "state"), TrustState.Signed, StringComparison.OrdinalIgnoreCase)
            || GetBoolean(passport, "trust", "isDirty"))
        {
            return false;
        }

        var proof = BsonHelpers.GetValue(passport, "trust", "latestProof") as BsonDocument;
        var proofValue = proof == null ? string.Empty : BsonHelpers.GetString(proof, "proofValue");
        return !string.IsNullOrWhiteSpace(proofValue);
    }

    private static bool GetBoolean(BsonDocument document, params string[] path)
    {
        var value = BsonHelpers.GetValue(document, path);
        return value is { IsBoolean: true } && value.AsBoolean;
    }

    private static BsonDocument EnsureDocument(BsonDocument parent, string key)
    {
        if (!parent.TryGetValue(key, out var value) || value is not BsonDocument document)
        {
            document = new BsonDocument();
            parent[key] = document;
        }

        return document;
    }
}
