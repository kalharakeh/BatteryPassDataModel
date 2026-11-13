using System.Text.RegularExpressions;

namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>
/// A model's serial number format (decision 81, the owner, 13 Nov 2026): letters, digits and dashes as they must appear,
/// and <c>#</c> for a digit, such as <c>VPC8C05s-#####</c>. It is the app's own data, not passport data. A model without
/// one accepts any serial number; test IDs are free of it. Releasing IDs for the model, creating a battery of it and
/// swapping a draft's ID refuse a serial number that doesn't fit. A format is exact about its letters' places, not their
/// case: <c>VPC8C05-#####</c> and <c>VPC8C05s-#####</c> are different formats.
/// </summary>
public static partial class SerialFormats
{
    /// <summary>What stands for one digit in a format.</summary>
    public const char DigitSign = '#';

    /// <summary>The line under the field on the model's page.</summary>
    public const string Hint = "Letters, digits and dashes as they must appear, and # for one digit, for example VPC8C05s-#####. Capital and small letters count as the same.";

    public const string OnlyTheseCharacters = "a format holds only letters, digits, single dashes between them and # for a digit, such as VPC8C05s-#####";

    public const string NeedsADigitSign = "a format needs at least one # for a digit, such as VPC8C05s-#####";

    /// <summary>The format as typed, without the spaces around it; null when nothing was typed (no format).</summary>
    public static string? Read(string? typed) => string.IsNullOrWhiteSpace(typed) ? null : typed.Trim();

    /// <summary>
    /// Why a format can't be saved, or null: only letters, digits, single dashes and #; at least one #; and a serial
    /// number of it must be able to make a battery ID, as <paramref name="identifiers"/> judges it (its length, not
    /// starting with TEST).
    /// </summary>
    public static string? Problem(string format, IIdentifierScheme identifiers)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(identifiers);
        if (!Form().IsMatch(format))
        {
            return OnlyTheseCharacters;
        }

        if (!format.Contains(DigitSign, StringComparison.Ordinal))
        {
            return NeedsADigitSign;
        }

        return identifiers.ItemFor(Example(format), demo: false).Problem is { } problem
            ? $"a serial number of this format can't make a battery ID: {problem}"
            : null;
    }

    /// <summary>
    /// True when <paramref name="serialNumber"/> has its letters, digits and dashes where <paramref name="format"/> has
    /// them, a digit for each #; letters are compared without regard to case, like serial numbers (decision 80). A model
    /// without a format (null or empty) accepts anything.
    /// </summary>
    public static bool Fits(string? format, string? serialNumber)
    {
        if (string.IsNullOrEmpty(format))
        {
            return true;
        }

        if (serialNumber is null || serialNumber.Length != format.Length)
        {
            return false;
        }

        for (var index = 0; index < format.Length; index++)
        {
            var wanted = format[index];
            var given = serialNumber[index];
            if (wanted == DigitSign ? !char.IsAsciiDigit(given) : char.ToUpperInvariant(wanted) != char.ToUpperInvariant(given))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A serial number that fits: every # a 0, the last one a 1 ("VPC8C05s-#####" gives "VPC8C05s-00001").</summary>
    public static string Example(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var last = format.LastIndexOf(DigitSign);
        return new string(format.Select((letter, index) => letter != DigitSign ? letter : index == last ? '1' : '0').ToArray());
    }

    /// <summary>True for the very same format: the same characters, whatever the case of its letters.</summary>
    public static bool Same(string? one, string? other) =>
        !string.IsNullOrEmpty(one) && string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refusal at release, creation and swap: a plain sentence naming the model and its format.</summary>
    public static string Misfit(string serialNumber, string modelName, string format) =>
        $"serial number {serialNumber} doesn't fit the serial number format of model {modelName}, {format}";

    [GeneratedRegex("^[A-Za-z0-9#]+(?:-[A-Za-z0-9#]+)*$")]
    private static partial Regex Form();
}
