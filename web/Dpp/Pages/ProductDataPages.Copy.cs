using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;

namespace BatteryPassWeb.Dpp.Pages;

/// <summary>"Copy as real" on a demo site or operator (decision 66): where it posts and the name it suggests.</summary>
public sealed record RealCopyView(string Action, string KindWords, string Name, string SuggestedName);

/// <summary>
/// Real product data made from the demo data (decision 66): a demo family with its models, batches, documents and the
/// sites they use, or one demo site or operator, copied without the demo mark. The copies get new IDs and their own
/// files, so clearing the demo data never takes anything from them; the demo data is left as it is. Editing a demo record
/// never makes it real (decision 45): this copy is the way.
/// </summary>
public sealed partial class ProductDataPages
{
    public const string AlreadyReal = "it is already real product data";

    /// <summary>
    /// The real copy's name: the demo name with " (demo)" or "Demo " left out, else the same name with " (real)".
    /// </summary>
    public static string RealCopyName(string demoName)
    {
        var name = (demoName ?? string.Empty).Trim();
        string? stripped = null;
        if (name.EndsWith(" (demo)", StringComparison.OrdinalIgnoreCase))
        {
            stripped = name[..^" (demo)".Length].Trim();
        }
        else if (name.StartsWith("Demo ", StringComparison.OrdinalIgnoreCase))
        {
            stripped = name["Demo ".Length..].Trim();
            stripped = stripped.Length == 0 ? stripped : char.ToUpperInvariant(stripped[0]) + stripped[1..];
        }

        return string.IsNullOrEmpty(stripped) ? $"{name} (real)" : stripped;
    }

    /// <summary>
    /// Copies a demo family as real product data under a new name: every model (values, module description, picture,
    /// uploaded documents) and every batch of those models (values, uploaded documents), each demo site they use replaced
    /// by the real site of the same name or a real copy of it. No batteries. The new family's id, or the problem.
    /// </summary>
    public async Task<(string? FamilyId, string? Problem)> CopyFamilyAsRealAsync(string who, string familyId, string? name, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<BatteryFamily>(familyId, cancellationToken) is not { } demo)
        {
            return (null, "the battery family was not found");
        }

