using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace BatteryPassWeb.Dpp.Documents;

/// <summary>One uploaded file for a restricted document attribute of a model, batch or pack (design 4.1).</summary>
public sealed class DppDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonRepresentation(BsonType.String)]
    public required TargetKind OwnerKind { get; init; }

    public required string OwnerId { get; init; }

    /// <summary>The attribute's v2.0.1 property name, e.g. dismantlingInformation.</summary>
    public required string Property { get; init; }

    public required string FileName { get; init; }

    /// <summary>The type found in the file's own bytes, never the one the uploader claimed.</summary>
    public required string ContentType { get; init; }

    public required long Length { get; init; }

    /// <summary>Lowercase hex SHA-256 of the content.</summary>
    public required string Sha256 { get; init; }

    /// <summary>The GridFS file id.</summary>
    public required string FileId { get; init; }

    public required string Title { get; init; }

    public string? Language { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UploadedAtUtc { get; init; }

    public required string UploadedBy { get; init; }

    /// <summary>A copy of a demo file made by "Copy as real product data" (decision 70); old records aren't examples.</summary>
    public bool IsExample { get; init; }
}

/// <summary>Keeps uploaded files in GridFS and their records in a collection.</summary>
public sealed class DocumentStore
{
    public const string Records = "dpp_documents";
    public const string Bucket = "dpp_files";

    private readonly IMongoDatabase? _database;

    public DocumentStore(MongoContext mongoContext)
    {
        ArgumentNullException.ThrowIfNull(mongoContext);
        _database = mongoContext.Database;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return;
        }

        await Collection().Indexes.CreateOneAsync(
            new CreateIndexModel<DppDocument>(
                Builders<DppDocument>.IndexKeys.Ascending(d => d.OwnerKind).Ascending(d => d.OwnerId).Ascending(d => d.Property),
                new CreateIndexOptions { Name = "owner_property" }),
            cancellationToken: cancellationToken);
    }

    /// <summary>Stores the file's bytes and returns the GridFS id for its record.</summary>
    public async Task<string> UploadFileAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        var id = await Files().UploadFromBytesAsync(fileName, content, cancellationToken: cancellationToken);
        return id.ToString();
    }

    public Task InsertAsync(DppDocument document, CancellationToken cancellationToken = default) =>
        Collection().InsertOneAsync(document, cancellationToken: cancellationToken);

    public async Task<DppDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_database is null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await Collection().Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The documents of one attribute of one record, newest first.</summary>
    public async Task<IReadOnlyList<DppDocument>> ListAsync(TargetKind ownerKind, string ownerId, string property, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection()
            .Find(d => d.OwnerKind == ownerKind && d.OwnerId == ownerId && d.Property == property)
            .SortByDescending(d => d.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Every document of one record, oldest first (decision 66 copies them).</summary>
    public async Task<IReadOnlyList<DppDocument>> ListOwnedAsync(TargetKind ownerKind, string ownerId, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return [];
        }

        return await Collection().Find(d => d.OwnerKind == ownerKind && d.OwnerId == ownerId).SortBy(d => d.UploadedAtUtc).ToListAsync(cancellationToken);
    }

    /// <summary>Deletes every document of a removed record, with its stored file.</summary>
    public async Task DeleteOwnedAsync(TargetKind ownerKind, string ownerId, CancellationToken cancellationToken = default)
    {
        if (_database is null)
        {
            return;
        }

        foreach (var document in await Collection().Find(d => d.OwnerKind == ownerKind && d.OwnerId == ownerId).ToListAsync(cancellationToken))
        {
            await DeleteFileAsync(document.FileId, cancellationToken);
            await Collection().DeleteOneAsync(d => d.Id == document.Id, cancellationToken);
        }
    }

    /// <summary>Deletes one document: its stored file first, then its record, so no address opens it any more.</summary>
    public async Task DeleteAsync(DppDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_database is null)
        {
            return;
        }

        await DeleteFileAsync(document.FileId, cancellationToken);
        await Collection().DeleteOneAsync(d => d.Id == document.Id, cancellationToken);
    }

    /// <summary>Deletes a stored file; a file that is already gone is fine.</summary>
    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            await Files().DeleteAsync(ObjectId.Parse(fileId), cancellationToken);
        }
        catch (GridFSFileNotFoundException)
        {
            // already gone
        }
    }

    public Task<byte[]> DownloadAsync(DppDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        return DownloadFileAsync(document.FileId, cancellationToken);
    }

    /// <summary>The bytes of a stored file by its id: a document's FileId, or a family picture.</summary>
    public Task<byte[]> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default) =>
        Files().DownloadAsBytesAsync(ObjectId.Parse(fileId), cancellationToken: cancellationToken);

    private IMongoCollection<DppDocument> Collection() => RequireDatabase().GetCollection<DppDocument>(Records);

    private GridFSBucket Files() => new(RequireDatabase(), new GridFSBucketOptions { BucketName = Bucket });

    private IMongoDatabase RequireDatabase() =>
        _database ?? throw new InvalidOperationException("MongoDB is not configured (MONGODB_URI).");
}
