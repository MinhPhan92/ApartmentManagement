using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Register Application Services
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IApartmentService, ApartmentService>();
builder.Services.AddScoped<IResidentService, ResidentService>();
builder.Services.AddScoped<IAccountManagementService, AccountManagementService>();
builder.Services.AddScoped<IOccupancyService, OccupancyService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IIncidentTicketService, IncidentTicketService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddSingleton<IVietQrCodeGenerator, VietQrCodeGenerator>();
builder.Services.Configure<VietQrOptions>(builder.Configuration.GetSection("Payments:VietQr"));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated != true) return;

        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.Principal);
        if (user == null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(AppPolicies.RequireManagement, policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.BuildingManager));

    options.AddPolicy(AppPolicies.RequireAccountantOrManager, policy =>
        policy.RequireRole(
            AppRoles.SuperAdmin,
            AppRoles.BuildingManager,
            AppRoles.Accountant));

    options.AddPolicy(AppPolicies.RequireResident, policy =>
        policy.RequireRole(AppRoles.Resident));

    options.AddPolicy(AppPolicies.RequireSuperAdmin, policy =>
        policy.RequireRole(AppRoles.SuperAdmin));

    options.AddPolicy(AppPolicies.RequireTicketStaff, policy =>
        policy.RequireRole(AppRoles.SuperAdmin, AppRoles.BuildingManager, AppRoles.Technician));
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

await IdentityDataSeeder.InitializeAsync(app.Services, app.Configuration);

app.Run();

public partial class Program;
