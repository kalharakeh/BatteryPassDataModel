using System.Text.RegularExpressions;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Store;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Dpp.Identifiers;

/// <summary>
/// The battery ID chosen by the product owner (O-1 and 6d, 5 Nov 2026; the dash, 6d-5, 9 Nov 2026): an ISO/IEC 15459
/// unique item ID under issuing agency code <c>UN</c> (Dun &amp; Bradstreet), the company's DUNS, one dash and the
/// battery's serial number (#11) exactly as written (capital letters, digits and single dashes), as a web address per
/// ISO/IEC 18975 with the MH10.8.2 data identifier <c>25S</c>: serial <c>VBC-92921</c> gives
/// <c>https://www.sib-scania-eubp.eu/25S/UN778474481-VBC-92921</c>. The dash is unambiguous: <c>UN778474481-</c> is always
/// the first 12 characters. A battery gets it when it is created; a new passport of the same battery (a successor) adds
/// <c>-R1</c>, <c>-R2</c>, …, the next free one. A demo battery's is marked <c>-TEST-</c> after the DUNS. A version's
/// passport ID (#5) is <c>…/dpp/UN778474481-VBC-92921/v1</c>. IDs made before 6d-5 (<c>UN778474481VPC12221</c>) stay as
/// they are and are still read; their successors continue them (<c>…R1</c>).
/// </summary>
public sealed class DunsIdentifierScheme(IOptions<BatteryPassOptions> options, DppStore store) : IIdentifierScheme
{
    /// <summary>ISO/IEC 15459: an item ID has at most 50 characters, agency code included.</summary>
    public const int MaxItemLength = 50;

    /// <summary>
    /// Room kept for a successor's <c>-R99</c>, so a battery that gets a first ID can always get its next ones: a real
    /// serial number has at most 50 − 12 (<c>UN778474481-</c>) − 4 = 34 characters, a demo one 29 (6d-5).
    /// </summary>
    public const int SuccessorRoom = 4;

    public const int MaxSuccessors = 99;

    /// <summary>The test scheme's only refusal: a serial number with no letters or digits.</summary>
    public const string NoLettersOrDigits = "the serial number has no letters or digits, so it can't make a battery ID";

    public const string StartsWithTest = "a real battery's serial number can't start with TEST, which marks demo batteries' IDs";

    public const string TooManySuccessors = "this battery already has 99 new passports, so it can't get another battery ID";

    public static string TooLong(int characters) => $"the serial number is too long to make a battery ID: at most {characters} characters, dashes included";

    public static string Taken(string item, string serial) =>
        $"the battery ID {item} is already used by battery {serial}. Serial numbers that differ only in their dashes can't both have one";

    /// <summary>The fixed start of every battery ID: agency, company and one dash (<c>UN778474481-</c>).</summary>
    private string Prefix => $"{options.Value.IdAgency}{options.Value.IdCompany}-";

