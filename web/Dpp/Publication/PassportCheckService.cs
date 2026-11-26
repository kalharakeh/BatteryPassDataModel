namespace BatteryPassWeb.Dpp.Publication;

/// <summary>
/// Decision 65: the check of all published passports, off any request: shortly after the app has started, and then every
/// hour. It puts the markers of waiting changes and the active versions right whatever went wrong (a fault, bad timing,
/// data from before an update) and never publishes. Two app instances running it at once are harmless: every write it
/// makes is conditional or idempotent. The setting DPP_PASSPORT_CHECK=off (or BatteryPass:PassportCheck=off) turns it off.
/// </summary>
public sealed class PassportCheckService(PublicationService publication, IConfiguration? configuration = null, ILogger<PassportCheckService>? logger = null) : BackgroundService
{
    /// <summary>How long after start-up the first check runs: start-up itself isn't held up.</summary>
    public static readonly TimeSpan FirstRunAfter = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan Every = TimeSpan.FromHours(1);

    /// <summary>Who the check's audit entry names.</summary>
    public const string Actor = "passport-check";

    /// <summary>False when the setting turns the background check off.</summary>
    public static bool IsOn(IConfiguration? configuration)
    {
        var value = configuration?["DPP_PASSPORT_CHECK"] ?? configuration?["BatteryPass:PassportCheck"];
        return value?.Trim().ToLowerInvariant() is not ("off" or "false" or "0" or "no");
    }

    /// <summary>
    /// One run of the check. Everything it throws is logged, so the next hour tries again, except the stopping token's
    /// own cancellation: then it answers false and the service ends. A stray cancellation (a driver's time-out, a token
    /// that isn't the app's) never stops the host (round seven, 4).
    /// </summary>
    public static async Task<bool> RunOnceAsync(Func<CancellationToken, Task<PassportCheckResult>> check, ILogger? logger, CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        try
        {
            var result = await check(stoppingToken);
            logger?.LogInformation("Passport check: {Words}", result.Words);
            return !stoppingToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false; // the app is stopping
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "The passport check failed; it runs again in an hour.");
            return true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsOn(configuration))
        {
            logger?.LogInformation("The passport check is off (DPP_PASSPORT_CHECK).");
            return;
        }

        try
        {
            await Task.Delay(FirstRunAfter, stoppingToken);
            using var timer = new PeriodicTimer(Every);
            do
            {
                if (!await RunOnceAsync(token => publication.ReconcileAsync(Actor, token), logger, stoppingToken))
                {
                    return;
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping.
        }
    }
}
