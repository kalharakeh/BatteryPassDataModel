using System.Globalization;
using System.Security.Claims;
using BatteryPassWeb.Configuration;
using BatteryPassWeb.Controllers;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Values;
using BatteryPassWeb.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace BatteryPassWeb.Tests.Dpp;

/// <summary>The team-page services around a publication rig, and forms posted the way a browser would.</summary>
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

    public static ProductDataPages Pages(PublicationRig rig) => new(
        rig.Store,
        Editor(rig),
        new DocumentStore(rig.Context),
        new ApplicationAuditService(rig.Context),
        Options.Create(new BatteryPassOptions { AppBaseUrl = DocumentTesting.BaseUrl }),
        rig.Clock);

    /// <summary>The controller as a signed-in team member (role "admin") would reach it, with the given form posted.</summary>
    public static ProductDataController Controller(PublicationRig rig, IReadOnlyDictionary<string, string>? form = null)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, "team@example.com"), new Claim(ClaimTypes.Role, AccessControlService.RoleAdmin)],
                "test")),
        };
        http.Request.Form = new FormCollection((form ?? new Dictionary<string, string>()).ToDictionary(entry => entry.Key, entry => new StringValues(entry.Value)));
        return new ProductDataController(Pages(rig), Editor(rig), rig.Store, Documents(rig))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataStorage()),
        };
    }

    private sealed class NoTempDataStorage : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
