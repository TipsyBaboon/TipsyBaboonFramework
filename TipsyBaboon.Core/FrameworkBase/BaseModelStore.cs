using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models;

namespace TipsyBaboon.Core.FrameworkBase
{
    /// <summary>
    /// Abstract base class for model storage implementations.
    /// Database providers (SqlServer, PostgreSQL, etc.) must implement this class
    /// to provide CRUD operations, querying, and view support.
    /// Consumed by generic UI controllers and services for all model operations.
    /// </summary>
    /// <remarks>
    /// Implementation responsibilities:
    /// - Typed generic methods (GetTypedByIdAsync, InsertTypedAsync, UpdateTypedAsync, DeleteTypedAsync)
    /// - Bulk operations (GetTypedByIdsAsync, BulkDeleteTypedAsync)
    /// - Query operations with expressions and PagedRequest support
    /// - Aggregate operations (CountTypedAsync, ExistsTypedAsync)
    /// - View registration and querying
    /// - Lifecycle hooks (IModelWithSaveAction, IModelWithPostSaveAction, IModelWithGetAction)
    /// - Navigation property eager loading based on [IncludeNavigation] attributes
    /// </remarks>
    public abstract class BaseModelStore
    {
        #region Typed Methods (Generic)

        #region Async Declarations

        #region Single Model

        /// <summary>
        /// Retrieves a single typed entity by its primary key.
        /// Eagerly loads navigation properties marked with <see cref="Attributes.IncludeNavigationAttribute"/>.
        /// Invokes <see cref="IModelWithGetAction.BeforeGet"/> if the model implements it.
        /// </summary>
        /// <typeparam name="T">The model type, must derive from <see cref="TipsyBaboonModel"/>.</typeparam>
        /// <param name="id">Primary key value (Guid or composite key dictionary).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entity if found; otherwise <c>null</c>.</returns>
        public virtual Task<T?> GetTypedByIdAsync<T>(object id, CancellationToken cancellationToken = default) where T :  TipsyBaboonModel => throw new NotImplementedException();
        
