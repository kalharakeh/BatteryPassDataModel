using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 6: the data answers carry the same Cache-Control as the public page (which the page gets from its
/// form protection: "no-cache, no-store" and "Pragma: no-cache"), the 404 included.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class PublicDataCachingTests(MongoFixture mongo)
{
    [Fact]
    public async Task TheDataAnswers_ShouldNotBeCached_TheirNotFoundIncluded()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo);
        var id = PassportPagesTests.Identifier(rig);

        foreach (var (target, found) in new[] { (id, true), ("VPC8C05-0000", false) })
        {
            foreach (var call in new Func<BatteryPassWeb.Controllers.PassportController, Task<IActionResult>>[]
            {
                controller => controller.SummaryData(target, CancellationToken.None),
                controller => controller.SummaryJson(target, CancellationToken.None),
            })
            {
                var controller = PublicPassportDataTests.Controller(rig);
                var result = await call(controller);

                Assert.Equal(found, result is ContentResult);
                Assert.Equal("no-cache, no-store", controller.Response.Headers.CacheControl.ToString());
                Assert.Equal("no-cache", controller.Response.Headers.Pragma.ToString());
            }
        }
    }
}
