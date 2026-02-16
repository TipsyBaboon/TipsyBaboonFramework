using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Text.Json;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models;
using TipsyBaboon.Core.Models.Governance;
using TipsyBaboon.UI.Configuration;
using TipsyBaboon.UI.Extensions;
using TipsyBaboon.UI.Models;

namespace TipsyBaboon.UI.Controllers
{
    /// <summary>
    /// Generic REST API controller that provides CRUD, batch, lookup, and action endpoints
    /// for all registered model types. Routes are dynamically generated per module/model pair
    /// (e.g., <c>/api/Governance/User</c>). All operations enforce RBAC permission checks
    /// and respect invariant record protection.
    /// </summary>
    [ApiController]
    [Authorize]
    public class GenericApiController : ControllerBase
    {
        private readonly BaseModelStore _store;
        private readonly IHostEnvironment _environment;
        private readonly IPermissionService _permissionService;

        private const int MaxBatchSize = 100;

        private string? _requestTypeName;
        private string? _requestModuleName;

        private SchemaDefinition? RequestSchema
        {
            get => field;
            set => field = value;
        }

        public GenericApiController(
            BaseModelStore store,
            IHostEnvironment environment,
            IPermissionService permissionService)
        {
            _store = store;
            _environment = environment;
            _permissionService = permissionService;
        }



        private (Type? EntityType, SchemaDefinition? Schema) ResolveEntityType(string typename, string? moduleName = null)
        {
            // Use cached schema if it matches the current request
            if (RequestSchema != null && _requestTypeName == typename && _requestModuleName == moduleName)
                return (RequestSchema.ModelType, RequestSchema);

            var schema = ModelRegistry.GetModel(typename, moduleName);
            if (schema == null)
                return (null, null);

            // Cache for subsequent calls in this request
            RequestSchema = schema;
            _requestTypeName = typename;
            _requestModuleName = moduleName;

            return (schema.ModelType, schema);
        }

        private const char KeyDelimiter = '~';

        private static object? ConvertValue(string value, Type targetType)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(Guid))
                return Guid.TryParse(value, out var guidValue) ? guidValue : null;
            if (underlyingType == typeof(int))
                return int.TryParse(value, out var intValue) ? intValue : null;
            if (underlyingType == typeof(long))
                return long.TryParse(value, out var longValue) ? longValue : null;
            if (underlyingType == typeof(bool))
                return bool.TryParse(value, out var boolValue) ? boolValue : null;
            if (underlyingType == typeof(DateTime))
                return DateTime.TryParse(value, out var dateValue) ? dateValue : null;
            if (underlyingType == typeof(DateOnly))
                return DateOnly.TryParse(value, out var dateOnlyValue) ? dateOnlyValue : null;
            if (underlyingType == typeof(TimeOnly))
                return TimeOnly.TryParse(value, out var timeOnlyValue) ? timeOnlyValue : null;
            if (underlyingType == typeof(decimal))
                return decimal.TryParse(value, out var decValue) ? decValue : null;
            if (underlyingType == typeof(double))
                return double.TryParse(value, out var dblValue) ? dblValue : null;
            if (underlyingType == typeof(string))
                return value;

