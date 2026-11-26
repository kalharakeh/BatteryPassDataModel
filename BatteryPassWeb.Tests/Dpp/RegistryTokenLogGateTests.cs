using BatteryPassWeb.Dpp.RegistryToken;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 round two, row 4: a refused token (401) is one log line per caller's address per minute, so a flood of junk tokens
/// can't fill the log. The next line says how many were left out. Valid reads and 403s are always logged.
/// </summary>
public sealed class RegistryTokenLogGateTests
{
    private readonly TestClock _clock = new(Start);

    [Fact]
    public void TheFirst401_ShouldBeLogged_TheRestOfTheMinuteCounted()
    {
        var gate = new RegistryTokenLogGate(_clock);

        Assert.True(gate.Allow("203.0.113.7", out var skipped));
        Assert.Equal(0, skipped);
        for (var i = 0; i < 5; i++)
        {
            _clock.Now = _clock.Now.AddSeconds(10);
            Assert.False(gate.Allow("203.0.113.7", out _));
        }

        _clock.Now = Start.AddSeconds(60);
        Assert.True(gate.Allow("203.0.113.7", out skipped));
        Assert.Equal(5, skipped);
    }

    [Fact]
    public void EachAddress_ShouldHaveItsOwnLine()
    {
        var gate = new RegistryTokenLogGate(_clock);

        Assert.True(gate.Allow("203.0.113.7", out _));
        Assert.True(gate.Allow("203.0.113.8", out _));
        Assert.False(gate.Allow("203.0.113.7", out _));
    }

    [Fact]
    public void ManyAddresses_ShouldNotGrowTheGateWithoutEnd()
    {
        var gate = new RegistryTokenLogGate(_clock);

        for (var i = 0; i < RegistryTokenLogGate.MaxAddresses * 3; i++)
        {
            gate.Allow($"10.0.{i / 250}.{i % 250}", out _);
        }

        Assert.True(gate.Count <= RegistryTokenLogGate.MaxAddresses);
    }
}
