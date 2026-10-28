using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BatteryPassWeb.Dpp;

public static class DppServiceCollectionExtensions
{
    /// <summary>Registers the V3 passport services (design section 3). They need MongoContext, ApplicationAuditService and DemoSigningKeyService.</summary>
    public static IServiceCollection AddDpp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => OfficialSchemas.Load());
        services.AddSingleton<AttributeValueValidator>();
        services.AddSingleton<AttributeValueNormalizer>();
        services.AddSingleton<DppStore>();
        services.AddSingleton<IIdentifierScheme, TestIdentifierScheme>();
        services.AddSingleton<SavePipeline>();
        services.AddSingleton<PackCreationService>();
        services.AddSingleton<PassportSigner>();
        services.AddSingleton<PublicationChecks>();
        services.AddSingleton<PublicationService>();
        services.AddSingleton<IPackChangeListener>(provider => provider.GetRequiredService<PublicationService>());
        services.AddSingleton<PassportReader>();
        return services;
    }
}
