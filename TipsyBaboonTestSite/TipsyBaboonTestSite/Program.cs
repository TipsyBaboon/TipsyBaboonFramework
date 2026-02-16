using System.Threading.Tasks;
using TipsyBaboonTestSite.Services;
using TipsyBaboonTestSite.Services.Identity;
using TipsyBaboon.Core.Startup;
using TipsyBaboon.SqlServer.Extensions;
using TipsyBaboon.UI.Extensions;
using TipsyBaboon.UI.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TipsyBaboon.Core.Models.Governance;
using IdentityUser = TipsyBaboonTestSite.Services.Identity.Models.IdentityUser;
using IdentityRole = TipsyBaboonTestSite.Services.Identity.Models.IdentityRole;

namespace TipsyBaboonTestSite
{
    public class Program
    {
        public static readonly Guid MatthewUserId = Guid.Parse("83B36BD5-B123-4109-80F2-8153768296FF");
        public static readonly Guid VeritoUserId = Guid.Parse("9A73E943-AD98-4611-B103-E920EA98FC78");

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var connStr = builder.Configuration.GetConnectionString("TipsyBaboonCore") 
                ?? throw new InvalidOperationException("Connection string 'TipsyBaboonCore' not configured");

            builder.Services.AddTipsyBaboon(config =>
            {
                config.UseSqlServer();
                
                config.AddModule("Governance", connStr, "Governance module (permissions, roles, etc.)");
                
                config.AddModule("TipsyBaboon.UI", connStr, "UI framework module (grid settings, layout preferences)");

                // Northwind demo module
                var nwConnStr = builder.Configuration.GetConnectionString("Northwind");
                if (!string.IsNullOrEmpty(nwConnStr))
                {
                    config.AddModule("Northwind", nwConnStr, "Northwind Test Environment")
                        .AddSeedPermission("Basic Rights", "Northwind", "Supplier",
                            usePages: true, canCreate: true,
                            readLevel: TipsyBaboon.Core.PermissionLevel.All,
                            updateLevel: TipsyBaboon.Core.PermissionLevel.Own,
                            deleteLevel: TipsyBaboon.Core.PermissionLevel.Own);
                }
                
                //// AdventureWorks demo module
                //var awConnStr = builder.Configuration.GetConnectionString("AdventureWorks");
                //if (!string.IsNullOrEmpty(awConnStr))
                //{
                //    config.AddModule("AdventureWorks", awConnStr, "AdventureWorks Sample Database")
                //        .AddSeedPermission("Basic Rights", "AdventureWorks", "Customer",
                //            usePages: true, canCreate: true,
                //            readLevel: TipsyBaboon.Core.PermissionLevel.All,
                //            updateLevel: TipsyBaboon.Core.PermissionLevel.All,
                //            deleteLevel: TipsyBaboon.Core.PermissionLevel.All);
                //}
                
            });

            builder.Services.AddTipsyBaboonUI();
            
            TipsyBaboon.UI.Rendering.FieldRenderer.Register(new TipsyBaboon.UI.Rendering.FieldRegistrationParams<Models.Northwind.ContactInfo>
            {
                PartialPath = "~/Pages/Fields/_ContactInfoField.cshtml",
                ControlName = "ContactInfo",
                RequiredScripts = new List<string> { "/js/contact-info.js" }
            });
            
            builder.Services.AddTipsyBaboonGenericPages(routePrefix: "Tipsy");
            builder.Services.AddTipsyBaboonGenericApis(routePrefix: "api/Baboon");

            builder.Services.AddDbContext<GovernanceDbContext>(options =>
                options.UseSqlServer(connStr));

            builder.Services.AddDbContext<Models.Northwind.NorthwindContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("Northwind")));

            builder.Services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddUserStore<TipsyBaboonUserStore>()
            .AddRoleStore<TipsyBaboonRoleStore>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

            //var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
            //var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            //var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
            //var microsoftSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];

            var authBuilder = builder.Services
                .AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Identity/Account/Login";
                    options.LogoutPath = "/Identity/Account/Logout";
                    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                    options.SlidingExpiration = true;
                });

            //if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleSecret))
            //{
            //    authBuilder.AddGoogle(options =>
            //    {
            //        options.ClientId = googleClientId;
            //        options.ClientSecret = googleSecret;
            //    });
            //}

            //if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftSecret))
            //{
            //    authBuilder.AddMicrosoftAccount(options =>
            //    {
            //        options.ClientId = microsoftClientId;
            //        options.ClientSecret = microsoftSecret;
            //    });
            //}

            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            builder.Services.AddRazorPages()
                .AddTipsyBaboonIdentityPages();
            
            builder.Services.AddControllers();

            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-XSRF-TOKEN";
                options.Cookie.Name = "XSRF-TOKEN";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
            
            builder.Services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
            builder.Services.AddScoped<TipsyBaboon.Core.Interfaces.IAuthenticationService>(sp => sp.GetRequiredService<IAuthenticationService>());
            builder.Services.AddScoped<AppConfigurationManager>();
            

            var app = builder.Build();

            TipsyBaboon.Core.Services.ServiceLocator.SetProvider(app.Services);

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseTipsyBaboonEmbeddedResources();

            app.UseRouting();

            app.UseSession();
            app.UseAuthentication();
            app.UseStaleAuthenticationCheck();
            app.UseImpersonationKeepalive();
            app.UseAuthorization();
            
            
            
            app.MapRazorPages()
               .WithStaticAssets();
            
            app.MapControllers();

            await app.RunAsync();
        }
    }
}
