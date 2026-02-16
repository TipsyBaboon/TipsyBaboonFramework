using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TipsyBaboon.Core.Attributes;
using TipsyBaboon.Core.FrameworkBase;
using TipsyBaboon.Core.Interfaces;

namespace TipsyBaboon.Core.Models.Governance
{
    /// <summary>
    /// Represents a user in the TipsyBaboon system.
    /// Provides identity, authentication, and authorization functionality.
    /// Integrates with ASP.NET Core Identity for authentication and stores internal user records.
    /// </summary>
    [Privilege("Change Password")]
    [Privilege("Remove Lockout")]
    [Privilege("Impersonate User")]
    [UIEntity("User", ModuleName = "Governance", GroupName = "Governance", Order = 10, Icon = "bi bi-person-fill", HasOwnership = false)]
    [UIGroup("Identity", Title = "Identity", Order = 10)]
    [UIGroup("Contact", Title = "Contact Information", Order = 20)]
    [UIGroup("Security", Title = "Security Settings", Order = 30, Collapsible = true)]
    [UIGroup("Relationships", Title = "Relationships", Order = 40, Collapsible = true, InitiallyCollapsed = true)]
    [UIGroup("Metadata", Title = "Metadata", Order = 50, Collapsible = true, InitiallyCollapsed = true)]
    [Index(nameof(UserName), IsUnique = true)]
    [Index(nameof(NormalizedUserName), IsUnique = true)]
    [Index(nameof(NormalizedEmail), IsUnique = true)]
    public class User : TipsyBaboonModel, IModelWithSaveAction, IModelWithGetAction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [RecordName]
        [Required]
        [StringLength(256)]
        [UIDisplay(Name = "Username", GroupName = "Identity", Order = 10, ColumnSize = 6, ShowInList = true)]
        public string UserName { get; set; } = string.Empty;

        [UIReadOnly]
        [StringLength(256)]
        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public string? NormalizedUserName { get; set; }

        [UIDisplay(Name = "Display Name", GroupName = "Identity", Order = 20, ColumnSize = 6, ShowInList = true)]
        public string? DisplayName { get; set; }

        [EmailAddress]
        [FieldControl("email")]
        [StringLength(256)]
        [UIDisplay(Name = "Email", GroupName = "Contact", Order = 10, ColumnSize = 6, ShowInList = true)]
        public string? Email { get; set; }

        [UIReadOnly]
        [StringLength(256)]
        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public string? NormalizedEmail { get; set; }

