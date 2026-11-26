using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Loading;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Tests.Dpp;
using static BatteryPassWeb.Tests.Loading.LoadTesting;

namespace BatteryPassWeb.Tests.Loading;

/// <summary>
/// Package X1, point 2: every problem is named with its sheet, its row's name and its column, and a workbook with a
/// problem saves nothing. The problems are what the forms would refuse (the forms' reading of a value and the save
/// pipeline's checks), and what only a workbook can get wrong: a file that wasn't sent, a model no sheet has.
/// </summary>
[Collection(MongoCollection.Name)]
public sealed class ProductDataLoadProblemsTests(MongoFixture mongo)
{
    private async Task<string[]> ProblemsAsync(FilledWorkbook workbook, IReadOnlyList<SentFile>? files = null)
    {
        var rig = await EmptyAsync(mongo);
        var check = await rig.CheckAsync(workbook, files);
        Assert.False(check.CanLoad);
        Assert.Equal(Nothing, await rig.CountsAsync());
        return check.Texts();
    }

    [Fact]
    public async Task TheManagersFiveFaults_ShouldEachBeNamedWithSheetRowAndColumn_AndNothingSaved()
    {
        // The manager's check 4: a text in a number cell, a missing file, a small-letter yes, a comma decimal and an
        // unknown model on the documents sheet.
        var workbook = Core()
            .Set(ModelSheet, "model|ratedCapacity|amperehourMiliamperehourValue", "Core800", "about 220")
            .Set(ModelSheet, "model|MadeForCyclingUse", "Core800s", "yes")
            .Set(ModelSheet, "model|batteryMass|gramKilogramValue", "Core700", "4,7")
            .DocumentCell("Core700", "model", "safetyMeasures", LoadWorkbook.ModelHeader, "Core900")
            .Document("Core700", "model", "safetyMeasures", "safety-core900.pdf", "Safety measures", "en-GB");
        var files = CoreFiles().Where(file => file.FileName != "spare-parts.pdf").ToArray();
        var rig = await EmptyAsync(mongo);

        var result = await rig.LoadAsync(workbook, files);

        Assert.False(result.Loaded);
        SameLines(
            [
                "Documents and links, Safety measures (Core900), Model: the sheet Models has no model called Core900. Write a model's name as it is on that sheet",
                "Models, Battery mass, Core700: 4,7 is not a number; type the number only, with a point for decimals",
                "Models, Is this battery made for cycling use?, Core800s: yes isn't one of the two answers. Choose Yes or No from the cell's list",
                "Models, Rated capacity, Core800: about 220 is not a number; type the number only",
            ],
            result.Check.Texts().Order(StringComparer.Ordinal));
        Assert.Equal(Nothing, await rig.CountsAsync());
        Assert.Contains(result.Check.Pending, pending => pending.Missing == "the file spare-parts.pdf wasn't sent");
    }

