using Microsoft.AspNetCore.Identity;
using IdentityUser = TipsyBaboonTestSite.Services.Identity.Models.IdentityUser;
using TipsyBaboon.Core.Models.Governance;

namespace TipsyBaboonTestSite.Services.Identity
{
    public class IdentityAuthenticationService : IAuthenticationService, TipsyBaboon.Core.Interfaces.IAuthenticationService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public IdentityAuthenticationService(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return null;
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            return result.Succeeded ? ConvertToFrameworkUser(user) : null;
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            return user != null ? ConvertToFrameworkUser(user) : null;
        }

        public async Task<User?> FindByIdAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            return user != null ? ConvertToFrameworkUser(user) : null;
        }

        private User ConvertToFrameworkUser(IdentityUser identityUser)
        {
            return new User
            {
                Id = identityUser.Id,
                OwnerId = identityUser.OwnerId,
                IsInvariant = identityUser.IsInvariant,
                CreatedOn = identityUser.CreatedOn,
                ModifiedOn = identityUser.ModifiedOn,
                UserName = identityUser.UserName,
                NormalizedUserName = identityUser.NormalizedUserName,
                Email = identityUser.Email,
                NormalizedEmail = identityUser.NormalizedEmail,
                EmailConfirmed = identityUser.EmailConfirmed,
                PasswordHash = identityUser.PasswordHash,
                SecurityStamp = identityUser.SecurityStamp,
                ConcurrencyStamp = identityUser.ConcurrencyStamp,
                PhoneNumber = identityUser.PhoneNumber,
                PhoneNumberConfirmed = identityUser.PhoneNumberConfirmed,
                TwoFactorEnabled = identityUser.TwoFactorEnabled,
                LockoutEnd = identityUser.LockoutEnd,
                LockoutEnabled = identityUser.LockoutEnabled,
                AccessFailedCount = identityUser.AccessFailedCount
            };
        }

        public async Task<User> CreateUserAsync(string email, string? displayName = null, string? password = null)
        {
            var user = new IdentityUser
            {
                Id = Guid.NewGuid(),
                OwnerId = null,
                OwnershipLevel = 0,
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedOn = DateTime.UtcNow
            };

            IdentityResult result;
            if (!string.IsNullOrEmpty(password))
            {
                result = await _userManager.CreateAsync(user, password);
            }
            else
            {
                result = await _userManager.CreateAsync(user);
            }

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create user: {errors}");
            }

            return ConvertToFrameworkUser(user);
        }

        public async Task<UserLogin?> FindUserLoginAsync(string loginProvider, string providerKey)
        {
            var user = await _userManager.FindByLoginAsync(loginProvider, providerKey);
            if (user == null)
            {
                return null;
            }

            var logins = await _userManager.GetLoginsAsync(user);
            var login = logins.FirstOrDefault(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey);

            if (login == null)
            {
                return null;
            }

            return new UserLogin
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OwnerId = user.Id,
                OwnershipLevel = 0,
                CreatedOn = DateTime.UtcNow,
                LoginProvider = login.LoginProvider,
                ProviderKey = login.ProviderKey,
                ProviderDisplayName = login.ProviderDisplayName
            };
        }

        public async Task AddUserLoginAsync(Guid userId, string loginProvider, string providerKey, string? providerDisplayName = null)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new InvalidOperationException($"User {userId} not found.");
            }

            var loginInfo = new UserLoginInfo(loginProvider, providerKey, providerDisplayName);
            var result = await _userManager.AddLoginAsync(user, loginInfo);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to add login: {errors}");
            }
        }

        public async Task<bool> ValidatePasswordAsync(string password, string hashedPassword)
        {
            var verificationResult = _userManager.PasswordHasher.VerifyHashedPassword(null!, hashedPassword, password);
            return await Task.FromResult(verificationResult != PasswordVerificationResult.Failed);
        }

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            return result.Succeeded;
        }

        public async Task<string> GenerateAuthenticatorKeyAsync()
        {
            var user = new IdentityUser { Id = Guid.NewGuid(), OwnerId = null, CreatedOn = DateTime.UtcNow };
            await _userManager.ResetAuthenticatorKeyAsync(user);
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            return key ?? string.Empty;
        }

        public async Task<bool> EnableTwoFactorAsync(Guid userId, string authenticatorKey, string verificationCode)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                verificationCode);

            if (!isValid)
            {
                return false;
            }

            var result = await _userManager.SetTwoFactorEnabledAsync(user, true);
            return result.Succeeded;
        }

        public async Task<bool> DisableTwoFactorAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (result.Succeeded)
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
            }
            return result.Succeeded;
        }

        public async Task<bool> VerifyTwoFactorCodeAsync(string authenticatorKey, string code)
        {
            var user = new IdentityUser
            {
                Id = Guid.NewGuid(),
                OwnerId = null,
                CreatedOn = DateTime.UtcNow,
                SecurityStamp = authenticatorKey
            };

            return await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                code);
        }

        public async Task UpdateDisplayNameAsync(Guid userId, string? displayName)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException($"User '{userId}' not found");

            await _userManager.UpdateAsync(user);
        }
    }
}
