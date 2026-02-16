using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.Data;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("Role")]
    [PrimaryKey(nameof(RoleId), nameof(PrivilegeId))]
    public class RolePrivilege : TipsyBaboonModel
    {

        
        [ForeignKey("Role")]
        public Guid RoleId { get; set; }
        [ForeignKey("Privilege")]
        public Guid PrivilegeId { get; set; }

        [GenerateInvariantRecords]
        public static IEnumerable<RolePrivilege> GetInvariantRecords
        {
            get
            {
                var radModuleRecordId = ModelRegistry.GenerateDeterministicId("Governance|RADModule");
                var roleRecordId = ModelRegistry.GenerateDeterministicId("Governance|Role");
                
                return new List<RolePrivilege>
                {
                    // User Admin - Manage Configuration privilege
                    new RolePrivilege
                    {
                        RoleId = Role.UserAdminRoleId,
                        PrivilegeId = ModelRegistry.GeneratePrivilegeId(radModuleRecordId, "Manage Configuration"),
                        IsInvariant = true,
                        CreatedByDisplayName = "System"
                    },
                    // User Admin - View Change Log privilege
                    new RolePrivilege
                    {
                        RoleId = Role.UserAdminRoleId,
                        PrivilegeId = ModelRegistry.GeneratePrivilegeId(radModuleRecordId, "View Change Log"),
                        IsInvariant = true,
                        CreatedByDisplayName = "System"
                    },

                };
            }
        }

    }
}
