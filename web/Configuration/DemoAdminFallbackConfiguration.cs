namespace BatteryPassWeb.Configuration;

/// <summary>
/// The fallback admin (ENABLE_DEMO_ADMIN_FALLBACK, DEMO_ADMIN_EMAIL, DEMO_ADMIN_PASSWORD) is for a developer's own machine
/// (finding R21): never in Production, and only with a password the environment gives; no default is written anywhere.
/// </summary>
public static class DemoAdminFallbackConfiguration
{
    public const string NoPasswordWarning =
        "ENABLE_DEMO_ADMIN_FALLBACK is on but DEMO_ADMIN_PASSWORD isn't set, so the fallback admin can't sign in. Set DEMO_ADMIN_PASSWORD, or switch the fallback off.";

    /// <summary>Why the app can't start, or null: in Production the fallback admin must be off, as the database name must be set.</summary>
    public static string? ProductionRefusal(bool isProduction, bool fallbackOn) =>
        isProduction && fallbackOn
            ? "ENABLE_DEMO_ADMIN_FALLBACK is on. In Production the app never has a fallback admin: anyone with its e-mail and password would get the team's rights. Set ENABLE_DEMO_ADMIN_FALLBACK=false and sign in with a team account."
            : null;
}