            return null;
        }

        private Dictionary<string, object>? ParseKeyValues(string keyString, SchemaDefinition schema)
        {
            var keyColumns = schema.KeyColumns;
            if (keyColumns.Count == 0) return null;

            var keyParts = keyString.Split(KeyDelimiter);
            if (keyParts.Length != keyColumns.Count)
                return null;

            var keyValues = new Dictionary<string, object>();
            for (int i = 0; i < keyColumns.Count; i++)
            {
                var column = keyColumns[i];
                var rawValue = keyParts[i];
                var convertedValue = ConvertValue(rawValue, column.ClrType);
                if (convertedValue == null && !column.IsNullable)
                    return null;
                keyValues[column.PropertyName] = convertedValue!;
            }

            return keyValues;
        }

        /// <summary>
        /// Lists records with server-side paging, sorting, and filtering.
        /// Enforces read permissions and ownership-scoped filtering.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{type}")]
        public async Task<IActionResult> List(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDir = null,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            // ChangeLog uses privilege-based access instead of standard CRUD permissions
            if (module == "Governance" && type == "ChangeLog")
            {
                var radModuleSchema = ModelRegistry.GetModel("RADModule", "Governance");
                if (radModuleSchema == null || !await _permissionService.HasPrivilegeAsync(radModuleSchema.RecordModelId, "View Change Log", cancellationToken))
                {
                    return StatusCode(403, ApiResponse.Error("Insufficient permissions to read this resource"));
                }
            }
            else if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Read, cancellationToken))
            {
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to read this resource"));
            }

            try
            {
                var request = new PagedRequest { Page = page, PageSize = pageSize };

                if (!string.IsNullOrEmpty(sortBy))
                {
                    var sortFields = sortBy.Split(',');
                    var sortDirs = (sortDir ?? "").Split(',');

                    for (int i = 0; i < sortFields.Length; i++)
                    {
                        var field = sortFields[i].Trim();
                        if (string.IsNullOrEmpty(field)) continue;

                        var direction = i < sortDirs.Length && sortDirs[i].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase)
                            ? SortDirection.Descending
                            : SortDirection.Ascending;

                        var sortProperty = entityType.GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (sortProperty != null)
                        {
                            request.Sorts.Add(new SortCriteria { PropertyName = sortProperty.Name, Direction = direction });
                        }
                    }
                }

                var queryParams = Request.Query;
                var knownParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "page", "pageSize", "sortBy", "sortDir" };

                foreach (var param in queryParams)
                {
                    if (knownParams.Contains(param.Key))
                        continue;

                    var filterValue = param.Value.ToString();
                    if (string.IsNullOrEmpty(filterValue))
                        continue;

                    string propertyName;
                    bool isColumnFilter = false;

                    if (param.Key.StartsWith("filter_", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = param.Key.Substring(7);
                        isColumnFilter = true;
                    }
                    else
                    {
                        propertyName = param.Key;
                    }

                    var property = entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property != null)
                    {
                        var underlyingType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                        if (isColumnFilter && underlyingType == typeof(string))
                        {
                            request.Filters.Add(FilterCriteria.Contains(property.Name, filterValue));
                        }
                        else
                        {
                            var value = ConvertValue(filterValue, property.PropertyType);
                            if (value != null)
                            {
                                request.Filters.Add(FilterCriteria.Equals(property.Name, value));
                            }
                        }
                    }
                }

                var result = await _store.QueryAsync(entityType, request, cancellationToken);

                var filteredItems = await _permissionService.FilterRecordsByPermissionForSchemaAsync(
                    result.Items, schema, ActionType.Read, cancellationToken);

                var itemsList = filteredItems.ToList();
                await ApplyGetActionAsync(itemsList, cancellationToken);

                var filteredResult = new PagedResult<object>
                {
                    Items = itemsList,
                    TotalCount = result.TotalCount,
                    Page = result.Page,
                    PageSize = result.PageSize
                };

                return Ok(ApiResponse.Ok(filteredResult));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.FromException("An error occurred while retrieving the list", ex, _environment.IsDevelopment()));
            }
        }

        /// <summary>
        /// Retrieves a single record by primary key (supports composite keys delimited by <c>~</c>).
        /// Enforces read permissions and ownership-level access checks.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{type}/{id}")]
        public async Task<IActionResult> GetById(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromRoute] string id,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            // ChangeLog uses privilege-based access instead of standard CRUD permissions
            if (module == "Governance" && type == "ChangeLog")
            {
                var radModuleSchema = ModelRegistry.GetModel("RADModule", "Governance");
                if (radModuleSchema == null || !await _permissionService.HasPrivilegeAsync(radModuleSchema.RecordModelId, "View Change Log", cancellationToken))
                {
                    return StatusCode(403, ApiResponse.Error("Insufficient permissions to read this resource"));
                }
            }
            else if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Read, cancellationToken))
            {
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to read this resource"));
            }

            var keyValues = ParseKeyValues(id, schema);
            if (keyValues == null)
            {
                var expectedKeys = string.Join(KeyDelimiter.ToString(), schema.KeyColumns.Select(k => k.PropertyName));
                return BadRequest(ApiResponse.Error($"Invalid key format. Expected format: {expectedKeys}"));
            }

            try
            {
                var result = await _store.GetByIdAsync(entityType, keyValues, cancellationToken);

                if (result == null)
                    return NotFound(ApiResponse.Error($"Record with key '{id}' not found"));

                // Apply record-level permission filtering
                var filteredResults = await _permissionService.FilterRecordsByPermissionForSchemaAsync(
                    new[] { result }, schema, ActionType.Read, cancellationToken);
                if (!filteredResults.Any())
                    return NotFound(ApiResponse.Error($"Record with key '{id}' not found"));

                if (schema.ModuleName == "Governance" && schema.TypeName == "Permission")
                {
                    var pEntity = result as Permission;
                    if (pEntity != null)
                    {
                        var subModel = ModelRegistry.GetModel(pEntity.RecordId);

                        if (subModel != null && subModel.IsUIReadOnly)
                        {
                            schema.Columns.Single(c => c.ColumnName == "CanCreate").IsUIReadOnly = true;
                            schema.Columns.Single(c => c.ColumnName == "UpdateLevel").IsUIReadOnly = true;
                            schema.Columns.Single(c => c.ColumnName == "DeleteLevel").IsUIReadOnly = true;

                            pEntity.CanCreate = false;
                            pEntity.UpdateLevel = PermissionLevel.None;
                            pEntity.DeleteLevel = PermissionLevel.None;
                        }
                    }
                }

                await ApplyGetActionAsync(result, cancellationToken);
                return Ok(ApiResponse.Ok(result));
            }
            catch (Exception ex)
            {

                return StatusCode(500, ApiResponse.FromException("An error occurred while retrieving the entity", ex, _environment.IsDevelopment()));
            }
        }

        /// <summary>
        /// Creates a new record. Deserializes the JSON body to the model type,
        /// applies ownership defaults, and invokes lifecycle hooks.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("{type}")]
        public async Task<IActionResult> Create(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromBody] JsonElement entity,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Create, cancellationToken))
            {
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to create this resource"));
            }

            try
            {
                var deserializedEntity = JsonSerializer.Deserialize(entity.GetRawText(), entityType, TipsyBaboonJsonOptions.Default);
                if (deserializedEntity == null)
                    return BadRequest(ApiResponse.Error("Invalid entity data"));

                if (schema.ModuleName == "Governance" && schema.TypeName == "Permission")
                {
                    var pEntity = deserializedEntity as Permission;
                    if (pEntity != null)
                    {
                        var subModel = ModelRegistry.GetModel(pEntity.RecordId);

                        if (subModel != null && subModel.IsUIReadOnly)
                        {
                            pEntity.CanCreate = false;
                            pEntity.UpdateLevel = PermissionLevel.None;
                            pEntity.DeleteLevel = PermissionLevel.None;
                        }

                        if (pEntity.UpdateLevel > pEntity.ReadLevel)
                            return BadRequest(ApiResponse.Error("UpdateLevel cannot exceed ReadLevel"));
                        if (pEntity.DeleteLevel > pEntity.ReadLevel)
                            return BadRequest(ApiResponse.Error("DeleteLevel cannot exceed ReadLevel"));
                    }
                }

                if (schema.ModuleName == "Governance" && schema.TypeName == "Layout")
                {
                    var layoutEntity = deserializedEntity as Layout;
                    if (layoutEntity != null)
                    {
                        if (layoutEntity.ModelRecordId == Guid.Empty)
                            return BadRequest(ApiResponse.Error("ModelRecordId is required. Navigate to a Model detail page to create a Layout."));

                        var modelRecord = await _store.GetByIdAsync("ModelRecord", layoutEntity.ModelRecordId.ToString(), cancellationToken) as ModelRecord;
                        if (modelRecord == null)
                            return BadRequest(ApiResponse.Error($"ModelRecord with Id '{layoutEntity.ModelRecordId}' not found"));

                    }
                }

                if (schema.KeyColumns.Count == 1)
                {
                    var keyCol = schema.KeyColumns[0];
                    var keyProp = entityType.GetProperty(keyCol.PropertyName);
                    if (keyProp?.PropertyType == typeof(Guid))
                    {
                        var currentId = keyProp.GetValue(deserializedEntity);
                        if (currentId == null || (currentId is Guid guidId && guidId == Guid.Empty))
                            keyProp.SetValue(deserializedEntity, Guid.NewGuid());
                    }
                }

                if (deserializedEntity is TipsyBaboonModel tipsyModel && schema.HasOwnership)
                {
                    tipsyModel.OwnerId = _permissionService.CurrentUserId;
                }

                var result = await _store.InsertAsync(entityType, deserializedEntity, cancellationToken);

                await ApplyGetActionAsync(result, cancellationToken);
                var keyString = GetEntityKeyString(result, schema);
                return CreatedAtAction(nameof(GetById), new { module, type, id = keyString }, ApiResponse.Ok(result, "Entity created successfully"));
            }
            catch (Exception ex)
            {

                return StatusCode(500, ApiResponse.FromException("An error occurred while creating the entity", ex, _environment.IsDevelopment()));
            }
        }

        /// <summary>
        /// Full update of an existing record. Delegates to <see cref="PartialUpdate"/>.
        /// </summary>
        [AllowAnonymous]
        [HttpPut("{type}")]
        public async Task<IActionResult> Update(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromBody] JsonElement entity,
            CancellationToken cancellationToken = default)
        {
            return await PartialUpdate(module, type, entity, cancellationToken);
        }

        /// <summary>
        /// Partial update of an existing record. Merges the JSON patch body
        /// with the existing record, preserving fields not included in the request.
        /// Enforces update permissions, ownership checks, and invariant protection.
        /// </summary>
        [AllowAnonymous]
        [HttpPatch("{type}")]
        public async Task<IActionResult> PartialUpdate(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromBody] JsonElement entity,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Update, cancellationToken))
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to update this resource"));

            // Extract key values from the body
            var keyValues = new Dictionary<string, object>();
            foreach (var keyColumn in schema.KeyColumns)
            {
                if (!entity.TryGetProperty(keyColumn.PropertyName, out var keyElement))
                    return BadRequest(ApiResponse.Error($"Missing required key field: {keyColumn.PropertyName}"));

                var keyValue = JsonSerializer.Deserialize(keyElement.GetRawText(), keyColumn.ClrType, TipsyBaboonJsonOptions.Default);
                if (keyValue == null && !keyColumn.IsNullable)
                    return BadRequest(ApiResponse.Error($"Key field {keyColumn.PropertyName} cannot be null"));

                keyValues[keyColumn.PropertyName] = keyValue!;
            }

            try
            {
                var existing = await _store.GetByIdAsync(entityType, keyValues, cancellationToken);
                if (existing == null)
                    return NotFound(ApiResponse.Error($"Entity not found"));

                if (existing.IsInvariantRecord())
                    return StatusCode(405, ApiResponse.Error("Cannot modify invariant records"));

                var entityModel = existing as TipsyBaboonModel;
                if (entityModel != null && !await _permissionService.CanAccessRecordAsync(entityModel, schema.EffectiveRecordModelId, ActionType.Update, cancellationToken))
                    return StatusCode(403, ApiResponse.Error("Insufficient permissions to update this specific record"));

                if (schema.ModuleName == "Governance" && schema.TypeName == "Permission" && existing is Permission pExisting)
                {
                    var mergedReadLevel = entity.TryGetProperty("ReadLevel", out var rl) ? (PermissionLevel)rl.GetInt32() : pExisting.ReadLevel;
                    var mergedUpdateLevel = entity.TryGetProperty("UpdateLevel", out var ul) ? (PermissionLevel)ul.GetInt32() : pExisting.UpdateLevel;
                    var mergedDeleteLevel = entity.TryGetProperty("DeleteLevel", out var dl) ? (PermissionLevel)dl.GetInt32() : pExisting.DeleteLevel;

                    var subModel = ModelRegistry.GetModel(pExisting.RecordId);
                    if (subModel != null && subModel.IsUIReadOnly)
                    {
                        if (mergedUpdateLevel != PermissionLevel.None || mergedDeleteLevel != PermissionLevel.None)
                            return BadRequest(ApiResponse.Error("Cannot set Update/Delete levels on read-only models"));
                    }

                    if (mergedUpdateLevel > mergedReadLevel)
                        return BadRequest(ApiResponse.Error("UpdateLevel cannot exceed ReadLevel"));
                    if (mergedDeleteLevel > mergedReadLevel)
                        return BadRequest(ApiResponse.Error("DeleteLevel cannot exceed ReadLevel"));
                }

                var privilegedColumns = schema.Columns.Where(c => !string.IsNullOrEmpty(c.RequiredPrivilege)).ToList();
                foreach (var col in privilegedColumns)
                {
                    if (entity.TryGetProperty(col.PropertyName, out _))
                    {
                        var hasPrivilege = await _permissionService.HasPrivilegeAsync(schema.EffectiveRecordModelId, col.RequiredPrivilege!, cancellationToken);
                        if (!hasPrivilege)
                            return StatusCode(403, ApiResponse.Error($"You don't have permission to modify the '{col.DisplayName}' field"));
                    }
                }

                var result = await _store.UpdateAsync(entityType, entity, cancellationToken);

                await ApplyGetActionAsync(result, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Entity updated successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.FromException("An error occurred while updating the entity", ex, _environment.IsDevelopment()));
            }
        }

        /// <summary>
        /// Deletes a record by primary key. Enforces delete permissions,
        /// ownership checks, and invariant record protection.
        /// </summary>
        [AllowAnonymous]
        [HttpDelete("{type}/{id}")]
        public async Task<IActionResult> Delete(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromRoute] string id,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Delete, cancellationToken))
            {
                return StatusCode(403, ApiResponse.Error($"Insufficient permissions to delete {schema.TypeName} records"));
            }

            var keyValues = ParseKeyValues(id, schema);
            if (keyValues == null)
            {
                var expectedKeys = string.Join(KeyDelimiter.ToString(), schema.KeyColumns.Select(k => k.PropertyName));
                return BadRequest(ApiResponse.Error($"Invalid key format. Expected format: {expectedKeys}"));
            }

            try
            {
                var record = await _store.GetByIdAsync(entityType, keyValues, cancellationToken);
                if (record == null)
                    return NotFound(ApiResponse.Error($"Record with key '{id}' not found"));

                if (record.IsInvariantRecord())
                {
                    return StatusCode(405, ApiResponse.Error("Cannot delete invariant records"));
                }

                var entityModel = record as TipsyBaboonModel;
                if (entityModel != null && !await _permissionService.CanAccessRecordForSchemaAsync(entityModel, schema, ActionType.Delete, cancellationToken))
                {
                    return StatusCode(403, ApiResponse.Error("Insufficient permissions to delete this specific record"));
                }

                var success = await _store.DeleteAsync(entityType, keyValues, cancellationToken);
                if (!success)
                    return NotFound(ApiResponse.Error($"Record with key '{id}' not found"));

                return Ok(ApiResponse.Ok(null, "Entity deleted successfully"));
            }
            catch (Exception ex)
            {

                return StatusCode(500, ApiResponse.FromException("An error occurred while deleting the entity", ex, _environment.IsDevelopment()));
            }
        }

        /// <summary>
        /// Executes a batch of insert, update, and delete operations in a single request.
        /// Each operation type independently requires the corresponding CRUD permission.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("{type}/batch")]
        public async Task<IActionResult> Batch(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromBody] BatchRequest request,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            var totalItems = (request.Inserts?.Count ?? 0) + (request.Updates?.Count ?? 0) + (request.Deletes?.Count ?? 0);
            if (totalItems > MaxBatchSize)
                return BadRequest(ApiResponse.Error($"Batch size exceeds maximum of {MaxBatchSize} items"));

            if (totalItems == 0)
                return BadRequest(ApiResponse.Error("Batch request contains no operations"));

            var permissions = await _permissionService.GetEffectivePermissionsForSchemaAsync(schema, cancellationToken);

            if (request.Inserts?.Count > 0 && !permissions.CanCreate)
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to create records in batch"));

            if (request.Updates?.Count > 0 && !permissions.CanUpdate)
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to update records in batch"));

            if (request.Deletes?.Count > 0 && !permissions.CanDelete)
                return StatusCode(403, ApiResponse.Error("Insufficient permissions to delete records in batch"));

            var result = new BatchResult();

            try
            {
                if (request.Inserts?.Count > 0)
                {
                    foreach (var item in request.Inserts)
                    {
                        try
                        {
                            var entity = JsonSerializer.Deserialize(item.GetRawText(), entityType, TipsyBaboonJsonOptions.Default);
                            if (entity != null)
                            {
                                await _store.InsertAsync(entityType, entity, cancellationToken);
                                result.Inserted++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Insert failed: {ex.Message}");
                        }
                    }
                }

                if (request.Updates?.Count > 0)
                {
                    foreach (var item in request.Updates)
                    {
                        try
                        {
                            var entity = JsonSerializer.Deserialize(item.GetRawText(), entityType, TipsyBaboonJsonOptions.Default);
                            if (entity != null && entity.IsInvariantRecord())
                            {
                                result.Errors.Add("Cannot modify invariant records");
                                continue;
                            }

                            if (entity is TipsyBaboonModel model)
                            {
                                if (!await _permissionService.CanAccessRecordAsync(model, schema.EffectiveRecordModelId, ActionType.Update, cancellationToken))
                                {
                                    result.Errors.Add($"Insufficient permissions to update record");
                                    continue;
                                }
                            }
                            if (entity != null)
                            {
                                await _store.UpdateAsync(entityType, entity, cancellationToken);
                                result.Updated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Update failed: {ex.Message}");
                        }
                    }
                }

                if (request.Deletes?.Count > 0)
                {
                    foreach (var id in request.Deletes)
                    {
                        try
                        {
                            var keyValues = new Dictionary<string, object> { { schema.KeyColumns[0].PropertyName, id } };
                            var record = await _store.GetByIdAsync(entityType, keyValues, cancellationToken);

                            if (record != null && record.IsInvariantRecord())
                            {
                                result.Errors.Add($"Cannot delete invariant record {id}");
                                continue;
                            }

                            if (record is TipsyBaboonModel model)
                            {
                                if (!await _permissionService.CanAccessRecordAsync(model, schema.EffectiveRecordModelId, ActionType.Delete, cancellationToken))
                                {
                                    result.Errors.Add($"Insufficient permissions to delete record {id}");
                                    continue;
                                }
                            }

                            var deleted = await _store.DeleteAsync(entityType, keyValues, cancellationToken);
                            if (deleted) result.Deleted++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Delete failed for {id}: {ex.Message}");
                        }
                    }
                }

                var success = result.Errors.Count == 0;
                var message = success
                    ? $"Batch completed: {result.Inserted} inserted, {result.Updated} updated, {result.Deleted} deleted"
                    : $"Batch completed with {result.Errors.Count} errors";

                return Ok(new ApiResponse
                {
                    Success = success,
                    Message = message,
                    Data = result,
                    Errors = result.Errors.Count > 0 ? result.Errors : null
                });
            }
            catch (Exception ex)
            {

                return StatusCode(500, ApiResponse.FromException("An error occurred while processing the batch operation", ex, _environment.IsDevelopment()));
            }
        }

        private async Task ApplyGetActionAsync(object? entity, CancellationToken cancellationToken = default)
        {
            if (entity is IModelWithGetAction modelWithGetAction)
            {
                await modelWithGetAction.BeforeGet();
            }
        }

        private async Task ApplyGetActionAsync(IEnumerable<object> entities, CancellationToken cancellationToken = default)
        {
            foreach (var entity in entities)
            {
                if (entity is IModelWithGetAction modelWithGetAction)
                {
                    await modelWithGetAction.BeforeGet();
                }
            }
        }

        private string GetEntityKeyString(object entity, SchemaDefinition schema)
        {
            var keyValues = new List<string>();
            var entityType = entity.GetType();
            foreach (var keyColumn in schema.KeyColumns)
            {
                var prop = entityType.GetProperty(keyColumn.PropertyName)
                    ?? throw new InvalidOperationException($"Key property '{keyColumn.PropertyName}' not found on type '{entityType.Name}'");
                var value = prop.GetValue(entity);
                keyValues.Add(value?.ToString() ?? string.Empty);
            }
            return string.Join(KeyDelimiter, keyValues);
        }

        /// <summary>
        /// Provides a searchable lookup endpoint for foreign key dropdowns.
        /// Returns key-display pairs filtered by the model's <c>RecordName</c> property.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{type}/lookup")]
        public async Task<IActionResult> Lookup(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromQuery] string? search = null,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            if (!await _permissionService.HasPermissionForSchemaAsync(schema, ActionType.Read, cancellationToken))
                return StatusCode(403, ApiResponse.Error("Insufficient permissions"));

            var recordNameProp = schema.RecordNameProperty;
            if (string.IsNullOrEmpty(recordNameProp))
                return BadRequest(ApiResponse.Error($"Model '{type}' does not have a RecordName property"));

            try
            {
                var request = new PagedRequest { Page = 1, PageSize = pageSize };

                if (!string.IsNullOrWhiteSpace(search))
                    request.Filters.Add(FilterCriteria.Contains(recordNameProp, search));

                request.Sorts.Add(SortCriteria.Asc(recordNameProp));

                var result = await _store.QueryAsync(entityType, request, cancellationToken);

                var filteredItems = await _permissionService.FilterRecordsByPermissionForSchemaAsync(
                    result.Items, schema, ActionType.Read, cancellationToken);

                var keyPropName = schema.KeyColumns[0].PropertyName;
                var lookupItems = filteredItems.Select(item =>
                {
                    var itemType = item.GetType();
                    var keyProp = itemType.GetProperty(keyPropName)
                        ?? throw new InvalidOperationException($"Key property '{keyPropName}' not found on type '{itemType.Name}'");
                    var displayProp = itemType.GetProperty(recordNameProp)
                        ?? throw new InvalidOperationException($"RecordName property '{recordNameProp}' not found on type '{itemType.Name}'");
                    var keyValue = keyProp.GetValue(item)?.ToString() ?? "";
                    var displayValue = displayProp.GetValue(item)?.ToString() ?? "";
                    return new { Key = keyValue, Display = displayValue };
                }).ToList();

                return Ok(ApiResponse.Ok(lookupItems));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.FromException("An error occurred during lookup", ex, _environment.IsDevelopment()));
            }
        }

        /// <summary>
        /// Executes a custom action method declared on a model via <see cref="Core.Attributes.UIActionAttribute"/>.
        /// The action is invoked via reflection with the service provider and optional entity instance.
        /// </summary>
        [HttpPost("{type}/{id?}/action/{actionName}")]
        public async Task<IActionResult> ExecuteAction(
            [FromRoute] string module,
            [FromRoute] string type,
            [FromRoute] string? id,
            [FromRoute] string actionName,
            [FromServices] IServiceProvider serviceProvider,
            CancellationToken cancellationToken = default)
        {
            var (entityType, schema) = ResolveEntityType(type, module);

            if (schema == null)
                return NotFound(ApiResponse.Error($"Model '{type}' not found"));

            if (entityType == null)
                return NotFound(ApiResponse.Error($"Type '{schema.ModelType?.FullName}' could not be loaded"));

            // Find the action definition
            var actionDef = schema.Actions.FirstOrDefault(a =>
                string.Equals(a.MethodName, actionName, StringComparison.OrdinalIgnoreCase));

            if (actionDef == null)
                return NotFound(ApiResponse.Error($"Action '{actionName}' not found on model '{type}'"));

            // Get the method via reflection
            var method = entityType.GetMethod(actionDef.MethodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            if (method == null)
                return NotFound(ApiResponse.Error($"Method '{actionDef.MethodName}' not found on type '{entityType.Name}'"));

            // Determine effective model for permission check
            var effectiveModuleName = actionDef.ModuleName ?? schema.ModuleName;
            var effectiveModelName = actionDef.ModelName ?? schema.TypeName;
            var effectiveSchema = string.IsNullOrEmpty(actionDef.ModelName)
                ? schema
                : ModelRegistry.GetModel(effectiveModelName, effectiveModuleName);

            if (effectiveSchema == null)
                return BadRequest(ApiResponse.Error($"Permission model '{effectiveModelName}' not found in module '{effectiveModuleName}'"));

            // Check privilege if specified
            if (!string.IsNullOrEmpty(actionDef.Privilege))
            {
                var hasPrivilege = await _permissionService.HasPrivilegeAsync(
                    effectiveSchema.EffectiveRecordModelId,
                    actionDef.Privilege,
                    cancellationToken);

                if (!hasPrivilege)
                    return StatusCode(403, ApiResponse.Error($"Insufficient privileges: '{actionDef.Privilege}' required"));
            }

            // Check permission level if specified
            if (actionDef.PermissionLevel.HasValue)
            {
                var hasPermission = await _permissionService.HasPermissionForSchemaAsync(
                    effectiveSchema,
                    actionDef.PermissionLevel.Value,
                    cancellationToken);

                if (!hasPermission)
                    return StatusCode(403, ApiResponse.Error($"Insufficient permissions: '{actionDef.PermissionLevel}' access required"));
            }

            try
            {
                object? target = null;

                // Load record if instance method
                if (!method.IsStatic)
                {
                    if (string.IsNullOrEmpty(id))
                        return BadRequest(ApiResponse.Error("Record ID required for instance action"));

                    var keyValues = ParseKeyValues(id, schema);
                    if (keyValues == null)
                        return BadRequest(ApiResponse.Error($"Invalid key format for action '{actionName}'"));

                    target = await _store.GetByIdAsync(entityType, keyValues, cancellationToken);

                    if (target == null)
                        return NotFound(ApiResponse.Error($"Record with key '{id}' not found"));

                    // Check record-level permission
                    if (actionDef.PermissionLevel.HasValue && target is TipsyBaboonModel model)
                    {
                        var canAccessRecord = await _permissionService.CanAccessRecordAsync(
                            model,
                            effectiveSchema.EffectiveRecordModelId,
                            actionDef.PermissionLevel.Value,
                            cancellationToken);

                        if (!canAccessRecord)
                            return NotFound(ApiResponse.Error($"Record with key '{id}' not found"));
                    }
                }

                var committingUser = User.Identity?.Name
                    ?? throw new InvalidOperationException("User identity is not available for action execution");

                // Invoke the method
                var result = method.Invoke(target, new object[] { serviceProvider, committingUser, cancellationToken });

                // Handle async methods
                if (result is Task<ApiResponse> asyncTask)
                {
                    var apiResponse = await asyncTask;
                    return Ok(apiResponse);
                }
                else if (result is ApiResponse syncResponse)
                {
                    return Ok(syncResponse);
                }
                else
                {
                    return StatusCode(500, ApiResponse.Error($"Action '{actionName}' returned unexpected type"));
                }
            }
            catch (TargetInvocationException tex)
            {
                var innerEx = tex.InnerException ?? tex;
                return StatusCode(500, ApiResponse.FromException($"Error executing action '{actionName}'", innerEx, _environment.IsDevelopment()));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.FromException($"Error executing action '{actionName}'", ex, _environment.IsDevelopment()));
            }
        }
    }
}

