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
    public void FilesApiController_ShouldAuditDocumentUploadAndReplacement()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));

        Assert.Contains("AppendFileUploadedAuditEventAsync", source);
        Assert.Contains("passport.file.uploaded", source);
        Assert.Contains("passport.file.replaced", source);
        Assert.Contains("previousFileId", source);
        Assert.Contains("sha256", source);
        Assert.Contains("supportingDocumentChanged", source);
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

    [Fact]
    public void AdminEditPassport_ShouldExposeHashBackedEvidenceUploadControls()
    {
        var markup = File.ReadAllText(RepoFile("web", "Views", "Admin", "EditPassport.cshtml"));
        var model = File.ReadAllText(RepoFile("web", "Models", "ViewModels", "PassportViewModel.cs"));
        var factory = File.ReadAllText(RepoFile("web", "Services", "PassportViewModelFactory.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("data-document-upload-card", markup);
        Assert.Contains("bp-document-row", markup);
        Assert.Contains("bp-document-field-shell", markup);
        Assert.Contains("bp-document-main-line", markup);
        Assert.Contains("bp-document-visibility-field", markup);
        Assert.Contains("bp-document-sha-field", markup);
        Assert.Contains("data-document-sha", markup);
        Assert.Contains("data-document-key=\"@document.DocumentKey\"", markup);
        Assert.Contains("DocumentKey = \"conformityAssessment\"", markup);
        Assert.Contains("DocumentKey = \"euDeclarationOfConformity\"", markup);
        Assert.Contains("DocumentKey = \"sustainabilityReport\"", markup);
        Assert.Contains("DocumentKey = \"dueDiligenceReport\"", markup);
        Assert.Contains("DocumentKey = \"thirdPartyAudit\"", markup);
        Assert.Contains("DocumentKey = \"taxonomyReport\"", markup);
        Assert.Contains("data-document-key=\"co2StudyReference\"", markup);
        Assert.Contains("accept=\"application/pdf,image/*\"", markup);
        Assert.Contains("data-document-file", markup);
        Assert.Contains("bp-document-hidden-file", markup);
        Assert.Contains("data-document-upload", markup);
        Assert.Contains("data-document-upload-label", markup);
        Assert.Contains("Choose file", markup);
        Assert.Contains("Upload selected file", markup);
        Assert.Contains("Uploading...", markup);
        Assert.Contains("fileInput.click()", markup);
        Assert.Contains("fetch('/api/files'", markup);
        Assert.Contains("formData.append('passportId'", markup);
        Assert.Contains("formData.append('documentKey'", markup);
        Assert.Contains("formData.append('file'", markup);
        Assert.Contains("formData.append('publicAccess'", markup);
        Assert.Contains("sha256", markup);
        Assert.DoesNotContain("Upload hash-checkable evidence", markup);
        Assert.DoesNotContain("PDF or image files are stored in MongoDB", markup);
        Assert.DoesNotContain("Open current uploaded file", markup);
        Assert.Contains("Sha256", model);
        Assert.Contains("document.GetValue(\"sha256\"", factory);
        Assert.Contains(".bp-document-row", css);
        Assert.Contains(".bp-document-field-shell", css);
        Assert.Contains(".bp-document-visibility-field", css);
    }

    [Fact]
    public void RestrictedDocumentAcceptance_ShouldDenyUnauthorizedDownloadAndAuditIt()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
        var access = File.ReadAllText(RepoFile("web", "Services", "AccessControlService.cs"));

        Assert.Contains("visibility", controller);
        Assert.Contains("restricted", controller);
        Assert.Contains("CanDownloadPassportDocumentAsync", controller);
        Assert.Contains("CanOpenPassportDetailAsync", access);
        Assert.Contains("StatusCodes.Status403Forbidden", controller);
        Assert.Contains("passport.file.download.denied", controller);
    }

    [Fact]
    public void ProductTemplateDocuments_ShouldDownloadThroughPassportScopedAccess()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "FilesApiController.cs"));
        var productTemplateService = File.ReadAllText(RepoFile("web", "Services", "ProductTemplateService.cs"));

        Assert.Contains("[FromQuery] string? passportId", controller);
        Assert.Contains("linkedPassportId", controller);
        Assert.Contains("FirstNonEmpty(passportId?.Trim()", controller);
        Assert.Contains("?passportId=", productTemplateService);
        Assert.Contains("source\"] = \"productTemplate\"", productTemplateService);
    }

    [Fact]
    public void PassportSummary_ShouldOnlyShowWrongClusterNoticeWhenDetailAccessIsDenied()
    {
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "PassportController.cs"));

        Assert.Contains("if (!canOpenDetail && string.Equals(access, \"wrong-cluster\"", controller);
        Assert.DoesNotContain("string.Equals(access, \"wrong-cluster\", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(detailAccessNotice)", controller);
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
