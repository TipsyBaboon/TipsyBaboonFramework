using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.SqlServer.Configuration;
using TipsyBaboon.SqlServer.Data;
using TipsyBaboon.SqlServer.Storage;

namespace TipsyBaboon.SqlServer
{
    /// <summary>
    /// SQL Server provider implementation for TipsyBaboon framework.
    /// Registers all SQL Server-specific services including model store, view store, schema sync, and configuration services.
    /// This provider uses pure ADO.NET via Microsoft.Data.SqlClient (no Entity Framework).
    /// </summary>
    public class SqlServerTipsyBaboonProvider : ITipsyBaboonProvider
    {
        /// <summary>
        /// Initializes a new instance of the SQL Server provider.
        /// </summary>
        public SqlServerTipsyBaboonProvider()
        {
        }

        /// <summary>
        /// Configures SQL Server-specific services in the dependency injection container.
        /// Registers BaseModelStore, IViewModelStore, SchemaSyncServiceBase, and configuration services.
        /// </summary>
        /// <param name="services">Service collection to register services into.</param>
        /// <exception cref="InvalidOperationException">Thrown if Governance module is not registered with a connection string.</exception>
        public void Configure(IServiceCollection services)
        {
            var coreConnStr = ModelRegistry.GetConnectionString("Governance");
            if (string.IsNullOrWhiteSpace(coreConnStr))
                throw new InvalidOperationException("Module 'Governance' must be registered with a connection string.");

            services.AddHttpContextAccessor();

            services.AddScoped<BaseModelStore, TipsyBaboonModelStore>();
            services.AddScoped<TipsyBaboonModelStore>();

            services.AddSingleton<IViewModelStore, TipsyBaboonViewModelStore>();

            services.AddSingleton<SchemaSyncServiceBase, TipsyBaboonSchemaSyncService>();

            services.AddScoped<TipsyBaboonDatabaseConfigurationService>();
            services.AddScoped<IConfigurationPageService>(sp => sp.GetRequiredService<TipsyBaboonDatabaseConfigurationService>());

            services.AddScoped<TipsyBaboonUserPreferenceService>();
            services.AddScoped<IUserPreferenceService>(sp => sp.GetRequiredService<TipsyBaboonUserPreferenceService>());
        }
    }
}
