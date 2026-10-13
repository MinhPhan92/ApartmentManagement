using System.Text.RegularExpressions;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class VehicleParkingPreflightScriptTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory, "scripts", "preflight-vehicle-parking-phase2.sql");

    [Fact]
    public void PreflightScript_IsPackagedAndContainsRequiredDiagnostics()
    {
        Assert.True(File.Exists(ScriptPath), $"Missing preflight script: {ScriptPath}");
        var sql = File.ReadAllText(ScriptPath);

        Assert.Contains("VEHICLE_WITHOUT_OCCUPANCY", sql);
        Assert.Contains("PROPOSED_CURRENT_ALGORITHM_MAPPING", sql);
        Assert.Contains("NO_OCCUPANCY_AT_VEHICLE_CREATION", sql);
        Assert.Contains("MULTIPLE_OCCUPANCIES_AT_VEHICLE_CREATION", sql);
        Assert.Contains("NORMALIZED_LICENSE_PLATE_COLLISION", sql);
        Assert.Contains("TICKET_FOREIGN_KEY_DEPENDENCY", sql);
        Assert.Contains("TICKET_SQL_EXPRESSION_DEPENDENCY", sql);
        Assert.Contains("TICKET_TRIGGER_DEPENDENCY", sql);
        Assert.Contains("COUNT_BIG(*)", sql);
    }

    [Fact]
    public void PreflightScript_ContainsNoMutatingOrExecutionStatements()
    {
        var sql = File.ReadAllText(ScriptPath);
        var forbiddenAtStatementStart = new Regex(
            @"(?im)^\s*(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC(?:UTE)?|GRANT|REVOKE|DENY|DBCC|BACKUP|RESTORE|USE)\b",
            RegexOptions.CultureInvariant);

        Assert.False(forbiddenAtStatementStart.IsMatch(sql), "Preflight must remain read-only.");
        Assert.DoesNotContain("SELECT INTO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
    }
}
