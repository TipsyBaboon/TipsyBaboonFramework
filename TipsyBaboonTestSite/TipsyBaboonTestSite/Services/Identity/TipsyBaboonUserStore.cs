using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityUser = TipsyBaboonTestSite.Services.Identity.Models.IdentityUser;
using IdentityRole = TipsyBaboonTestSite.Services.Identity.Models.IdentityRole;
using IdentityUserRole = TipsyBaboonTestSite.Services.Identity.Models.IdentityUserRole;
using IdentityUserClaim = TipsyBaboonTestSite.Services.Identity.Models.IdentityUserClaim;
using IdentityUserLogin = TipsyBaboonTestSite.Services.Identity.Models.IdentityUserLogin;
using IdentityUserToken = TipsyBaboonTestSite.Services.Identity.Models.IdentityUserToken;

namespace TipsyBaboonTestSite.Services.Identity
{
    public class TipsyBaboonUserStore :
        IUserStore<IdentityUser>,
        IUserPasswordStore<IdentityUser>,
        IUserEmailStore<IdentityUser>,
        IUserRoleStore<IdentityUser>,
        IUserClaimStore<IdentityUser>,
        IUserLoginStore<IdentityUser>,
        IUserLockoutStore<IdentityUser>,
        IUserTwoFactorStore<IdentityUser>,
        IUserSecurityStampStore<IdentityUser>,
        IUserPhoneNumberStore<IdentityUser>,
        IQueryableUserStore<IdentityUser>
    {
        private readonly GovernanceDbContext _context;

        public TipsyBaboonUserStore(GovernanceDbContext context)
        {
            _context = context;
        }

        public IQueryable<IdentityUser> Users => _context.Users;

        public void Dispose()
        {
        }

        #region IUserStore

        public async Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.CreatedOn = DateTime.UtcNow;
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.ModifiedOn = DateTime.UtcNow;
            _context.Users.Update(user);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Concurrency failure" });
            }
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (user == null) throw new ArgumentNullException(nameof(user));

