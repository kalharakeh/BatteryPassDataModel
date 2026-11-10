using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Store;

namespace BatteryPassWeb.Dpp.Access;

/// <summary>
/// What is demo stays in the demo (finding R03, decision 32): a demo account's or demo token's roles and memberships
/// count only on demo batteries. On any other battery it is a visitor: public data only, no actions. The one rule;
/// <see cref="DppReaders"/> marks the reader, and whoever meets a reader with a battery asks here.
/// </summary>
public static class DemoScope
{
    public const string RealBattery = "Demo accounts and demo tokens work only on demo batteries.";

    /// <summary>
    /// The one definition of a battery inside the demo (the manager's decision): the pack is demo, its batch, model and
    /// operator are demo, and its cluster is a demo cluster or none. Any other battery is real to a demo identity, a
    /// demo-ID battery the team made on real product data included. <see cref="DemoBoundary"/> reads these from the store.
    /// </summary>
    public static bool Inside(BatteryPack pack, ProductionBatch? batch, BatteryModel? model, EconomicOperator? economicOperator, bool clusterIsDemoOrNone)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return pack.IsDemo && batch?.IsDemo == true && model?.IsDemo == true && economicOperator?.IsDemo == true && clusterIsDemoOrNone;
    }

    /// <summary>Whether the reader's roles and memberships count on this battery.</summary>
    public static bool Reaches(Reader reader, bool batteryIsDemo)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return !reader.DemoOnly || batteryIsDemo;
    }

    /// <summary>The reader as it counts on this battery: itself, or a visitor (who keeps its name for the logs).</summary>
    public static Reader On(Reader reader, bool batteryIsDemo) =>
        Reaches(reader, batteryIsDemo) ? reader : Reader.Anonymous with { Who = reader.Who };

    /// <summary>Whether the sender may change this battery at all: a demo sender changes only demo batteries.</summary>
    public static bool Reaches(ChangeSender sender, bool batteryIsDemo)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return !sender.DemoOnly || batteryIsDemo;
    }

    /// <summary>A battery moves only into a cluster of its own kind: demo with demo, real with real (finding R03).</summary>
    /// <summary>A demo sender hands a battery over only into a demo cluster; anyone else as before (finding R03).</summary>
    public static bool MayMove(ChangeSender sender, bool clusterIsDemo)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return !sender.DemoOnly || clusterIsDemo;
    }

    /// <summary>A list as the reader may meet it: a demo reader meets only demo rows.</summary>
    public static IEnumerable<T> Lists<T>(Reader reader, IEnumerable<T> rows, Func<T, bool> isDemo)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(rows);
        return reader.DemoOnly ? rows.Where(isDemo) : rows;
    }
}
