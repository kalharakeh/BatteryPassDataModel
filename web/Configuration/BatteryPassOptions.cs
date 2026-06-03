namespace BatteryPassWeb.Configuration;

public sealed class BatteryPassOptions
{
    public string MongoDbUri { get; set; } = string.Empty;
    public string MongoDbName { get; set; } = "battery_pass_demo";
    public string SessionSecret { get; set; } = string.Empty;
    public string DemoAdminEmail { get; set; } = "admin@example.test";
    public string DemoAdminPassword { get; set; } = "Password123!";
    public string ExternalApiEncryptionKey { get; set; } = string.Empty;
    public string IdGenerationSecret { get; set; } = string.Empty;
    public string EmailSmtpHost { get; set; } = string.Empty;
    public int EmailSmtpPort { get; set; } = 587;
    public string EmailSmtpUsername { get; set; } = string.Empty;
    public string EmailSmtpPassword { get; set; } = string.Empty;
    public string EmailFromEmail { get; set; } = string.Empty;
    public string EmailFromName { get; set; } = "Battery Pass";
    public string AppBaseUrl { get; set; } = string.Empty;
}
