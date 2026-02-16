using Microsoft.EntityFrameworkCore;
using TipsyBaboonTestSite.Services.Identity.Models;

namespace TipsyBaboonTestSite.Services.Identity
{
    public class GovernanceDbContext : DbContext
    {
        public GovernanceDbContext(DbContextOptions<GovernanceDbContext> options)
            : base(options)
        {
        }

        public DbSet<IdentityUser> Users { get; set; } = null!;
        public DbSet<IdentityRole> Roles { get; set; } = null!;
        public DbSet<IdentityUserRole> UserRoles { get; set; } = null!;
        public DbSet<IdentityUserClaim> UserClaims { get; set; } = null!;
        public DbSet<IdentityUserLogin> UserLogins { get; set; } = null!;
        public DbSet<IdentityUserToken> UserTokens { get; set; } = null!;
        public DbSet<IdentityRoleClaim> RoleClaims { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IdentityUser>(b =>
            {
                b.ToTable("User");
                b.HasKey(u => u.Id);
                b.HasIndex(u => u.NormalizedUserName).IsUnique();
                b.HasIndex(u => u.NormalizedEmail).IsUnique();
                b.Property(u => u.UserName).HasMaxLength(256);
                b.Property(u => u.NormalizedUserName).HasMaxLength(256);
                b.Property(u => u.Email).HasMaxLength(256);
                b.Property(u => u.NormalizedEmail).HasMaxLength(256);
            });

            modelBuilder.Entity<IdentityRole>(b =>
            {
                b.ToTable("Role");
                b.HasKey(r => r.Id);
                b.HasIndex(r => r.NormalizedName).IsUnique();
                b.Property(r => r.Name).HasMaxLength(256);
                b.Property(r => r.NormalizedName).HasMaxLength(256);
            });

            modelBuilder.Entity<IdentityUserRole>(b =>
            {
                b.ToTable("UserRole");
                b.HasKey(ur => new { ur.UserId, ur.RoleId });
                b.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IdentityUserClaim>(b =>
            {
                b.ToTable("UserClaim");
                b.HasKey(uc => uc.Id);
                b.HasOne(uc => uc.User)
                    .WithMany(u => u.Claims)
                    .HasForeignKey(uc => uc.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IdentityUserLogin>(b =>
            {
                b.ToTable("UserLogin");
                b.HasKey(ul => ul.Id);
                b.HasIndex(ul => new { ul.LoginProvider, ul.ProviderKey }).IsUnique();
                b.HasOne(ul => ul.User)
                    .WithMany(u => u.Logins)
                    .HasForeignKey(ul => ul.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IdentityUserToken>(b =>
            {
                b.ToTable("UserToken");
                b.HasKey(ut => ut.Id);
                b.HasIndex(ut => new { ut.UserId, ut.LoginProvider, ut.Name }).IsUnique();
                b.HasOne(ut => ut.User)
                    .WithMany(u => u.Tokens)
                    .HasForeignKey(ut => ut.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IdentityRoleClaim>(b =>
            {
                b.ToTable("RoleClaim");
                b.HasKey(rc => rc.Id);
                b.HasOne(rc => rc.Role)
                    .WithMany(r => r.Claims)
                    .HasForeignKey(rc => rc.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
