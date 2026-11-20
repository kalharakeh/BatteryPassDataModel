using BatteryPassWeb.Dpp.Publication;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Round seven, 4: one run of the background check catches everything except the stopping token's own cancellation, so
/// a stray cancellation (a driver's time-out, another token) can't stop the app; the next hour tries again.
/// </summary>
public sealed class PassportCheckServiceTests
{
    [Fact]
    public async Task AStrayCancellation_ShouldBeCaught_AndTheServiceGoOn()
    {
        using var other = new CancellationTokenSource();
        other.Cancel();

        var goOn = await PassportCheckService.RunOnceAsync(_ => throw new OperationCanceledException(other.Token), null, CancellationToken.None);

        Assert.True(goOn);
    }

    [Fact]
    public async Task AnyOtherFailure_ShouldBeCaught_AndTheServiceGoOn()
    {
        Assert.True(await PassportCheckService.RunOnceAsync(_ => throw new TimeoutException("the database"), null, CancellationToken.None));
    }

    [Fact]
    public async Task TheAppStopping_ShouldEndTheService()
    {
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        var goOn = await PassportCheckService.RunOnceAsync(token => throw new OperationCanceledException(token), null, stopping.Token);

        Assert.False(goOn);
    }
}
