using System.Text.Json.Nodes;
using BatteryPassWeb.Dpp.Assembly;
using BatteryPassWeb.Dpp.Signing;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>
/// C3 round two, point 8: the exact text that is hashed and signed, pinned as it is, so the Help's description for
/// outsiders stays true. The form itself must not change: that would break every proof made so far.
/// </summary>
public sealed class CanonicalFormTests
{
    /// <summary>
    /// Every character of the Basic Multilingual Plane (BMP) is checked: the writer escapes it exactly when it is a
    /// control character, a space other than U+0020, a line or paragraph separator, private use, U+FEFF or unassigned in
    /// .NET's Unicode tables, as the Help says.
    /// </summary>
    [Fact]
    public void EveryEscapedCharacter_ShouldBeListed()
    {
        var escaped = new List<(int From, int To)>();
        for (var unit = 0x20; unit <= 0xFFFF; unit++)
        {
            if (unit is >= 0xD800 and <= 0xDFFF || unit == '"' || unit == '\\')
            {
                continue;
            }

            var text = ((char)unit).ToString();
            if (CanonicalJson.Serialize(JsonValue.Create(text)) != "\"" + text + "\"")
            {
                if (escaped.Count > 0 && escaped[^1].To == unit - 1)
                {
                    escaped[^1] = (escaped[^1].From, unit);
                }
                else
                {
                    escaped.Add((unit, unit));
                }
            }
        }

        bool ByCategory(int unit) => unit == 0xFEFF || System.Globalization.CharUnicodeInfo.GetUnicodeCategory((char)unit) is System.Globalization.UnicodeCategory.Control or System.Globalization.UnicodeCategory.SpaceSeparator or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator or System.Globalization.UnicodeCategory.PrivateUse or System.Globalization.UnicodeCategory.OtherNotAssigned;
        var actual = escaped.SelectMany(range => Enumerable.Range(range.From, range.To - range.From + 1)).ToHashSet();
        var differ = Enumerable.Range(0x21, 0xFFFF - 0x20).Where(unit => unit is < 0xD800 or > 0xDFFF && unit != 0x22 && unit != 0x5C && actual.Contains(unit) != ByCategory(unit)).ToArray();
        // The whole BMP: escaped exactly when the Help says so, by .NET's own Unicode tables. A .NET update that changes
        // these tables changes the hashed text of such characters, and this test says so.
        Assert.Empty(differ.Select(unit => $"{unit:X4}:{(actual.Contains(unit) ? "escaped" : "kept")}:{System.Globalization.CharUnicodeInfo.GetUnicodeCategory((char)unit)}"));
        Assert.Contains((0x7F, 0xA0), escaped); // the C1 controls and the no-break space
        Assert.Contains(escaped, range => range.From <= 0x2000 && range.To >= 0x200A); // the other spaces
        Assert.Contains((0x3000, 0x3000), escaped);
    }

