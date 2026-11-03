using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// The Registry (decision 30): a read-only list of the published batteries a signed-in reader may open, searched like the
/// team's battery list, each opening its passport's detail page. A cluster admin answers the hand-overs waiting for them
/// at its top (step 4d).
/// </summary>
[Authorize]
[Route("registry")]
public sealed class RegistryController(DppReaders readers, BatteryPages batteries) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
    {
        var reader = await readers.ForAsync(User, cancellationToken);
        var editor = await readers.EditorForAsync(User, cancellationToken);
        // One row per battery, the newest passport this reader may see (redesign R5, owner's item 3).
        // A cluster is the team's and its members' business: only they find batteries by it (redesign R6).
        var list = await batteries.ListOnePerBatteryAsync(q, row => RegistryList.MayList(reader, row), RegistryList.ShowsClusters(reader), cancellationToken);
        return View(RegistryList.Build(reader, list, editor) with
        {
            Waiting = editor is null ? [] : await handOvers.WaitingForAsync(editor, cancellationToken),
        });
    }

    /// <summary>The receiving cluster's admin accepts or declines a hand-over (step 4d).</summary>
    [HttpPost("handovers/{handOverId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(string handOverId, [FromForm] bool accept, [FromServices] HandOverService handOvers, CancellationToken cancellationToken)
    {
        var editor = await readers.EditorForAsync(User, cancellationToken);
        var problem = editor is null ? HandOverService.NotTheReceiver : await handOvers.AnswerAsync(editor, handOverId, accept, cancellationToken);
        if (problem is null)
        {
            TempData[ProductDataController.StatusKey] = accept
                ? "Accepted. The battery is now your cluster's; manage it from its row below."
                : "Declined. The battery stays with the sending cluster.";
        }
        else
        {
            TempData[ProductDataController.ErrorKey] = $"{(accept ? "Not accepted" : "Not declined")}: {problem}.";
        }

        return Redirect("/registry");
    }
}
