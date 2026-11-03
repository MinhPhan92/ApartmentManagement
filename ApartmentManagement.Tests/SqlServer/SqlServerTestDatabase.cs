using ApartmentManagement.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private const string DatabasePrefix = "ApartmentHubTests_";
    public string ConnectionString { get; }
    public DbContextOptions<ApplicationDbContext> Options { get; }

    private SqlServerTestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            // `dotnet ef migrations has-pending-model-changes` is executed separately.
            // The test host builds DbContextOptions outside the application service provider,
            // which otherwise promotes this warning to an exception before SQL can be tested.
            .ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning))
            .Options;
    }

    public static SqlServerTestDatabase Create()
    {
        var configured = Environment.GetEnvironmentVariable(SqlServerFactAttribute.ConnectionVariable)
            ?? throw new InvalidOperationException("SQL Server test connection chưa được cấu hình.");
        var builder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = $"{DatabasePrefix}{Guid.NewGuid():N}",
            TrustServerCertificate = true
        };
        if (!builder.InitialCatalog.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Test chỉ được phép sử dụng database có prefix ApartmentHubTests_.");
        return new SqlServerTestDatabase(builder.ConnectionString);
    }

    public ApplicationDbContext CreateContext() => new(Options);

    public ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
    {
        if (interceptors.Length == 0)
            return new ApplicationDbContext(Options);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(interceptors)
            .Options;
        return new ApplicationDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();
        if (!context.Database.GetDbConnection().Database.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Từ chối xóa database không thuộc test suite.");
        await context.Database.EnsureDeletedAsync();
    }
}
