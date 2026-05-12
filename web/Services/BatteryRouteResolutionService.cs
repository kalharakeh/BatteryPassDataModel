using MongoDB.Bson;

namespace BatteryPassWeb.Services;

public enum BatteryRouteTargetKind
{
    NotFound,
    Battery,
    Passport
}

public sealed record BatteryRouteResolution(BatteryRouteTargetKind Kind, BsonDocument? Document);

public sealed class BatteryRouteResolutionService
{
    private readonly BatteryRepository _batteryRepository;
    private readonly PassportRepository _passportRepository;

    public BatteryRouteResolutionService(BatteryRepository batteryRepository, PassportRepository passportRepository)
    {
        _batteryRepository = batteryRepository;
        _passportRepository = passportRepository;
    }

    public async Task<BatteryRouteResolution> ResolveAsync(string id, CancellationToken cancellationToken = default)
    {
        // BatteryFirst: root IDs resolve as Battery ID before Passport ID.
        var battery = await _batteryRepository.GetByBatteryIdAsync(id, cancellationToken);
        if (battery != null)
        {
            return new BatteryRouteResolution(BatteryRouteTargetKind.Battery, battery);
        }

        var passport = await _passportRepository.GetByPassportIdAsync(id, cancellationToken);
        return passport == null
            ? new BatteryRouteResolution(BatteryRouteTargetKind.NotFound, null)
            : new BatteryRouteResolution(BatteryRouteTargetKind.Passport, passport);
    }
}