            _context.Users.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(userId, out var id)) return null;
            return await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken);
        }

        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.Id.ToString());
        }

        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.UserName = userName ?? string.Empty;
            return Task.CompletedTask;
        }

        #endregion

        #region IUserPasswordStore

        public Task<string?> GetPasswordHashAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        }

        public Task SetPasswordHashAsync(IdentityUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        #endregion

        #region IUserEmailStore

        public async Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        }

        public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.EmailConfirmed);
        }

        public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.NormalizedEmail);
        }

        public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }

        public Task SetNormalizedEmailAsync(IdentityUser user, string? normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }

        #endregion

        #region IUserRoleStore

        public async Task AddToRoleAsync(IdentityUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedRoleName = roleName.ToUpperInvariant().Replace(" ", "");
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);

            if (role == null)
                throw new InvalidOperationException($"Role '{roleName}' does not exist.");

            var userRole = new IdentityUserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                OwnerId = user.Id,
                OwnershipLevel = 0,
                CreatedOn = DateTime.UtcNow
            };
            _context.UserRoles.Add(userRole);
        }

        public async Task<IList<string>> GetRolesAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roleNames = await _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .ToListAsync(cancellationToken);
            return roleNames!;
        }

        public async Task<IList<IdentityUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedRoleName = roleName.ToUpperInvariant().Replace(" ", "");
            return await _context.UserRoles
                .Join(_context.Roles.Where(r => r.NormalizedName == normalizedRoleName),
                    ur => ur.RoleId, r => r.Id, (ur, r) => ur.UserId)
                .Join(_context.Users, userId => userId, u => u.Id, (userId, u) => u)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsInRoleAsync(IdentityUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedRoleName = roleName.ToUpperInvariant().Replace(" ", "");
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);

            if (role == null) return false;

            return await _context.UserRoles
                .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);
        }

        public async Task RemoveFromRoleAsync(IdentityUser user, string roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedRoleName = roleName.ToUpperInvariant().Replace(" ", "");
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);

            if (role == null) return;

            var userRole = await _context.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);

            if (userRole != null)
                _context.UserRoles.Remove(userRole);
        }

        #endregion

        #region IUserClaimStore

        public async Task AddClaimsAsync(IdentityUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var claim in claims)
            {
                var userClaim = new IdentityUserClaim
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    OwnerId = user.Id,
                    OwnershipLevel = 0,
                    ClaimType = claim.Type,
                    ClaimValue = claim.Value,
                    CreatedOn = DateTime.UtcNow
                };
                _context.UserClaims.Add(userClaim);
            }
            await Task.CompletedTask;
        }

        public async Task<IList<Claim>> GetClaimsAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claims = await _context.UserClaims
                .Where(uc => uc.UserId == user.Id)
                .Select(uc => new Claim(uc.ClaimType ?? string.Empty, uc.ClaimValue ?? string.Empty))
                .ToListAsync(cancellationToken);
            return claims;
        }

        public async Task<IList<IdentityUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.UserClaims
                .Where(uc => uc.ClaimType == claim.Type && uc.ClaimValue == claim.Value)
                .Join(_context.Users, uc => uc.UserId, u => u.Id, (uc, u) => u)
                .ToListAsync(cancellationToken);
        }

        public async Task RemoveClaimsAsync(IdentityUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var claim in claims)
            {
                var userClaims = await _context.UserClaims
                    .Where(uc => uc.UserId == user.Id && uc.ClaimType == claim.Type && uc.ClaimValue == claim.Value)
                    .ToListAsync(cancellationToken);
                _context.UserClaims.RemoveRange(userClaims);
            }
        }

        public async Task ReplaceClaimAsync(IdentityUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userClaims = await _context.UserClaims
                .Where(uc => uc.UserId == user.Id && uc.ClaimType == claim.Type && uc.ClaimValue == claim.Value)
                .ToListAsync(cancellationToken);

            foreach (var uc in userClaims)
            {
                uc.ClaimType = newClaim.Type;
                uc.ClaimValue = newClaim.Value;
            }
        }

        #endregion

        #region IUserLoginStore

        public async Task AddLoginAsync(IdentityUser user, UserLoginInfo login, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userLogin = new IdentityUserLogin
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OwnerId = user.Id,
                OwnershipLevel = 0,
                LoginProvider = login.LoginProvider,
                ProviderKey = login.ProviderKey,
                ProviderDisplayName = login.ProviderDisplayName,
                CreatedOn = DateTime.UtcNow
            };
            _context.UserLogins.Add(userLogin);
            await Task.CompletedTask;
        }

        public async Task<IdentityUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.LoginProvider == loginProvider && ul.ProviderKey == providerKey, cancellationToken);

            if (userLogin == null) return null;

            return await _context.Users.FindAsync(new object[] { userLogin.UserId }, cancellationToken);
        }

        public async Task<IList<UserLoginInfo>> GetLoginsAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logins = await _context.UserLogins
                .Where(ul => ul.UserId == user.Id)
                .Select(ul => new UserLoginInfo(ul.LoginProvider ?? string.Empty, ul.ProviderKey ?? string.Empty, ul.ProviderDisplayName))
                .ToListAsync(cancellationToken);
            return logins;
        }

        public async Task RemoveLoginAsync(IdentityUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userLogin = await _context.UserLogins
                .FirstOrDefaultAsync(ul => ul.UserId == user.Id && ul.LoginProvider == loginProvider && ul.ProviderKey == providerKey, cancellationToken);

            if (userLogin != null)
                _context.UserLogins.Remove(userLogin);
        }

        #endregion

        #region IUserLockoutStore

        public Task<int> GetAccessFailedCountAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task<bool> GetLockoutEnabledAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.LockoutEnabled);
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.LockoutEnd);
        }

        public Task<int> IncrementAccessFailedCountAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.AccessFailedCount++;
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task ResetAccessFailedCountAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        public Task SetLockoutEnabledAsync(IdentityUser user, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.LockoutEnabled = enabled;
            return Task.CompletedTask;
        }

        public Task SetLockoutEndDateAsync(IdentityUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }

        #endregion

        #region IUserTwoFactorStore

        public Task<bool> GetTwoFactorEnabledAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.TwoFactorEnabled);
        }

        public Task SetTwoFactorEnabledAsync(IdentityUser user, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.TwoFactorEnabled = enabled;
            return Task.CompletedTask;
        }

        #endregion

        #region IUserSecurityStampStore

        public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.SecurityStamp);
        }

        public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }

        #endregion

        #region IUserPhoneNumberStore

        public Task<string?> GetPhoneNumberAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.PhoneNumber);
        }

        public Task<bool> GetPhoneNumberConfirmedAsync(IdentityUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(user.PhoneNumberConfirmed);
        }

        public Task SetPhoneNumberAsync(IdentityUser user, string? phoneNumber, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PhoneNumber = phoneNumber;
            return Task.CompletedTask;
        }

        public Task SetPhoneNumberConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            user.PhoneNumberConfirmed = confirmed;
            return Task.CompletedTask;
        }

        #endregion
    }
}
