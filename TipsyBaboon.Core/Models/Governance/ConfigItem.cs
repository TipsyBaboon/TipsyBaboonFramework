using System;
using System.ComponentModel.DataAnnotations;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Configuration key-value pair for module settings.
    /// Stores typed configuration values with precedence ordering and validation metadata.
    /// Used to store both invariant (system-locked) and user-configurable settings.
    /// </summary>
    [ModuleName("Governance")]
    [Index(nameof(ModuleId), nameof(Key))]
    [OwnershipLevels(TipsyBaboon.Core.OwnershipLevel.Public)]
    [AuthorizeAs(AuthorizeAsAttribute.None)]
    public class ConfigItem : TipsyBaboonModel
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ModuleId { get; set; }

        [StringLength(150)]
        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }

        public string ValueType { get; set; } = "System.String";

        public int Precedence { get; set; }

        public bool IsRequired { get; set; }

        public string? Description { get; set; }
    }
}