        protected virtual Task<T> InsertTypedInternalAsync<T>(dynamic record, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();
        protected virtual Task<T> UpdateTypedInternalAsync<T>(dynamic record, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();
        protected virtual Task<T> UpsertTypedInternalAsync<T>(dynamic record, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();
        protected virtual Task<bool> DeleteTypedInternalAsync<T>(object id, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Inserts a new typed entity. Invokes <see cref="IModelWithSaveAction.BeforeChangeAsync"/>
        /// and <see cref="IModelWithPostSaveAction.AfterSaveAsync"/> lifecycle hooks and records a change log entry.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="record">The entity to insert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The inserted entity with server-generated values populated.</returns>
        public Task<T> InsertTypedAsync<T>(dynamic record, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => InsertTypedInternalAsync<T>(record, skipHooks: false, skipChangeLog: false, cancellationToken: cancellationToken);
        
        /// <summary>
        /// Updates an existing typed entity. Invokes lifecycle hooks, enforces invariant protection,
        /// and records a change log entry.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="record">The entity with updated values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated entity.</returns>
        public Task<T> UpdateTypedAsync<T>(dynamic record, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => UpdateTypedInternalAsync<T>(record, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken: cancellationToken);
        
        /// <summary>
        /// Inserts or updates a typed entity based on primary key existence.
        /// Invokes lifecycle hooks and enforces invariant protection.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="record">The entity to upsert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The upserted entity.</returns>
        public Task<T> UpsertTypedAsync<T>(dynamic record, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => UpsertTypedInternalAsync<T>(record, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken: cancellationToken);
        
        /// <summary>
        /// Deletes a typed entity by primary key. Enforces invariant protection
        /// (invariant records cannot be deleted unless explicitly allowed).
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="id">Primary key value (Guid or composite key dictionary).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the entity was deleted; otherwise <c>false</c>.</returns>
        public Task<bool> DeleteTypedAsync<T>(object id, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => DeleteTypedInternalAsync<T>(id, skipHooks: false, ignoreInvariant: false, skipChangeLog: false, cancellationToken);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Retrieves multiple typed entities by their primary keys in a single round-trip.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="ids">Collection of primary key values.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Entities matching the provided IDs.</returns>
        /// Inserts a new typed entity. Invokes <see cref="IModelWithSaveAction.BeforeChangeAsync"/>
        public virtual Task<IEnumerable<T>> GetTypedByIdsAsync<T>(IEnumerable<object> ids, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        protected virtual Task<IEnumerable<T>> BulkInsertTypedInternalAsync<T>(IEnumerable<dynamic> entities, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();
        protected virtual Task<IEnumerable<T>> BulkUpdateTypedInternalAsync<T>(IEnumerable<dynamic> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();
        protected virtual Task<IEnumerable<T>> BulkUpsertTypedInternalAsync<T>(IEnumerable<dynamic> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();
        protected virtual Task<int> BulkDeleteTypedInternalAsync<T>(IEnumerable<object> ids, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Removes invariant records from the database that are no longer declared in code.
        /// Called during startup to prune stale invariant data after code changes.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="validEntities">The current set of code-declared invariant entities.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public virtual Task ClearOrphanedInvariantsAsync<T>(IEnumerable<T> validEntities, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Inserts multiple typed entities in bulk. Invokes lifecycle hooks for each entity.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="entities">The entities to insert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The inserted entities.</returns>
        public Task<IEnumerable<T>> BulkInsertTypedAsync<T>(IEnumerable<dynamic> entities, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => BulkInsertTypedInternalAsync<T>(entities, skipHooks: false, skipChangeLog: false, cancellationToken);
        
        /// <summary>
        /// Updates multiple typed entities in bulk. Invokes lifecycle hooks and enforces invariant protection.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="entities">The entities to update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated entities.</returns>
        public Task<IEnumerable<T>> BulkUpdateTypedAsync<T>(IEnumerable<dynamic> entities, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => BulkUpdateTypedInternalAsync<T>(entities, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);
        
        /// <summary>
        /// Inserts or updates multiple typed entities in bulk based on primary key existence.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="entities">The entities to upsert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The upserted entities.</returns>
        public Task<IEnumerable<T>> BulkUpsertTypedAsync<T>(IEnumerable<dynamic> entities, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => BulkUpsertTypedInternalAsync<T>(entities, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);
        
        /// <summary>
        /// Deletes multiple typed entities by their primary keys.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="ids">Collection of primary key values to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of entities deleted.</returns>
        public Task<int> BulkDeleteTypedAsync<T>(IEnumerable<object> ids, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
            => BulkDeleteTypedInternalAsync<T>(ids, skipHooks: false, ignoreInvariant: false, skipChangeLog: false, cancellationToken);

        #endregion

        #region Query Operations

        /// <summary>
        /// Queries typed entities matching an expression predicate.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="predicate">LINQ expression filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>All matching entities.</returns>
        public virtual Task<IEnumerable<T>> QueryTypedAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Queries typed entities using a <see cref="PagedRequest"/> with server-side paging, sorting, and filtering.
        /// </summary>
        /// <typeparam name="T">The model type.</typeparam>
        /// <param name="request">Paging, sorting, and filter parameters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="PagedResult{T}"/> containing the page of results and total count.</returns>
        public virtual Task<PagedResult<T>> QueryTypedAsync<T>(PagedRequest request, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        #endregion

        #region Aggregate Operations

        /// <summary>
        /// Returns the total count of all records for the specified model type.
        /// </summary>
        public virtual Task<int> CountTypedAsync<T>(CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Returns the count of records matching the specified predicate.
        /// </summary>
        public virtual Task<int> CountTypedAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Checks whether a record with the specified primary key exists.
        /// </summary>
        public virtual Task<bool> ExistsTypedAsync<T>(object id, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Checks whether any record matches the specified predicate.
        /// </summary>
        public virtual Task<bool> ExistsTypedAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        #endregion

        #region Paging Operations

        /// <summary>
        /// Retrieves a page of typed entities with default ordering.
        /// </summary>
        public virtual Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Retrieves a page of typed entities filtered by a predicate.
        /// </summary>
        public virtual Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Retrieves a page of typed entities with custom ordering.
        /// </summary>
        public virtual Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Retrieves a page of typed entities filtered by a predicate with custom ordering.
        /// </summary>
        public virtual Task<IEnumerable<T>> GetTypedPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate, Expression<Func<T, object>> orderBy, bool ascending = true, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        #endregion

        #region View Operations

        /// <summary>
        /// Registers a SQL view type for typed querying. The view must already exist in the database.
        /// Models are marked with <see cref="Attributes.ViewModelAttribute"/> and mapped via <see cref="ViewModelAttribute"/>.
        /// </summary>
        /// <typeparam name="TView">The view model type.</typeparam>
        /// <param name="viewName">The SQL view name.</param>
        public virtual void RegisterView<TView>(string viewName) where TView : class => throw new NotImplementedException();

        /// <summary>
        /// Queries a registered view using an expression predicate.
        /// </summary>
        public virtual Task<IEnumerable<T>> QueryTypedViewAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Queries a registered view with server-side paging, sorting, and filtering.
        /// </summary>
        public virtual Task<PagedResult<T>> QueryTypedViewAsync<T>(PagedRequest request, CancellationToken cancellationToken = default) where T : TipsyBaboonModel => throw new NotImplementedException();

        /// <summary>
        /// Checks whether a view type has been registered for typed querying.
        /// </summary>
        public virtual bool IsViewRegistered<TView>() where TView : class => throw new NotImplementedException();

        #endregion

        #endregion

        #region Sync Wrappers

        #region Single Model

        /// <inheritdoc cref="GetTypedByIdAsync{T}(object, CancellationToken)"/>
        public T? GetTypedById<T>(object id) where T : TipsyBaboonModel => GetTypedByIdAsync<T>(id).GetAwaiter().GetResult();
        /// <inheritdoc cref="InsertTypedAsync{T}(dynamic, CancellationToken)"/>
        public T InsertTyped<T>(dynamic record) where T : TipsyBaboonModel => InsertTypedAsync<T>(record).GetAwaiter().GetResult();
        /// <inheritdoc cref="UpdateTypedAsync{T}(dynamic, CancellationToken)"/>
        public T UpdateTyped<T>(dynamic record) where T : TipsyBaboonModel => UpdateTypedAsync<T>(record).GetAwaiter().GetResult();
        /// <inheritdoc cref="UpsertTypedAsync{T}(dynamic, CancellationToken)"/>
        public T UpsertTyped<T>(dynamic record) where T : TipsyBaboonModel => UpsertTypedAsync<T>(record).GetAwaiter().GetResult();
        /// <inheritdoc cref="DeleteTypedAsync{T}(object, CancellationToken)"/>
        public bool DeleteTyped<T>(object id) where T : TipsyBaboonModel => DeleteTypedAsync<T>(id).GetAwaiter().GetResult();

        #endregion

        #region Bulk Operations

        /// <inheritdoc cref="GetTypedByIdsAsync{T}(IEnumerable{object}, CancellationToken)"/>
        public IEnumerable<T> GetTypedByIds<T>(IEnumerable<object> ids) where T : TipsyBaboonModel => GetTypedByIdsAsync<T>(ids).GetAwaiter().GetResult();
        /// <inheritdoc cref="BulkInsertTypedAsync{T}(IEnumerable{dynamic}, CancellationToken)"/>
        public IEnumerable<T> BulkInsertTyped<T>(IEnumerable<dynamic> entities) where T : TipsyBaboonModel => BulkInsertTypedAsync<T>(entities).GetAwaiter().GetResult();
        /// <inheritdoc cref="BulkUpdateTypedAsync{T}(IEnumerable{dynamic}, CancellationToken)"/>
        public IEnumerable<T> BulkUpdateTyped<T>(IEnumerable<dynamic> entities) where T : TipsyBaboonModel => BulkUpdateTypedAsync<T>(entities).GetAwaiter().GetResult();
        /// <inheritdoc cref="BulkUpsertTypedAsync{T}(IEnumerable{dynamic}, CancellationToken)"/>
        public IEnumerable<T> BulkUpsertTyped<T>(IEnumerable<dynamic> entities) where T : TipsyBaboonModel => BulkUpsertTypedAsync<T>(entities).GetAwaiter().GetResult();
        /// <inheritdoc cref="BulkDeleteTypedAsync{T}(IEnumerable{object}, CancellationToken)"/>
        public int BulkDeleteTyped<T>(IEnumerable<object> ids) where T : TipsyBaboonModel => BulkDeleteTypedAsync<T>(ids).GetAwaiter().GetResult();
        /// <inheritdoc cref="ClearOrphanedInvariantsAsync{T}(IEnumerable{T}, CancellationToken)"/>
        public void ClearOrphanedInvariants<T>(IEnumerable<T> validEntities) where T : TipsyBaboonModel => ClearOrphanedInvariantsAsync<T>(validEntities).GetAwaiter().GetResult();

        #endregion

        #region Query Operations

        /// <inheritdoc cref="QueryTypedAsync{T}(Expression{Func{T, bool}}, CancellationToken)"/>
        public IEnumerable<T> QueryTyped<T>(Expression<Func<T, bool>> predicate) where T : TipsyBaboonModel => QueryTypedAsync<T>(predicate).GetAwaiter().GetResult();
        /// <inheritdoc cref="QueryTypedAsync{T}(PagedRequest, CancellationToken)"/>
        public PagedResult<T> QueryTyped<T>(PagedRequest request) where T : TipsyBaboonModel => QueryTypedAsync<T>(request).GetAwaiter().GetResult();

        #endregion

        #region Aggregate Operations

        /// <inheritdoc cref="CountTypedAsync{T}(CancellationToken)"/>
        public int CountTyped<T>() where T : TipsyBaboonModel => CountTypedAsync<T>().GetAwaiter().GetResult();
        /// <inheritdoc cref="CountTypedAsync{T}(Expression{Func{T, bool}}, CancellationToken)"/>
        public int CountTyped<T>(Expression<Func<T, bool>> predicate) where T : TipsyBaboonModel => CountTypedAsync<T>(predicate).GetAwaiter().GetResult();
        /// <inheritdoc cref="ExistsTypedAsync{T}(object, CancellationToken)"/>
        public bool ExistsTyped<T>(object id) where T : TipsyBaboonModel => ExistsTypedAsync<T>(id).GetAwaiter().GetResult();
        /// <inheritdoc cref="ExistsTypedAsync{T}(Expression{Func{T, bool}}, CancellationToken)"/>
        public bool ExistsTyped<T>(Expression<Func<T, bool>> predicate) where T : TipsyBaboonModel => ExistsTypedAsync<T>(predicate).GetAwaiter().GetResult();

        #endregion

        #region Paging Operations

        /// <inheritdoc cref="GetTypedPagedAsync{T}(int, int, CancellationToken)"/>
        public IEnumerable<T> GetTypedPaged<T>(int pageNumber, int pageSize) where T : TipsyBaboonModel => GetTypedPagedAsync<T>(pageNumber, pageSize).GetAwaiter().GetResult();
        /// <inheritdoc cref="GetTypedPagedAsync{T}(int, int, Expression{Func{T, bool}}, CancellationToken)"/>
        public IEnumerable<T> GetTypedPaged<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate) where T : TipsyBaboonModel => GetTypedPagedAsync<T>(pageNumber, pageSize, predicate).GetAwaiter().GetResult();
        /// <inheritdoc cref="GetTypedPagedAsync{T}(int, int, Expression{Func{T, object}}, bool, CancellationToken)"/>
        public IEnumerable<T> GetTypedPaged<T>(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true) where T : TipsyBaboonModel => GetTypedPagedAsync<T>(pageNumber, pageSize, orderBy, ascending).GetAwaiter().GetResult();
        /// <inheritdoc cref="GetTypedPagedAsync{T}(int, int, Expression{Func{T, bool}}, Expression{Func{T, object}}, bool, CancellationToken)"/>
        public IEnumerable<T> GetTypedPaged<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate, Expression<Func<T, object>> orderBy, bool ascending = true) where T : TipsyBaboonModel => GetTypedPagedAsync<T>(pageNumber, pageSize, predicate, orderBy, ascending).GetAwaiter().GetResult();

        #endregion

        #region View Operations

        /// <inheritdoc cref="QueryTypedViewAsync{T}(Expression{Func{T, bool}}, CancellationToken)"/>
        public IEnumerable<T> QueryTypedView<T>(Expression<Func<T, bool>> predicate) where T : TipsyBaboonModel => QueryTypedViewAsync<T>(predicate).GetAwaiter().GetResult();
        /// <inheritdoc cref="QueryTypedViewAsync{T}(PagedRequest, CancellationToken)"/>
        public PagedResult<T> QueryTypedView<T>(PagedRequest request) where T : TipsyBaboonModel => QueryTypedViewAsync<T>(request).GetAwaiter().GetResult();

        #endregion

        #endregion

        #endregion

        #region Untyped Core Methods (Abstract - SQL Provider Implements)

        protected abstract Type? ResolveModelType(string moduleName, string modelName);

        /// <summary>
        /// Gets all schema definitions from the ModelRegistry.
        /// Protected accessor for provider implementations to query registered models.
        /// </summary>
        protected static IReadOnlyList<SchemaDefinition> GetAllSchemaDefinitions()
            => ModelRegistry.GetAllModels();

        protected abstract Task<object?> GetByIdCoreAsync(Type entityType, object id, CancellationToken cancellationToken = default);
        protected abstract Task<object> InsertCoreAsync(Type entityType, object entity, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<object> UpdateCoreAsync(Type entityType, object entity, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<object> UpsertCoreAsync(Type entityType, object entity, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<bool> DeleteCoreAsync(Type entityType, object id, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<PagedResult<object>> QueryCoreAsync(Type entityType, PagedRequest request, CancellationToken cancellationToken = default);
        protected abstract Task<IEnumerable<object>> GetByIdsCoreAsync(Type entityType, IEnumerable<object> ids, CancellationToken cancellationToken = default);
        protected abstract Task<IEnumerable<object>> BulkInsertCoreAsync(Type entityType, IEnumerable<object> entities, bool skipHooks = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<IEnumerable<object>> BulkUpdateCoreAsync(Type entityType, IEnumerable<object> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<IEnumerable<object>> BulkUpsertCoreAsync(Type entityType, IEnumerable<object> entities, bool skipHooks = false, bool ignoreInvariant = false, bool allowChangeInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<int> BulkDeleteCoreAsync(Type entityType, IEnumerable<object> ids, bool skipHooks = false, bool ignoreInvariant = false, bool skipChangeLog = false, CancellationToken cancellationToken = default);
        protected abstract Task<int> CountCoreAsync(Type entityType, CancellationToken cancellationToken = default);
        protected abstract Task<bool> ExistsCoreAsync(Type entityType, object id, CancellationToken cancellationToken = default);
        protected abstract Task<IEnumerable<object>> GetPagedCoreAsync(Type entityType, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

        #endregion

        #region Untyped Methods (Type Parameter - delegates to Core)

        /// <summary>
        /// Retrieves an entity by its CLR type and primary key. Used by generic UI controllers
        /// that resolve model types at runtime from route parameters.
        /// </summary>
        public Task<object?> GetByIdAsync(Type entityType, object id, CancellationToken cancellationToken = default)
            => GetByIdCoreAsync(entityType, id, cancellationToken);

        /// <summary>
        /// Inserts an entity identified by CLR type. Delegates to the provider's core insert with full hook execution.
        /// </summary>
        public Task<object> InsertAsync(Type entityType, object entity, CancellationToken cancellationToken = default)
            => InsertCoreAsync(entityType, entity, skipHooks: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Updates an entity identified by CLR type. Delegates to the provider's core update with full hook execution.
        /// </summary>
        public Task<object> UpdateAsync(Type entityType, object entity, CancellationToken cancellationToken = default)
            => UpdateCoreAsync(entityType, entity, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Upserts an entity identified by CLR type.
        /// </summary>
        public Task<object> UpsertAsync(Type entityType, object entity, CancellationToken cancellationToken = default)
            => UpsertCoreAsync(entityType, entity, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Deletes an entity identified by CLR type and primary key.
        /// </summary>
        public Task<bool> DeleteAsync(Type entityType, object id, CancellationToken cancellationToken = default)
            => DeleteCoreAsync(entityType, id, skipHooks: false, ignoreInvariant: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Queries entities identified by CLR type using <see cref="PagedRequest"/> for server-side paging.
        /// </summary>
        public Task<PagedResult<object>> QueryAsync(Type entityType, PagedRequest request, CancellationToken cancellationToken = default)
            => QueryCoreAsync(entityType, request, cancellationToken);

        /// <summary>
        /// Retrieves multiple entities by CLR type and primary keys.
        /// </summary>
        public Task<IEnumerable<object>> GetByIdsAsync(Type entityType, IEnumerable<object> ids, CancellationToken cancellationToken = default)
            => GetByIdsCoreAsync(entityType, ids, cancellationToken);

        /// <summary>
        /// Bulk-inserts entities identified by CLR type.
        /// </summary>
        public Task<IEnumerable<object>> BulkInsertAsync(Type entityType, IEnumerable<object> entities, CancellationToken cancellationToken = default)
            => BulkInsertCoreAsync(entityType, entities, skipHooks: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Bulk-updates entities identified by CLR type.
        /// </summary>
        public Task<IEnumerable<object>> BulkUpdateAsync(Type entityType, IEnumerable<object> entities, CancellationToken cancellationToken = default)
            => BulkUpdateCoreAsync(entityType, entities, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Bulk-upserts entities identified by CLR type.
        /// </summary>
        public Task<IEnumerable<object>> BulkUpsertAsync(Type entityType, IEnumerable<object> entities, CancellationToken cancellationToken = default)
            => BulkUpsertCoreAsync(entityType, entities, skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Bulk-deletes entities identified by CLR type and primary keys.
        /// </summary>
        public Task<int> BulkDeleteAsync(Type entityType, IEnumerable<object> ids, CancellationToken cancellationToken = default)
            => BulkDeleteCoreAsync(entityType, ids, skipHooks: false, ignoreInvariant: false, skipChangeLog: false, cancellationToken);

        /// <summary>
        /// Returns the total count of records for a CLR type.
        /// </summary>
        public Task<int> CountAsync(Type entityType, CancellationToken cancellationToken = default)
            => CountCoreAsync(entityType, cancellationToken);

        /// <summary>
        /// Checks whether a record exists by CLR type and primary key.
        /// </summary>
        public Task<bool> ExistsAsync(Type entityType, object id, CancellationToken cancellationToken = default)
            => ExistsCoreAsync(entityType, id, cancellationToken);

        /// <summary>
        /// Retrieves a page of records for a CLR type with default ordering.
        /// </summary>
        public Task<IEnumerable<object>> GetPagedAsync(Type entityType, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => GetPagedCoreAsync(entityType, pageNumber, pageSize, cancellationToken);

        #endregion

        #region Untyped Methods (Model Name Lookup)

        #region Single Model - Async

        /// <summary>
        /// Retrieves an entity by module and model name (resolved from route parameters) and primary key.
        /// This is the primary entry point used by <c>GenericApiController</c>.
        /// </summary>
        /// <param name="moduleName">Module name (e.g., "Governance").</param>
        /// <param name="modelName">Model type name (e.g., "User").</param>
        /// <param name="id">Primary key value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entity if found; otherwise <c>null</c>.</returns>
        public async Task<object?> GetByIdAsync(string moduleName, string modelName, object id, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await GetByIdCoreAsync(entityType, id, cancellationToken);
        }

        /// <summary>
        /// Inserts an entity resolved by module and model name. Used by generic API controllers.
        /// </summary>
        public async Task<object> InsertAsync(string moduleName, string modelName, dynamic record, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await InsertCoreAsync(entityType, (object)record, false, false, cancellationToken);
        }

        /// <summary>
        /// Updates an entity resolved by module and model name.
        /// </summary>
        public async Task<object> UpdateAsync(string moduleName, string modelName, dynamic record, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await UpdateCoreAsync(entityType, (object)record, false, false, false, false, cancellationToken);
        }

        /// <summary>
        /// Upserts an entity resolved by module and model name.
        /// </summary>
        public async Task<object> UpsertAsync(string moduleName, string modelName, dynamic record, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await UpsertCoreAsync(entityType, (object)record, false, false, false, false, cancellationToken);
        }

        /// <summary>
        /// Deletes an entity resolved by module and model name.
        /// </summary>
        public async Task<bool> DeleteAsync(string moduleName, string modelName, object id, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await DeleteCoreAsync(entityType, id, skipHooks: false, ignoreInvariant: false, skipChangeLog: false, cancellationToken);
        }

        #endregion

        #region Single Model - Sync

        /// <inheritdoc cref="GetByIdAsync(string, string, object, CancellationToken)"/>
        public object? GetById(string moduleName, string modelName, object id)
            => GetByIdCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), id).GetAwaiter().GetResult();

        /// <inheritdoc cref="InsertAsync(string, string, dynamic, CancellationToken)"/>
        public object Insert(string moduleName, string modelName, dynamic record)
            => InsertCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), record).GetAwaiter().GetResult();

        /// <inheritdoc cref="UpdateAsync(string, string, dynamic, CancellationToken)"/>
        public object Update(string moduleName, string modelName, dynamic record)
            => UpdateCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), record).GetAwaiter().GetResult();

        /// <inheritdoc cref="UpsertAsync(string, string, dynamic, CancellationToken)"/>
        public object Upsert(string moduleName, string modelName, dynamic record)
            => UpsertCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), record).GetAwaiter().GetResult();

        /// <inheritdoc cref="DeleteAsync(string, string, object, CancellationToken)"/>
        public bool Delete(string moduleName, string modelName, object id)
            => DeleteCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), id).GetAwaiter().GetResult();

        #endregion

        #region Bulk Operations - Async

        /// <summary>
        /// Retrieves multiple entities by module/model name and primary keys.
        /// </summary>
        public async Task<IEnumerable<object>> GetByIdsAsync(string moduleName, string modelName, IEnumerable<object> ids, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await GetByIdsCoreAsync(entityType, ids, cancellationToken);
        }

        /// <summary>
        /// Bulk-inserts entities resolved by module and model name.
        /// </summary>
        public async Task<IEnumerable<object>> BulkInsertAsync(string moduleName, string modelName, IEnumerable<dynamic> entities, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await BulkInsertCoreAsync(entityType, entities.Cast<object>(), skipHooks: false, skipChangeLog: false, cancellationToken);
        }

        /// <summary>
        /// Bulk-updates entities resolved by module and model name.
        /// </summary>
        public async Task<IEnumerable<object>> BulkUpdateAsync(string moduleName, string modelName, IEnumerable<dynamic> entities, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await BulkUpdateCoreAsync(entityType, entities.Cast<object>(), skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);
        }

        /// <summary>
        /// Bulk-upserts entities resolved by module and model name.
        /// </summary>
        public async Task<IEnumerable<object>> BulkUpsertAsync(string moduleName, string modelName, IEnumerable<dynamic> entities, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await BulkUpsertCoreAsync(entityType, entities.Cast<object>(), skipHooks: false, ignoreInvariant: false, allowChangeInvariant: false, skipChangeLog: false, cancellationToken);
        }

        /// <summary>
        /// Bulk-deletes entities resolved by module and model name.
        /// </summary>
        public async Task<int> BulkDeleteAsync(string moduleName, string modelName, IEnumerable<object> ids, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await BulkDeleteCoreAsync(entityType, ids, skipHooks: false, ignoreInvariant: false, skipChangeLog: false, cancellationToken);
        }

        #endregion

        #region Bulk Operations - Sync

        /// <inheritdoc cref="GetByIdsAsync(string, string, IEnumerable{object}, CancellationToken)"/>
        public IEnumerable<object> GetByIds(string moduleName, string modelName, IEnumerable<object> ids)
            => GetByIdsCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), ids).GetAwaiter().GetResult();

        /// <inheritdoc cref="BulkInsertAsync(string, string, IEnumerable{dynamic}, CancellationToken)"/>
        public IEnumerable<object> BulkInsert(string moduleName, string modelName, IEnumerable<dynamic> entities)
            => BulkInsertCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), entities.Cast<object>()).GetAwaiter().GetResult();

        /// <inheritdoc cref="BulkUpdateAsync(string, string, IEnumerable{dynamic}, CancellationToken)"/>
        public IEnumerable<object> BulkUpdate(string moduleName, string modelName, IEnumerable<dynamic> entities)
            => BulkUpdateCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), entities.Cast<object>()).GetAwaiter().GetResult();

        /// <inheritdoc cref="BulkUpsertAsync(string, string, IEnumerable{dynamic}, CancellationToken)"/>
        public IEnumerable<object> BulkUpsert(string moduleName, string modelName, IEnumerable<dynamic> entities)
            => BulkUpsertCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), entities.Cast<object>()).GetAwaiter().GetResult();

        /// <inheritdoc cref="BulkDeleteAsync(string, string, IEnumerable{object}, CancellationToken)"/>
        public int BulkDelete(string moduleName, string modelName, IEnumerable<object> ids)
            => BulkDeleteCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), ids).GetAwaiter().GetResult();

        #endregion

        #region Query Operations - Async

        /// <summary>
        /// Queries entities resolved by module and model name with server-side paging.
        /// </summary>
        public async Task<PagedResult<object>> QueryAsync(string moduleName, string modelName, PagedRequest request, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await QueryCoreAsync(entityType, request, cancellationToken);
        }

        #endregion

        #region Query Operations - Sync

        /// <inheritdoc cref="QueryAsync(string, string, PagedRequest, CancellationToken)"/>
        public PagedResult<object> Query(string moduleName, string modelName, PagedRequest request) => QueryAsync(moduleName, modelName, request).GetAwaiter().GetResult();

        #endregion

        #region Aggregate Operations - Async

        /// <summary>
        /// Returns the total record count for a model resolved by module and model name.
        /// </summary>
        public async Task<int> CountAsync(string moduleName, string modelName, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await CountCoreAsync(entityType, cancellationToken);
        }

        /// <summary>
        /// Checks whether a record exists by module/model name and primary key.
        /// </summary>
        public async Task<bool> ExistsAsync(string moduleName, string modelName, object id, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await ExistsCoreAsync(entityType, id, cancellationToken);
        }

        #endregion

        #region Aggregate Operations - Sync

        /// <inheritdoc cref="CountAsync(string, string, CancellationToken)"/>
        public int Count(string moduleName, string modelName)
            => CountCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found")).GetAwaiter().GetResult();

        /// <inheritdoc cref="ExistsAsync(string, string, object, CancellationToken)"/>
        public bool Exists(string moduleName, string modelName, object id)
            => ExistsCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), id).GetAwaiter().GetResult();

        #endregion

        #region Paging Operations - Async

        /// <summary>
        /// Retrieves a page of records resolved by module and model name.
        /// </summary>
        public async Task<IEnumerable<object>> GetPagedAsync(string moduleName, string modelName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var entityType = ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found");
            return await GetPagedCoreAsync(entityType, pageNumber, pageSize, cancellationToken);
        }

        #endregion

        #region Paging Operations - Sync

        /// <inheritdoc cref="GetPagedAsync(string, string, int, int, CancellationToken)"/>
        public IEnumerable<object> GetPaged(string moduleName, string modelName, int pageNumber, int pageSize)
            => GetPagedCoreAsync(ResolveModelType(moduleName, modelName) ?? throw new InvalidOperationException($"Model type '{moduleName}.{modelName}' not found"), pageNumber, pageSize).GetAwaiter().GetResult();

        #endregion

        #endregion
    }
}