        if (!demo.IsDemo)
        {
            return (null, AlreadyReal);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, "give the new battery family a name");
        }

        if (await NameTakenAsync(RecordKind.Family, name, cancellationToken: cancellationToken) is { } taken)
        {
            return (null, taken);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var family = new BatteryFamily { Id = NewId("family"), Name = name.Trim(), PictureContentType = demo.PictureContentType };
        if (demo.PictureFileId is { } familyPicture)
        {
            family.PictureFileId = await CopyFileAsync(familyPicture, cancellationToken);
        }

        await store.InsertAsync(family, cancellationToken);

        var sites = new Dictionary<string, string>(StringComparer.Ordinal); // a demo site's id → the real site's id
        var sitesMade = new List<string>();
        var models = 0;
        var batches = 0;
        foreach (var source in await store.ListModelsAsync(demo.Id, cancellationToken))
        {
            var modelId = NewId("model");
            var documentIds = await CopyDocumentsAsync(TargetKind.Model, source.Id, modelId, cancellationToken);
            var model = new BatteryModel
            {
                Id = modelId,
                FamilyId = family.Id,
                Name = source.Name,
                Values = CopyValues(source.Values, documentIds, who, now),
                ModuleDescription = source.ModuleDescription,
                PictureContentType = source.PictureContentType,
            };
            if (source.PictureFileId is { } modelPicture)
            {
                model.PictureFileId = await CopyFileAsync(modelPicture, cancellationToken);
            }

            await store.InsertAsync(model, cancellationToken);
            models++;
            foreach (var sourceBatch in await store.ListBatchesAsync(source.Id, cancellationToken))
            {
                if (!sites.TryGetValue(sourceBatch.SiteId, out var siteId))
                {
                    (siteId, var made) = await RealSiteForAsync(who, sourceBatch.SiteId, now, cancellationToken);
                    sites[sourceBatch.SiteId] = siteId;
                    if (made is not null)
                    {
                        sitesMade.Add(made);
                    }
                }

                var batchId = NewId("batch");
                var batchDocuments = await CopyDocumentsAsync(TargetKind.Batch, sourceBatch.Id, batchId, cancellationToken);
                await store.InsertAsync(
                    new ProductionBatch { Id = batchId, ModelId = model.Id, SiteId = siteId, Year = sourceBatch.Year, Values = CopyValues(sourceBatch.Values, batchDocuments, who, now) },
                    cancellationToken);
                batches++;
            }
        }

        var siteWords = sitesMade.Count switch
        {
            0 => string.Empty,
            1 => $" and the site {sitesMade[0]}",
            _ => $" and the sites {string.Join(", ", sitesMade)}",
        };
        await AuditAsync(
            CreatedEvent,
            who,
            $"Battery family {family.Name} created as a copy of {demo.Name} (demo data), with {Words.Count(models, "model")}, {Words.Count(batches, "batch", "batches")}{siteWords}",
            "dpp-family",
            family.Id,
            cancellationToken);
        return (family.Id, null);
    }

    /// <summary>Copies a demo manufacturing site as a real one under a new name, with its values and documents.</summary>
    public async Task<(string? SiteId, string? Problem)> CopySiteAsRealAsync(string who, string siteId, string? name, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<ManufacturingSite>(siteId, cancellationToken) is not { } demo)
        {
            return (null, "the manufacturing site was not found");
        }

        if (!demo.IsDemo)
        {
            return (null, AlreadyReal);
        }

        if (await CopyNameProblemAsync(RecordKind.Site, name, cancellationToken) is { } problem)
        {
            return (null, problem);
        }

        var id = await InsertSiteCopyAsync(who, demo, name!.Trim(), clock.GetUtcNow().UtcDateTime, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Manufacturing site {name.Trim()} created as a copy of {demo.Name} (demo data)", "dpp-site", id, cancellationToken);
        return (id, null);
    }

    /// <summary>Copies a demo economic operator as a real one under a new name, with its values and documents.</summary>
    public async Task<(string? OperatorId, string? Problem)> CopyOperatorAsRealAsync(string who, string operatorId, string? name, CancellationToken cancellationToken = default)
    {
        if (await store.GetAsync<EconomicOperator>(operatorId, cancellationToken) is not { } demo)
        {
            return (null, "the economic operator was not found");
        }

        if (!demo.IsDemo)
        {
            return (null, AlreadyReal);
        }

        if (await CopyNameProblemAsync(RecordKind.Operator, name, cancellationToken) is { } problem)
        {
            return (null, problem);
        }

        var id = NewId("operator");
        var documentIds = await CopyDocumentsAsync(TargetKind.Operator, demo.Id, id, cancellationToken);
        await store.InsertAsync(new EconomicOperator { Id = id, Name = name!.Trim(), Values = CopyValues(demo.Values, documentIds, who, clock.GetUtcNow().UtcDateTime) }, cancellationToken);
        await AuditAsync(CreatedEvent, who, $"Economic operator {name.Trim()} created as a copy of {demo.Name} (demo data)", "dpp-operator", id, cancellationToken);
        return (id, null);
    }

    /// <summary>"Copy as real" for the site or operator a page shows, when it is demo data; null otherwise.</summary>
    public static RealCopyView? RealCopyFor(IDppRecord? record) => record switch
    {
        ManufacturingSite { IsDemo: true } site => new RealCopyView($"/admin/sites/{Uri.EscapeDataString(site.Id)}/copy-as-real", "manufacturing site", site.Name, RealCopyName(site.Name)),
        EconomicOperator { IsDemo: true } item => new RealCopyView($"/admin/operators/{Uri.EscapeDataString(item.Id)}/copy-as-real", "economic operator", item.Name, RealCopyName(item.Name)),
        _ => null,
    };

    private async Task<string?> CopyNameProblemAsync(RecordKind kind, string? name, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(name)
            ? $"give the new {KindName(kind).ToLowerInvariant()} a name"
            : await NameTakenAsync(kind, name, cancellationToken: cancellationToken);

    /// <summary>
    /// The real site a copied batch is made at: the demo site itself when it is real, else the real site called as the demo
    /// site or as its real name, else a new real copy. Returns the id and, when one was made, its name.
    /// </summary>
    private async Task<(string Id, string? Made)> RealSiteForAsync(string who, string demoSiteId, DateTime now, CancellationToken cancellationToken)
    {
        if (await store.GetAsync<ManufacturingSite>(demoSiteId, cancellationToken) is not { IsDemo: true } demo)
        {
            return (demoSiteId, null);
        }

        var realName = RealCopyName(demo.Name);
        var all = await store.ListAsync<ManufacturingSite>(cancellationToken);
        bool Called(ManufacturingSite site, string name) => string.Equals(site.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);
        if (all.FirstOrDefault(site => !site.IsDemo && (Called(site, realName) || Called(site, demo.Name))) is { } existing)
        {
            return (existing.Id, null);
        }

        // A demo record may already carry the real name; then the copy says it is the real one.
        var name = all.Any(site => Called(site, realName)) ? $"{demo.Name} (real)" : realName;
        return (await InsertSiteCopyAsync(who, demo, name, now, cancellationToken), name);
    }

    private async Task<string> InsertSiteCopyAsync(string who, ManufacturingSite demo, string name, DateTime now, CancellationToken cancellationToken)
    {
        var id = NewId("site");
        var documentIds = await CopyDocumentsAsync(TargetKind.Site, demo.Id, id, cancellationToken);
        await store.InsertAsync(new ManufacturingSite { Id = id, Name = name, Values = CopyValues(demo.Values, documentIds, who, now) }, cancellationToken);
        return id;
    }

    /// <summary>Copies every uploaded document of a record to its copy, each with its own stored file; old id → new id.</summary>
    private async Task<IReadOnlyDictionary<string, string>> CopyDocumentsAsync(TargetKind kind, string fromId, string toId, CancellationToken cancellationToken)
    {
        var copied = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var document in await documents.ListOwnedAsync(kind, fromId, cancellationToken))
        {
            var copy = new DppDocument
            {
                Id = $"doc-{Guid.NewGuid():N}",
                OwnerKind = kind,
                OwnerId = toId,
                Property = document.Property,
                FileName = document.FileName,
                ContentType = document.ContentType,
                Length = document.Length,
                Sha256 = document.Sha256,
                FileId = await CopyFileAsync(document.FileId, cancellationToken, document.FileName),
                Title = document.Title,
                Language = document.Language,
                UploadedAtUtc = document.UploadedAtUtc,
                UploadedBy = document.UploadedBy,
            };
            await documents.InsertAsync(copy, cancellationToken);
            copied[document.Id] = copy.Id;
        }

        return copied;
    }

    private async Task<string> CopyFileAsync(string fileId, CancellationToken cancellationToken, string fileName = "picture") =>
        await documents.UploadFileAsync(fileName, await documents.DownloadFileAsync(fileId, cancellationToken), cancellationToken);

    /// <summary>The values as they are, entered by <paramref name="who"/> now; a link to a copied document links its copy.</summary>
    private static Dictionary<string, AttributeValue> CopyValues(IReadOnlyDictionary<string, AttributeValue> values, IReadOnlyDictionary<string, string> documentIds, string who, DateTime now)
    {
        var copy = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            var node = value.Value.DeepClone();
            if (documentIds.Count > 0)
            {
                var text = node.ToJsonString();
                foreach (var (from, to) in documentIds)
                {
                    text = text.Replace(from, to, StringComparison.Ordinal);
                }

                node = JsonNode.Parse(text)!;
            }

            copy[key] = new AttributeValue { Value = node, GeneratedAtUtc = now, Source = value.Source, Who = who };
        }

        return copy;
    }
}
