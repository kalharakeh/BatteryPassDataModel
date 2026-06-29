namespace BatteryPassWeb.Tests;

public sealed class ExternalApiHashOnlyTokenTests
{
    [Fact]
    public void Repository_ShouldStoreOnlyTokenHashesForCreatedFixedAndRegeneratedTokens()
    {
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var createToken = ExtractMethod(repository, "CreateTokenAsync");
        var upsertFixedToken = ExtractMethod(repository, "UpsertFixedTokenAsync");
        var regenerateToken = ExtractMethod(repository, "RegenerateTokenAsync");

        Assert.Contains("[\"tokenHash\"] = _securityService.HashSecret(token)", createToken);
        Assert.DoesNotContain("encryptedToken", createToken);
        Assert.DoesNotContain("_securityService.Encrypt", createToken);

        Assert.Contains(".Set(\"tokenHash\", _securityService.HashSecret(normalizedToken))", upsertFixedToken);
        Assert.Contains(".Unset(\"encryptedToken\")", upsertFixedToken);
        Assert.DoesNotContain("_securityService.Encrypt", upsertFixedToken);

        Assert.Contains(".Set(\"tokenHash\", _securityService.HashSecret(newToken))", regenerateToken);
        Assert.Contains(".Unset(\"encryptedToken\")", regenerateToken);
        Assert.DoesNotContain("_securityService.Encrypt", regenerateToken);
    }

    [Fact]
    public void Repository_ShouldRemoveLegacyEncryptedTokensAtStartup()
    {
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var ensureIndexes = ExtractMethod(repository, "EnsureIndexesAsync");

        Assert.Contains("Filter.Exists(\"encryptedToken\")", ensureIndexes);
        Assert.Contains("Update.Unset(\"encryptedToken\")", ensureIndexes);
        Assert.Contains("UpdateManyAsync", ensureIndexes);
    }

    [Fact]
    public void Repository_ShouldNotExposeStoredTokenDecryptors()
    {
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));

        Assert.DoesNotContain("RevealToken", repository);
        Assert.DoesNotContain("TryRevealToken", repository);
        Assert.DoesNotContain("_securityService.Decrypt", repository);
        Assert.DoesNotContain("BsonHelpers.GetString(tokenDocument, \"encryptedToken\")", repository);
    }

    [Fact]
    public void AdminAndHelp_ShouldUseShowOnceOrExplicitDemoFallbacksInsteadOfStoredReveal()
    {
        var admin = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var clusterAdmin = File.ReadAllText(RepoFile("web", "Controllers", "ClusterAdminController.cs"));
        var help = File.ReadAllText(RepoFile("web", "Controllers", "HelpController.cs"));

        Assert.Contains("TempData[\"GeneratedCredential\"] = tokenValue", admin);
        Assert.Contains("TempData[\"GeneratedCredential\"] = newToken", admin);
        Assert.Contains("TempData[\"GeneratedCredential\"] = tokenValue", clusterAdmin);
        Assert.Contains("GeneratedCredential", clusterAdmin);
        Assert.Contains("SampleReadToken = _options.EnableDemoData", help);
        Assert.Contains("ExternalApiInitializer.SampleReadTokenValue", help);
        Assert.Contains("ExternalApiInitializer.SampleReadWriteTokenValue", help);
        Assert.Contains("ExternalApiInitializer.SampleLifecycleTokenValue", help);

        Assert.DoesNotContain("TryRevealToken", admin);
        Assert.DoesNotContain("TryRevealToken", help);
    }

    [Fact]
    public void Repository_ShouldStillValidateAgainstTokenHash()
    {
        var repository = File.ReadAllText(RepoFile("web", "Services", "ExternalApiRepository.cs"));
        var validateToken = ExtractMethod(repository, "ValidateTokenAsync", occurrence: 3);

        Assert.Contains("BsonHelpers.GetString(document, \"tokenHash\")", validateToken);
        Assert.Contains("_securityService.VerifySecret(rawToken, hash)", validateToken);
    }

    private static string ExtractMethod(string source, string methodName, int occurrence = 1)
    {
        var marker = methodName + "(";
        var index = -1;
        for (var i = 0; i < occurrence; i++)
        {
            index = source.IndexOf(marker, index + 1, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidOperationException($"Method {methodName} occurrence {occurrence} was not found.");
            }
        }

        var methodStart = source.LastIndexOf('\n', index);
        var nextMethod = source.IndexOf("\n    public ", index + marker.Length, StringComparison.Ordinal);
        if (nextMethod < 0)
        {
            nextMethod = source.IndexOf("\n    private ", index + marker.Length, StringComparison.Ordinal);
        }

        return nextMethod < 0
            ? source[methodStart..]
            : source[methodStart..nextMethod];
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(parts)}");
    }
}