    [Fact]
    public async Task WhatTheFormsWouldRefuse_ShouldBeRefusedInTheirWords_AtTheCellItIsAbout()
    {
        var problems = await ProblemsAsync(Core()
            .SetNumber(ModelSheet, "model|initialRoundTripEnergyEfficiency|percentageValue", "Core800", 120) // over 100 %
            .SetNumber(ModelSheet, "model|ratedCapacity|amperehourMiliamperehourValue", "Core800s", 220.5) // a whole number is asked
            .Set(ModelSheet, "model|batteryChemistry|chemicalCodeValue", "Core700", "Lithium") // not one of the choices
            .SetNumber(ModelSheet, "model|nominalVoltage|temperatureRangeLowerBoundaryValue", "Core700", -20.5)
            .SetNumber(ModelSheet, "model|batteryMass|gramKilogramValue", "Core700", -1)
            .Set(BatchSheet, "batch|webLinkToPublicCarbonFootprintStudy", FirstBatch, "www.sib-scania-eubp.eu/core/study")
            .Set(OperatorSheet, "operator|economicOperatorInformation|name", "Operator 1", "none") // a part the value can't be without
            .SetNumber(BatchSheet, "batch|renewableContentShare|percentageValue", FirstBatch, 101));

        SameLines(
            [
                "Batches, Renewable content share, Batch of Core800: 101 must be at most 100",
                "Batches, Web link to the public carbon footprint study, Batch of Core800: www.sib-scania-eubp.eu/core/study must be a web address starting with https:// or http://",
                "Models, Battery chemistry, Core700: Chemistry must be one of the listed choices",
                "Models, Battery mass, Core700: -1 must be at least 0",
                "Models, Initial round trip energy efficiency, Core800: 120 must be at most 100",
                "Models, Nominal voltage: valid from this temperature, Core700: Test temperature from must be a whole number",
                "Models, Rated capacity, Core800s: 220.5 must be a whole number",
                "Operator, Economic operator: name, Operator 1: Name is missing",
            ],
            problems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AValueThatIsStillTheDemosExample_ShouldBeNamed()
    {
        // The manager's check 1: the example column copied in. The examples the demo made up are named, nothing else.
        var problems = await ProblemsAsync(FilledWorkbook.Empty()
            .ExampleInto(ModelSheet, "Core800")
            .ExampleInto(BatchSheet, FirstBatch)
            .ExampleInto(SiteSheet, "Site 1").Set(SiteSheet, "site|Name", "Site 1", "Plant South")
            .ExampleInto(OperatorSheet, "Operator 1").Set(OperatorSheet, "operator|Name", "Operator 1", "Fleet Services AB")
            .Clear(ModelSheet, "model|Picture", "Core800")
            .Hazard(1, "all", "Lithium hexafluorophosphate")
            .Document("Core800", "model", "separateCollectionSymbol", "https://www.example.com/battery/symbols/separate-collection.svg", "Separate collection symbol", "en-GB"),
            files: []);

        SameLines(
            [
                "Batches, Web link to the public carbon footprint study, Batch of Core800: https://www.example.com/demo/core/carbon-footprint-study is still the demo's example. Type your own value",
                "Documents and links, Separate collection symbol (Core800), File name or web link: https://www.example.com/battery/symbols/separate-collecti... is still the demo's example. Type your own",
                "Operator, Economic operator: e-mail address, Operator 1: fleet@example.com is still the demo's example. Type your own value",
                "Operator, Economic operator: name, Operator 1: Demo Fleet Services AB is still the demo's example. Type your own value",
                "Operator, Economic operator: registered trade name or trademark, Operator 1: Demo Fleet Services is still the demo's example. Type your own value",
                "Operator, Economic operator: web address, Operator 1: https://fleet.example.com is still the demo's example. Type your own value",
                "Site, Manufacturer: e-mail address, Site 1: batteries@example.com is still the demo's example. Type your own value",
                "Site, Manufacturer: name, Site 1: Scania Industrial Batteries (demo) is still the demo's example. Type your own value",
                "Site, Manufacturer: postal address, Site 1: Demo Street 1, 111 11 Stockholm, Sweden is still the demo's example. Type your own value",
                "Site, Manufacturer: web address, Site 1: https://www.example.com is still the demo's example. Type your own value",
                "Site, Manufacturing place, Site 1: Demo plant South, Malmo, Sweden is still the demo's example. Type your own value",
            ],
            problems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task FilesAndLinks_ShouldBeChecked_AsAnUploadAndALinkOnTheFormsAre()
    {
        var files = CoreFiles()
            .Where(file => file.FileName != "part-numbers.pdf")
            .Append(new SentFile("part-numbers.pdf", "PK not a pdf"u8.ToArray())) // its type is found in its content
            .Append(new SentFile("holiday.png", DocumentTesting.Png())) // named nowhere
            .ToArray();
        var problems = await ProblemsAsync(
            Core()
                .Document("Core800", "model", "separateCollectionSymbol", "separate-collection.svg, no link yet", "Separate collection symbol", "en-GB")
                .Document("Core800", "model", "dismantlingInformation", $"{Web}/core/dismantling.pdf", "Dismantling manual", "en-GB")
                .Document("Core800", "model", "safetyMeasures", "safety-measures.pdf", "Safety measures", "Swedish")
                .Document("Core800", "batch", "euDeclarationOfConformity", null, "EU declaration of conformity", "en-GB")
                .Set(ModelSheet, "model|Picture", "Core800", "core-model.pdf"),
            files.Append(new SentFile("core-model.pdf", DocumentTesting.Pdf())).ToArray());

        SameLines(
            [
                "Documents and links, Dismantling information: manuals for removal and disassembly (Core800), File name or web link: this document is a file you send, not a web link: only the people it is for may open it. Write the file's name and send the file with the workbook",
                "Documents and links, Part numbers for components (Core800), File name or web link: part-numbers.pdf: only PDF, PNG and JPEG files are accepted, recognised by their content",
                "Documents and links, Safety measures (Core800), Language: Swedish must be one of the listed choices",
                "Documents and links, Separate collection symbol (Core800), File name or web link: separate-collection.svg, no link yet is not a web link. The passport keeps only the address, so the document must be on a public page that starts with https://",
                "Models, Picture of the model, Core800: core-model.pdf: the picture must be a PNG or JPEG file",
                "the file core-model.png was sent, but the workbook doesn't name it anywhere. Write its name in the cell it belongs to, or leave it out",
                "the file dismantling-manual.pdf was sent, but the workbook doesn't name it anywhere. Write its name in the cell it belongs to, or leave it out",
                "the file holiday.png was sent, but the workbook doesn't name it anywhere. Write its name in the cell it belongs to, or leave it out",
            ],
            problems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task TheSheetsThemselves_ShouldBeChecked_AnUnknownKeyAModelNoSheetHasABatchWithoutItsYear()
    {
        var problems = await ProblemsAsync(Core()
            .Rekey(ModelSheet, "model|cycleLifeReferenceTest", "model|cycleLifeTest")
            .Hazard(2, "Core900", "Cobalt sulphate")
            .Hazard(3, null, "Nickel sulphate")
            .Clear(BatchSheet, "batch|Year", FirstBatch)
            .Set(BatchSheet, "batch|Site", "Batch of Core700", "Plant North")
            .Set(SiteSheet, "site|manufacturingPlace", "Site 2 (only if needed)", "Lulea, Sweden"));

        SameLines(
            [
                "Batches, Manufacturing site, Batch of Core700: there is no manufacturing site called Plant North, on the sheet Site or in the app. Write the site's name as it is on the sheet Site",
                "Batches, Production year, Batch of Core700: give the production year: a batch is one model, made at one site, in one year",
                "Batches, Production year, Batch of Core800: give the production year: a batch is one model, made at one site, in one year",
                "Batches, Production year, Batch of Core800s: give the production year: a batch is one model, made at one site, in one year",
                "Hazardous substances, row 5 (Cobalt sulphate), Model: the sheet Models has no model called Core900. Write a model's name as it is on that sheet, or all",
                "Hazardous substances, row 6 (Nickel sulphate), Model: say which model this substance is in: a model's name, or all",
                "Models, Cycle-life reference test: the app doesn't know this row (its key is model|cycleLifeTest). Use the workbook as it was sent; don't add rows or change column A",
                "Site, Site name, Site 2 (only if needed): this column has values but no name. Give the manufacturing site a name, or empty the column",
            ],
            problems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AFamilyWithoutAName_ShouldBeSaidOnce_AndTheRestStillChecked()
    {
        var problems = await ProblemsAsync(Core()
            .Clear(FamilySheet, "family|Name", "Core")
            .Set(ModelSheet, "model|batteryMass|gramKilogramValue", "Core800", "heavy"));

        SameLines(
            [
                "Family, Family name, Core: give the family's name: the models belong to it",
                "Models, Battery mass, Core800: heavy is not a number; type the number only",
            ],
            problems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AHazardousSubstanceLine_ShouldBeCheckedAsARowOfTheForm_AndNamedByItsOwnRow()
    {
        var problems = await ProblemsAsync(Core()
            .Hazard(2, "Core800s", "Cobalt sulphate", identifierType: "REACH", identifier: null, concentration: "a little", hazards: "H317"));
        var again = await ProblemsAsync(Core()
            .Hazard(2, "Core800s", "Cobalt sulphate", identifierType: "REACH", identifier: null, hazards: null));

        SameLines(["Hazardous substances, row 5 (Cobalt sulphate), Concentration: a little is not a number; type the number only"], problems);
        SameLines(
            [
                "Hazardous substances, row 5 (Cobalt sulphate), Identifier type: Identifier type must be one of the listed choices",
                "Hazardous substances, row 5 (Cobalt sulphate), Identifier: Identifier and Hazard phrases are missing",
            ],
            again.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AFileThatIsNoWorkbook_ShouldBeRefusedWhole()
    {
        var rig = await EmptyAsync(mongo);

        var pdf = await rig.Loader.CheckAsync("Core.xlsx", DocumentTesting.Pdf(), []);
        var wrongName = await rig.Loader.CheckAsync("Core.xlsm", Core().Bytes(), CoreFiles());
        var otherWorkbook = await rig.Loader.CheckAsync("ids.xlsx", BatteryPassWeb.Dpp.Identifiers.XlsxWriter.Write("IDs", ["Serial number"], [["1"]]), []);

        Assert.Equal((XlsxReader.NotAWorkbook, false), (pdf.WorkbookProblem, pdf.CanLoad));
        Assert.Equal(XlsxReader.NotAWorkbook, wrongName.WorkbookProblem);
        Assert.False(otherWorkbook.CanLoad);
        Assert.Contains($"Family: the workbook has no sheet called Family; {LoadWorkbook.NotTheWorkbook}", otherWorkbook.Texts());
        Assert.Equal(Nothing, await rig.CountsAsync());
    }

    [Fact]
    public async Task ANameADemoRecordHolds_ShouldBeAProblemThatSaysSo_AndTheDemoRecordStaysAsItIs()
    {
        var rig = await EmptyAsync(mongo);
        await rig.Store.InsertAsync(new BatteryFamily { Id = "family-demo", Name = "core", IsDemo = true });
        await rig.Store.InsertAsync(new ManufacturingSite { Id = "site-demo", Name = "Plant South", IsDemo = true });
        await rig.Store.InsertAsync(new EconomicOperator { Id = "operator-demo", Name = "Fleet Services AB", IsDemo = true });

        var result = await rig.LoadAsync(Core());

        Assert.False(result.Loaded);
        SameLines(
            [
                "Family, Family name, Core: a demo battery family is already called Core, and real product data can't share a name with demo data. Clear the demo data on the Clusters tab, or choose another name",
                "Operator, Operator name, Operator 1: a demo economic operator is already called Fleet Services AB, and real product data can't share a name with demo data. Clear the demo data on the Clusters tab, or choose another name",
                "Site, Site name, Site 1: a demo manufacturing site is already called Plant South, and real product data can't share a name with demo data. Clear the demo data on the Clusters tab, or choose another name",
            ],
            result.Check.Texts().Order(StringComparer.Ordinal));
        Assert.Equal("families 1, models 0, batches 0, sites 1, operators 1, documents 0, files 0, audit 0", await rig.CountsAsync());
        Assert.Empty((await rig.Store.GetAsync<ManufacturingSite>("site-demo"))!.Values);
    }
}
