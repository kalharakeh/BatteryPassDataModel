using BatteryPassWeb.Services;

namespace BatteryPassWeb.Tests;

public sealed class QrWorkflowTests
{
    [Fact]
    public void Program_ShouldRegisterQrGenerationService()
    {
        var program = File.ReadAllText(RepoFile("web", "Program.cs"));
        var project = File.ReadAllText(RepoFile("web", "BatteryPassWeb.csproj"));

        Assert.Contains("AddSingleton<PassportQrCodeService>", program);
        Assert.Contains("PackageReference Include=\"QRCoder\"", project);
    }

    [Fact]
    public void QrController_ShouldExposeSvgPreviewAndDownloadRoutes()
    {
        var source = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));

        Assert.Contains("[Route(\"qr\")]", source);
        Assert.Contains("[HttpGet(\"{batteryId}/svg\")]", source);
        Assert.Contains("[HttpGet(\"{batteryId}/download\")]", source);
        Assert.Contains("PassportQrCodeService", source);
        Assert.Contains("PassportPublishPolicyService", source);
        Assert.Contains("BatteryRepository", source);
        Assert.Contains("CanOpenPassportDetailAsync", source);
        Assert.Contains("image/svg+xml", source);
        Assert.Contains("BuildPayloadUrl", source);
        Assert.Contains("CreateSampleBatteryId", source);
    }

    [Fact]
    public void PassportQrCodeService_ShouldGenerateSvgForSummaryPayload()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));

        Assert.Contains("using QRCoder", source);
        Assert.Contains("CreateQrCode", source);
        Assert.Contains("SvgQRCode", source);
        Assert.Contains("AddBatteryCellMark", source);
        Assert.Contains("bp-qr-battery-mark", source);
        Assert.Contains("/latest", source);
        Assert.Contains("BuildPayloadUrl", source);
        Assert.Contains("BuildFileName", source);
    }

    [Fact]
    public void PassportQrCodeService_ShouldEmbedBatteryCellMarkWithoutReplacingQr()
    {
        var svg = new PassportQrCodeService().GenerateSvg("https://example.test/did%3Aweb%3Alocal.battery.pass%3Aqr/summary");

        Assert.Contains("<svg", svg);
        Assert.Contains("bp-qr-battery-mark", svg);
        Assert.Contains("bp-qr-battery-cell", svg);
        Assert.Contains("#18bd84", svg);
    }

    [Fact]
    public void PassportSummary_ShouldShowDownloadableQrBesideBatteryImage()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));

        Assert.Contains("bp-report-hero-qr", summary);
        Assert.Contains("bp-report-hero-qr", detail);
        Assert.Contains("bp-report-hero-media", summary);
        Assert.Contains("bp-report-hero-media", detail);
        Assert.Contains("/qr/", summary);
        Assert.Contains("/qr/", detail);
        Assert.Contains("qrBatteryId", summary);
        Assert.Contains("qrBatteryId", detail);
        Assert.Contains("/svg", summary);
        Assert.Contains("/svg", detail);
        Assert.Contains("/download", summary);
        Assert.Contains("/download", detail);
        Assert.DoesNotContain("@passport.BatteryImageAlt</p>", summary);
        Assert.DoesNotContain("Download QR", summary);
        Assert.DoesNotContain("QR code for battery ID", summary);
        Assert.Contains(".bp-report-hero-qr", File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css")));
        Assert.DoesNotContain("QR access", detail);
        Assert.DoesNotContain("Download QR", detail);
    }

    [Fact]
    public void SummaryReport_ShouldUseThreeColumnHeaderAndImageQrDownload()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var qrController = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("bp-summary-identity-grid", summary);
        Assert.Contains("bp-summary-id-panel", summary);
        Assert.Contains("bp-summary-qr-panel", summary);
        Assert.Contains("image/png", qrController);
        Assert.Contains(".bp-summary-identity-grid", css);
        Assert.Contains("@media (max-width: 900px)", css);
    }

    [Fact]
    public void LandingPage_ShouldOpenQrScannerFromInlineSearchIcon()
    {
        var landing = File.ReadAllText(RepoFile("web", "Views", "Home", "Index.cshtml"));
        var search = File.ReadAllText(RepoFile("web", "Views", "Home", "Search.cshtml"));
        var home = File.ReadAllText(RepoFile("web", "Controllers", "HomeController.cs"));
        var css = File.ReadAllText(RepoFile("web", "wwwroot", "css", "site.css"));

        Assert.Contains("data-search-form", landing);
        Assert.Contains("data-empty-search-message", landing);
        Assert.Contains("searchWasNotFound", landing);
        Assert.Contains("data-qr-scanner", landing);
        Assert.Contains("data-qr-trigger", landing);
        Assert.Contains("bp-qr-trigger-icon", landing);
        Assert.Contains("data-qr-modal", landing);
        Assert.Contains("data-qr-close", landing);
        Assert.Contains("data-qr-camera", landing);
        Assert.Contains("data-qr-file", landing);
        Assert.Contains("data-qr-video", landing);
        Assert.Contains("BarcodeDetector", landing);
        Assert.Contains("~/lib/jsqr/jsQR.js", landing);
        Assert.Contains("accept=\"image/*,.svg\"", landing);
        Assert.Contains("getUserMedia", landing);
        Assert.Contains("loadQrImage", landing);
        Assert.Contains("detectQrWithFallback", landing);
        Assert.Contains("readQrPixels", landing);
        Assert.Contains("window.jsQR", landing);
        Assert.Contains("getImageData", landing);
        Assert.Contains("URL.createObjectURL", landing);
        Assert.Contains("extractDidFromQr", landing);
        Assert.Contains("QR scan failed", landing);
        Assert.Contains("event.preventDefault()", landing);
        Assert.Contains("ViewData[\"SearchNotFound\"]", home);
        Assert.Contains("return RedirectToAction(nameof(Index));", home);
        Assert.Contains("notFound = \"1\"", home);
        Assert.DoesNotContain("data-qr-scanner", search);
        Assert.DoesNotContain("data-qr-trigger", search);
        Assert.Contains(".bp-search-input-shell", css);
        Assert.Contains(".bp-qr-trigger", css);
        Assert.Contains(".bp-qr-trigger-icon", css);
        Assert.Contains("z-index: 2", css);
        Assert.Contains(".bp-qr-modal", css);
        Assert.Contains(".bp-qr-video", css);
    }

    [Fact]
    public void QrScanner_ShouldShipLocalBrowserIndependentDecoder()
    {
        var decoder = File.ReadAllText(RepoFile("web", "wwwroot", "lib", "jsqr", "jsQR.js"));
        var license = File.ReadAllText(RepoFile("web", "wwwroot", "lib", "jsqr", "LICENSE"));

        Assert.Contains("jsQR", decoder);
        Assert.Contains("webpackUniversalModuleDefinition", decoder);
        Assert.Contains("Apache License", license);
    }

    [Fact]
    public void QrService_ShouldBuildPublicSummaryPayloadForDemoScenarioPassport()
    {
        var source = File.ReadAllText(RepoFile("web", "Services", "PassportQrCodeService.cs"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));

        Assert.Contains("/latest/summary", source);
        Assert.Contains("BuildPayloadUrl", source);
        Assert.Contains("GetLatestPublicByBatteryIdAsync", controller);
        Assert.Contains("CanOpenPassportDetailAsync", controller);
    }

    [Fact]
    public void FollowupQrWorkflow_ShouldRenderInsideReportIdentityHeader()
    {
        var summary = File.ReadAllText(RepoFile("web", "Views", "Passport", "Summary.cshtml"));
        var detail = File.ReadAllText(RepoFile("web", "Views", "Passport", "Detail.cshtml"));
        var controller = File.ReadAllText(RepoFile("web", "Controllers", "QrController.cs"));

        foreach (var view in new[] { summary, detail })
        {
            Assert.Contains("bp-report-identity-grid", view);
            Assert.Contains("bp-report-identity-panel--qr", view);
            Assert.Contains("/qr/@Uri.EscapeDataString(qrBatteryId)/download", view);
            Assert.Contains("/qr/@Uri.EscapeDataString(qrBatteryId)/svg", view);
            Assert.DoesNotContain("bp-report-media-code-row", view);
        }

        Assert.Contains("image/png", controller);
        Assert.Contains("GetLatestPublicByBatteryIdAsync", controller);
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
