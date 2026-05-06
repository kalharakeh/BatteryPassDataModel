using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http;

namespace BatteryPassWeb.Services;

public enum ExternalTokenAccessMode
{
    Read,
    ReadWrite
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

public sealed class BatterySecretValidationResult
{
    public bool IsRequired { get; init; }
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class ExternalApiRepository
{
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

        var secrets = _mongoContext.Database.GetCollection<BsonDocument>("batterySecrets");
        var secretIndexes = new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("passportId"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("clusterId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("isActive"))
        };
        await secrets.Indexes.CreateManyAsync(secretIndexes, cancellationToken);
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
            ["accessMode"] = accessMode == ExternalTokenAccessMode.ReadWrite ? "readWrite" : "read",
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

    public async Task<ExternalApiTokenValidationResult> ValidateTokenAsync(string rawToken, bool requireWrite, CancellationToken cancellationToken = default)
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
            if (requireWrite && context.AccessMode != ExternalTokenAccessMode.ReadWrite)
            {
                return new ExternalApiTokenValidationResult
                {
                    Success = false,
                    StatusCode = StatusCodes.Status403Forbidden,
                    Message = "Token does not have write access.",
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

    public async Task<IReadOnlyList<BsonDocument>> ListBatterySecretsAsync(IReadOnlyCollection<string>? clusterIds = null, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return [];
        }

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Empty;
        if (clusterIds != null && clusterIds.Count > 0)
        {
            filter = Builders<BsonDocument>.Filter.In("clusterId", clusterIds);
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("batterySecrets")
            .Find(filter)
            .SortBy(document => document["passportId"])
            .ToListAsync(cancellationToken);
    }

    public async Task<BsonDocument?> GetBatterySecretAsync(string passportId, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(passportId))
        {
            return null;
        }

        return await _mongoContext.Database.GetCollection<BsonDocument>("batterySecrets")
            .Find(Builders<BsonDocument>.Filter.Eq("passportId", passportId.Trim()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> UpsertBatterySecretAsync(
        string passportId,
        string clusterId,
        string actor,
        bool active,
        string? fixedSecret = null,
        CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            throw new InvalidOperationException("Database is not connected.");
        }

        var secret = string.IsNullOrWhiteSpace(fixedSecret) ? _securityService.CreateToken() : fixedSecret.Trim();
        var now = DateTime.UtcNow.ToString("O");
        var update = Builders<BsonDocument>.Update
            .Set("passportId", passportId.Trim())
            .Set("clusterId", clusterId.Trim())
            .Set("isActive", active)
            .Set("secretHash", _securityService.HashSecret(secret))
            .Set("encryptedSecret", _securityService.Encrypt(secret))
            .Set("updatedBy", actor)
            .Set("updatedAt", now)
            .SetOnInsert("createdBy", actor)
            .SetOnInsert("createdAt", now);

        await _mongoContext.Database.GetCollection<BsonDocument>("batterySecrets")
            .UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("passportId", passportId.Trim()),
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);

        return secret;
    }

    public async Task<bool> SetBatterySecretActiveAsync(string passportId, bool active, string actor, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null || string.IsNullOrWhiteSpace(passportId))
        {
            return false;
        }

        var update = Builders<BsonDocument>.Update
            .Set("isActive", active)
            .Set("updatedBy", actor)
            .Set("updatedAt", DateTime.UtcNow.ToString("O"));

        var result = await _mongoContext.Database.GetCollection<BsonDocument>("batterySecrets")
            .UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("passportId", passportId.Trim()), update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<BatterySecretValidationResult> ValidateBatterySecretAsync(string passportId, string? providedSecret, CancellationToken cancellationToken = default)
    {
        if (_mongoContext.Database == null)
        {
            return new BatterySecretValidationResult
            {
                IsRequired = true,
                IsValid = false,
                Message = "Database is not connected."
            };
        }

        var secretDocument = await GetBatterySecretAsync(passportId, cancellationToken);
        if (secretDocument == null || !secretDocument.GetValue("isActive", false).ToBoolean())
        {
            return new BatterySecretValidationResult
            {
                IsRequired = false,
                IsValid = true,
                Message = "Battery secret is not required."
            };
        }

        if (string.IsNullOrWhiteSpace(providedSecret))
        {
            return new BatterySecretValidationResult
            {
                IsRequired = true,
                IsValid = false,
                Message = "Battery secret is required."
            };
        }

        var hash = BsonHelpers.GetString(secretDocument, "secretHash");
        var isValid = _securityService.VerifySecret(providedSecret.Trim(), hash);
        return new BatterySecretValidationResult
        {
            IsRequired = true,
            IsValid = isValid,
            Message = isValid ? "Battery secret accepted." : "Battery secret is invalid."
        };
    }

    public string RevealToken(BsonDocument tokenDocument)
    {
        return _securityService.Decrypt(BsonHelpers.GetString(tokenDocument, "encryptedToken"));
    }

    public string RevealBatterySecret(BsonDocument secretDocument)
    {
        return _securityService.Decrypt(BsonHelpers.GetString(secretDocument, "encryptedSecret"));
    }

    private static ExternalApiTokenContext ToTokenContext(BsonDocument tokenDocument)
    {
        var accessMode = BsonHelpers.GetString(tokenDocument, "accessMode")
            .Equals("readWrite", StringComparison.OrdinalIgnoreCase)
            ? ExternalTokenAccessMode.ReadWrite
            : ExternalTokenAccessMode.Read;

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
