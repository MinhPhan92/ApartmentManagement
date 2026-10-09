using Xunit;

namespace ApartmentManagement.Tests.SqlServer;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "APARTMENTHUB_SQLSERVER_TEST_CONNECTION";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
            Skip = $"Thiếu biến môi trường {ConnectionVariable}.";
    }
}