    public async Task<NewIdentifier> NewPackIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);

        // A new passport continues its battery's first ID as it is (6d-5): an old-form one too, whatever its serial number.
        if (pack.SuccessorOf is not null && await FirstIdentifierAsync(pack, cancellationToken) is { } first)
        {
            return await NextSuccessorAsync(first, IdentifierForms.IsDashed(IdentifierForms.Short(first)) ? "-" : string.Empty, cancellationToken);
        }

        var made = ItemFor(SerialNumber(pack), pack.IsDemo);
        if (made.Identifier is not { } address)
        {
            return made;
        }

        if (pack.SuccessorOf is not null)
        {
            return await NextSuccessorAsync(address, "-", cancellationToken); // its first battery has no web-form ID (a test ID)
        }

        // Serial numbers that differ only in their dashes can't both have an ID (6d-5), nor an ID made before 6d-5.
        return await store.FindPackByItemIgnoringDashesAsync(IdentifierForms.Short(address), cancellationToken) is { } other && other.Id != pack.Id
            ? NewIdentifier.Refused(Taken(IdentifierForms.Short(Identifier(other) ?? address), SerialNumber(other)))
            : new NewIdentifier(address, null);
    }

    public NewIdentifier ItemFor(string serialNumber, bool demo)
    {
        var serial = serialNumber ?? string.Empty; // as written: never upper-cased or cleaned (6d-5)
        if (!IdentifierForms.IsSerial(serial))
        {
            return NewIdentifier.Refused(IdentifierForms.SerialRule);
        }

        if (!demo && serial.StartsWith(IdentifierForms.TestMarker, StringComparison.Ordinal))
        {
            return NewIdentifier.Refused(StartsWithTest);
        }

        var head = demo ? $"{Prefix}{IdentifierForms.TestMarker}-" : Prefix;
        return head.Length + serial.Length > MaxItemLength - SuccessorRoom
            ? NewIdentifier.Refused(TooLong(MaxItemLength - SuccessorRoom - head.Length))
            : new NewIdentifier(IdentifierForms.ItemAddress(options.Value.IdBaseUrl, head + serial), null);
    }

    public string NewPassportIdentifier(string packIdentifier, int version) =>
        IdentifierForms.PassportAddress(options.Value.IdBaseUrl, IdentifierForms.Short(packIdentifier), version);

    public bool IsTestIdentifier(string identifier) => IdentifierForms.IsTest(identifier);

    private static string SerialNumber(BatteryPack pack) => Text(pack, "batterySerialNumber") ?? string.Empty;

    private static string? Identifier(BatteryPack pack) => Text(pack, "uniqueProductIdentifier");

    private static string? Text(BatteryPack pack, string key) =>
        pack.Values.GetValueOrDefault(key)?.Value is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The web-form battery ID of the first battery in <paramref name="pack"/>'s chain of passports; null when it has none.</summary>
    private async Task<string?> FirstIdentifierAsync(BatteryPack pack, CancellationToken cancellationToken)
    {
        var current = pack;
        for (var step = 0; step <= MaxSuccessors && current.SuccessorOf is { } previous; step++)
        {
            if (await store.GetAsync<BatteryPack>(previous, cancellationToken) is not { } before)
            {
                return null;
            }

            current = before;
        }

        return current.SuccessorOf is null && Identifier(current) is { } first && IdentifierForms.Short(first) != first ? first : null;
    }

    /// <summary>The first ID with a free R-number after <paramref name="first"/>: <c>…-R1</c>, <c>…-R2</c>, … (<c>…R1</c> before 6d-5).</summary>
    private async Task<NewIdentifier> NextSuccessorAsync(string first, string dash, CancellationToken cancellationToken)
    {
        var used = await store.FindPackIdentifiersWithSuffixesAsync(first, cancellationToken);
        for (var number = 1; number <= MaxSuccessors; number++)
        {
            var next = $"{first}{dash}R{number}";
            if (!used.Contains(next))
            {
                return new NewIdentifier(next, null);
            }
        }

        return NewIdentifier.Refused(TooManySuccessors);
    }
}

/// <summary>The identifiers' forms, shared by every scheme and by the pages' addresses.</summary>
public static partial class IdentifierForms
{
    /// <summary>The MH10.8.2 data identifier for an issuing agency's unique item ID (agency + company + serial).</summary>
    public const string DataIdentifier = "25S";

    public const string TestMarker = "TEST";

    /// <summary>
    /// What a serial number may hold to make a battery ID (the owner, 6d-5): <c>^[A-Z0-9]+(-[A-Z0-9]+)*$</c>. Anything else
    /// is refused at release, never upper-cased or cleaned silently.
    /// </summary>
    public const string SerialRule =
        "a serial number may hold only capital letters A–Z, digits 0–9 and single dashes between them, such as VBC-92921: no lower case, spaces or other characters, and no dash at the start, at the end or next to another";

