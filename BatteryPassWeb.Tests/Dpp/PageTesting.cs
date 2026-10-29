using System.Globalization;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.Extensions.Options;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The team-page services around a publication rig, and forms filled in the way the page shows them.</summary>
internal static class PageTesting
{
    public static RecordEditor Editor(PublicationRig rig) =>
        new(AttributeFormsTests.Forms, rig.Pipeline, rig.Store, new DocumentStore(rig.Context));

    public static DocumentService Documents(PublicationRig rig) => new(
        new DocumentStore(rig.Context),
        rig.Store,
        rig.Pipeline,
        DocumentTesting.Links,
        Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }),
        new ApplicationAuditService(rig.Context),
        rig.Clock);

    /// <summary>Every field of a record's editor as the page shows it, with the row counts of its lists.</summary>
    public static Dictionary<string, string> ShownForm(TargetKind kind, IReadOnlyDictionary<string, AttributeValue> stored)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in RecordEditor.AttributesOf(kind))
        {
            var form = AttributeFormsTests.Forms.For(attribute);
            var value = stored.GetValueOrDefault(attribute.Property)?.Value;
            foreach (var (name, text) in FormValues.Show(form, value))
            {
                texts[name] = text;
            }

            if (form.Kind == FormKind.Rows)
            {
                texts[form.RowCountField] = FormValues.RowCount(value).ToString(CultureInfo.InvariantCulture);
            }
        }

        return texts;
    }

    public static Func<string, string?> Field(IReadOnlyDictionary<string, string> form) => name => form.GetValueOrDefault(name);
}
