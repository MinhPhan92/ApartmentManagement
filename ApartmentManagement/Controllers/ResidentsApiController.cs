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
[Route("api/residents")]
public sealed class ResidentsApiController : ControllerBase
{
    private readonly IResidentService _residentService;
    public ResidentsApiController(IResidentService residentService) => _residentService = residentService;

    [HttpGet]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<ActionResult<IEnumerable<ResidentResponse>>> GetResidents(string? searchTerm = null) =>
        Ok((await _residentService.GetAllAsync(searchTerm)).Select(ToResponse));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResidentResponse>> GetResident(int id)
    {
        var resident = await _residentService.GetByIdAsync(id);
        if (resident == null) return NotFound();

        if (User.IsInRole(AppRoles.Resident))
        {
            if (resident.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return Forbid();
        }
        else if (!User.IsInRole(AppRoles.SuperAdmin) && !User.IsInRole(AppRoles.BuildingManager))
        {
            return Forbid();
        }

        return Ok(ToResponse(resident));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<ActionResult<ResidentResponse>> ProvisionResident(ProvisionResidentRequest request)
    {
        var result = await _residentService.CreateAsync(new ResidentCreateViewModel
        {
            Email = request.Email,
            FullName = request.FullName,
            TemporaryPassword = request.TemporaryPassword,
            CitizenId = request.CitizenId,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            EmergencyContact = request.EmergencyContact
        });
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return ValidationProblem(ModelState);
        }

        return CreatedAtAction(nameof(GetResident), new { id = result.Resident!.ResidentId }, ToResponse(result.Resident));
    }

    private static ResidentResponse ToResponse(Resident resident) => new(
        resident.ResidentId, resident.User.FullName, resident.User.Email, resident.CitizenId,
        resident.DateOfBirth, resident.Gender, resident.Address, resident.EmergencyContact,
        resident.ApartmentResidents.Select(x => new ResidentApartmentResponse(
            x.ApartmentId, x.Apartment.ApartmentCode, x.Apartment.Building?.BuildingName,
            x.Relationship, x.MoveInDate, x.MoveOutDate, x.IsOwner)));
}

public sealed record ProvisionResidentRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(ResidentValidation.FullNameMaxLength)] string FullName,
    [Required] string TemporaryPassword,
    [Required, StringLength(ResidentValidation.CitizenIdMaxLength), RegularExpression(ResidentValidation.CitizenIdPattern)] string CitizenId,
    [ValidDateOfBirth] DateTime? DateOfBirth,
    [StringLength(ResidentValidation.GenderMaxLength)] string? Gender,
    [StringLength(ResidentValidation.AddressMaxLength)] string? Address,
    [StringLength(ResidentValidation.PhoneMaxLength), RegularExpression(ResidentValidation.PhonePattern)] string? EmergencyContact);

public sealed record ResidentResponse(
    int ResidentId, string FullName, string? Email, string CitizenId, DateTime? DateOfBirth,
    string? Gender, string? Address, string? EmergencyContact,
    IEnumerable<ResidentApartmentResponse> Apartments);

public sealed record ResidentApartmentResponse(
    int ApartmentId, string ApartmentCode, string? BuildingName, string Relationship,
    DateTime MoveInDate, DateTime? MoveOutDate, bool IsOwner);
