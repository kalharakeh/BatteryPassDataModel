namespace BatteryPassWeb.Dpp.Catalog;

/// <summary>
/// Two of the EU's code lists (battery format 1.0.0), kept as the EU's addresses with the EU's labels: the 34 critical
/// raw materials (<c>core/1.0.0/vocabs/critical-raw-material.ttl</c>, #42) and the chemical symbols for cadmium and lead
/// (<c>product/batteries/1.0.0/vocabs/battery-chemical-symbol.ttl</c>, E9). Copied from the EU's files on
/// 23 November 2026; a new EU release may add codes.
/// </summary>
public static class EuCodeLists
{
    public const string RawMaterialBase = "https://repository.product-passport.ec.europa.eu/core/vocabs/critical-raw-material#";
    public const string ChemicalSymbolBase = "https://repository.product-passport.ec.europa.eu/product/batteries/vocabs/battery-chemical-symbol#";

    /// <summary>The critical raw materials: the EU's address, then its label, in the EU's order.</summary>
    public static IReadOnlyDictionary<string, string> RawMaterials { get; } = Build(RawMaterialBase,
    [
        ("antimony", "Antimony"), ("arsenic", "Arsenic"), ("bauxiteAluminaAluminium", "Bauxite/alumina/aluminium"), ("baryte", "Baryte"),
        ("beryllium", "Beryllium"), ("bismuth", "Bismuth"), ("boron", "Boron"), ("cobalt", "Cobalt"), ("cokingCoal", "Coking coal"),
        ("copper", "Copper"), ("feldspar", "Feldspar"), ("fluorspar", "Fluorspar"), ("gallium", "Gallium"), ("germanium", "Germanium"),
        ("hafnium", "Hafnium"), ("helium", "Helium"), ("heavyRareEarthElements", "Heavy rare earth elements"),
        ("lightRareEarthElements", "Light rare earth elements"), ("lithium", "Lithium"), ("magnesium", "Magnesium"),
        ("manganese", "Manganese"), ("graphite", "Graphite"), ("nickelBatteryGrade", "Nickel – battery grade"), ("niobium", "Niobium"),
        ("phosphateRock", "Phosphate rock"), ("phosphorus", "Phosphorus"), ("platinumGroupMetals", "Platinum group metals"),
        ("scandium", "Scandium"), ("siliconMetal", "Silicon metal"), ("strontium", "Strontium"), ("tantalum", "Tantalum"),
        ("titaniumMetal", "Titanium metal"), ("tungsten", "Tungsten"), ("vanadium", "Vanadium"),
    ]);

    /// <summary>
    /// Names the app reads as an entry of the list although they aren't its label, where the meaning is plain: the EU
    /// lists graphite without "natural", nickel only as battery grade (a battery's nickel is), aluminium with its ores.
    /// One place; the report of package R1 lists them.
    /// </summary>
    public static IReadOnlyDictionary<string, string> RawMaterialAliases { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Natural graphite"] = "graphite",
        ["Nickel"] = "nickelBatteryGrade",
        ["Nickel - battery grade"] = "nickelBatteryGrade",
        ["Battery grade nickel"] = "nickelBatteryGrade",
        ["Battery-grade nickel"] = "nickelBatteryGrade",
        ["Aluminium"] = "bauxiteAluminaAluminium",
        ["Aluminum"] = "bauxiteAluminaAluminium",
        ["Bauxite"] = "bauxiteAluminaAluminium",
        ["Alumina"] = "bauxiteAluminaAluminium",
        ["Silicon"] = "siliconMetal",
        ["Titanium"] = "titaniumMetal",
        ["Platinum group metals (PGM)"] = "platinumGroupMetals",
        ["PGM"] = "platinumGroupMetals",
        ["Tungsten (wolfram)"] = "tungsten",
    };

    /// <summary>The chemical symbols a model with cadmium or lead above the limit shows (O13): cadmium, lead or both.</summary>
    public static IReadOnlyDictionary<string, string> ChemicalSymbols { get; } = Build(ChemicalSymbolBase,
    [
        ("cadmiumSymbol", "Cadmium (Cd)"), ("leadSymbol", "Lead (Pb)"),
    ]);

    private static readonly Dictionary<string, string> SymbolAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cadmium"] = "cadmiumSymbol",
        ["Cd"] = "cadmiumSymbol",
        ["Cadmium symbol"] = "cadmiumSymbol",
        ["Lead"] = "leadSymbol",
        ["Pb"] = "leadSymbol",
        ["Lead symbol"] = "leadSymbol",
    };

    /// <summary>The address of a critical raw material typed as its address, its code, its label or a plain other name; null when none.</summary>
    public static string? FindRawMaterial(string? typed) => Find(typed, RawMaterialBase, RawMaterials, RawMaterialAliases);

    /// <summary>The address of a chemical symbol typed as its address, its code, its label, "Cadmium" or "Lead"; null when none.</summary>
    public static string? FindChemicalSymbol(string? typed) => Find(typed, ChemicalSymbolBase, ChemicalSymbols, SymbolAliases);


    private static string? Find(string? typed, string prefix, IReadOnlyDictionary<string, string> list, IReadOnlyDictionary<string, string> aliases)
    {
        var text = typed?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (list.ContainsKey(text))
        {
            return text;
        }

        var code = text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? text[prefix.Length..] : text;
        return list.Keys.FirstOrDefault(address => string.Equals(address[prefix.Length..], code, StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(pair => string.Equals(pair.Value, text, StringComparison.OrdinalIgnoreCase)).Key
            ?? (aliases.TryGetValue(text, out var alias) ? prefix + alias : null);
    }

    private static Dictionary<string, string> Build(string prefix, (string Code, string Label)[] entries)
    {
        var list = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, label) in entries)
        {
            list[prefix + code] = label;
        }

        return list;
    }
}
