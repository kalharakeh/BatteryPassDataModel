namespace BatteryPassWeb.Configuration;

/// <summary>
/// TLS terminates at the Elastic Beanstalk load balancer, so Kestrel only ever
/// listens on plain HTTP. That means ASP.NET Core cannot infer the public HTTPS
/// port from its own bindings, and <c>UseHttpsRedirection</c> quietly does nothing
/// unless it is told explicitly.
///
/// This is the port clients connect to, not the port the application listens on.
/// </summary>
public static class HttpsRedirectionDefaults
{
    public const int PublicHttpsPort = 443;
}
