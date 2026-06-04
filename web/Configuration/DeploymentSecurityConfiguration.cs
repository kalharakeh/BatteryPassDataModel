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
}
