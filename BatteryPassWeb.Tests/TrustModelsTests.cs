using BatteryPassWeb.Models.Trust;

namespace BatteryPassWeb.Tests;

public sealed class TrustModelsTests
{
    [Fact]
    public void ValidationSummary_ShouldCountBlockingErrorsAndWarnings()
    {
        var summary = new TrustValidationSummary
        {
            Sections =
            [
                new TrustValidationSectionResult
                {
                    SectionKey = "generalProductInformation",
                    SectionLabel = "General",
                    Issues =
                    [
                        new TrustValidationIssue(TrustValidationSeverity.BlockingError, "passportId", "Passport ID is required."),
                        new TrustValidationIssue(TrustValidationSeverity.Warning, "batteryImageUrl", "Battery image uses fallback.")
                    ]
                },
                new TrustValidationSectionResult
                {
                    SectionKey = "performanceAndDurability",
                    SectionLabel = "Performance",
                    Issues =
                    [
                        new TrustValidationIssue(TrustValidationSeverity.Passed, "ratedEnergy", "Rated energy is present.")
                    ]
                }
            ]
        };

        Assert.Equal(1, summary.BlockingErrorCount);
        Assert.Equal(1, summary.WarningCount);
        Assert.Equal(1, summary.PassedCount);
        Assert.False(summary.CanSign);
    }

    [Fact]
    public void TrustState_ShouldExposeKnownLifecycleValues()
    {
        Assert.Equal("unvalidated", TrustState.Unvalidated);
        Assert.Equal("invalid", TrustState.Invalid);
        Assert.Equal("valid", TrustState.Valid);
        Assert.Equal("signed", TrustState.Signed);
        Assert.Equal("dirty", TrustState.Dirty);
        Assert.Equal("signatureInvalid", TrustState.SignatureInvalid);
    }
}
