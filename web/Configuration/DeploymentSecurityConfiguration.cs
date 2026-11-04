namespace BatteryPassWeb.Configuration;

public static class DeploymentSecurityConfiguration
{
    public static bool ParseRequireHttpsRedirection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.Trim();
        return !normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("0", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Why the app can't start, or null. Without MONGODB_DB the app falls back to battery_pass_demo, the live app's
    /// database (design, "Where it runs"), so in Production the name must be given; Development keeps the fallback.
    /// </summary>
    public static string? MissingDatabaseName(bool isProduction, string? mongoDb) =>
        isProduction && string.IsNullOrWhiteSpace(mongoDb)
            ? "MONGODB_DB isn't set. In Production the app needs the database's name, so it never falls back to battery_pass_demo, the live app's database. Set MONGODB_DB to the database this app should use."
            : null;
}
