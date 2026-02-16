using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TipsyBaboon.Core.Attributes;

namespace TipsyBaboon.Core.Models.Governance
{
    [ModuleName("Governance")]
    [AuthorizeAs("User")]
    [PrimaryKey(nameof(UserId), nameof(UserGroupId))]
    public class UserGroupAssignment : TipsyBaboonModel
    {
        [ForeignKey("User")]
        public Guid UserId { get; set; }

        [ForeignKey("UserGroup")]
        public Guid UserGroupId { get; set; }
    }
}
