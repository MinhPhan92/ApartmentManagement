using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IFinancialReportService
{
    Task<MonthlyStatementReportDto> GenerateMonthlyStatementAsync(
        MonthlyStatementFilterInput filter,
        CancellationToken cancellationToken = default);

    byte[] ExportMonthlyStatementToCsv(MonthlyStatementReportDto report);

    string GetCsvFileName(MonthlyStatementReportDto report);
}

