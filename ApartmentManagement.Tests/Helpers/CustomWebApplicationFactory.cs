using ApartmentManagement.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ApartmentManagement.Tests.Helpers;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ApartmentManagementTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "TestOrIdentity";
                    options.DefaultAuthenticateScheme = "TestOrIdentity";
                    options.DefaultChallengeScheme = "TestOrIdentity";
                    options.DefaultForbidScheme = "TestOrIdentity";
                })
                .AddPolicyScheme("TestOrIdentity", null, options =>
                {
                    options.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey("X-Test-Role")
                            ? TestAuthHandler.SchemeName
                            : IdentityConstants.ApplicationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
        });
    }
}
