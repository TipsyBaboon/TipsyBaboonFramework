using System;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.Core.Startup
{
    /// <summary>
    /// Fluent builder for configuring TipsyBaboon framework.
    /// Used within AddTipsyBaboon() service registration to configure modules, providers, and seed data.
    /// </summary>
    public class TipsyBaboonBuilder
    {
        private readonly IServiceCollection _services;
        private Func<ITipsyBaboonProvider>? _providerFactory;
        private string? _currentModuleName;

        internal TipsyBaboonBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Gets the underlying service collection for advanced service registration.
        /// </summary>
        public IServiceCollection Services => _services;

        /// <summary>
        /// Registers a module with its connection string.
        /// Subsequent AddInvariantConfig/AddSeedConfig calls apply to this module.
        /// </summary>
        /// <param name="moduleName">Unique module name.</param>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="description">Optional module description.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddModule(string moduleName, string connectionString, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("Module name is required", nameof(moduleName));
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string is required", nameof(connectionString));

            ModelRegistry.Register(moduleName, connectionString, description);
            _currentModuleName = moduleName;
            return this;
        }

        /// <summary>
        /// Adds an invariant record to be upserted on every startup.
        /// Invariant records have IsInvariant=true and deterministic GUIDs.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="record">Record instance to add.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddInvariant<T>(T record) where T : class
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            ModelRegistry.AddInvariant(record);
            return this;
        }

        /// <summary>
        /// Adds an invariant configuration value for the current module.
        /// Must call AddModule() first to establish module context.
        /// </summary>
        /// <param name="key">Configuration key (property name).</param>
        /// <param name="value">Configuration value.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddInvariantConfig(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Config key is required", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (string.IsNullOrWhiteSpace(_currentModuleName))
                throw new InvalidOperationException("No module context. Call AddModule() before AddInvariantConfig().");

            ModelRegistry.AddInvariantConfigToModule(_currentModuleName, key, value);
            return this;
        }

        /// <summary>
        /// Adds a seed record to be inserted at table creation only (not maintained).
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="record">Record instance to seed.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddSeed<T>(T record) where T : class
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            ModelRegistry.AddSeed(record);
            return this;
        }

        /// <summary>
        /// Adds a seed configuration value for the current module (inserted at table creation only).
        /// Must call AddModule() first to establish module context.
        /// </summary>
        /// <param name="key">Configuration key (property name).</param>
        /// <param name="value">Configuration value.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddSeedConfig(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Config key is required", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (string.IsNullOrWhiteSpace(_currentModuleName))
                throw new InvalidOperationException("No module context. Call AddModule() before AddSeedConfig().");

            ModelRegistry.AddSeedConfigToModule(_currentModuleName, key, value);
            return this;
        }

        /// <summary>
        /// Adds a seed permission for a role on a model (inserted at table creation only).
        /// </summary>
        /// <param name="roleName">Role name.</param>
        /// <param name="moduleName">Module name.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="usePages">Whether role can access UI pages.</param>
        /// <param name="canCreate">Whether role can create records.</param>
        /// <param name="readLevel">Read permission level.</param>
        /// <param name="updateLevel">Update permission level.</param>
        /// <param name="deleteLevel">Delete permission level.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddSeedPermission(string roleName, string moduleName, string modelName, 
            bool usePages, bool canCreate, PermissionLevel readLevel, PermissionLevel updateLevel, PermissionLevel deleteLevel)
        {
            ModelRegistry.AddSeedPermission(roleName, moduleName, modelName, usePages, canCreate, readLevel, updateLevel, deleteLevel);
            return this;
        }

        /// <summary>
        /// Adds an invariant permission for a role on a model (upserted on every startup).
        /// </summary>
        /// <param name="roleName">Role name.</param>
        /// <param name="moduleName">Module name.</param>
        /// <param name="modelName">Model name.</param>
        /// <param name="usePages">Whether role can access UI pages.</param>
        /// <param name="canCreate">Whether role can create records.</param>
        /// <param name="readLevel">Read permission level.</param>
        /// <param name="updateLevel">Update permission level.</param>
        /// <param name="deleteLevel">Delete permission level.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder AddInvariantPermission(string roleName, string moduleName, string modelName,
            bool usePages, bool canCreate, PermissionLevel readLevel, PermissionLevel updateLevel, PermissionLevel deleteLevel)
        {
            ModelRegistry.AddInvariantPermission(roleName, moduleName, modelName, usePages, canCreate, readLevel, updateLevel, deleteLevel);
            return this;
        }

        /// <summary>
        /// Configures the database provider (SqlServer, PostgreSQL, etc.).
        /// Provider factory is invoked during Build() to configure services.
        /// </summary>
        /// <param name="providerFactory">Factory function that returns a provider instance.</param>
        /// <returns>Builder for method chaining.</returns>
        public TipsyBaboonBuilder UseProvider(Func<ITipsyBaboonProvider> providerFactory)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            return this;
        }

        internal void Build()
        {
            if (ModelRegistry.GetAllModules().Count == 0)
                throw new InvalidOperationException("At least one module must be registered. Call AddModule(\"Governance\", connectionString) first.");

            if (_providerFactory == null)
                throw new InvalidOperationException("A storage provider must be configured. Call UseSqlServer() from TipsyBaboon.SqlServer.");

            var provider = _providerFactory();

            _services.AddHostedService<StartupHostedService>();

            provider.Configure(_services);
        }
    }
}
