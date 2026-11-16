namespace BatteryPassWeb.Services;

/// <summary>
/// Finding W6, the one rule for an e-mail address that is typed or sent: one address, at most 254 characters, no control
/// characters or line breaks, and a plain shape (something, one @, something). The address is the name in the audit log and
/// goes into mails, so anything else is refused before it is stored, logged or mailed.
/// <see cref="IsClean"/> is the part that also guards a look-up, which must never refuse an account that exists;
/// <see cref="Problem"/> is the whole rule, for an address that is set or changed.
/// </summary>
public static class EmailAddressRule
{
    public const int MaxLength = 254;

    /// <summary>What a form says about an address it refuses.</summary>
    public const string Words = "Type one e-mail address, such as name@example.com.";

    private static readonly char[] NotInAnAddress = ['<', '>', '(', ')', '[', ']', '\\', ',', ';', ':', '"'];

    /// <summary>Whether the text, as typed (before any trimming), is short enough and holds no control character or line break.</summary>
    public static bool IsClean(string? typed)
    {
        if (typed is null)
        {
            return true;
        }

        if (typed.Trim().Length > MaxLength)
        {
            return false;
        }

        foreach (var character in typed)
        {
            if (char.IsControl(character) || character is (char)0x2028 or (char)0x2029)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Why the address can't be set or changed to this, or null. An empty address is for the form's own "required".</summary>
    public static string? Problem(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        if (!IsClean(typed))
        {
            return Words;
        }

        var address = typed.Trim();
        var at = address.IndexOf('@');
        if (at <= 0
            || at != address.LastIndexOf('@')
            || at == address.Length - 1
            || address.Any(char.IsWhiteSpace)
            || address.IndexOfAny(NotInAnAddress) >= 0)
        {
            return Words;
        }

        return null;
    }
}
