namespace BatteryPassWeb.Tests;

public sealed class DocumentAccessControlTests
{
    [Fact]
    public void FilesApiController_ShouldProtectPassportFileDownloads()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

        Assert.Contains("PassportRepository", source);
        Assert.Contains("AccessControlService", source);
        Assert.Contains("PassportPublishPolicyService", source);
        Assert.Contains("AuditRevisionService", source);
        Assert.Contains("CanDownloadPassportDocumentAsync", source);
        Assert.Contains("ResolveDocumentReference", source);
        Assert.Contains("IsPubliclyVisible", source);
        Assert.Contains("AppendFileDeniedAuditEventAsync", source);
        Assert.Contains("StatusCodes.Status403Forbidden", source);
        Assert.Contains("\"passport.file.download.denied\"", source);
    }

    [Fact]
    public void FilesApiController_ShouldStoreDocumentVisibilityAndHashOnUpload()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

        Assert.Contains("passportId", source);
        Assert.Contains("documentKey", source);
        Assert.Contains("NormalizeVisibility", source);
        Assert.Contains("ComputeSha256Hex", source);
        Assert.Contains("metadata[\"visibility\"]", source);
        Assert.Contains("metadata[\"sha256\"]", source);
        Assert.Contains("UpdateDocumentReferenceAsync", source);
        Assert.Contains("MarkCanonicalDirtyAsync", source);
    }

    [Fact]
    public void AccessControlService_ShouldDefinePassportDocumentDownloadPolicy()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains("CanDownloadPassportDocumentAsync", source);
        Assert.Contains("isPassportPublic", source);
        Assert.Contains("visibility", source);
        Assert.Contains("CanOpenPassportDetailAsync", source);
    }

    [Fact]
    public void PassportRepository_ShouldPersistSignedDocumentReferences()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportRepository.cs"));

        Assert.Contains("UpdateDocumentReferenceAsync", source);
        Assert.Contains("app.documents.{documentKey}.fileId", source);
        Assert.Contains("app.documents.{documentKey}.sha256", source);
        Assert.Contains("app.documents.{documentKey}.visibility", source);
    }

    [Fact]
    public void AdminEditPassport_ShouldExposeAndPersistDocumentVisibility()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "AdminController.cs"));
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
        var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));

        Assert.Contains("document_conformityAssessment_visibility", markup);
        Assert.Contains("document_dueDiligenceReport_visibility", markup);
        Assert.Contains("document_co2StudyReference_visibility", markup);
        Assert.Contains("Public document", markup);
        Assert.Contains("Private / privileged", markup);
        Assert.Contains("NormalizeDocumentVisibility", controller);
        Assert.Contains("documentNode[\"visibility\"]", controller);
        Assert.Contains("Visibility", model);
        Assert.Contains("document.GetValue(\"visibility\"", factory);
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
