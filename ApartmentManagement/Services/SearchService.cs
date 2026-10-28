using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services;

public sealed class SearchService : ISearchService
{
    private const int MaxPerCategory = 5;
    private const int MaxQueryLength = 100;
    private const int MinQueryLength = 2;

    private readonly ApplicationDbContext _context;

    public SearchService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GlobalSearchResponseDto> SearchAsync(string? query, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new GlobalSearchResponseDto(string.Empty, 0, []);
        }

        var term = query.Trim();
        if (term.Length < MinQueryLength)
        {
            return new GlobalSearchResponseDto(term, 0, []);
        }

        if (term.Length > MaxQueryLength)
        {
            term = term[..MaxQueryLength];
        }

        if (user?.Identity?.IsAuthenticated != true)
        {
            return new GlobalSearchResponseDto(term, 0, []);
        }

        var groups = new List<GlobalSearchGroupDto>();

        var isManagement = user.IsInRole(AppRoles.SuperAdmin) || user.IsInRole(AppRoles.BuildingManager);
        var isAccountant = user.IsInRole(AppRoles.Accountant);
        var isResident = user.IsInRole(AppRoles.Resident);

        if (isManagement)
        {
            await SearchBuildingsAsync(term, groups);
            await SearchApartmentsAsync(term, groups);
            await SearchResidentsAsync(term, groups);
            await SearchContractsAsync(term, groups);
            await SearchInvoicesAsync(term, groups);
        }
        else if (isAccountant)
        {
            await SearchInvoicesAsync(term, groups);
        }
        else if (isResident)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await SearchResidentContractsAsync(term, userId, groups);
                await SearchResidentInvoicesAsync(term, userId, groups);
            }
        }

        var totalCount = groups.Sum(g => g.Items.Count);
        return new GlobalSearchResponseDto(term, totalCount, groups);
    }

    private async Task SearchBuildingsAsync(string term, List<GlobalSearchGroupDto> groups)
    {
        var items = await _context.Buildings.AsNoTracking()
            .Where(b => b.BuildingCode.Contains(term) ||
                        b.BuildingName.Contains(term) ||
                        (b.Address != null && b.Address.Contains(term)))
            .OrderBy(b => b.BuildingName)
            .Take(MaxPerCategory)
            .Select(b => new GlobalSearchResultItemDto(
                "Tòa nhà",
                $"{b.BuildingName} ({b.BuildingCode})",
                b.Address ?? $"{b.NumberOfFloors} tầng",
                $"/Buildings/Details/{b.BuildingId}",
                $"{b.NumberOfFloors} tầng",
                "bg-info text-dark"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Tòa nhà", items));
        }
    }

    private async Task SearchApartmentsAsync(string term, List<GlobalSearchGroupDto> groups)
    {
        var items = await _context.Apartments.AsNoTracking()
            .Include(a => a.Building)
            .Where(a => a.ApartmentCode.Contains(term) ||
                        (a.Building != null && a.Building.BuildingName.Contains(term)))
            .OrderBy(a => a.ApartmentCode)
            .Take(MaxPerCategory)
            .Select(a => new GlobalSearchResultItemDto(
                "Căn hộ",
                $"Căn {a.ApartmentCode} — {(a.Building != null ? a.Building.BuildingName : string.Empty)}",
                $"Tầng {a.Floor} · {a.Area:N0} m²",
                $"/Apartments/Details/{a.ApartmentId}",
                a.Status,
                a.Status == "Đang sử dụng" ? "bg-success" : "bg-secondary"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Căn hộ", items));
        }
    }

    private async Task SearchResidentsAsync(string term, List<GlobalSearchGroupDto> groups)
    {
        var items = await _context.Residents.AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.CitizenId.Contains(term) ||
                        r.User.FullName.Contains(term) ||
                        (r.User.PhoneNumber != null && r.User.PhoneNumber.Contains(term)) ||
                        (r.User.Email != null && r.User.Email.Contains(term)))
            .OrderBy(r => r.User.FullName)
            .Take(MaxPerCategory)
            .Select(r => new GlobalSearchResultItemDto(
                "Cư dân",
                r.User.FullName,
                $"CCCD: {r.CitizenId}{(string.IsNullOrWhiteSpace(r.User.PhoneNumber) ? string.Empty : $" · {r.User.PhoneNumber}")}",
                $"/Residents/Details/{r.ResidentId}",
                r.User.IsActive ? "Hoạt động" : "Khóa",
                r.User.IsActive ? "bg-primary" : "bg-danger"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Cư dân", items));
        }
    }

    private async Task SearchContractsAsync(string term, List<GlobalSearchGroupDto> groups)
    {
        var pattern = $"%{term}%";
        var items = await _context.ApartmentContracts.AsNoTracking()
            .Include(c => c.Apartment).ThenInclude(a => a.Building)
            .Where(c => EF.Functions.Like(c.ContractCode, pattern) ||
                        EF.Functions.Like(c.Apartment.ApartmentCode, pattern) ||
                        (c.Apartment.Building != null && EF.Functions.Like(c.Apartment.Building.BuildingName, pattern)) ||
                        c.Parties.Any(p => EF.Functions.Like(p.Resident.User.FullName, pattern)))
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(MaxPerCategory)
            .Select(c => new GlobalSearchResultItemDto(
                "Hợp đồng",
                $"HĐ {c.ContractCode} — Căn {c.Apartment.ApartmentCode}",
                $"Tiền thuê: {c.MonthlyRent:N0} đ/tháng",
                $"/Contracts/Details/{c.ApartmentContractId}",
                c.Status == ApartmentContractStatus.Active ? "Đang hiệu lực" :
                c.Status == ApartmentContractStatus.Draft ? "Nháp" :
                c.Status == ApartmentContractStatus.Completed ? "Đã hoàn tất" : "Đã hủy",
                c.Status == ApartmentContractStatus.Active ? "bg-success" :
                c.Status == ApartmentContractStatus.Draft ? "bg-warning text-dark" : "bg-secondary"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Hợp đồng", items));
        }
    }

    private async Task SearchInvoicesAsync(string term, List<GlobalSearchGroupDto> groups)
    {
        var items = await _context.ApartmentInvoices.AsNoTracking()
            .Include(i => i.Apartment)
            .Include(i => i.Resident).ThenInclude(r => r.User)
            .Where(i => i.InvoiceCode.Contains(term) ||
                        i.Apartment.ApartmentCode.Contains(term) ||
                        i.Resident.User.FullName.Contains(term))
            .OrderByDescending(i => i.BillingYear).ThenByDescending(i => i.BillingMonth)
            .Take(MaxPerCategory)
            .Select(i => new GlobalSearchResultItemDto(
                "Hóa đơn",
                $"HĐ {i.InvoiceCode} — Căn {i.Apartment.ApartmentCode}",
                $"Kỳ {i.BillingMonth}/{i.BillingYear} · {i.TotalAmount:N0} đ",
                $"/Billing/Details/{i.ApartmentInvoiceId}",
                i.Status == ApartmentInvoiceStatus.Issued ? "Đã phát hành" :
                i.Status == ApartmentInvoiceStatus.Draft ? "Nháp" : "Đã hủy",
                i.Status == ApartmentInvoiceStatus.Issued ? "bg-primary" : "bg-secondary"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Hóa đơn", items));
        }
    }

    private async Task SearchResidentContractsAsync(string term, string userId, List<GlobalSearchGroupDto> groups)
    {
        var pattern = $"%{term}%";
        var items = await _context.ApartmentContracts.AsNoTracking()
            .Include(c => c.Apartment).ThenInclude(a => a.Building)
            .Where(c => c.Parties.Any(p => p.Resident.UserId == userId) &&
                       (EF.Functions.Like(c.ContractCode, pattern) ||
                        EF.Functions.Like(c.Apartment.ApartmentCode, pattern) ||
                        (c.Apartment.Building != null && EF.Functions.Like(c.Apartment.Building.BuildingName, pattern))))
            .OrderByDescending(c => c.StartDate)
            .Take(MaxPerCategory)
            .Select(c => new GlobalSearchResultItemDto(
                "Hợp đồng của tôi",
                $"HĐ {c.ContractCode} — Căn {c.Apartment.ApartmentCode}",
                $"Tiền thuê: {c.MonthlyRent:N0} đ/tháng",
                $"/MyContracts/Details/{c.ApartmentContractId}",
                c.Status == ApartmentContractStatus.Active ? "Đang hiệu lực" :
                c.Status == ApartmentContractStatus.Completed ? "Đã hoàn tất" : "Đã hủy",
                c.Status == ApartmentContractStatus.Active ? "bg-success" : "bg-secondary"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Hợp đồng của tôi", items));
        }
    }

    private async Task SearchResidentInvoicesAsync(string term, string userId, List<GlobalSearchGroupDto> groups)
    {
        var items = await _context.ApartmentInvoices.AsNoTracking()
            .Include(i => i.Apartment)
            .Where(i => i.Resident.UserId == userId &&
                        i.Status == ApartmentInvoiceStatus.Issued &&
                        (i.InvoiceCode.Contains(term) || i.Apartment.ApartmentCode.Contains(term)))
            .OrderByDescending(i => i.BillingYear).ThenByDescending(i => i.BillingMonth)
            .Take(MaxPerCategory)
            .Select(i => new GlobalSearchResultItemDto(
                "Hóa đơn của tôi",
                $"HĐ {i.InvoiceCode} — Căn {i.Apartment.ApartmentCode}",
                $"Kỳ {i.BillingMonth}/{i.BillingYear} · {i.TotalAmount:N0} đ",
                $"/MyInvoices/Details/{i.ApartmentInvoiceId}",
                "Đã phát hành",
                "bg-primary"))
            .ToListAsync();

        if (items.Count > 0)
        {
            groups.Add(new GlobalSearchGroupDto("Hóa đơn của tôi", items));
        }
    }
}

