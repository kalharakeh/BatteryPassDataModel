using BatteryPassWeb.Dpp.Access;
using BatteryPassWeb.Dpp.Api;
using BatteryPassWeb.Dpp.Audit;
using BatteryPassWeb.Dpp.Documents;
using BatteryPassWeb.Dpp.Editing;
using BatteryPassWeb.Dpp.Forms;
using BatteryPassWeb.Dpp.Identifiers;
using BatteryPassWeb.Dpp.Pages;
using BatteryPassWeb.Dpp.Publication;
using BatteryPassWeb.Dpp.Schemas;
using BatteryPassWeb.Dpp.Signing;
using BatteryPassWeb.Dpp.Store;
using BatteryPassWeb.Dpp.Uploads;
using BatteryPassWeb.Dpp.Values;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BatteryPassWeb.Dpp;

public static class DppServiceCollectionExtensions
{
    /// <summary>
    /// Registers the V3 passport services (design section 3). They need MongoContext, ApplicationAuditService,
    /// DemoSigningKeyService, ClusterRepository and the BatteryPassOptions (APP_BASE_URL for document links).
    /// </summary>
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
        services.AddSingleton<StatusChangeService>();
        services.AddSingleton<SuccessionService>();
        services.AddSingleton<PassportMarks>();
        services.AddSingleton<HandOverService>();
        services.AddSingleton<PassportSigner>();
        services.AddSingleton<PublicationChecks>();
        services.AddSingleton<PublicationService>();
        services.AddSingleton<IPackChangeListener>(provider => provider.GetRequiredService<PublicationService>());
        services.AddSingleton<PassportReader>();
        services.AddSingleton<DppLinks>();
        services.AddSingleton<DocumentStore>();
        services.AddSingleton<DocumentService>();
        services.AddSingleton<AttributeForms>();
        services.AddSingleton<RecordEditor>();
        services.AddSingleton<ProductDataPages>();
        services.AddSingleton<StatusPages>();
        services.AddSingleton<LiveTrends>();
        services.AddSingleton<BatteryPages>();
        services.AddSingleton<ClusterBatteryPages>();
        services.AddSingleton<DppReaders>();
        services.AddSingleton<ReaderPages>();
        services.AddSingleton<PassportApi>();
        services.AddSingleton<BatteryApi>();
        services.AddSingleton<IUploadAdapter, NativeUploadFormat>();
        services.AddSingleton<IUploadAdapter, BmsExportFormat>();
        services.AddSingleton<UploadService>();
        services.AddSingleton<PassportPages>();
        services.AddSingleton<AuditLines>();
        services.AddSingleton<ClusterHistory>();
        return services;
    }
}
