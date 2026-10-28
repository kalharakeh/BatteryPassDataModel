namespace BatteryPassWeb.Dpp.Editing;

/// <summary>Told after the save pipeline stored values on a record; publication uses it to keep versions current.</summary>
public interface IPackChangeListener
{
    Task ValuesSavedAsync(TargetKind target, string targetId, IReadOnlyCollection<string> keys, string who, CancellationToken cancellationToken);
}
