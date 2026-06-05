using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http;

namespace BatteryPassWeb.Services;

public enum ExternalTokenAccessMode
{
    Read,
    ReadWrite,
    Sign,
    Lifecycle
}

public enum ExternalTokenRequirement
{
    Read,
    Write,
    Sign
}

public sealed class ExternalApiTokenValidationResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status401Unauthorized;
    public string Message { get; init; } = "Unauthorized";
    public ExternalApiTokenContext? Context { get; init; }
}

public sealed class ExternalApiTokenContext
{
    public string TokenId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ExternalTokenAccessMode AccessMode { get; init; } = ExternalTokenAccessMode.Read;
    public bool GlobalAccess { get; init; }
    public bool AllowUnassigned { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> ClusterIds { get; init; } = [];
    public bool IsSample { get; init; }
}

public sealed class ExternalApiRepository
{
    public const string ValidateSignPublish = "validateSignPublish";

    private readonly MongoContext _mongoContext;
    private readonly ExternalApiSecurityService _securityService;

    public ExternalApiRepository(MongoContext mongoContext, ExternalApiSecurityService securityService)
    {
        _mongoContext = mongoContext;
        _securityService = securityService;
    }

    public bool IsAvailable => _mongoContext.Database != null;

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return;
        }

        var tokens = _mongoContext.Database.GetCollection<BsonDocument>("apiTokens");
        var tokenIndexes = new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tokenId"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("isActive")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("clusterIds")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("autoClusterId").Ascending("accessMode"))
        };
        await tokens.Indexes.CreateManyAsync(tokenIndexes, cancellationToken);

        var collectionNames = await _mongoContext.Database.ListCollectionNames().ToListAsync(cancellationToken);
        if (collectionNames.Contains("batterySecrets", StringComparer.OrdinalIgnoreCase))
        {
            await _mongoContext.Database.DropCollectionAsync("batterySecrets", cancellationToken);
        }
    }

    public async Task<IReadOnlyList<BsonDocument>> ListTokensAsync(CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .SortByDescending(document => document["updatedAt"])
            .ToListAsync(cancellationToken);
    }

    public async Task<BsonDocument?> GetTokenByIdAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .Find(Builders<BsonDocument>.Filter.Eq("tokenId", tokenId.Trim()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(BsonDocument Document, string PlainToken)> CreateTokenAsync(
        string name,
        ExternalTokenAccessMode accessMode,
        IEnumerable<string> clusterIds,
        bool allowUnassigned,
        bool globalAccess,
        string actor,
        bool isSample = false,
        string? fixedToken = null,
        string? fixedTokenId = null,
        string? autoClusterId = null,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            throw new InvalidOperationException("Database is not connected.");
        }

        var now = DateTime.UtcNow.ToString("O");
        var token = string.IsNullOrWhiteSpace(fixedToken) ? _securityService.CreateToken() : fixedToken.Trim();
        var tokenId = string.IsNullOrWhiteSpace(fixedTokenId) ? Guid.NewGuid().ToString("N") : fixedTokenId.Trim();
        var normalizedClusters = clusterIds
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Select(clusterId => clusterId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var document = new BsonDocument
        {
            ["tokenId"] = tokenId,
            ["name"] = string.IsNullOrWhiteSpace(name) ? tokenId : name.Trim(),
            ["accessMode"] = AccessModeValue(accessMode),
            ["clusterIds"] = new BsonArray(normalizedClusters),
            ["allowUnassigned"] = allowUnassigned,
            ["globalAccess"] = globalAccess,
            ["isActive"] = true,
            ["tokenHash"] = _securityService.HashSecret(token),
            ["encryptedToken"] = _securityService.Encrypt(token),
            ["isSample"] = isSample,
            ["createdBy"] = actor,
            ["updatedBy"] = actor,
            ["createdAt"] = now,
            ["updatedAt"] = now,
            ["lastUsedAt"] = BsonNull.Value
        };

        if (!string.IsNullOrWhiteSpace(autoClusterId))
        {
            document["autoClusterId"] = autoClusterId.Trim();
        }

        await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens").InsertOneAsync(document, cancellationToken: cancellationToken);
        return (document, token);
    }

    public async Task UpsertFixedTokenAsync(
        string tokenId,
        string token,
        string name,
        ExternalTokenAccessMode accessMode,
        IEnumerable<string> clusterIds,
        bool allowUnassigned,
        bool globalAccess,
        string actor,
        bool isSample = false,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            throw new InvalidOperationException("Database is not connected.");
        }

        if (string.IsNullOrWhiteSpace(tokenId) || string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Fixed token ID and value are required.");
        }

        var now = DateTime.UtcNow.ToString("O");
        var normalizedTokenId = tokenId.Trim();
        var normalizedToken = token.Trim();
        var normalizedClusters = clusterIds
            .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
            .Select(clusterId => clusterId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var update = Builders<BsonDocument>.Update
            .Set("name", string.IsNullOrWhiteSpace(name) ? normalizedTokenId : name.Trim())
            .Set("accessMode", AccessModeValue(accessMode))
            .Set("clusterIds", new BsonArray(normalizedClusters))
            .Set("allowUnassigned", allowUnassigned)
            .Set("globalAccess", globalAccess)
            .Set("isActive", true)
            .Set("tokenHash", _securityService.HashSecret(normalizedToken))
            .Set("encryptedToken", _securityService.Encrypt(normalizedToken))
            .Set("isSample", isSample)
            .Set("updatedBy", actor)
            .Set("updatedAt", now)
            .Set("lastUsedAt", BsonNull.Value)
            .SetOnInsert("tokenId", normalizedTokenId)
            .SetOnInsert("createdBy", actor)
            .SetOnInsert("createdAt", now);

        await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("tokenId", normalizedTokenId),
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
    }

    public async Task<bool> SetTokenActiveAsync(string tokenId, bool isActive, string actor, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        var update = Builders<BsonDocument>.Update
            .Set("isActive", isActive)
            .Set("updatedBy", actor)
            .Set("updatedAt", DateTime.UtcNow.ToString("O"));

        var result = await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("tokenId", tokenId.Trim()), update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteTokenAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        var result = await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("tokenId", tokenId.Trim()), cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<long> DeleteTokensAsync(IEnumerable<string> tokenIds, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return 0;
        }

        var normalizedTokenIds = tokenIds
            .Where(tokenId => !string.IsNullOrWhiteSpace(tokenId))
            .Select(tokenId => tokenId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedTokenIds.Count == 0)
        {
            return 0;
        }

        var result = await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .DeleteManyAsync(Builders<BsonDocument>.Filter.In("tokenId", normalizedTokenIds), cancellationToken);
        return result.DeletedCount;
    }

    public async Task<string?> RegenerateTokenAsync(string tokenId, string actor, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        var newToken = _securityService.CreateToken();
        var update = Builders<BsonDocument>.Update
            .Set("tokenHash", _securityService.HashSecret(newToken))
            .Set("encryptedToken", _securityService.Encrypt(newToken))
            .Set("updatedBy", actor)
            .Set("updatedAt", DateTime.UtcNow.ToString("O"))
            .Set("lastUsedAt", BsonNull.Value);

        var result = await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("tokenId", tokenId.Trim()), update, cancellationToken: cancellationToken);

        return result.MatchedCount > 0 ? newToken : null;
    }

    public Task<ExternalApiTokenValidationResult> ValidateTokenAsync(string rawToken, bool requireWrite, CancellationToken cancellationToken = default)
    {
        return ValidateTokenAsync(
            rawToken,
            requireWrite ? ExternalTokenRequirement.Write : ExternalTokenRequirement.Read,
            cancellationToken);
    }

    public async Task<ExternalApiTokenValidationResult> ValidateTokenAsync(string rawToken, ExternalTokenRequirement requirement, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return new ExternalApiTokenValidationResult
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
                Message = "Database is not connected."
            };
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new ExternalApiTokenValidationResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Token is required."
            };
        }

        var tokenDocuments = await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .Find(Builders<BsonDocument>.Filter.Eq("isActive", true))
            .ToListAsync(cancellationToken);

        foreach (var document in tokenDocuments)
        {
            var hash = BsonHelpers.GetString(document, "tokenHash");
            if (!_securityService.VerifySecret(rawToken, hash))
            {
                continue;
            }

            var context = ToTokenContext(document);
            if (!HasRequiredAccess(context.AccessMode, requirement))
            {
                return new ExternalApiTokenValidationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status403Forbidden,
                    Message = TokenRequirementMessage(requirement),
                    Context = context
                };
            }

            await TouchTokenUsageAsync(context.TokenId, cancellationToken);
            return new ExternalApiTokenValidationResult
            {
                Success = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Token accepted.",
                Context = context
            };
        }

        var tokenIdMatch = tokenDocuments.FirstOrDefault(document =>
            BsonHelpers.GetString(document, "tokenId").Equals(rawToken.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tokenIdMatch != null)
        {
            return new ExternalApiTokenValidationResult
            {
                Success = false,
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Token ID was provided instead of token value. Use the generated credential shown right after create/regenerate."
            };
        }

        return new ExternalApiTokenValidationResult
        {
            Success = false,
            StatusCode = StatusCodes.Status401Unauthorized,
            Message = "Token is invalid."
        };
    }

    public string RevealToken(BsonDocument tokenDocument)
    {
        return _securityService.Decrypt(BsonHelpers.GetString(tokenDocument, "encryptedToken"));
    }

    public string TryRevealToken(BsonDocument tokenDocument, string fallback)
    {
        try
        {
            return RevealToken(tokenDocument);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return fallback;
        }
    }

    private static ExternalApiTokenContext ToTokenContext(BsonDocument tokenDocument)
    {
        var accessMode = AccessModeFromValue(BsonHelpers.GetString(tokenDocument, "accessMode"));

        var clusters = tokenDocument.GetValue("clusterIds", new BsonArray()) is BsonArray clusterArray
            ? clusterArray
                .Select(item => item.ToString())
                .Where(clusterId => !string.IsNullOrWhiteSpace(clusterId))
                .Select(clusterId => clusterId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        return new ExternalApiTokenContext
        {
            TokenId = BsonHelpers.GetString(tokenDocument, "tokenId"),
            Name = BsonHelpers.GetString(tokenDocument, "name"),
            AccessMode = accessMode,
            GlobalAccess = tokenDocument.GetValue("globalAccess", false).ToBoolean(),
            AllowUnassigned = tokenDocument.GetValue("allowUnassigned", false).ToBoolean(),
            IsActive = tokenDocument.GetValue("isActive", false).ToBoolean(),
            ClusterIds = clusters,
            IsSample = tokenDocument.GetValue("isSample", false).ToBoolean()
        };
    }

    private static string AccessModeValue(ExternalTokenAccessMode accessMode)
    {
        return accessMode switch
        {
            ExternalTokenAccessMode.Lifecycle => "readWriteSign",
            ExternalTokenAccessMode.Sign => "sign",
            ExternalTokenAccessMode.ReadWrite => "readWrite",
            _ => "read"
        };
    }

    private static ExternalTokenAccessMode AccessModeFromValue(string accessMode)
    {
        if (accessMode.Equals("sign", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalTokenAccessMode.Sign;
        }

        if (accessMode.Equals("readWriteSign", StringComparison.OrdinalIgnoreCase)
            || accessMode.Equals("lifecycle", StringComparison.OrdinalIgnoreCase))
        {
            return ExternalTokenAccessMode.Lifecycle;
        }

        return accessMode.Equals("readWrite", StringComparison.OrdinalIgnoreCase)
            ? ExternalTokenAccessMode.ReadWrite
            : ExternalTokenAccessMode.Read;
    }

    private static bool HasRequiredAccess(ExternalTokenAccessMode accessMode, ExternalTokenRequirement requirement)
    {
        return requirement switch
        {
            ExternalTokenRequirement.Sign => accessMode is ExternalTokenAccessMode.Sign or ExternalTokenAccessMode.Lifecycle,
            ExternalTokenRequirement.Write => accessMode is ExternalTokenAccessMode.ReadWrite or ExternalTokenAccessMode.Lifecycle,
            _ => true
        };
    }

    private static string TokenRequirementMessage(ExternalTokenRequirement requirement)
    {
        return requirement switch
        {
            ExternalTokenRequirement.Sign => "Token does not have sign access.",
            ExternalTokenRequirement.Write => "Token does not have write access.",
            _ => "Token does not have read access."
        };
    }

    private async Task TouchTokenUsageAsync(string tokenId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenId) || _mongoContext.Database == null)
        {
            return;
        }

        var update = Builders<BsonDocument>.Update
            .Set("lastUsedAt", DateTime.UtcNow.ToString("O"))
            .Set("updatedAt", DateTime.UtcNow.ToString("O"));

        await _mongoContext.Database.GetCollection<BsonDocument>("apiTokens")
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("tokenId", tokenId.Trim()), update, cancellationToken: cancellationToken);
    }
}
