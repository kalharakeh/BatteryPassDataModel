using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>Round three, 2: one place turns a period into words, and a value too large to read shows as written.</summary>
public sealed class PeriodWordsTests
{
    [Theory]
    [InlineData("P8Y", "8 years")]
    [InlineData("P1Y6M", "1 year 6 months")]
    [InlineData("P6M", "6 months")]
    [InlineData("P99999999999Y", "P99999999999Y")]
    [InlineData("P1Y99999999999M", "P1Y99999999999M")]
    [InlineData("eight years", "eight years")]
    public void APeriod_ShouldReadInWordsOrAsWritten(string stored, string expected)
    {
        Assert.Equal(expected, ValueWords.Period(stored));
    }

    [Fact]
    public void TheBatteryPagesHeaderLine_ShouldUseTheOneSafeReading()
    {
        var view = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Batteries", "Battery.cshtml"));

        Assert.Contains("string Period(string text) => ValueWords.Period(text);", view, StringComparison.Ordinal);
        Assert.DoesNotContain("int.Parse", view, StringComparison.Ordinal);
    }
}
