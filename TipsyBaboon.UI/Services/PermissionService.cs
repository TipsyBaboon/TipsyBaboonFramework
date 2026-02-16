using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TipsyBaboon.Core;
using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;
using TipsyBaboon.Core.Models;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.UI.Services
{
    /// <summary>
    /// RBAC permission service that evaluates CRUD and privilege-based access for the current HTTP user.
    /// Resolves user roles, computes effective permissions across the role hierarchy,
    /// and caches results per request to minimize database round-trips.
    /// Implements <see cref="IPermissionService"/> consumed by controllers, pages, and rendering components.
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly BaseModelStore _store;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private List<Guid>? _cachedRoleIds;
        private Dictionary<Guid, EffectivePermissions>? _cachedPermissions;
        private Dictionary<(Guid RecordId, string PrivilegeName), PermissionLevel>? _cachedPrivilegeLevels;
        private List<Guid>? _cachedUserGroupIds;
        private User? _cachedCurrentUser;
        private bool _currentUserResolved;

        public PermissionService(BaseModelStore store, IHttpContextAccessor httpContextAccessor)
        {
            _store = store;
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? CurrentUserId => GetCurrentUser()?.Id;

        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

        private ClaimsPrincipal? ClaimsPrincipal => _httpContextAccessor.HttpContext?.User;

        private User? GetCurrentUser()
        {
            if (_currentUserResolved)
                return _cachedCurrentUser;

            _currentUserResolved = true;

            var claimsPrincipal = ClaimsPrincipal;
            var nameIdentifier = claimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = claimsPrincipal?.FindFirst(ClaimTypes.Email)?.Value;
            var identityName = claimsPrincipal?.Identity?.Name;

            if (nameIdentifier == null && email == null && identityName == null)
            {
                _cachedCurrentUser = null;
                return null;
            }

            Guid? parsedId = Guid.TryParse(nameIdentifier, out var id) ? id : null;
            _cachedCurrentUser = _store.QueryTyped<User>(u =>
                (parsedId.HasValue && u.Id == parsedId.Value) ||
                (email != null && u.UserName == email) ||
                (identityName != null && u.UserName == identityName))
                .FirstOrDefault();

            return _cachedCurrentUser;
        }

        public async Task<EffectivePermissions> GetEffectivePermissionsAsync(Guid recordModelId, CancellationToken cancellationToken = default)
        {
            _cachedPermissions ??= new Dictionary<Guid, EffectivePermissions>();

            if (_cachedPermissions.TryGetValue(recordModelId, out var cached))
                return cached;

            var roleIds = await GetCurrentUserRoleIdsAsync(cancellationToken);

            if (!roleIds.Any())
            {
                var none = new EffectivePermissions();
                _cachedPermissions[recordModelId] = none;
                return none;
            }

            var request = PagedRequest.Create(page: 1, pageSize: 1000)
                .WithFilter(FilterCriteria.In("RoleId", roleIds))
                .WithFilter(FilterCriteria.Equals("RecordId", recordModelId));
            var result = await _store.QueryTypedAsync<RolePermission>(request, cancellationToken);
            var rolePermissions = result.Items.ToList();

            var permissions = new EffectivePermissions
            {
                UsePages = rolePermissions.Any(p => p.UsePages),
                CanCreate = rolePermissions.Any(p => p.CanCreate),
                ReadLevel = rolePermissions.Any() ? rolePermissions.Max(p => p.ReadLevel) : PermissionLevel.None,
                UpdateLevel = rolePermissions.Any() ? rolePermissions.Max(p => p.UpdateLevel) : PermissionLevel.None,
                DeleteLevel = rolePermissions.Any() ? rolePermissions.Max(p => p.DeleteLevel) : PermissionLevel.None
            };
            _cachedPermissions[recordModelId] = permissions;
            return permissions;
        }

        public async Task<EffectivePermissions> GetEffectivePermissionsForSchemaAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
        {
            if (schema.SkipPermissionChecks)
                return new EffectivePermissions { UsePages = true, CanCreate = true, ReadLevel = PermissionLevel.All, UpdateLevel = PermissionLevel.All, DeleteLevel = PermissionLevel.All };

            return await GetEffectivePermissionsAsync(schema.EffectiveRecordModelId, cancellationToken);
        }

        public Task<bool> HasPermissionForSchemaAsync(SchemaDefinition schema, ActionType action, CancellationToken cancellationToken = default)
        {
            if (schema.SkipPermissionChecks)
                return Task.FromResult(true);

            return HasPermissionAsync(schema.EffectiveRecordModelId, action, cancellationToken);
        }

        public Task<bool> HasPageAccessForSchemaAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
        {
            if (schema.SkipPermissionChecks)
                return Task.FromResult(true);

            return HasPageAccessAsync(schema.EffectiveRecordModelId, cancellationToken);
        }

        public async Task<bool> HasPermissionAsync(Guid recordModelId, ActionType action, CancellationToken cancellationToken = default)
        {
            var permissions = await GetEffectivePermissionsAsync(recordModelId, cancellationToken);

            return action switch
            {
                ActionType.Create => permissions.CanCreate,
                ActionType.Read => permissions.CanRead,
                ActionType.Update => permissions.CanUpdate,
                ActionType.Delete => permissions.CanDelete,
                _ => false
            };
        }

        public async Task<PermissionLevel> GetPermissionLevelAsync(Guid recordModelId, ActionType action, CancellationToken cancellationToken = default)
        {
            var permissions = await GetEffectivePermissionsAsync(recordModelId, cancellationToken);

            return action switch
            {
                ActionType.Create => permissions.CanCreate ? PermissionLevel.All : PermissionLevel.None,
                ActionType.Read => permissions.ReadLevel,
                ActionType.Update => permissions.UpdateLevel,
                ActionType.Delete => permissions.DeleteLevel,
                _ => PermissionLevel.None
            };
        }

        public async Task<bool> CanAccessRecordAsync(TipsyBaboonModel record, Guid recordModelId, ActionType action, CancellationToken cancellationToken = default)
        {
            var level = await GetPermissionLevelAsync(recordModelId, action, cancellationToken);
            return await CanAccessRecordWithLevelAsync(record, level, cancellationToken);
        }

        public Task<bool> CanAccessRecordForSchemaAsync(TipsyBaboonModel record, SchemaDefinition schema, ActionType action, CancellationToken cancellationToken = default)
        {
            if (schema.SkipPermissionChecks)
                return Task.FromResult(true);

            return CanAccessRecordAsync(record, schema.EffectiveRecordModelId, action, cancellationToken);
        }

        public async Task<IEnumerable<T>> FilterRecordsByPermissionAsync<T>(IEnumerable<T> records, Guid recordModelId, ActionType action, CancellationToken cancellationToken = default) where T : TipsyBaboonModel
        {
            var level = await GetPermissionLevelAsync(recordModelId, action, cancellationToken);

            if (level == PermissionLevel.All)
                return records;

            if (level == PermissionLevel.None)
                return Enumerable.Empty<T>();

            var result = new List<T>();
            foreach (var record in records)
            {
                if (await CanAccessRecordWithLevelAsync(record, level, cancellationToken))
                    result.Add(record);
            }

            return result;
        }

        public async Task<IEnumerable<object>> FilterRecordsByPermissionAsync(IEnumerable<object> records, Guid recordModelId, ActionType action, CancellationToken cancellationToken = default)
        {
            var level = await GetPermissionLevelAsync(recordModelId, action, cancellationToken);

            if (level == PermissionLevel.All)
                return records;

            if (level == PermissionLevel.None)
                return Array.Empty<object>();

            var result = new List<object>();
            foreach (var record in records)
            {
                if (record is TipsyBaboonModel model && await CanAccessRecordWithLevelAsync(model, level, cancellationToken))
                    result.Add(record);
            }

            return result;
        }

        public Task<IEnumerable<object>> FilterRecordsByPermissionForSchemaAsync(IEnumerable<object> records, SchemaDefinition schema, ActionType action, CancellationToken cancellationToken = default)
        {
            if (schema.SkipPermissionChecks)
                return Task.FromResult(records);

            return FilterRecordsByPermissionAsync(records, schema.EffectiveRecordModelId, action, cancellationToken);
        }

        public async Task<bool> HasPrivilegeAsync(Guid recordModelId, string privilegeName, CancellationToken cancellationToken = default)
        {
            var level = await GetPrivilegeLevelAsync(recordModelId, privilegeName, cancellationToken);
            return level != PermissionLevel.None;
        }

        public async Task<PermissionLevel> GetPrivilegeLevelAsync(Guid recordModelId, string privilegeName, CancellationToken cancellationToken = default)
        {
            _cachedPrivilegeLevels ??= new Dictionary<(Guid, string), PermissionLevel>();

            var cacheKey = (recordModelId, privilegeName);
            if (_cachedPrivilegeLevels.TryGetValue(cacheKey, out var cachedLevel))
                return cachedLevel;

            var roleIds = await GetCurrentUserRoleIdsAsync(cancellationToken);
            if (!roleIds.Any())
            {
                _cachedPrivilegeLevels[cacheKey] = PermissionLevel.None;
                return PermissionLevel.None;
            }

            var privilege = (await _store.QueryTypedAsync<Privilege>(
                p => p.RecordId == recordModelId && p.Name == privilegeName, cancellationToken))
                .FirstOrDefault();

            if (privilege == null)
            {
                _cachedPrivilegeLevels[cacheKey] = PermissionLevel.None;
                return PermissionLevel.None;
            }

            var userRolePrivileges = (await _store.QueryTypedAsync<RolePrivilege>(
                rp => roleIds.Contains(rp.RoleId) && rp.PrivilegeId == privilege.Id, cancellationToken))
                .ToList();

            var level = userRolePrivileges.Any() ? PermissionLevel.All : PermissionLevel.None;
            _cachedPrivilegeLevels[cacheKey] = level;
            return level;
        }

        private async Task<List<Guid>> GetCurrentUserRoleIdsAsync(CancellationToken cancellationToken)
        {
            if (_cachedRoleIds != null)
                return _cachedRoleIds;

            var currentUser = GetCurrentUser();

            if (currentUser != null && currentUser.IsActive)
            {
                var userRoles = await _store.QueryTypedAsync<UserRole>(
                    ur => ur.UserId == currentUser.Id, cancellationToken);

                _cachedRoleIds = userRoles
                    .Select(ur => ur.RoleId)
                    .ToList();

                if (!_cachedRoleIds.Contains(Role.BasicRightsRoleId))
                    _cachedRoleIds.Add(Role.BasicRightsRoleId);
            }
            else
            {
                _cachedRoleIds = new List<Guid> { Role.AnonymousRoleId };
            }

            return _cachedRoleIds;
        }

        private async Task<List<Guid>> GetCurrentUserGroupIdsAsync(CancellationToken cancellationToken)
        {
            if (_cachedUserGroupIds != null)
                return _cachedUserGroupIds;

            var currentUser = GetCurrentUser();
            if (currentUser == null)
            {
                _cachedUserGroupIds = new List<Guid>();
                return _cachedUserGroupIds;
            }

            var userGroups = await _store.QueryTypedAsync<UserGroupAssignment>(
                ug => ug.UserId == currentUser.Id, cancellationToken);
            _cachedUserGroupIds = userGroups
                .Select(ug => ug.UserGroupId)
                .ToList();

            return _cachedUserGroupIds;
        }

        private async Task<bool> CanAccessRecordWithLevelAsync(TipsyBaboonModel record, PermissionLevel level, CancellationToken cancellationToken)
        {
            if (level == PermissionLevel.All)
                return true;

            if (level == PermissionLevel.None)
                return false;

            var currentUser = GetCurrentUser();
            if (currentUser == null)
                return false;

            if (level == PermissionLevel.Own)
            {
                return record.OwnerId == currentUser.Id ||
                       record.CreatedById == currentUser.Id;
            }

            if (level == PermissionLevel.Group)
            {
                if (record.OwnerId == currentUser.Id || record.CreatedById == currentUser.Id)
                    return true;

                if (record.GroupId.HasValue)
                {
                    var userGroupIds = await GetCurrentUserGroupIdsAsync(cancellationToken);
                    return userGroupIds.Contains(record.GroupId.Value);
                }
            }

            return false;
        }

        public async Task<bool> CanManageConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var radModuleSchema = ModelRegistry.GetModel("RADModule", "Governance");
            if (radModuleSchema == null)
                return false;

            return await HasPrivilegeAsync(radModuleSchema.RecordModelId, "Manage Configuration", cancellationToken);
        }

        public async Task<bool> HasPageAccessAsync(Guid recordModelId, CancellationToken cancellationToken = default)
        {
            if (!Configuration.TipsyBaboonUIOptions.SpecifyPageAccess)
                return true;

            var permissions = await GetEffectivePermissionsAsync(recordModelId, cancellationToken);
            return permissions.UsePages;
        }

        public async Task<List<ExternalLink>> GetAccessibleExternalLinksAsync(CancellationToken cancellationToken = default)
        {
            var roleIds = await GetCurrentUserRoleIdsAsync(cancellationToken);

            if (!roleIds.Any())
                return new List<ExternalLink>();

            var request = PagedRequest.Create(page: 1, pageSize: 1000)
                .WithFilter(FilterCriteria.In("RoleId", roleIds));
            var roleExternalLinks = await _store.QueryTypedAsync<RoleExternalLink>(request, cancellationToken);

            var externalLinkIds = roleExternalLinks.Items
                .Select(rel => rel.ExternalLinkId)
                .Distinct()
                .ToList();

            if (!externalLinkIds.Any())
                return new List<ExternalLink>();

            var externalLinksRequest = PagedRequest.Create(page: 1, pageSize: 1000)
                .WithFilter(FilterCriteria.In("Id", externalLinkIds));
            var externalLinks = await _store.QueryTypedAsync<ExternalLink>(externalLinksRequest, cancellationToken);

            return externalLinks.Items
                .OrderBy(el => el.MenuOrder)
                .ThenBy(el => el.Title)
                .ToList();
        }

        public async Task<List<UserGroupInfo>> GetCurrentUserGroupsAsync(CancellationToken cancellationToken = default)
        {
            var groupIds = await GetCurrentUserGroupIdsAsync(cancellationToken);
            if (groupIds.Count == 0)
                return new List<UserGroupInfo>();

            var groups = await _store.QueryTypedAsync<UserGroup>(
                g => groupIds.Contains(g.Id), cancellationToken);

            return groups
                .Select(g => new UserGroupInfo { Id = g.Id, Name = g.Name })
                .OrderBy(g => g.Name)
                .ToList();
        }

        public async Task ApplyFieldPrivilegesToSchemaAsync(SchemaDefinition schema, CancellationToken cancellationToken = default)
        {
            if (schema.SkipPermissionChecks)
                return;

            var columnsWithPrivilege = schema.Columns.Where(c => !string.IsNullOrEmpty(c.RequiredPrivilege)).ToList();
            if (!columnsWithPrivilege.Any())
                return;

            foreach (var column in columnsWithPrivilege)
            {
                var hasPrivilege = await HasPrivilegeAsync(schema.EffectiveRecordModelId, column.RequiredPrivilege!, cancellationToken);
                if (!hasPrivilege)
                {
                    column.IsUIReadOnly = true;
                }
            }
        }
    }
}
