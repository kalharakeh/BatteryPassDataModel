using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Signing;

namespace BatteryPassWeb.Dpp.Assembly;

/// <summary>
/// The public part of a passport as data (finding R07, the owner's decision of 10 Nov 2026): the values anyone may see,
/// cut by the one who-sees-what rule (<see cref="PassportFilter"/> for an anonymous reader), in the official format's
/// structure and names. Values the public may not see are left out, so it is not a full passport.
/// </summary>
public static class PublicPassport
{
    /// <summary>The property that carries the proof over the public part; null for a version published before it existed.</summary>
    public const string ProofProperty = "proof";

    /// <summary>The public values of a passport (or of a version's signed core).</summary>
    public static JsonObject Cut(JsonObject passport) => PassportFilter.Cut(passport, Reader.Anonymous, []);

    /// <summary>
    /// The part of the public data the public proof signs: the public values without the DPP status (#2), the last update
    /// (#4), the live values and the proof itself, with any object left empty removed. The same from the version's signed
    /// core at publication and from the data a reader gets later.
    /// </summary>
    public static JsonObject SignedPart(JsonObject publicData)
    {
        ArgumentNullException.ThrowIfNull(publicData);
        var copy = publicData.DeepClone().AsObject();
        copy.Remove(ProofProperty);
        var part = PassportStaticCore.Extract(copy);
        Prune(part);
        return part;
    }

    /// <summary>
    /// The public data as served: the public values as cut now, and the public proof, or <c>"proof": null</c> when there is
    /// none or it doesn't hold for these values (the caller checks it).
    /// </summary>
    public static JsonObject Data(JsonObject publicValues, SignatureProof? publicProof)
    {
        ArgumentNullException.ThrowIfNull(publicValues);
        var data = publicValues.DeepClone().AsObject();
        data[ProofProperty] = publicProof is null ? null : ProofJson(publicProof);
        return data;
    }

    /// <summary>A proof in the data's words: the same shape the app stores, with camel-case names.</summary>
    public static JsonObject ProofJson(SignatureProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        return new JsonObject
        {
            ["type"] = proof.Type,
            ["cryptosuite"] = proof.Cryptosuite,
            ["created"] = PassportAssembler.FormatTimestamp(proof.CreatedUtc),
            ["issuer"] = proof.Issuer,
            ["verificationMethod"] = proof.VerificationMethod,
            ["hash"] = proof.Hash,
            ["proofValue"] = proof.ProofValue,
            ["publicKeyX"] = proof.PublicKeyX,
            ["publicKeyY"] = proof.PublicKeyY,
        };
    }

    private static void Prune(JsonObject node)
    {
        foreach (var (name, child) in node.ToArray())
        {
            if (child is JsonObject inner)
            {
                Prune(inner);
                if (inner.Count == 0)
                {
                    node.Remove(name);
                }
            }
        }
    }
}
