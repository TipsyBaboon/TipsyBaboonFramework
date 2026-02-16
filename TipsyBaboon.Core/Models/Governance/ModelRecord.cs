using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Registry record for a model type discovered by ModelRegistry.
    /// Stores model metadata including name, table name, assembly information, and version hash.
    /// Automatically created during schema sync for all models in registered assemblies.
    /// </summary>
    [ModuleName("Governance")]
    [UIEntity("Model", ModuleName = "Governance", GroupName = "Governance", Order = 90, Icon = "bi bi-window", HasOwnership = false, 
        ScriptPaths = new[] { "/_content/TipsyBaboon.UI/js/model-record.js" })]
    [UIGroup("Model", Title = "Model Metadata", Order = 10)]
    [UIGroup("Module", Title = "Module Information", Order = 20)]
    [UIGroup("Permissions", Title = "Permissions", Order = 30)]
    [Index(nameof(Name), nameof(ModuleId), IsUnique = true)]
    [ReadOnly(true)]
    public class ModelRecord : TipsyBaboonModel
    {
        public ModelRecord()
        {
            OwnershipLevel = OwnershipLevel.Public;
        }

        [Key]
        [UIDisplay(Order = -1, ShowInList = false, ShowInDetail = true)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [RecordName]
        [Required]
        [StringLength(150)]
        [UIDisplay(Name = "Model Name", GroupName = "Model", Order = 10, ColumnSize = 6, ShowInList = true)]
        public string Name { get; set; } = string.Empty;

        [UIDisplay(Name = "In Menu", GroupName = "Model", Order = 20, ColumnSize = 2, ShowInList = true)]
        public bool IsInMenu { get; set; }

        [UIDisplay(Name = "Has Forms", GroupName = "Model", Order = 21, ColumnSize = 2, ShowInList = true)]
        public bool HasForms { get; set; }

        [UIDisplay(Name = "Local Permissions", GroupName = "Model", Order = 22, ColumnSize = 2, ShowInList = true)]
        public bool HasLocalPermissions { get; set; }

        [UIDisplay(Name = "Table Name", GroupName = "Model", Order = 30, ColumnSize = 6, ShowInList = false)]
        public string TableName { get; set; } = string.Empty;

        [UIDisplay(Name = "Assembly", GroupName = "Module", Order = 10, ColumnSize = 6, ShowInList = false)]
        public string AssemblyName { get; set; } = string.Empty;

        [Required]
        [UIDisplay(Name = "Fully Qualified Type", GroupName = "Module", Order = 20, ColumnSize = 12, ShowInList = false)]
        public string? FullyQualifiedTypeName { get; set; } // Intentionally nullable for future feature development

        [UIDisplay(Name = "Version Hash", GroupName = "Module", Order = 30, ColumnSize = 6, ShowInList = false)]
        public string VersionHash { get; set; } = string.Empty;

        [UIDisplay(Name = "Module ID", GroupName = "Module", Order = 40, ColumnSize = 6, ShowInList = false)]
        public Guid ModuleId { get; set; } = Guid.NewGuid();

        [UIDisplay(Name = "Layouts", GroupName = "Model", Order = 40, ColumnSize = 12, ShowInList = false)]
        public List<Layout> Layouts { get; set; } = new List<Layout>();

        [UIDisplay(Name = "Role Permissions", GroupName = "Permissions", ColumnSize = 12, Order = 50, ShowInList = false)]
        public List<ModelPermission> ModelPermissions { get; set; } = new List<ModelPermission>();

    }
}
