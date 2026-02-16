using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Represents a registered module in the TipsyBaboon RAD framework.
    /// Stores module metadata, version tracking, and database connection information.
    /// Modules define logical groupings of models and their associated database context.
    /// Includes privileges for configuration management and change log viewing.
    /// </summary>
    [ModuleName("Governance")]
    [UIEntity("Module", ModuleName = "Governance", GroupName = "Governance", Order = 80, Icon = "bi bi-window-stack", HasOwnership = false)]
    [UIGroup("Module", Title = "Module Configuration", Order = 10)]
    [UIGroup("Database", Title = "Database Settings", Order = 20, Collapsible = true)]
    [Index(nameof(Name), IsUnique = true)]
    [ReadOnly(true)]
    [Privilege("Manage Configuration")]
    [Privilege("View Change Log")]
    public class RADModule : TipsyBaboonModel
    {
        public RADModule()
        {
            OwnershipLevel = OwnershipLevel.Public;
        }

        [Key]
        [UIDisplay(Name = "Id", Order = -1, ShowInList = false, ShowInDetail = true)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [RecordName]
        [Required]
        [StringLength(150)]
        [UIDisplay(Name = "Module Name", GroupName = "Module", Order = 10, ColumnSize = 6, ShowInList = true)]
        public string Name { get; set; } = string.Empty;

        [UIDisplay(Name = "Description", GroupName = "Module", Order = 20, ColumnSize = 12, ShowInList = true)]
        public string? Description { get; set; }

        [ReadOnly(true)]
        [UIDisplay(Name = "Version Hash", GroupName = "Module", Order = 30, ColumnSize = 6, ShowInList = false)]
        public string? VersionHash { get; set; }

        [FieldControl("password")]
        [UIDisplay(Name = "Connection String", GroupName = "Database", Order = 10, ColumnSize = 12)]
        public string? ConnectionString { get; set; }

        [ReadOnly(true)]
        [UIDisplay(Name = "Context Type", GroupName = "Database", Order = 20, ColumnSize = 12, ShowInList = false)]
        public string? ContextTypeName { get; set; }
    }
}
