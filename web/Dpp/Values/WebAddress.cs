namespace BatteryPassWeb.Dpp.Values;

/// <summary>
/// The one check for a web address that a page may turn into a link: an absolute http or https address. Anything else
/// (<c>javascript:</c>, <c>data:</c>, a relative path, plain words) is shown as text, so a typed or stored value can't
/// run a script when someone clicks it.
/// </summary>
public static class WebAddress
{
    /// <summary>True for an absolute http or https address.</summary>
    public static bool IsWebAddress(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var address)
        && address.Scheme is "http" or "https";

    /// <summary>The address as written (trimmed) when it is an http or https address; null otherwise. Use it for every <c>href</c> that holds a typed or stored value.</summary>
    public static string? Safe(string? text) => IsWebAddress(text) ? text!.Trim() : null;
}
