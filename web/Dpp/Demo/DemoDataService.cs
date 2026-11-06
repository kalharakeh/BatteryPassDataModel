using System.Text;
using System.Text.Json.Nodes;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BatteryPassWeb.Dpp.Demo;

/// <summary>A demo token's value, shown once after seeding and never stored or logged.</summary>
public sealed record DemoToken(string Label, string Access, string Value);

/// <summary>What a seed or a clear did, in plain lines, with notes for anything left as it was.</summary>
public sealed record DemoResult(IReadOnlyList<string> Done, IReadOnlyList<string> Notes, IReadOnlyList<DemoToken> Tokens);

/// <summary>
/// The demo data (decision 32). Seeding makes the catalogue's records through the app's own creation, save and
/// publication paths, so values are checked, logged and signed as usual, and marks each record it makes as demo data.
/// Clearing removes only marked records; a marked record that real data uses stays, loses its mark and is listed.
/// </summary>
public sealed class DemoDataService(
    MongoContext mongo,
    DppStore store,
    ProductDataPages productData,
    BatteryPages batteries,
    PackCreationService packCreation,
    SavePipeline pipeline,
    PublicationService publication,
    StatusChangeService statusChanges,
    SuccessionService successions,
    PassportMarks marks,
    HandOverService handOvers,
    DocumentService documents,
    DocumentStore documentStore,
    DppLinks links,
    BatteryApi api,
    ClusterRepository clusters,
    ExternalApiRepository tokens,
    ApplicationAuditService audit,
    IOptions<BatteryPassOptions> options,
    TimeProvider clock,
    UploadService uploads)
{
    public const string SeededEvent = "demo.seeded";
    public const string ClearedEvent = "demo.cleared";
    public const string Mark = "IsDemo";
    public const string AccountMark = "isDemo";
    public const string NoPassword = "Set DEMO_USERS_PASSWORD to create the demo accounts.";

    /// <summary>Hours between two demo live values; seven days fit the detail page's history (168 hours).</summary>
    private const int HistoryStepHours = 6;
    private const int HistoryPoints = 28;

    private IMongoDatabase Database => mongo.Database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");

    /// <summary>How many batteries the demo has (a battery placed on the market again counts once, not once per passport).</summary>
    public static int BatteryCount { get; } = DemoCatalogue.Load().Packs.Count;

    /// <summary>True when the demo accounts can be made: DEMO_USERS_PASSWORD is set.</summary>
    public bool AccountsPossible => !string.IsNullOrWhiteSpace(options.Value.DemoUsersPassword);

    /// <summary>True when any demo battery is there; false without a database.</summary>
    public async Task<bool> HasDemoDataAsync(CancellationToken cancellationToken = default) =>
        mongo.Database is not null
        && await Collection(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq(Mark, true), cancellationToken: cancellationToken) > 0;

    /// <summary>Clears the demo data, then seeds it again.</summary>
    public async Task<DemoResult> ResetAsync(string who, CancellationToken cancellationToken = default)
    {
        if (!links.IsConfigured)
        {
            return new DemoResult([], [DppLinks.NotConfigured], []);
        }

        var cleared = await ClearAsync(who, cancellationToken);
        var seeded = await SeedAsync(who, cancellationToken);
        return new DemoResult([.. cleared.Done, .. seeded.Done], [.. cleared.Notes, .. seeded.Notes], seeded.Tokens);
    }

    public async Task<DemoResult> SeedAsync(string who, CancellationToken cancellationToken = default)
    {
        if (!links.IsConfigured)
        {
            return new DemoResult([], [DppLinks.NotConfigured], []);
        }

        if (await HasDemoDataAsync(cancellationToken))
        {
            return new DemoResult([], ["The demo data is already there: reset it or clear it first."], []);
        }

        var catalogue = DemoCatalogue.Load();
        var team = ChangeSender.Team(who);
        var notes = new List<string>();
        var demoTokens = new List<DemoToken>();

        foreach (var cluster in catalogue.Clusters)
        {
            if (await Collection("clusters").Find(Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id)).AnyAsync(cancellationToken))
            {
                notes.Add($"Cluster {cluster.Id} was already there as real data: the demo batteries use it, and it stays real.");
                continue;
            }

            await clusters.UpsertClusterAsync(cluster.Id, cluster.Name, cancellationToken);
            await MarkAccountRecordAsync("clusters", Builders<BsonDocument>.Filter.Eq("clusterId", cluster.Id), cancellationToken);
        }

        // The accounts don't depend on the product data, so they are made side by side with it (redesign R1).
        var accountNotes = new List<string>();
        var accountsSeeded = SeedAccountsAsync(catalogue, accountNotes, cancellationToken);

        var families = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var family in catalogue.Families)
        {
            var id = await productData.CreateFamilyAsync(who, family.Name, cancellationToken);
            await MarkAsync(DppStore.Families, id, cancellationToken);
            await productData.SetFamilyPictureAsync(who, id, family.Picture, DemoCatalogue.Picture(family.Picture), cancellationToken);
            families[family.Key] = id;
        }

        var sites = await SeedNamedAsync(catalogue.Sites, TargetKind.Site, DppStore.Sites, name => productData.CreateSiteAsync(who, name, cancellationToken), team, cancellationToken);
        var operators = await SeedNamedAsync(catalogue.Operators, TargetKind.Operator, DppStore.Operators, name => productData.CreateOperatorAsync(who, name, cancellationToken), team, cancellationToken);

        var models = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in catalogue.Models)
        {
            var id = await productData.CreateModelAsync(who, families[model.Family], model.Name, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException($"The demo model {model.Name} couldn't be made.");
            await MarkAsync(DppStore.Models, id, cancellationToken);
            await SaveAsync(team, TargetKind.Model, id, model.Values, cancellationToken);
            foreach (var (property, title) in model.Documents)
            {
                await UploadAsync(team, TargetKind.Model, id, property, $"{title} - {model.Name}", cancellationToken);
            }

            var stored = await store.GetAsync<BatteryModel>(id, cancellationToken);
            await productData.SetModuleDescriptionAsync(who, stored!, model.ModuleDescription, cancellationToken);
            if (model.Picture is { } picture)
            {
                await productData.SetModelPictureAsync(who, id, picture, DemoCatalogue.Picture(picture), cancellationToken);
            }

            models[model.Key] = id;
        }

        var batches = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in catalogue.Batches)
        {
            var (id, problem) = await productData.CreateBatchAsync(who, models[batch.Model], batch.Year, sites[batch.Site], cancellationToken);
            if (id is null)
            {
                throw new InvalidOperationException($"The demo batch {batch.Key} couldn't be made: {problem}.");
            }

            await MarkAsync(DppStore.Batches, id, cancellationToken);
            await SaveAsync(team, TargetKind.Batch, id, batch.Values, cancellationToken);
            await UploadAsync(team, TargetKind.Batch, id, "resultsOfTestReportsProvingCompliance", batch.TestReport, cancellationToken);
            batches[batch.Key] = id;
        }

        var accounts = await accountsSeeded;
        notes.AddRange(accountNotes);

        // The batteries don't depend on each other: on a remote database each call waits for its round trip, so they are
        // made side by side, which about halves the reset (redesign R1, point 1). Each battery's own steps stay in order,
        // and every save still goes through the save rules.
        var outcomes = await Task.WhenAll(catalogue.Packs.Select(demo => SeedPackAsync(who, team, demo, batches, operators, catalogue, cancellationToken)));
        var published = outcomes.Sum(outcome => outcome.Published);
        var successors = outcomes.Sum(outcome => outcome.Successors);
        notes.AddRange(outcomes.SelectMany(outcome => outcome.Notes));

        var writeTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        demoTokens.AddRange(await SeedTokensAsync(who, catalogue, cancellationToken, writeTokens));
        await SeedUploadsAsync(catalogue, writeTokens, cancellationToken);

        var done = new List<string>
        {
            $"{catalogue.Families.Count} families, {catalogue.Models.Count} models, {catalogue.Sites.Count} sites, {catalogue.Operators.Count} operators and {catalogue.Batches.Count} batches",
            $"{catalogue.Packs.Count + successors} batteries, {published} of them published",
            $"{accounts} accounts and {demoTokens.Count} API tokens",
        };
        await audit.AppendApplicationAuditEventAsync(
            SeededEvent,
            who,
            "admin",
            "admin-ui",
            "Demo data seeded.",
            new BsonDocument
            {
                ["families"] = catalogue.Families.Count,
                ["models"] = catalogue.Models.Count,
                ["batteries"] = catalogue.Packs.Count + successors,
                ["published"] = published,
                ["accounts"] = accounts,
                ["tokens"] = demoTokens.Count,
            },
            "demo",
            "demo-data",
            cancellationToken: cancellationToken);
        return new DemoResult(done, notes, demoTokens);
    }

    public async Task<DemoResult> ClearAsync(string who, CancellationToken cancellationToken = default)
    {
        var notes = new List<string>();
        var removed = new Dictionary<string, int>(StringComparer.Ordinal);
        void Count(string what) => removed[what] = removed.GetValueOrDefault(what) + 1;

        // Batteries first: nothing real can depend on a demo battery. Their versions and documents go with them, and their
        // kept upload checks in one delete (step 5d).
        var demoPacks = await DemoIdsAsync(DppStore.Packs, cancellationToken);
        await Collection(DppStore.UploadChecks).DeleteManyAsync(Builders<BsonDocument>.Filter.In("PackId", demoPacks), cancellationToken);
        foreach (var packId in demoPacks)
        {
            await Collection(DppStore.Versions).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
            await Collection(DppStore.HandOvers).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("PackId", packId), cancellationToken);
            await documentStore.DeleteOwnedAsync(TargetKind.Pack, packId, cancellationToken);
            await store.DeleteAsync<BatteryPack>(packId, cancellationToken);
            Count("batteries");
        }

        // The demo uploads go with them (step 5d); a real upload naming a demo battery stays in the log.
        await Collection(DppStore.Uploads).DeleteManyAsync(Builders<BsonDocument>.Filter.Eq("IsDemo", true), cancellationToken);

        await ClearUnlessUsedAsync<ProductionBatch>(DppStore.Batches, "batches", (DppStore.Packs, "BatchId"), "real batteries", TargetKind.Batch, notes, Count, cancellationToken);
        await ClearUnlessUsedAsync<EconomicOperator>(DppStore.Operators, "operators", (DppStore.Packs, "OperatorId"), "real batteries", null, notes, Count, cancellationToken);
        // A removed demo model takes its own picture with it (redesign R2); a kept one keeps it.
        var modelPictures = (await Collection(DppStore.Models).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
            .Where(model => model.GetValue("PictureFileId", BsonNull.Value).IsString)
            .ToDictionary(model => model["_id"].AsString, model => model["PictureFileId"].AsString);
        await ClearUnlessUsedAsync<BatteryModel>(DppStore.Models, "models", (DppStore.Batches, "ModelId"), "real batches", TargetKind.Model, notes, Count, cancellationToken);
        foreach (var (modelId, fileId) in modelPictures)
        {
            if (await store.GetAsync<BatteryModel>(modelId, cancellationToken) is null)
            {
                await documentStore.DeleteFileAsync(fileId, cancellationToken);
            }
        }
        await ClearUnlessUsedAsync<ManufacturingSite>(DppStore.Sites, "sites", (DppStore.Batches, "SiteId"), "real batches", null, notes, Count, cancellationToken);
        foreach (var family in await Collection(DppStore.Families).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
        {
            var id = family["_id"].AsString;
            var used = await Collection(DppStore.Models).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("FamilyId", id), cancellationToken: cancellationToken);
            if (used > 0)
            {
                await Unmark(DppStore.Families, id, cancellationToken);
                notes.Add($"Kept {BsonName(family)}: {used} real models use it, so it is no longer marked as demo data.");
                continue;
            }

            if (family.GetValue("PictureFileId", BsonNull.Value) is { IsString: true } picture)
            {
                await documentStore.DeleteFileAsync(picture.AsString, cancellationToken);
            }

            await store.DeleteAsync<BatteryFamily>(id, cancellationToken);
            Count("families");
        }

        var demoTokens = Builders<BsonDocument>.Filter.Eq(AccountMark, true);
        removed["API tokens"] = (int)(await Collection("apiTokens").DeleteManyAsync(demoTokens, cancellationToken)).DeletedCount;
        foreach (var user in await Collection("users").Find(Builders<BsonDocument>.Filter.Eq(AccountMark, true)).ToListAsync(cancellationToken))
        {
            await clusters.DeleteUserAsync(BsonHelpers.GetString(user, "email"), cancellationToken);
            Count("accounts");
        }

        foreach (var cluster in await Collection("clusters").Find(Builders<BsonDocument>.Filter.Eq(AccountMark, true)).ToListAsync(cancellationToken))
        {
            var clusterId = BsonHelpers.GetString(cluster, "clusterId");
            var uses = new List<string>();
            var packs = await Collection(DppStore.Packs).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("ClusterId", clusterId), cancellationToken: cancellationToken);
            var members = await Collection("clusterMemberships").CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("clusterId", clusterId), cancellationToken: cancellationToken);
            // The cluster's own tokens, which the app's start makes for every cluster, go with it and aren't real use.
            var ownTokens = Builders<BsonDocument>.Filter.Eq("autoClusterId", clusterId);
            var realTokens = await Collection("apiTokens").CountDocumentsAsync(
                Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.AnyEq("clusterIds", clusterId), Builders<BsonDocument>.Filter.Not(ownTokens)),
                cancellationToken: cancellationToken);
            if (packs > 0) uses.Add($"{packs} real batteries");
            if (members > 0) uses.Add($"{members} real accounts");
            if (realTokens > 0) uses.Add($"{realTokens} real API tokens");
            if (uses.Count > 0)
            {
                await MarkAccountRecordAsync("clusters", Builders<BsonDocument>.Filter.Eq("clusterId", clusterId), cancellationToken, false);
                notes.Add($"Kept cluster {clusterId}: {string.Join(", ", uses)} use it, so it is no longer marked as demo data.");
                continue;
            }

            removed["API tokens"] += (int)(await Collection("apiTokens").DeleteManyAsync(ownTokens, cancellationToken)).DeletedCount;
            await clusters.DeleteClusterAsync(clusterId, cancellationToken);
            Count("clusters");
        }

        var done = removed.Where(entry => entry.Value > 0).Select(entry => $"{entry.Value} {entry.Key} removed").ToList();
        if (done.Count == 0 && notes.Count == 0)
        {
            done.Add("There was no demo data to clear.");
        }

        var metadata = new BsonDocument(removed.Select(entry => new BsonElement(entry.Key.Replace(' ', '_'), entry.Value)))
        {
            ["kept"] = notes.Count,
        };
        await audit.AppendApplicationAuditEventAsync(ClearedEvent, who, "admin", "admin-ui", "Demo data cleared.", metadata, "demo", "demo-data", cancellationToken: cancellationToken);
        return new DemoResult(done, notes, []);
    }

    private async Task<int> SeedAccountsAsync(DemoCatalogue catalogue, List<string> notes, CancellationToken cancellationToken)
    {
        var password = options.Value.DemoUsersPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            notes.Add(NoPassword);
            return 0;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        var made = 0;
        foreach (var account in catalogue.Accounts)
        {
            var byEmail = Builders<BsonDocument>.Filter.Eq("email", account.Email);
            if (await Collection("users").Find(byEmail).AnyAsync(cancellationToken))
            {
                notes.Add($"Account {account.Email} was already there as real data: it stays as it is.");
                continue;
            }

            await clusters.UpsertUserAsync(account.Email, account.Name, [account.Role], hash, cancellationToken);
            await MarkAccountRecordAsync("users", byEmail, cancellationToken);
            if (account.Cluster is not null)
            {
                await clusters.UpsertClusterMembershipAsync(account.Email, account.Cluster, account.ClusterRole ?? "member", cancellationToken);
                await MarkAccountRecordAsync("clusterMemberships", Builders<BsonDocument>.Filter.And(byEmail, Builders<BsonDocument>.Filter.Eq("clusterId", account.Cluster)), cancellationToken);
            }

            made++;
        }

        return made;
    }

    /// <summary>Each demo cluster's read and read-write tokens, and one team token with full access; values shown once.
    /// The cluster tokens are the cluster's own (autoClusterId), so the app's start adds no others for a demo cluster.</summary>
    private async Task<List<DemoToken>> SeedTokensAsync(string who, DemoCatalogue catalogue, CancellationToken cancellationToken, Dictionary<string, string>? writeTokens = null)
    {
        var made = new List<DemoToken>();
        foreach (var cluster in catalogue.Clusters)
        {
            foreach (var (mode, access) in new[] { (ExternalTokenAccessMode.Read, "Read"), (ExternalTokenAccessMode.ReadWrite, "Read and write live values") })
            {
                var (document, value) = await tokens.CreateTokenAsync($"{cluster.Name} ({access.ToLowerInvariant()})", mode, [cluster.Id], false, false, who, autoClusterId: cluster.Id, cancellationToken: cancellationToken);
                await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", document["_id"]), cancellationToken);
                made.Add(new DemoToken($"{cluster.Name}", access, value));
                if (mode == ExternalTokenAccessMode.ReadWrite && writeTokens is not null)
                {
                    writeTokens[cluster.Id] = document["_id"].ToString()!;
                }
            }
        }

        var (team, teamValue) = await tokens.CreateTokenAsync("Demo team (full access)", ExternalTokenAccessMode.Lifecycle, [], false, true, who, cancellationToken: cancellationToken);
        await MarkAccountRecordAsync("apiTokens", Builders<BsonDocument>.Filter.Eq("_id", team["_id"]), cancellationToken);
        made.Add(new DemoToken("Team, all clusters", "Full access", teamValue));
        return made;
    }

    private async Task<Dictionary<string, string>> SeedNamedAsync(
        IEnumerable<DemoNamed> records, TargetKind kind, string collection, Func<string, Task<string>> create, ChangeSender team, CancellationToken cancellationToken)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var id = await create(record.Name);
            await MarkAsync(collection, id, cancellationToken);
            await SaveAsync(team, kind, id, record.Values, cancellationToken);
            ids[record.Key] = id;
        }

        return ids;
    }

    /// <summary>Seven days of live values every six hours, oldest first, through the API's own write path.</summary>
    /// <summary>One demo battery's steps, in order: made, its modules, published with its history, its accident report, its
    /// status change and new passport, its mark and its hand-over. Batteries are seeded side by side (redesign R1).</summary>
    private async Task<PackOutcome> SeedPackAsync(
        string who,
        ChangeSender team,
        DemoPack demo,
        IReadOnlyDictionary<string, string> batches,
        IReadOnlyDictionary<string, string> operators,
        DemoCatalogue catalogue,
        CancellationToken cancellationToken)
    {
        var published = 0;
        var successors = 0;
        var created = await packCreation.CreateAsync(team, new NewPack(batches[demo.Batch], operators[demo.Operator], demo.Cluster, Values(demo.Values), "form", IsDemo: true), cancellationToken);
        if (created.Pack is null)
        {
            var rejected = created.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key} ({key.Message})");
            return new PackOutcome(0, 0, [$"Battery {demo.Serial} wasn't made: {created.Problem} {string.Join("; ", rejected)}".TrimEnd() + "."]);
        }

        var packId = created.Pack.Id;
        await MarkAsync(DppStore.Packs, packId, cancellationToken);
        if (demo.Modules > 0)
        {
            await batteries.ImportModulesAsync(who, packId, ModuleExport(demo.Serial, demo.Modules), cancellationToken);
        }

        if (demo.Publish)
        {
            var result = await publication.PublishAsync(team, packId, cancellationToken);
            if (result.Version is null)
            {
                throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
            }

            published++;
            if (demo.History)
            {
                await WriteHistoryAsync(team, packId, demo, cancellationToken);
            }

            if (demo.NewVersion is not null)
            {
                await SaveAsync(team, TargetKind.Pack, packId, demo.NewVersion, cancellationToken);
            }
        }

        if (demo.Accident is not null)
        {
            await documents.AddAccidentReportAsync(team, packId, new DocumentUpload("accident-report.pdf", DemoPdf.Make("Accident report", demo.Accident), demo.Accident, "en-GB"), cancellationToken);
        }

        if (demo.Status is { } status && demo.Publish)
        {
            await ChangeStatusAsync(team, packId, demo, status, cancellationToken);
            if (status.Successor is { } next)
            {
                var newest = await MakeSuccessorAsync(team, packId, demo, next, cancellationToken);
                successors++;
                published++;

                // Placed on the market again in turn: a chain of passports (redesign R6, the owner's item 12).
                for (var then = next.Then; then is not null; then = then.Successor?.Then)
                {
                    await ChangeStatusAsync(team, newest, demo, then, cancellationToken);
                    if (then.Successor is not { } again)
                    {
                        break;
                    }

                    newest = await MakeSuccessorAsync(team, newest, demo, again, cancellationToken);
                    successors++;
                    published++;
                }
            }
        }

        if (demo.Mark is { } mark && demo.Publish)
        {
            await MarkAsync(team, packId, demo, mark, catalogue, cancellationToken);
        }

        if (demo.HandOverTo is { } receiver && demo.Publish)
        {
            await HandOverAsync(packId, demo, receiver, catalogue, cancellationToken);
        }

        return new PackOutcome(published, successors, []);
    }

    /// <summary>What seeding one battery made: published passports, new passports, and notes.</summary>
    private sealed record PackOutcome(int Published, int Successors, IReadOnlyList<string> Notes);

    private async Task WriteHistoryAsync(ChangeSender team, string packId, DemoPack demo, CancellationToken cancellationToken)
    {
        var pack = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
        var now = clock.GetUtcNow().UtcDateTime;
        var cycles = demo.Values["numberOfFullChargingAndDischargingCycles"]!.GetValue<int>() - HistoryPoints / 2;
        for (var point = 0; point < HistoryPoints; point++)
        {
            var hoursAgo = HistoryStepHours * (HistoryPoints - 1 - point);
            var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
            {
                ["stateOfChargeSoC"] = 55 + (int)Math.Round(30 * Math.Sin(point * Math.PI / 4)),
                ["numberOfFullChargingAndDischargingCycles"] = cycles + point / 2,
                ["temperatureInformation"] = 22 + (int)Math.Round(6 * Math.Sin(point * Math.PI / 2)),
                ["timeSpentInExtremeTemperaturesAboveBoundary"] = 30 + point,
                ["timeSpentInExtremeTemperaturesBelowBoundary"] = 12 + point / 4,
                ["timeSpentChargingDuringExtremeTemperaturesAboveBoundary"] = 0,
                ["timeSpentChargingDuringExtremeTemperaturesBelowBoundary"] = 0,
            };
            await api.WriteLiveValuesAsync(team, pack, values, now.AddHours(-hoursAgo), preview: false, cancellationToken);
        }
    }

    private async Task ClearUnlessUsedAsync<T>(
        string collection,
        string plural,
        (string Collection, string Field) usedBy,
        string usersWord,
        TargetKind? documentsOf,
        List<string> notes,
        Action<string> count,
        CancellationToken cancellationToken)
        where T : class, IDppRecord
    {
        foreach (var record in await Collection(collection).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).ToListAsync(cancellationToken))
        {
            var id = record["_id"].AsString;
            var used = await Collection(usedBy.Collection).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq(usedBy.Field, id), cancellationToken: cancellationToken);
            if (used > 0)
            {
                await Unmark(collection, id, cancellationToken);
                notes.Add($"Kept {BsonName(record)}: {used} {usersWord} use it, so it is no longer marked as demo data.");
                continue;
            }

            if (documentsOf is { } owner)
            {
                await documentStore.DeleteOwnedAsync(owner, id, cancellationToken);
            }

            await store.DeleteAsync<T>(id, cancellationToken);
            count(plural);
        }
    }

    private async Task SaveAsync(ChangeSender team, TargetKind kind, string id, JsonObject values, CancellationToken cancellationToken)
    {
        var result = await pipeline.SaveAsync(team, new ChangeSet(kind, id, Values(values), "form"), cancellationToken: cancellationToken);
        var rejected = result.Keys.Where(key => key.Outcome == KeyOutcome.Rejected).Select(key => $"{key.Key}: {key.Message}").ToList();
        if (!result.Saved || rejected.Count > 0)
        {
            throw new InvalidOperationException($"The demo {kind} values weren't all accepted: {string.Join("; ", rejected)}");
        }
    }

    private async Task UploadAsync(ChangeSender team, TargetKind kind, string id, string property, string title, CancellationToken cancellationToken)
    {
        var fileName = $"{property}.pdf";
        var result = await documents.UploadAsync(team, kind, id, property, new DocumentUpload(fileName, DemoPdf.Make(title, $"{kind} document for the demo data."), title, "en-GB"), cancellationToken);
        if (result.Problem is not null)
        {
            throw new InvalidOperationException($"The demo document {title} couldn't be stored: {result.Problem}.");
        }
    }

    private static Dictionary<string, JsonNode?> Values(JsonObject values) =>
        values.ToDictionary(entry => entry.Key, entry => entry.Value?.DeepClone(), StringComparer.Ordinal);

    /// <summary>
    /// A demo battery's status change (step 4b), through the app's own status change like a person's: the re-stated
    /// values start as the battery's current ones, as the window fills them in, with the catalogue's on top.
    /// </summary>
    private async Task ChangeStatusAsync(ChangeSender team, string packId, DemoPack demo, DemoStatus status, CancellationToken cancellationToken)
    {
        var sources = await store.LoadSourcesAsync(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
        var restated = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var attribute in StatusChangeService.RestatedAttributes(sources.Model))
        {
            restated[attribute.Property] = status.Restated?[attribute.Property]?.DeepClone()
                ?? (sources.Pack.Values.GetValueOrDefault(attribute.Property) ?? sources.Model.Values.GetValueOrDefault(attribute.Property))?.Value?.DeepClone();
        }

        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(-status.DaysAgo).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var result = await statusChanges.ChangeAsync(team, packId, new StatusChange(Enum.Parse<StatusEvent>(status.What), restated, day, status.Repair), cancellationToken);
        if (result.Problem is not null)
        {
            throw new InvalidOperationException($"The demo battery {demo.Serial}'s status couldn't change: {result.Problem}.");
        }
    }

    /// <summary>
    /// Scania's new passport of a demo battery placed on the market again (step 4c-1), the way the team makes it: made
    /// from the old battery with its operator, given a warranty, published, which makes the old passport inactive.
    /// </summary>
    private async Task<string> MakeSuccessorAsync(ChangeSender team, string packId, DemoPack demo, DemoSuccessor next, CancellationToken cancellationToken)
    {
        var old = await store.GetAsync<BatteryPack>(packId, cancellationToken) ?? throw new InvalidOperationException($"The demo battery {demo.Serial} is missing.");
        var (successorId, problem) = await successions.CreateSuccessorAsync(team, packId, old.OperatorId, cancellationToken);
        if (successorId is null)
        {
            throw new InvalidOperationException($"The demo battery {demo.Serial}'s new passport couldn't be made: {problem}.");
        }

        await MarkAsync(DppStore.Packs, successorId, cancellationToken);
        await SaveAsync(team, TargetKind.Pack, successorId, new JsonObject { ["warrantyPeriod"] = next.Warranty }, cancellationToken);
        var result = await publication.PublishAsync(team, successorId, cancellationToken);
        if (result.Version is null)
        {
            throw new InvalidOperationException($"The demo battery {demo.Serial}'s new passport couldn't be published: {result.Problem ?? string.Join("; ", result.Report?.Blocking ?? [])}.");
        }

        return successorId;
    }

    /// <summary>
    /// A demo battery's passport marked invalid by the demo market surveillance account, as it would mark it on the
    /// detail page, or marked for deletion by the team (step 4c-2).
    /// </summary>
    private async Task MarkAsync(ChangeSender team, string packId, DemoPack demo, DemoMark mark, DemoCatalogue catalogue, CancellationToken cancellationToken)
    {
        string? problem = null;
        if (mark.Invalid is { } reason)
        {
            var surveillance = catalogue.Accounts.FirstOrDefault(account => account.Role == AccessControlService.RoleMarketSurveillanceAuthority)?.Email ?? team.Who;
            problem = await marks.MarkInvalidAsync(ChangeSender.MarketSurveillance(surveillance), packId, reason, cancellationToken);
        }
        else if (mark.Deletion)
        {
            problem = await marks.MarkForDeletionAsync(team, packId, cancellationToken: cancellationToken);
        }

        if (problem is not null)
        {
            throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be marked: {problem}.");
        }
    }

    /// <summary>
    /// A demo battery handed over by its cluster's demo admin, as they would on its page, and left waiting for the
    /// receiving cluster's answer (step 4d).
    /// </summary>
    private async Task HandOverAsync(string packId, DemoPack demo, string receiver, DemoCatalogue catalogue, CancellationToken cancellationToken)
    {
        var admin = catalogue.Accounts.FirstOrDefault(account => account.Cluster == demo.Cluster && account.ClusterRole == "clusterAdmin")?.Email
            ?? throw new InvalidOperationException($"The demo cluster {demo.Cluster} has no admin to hand {demo.Serial} over.");
        var asked = await handOvers.AskAsync(ChangeSender.ClusterAdmin(admin, demo.Cluster), packId, receiver, confirm: true, cancellationToken);
        if (asked.Problem is { } problem)
        {
            throw new InvalidOperationException($"The demo battery {demo.Serial} couldn't be handed over: {problem}.");
        }
    }

    /// <summary>An export-shaped file (decision 31) with this many modules for the battery.</summary>
    private static byte[] ModuleExport(string serial, int modules)
    {
        var children = new JsonArray();
        for (var position = 1; position <= modules; position++)
        {
            children.Add(new JsonObject
            {
                ["position"] = position,
                ["serialNumber"] = $"{serial}-M{position:D2}",
                ["connectivityId"] = $"CAN-{serial}-{position:D2}",
            });
        }

        var export = new JsonObject
        {
            ["metadata"] = new JsonObject
            {
                ["identifiers"] = new JsonObject
                {
                    ["deviceIdentifiers"] = new JsonObject { ["serialNumber"] = serial, ["childrenIdentifiers"] = children },
                },
            },
        };
        return Encoding.UTF8.GetBytes(export.ToJsonString());
    }

    /// <summary>
    /// The demo uploads (step 5d, Q25), after the tokens: for each battery the catalogue marks, an export from its
    /// cluster's battery system (its read-write token) two hours ago, and a file of live values from its cluster admin half
    /// an hour ago, through the one upload engine. Both are marked as demo data. No status change: the battery's demo
    /// repair is 4b's.
    /// </summary>
    private async Task SeedUploadsAsync(DemoCatalogue catalogue, IReadOnlyDictionary<string, string> writeTokens, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var demo in catalogue.Packs.Where(pack => pack.Uploads && pack.Publish))
        {
            var admin = catalogue.Accounts.FirstOrDefault(account => account.Cluster == demo.Cluster && account.ClusterRole == "clusterAdmin");
            var made = new List<string?>();
            if (writeTokens.TryGetValue(demo.Cluster, out var tokenId))
            {
                var export = await uploads.RunAsync(ChangeSender.Feed($"api-token:{tokenId}", demo.Cluster), UploadSamples.Export(demo.Serial, now.AddHours(-2), demo.Modules), "api", null, preview: false, cancellationToken);
                made.Add(export.UploadId);
            }

            if (admin is not null)
            {
                var file = JsonNode.Parse(UploadSamples.LiveValuesFile(demo.Serial, now.AddMinutes(-30), 71, 22));
                var readings = await uploads.RunAsync(ChangeSender.ClusterAdmin(admin.Email, demo.Cluster), file, "page", "readings.json", preview: false, cancellationToken);
                made.Add(readings.UploadId);
            }

            foreach (var id in made.OfType<string>())
            {
                await store.SetUploadDemoAsync(id, cancellationToken);
            }
        }
    }

    private IMongoCollection<BsonDocument> Collection(string name) => Database.GetCollection<BsonDocument>(name);

    private async Task<List<string>> DemoIdsAsync(string collection, CancellationToken cancellationToken) =>
        (await Collection(collection).Find(Builders<BsonDocument>.Filter.Eq(Mark, true)).Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync(cancellationToken))
        .Select(document => document["_id"].AsString)
        .ToList();

    private Task MarkAsync(string collection, string id, CancellationToken cancellationToken) =>
        Collection(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set(Mark, true), cancellationToken: cancellationToken);

    private Task Unmark(string collection, string id, CancellationToken cancellationToken) =>
        Collection(collection).UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Update.Set(Mark, false), cancellationToken: cancellationToken);

    private Task MarkAccountRecordAsync(string collection, FilterDefinition<BsonDocument> filter, CancellationToken cancellationToken, bool isDemo = true) =>
        Collection(collection).UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(AccountMark, isDemo), cancellationToken: cancellationToken);

    private static string BsonName(BsonDocument record) =>
        record.GetValue("Name", BsonNull.Value) is { IsString: true } name ? name.AsString : record["_id"].AsString;
}