    /// <summary>
    /// The ID settings that aren't set or aren't valid (6d-2b): DPP_ID_AGENCY (two or three capital letters),
    /// DPP_ID_COMPANY (nine digits) and DPP_ID_BASE_URL (an https address). Empty when the scheme is complete.
    /// </summary>
    public static IReadOnlyList<string> MissingSettings(BatteryPassOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var missing = new List<string>();
        if (!AgencyCode().IsMatch(options.IdAgency ?? string.Empty))
        {
            missing.Add("DPP_ID_AGENCY");
        }

        if (!CompanyCode().IsMatch(options.IdCompany ?? string.Empty))
        {
            missing.Add("DPP_ID_COMPANY");
        }

        if (!Uri.TryCreate(options.IdBaseUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
        {
            missing.Add("DPP_ID_BASE_URL");
        }

        return missing;
    }

    /// <summary>Why real battery IDs can't be released or used while the settings are missing (6d-2b).</summary>
    public static string SettingsMissing(IReadOnlyList<string> missing) =>
        $"the battery ID settings aren't set, so batteries would get test IDs instead of their real battery IDs. Not set or not valid: {string.Join(", ", missing)}. Whoever runs the app sets them and restarts it";

    /// <summary>True when the settings give a full scheme; otherwise the app issues test IDs.</summary>
    public static bool IsConfigured(BatteryPassOptions options) =>
        AgencyCode().IsMatch(options.IdAgency) && CompanyCode().IsMatch(options.IdCompany) && Uri.TryCreate(options.IdBaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;

    /// <summary><c>https://…/25S/UN778474481-VBC-92921</c>: the battery ID (#6) as a web address.</summary>
    public static string ItemAddress(string baseUrl, string item) => $"{baseUrl.Trim().TrimEnd('/')}/{DataIdentifier}/{item}";

    /// <summary><c>https://…/dpp/UN778474481-VBC-92921/v1</c>: a version's passport ID (#5).</summary>
    public static string PassportAddress(string baseUrl, string item, int version) => $"{baseUrl.Trim().TrimEnd('/')}/dpp/{item}/v{version}";

    /// <summary>The item ID inside a web-form battery ID ("UN778474481-VBC-92921"); anything else as it is.</summary>
    public static string Short(string identifier)
    {
        var match = ItemInAddress().Match(identifier ?? string.Empty);
        return match.Success ? match.Groups["item"].Value : identifier ?? string.Empty;
    }

    /// <summary>
    /// What a page shows of an ID (the owner, 6d): the short item for a battery ID ("UN778474481-VBC-92921"), the item and
    /// version for a passport ID ("UN778474481-VBC-92921/v2"); anything else as it is. The full address is the link.
    /// </summary>
    public static string Display(string id) => IsWebAddress(id) ? PagePath(id) : id;

    /// <summary>True for a battery or passport ID in the web form (shown short, linked to its full address).</summary>
    public static bool IsWebAddress(string? id) => id is not null && (ItemInAddress().IsMatch(id) || VersionInAddress().IsMatch(id));

    /// <summary>
    /// The path of a passport page for an ID: "UN…" for a web-form battery ID, "UN…/v2" for a web-form passport ID, else
    /// the ID escaped as before.
    /// </summary>
    public static string PagePath(string id)
    {
        if (ItemInAddress().Match(id) is { Success: true } item)
        {
            return item.Groups["item"].Value;
        }

        if (VersionInAddress().Match(id) is { Success: true } version)
        {
            return $"{version.Groups["item"].Value}/v{version.Groups["version"].Value}";
        }

        return Uri.EscapeDataString(id).Replace("%3A", ":", StringComparison.Ordinal);
    }

    /// <summary>
    /// True for a short item ID a reader may type or scan: "UN778474481-VBC-92921", or one made before 6d-5
    /// ("UN778474481VPC12221"). At most 50 characters (ISO/IEC 15459).
    /// </summary>
    public static bool IsItem(string text) => text is { Length: <= DunsIdentifierScheme.MaxItemLength } && ItemId().IsMatch(text);

    /// <summary>
    /// True when the whole text is an ID of this app: a short item ID ("UN778474481-VPC-0001"), a battery or passport ID in
    /// the web form, or an old test URN. Unlike <see cref="IsTest"/>, which also matches the end of an address, nothing
    /// else may come before or after it (S01).
    /// </summary>
    public static bool IsAppId(string? text) =>
        text is not null && (IsItem(text) || IsWebAddress(text) || OldTestUrn().IsMatch(text));

    /// <summary>True for a serial number that may make a battery ID: capital letters, digits and single dashes (<see cref="SerialRule"/>).</summary>
    public static bool IsSerial(string? serialNumber) => serialNumber is not null && Serial().IsMatch(serialNumber);

    /// <summary>True for an item ID in the 6d-5 form, with a dash after the company; false for one made before.</summary>
    public static bool IsDashed(string item) => Dashed().IsMatch(item ?? string.Empty);

    /// <summary>
    /// A serial number's key, only its letters A–Z and digits 0–9, upper-cased ("VBC-92921" → "VBC92921"): the released
    /// IDs' key, so that serial numbers which differ only in their dashes are one (6d-5), and what searches compare.
    /// </summary>
    public static string Clean(string? serialNumber) =>
        new((serialNumber ?? string.Empty).ToUpperInvariant().Where(letter => letter is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());

    /// <summary>Test IDs: the old TEST- URNs, and items marked TEST after the company (demo batteries and their passports).</summary>
    public static bool IsTest(string? identifier) =>
        identifier?.StartsWith(TestIdentifierScheme.Prefix, StringComparison.Ordinal) == true
        || TestItem().IsMatch(identifier ?? string.Empty);

    /// <summary>The economic operator's (#7) and the manufacturer's (#12) ID in the owner's form (6d): ISO/IEC 6523 with DUNS.</summary>
    public const string ActorIdPrefix = "iso6523-actorid-upis::0060:";

    /// <summary>The hint under #7 and #12 on Sites and operators.</summary>
    public const string ActorIdHint = "The DUNS number in the ISO/IEC 6523 form: iso6523-actorid-upis::0060: and its 9 digits.";

    /// <summary>
    /// A gentle note when #7 or #12 isn't in that form; null otherwise. Only a note: the value is saved as it is, since
    /// another form may be right for another company (the owner).
    /// </summary>
    public static string? ActorIdNote(int number, string? text) =>
        number is 7 or 12 && !string.IsNullOrWhiteSpace(text) && !ActorId().IsMatch(text.Trim())
            ? $"Not in the usual form {ActorIdPrefix}<DUNS>, for example {ActorIdPrefix}778474481. It is kept as it is."
            : null;

    [GeneratedRegex("^iso6523-actorid-upis::0060:[0-9]{9}$")]
    private static partial Regex ActorId();

    [GeneratedRegex("^[A-Z]{2,3}$")]
    private static partial Regex AgencyCode();

    [GeneratedRegex("^[0-9]{9}$")]
    private static partial Regex CompanyCode();

    // ISO/IEC 15459: agency + company, a dash (6d-5; none before) and the serial: capital letters and digits in groups
    // joined by single dashes. At most 50 characters (checked in IsItem, and by the look-aheads in an address).
    private const string Item = "[A-Z]{2,3}[0-9]{9}-?[A-Z0-9]+(?:-[A-Z0-9]+)*";

    [GeneratedRegex("^" + Item + "$")]
    private static partial Regex ItemId();

    [GeneratedRegex("^https://[^/]+/25S/(?<item>(?=[A-Z0-9-]{1,50}$)" + Item + ")$")]
    private static partial Regex ItemInAddress();

    [GeneratedRegex("^https://[^/]+/dpp/(?<item>(?=[A-Z0-9-]{1,50}/)" + Item + ")/v(?<version>[0-9]+)$")]
    private static partial Regex VersionInAddress();

    [GeneratedRegex("^urn:x-battery-pass:TEST-[A-Za-z0-9-]+$")]
    private static partial Regex OldTestUrn();

    // A demo ID's mark, right after the company: -TEST- (6d-5) or TEST (before).
    [GeneratedRegex("(?:^|/)[A-Z]{2,3}[0-9]{9}-?TEST[A-Z0-9-]*(?:/v[0-9]+)?$")]
    private static partial Regex TestItem();

    [GeneratedRegex("^[A-Z0-9]+(?:-[A-Z0-9]+)*$")]
    private static partial Regex Serial();

    [GeneratedRegex("^[A-Z]{2,3}[0-9]{9}-")]
    private static partial Regex Dashed();
}
