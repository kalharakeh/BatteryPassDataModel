using System.Text;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>FU2: every message about the upload limit says it in megabytes, as the rest of the app does, never as a long number of bytes.</summary>
[Collection(MongoCollection.Name)]
public sealed class UploadLimitWordsTests(MongoFixture mongo)
{
    private const long TenMegabytes = 10_485_760;

    [Fact]
    public async Task ADocumentOverTheUploadLimit_ShouldBeRefusedInMegabytesWithoutALongNumber()
    {
        var rig = await PublicationTesting.RigAsync(mongo);
        var tooBig = new byte[TenMegabytes + 1];
        Encoding.ASCII.GetBytes("%PDF-1.7\n").CopyTo(tooBig, 0);

        var result = await DocumentServiceTests.Service(rig).UploadAsync(PublicationTesting.Team, TargetKind.Model, "model-test", "dismantlingInformation", new DocumentUpload("big.pdf", tooBig));

        Assert.Contains("10 MB", result.Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("10485760", result.Problem, StringComparison.Ordinal);
        Assert.DoesNotContain("byte", result.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APictureOverTheUploadLimit_ShouldBeRefusedInMegabytesWithoutALongNumber()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var tooBig = new byte[TenMegabytes + 1];
        DocumentTesting.Png().CopyTo(tooBig, 0);

        var problem = PageTesting.Pages(rig).PictureProblem(tooBig);

        Assert.Contains("10 MB", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("10485760", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10_485_760, "10 MB")]
    [InlineData(1_048_576, "1 MB")]
    [InlineData(5_242_880, "5 MB")]
    [InlineData(1_572_864, "1.5 MB")]
    [InlineData(10, "10 bytes")]
    public void TheLimitsWords_ShouldBeMegabytes(long bytes, string expected)
    {
        Assert.Equal(expected, UploadLimitWords.Size(bytes));
    }

    [Fact]
    public void ARequestOverTheBodyLimit_ShouldNameTheLimitInMegabytes()
    {
        var message = UploadLimitWords.RequestTooLarge(1_048_576);

        Assert.Contains("1 MB", message, StringComparison.Ordinal);
        Assert.DoesNotContain("1048576", message, StringComparison.Ordinal);
    }
}
