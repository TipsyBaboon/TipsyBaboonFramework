namespace TipsyBaboon.UI.Models
{
    /// <summary>
    /// View model for the role-privilege assignment UI, grouping assignable privileges by model
    /// so administrators can toggle per-model CRUD permissions for a given role.
    /// </summary>
    public class RolePrivilegeGroupModel
    {
        public string ContainerId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public Guid ParentRoleId { get; set; }
        public Dictionary<string, List<RolePrivilegeItem>> PrivilegesByModel { get; set; } = new();
    }

    /// <summary>
    /// Individual privilege toggle item within a role-privilege assignment group.
    /// </summary>
    public class RolePrivilegeItem
    {
        public Guid RoleId { get; set; }
        public Guid PrivilegeId { get; set; }
        public Guid ModelId { get; set; }
        public string PrivilegeName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
    }
}
