using BatteryPassWeb.Dpp.RegistryToken;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using static BatteryPassWeb.Tests.Dpp.RegistryTokenTesting;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// R3 (the guide's §4.3): exactly one Authorization header, of the Bearer scheme. Two header lines are refused as such,
/// before any token in them is read; the same two joined into one line by a client are one malformed token.
/// </summary>
public sealed class RegistryTokenRequestTests : IDisposable
{
    private readonly System.Security.Cryptography.ECDsa _key = NewKey();
    private readonly TestClock _clock = new(Start);

    public void Dispose() => _key.Dispose();

    private string Valid() => Token(_key, Header(), Claims(Start));

    private Task<RegistryTokenResult> CheckAsync(StringValues authorization)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = authorization;
        return RegistryTokenRequest.CheckAsync(context.Request, Check(KeySet.With((_key, KeyId)), _clock));
    }

    [Fact]
    public async Task OneValidBearerHeader_ShouldBeAuthorised()
    {
        Assert.True((await CheckAsync($"Bearer {Valid()}")).Authorised);
    }

    [Fact]
    public async Task TwoHeaderLines_ShouldBeRefused_AsSeveralHeaders_EvenWithTwoValidTokens()
    {
        var result = await CheckAsync(new StringValues([$"Bearer {Valid()}", $"Bearer {Valid()}"]));

        Assert.Equal((RegistryTokenVerdict.OtherScheme, "several-headers", 401), (result.Verdict, result.Reason, result.Status));
    }

    [Fact]
    public async Task TwoTokensJoinedInOneLine_ShouldBeOneMalformedToken()
    {
        var result = await CheckAsync($"Bearer {Valid()}, Bearer {Valid()}");

        Assert.Equal((RegistryTokenVerdict.Invalid, "malformed"), (result.Verdict, result.Reason));
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz", "other-scheme")]
    [InlineData("Bearer", "other-scheme")]
    [InlineData("bearer ", "malformed")]
    public async Task AnotherSchemeOrNoToken_ShouldBe401(string authorization, string reason)
    {
        var result = await CheckAsync(authorization);

        Assert.Equal((401, reason), (result.Status, result.Reason));
    }
}