    [Theory]
    [InlineData("""{"b":1,"a":2,"B":3,"é":4,"_":5}""", """{"B":3,"_":5,"a":2,"b":1,"é":4}""")]
    [InlineData("""{ "a" : [ 1 , { "y" : true , "x" : null } ] }""", """{"a":[1,{"x":null,"y":true}]}""")]
    [InlineData("""{"n":520.0}""", """{"n":520}""")]
    [InlineData("""{"n":-3.0}""", """{"n":-3}""")]
    [InlineData("""{"n":2.50}""", """{"n":2.5}""")]
    [InlineData("""{"n":0.1}""", """{"n":0.1}""")]
    [InlineData("""{"n":1e-7}""", """{"n":1E-07}""")]
    [InlineData("""{"n":123456789012345.0}""", """{"n":123456789012345}""")]
    [InlineData("""{"n":1e15}""", """{"n":1000000000000000}""")] // read as a double: from 1e15 its shortest form, plain below 1e17
    [InlineData("""{"n":1e16}""", """{"n":10000000000000000}""")]
    [InlineData("""{"n":99000000000000000.0}""", """{"n":99000000000000000}""")]
    [InlineData("""{"n":12345678901234567.0}""", """{"n":12345678901234568}""")] // the nearest double
    [InlineData("""{"n":1e17}""", """{"n":1E+17}""")] // C3 round three, point 3: from 1e17 with an E
    [InlineData("""{"n":1.0e17}""", """{"n":1E+17}""")]
    [InlineData("""{"n":100000000000000000.0}""", """{"n":1E+17}""")]
    [InlineData("""{"n":9.3e18}""", """{"n":9.3E+18}""")]
    [InlineData("""{"n":100000000000000000}""", """{"n":100000000000000000}""")] // digits only, fits 64 bits: as written
    [InlineData("""{"n":-9223372036854775808}""", """{"n":-9223372036854775808}""")]
    [InlineData("""{"n":0.00009}""", """{"n":9E-05}""")]
    [InlineData("""{"n":1.5e15}""", """{"n":1500000000000000}""")]
    [InlineData("""{"n":1e19}""", """{"n":1E+19}""")]
    [InlineData("""{"n":1500000000000000.0}""", """{"n":1500000000000000}""")]
    [InlineData("""{"n":9.5}""", """{"n":9.5}""")]
    [InlineData("""{"n":123456789012345.6}""", """{"n":123456789012345.6}""")]
    [InlineData("""{"n":1234567890123456.5}""", """{"n":1234567890123456.5}""")]
    [InlineData("""{"n":0.0001}""", """{"n":0.0001}""")]
    [InlineData("""{"n":0.00001}""", """{"n":1E-05}""")]
    [InlineData("""{"n":-0.0}""", """{"n":0}""")]
    [InlineData("""{"n":1.0e2}""", """{"n":100}""")]
    [InlineData("""{"n":1e21}""", """{"n":1E+21}""")]
    [InlineData("""{"n":12345678901234567890}""", """{"n":1.2345678901234567E+19}""")]
    [InlineData("""{"s":"é 中 <&'>"}""", """{"s":"é 中 <&'>"}""")]
    [InlineData("""{"s":"\"\\/"}""", """{"s":"\"\\/"}""")]
    [InlineData("""{"s":"a\nb\tc\u0001\u007f"}""", """{"s":"a\nb\tc\u0001\u007F"}""")]
    public void TheCanonicalText_ShouldBeWrittenThus(string json, string canonical) =>
        Assert.Equal(canonical, CanonicalJson.Serialize(JsonNode.Parse(json)));

    /// <summary>Characters the writer escapes although JSON doesn't require it, as uppercase \uXXXX (UTF-16 units).</summary>
    [Theory]
    [InlineData(new[] { 0x2028, 0x2029, 0xE000, 0xFEFF }, new[] { 0x2028, 0x2029, 0xE000, 0xFEFF })] // separators, private use, the byte order mark
    [InlineData(new[] { 0x08, 0x0C, 0x0D }, new int[0], "\\b\\f\\r")] // control characters with a short escape
    [InlineData(new[] { 0x80, 0x9F }, new[] { 0x80, 0x9F })] // C1 control characters
    [InlineData(new[] { 0xAD, 0x200D }, new int[0])] // the soft hyphen and the zero-width joiner stay as they are
    [InlineData(new[] { 0xD83D, 0xDE00 }, new[] { 0xD83D, 0xDE00 })] // an emoji: a character outside the BMP, as its two UTF-16 units
    [InlineData(new[] { 0xE9, 0x4E2D, 0x20AC, 0x2014 }, new int[0])] // é 中 € — stay as they are
    public void SomeCharacters_ShouldBeEscaped(int[] units, int[] escaped, string? written = null)
    {
        var text = new string(units.Select(unit => (char)unit).ToArray());
        static string Escape(int unit) => "\\" + "u" + unit.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        var expected = written ?? (escaped.Length == 0 ? text : string.Concat(escaped.Select(Escape)));

        Assert.Equal("{\"s\":\"" + expected + "\"}", CanonicalJson.Serialize(new JsonObject { ["s"] = text }));
    }

