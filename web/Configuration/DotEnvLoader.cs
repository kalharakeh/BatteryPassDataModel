namespace BatteryPassWeb.Configuration;

public static class DotEnvLoader
{
    public static void Load(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
            {
                value = value[1..^1];
            }

            var existingValue = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(existingValue) || IsPlaceholderValue(existingValue))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static bool IsPlaceholderValue(string value)
    {
        var normalized = value.Trim();
        return normalized.StartsWith("replace-with", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("your MongoDB connection string", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("your mongodb connection string", StringComparison.OrdinalIgnoreCase);
    }
}
