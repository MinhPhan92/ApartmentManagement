using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Common.Validation;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApartmentManagement.Controllers;

[ApiController]
[Authorize]
[Route("api/apartmentresidents")]
public sealed class ApartmentResidentsController : ControllerBase
{
    private readonly IOccupancyService _service;
    public ApartmentResidentsController(IOccupancyService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApartmentResidentResponse>>> GetApartmentResidents()
    {
        List<ApartmentResident> occupancies;
        if (User.IsInRole(AppRoles.Resident))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            occupancies = await _service.GetForUserAsync(userId);
        }
        else if (User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.BuildingManager))
        {
            occupancies = await _service.GetAllAsync();
        }
        else return Forbid();

        return Ok(occupancies.Select(ToResponse));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<ActionResult<ApartmentResidentResponse>> MoveIn(AssignResidentRequest request)
    {
        var result = await _service.MoveInAsync(new MoveInViewModel
        {
            ApartmentId = request.ApartmentId,
            ResidentId = request.ResidentId,
            Relationship = request.Relationship,
            MoveInDate = request.MoveInDate,
            IsOwner = request.IsOwner
        });
        if (!result.Success) return Conflict(new { error = result.ErrorMessage });
        var occupancy = await _service.GetByIdAsync(result.Occupancy!.ApartmentResidentId);
        return Ok(ToResponse(occupancy!));
    }

    [HttpPost("{id:int}/move-out")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<IActionResult> MoveOut(int id, MoveOutRequest request)
    {
        var result = await _service.MoveOutAsync(id, request.MoveOutDate);
        return result.Success ? NoContent() : Conflict(new { error = result.ErrorMessage });
    }

    private static ApartmentResidentResponse ToResponse(ApartmentResident x) => new(
        x.ApartmentResidentId, x.ApartmentId, x.Apartment.ApartmentCode,
        x.Apartment.Building?.BuildingName, x.ResidentId, x.Resident.User.FullName,
        x.Relationship, x.MoveInDate, x.MoveOutDate, x.IsOwner);
}

public sealed record AssignResidentRequest(
    [Range(1, int.MaxValue)] int ApartmentId,
    [Range(1, int.MaxValue)] int ResidentId,
    [Required, StringLength(ResidentValidation.RelationshipMaxLength)] string Relationship,
    DateTime MoveInDate,
    bool IsOwner);

public sealed record MoveOutRequest([Required] DateTime MoveOutDate);

public sealed record ApartmentResidentResponse(
    int ApartmentResidentId, int ApartmentId, string ApartmentCode, string? BuildingName,
    int ResidentId, string ResidentName, string Relationship,
    DateTime MoveInDate, DateTime? MoveOutDate, bool IsOwner);