    [Fact]
    public void TheHelp_ShouldSayHowAnOutsiderChecksTheData()
    {
        var help = File.ReadAllText(DppTestFiles.RepoFile("web", "Views", "Help", "Index.cshtml"));

        Assert.Contains("<p id=\"api-public-data-check\">", help, StringComparison.Ordinal);
        Assert.Contains("Never use <code>publicKeyX</code> and <code>publicKeyY</code> written inside the proof", help, StringComparison.Ordinal);
        foreach (var attribute in PassportStaticCore.NotSigned.Where(attribute => !attribute.Property.StartsWith("timeSpent", StringComparison.Ordinal)))
        {
            Assert.Contains($"<code>{(attribute.Path.StartsWith("dppHeader", StringComparison.Ordinal) ? attribute.Path : attribute.Property)}</code>", help, StringComparison.Ordinal);
        }

        Assert.Equal(4, PassportStaticCore.NotSigned.Count(attribute => attribute.Property.StartsWith("timeSpent", StringComparison.Ordinal)));
        Assert.Contains("the four <code>timeSpent…</code> values", help, StringComparison.Ordinal);
        // C3 round three, point 3: the three lines the second review compared with the code.
        Assert.Contains("going through objects only: nothing inside an array is touched, at any depth, so an empty object stays when it is in an array or anywhere below one", help, StringComparison.Ordinal);
        Assert.Contains("digits only (an optional minus, no point, no exponent) that fits a signed 64-bit integer stays as written", help, StringComparison.Ordinal);
        Assert.Contains("If that is a whole number below 10<sup>15</sup>, it is written as an integer", help, StringComparison.Ordinal);
        Assert.Contains("in plain digits from 0.0001 up to below 10<sup>17</sup>", help, StringComparison.Ordinal);
        Assert.Contains("<code>1.0e17</code> → <code>1E+17</code>", help, StringComparison.Ordinal);
        Assert.Contains("After the site changes its signing key, an earlier passport's <code>proof</code> is <code>null</code> until the earlier public key is listed as trusted again", help, StringComparison.Ordinal);
        Assert.DoesNotContain("2<sup>63</sup>", help, StringComparison.Ordinal);
        Assert.Contains("<code>&#92;uD83D&#92;uDE00</code>", help, StringComparison.Ordinal);
        Assert.Contains("U+2028, U+2029, U+FEFF", help, StringComparison.Ordinal);
        Assert.Contains("every space other than the ordinary space (the no-break space U+00A0, U+1680, U+2000–U+200A, U+202F, U+205F, U+3000)", help, StringComparison.Ordinal);
        Assert.Contains("characters that .NET 10's Unicode tables count as unassigned", help, StringComparison.Ordinal);
        Assert.Contains("r and s as 32 bytes each (64 bytes), base64url without padding", help, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHash_ShouldBeSha256OfTheUtf8Text_InLowercaseHex() =>
        Assert.Equal("sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("{\"a\":\"é\"}"u8.ToArray())), CanonicalJson.Hash(JsonNode.Parse("{\"a\":\"é\"}")));

    [Fact]
    public void TheSignedPart_ShouldLeaveOutTheStatusTheLastUpdateTheLiveValuesAndEmptyObjects()
    {
        Assert.Equal(
            [
                "dppHeader.dppStatus",
                "dppHeader.lastUpdated",
                "batteryPassportData.performanceAndDurability.stateOfChargeSoC",
                "batteryPassportData.performanceAndDurability.numberOfFullChargingAndDischargingCycles",
                "batteryPassportData.performanceAndDurability.temperatureInformation",
                "batteryPassportData.performanceAndDurability.informationOnAccidents",
                "batteryPassportData.performanceAndDurability.timeSpentInExtremeTemperaturesAboveBoundary",
                "batteryPassportData.performanceAndDurability.timeSpentInExtremeTemperaturesBelowBoundary",
                "batteryPassportData.performanceAndDurability.timeSpentChargingDuringExtremeTemperaturesAboveBoundary",
                "batteryPassportData.performanceAndDurability.timeSpentChargingDuringExtremeTemperaturesBelowBoundary",
            ],
            PassportStaticCore.NotSigned.Select(attribute => attribute.Path));

        var data = JsonNode.Parse("""
            {
              "dppHeader": { "dppStatus": { "dppStatusValue": "active" }, "lastUpdated": "2026-11-11T10:00:00Z", "dppSchemaVersion": "1.0" },
              "batteryPassportData": {
                "performanceAndDurability": { "stateOfChargeSoC": { "stateOfChargeValue": 80 } },
                "circularity": { "a": { "b": {} }, "list": [ {}, { "c": {}, "d": [ { "e": {} } ] } ] }
              },
              "proof": { "type": "DataIntegrityProof" }
            }
            """)!.AsObject();

        // Empty objects go through objects only; inside an array nothing is touched, at any depth.
        Assert.Equal(
            """{"batteryPassportData":{"circularity":{"list":[{},{"c":{},"d":[{"e":{}}]}]}},"dppHeader":{"dppSchemaVersion":"1.0"}}""",
            CanonicalJson.Serialize(PublicPassport.SignedPart(data)));
    }
}
