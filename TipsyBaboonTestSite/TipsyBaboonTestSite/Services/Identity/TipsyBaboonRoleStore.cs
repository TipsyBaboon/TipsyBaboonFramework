using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityRole = TipsyBaboonTestSite.Services.Identity.Models.IdentityRole;
using IdentityRoleClaim = TipsyBaboonTestSite.Services.Identity.Models.IdentityRoleClaim;

namespace TipsyBaboonTestSite.Services.Identity
{
    public class TipsyBaboonRoleStore :
        IRoleStore<IdentityRole>,
        IRoleClaimStore<IdentityRole>,
        IQueryableRoleStore<IdentityRole>
    {
        private readonly GovernanceDbContext _context;

        public TipsyBaboonRoleStore(GovernanceDbContext context)
        {
            _context = context;
        }

        public IQueryable<IdentityRole> Roles => _context.Roles;

        public void Dispose()
        {
        }

        #region IRoleStore

        public async Task<IdentityResult> CreateAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (role == null) throw new ArgumentNullException(nameof(role));

            role.CreatedOn = DateTime.UtcNow;
            _context.Roles.Add(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (role == null) throw new ArgumentNullException(nameof(role));

            role.ModifiedOn = DateTime.UtcNow;
            _context.Roles.Update(role);
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

        public async Task<IdentityResult> DeleteAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (role == null) throw new ArgumentNullException(nameof(role));

            if (role.IsInvariant)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Cannot delete invariant roles." });
            }

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        public async Task<IdentityRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParse(roleId, out var id)) return null;
            return await _context.Roles.FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IdentityRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _context.Roles
                .FirstOrDefaultAsync(r => r.NormalizedName == normalizedRoleName, cancellationToken);
        }

        public Task<string?> GetNormalizedRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(role.NormalizedName);
        }

        public Task<string> GetRoleIdAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(role.Id.ToString());
        }

        public Task<string?> GetRoleNameAsync(IdentityRole role, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(role.Name);
        }

        public Task SetNormalizedRoleNameAsync(IdentityRole role, string? normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetRoleNameAsync(IdentityRole role, string? roleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            role.Name = roleName ?? string.Empty;
            return Task.CompletedTask;
        }

        #endregion

        #region IRoleClaimStore

        public async Task AddClaimAsync(IdentityRole role, Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roleClaim = new IdentityRoleClaim
            {
                Id = Guid.NewGuid(),
                RoleId = role.Id,
                OwnerId = role.Id,
                OwnershipLevel = 0,
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
                CreatedOn = DateTime.UtcNow
            };
            _context.RoleClaims.Add(roleClaim);
            await Task.CompletedTask;
        }

        public async Task<IList<Claim>> GetClaimsAsync(IdentityRole role, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claims = await _context.RoleClaims
                .Where(rc => rc.RoleId == role.Id)
                .Select(rc => new Claim(rc.ClaimType ?? string.Empty, rc.ClaimValue ?? string.Empty))
                .ToListAsync(cancellationToken);
            return claims;
        }

        public async Task RemoveClaimAsync(IdentityRole role, Claim claim, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roleClaims = await _context.RoleClaims
                .Where(rc => rc.RoleId == role.Id && rc.ClaimType == claim.Type && rc.ClaimValue == claim.Value)
                .ToListAsync(cancellationToken);
            _context.RoleClaims.RemoveRange(roleClaims);
        }

        #endregion
    }
}
