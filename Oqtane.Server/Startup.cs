using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Oqtane.Extensions;
using Oqtane.Infrastructure;
using Oqtane.Infrastructure.Startup;
using Oqtane.Repository;
using Oqtane.Security;
using Oqtane.Services;
using Oqtane.Shared;

namespace Oqtane
{
    public class Startup
    {
        private string _webRoot;
        private Runtime _runtime;
        private bool _useSwagger;
        private IWebHostEnvironment _env;
        private string[] _supportedCultures;

        public IConfigurationRoot Configuration { get; }

        public Startup(IWebHostEnvironment env, ILocalizationManager localizationManager)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            Configuration = builder.Build();

            _supportedCultures = localizationManager.GetSupportedCultures();

            _runtime = (Configuration.GetSection("Runtime").Value == "WebAssembly") ? Runtime.WebAssembly : Runtime.Server;

            //add possibility to switch off swagger on production.
            _useSwagger = Configuration.GetSection("UseSwagger").Value != "false";

            _webRoot = env.WebRootPath;
            AppDomain.CurrentDomain.SetData("DataDirectory", Path.Combine(env.ContentRootPath, "Data"));

            _env = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var oqtaneServicesObjects = ResolveOqtaneServicesTypes();

            foreach (var oqtaneServicesObject in oqtaneServicesObjects)
            {
                if (oqtaneServicesObject.GetType().GetMethod(nameof(IOqtaneServices.AddLocalization)).IsOverriden())
                {
                    oqtaneServicesObject.AddLocalization(services);
                    break;
                }
            }

            services.AddServerSideBlazor().AddCircuitOptions(options =>
            {
                if (_env.IsDevelopment())
                {
                    options.DetailedErrors = true;
                }
            });

            // setup HttpClient for server side in a client side compatible fashion ( with auth cookie )
            if (!services.Any(x => x.ServiceType == typeof(HttpClient)))
            {
                services.AddScoped(s =>
                {
                    // creating the URI helper needs to wait until the JS Runtime is initialized, so defer it.
                    var navigationManager = s.GetRequiredService<NavigationManager>();
                    var httpContextAccessor = s.GetRequiredService<IHttpContextAccessor>();
                    var authToken = httpContextAccessor.HttpContext.Request.Cookies[".AspNetCore.Identity.Application"];
                    var client = new HttpClient(new HttpClientHandler {UseCookies = false});
                    if (authToken != null)
                    {
                        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Identity.Application=" + authToken);
                    }
                    client.BaseAddress = new Uri(navigationManager.Uri);
                    return client;
                });
            }