        [UIReadOnly]
        [UIDisplay(Name = "Email Confirmed", GroupName = "Contact", Order = 20, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        public bool EmailConfirmed { get; set; }

        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public string? PasswordHash { get; set; }

        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public string? SecurityStamp { get; set; }

        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public string? ConcurrencyStamp { get; set; }

        [Phone]
        [FieldControl("tel")]
        [UIDisplay(Name = "Phone Number", GroupName = "Contact", Order = 30, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        public string? PhoneNumber { get; set; }

        [UIReadOnly]
        [UIDisplay(Name = "Phone Confirmed", GroupName = "Contact", Order = 35, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        public bool PhoneNumberConfirmed { get; set; }

        [UIReadOnly]
        [UIDisplay(Name = "Two-Factor Enabled", GroupName = "Security", Order = 30, ColumnSize = 4, ShowInList = false, ShowInDetail = true)]
        public bool TwoFactorEnabled { get; set; }

        [UIReadOnly]
        [UIDisplay(Name = "Lockout Enabled", GroupName = "Security", Order = 40, ColumnSize = 4, ShowInList = false, ShowInDetail = true)]
        public bool LockoutEnabled { get; set; }

        [UIReadOnly]
        [UIDisplay(Name = "Lockout End", GroupName = "Security", Order = 45, ColumnSize = 4, ShowInList = false, ShowInDetail = true)]
        public DateTimeOffset? LockoutEnd { get; set; }

        [UIReadOnly]
        [UIDisplay(ShowInList = false, ShowInDetail = false)]
        public int AccessFailedCount { get; set; }

        [UIDisplay(Name = "Active", GroupName = "Metadata", Order = 10, ColumnSize = 3, ShowInList = true, ShowInDetail = true)]
        public bool IsActive { get; set; } = true;

        [UIReadOnly]
        [UIDisplay(Name = "Created Date", GroupName = "Metadata", Order = 20, ColumnSize = 3, ShowInList = false, ShowInDetail = true)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [UIReadOnly]
        [UIDisplay(Name = "Last Login", GroupName = "Metadata", Order = 30, ColumnSize = 3, ShowInList = false, ShowInDetail = true)]
        public DateTime? LastLoginDate { get; set; }

        [UIDisplay(Name = "Can Promote to Admin", GroupName = "Security", Order = 10, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        [RequirePrivilege("Promote Admin")]
        public bool CanPromoteToAdmin { get; set; }
        [UIDisplay(Name = "Can Revoke Admin", GroupName = "Security", Order = 15, ColumnSize = 6, ShowInList = false, ShowInDetail = true)]
        [RequirePrivilege("Revoke Admin")]
        public bool CanRevokeAdmin { get; set; }

        [NotMapped]
        [FieldControl("password")]
        [RequirePrivilege("Change Password")]
        [UIDisplay(Name = "New Password", GroupName = "Security", Order = 20, ColumnSize = 6, ShowInList = false)]
        public string NewPassword { get; set; } = string.Empty;

        [NotMapped]
        [FieldControl("password")]
        [RequirePrivilege("Change Password")]
        [UIDisplay(Name = "Confirm New Password", GroupName = "Security", Order = 21, ColumnSize = 6, ShowInList = false)]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public List<UserLogin> Logins { get; set; } = new List<UserLogin>();

        [UIDisplay(Name = "Roles", GroupName = "Relationships", Order = 20, ShowInList = false, ShowInDetail = false)]
        public List<UserRole> UserRoles { get; set; } = new List<UserRole>();

        [UIDisplay(Name = "Role Assignments", GroupName = "Relationships", Order = 21, ShowInList = false, ShowInDetail = true)]
        public List<UserRoleAssignment> UserRoleAssignments { get; set; } = new List<UserRoleAssignment>();

        [UIDisplay(Name = "Group Assignments", GroupName = "Relationships", Order = 22, ShowInList = false, ShowInDetail = true)]
        public List<UserUserGroupAssignment> GroupUserUserGroupAssignments { get; set; } = new List<UserUserGroupAssignment>();

        public List<UserClaim> Claims { get; set; } = new List<UserClaim>();

        public List<UserToken> Tokens { get; set; } = new List<UserToken>();

        public List<string> ExternalIds { get; set; } = new List<string>();

        public async Task<SaveResponseType> BeforeChangeAsync(ActionType action, IServiceProvider serviceProvider, string? committingUser = null, CancellationToken cancellationToken = default)
        {
            if (action == ActionType.Create || action == ActionType.Update)
            {
                NormalizedUserName = UserName?.ToUpperInvariant();
                NormalizedEmail = Email?.ToUpperInvariant();
                ConcurrencyStamp = Guid.NewGuid().ToString();

                // Handle password updates
                if (!string.IsNullOrEmpty(NewPassword))
                {
                    if (NewPassword != ConfirmNewPassword)
                    {
                        throw new InvalidOperationException("Password and confirmation do not match");
                    }

                    var passwordHasher = serviceProvider.GetService<IPasswordHasher>();
                    if (passwordHasher == null)
                    {
                        throw new InvalidOperationException("Password hasher service not available");
                    }

                    PasswordHash = passwordHasher.HashPassword(NewPassword);
                    SecurityStamp = Guid.NewGuid().ToString();
                    
                    // Clear password fields after hashing
                    NewPassword = string.Empty;
                    ConfirmNewPassword = string.Empty;
                }
                
                if (action == ActionType.Create)
                {
                    // Set defaults at creation
                    if (string.IsNullOrEmpty(SecurityStamp))
                        SecurityStamp = Guid.NewGuid().ToString();
                        
                    if (CreatedDate == default)
                        CreatedDate = DateTime.UtcNow;

                    // Set default password if none provided and NewPassword wasn't set
                    if (string.IsNullOrEmpty(PasswordHash))
                    {
                        var passwordHasher = serviceProvider.GetService<IPasswordHasher>();
                        if (passwordHasher != null)
                        {
                            PasswordHash = passwordHasher.HashPassword("TestPassword");
                        }
                    }
                }

                return SaveResponseType.Continue;
            }

            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

            IsActive = false;

            if (committingUser == null)
                throw new InvalidOperationException("committingUser is required for Delete operations on User");

            await repository.UpdateTypedAsync<User>(this, cancellationToken);

            return SaveResponseType.ChangeApplied;
        }

        public async Task BeforeGet()
        {
            // Blank out sensitive fields before sending to browser
            PasswordHash = null;
            SecurityStamp = null;
            ConcurrencyStamp = null;

            await Task.CompletedTask;
        }

        [UIAction("Reset Password", Order = 50, Privilege = "Change Password", Icon = "bi bi-key")]
        public async Task<ApiResponse> ResetPassword(IServiceProvider serviceProvider, string committingUser, CancellationToken cancellationToken)
        {
            var passwordHasher = serviceProvider.GetService<IPasswordHasher>();
            if (passwordHasher == null)
                return ApiResponse.Error("Password hasher service not available");
            PasswordHash = passwordHasher.HashPassword("TestPassword");
            SecurityStamp = Guid.NewGuid().ToString();
            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");
            await repository.UpdateTypedAsync<User>(this, cancellationToken);
            return ApiResponse.Ok(message: "Password reset to default value");
        }

        [UIAction("Remove Lockout", Order = 60, Privilege = "Remove Lockout", Icon = "bi bi-unlock")]
        public async Task<ApiResponse> RemoveLockout(IServiceProvider serviceProvider, string committingUser, CancellationToken cancellationToken)
        {
            LockoutEnd = null;
            AccessFailedCount = 0;
            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");
            await repository.UpdateTypedAsync<User>(this, cancellationToken);
            return ApiResponse.Ok(message: "Lockout removed");
        }

        [UIAction("Impersonate User", Order = 70, Privilege = "Impersonate User", Icon = "bi bi-person-badge")]
        public async Task<ApiResponse> Impersonate(IServiceProvider serviceProvider, string committingUser, CancellationToken cancellationToken)
        {
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>()
                ?? throw new InvalidOperationException("IHttpContextAccessor not registered in DI container");
            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is not available");

            var currentUserIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdClaim) || !Guid.TryParse(currentUserIdClaim, out var currentUserId))
                return ApiResponse.Error("Unable to identify current user");

            if (currentUserId == Id)
                return ApiResponse.Error("Cannot impersonate yourself");

            var expiresOn = DateTime.UtcNow.AddHours(2);
            var session = new ImpersonationSession
            {
                ImpersonatingUserId = currentUserId,
                ImpersonatedUserId = Id,
                ImpersonationKey = Guid.NewGuid(),
                ExpiresOn = expiresOn
            };

            await repository.InsertTypedAsync<ImpersonationSession>(session, cancellationToken);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Id.ToString()),
                new Claim(ClaimTypes.Name, UserName ?? Email ?? ""),
                new Claim(ClaimTypes.Email, Email ?? ""),
                new Claim("ImpersonationKey", session.ImpersonationKey.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return ApiResponse.Ok(message: $"Now impersonating {DisplayName ?? UserName}").WithRefresh();
        }

        [UIAction("End Impersonation", Order = 80, Privilege = "Impersonate User", Visible = false)]
        public static async Task<ApiResponse> EndImpersonation(IServiceProvider serviceProvider, string committingUser, CancellationToken cancellationToken)
        {
            var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>()
                ?? throw new InvalidOperationException("IHttpContextAccessor not registered in DI container");
            var repository = serviceProvider.GetService<BaseModelStore>()
                ?? throw new InvalidOperationException("BaseModelStore not registered in DI container");

            var httpContext = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is not available");

            var currentUserIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserIdClaim) || !Guid.TryParse(currentUserIdClaim, out var currentUserId))
                return ApiResponse.Error("Unable to identify current user");

            var impersonationKeyClaim = httpContext.User.FindFirst("ImpersonationKey")?.Value;
            if (string.IsNullOrEmpty(impersonationKeyClaim) || !Guid.TryParse(impersonationKeyClaim, out var impersonationKey))
                return ApiResponse.Error("Not currently impersonating a user");

            var sessions = await repository.QueryTypedAsync<ImpersonationSession>(
                s => s.ImpersonationKey == impersonationKey, cancellationToken);
            var session = sessions.FirstOrDefault();

            if (session == null)
                return ApiResponse.Error("Impersonation session not found");

            if (session.ImpersonatedUserId != currentUserId)
                return ApiResponse.Error("Current user does not match impersonation session");

            if (session.ExpiresOn < DateTime.UtcNow)
            {
                await repository.DeleteTypedAsync<ImpersonationSession>(session.Id, cancellationToken);
                return ApiResponse.Error("Impersonation session has expired");
            }

            var originalUser = await repository.GetTypedByIdAsync<User>(session.ImpersonatingUserId, cancellationToken);
            if (originalUser == null)
                return ApiResponse.Error("Original user not found");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, originalUser.Id.ToString()),
                new Claim(ClaimTypes.Name, originalUser.UserName ?? originalUser.Email ?? ""),
                new Claim(ClaimTypes.Email, originalUser.Email ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            await repository.DeleteTypedAsync<ImpersonationSession>(session.Id, cancellationToken);

            return ApiResponse.Ok(message: $"Returned to {originalUser.DisplayName ?? originalUser.UserName}").WithRefresh();
        }
    }
}
