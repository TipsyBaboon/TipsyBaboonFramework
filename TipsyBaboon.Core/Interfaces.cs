// Consolidated interfaces for TipsyBaboon.Core
// This file contains all interfaces from the previous Interfaces/ folder.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Configuration;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Models;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.Core.Interfaces
{

    /// <summary>
    /// Service for managing configuration pages and properties.
    /// Discovers configuration classes and provides CRUD operations for configuration settings.
    /// </summary>
    public interface IConfigurationPageService
    {
        /// <summary>
        /// Discovers all classes marked with [ConfigurationClass] attribute.
        /// </summary>
        /// <returns>List of configuration class types.</returns>
        List<Type> DiscoverConfigurationTypes();
        /// <summary>
        /// Gets all configuration properties for the specified configuration class.
        /// </summary>
        /// <typeparam name="T">Configuration class type.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of configuration properties with metadata.</returns>
        Task<List<ConfigurationPropertyInfo>> GetConfigurationPropertiesAsync<T>(CancellationToken cancellationToken = default)
            where T : ConfigurationBase, new();
        /// <summary>
        /// Saves a single configuration property value.
        /// </summary>
        /// <typeparam name="T">Configuration class type.</typeparam>
        /// <param name="propertyName">Name of the property to save.</param>
        /// <param name="value">New property value.</param>
        /// <param name="modifiedById">ID of user making the change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveConfigurationPropertyAsync<T>(string propertyName, object? value, int modifiedById, CancellationToken cancellationToken = default)
            where T : ConfigurationBase, new();
        /// <summary>
        /// Loads the complete configuration object with all property values.
        /// </summary>
        /// <typeparam name="T">Configuration class type.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Populated configuration instance.</returns>
        Task<T> LoadConfigurationAsync<T>(CancellationToken cancellationToken = default)
            where T : ConfigurationBase, new();
    }

    /// <summary>
    /// Service for loading and saving per-user preference values.
    /// Preferences are discovered from classes annotated with <see cref="Attributes.UserPreferenceClassAttribute"/>
    /// and stored as key-value pairs scoped to individual users.
    /// </summary>
    public interface IUserPreferenceService
    {
        /// <summary>
        /// Discovers all types annotated with <see cref="Attributes.UserPreferenceClassAttribute"/> from registered assemblies.
        /// </summary>
        List<Type> DiscoverUserPreferenceTypes();

        /// <summary>
        /// Loads all preference values for a user into a typed preference object.
        /// </summary>
        /// <typeparam name="T">The preference class type.</typeparam>
        /// <param name="userId">The user's ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Populated preference instance with current values or defaults.</returns>
        Task<T> LoadUserPreferencesAsync<T>(Guid userId, CancellationToken cancellationToken = default)
            where T : UserPreferenceBase, new();

        /// <summary>
        /// Saves a single preference property value for a user.
        /// </summary>
        /// <typeparam name="T">The preference class type.</typeparam>
        /// <param name="userId">The user's ID.</param>
        /// <param name="propertyName">Property name within the preference class.</param>
        /// <param name="value">The value to persist.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SaveUserPreferencePropertyAsync<T>(Guid userId, string propertyName, object? value, CancellationToken cancellationToken = default)
            where T : UserPreferenceBase, new();

        /// <summary>
        /// Retrieves a single raw preference value by key for a user.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="key">The preference key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The stored string value, or <c>null</c> if not set.</returns>
        Task<string?> GetPreferenceAsync(Guid userId, string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a single raw preference value by key for a user.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <param name="key">The preference key.</param>
        /// <param name="value">The value to store, or <c>null</c> to clear.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task SetPreferenceAsync(Guid userId, string key, string? value, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Model lifecycle hook executed before save operations.
    /// Allows models to validate, transform, or cancel save operations.
    /// </summary>
    public interface IModelWithSaveAction
    {
        object? OriginalState { get; set; }

        Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default);
    }
    /// <summary>
    /// Model lifecycle hook executed after successful save operations.
    /// Useful for side effects, notifications, or cascading updates.
    /// </summary>
    public interface IModelWithPostSaveAction
    {
        /// <summary>
        /// Called after save operation completes successfully.
        /// </summary>
        /// <param name="action">Type of action performed (Create, Read, Update, Delete).</param>
        /// <param name="committingUser">Username of the user who made the change.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task AfterSaveAsync(ActionType action, string? committingUser = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Model lifecycle hook executed before retrieving model data.
    /// Useful for lazy loading or computed properties.
    /// </summary>
    public interface IModelWithGetAction
    {
        /// <summary>
        /// Called before returning model data to the caller.
        /// </summary>
        Task BeforeGet();
    }

    /// <summary>
    /// Provider interface for database-specific implementations.
    /// Each provider (SqlServer, PostgreSQL, etc.) registers its services via Configure.
    /// </summary>
    public interface ITipsyBaboonProvider
    {
        /// <summary>
        /// Configures services for the provider implementation.
        /// </summary>
        /// <param name="services">Service collection to register services into.</param>
        void Configure(IServiceCollection services);
    }

    /// <summary>
    /// Storage and registration interface for database views.
    /// Allows read-only access to SQL views with type safety.
    /// </summary>
    public interface IViewModelStore
    {
        /// <summary>
        /// Registers a view definition for querying.
        /// </summary>
        /// <typeparam name="T">View model type.</typeparam>
        /// <param name="definition">View definition with SQL and mappings.</param>
        void RegisterView<T>(ViewDefinition definition) where T : class;
        /// <summary>
        /// Queries all rows from the registered view.
        /// </summary>
        /// <typeparam name="T">View model type.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Enumerable of view rows.</returns>
        Task<IEnumerable<T>> GetViewAsync<T>(CancellationToken cancellationToken = default) where T : class;
        /// <summary>
        /// Checks if a view has been registered.
        /// </summary>
        /// <typeparam name="T">View model type.</typeparam>
        /// <returns>True if registered, false otherwise.</returns>
        bool IsViewRegistered<T>() where T : class;
    }

    /// <summary>
    /// Authentication service for user management and credential validation.
    /// Handles password authentication, OAuth providers, and two-factor authentication.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticates a user by email and password.
        /// </summary>
        /// <param name="email">User email address.</param>
        /// <param name="password">Plaintext password.</param>
        /// <returns>User if authentication succeeds, null otherwise.</returns>
        Task<User?> AuthenticateAsync(string email, string password);
        /// <summary>
        /// Finds a user by email address.
        /// </summary>
        /// <param name="email">Email address to search for.</param>
        /// <returns>User if found, null otherwise.</returns>
        Task<User?> FindByEmailAsync(string email);
        /// <summary>
        /// Finds a user by ID.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>User if found, null otherwise.</returns>
        Task<User?> FindByIdAsync(Guid userId);
        /// <summary>
        /// Creates a new user account.
        /// </summary>
        /// <param name="email">User email address.</param>
        /// <param name="displayName">Optional display name.</param>
        /// <param name="password">Optional password (null for OAuth-only users).</param>
        /// <returns>Created user.</returns>
        Task<User> CreateUserAsync(string email, string? displayName = null, string? password = null);
        /// <summary>
        /// Finds an external login association (OAuth provider).
        /// </summary>
        /// <param name="loginProvider">Provider name (e.g., "Google").</param>
        /// <param name="providerKey">Provider-specific user key.</param>
        /// <returns>UserLogin if found, null otherwise.</returns>
        Task<UserLogin?> FindUserLoginAsync(string loginProvider, string providerKey);
        /// <summary>
        /// Associates an external login provider with a user.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="loginProvider">Provider name.</param>
        /// <param name="providerKey">Provider-specific user key.</param>
        /// <param name="providerDisplayName">Optional display name for the provider.</param>
        Task AddUserLoginAsync(Guid userId, string loginProvider, string providerKey, string? providerDisplayName = null);
        /// <summary>
        /// Validates a plaintext password against a hash.
        /// </summary>
        /// <param name="password">Plaintext password.</param>
        /// <param name="hashedPassword">Stored hash.</param>
        /// <returns>True if password matches.</returns>
        Task<bool> ValidatePasswordAsync(string password, string hashedPassword);
        /// <summary>
        /// Changes a user's password after validating current password.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="currentPassword">Current password for verification.</param>
        /// <param name="newPassword">New password to set.</param>
        /// <returns>True if password change succeeds.</returns>
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
        /// <summary>
        /// Generates a new authenticator key for two-factor authentication setup.
        /// </summary>
        /// <returns>Base32-encoded authenticator key.</returns>
        Task<string> GenerateAuthenticatorKeyAsync();
        /// <summary>
        /// Enables two-factor authentication for a user after verifying setup code.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="authenticatorKey">Authenticator key to store.</param>
        /// <param name="verificationCode">Code from authenticator app to verify setup.</param>
        /// <returns>True if enabled successfully.</returns>
        Task<bool> EnableTwoFactorAsync(Guid userId, string authenticatorKey, string verificationCode);
        /// <summary>
        /// Disables two-factor authentication for a user.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>True if disabled successfully.</returns>
        Task<bool> DisableTwoFactorAsync(Guid userId);
        /// <summary>
        /// Verifies a two-factor authentication code.
        /// </summary>
        /// <param name="authenticatorKey">User's authenticator key.</param>
        /// <param name="code">Code to verify.</param>
        /// <returns>True if code is valid.</returns>
        Task<bool> VerifyTwoFactorCodeAsync(string authenticatorKey, string code);
        /// <summary>
        /// Updates a user's display name.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="displayName">New display name.</param>
        Task UpdateDisplayNameAsync(Guid userId, string? displayName);
    }

    /// <summary>
    /// Password hashing service using secure cryptographic algorithms.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hashes a plaintext password.
        /// </summary>
        /// <param name="password">Plaintext password.</param>
        /// <returns>Hashed password string.</returns>
        string HashPassword(string password);
        /// <summary>
        /// Verifies a plaintext password against a hash.
        /// </summary>
        /// <param name="hashedPassword">Stored hash.</param>
        /// <param name="providedPassword">Plaintext password to verify.</param>
        /// <returns>True if password matches hash.</returns>
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }

    /// <summary>
    /// Calculated permission set for a user on a specific model.
    /// Combines role-based permissions, record ownership, and privilege checks.
    /// </summary>
    public class EffectivePermissions
    {
        /// <summary>
        /// Whether the user can access UI pages for this model.
        /// </summary>
        public bool UsePages { get; set; }
        /// <summary>
        /// Whether the user can create new records.
        /// </summary>
        public bool CanCreate { get; set; }
        /// <summary>
        /// Permission level for reading records (None, Own, Group, All).
        /// </summary>
        public PermissionLevel ReadLevel { get; set; } = PermissionLevel.None;
        /// <summary>
        /// Permission level for updating records (None, Own, Group, All).
        /// </summary>
        public PermissionLevel UpdateLevel { get; set; } = PermissionLevel.None;
        /// <summary>
        /// Permission level for deleting records (None, Own, Group, All).
        /// </summary>
        public PermissionLevel DeleteLevel { get; set; } = PermissionLevel.None;

        /// <summary>
        /// Whether the user can read any records.
        /// </summary>
        public bool CanRead => ReadLevel != PermissionLevel.None;
        /// <summary>
        /// Whether the user can update any records.
        /// </summary>
        public bool CanUpdate => UpdateLevel != PermissionLevel.None;
        /// <summary>
        /// Whether the user can delete any records.
        /// </summary>
        public bool CanDelete => DeleteLevel != PermissionLevel.None;
    }

    /// <summary>
    /// Permission evaluation service for role-based access control (RBAC).
    /// Combines user roles, record ownership, and privilege checks to determine access.
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// Gets the effective permissions for the current user on a model.
        /// </summary>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Calculated permission set.</returns>
        Task<EffectivePermissions> GetEffectivePermissionsAsync(Guid recordModelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the effective permissions for the current user using a schema definition.
        /// </summary>
        /// <param name="schema">Schema definition.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Calculated permission set.</returns>
        Task<EffectivePermissions> GetEffectivePermissionsForSchemaAsync(SchemaDefinition schema, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user has permission to perform an action using a schema definition.
        /// Respects SkipPermissionChecks for models that bypass permission checks.
        /// </summary>
        /// <param name="schema">Schema definition.</param>
        /// <param name="action">Action type (Create, Read, Update, Delete).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user has permission.</returns>
        Task<bool> HasPermissionForSchemaAsync(SchemaDefinition schema, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user has page access using a schema definition.
        /// Respects SkipPermissionChecks for models that bypass permission checks.
        /// </summary>
        /// <param name="schema">Schema definition.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user can access pages.</returns>
        Task<bool> HasPageAccessForSchemaAsync(SchemaDefinition schema, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user can access a specific record using a schema definition.
        /// Respects SkipPermissionChecks for models that bypass permission checks.
        /// </summary>
        /// <param name="record">Record to check.</param>
        /// <param name="schema">Schema definition.</param>
        /// <param name="action">Action type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user can access the record.</returns>
        Task<bool> CanAccessRecordForSchemaAsync(TipsyBaboonModel record, SchemaDefinition schema, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Filters a collection of records using a schema definition.
        /// Respects SkipPermissionChecks for models that bypass permission checks.
        /// </summary>
        /// <param name="records">Records to filter.</param>
        /// <param name="schema">Schema definition.</param>
        /// <param name="action">Action type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Filtered records the user can access.</returns>
        Task<IEnumerable<object>> FilterRecordsByPermissionForSchemaAsync(IEnumerable<object> records, SchemaDefinition schema, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user has permission to perform an action on a model.
        /// </summary>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="action">Action type (Create, Read, Update, Delete).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user has permission.</returns>
        Task<bool> HasPermissionAsync(Guid recordModelId, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the permission level for an action on a model.
        /// </summary>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="action">Action type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Permission level (None, Own, Group, All).</returns>
        Task<PermissionLevel> GetPermissionLevelAsync(Guid recordModelId, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user can access a specific record based on ownership and permissions.
        /// </summary>
        /// <param name="record">Record to check.</param>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="action">Action type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user can access the record.</returns>
        Task<bool> CanAccessRecordAsync(TipsyBaboonModel record, Guid recordModelId, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Filters a collection of records based on current user's permissions and record ownership.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="records">Records to filter.</param>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="action">Action type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Filtered records the user can access.</returns>
        Task<IEnumerable<T>> FilterRecordsByPermissionAsync<T>(IEnumerable<T> records, Guid recordModelId, ActionType action, CancellationToken cancellationToken = default) where T : TipsyBaboonModel;

        /// <summary>
        /// Filters a collection of records (non-generic) based on current user's permissions.
        /// </summary>
        /// <param name="records">Records to filter.</param>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="action">Action type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Filtered records the user can access.</returns>
        Task<IEnumerable<object>> FilterRecordsByPermissionAsync(IEnumerable<object> records, Guid recordModelId, ActionType action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user has a specific named privilege for a model.
        /// </summary>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="privilegeName">Privilege name declared on the model.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user has the privilege.</returns>
        Task<bool> HasPrivilegeAsync(Guid recordModelId, string privilegeName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the permission level for a specific named privilege.
        /// </summary>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="privilegeName">Privilege name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Permission level for the privilege.</returns>
        Task<PermissionLevel> GetPrivilegeLevelAsync(Guid recordModelId, string privilegeName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user can manage system configuration.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user has configuration management permissions.</returns>
        Task<bool> CanManageConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the current user has page access for a model (UsePages permission).
        /// </summary>
        /// <param name="recordModelId">Model record ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if user can access pages.</returns>
        Task<bool> HasPageAccessAsync(Guid recordModelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all external links accessible to the current user based on permissions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of accessible external links.</returns>
        Task<List<ExternalLink>> GetAccessibleExternalLinksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all groups the current user belongs to.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of user groups.</returns>
        Task<List<UserGroupInfo>> GetCurrentUserGroupsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Current authenticated user's ID, or null if not authenticated.
        /// </summary>
        Guid? CurrentUserId { get; }

        /// <summary>
        /// Whether a user is currently authenticated.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Applies field-level privilege restrictions to a schema by setting IsUIReadOnly on 
        /// columns where the user lacks the required privilege.
        /// </summary>
        /// <param name="schema">Schema to modify.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ApplyFieldPrivilegesToSchemaAsync(SchemaDefinition schema, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Information about a user group (Group model).
    /// </summary>
    public class UserGroupInfo
    {
        /// <summary>
        /// Group ID.
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// Group name.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service for applying custom layouts to schema definitions.
    /// Loads user-defined layouts from the database and applies them to model schemas
    /// before form rendering, allowing different layouts for Create vs Detail views.
    /// </summary>
    public interface ILayoutService
    {
        /// <summary>
        /// Applies a custom layout to the schema if one exists for the model and layout type.
        /// Falls back to LayoutType.Default if no specific layout exists.
        /// Modifies the schema's Layout property in-place.
        /// </summary>
        /// <param name="schema">Schema to modify.</param>
        /// <param name="layoutType">Layout type (Create, Read, Update).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ApplyLayoutToSchemaAsync(SchemaDefinition schema, LayoutType layoutType, CancellationToken cancellationToken = default);
    }
}
