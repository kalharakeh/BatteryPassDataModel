using BatteryPassWeb.Dpp.Pages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BatteryPassWeb.Controllers;

/// <summary>
/// A battery page's one-time message after a change (which section, "saved", the refusal, info) and the upload section's
/// kept check and saved upload. Each carries its battery and shows once, on that battery's page only: the page takes them
/// all from TempData at once, so nothing peeks a message alive, and another battery's message is dropped, not kept (the
/// product owner's step-5 test, 4 Nov 2026: a refused file's message followed them to other batteries and accounts).
/// Signing in or out forgets them, as the TempData cookie belongs to the browser, not to the person.
/// </summary>
public static class CardMessages
{
    /// <summary>The battery the messages in TempData are for.</summary>
    public const string PackKey = "BatteryCardPack";

    private static readonly string[] Keys =
    [
        BatteriesController.CardKey,
        BatteriesController.CardStatusKey,
        BatteriesController.CardErrorKey,
        BatteriesController.CardInfoKey,
        UploadPages.CheckKey,
        UploadPages.OpenKey,
    ];

    /// <summary>Keeps a message for the battery's page after the redirect.</summary>
    public static void Put(ITempDataDictionary tempData, string packId, string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        tempData[PackKey] = packId;
        tempData[key] = value;
    }

    /// <summary>
    /// Moves the messages for this battery into the page's ViewData, where the views read them; a message the request set
    /// itself (a refused save shown without a redirect) stays. Whatever was there, for any battery, is gone from TempData.
    /// </summary>
    public static void Take(ITempDataDictionary tempData, ViewDataDictionary viewData, string packId)
    {
        ArgumentNullException.ThrowIfNull(tempData);
        ArgumentNullException.ThrowIfNull(viewData);
        var mine = string.Equals(tempData[PackKey] as string, packId, StringComparison.Ordinal);
        foreach (var key in Keys)
        {
            var value = tempData[key];
            tempData.Remove(key);
            if (mine && value is not null && viewData[key] is null)
            {
                viewData[key] = value;
            }
        }

        tempData.Remove(PackKey);
    }

    /// <summary>Forgets every one-time message of this browser, on signing in and out.</summary>
    public static void Forget(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (http.RequestServices?.GetService(typeof(ITempDataDictionaryFactory)) is ITempDataDictionaryFactory factory)
        {
            var tempData = factory.GetTempData(http);
            tempData.Clear();
            tempData.Save(); // saved here: a sign-out by the session timeout or the auth API runs outside the MVC save filter
        }
    }

    /// <summary><see cref="Forget"/> for the cookie events.</summary>
    public static Task ForgetAsync(HttpContext http)
    {
        Forget(http);
        return Task.CompletedTask;
    }
}
