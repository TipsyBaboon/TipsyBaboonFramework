using TipsyBaboon.Core.Data;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboon.UI.Services
{
    /// <summary>
    /// Handles one-time governance bootstrapping, such as granting the first registered user
    /// the UserAdmin role so the system is not left without an administrator.
    /// </summary>
    public class UserGovernanceService
    {
        private readonly BaseModelStore _store;

        public UserGovernanceService(BaseModelStore store)
        {
            _store = store;
        }

        /// <summary>
        /// If <paramref name="user"/> is the only user in the system, assigns them the built-in UserAdmin role.
        /// Called during registration to ensure the first user can manage the governance area.
        /// </summary>
        /// <param name="user">The newly registered user to evaluate.</param>
        public async Task AssignFirstUserAdminIfNeededAsync(User user)
        {
            var allUsers = await _store.QueryTypedAsync<User>(new PagedRequest { PageSize = 2 });
            if (allUsers.TotalCount == 1)
            {
                var userAdminRole = new UserRole
                {
                    UserId = user.Id,
                    RoleId = Role.UserAdminRoleId,
                    CreatedOn = DateTime.UtcNow
                };
                await _store.InsertTypedAsync<UserRole>(userAdminRole);
            }
        }
    }
}
