using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.Core.Startup
{
    /// <summary>
    /// Hosted service that runs on application startup to discover models and synchronize database schemas.
    /// Executes the following phases:
    /// 1. Assembly discovery (scans for assemblies referencing TipsyBaboon.Core)
    /// 2. Model type discovery (scans for classes inheriting TipsyBaboonModel)
    /// 3. Schema synchronization (creates/updates database tables, indexes, and seeds data)
    /// </summary>
    public class StartupHostedService : IHostedService
    {
        private readonly IServiceProvider _services;

        /// <summary>
        /// Initializes the startup hosted service with the service provider.
        /// </summary>
        /// <param name="services">Service provider for dependency resolution.</param>
        public StartupHostedService(IServiceProvider services)
        {
            _services = services;
        }

        /// <summary>
        /// Executes startup logic: discovers models and synchronizes schemas.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var sp = scope.ServiceProvider;

            var registeredModules = ModelRegistry.GetRegisteredModuleNames();

            var assembliesToScan = DiscoverAssemblies();
            
            ModelRegistry.DiscoverModelTypes(assembliesToScan);

            var filteredSchemas = ModelRegistry.GetSchemaDefinitionsForSync();

            var schemaSync = sp.GetService<SchemaSyncServiceBase>();
            if (schemaSync != null)
            {
                await schemaSync.EnsureCoreSchemaAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Called when the application is stopping. No cleanup needed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private Assembly[] DiscoverAssemblies()
        {
            var coreAssembly = typeof(ModelRegistry).Assembly;
            var coreAssemblyName = coreAssembly.GetName().Name!;
            var result = new HashSet<Assembly> { coreAssembly };

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                result.Add(entryAssembly);
            }

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                    continue;
                    
                if (asm.GetReferencedAssemblies().Any(r => r.Name == coreAssemblyName))
                {
                    result.Add(asm);
                }
            }


            
            return result.ToArray();
        }
    }
}
