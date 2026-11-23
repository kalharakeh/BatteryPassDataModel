namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>What a value added beyond the V3 file and the v2.0.1 schema is, which decides its form and its check.</summary>
public enum ExtensionKind
{
    /// <summary>A whole number of minutes, not below zero (E1–E4, decision 28).</summary>
    Minutes,

    /// <summary>A short text, such as a city or a batch label.</summary>
    Text,

    /// <summary>A country, kept as its ISO 3166 three-letter code (<see cref="Countries"/>).</summary>
    Country,

    /// <summary>A text in the passport's language, kept as a language map (<see cref="PassportLanguage"/>).</summary>
    LanguageText,

    /// <summary>One or more codes from one of the EU's lists, kept as the EU's addresses (<see cref="EuCodeLists"/>).</summary>
    Codes,
}

/// <summary>The kind of each extension, by its property; E1–E4 are minutes.</summary>
public static class ExtensionKinds
{
    private static readonly Dictionary<string, ExtensionKind> Kinds = Build();

    private static Dictionary<string, ExtensionKind> Build()
    {
        var kinds = new Dictionary<string, ExtensionKind>(StringComparer.Ordinal)
        {
            [AttributeCatalog.ManufacturingCity] = ExtensionKind.Text,
            [AttributeCatalog.ManufacturingRegion] = ExtensionKind.Text,
            [AttributeCatalog.ManufacturingCountry] = ExtensionKind.Country,
            [AttributeCatalog.BatchNumber] = ExtensionKind.Text,
            [AttributeCatalog.NotInUseTemperatureRangeTest] = ExtensionKind.LanguageText,
            [AttributeCatalog.ChemicalSymbol] = ExtensionKind.Codes,
        };
        foreach (var (property, _) in AttributeCatalog.EndOfLifeTexts)
        {
            kinds[property] = ExtensionKind.LanguageText;
        }

        return kinds;
    }

    /// <summary>The kind of an extension; null for an attribute of the file, whose form comes from the schema.</summary>
    public static ExtensionKind? Of(AttributeDefinition attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        return attribute.IsExtension ? Kinds.GetValueOrDefault(attribute.Property, ExtensionKind.Minutes) : null;
    }
}
