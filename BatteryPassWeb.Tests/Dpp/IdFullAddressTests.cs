namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// Decision 68 (the owner, 12 Nov 2026): the public and the detailed passport page, and the team's and the cluster admin's
/// battery pages, show the passport ID and the battery ID as their whole web address, website included, as text that
/// selects and copies whole. The copy button and the link stay. Lists keep the short form. Changes step 6d's choice.
/// </summary>
public sealed class IdFullAddressTests
{
    private static string View(params string[] path) => File.ReadAllText(DppTestFiles.RepoFile(["web", "Views", .. path]));

    private const string Full = "new ViewDataDictionary(ViewData) { [\"IdFull\"] = true }";

    [Fact]
    public void TheIdPartial_ShouldShowTheWholeAddressAsTheLinkText_WhenAskedForTheFullForm()
    {
        var partial = View("Shared", "_IdValue.cshtml");

        Assert.Contains("ViewData[\"IdFull\"] is true", partial, StringComparison.Ordinal);
        Assert.Contains("<a class=\"bp-id-value is-full\" href=\"@Links.PassportUrl(Model)\">@foreach (var part in Parts(Model)) {<span class=\"bp-id-part\">@part</span><wbr />}</a>@await Html.PartialAsync(\"_CopyIdButton\", Model)", partial, StringComparison.Ordinal);
        Assert.Contains("yield return address[..(hostEnd + 1)];", partial, StringComparison.Ordinal); // "https://host/" stays whole
        Assert.Contains("@IdentifierForms.Display(Model)", partial, StringComparison.Ordinal); // the short form stays for lists
    }

    [Fact]
    public void ThePassportPages_ShouldShowBothIdsAsTheirWholeAddress()
    {
        var hero = View("Passport", "_PassportHero.cshtml");

        Assert.Contains($"@await Html.PartialAsync(\"_IdValue\", Model.Version.DigitalProductPassportId, {Full})", hero, StringComparison.Ordinal);
        Assert.Contains($"@await Html.PartialAsync(\"_IdValue\", Model.Identifier, {Full})", hero, StringComparison.Ordinal);
        Assert.Contains("<span class=\"bp-id-value\">@Model.Identifier</span>", hero, StringComparison.Ordinal); // an ID that isn't a web address shows as it is
    }

    [Theory]
    [InlineData("Batteries")]
    [InlineData("ClusterAdmin")]
    public void TheBatteryPages_ShouldShowTheBatteryIdAsItsWholeAddress(string folder) =>
        Assert.Contains($"<dd>@await Html.PartialAsync(\"_IdValue\", Model.Identifier, {Full})</dd>", View(folder, "Battery.cshtml"), StringComparison.Ordinal);

    [Fact]
    public void TheVersionsList_ShouldShowEachPassportIdAsItsWholeAddress()
    {
        var card = View("Batteries", "_VersionsCard.cshtml");

        Assert.Contains("title=\"@row.Version.DigitalProductPassportId\">@row.Version.DigitalProductPassportId</a>", card, StringComparison.Ordinal);
        Assert.DoesNotContain("@BatteryPages.ShortId(row.Version.DigitalProductPassportId)", card, StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_ShouldKeepTheShortForm()
    {
        Assert.Contains("@await Html.PartialAsync(\"_IdValue\", item.Identifier)</td>", View("BatteryIds", "_IdRows.cshtml"), StringComparison.Ordinal);
        Assert.DoesNotContain("IdFull", View("Shared", "_BatteryList.cshtml"), StringComparison.Ordinal);
    }

    [Fact]
    public void AWholeAddress_ShouldWrapAnywhere_InsteadOfBeingCutOff()
    {
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "site.css"));
        var m1 = css[css.LastIndexOf("/* M1 */", StringComparison.Ordinal)..];

        Assert.Contains(".bp-report-identity-panel--identifiers .bp-id-value.is-full", m1, StringComparison.Ordinal);
        Assert.Contains("white-space: normal;", m1, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", m1, StringComparison.Ordinal);
        Assert.Contains("text-overflow: clip;", m1, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePanel_ShouldPutEachLabelAboveItsAddress_AndBreakAfterASlashFirst()
    {
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "site.css"));
        var m1 = css[css.IndexOf("/* M1 round two F", StringComparison.Ordinal)..];

        Assert.Contains(".bp-report-identity-panel--identifiers .bp-passport-id:has(.bp-id-copy-row.is-full) {\n  grid-template-columns: minmax(0, 1fr);", m1, StringComparison.Ordinal);
        Assert.Contains(".bp-id-value.is-full .bp-id-part {\n  display: inline-block;\n  max-width: 100%;\n  overflow-wrap: anywhere;", m1, StringComparison.Ordinal);
        Assert.Contains(".bp-id-copy-row.is-full {\n  align-items: start;", m1, StringComparison.Ordinal);
    }

    [Fact]
    public void AWholeAddress_ShouldStayOneTextRun_SoSelectingAndCopyingItGivesItWhole()
    {
        var css = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "site.css"));
        var m1 = css[css.IndexOf("/* M1 round two G", StringComparison.Ordinal)..];
        var header = File.ReadAllText(DppTestFiles.RepoFile("web", "wwwroot", "css", "bp-product-data.css"));

        // The battery pages' header makes its links flex boxes, which would put a line break between the address's parts
        // in a copied selection; a full address's link stays an inline box, and this rule is the more specific one.
        Assert.Contains(".bp-bh-facts dd a {", header, StringComparison.Ordinal);
        Assert.Contains("a.bp-id-value.is-full,\n.bp-bh-facts dd a.bp-id-value.is-full {\n  display: inline;", m1, StringComparison.Ordinal);
    }
}
