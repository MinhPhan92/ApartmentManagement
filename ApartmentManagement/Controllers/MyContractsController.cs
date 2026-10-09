using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[Authorize(Policy = AppPolicies.RequireResident)]
public sealed class MyContractsController : Controller
{
    private readonly IContractService _contractService;

    public MyContractsController(IContractService contractService) =>
        _contractService = contractService;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var contracts = await _contractService.GetForResidentUserAsync(userId);
        return View(new ResidentContractListViewModel(contracts.Select(ToSummary).ToList()));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = CurrentUserId();
        if (userId == null) return Forbid();

        var contract = (await _contractService.GetForResidentUserAsync(userId))
            .FirstOrDefault(x => x.ApartmentContractId == id);
        if (contract == null) return NotFound();

        return View(new ResidentContractDetailsViewModel(
            contract.ContractCode,
            $"{contract.Apartment.Building?.BuildingName ?? "Không rõ tòa nhà"} — {contract.Apartment.ApartmentCode}",
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.Status,
            contract.Parties
                .Select(x => new ResidentContractPartyViewModel(x.Resident.User.FullName, x.Role))
                .ToList(),
            contract.StatusHistory
                .OrderByDescending(x => x.ChangedAtUtc)
                .Select(x => new ResidentContractHistoryViewModel(
                    x.PreviousStatus,
                    x.NewStatus,
                    x.ChangedAtUtc))
                .ToList()));
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private static ResidentContractSummaryViewModel ToSummary(ApartmentContract contract) =>
        new(
            contract.ApartmentContractId,
            contract.ContractCode,
            $"{contract.Apartment.Building?.BuildingName ?? "Không rõ tòa nhà"} — {contract.Apartment.ApartmentCode}",
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.Status);
}
