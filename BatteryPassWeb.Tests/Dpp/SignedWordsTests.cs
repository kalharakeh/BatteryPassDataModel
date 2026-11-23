using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Catalog;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Package R1, round two (review M1, L2, L7): a published passport shows exactly what was signed. On the pages, an entry of
/// #42 or E9 that is an EU address reads as its label, and any other text exactly as stored, with no other name for it.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class SignedWordsTests(MongoFixture mongo)
{
    private const string Raw = EuCodeLists.RawMaterialBase;

    private static AttributeForm Materials => AttributeFormsTests.Forms.For(AttributeCatalog.ByNumber(42));

    private static JsonArray List(params string[] entries) => new(entries.Select(entry => (JsonNode)JsonValue.Create(entry)).ToArray());

    [Fact]
    public void AnAddress_ShouldReadAsItsLabel_AndAnyOtherNameExactlyAsStored()
    {
        var lines = ValueWords.Lines(Materials, List(Raw + "graphite", "Natural graphite", "Nickel", "Aluminium"));

        Assert.Equal(["Graphite", "Natural graphite", "Nickel", "Aluminium"], lines.Select(line => line.Text));
    }

    [Fact]
    public async Task APassportPublishedAsV38PublishedIt_ShouldShowItsMaterialsExactlyAsSigned()
    {
        var rig = await PublicationTesting.PublishedRigAsync(mongo); // the seed's #42: names, as v3.8 kept them

        var page = (await PassportPagesTests.Pages(rig).DetailAsync(Reader.Anonymous, PassportPagesTests.Identifier(rig)))!;

        var materials = page.Sections.SelectMany(section => section.Values).Single(value => value.Attribute.Number == 42);
        Assert.Equal(["Cobalt", "Lithium", "Natural graphite", "Nickel"], materials.Lines.Select(line => line.Text));
    }
}
