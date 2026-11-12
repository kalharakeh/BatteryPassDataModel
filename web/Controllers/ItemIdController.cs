using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The battery ID's own address (#6, step 6d): <c>/25S/UN778474481-VBC-92921</c>, the ISO/IEC 18975 web form of an
/// ISO/IEC 15459 item ID, which the QR code carries. It opens the battery's public summary.
/// </summary>
[AllowAnonymous]
public sealed class ItemIdController : Controller
{
    [HttpGet("25S/{item}")]
    public IActionResult Item(string item) =>
        IdentifierForms.IsItem(item) ? Redirect(PassportPages.SummaryHref(item)) : NotFound();
}
