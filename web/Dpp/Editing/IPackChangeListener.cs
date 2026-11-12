namespace BatteryPassWeb.Dpp.Editing;

/// <summary>Told after the save pipeline stored values on a record; publication uses it to keep versions current.</summary>
public interface IPackChangeListener
{
    /// <param name="sender">Who sent the change, so what follows from it (a new version) is logged in their name (step 4b).</param>
    Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, ChangeSender sender, CancellationToken cancellationToken);
}
