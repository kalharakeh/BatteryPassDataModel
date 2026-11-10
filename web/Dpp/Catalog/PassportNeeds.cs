namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>Whether a passport needs a value before it is published.</summary>
public enum Need
{
    /// <summary>Needed before publishing: a missing value blocks.</summary>
    Required,

    /// <summary>Needed only after February 2027: a missing value is worth knowing, nothing more.</summary>
    Later,

    /// <summary>Never needed: a value filled in anyway is kept and published.</summary>
    Optional,
}

/// <summary>
/// The one rule for what a passport needs before it is published. The publication check, the editors' "missing" marks
/// and counts, and the guard against removing a value a published passport needs all read it.
/// </summary>
public static class PassportNeeds
{
    /// <summary>
    /// Values needed before publishing although the official list doesn't mark them "Feb 2027", with the reason. The
    /// catalogue keeps saying what the list says; this is the owner's decision on top of it.
    /// </summary>
    public static IReadOnlyDictionary<int, string> RequiredBeyondTheList { get; } = new Dictionary<int, string>
    {
        [57] = "The Regulation (Annex VI Part A, Annex XIII 1(g)) and the Commission's guidance make the rated capacity mandatory; the owner follows them, not the list's \"no\" (R22).",
    };

    /// <summary>A mandatory value due by February 2027: the list's mark, or one of <see cref="RequiredBeyondTheList"/>.</summary>
    public static bool DueByFeb2027(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return attribute.MandatoryFromFeb2027 || RequiredBeyondTheList.ContainsKey(attribute.Number);
    }

    /// <summary>Whether the passport needs this value before it is published.</summary>
    public static Need NeedOf(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (attribute.Requirement == Requirement.Conditional)
        {
            return Need.Optional;
        }

        return DueByFeb2027(attribute) ? Need.Required : Need.Later;
    }
}