            // register custom authorization policies
            services.AddAuthorizationCore(options =>
            {
                options.AddPolicy(PolicyNames.ViewPage, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Page, PermissionNames.View)));
                options.AddPolicy(PolicyNames.EditPage, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Page, PermissionNames.Edit)));
                options.AddPolicy(PolicyNames.ViewModule, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Module, PermissionNames.View)));
                options.AddPolicy(PolicyNames.EditModule, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Module, PermissionNames.Edit)));
                options.AddPolicy(PolicyNames.ViewFolder, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Folder, PermissionNames.View)));
                options.AddPolicy(PolicyNames.EditFolder, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Folder, PermissionNames.Edit)));
                options.AddPolicy(PolicyNames.ListFolder, policy => policy.Requirements.Add(new PermissionRequirement(EntityNames.Folder, PermissionNames.Browse)));
            });

            // register scoped core services
            services.AddScoped<SiteState>();
            services.AddScoped<IAuthorizationHandler, PermissionHandler>();
            services.AddScoped<IInstallationService, InstallationService>();
            services.AddScoped<IModuleDefinitionService, ModuleDefinitionService>();
            services.AddScoped<IThemeService, ThemeService>();
            services.AddScoped<IAliasService, AliasService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ISiteService, SiteService>();
            services.AddScoped<IPageService, PageService>();
            services.AddScoped<IModuleService, ModuleService>();
            services.AddScoped<IPageModuleService, PageModuleService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IUserRoleService, UserRoleService>();
            services.AddScoped<ISettingService, SettingService>();
            services.AddScoped<IPackageService, PackageService>();
            services.AddScoped<ILogService, LogService>();
            services.AddScoped<IJobService, JobService>();
            services.AddScoped<IJobLogService, JobLogService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IFolderService, FolderService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<ISiteTemplateService, SiteTemplateService>();
            services.AddScoped<ISqlService, SqlService>();
            services.AddScoped<ISystemService, SystemService>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            var connectionString = Configuration.GetConnectionString("DefaultConnection")
                    .Replace("|DataDirectory|", AppContext.GetData("DataDirectory")?.ToString());
            foreach (var oqtaneServicesObject in oqtaneServicesObjects)
            {
                if (oqtaneServicesObject.GetType().GetMethod(nameof(IOqtaneServices.AddDatabase)).IsOverriden())
                {
                    oqtaneServicesObject.AddDatabase(services, connectionString);
                    break;
                }
            }

            foreach (var oqtaneServicesObject in oqtaneServicesObjects)
            {
                if (oqtaneServicesObject.GetType().GetMethod(nameof(IOqtaneServices.AddIdentity)).IsOverriden())
                {
                    oqtaneServicesObject.AddIdentity(services);
                    break;
                }
            }

            services.Configure<IdentityOptions>(options =>
            {
                // Password settings
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.RequireUniqueEmail = false;
            });

            foreach (var oqtaneServicesObject in oqtaneServicesObjects)
            {
                if (oqtaneServicesObject.GetType().GetMethod(nameof(IOqtaneServices.AddAuthentication)).IsOverriden())
                {
                    oqtaneServicesObject.AddAuthentication(services);
                    break;
                }
            }

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
            });

            // register custom claims principal factory for role claims
            services.AddTransient<IUserClaimsPrincipalFactory<IdentityUser>, ClaimsPrincipalFactory<IdentityUser>>();

            // register singleton scoped core services
            services.AddSingleton(Configuration);
            services.AddSingleton<IInstallationManager, InstallationManager>();
            services.AddSingleton<ISyncManager, SyncManager>();
            services.AddSingleton<IDatabaseManager, DatabaseManager>();

            // install any modules or themes ( this needs to occur BEFORE the assemblies are loaded into the app domain )
            InstallationManager.InstallPackages("Modules,Themes", _webRoot);

            // register transient scoped core services
            services.AddTransient<IModuleDefinitionRepository, ModuleDefinitionRepository>();
            services.AddTransient<IThemeRepository, ThemeRepository>();
            services.AddTransient<IUserPermissions, UserPermissions>();
            services.AddTransient<ITenantResolver, TenantResolver>();
            services.AddTransient<IAliasRepository, AliasRepository>();
            services.AddTransient<ITenantRepository, TenantRepository>();
            services.AddTransient<ISiteRepository, SiteRepository>();
            services.AddTransient<IPageRepository, PageRepository>();
            services.AddTransient<IModuleRepository, ModuleRepository>();
            services.AddTransient<IPageModuleRepository, PageModuleRepository>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IProfileRepository, ProfileRepository>();
            services.AddTransient<IRoleRepository, RoleRepository>();
            services.AddTransient<IUserRoleRepository, UserRoleRepository>();
            services.AddTransient<IPermissionRepository, PermissionRepository>();
            services.AddTransient<ISettingRepository, SettingRepository>();
            services.AddTransient<ILogRepository, LogRepository>();
            services.AddTransient<ILogManager, LogManager>();
            services.AddTransient<ILocalizationManager, LocalizationManager>();
            services.AddTransient<IJobRepository, JobRepository>();
            services.AddTransient<IJobLogRepository, JobLogRepository>();
            services.AddTransient<INotificationRepository, NotificationRepository>();
            services.AddTransient<IFolderRepository, FolderRepository>();
            services.AddTransient<IFileRepository, FileRepository>();
            services.AddTransient<ISiteTemplateRepository, SiteTemplateRepository>();
            services.AddTransient<ISqlRepository, SqlRepository>();
            services.AddTransient<IUpgradeManager, UpgradeManager>();

            // load the external assemblies into the app domain, install services 
            services.AddOqtane(_runtime, _supportedCultures);

            services.AddMvc()
                .AddNewtonsoftJson()
                .AddOqtaneApplicationParts() // register any Controllers from custom modules
                .ConfigureOqtaneMvc(); // any additional configuration from IStart classes.

            if (_useSwagger)
            {
                services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo {Title = "Oqtane", Version = "v1"}); });
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ServiceActivator.Configure(app.ApplicationServices);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            // to allow install middleware it should be moved up
            app.ConfigureOqtaneAssemblies(env);

            // Allow oqtane localization middleware
            app.UseOqtaneLocalization();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseBlazorFrameworkFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            if (_useSwagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Oqtane V1"); });
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapControllers();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

        private static IEnumerable<IOqtaneServices> ResolveOqtaneServicesTypes()
        {
            var oqtaneServicesAttributes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetCustomAttributes(typeof(OqtaneServicesAttribute), false).Length == 1)
                .Select(a => a.GetCustomAttributes(typeof(OqtaneServicesAttribute), false).First())
                .Cast<OqtaneServicesAttribute>();
            var oqtaneServicesTypes = oqtaneServicesAttributes.Select(a => a.ServicesType).ToList();

            // Adds default Oqtane services implementation at the end, so it can be called if there's no overriden occurs
            oqtaneServicesTypes.Add(typeof(DefaultOqtaneServerServices));

            return oqtaneServicesTypes.Select(t => Activator.CreateInstance(t) as IOqtaneServices);
        }
    }
}
