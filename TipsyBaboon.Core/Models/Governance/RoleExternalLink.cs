using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("ExternalLink")]
    [PrimaryKey(nameof(RoleId), nameof(ExternalLinkId))]
    [OwnershipLevels(TipsyBaboon.Core.OwnershipLevel.Public)]
    public class RoleExternalLink : TipsyBaboonModel
    {
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }
        
        [ForeignKey("ExternalLink")]
        public Guid ExternalLinkId { get; set; }

        public Role? Role { get; set; }
        public ExternalLink? ExternalLink { get; set; }
    }
}
