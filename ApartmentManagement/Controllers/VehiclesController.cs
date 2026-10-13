using System.Text.RegularExpressions;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Controllers;

[ApiController]
[Route("api/vehicles")]
[Authorize(Roles = "SuperAdmin,BuildingManager,Resident")]
public sealed class VehiclesController : ControllerBase
{
    private static readonly Regex PlatePattern = new("^[A-Z0-9.-]{5,20}$", RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VehiclesController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await VisibleVehicles().AsNoTracking()
            .Include(x => x.Apartment).Include(x => x.Resident).ThenInclude(x => x!.User)
            .Include(x => x.ParkingCard).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var vehicle = await VisibleVehicles().AsNoTracking()
            .Include(x => x.Apartment).Include(x => x.Resident).ThenInclude(x => x!.User)
            .Include(x => x.ParkingCard).SingleOrDefaultAsync(x => x.VehicleId == id, cancellationToken);
        return vehicle is null ? NotFound() : Ok(ToResponse(vehicle));
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireResident)]
    public async Task<ActionResult<VehicleResponse>> Register(
        RegisterVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var plate = Normalize(request.LicensePlate);
        if (!PlatePattern.IsMatch(plate))
        {
            ModelState.AddModelError(nameof(request.LicensePlate), "Biển số chỉ được chứa chữ, số, dấu chấm và dấu gạch ngang.");
            return ValidationProblem(ModelState);
        }

        var resident = await _db.Residents.SingleOrDefaultAsync(
            x => x.UserId == _currentUser.UserId, cancellationToken);
        if (resident is null) return Problem(statusCode: 409, detail: "Tài khoản chưa liên kết với hồ sơ cư dân.");

        var occupiesApartment = await _db.ApartmentResidents.AnyAsync(
            x => x.ApartmentId == request.ApartmentId && x.ResidentId == resident.ResidentId && x.MoveOutDate == null,
            cancellationToken);
        if (!occupiesApartment) return NotFound("Không tìm thấy căn hộ đang cư trú.");

        if (await _db.Vehicles.AnyAsync(x => x.LicensePlate == plate, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Biển số đã tồn tại.", Detail = plate, Status = 409 });

        var vehicle = new Vehicle
        {
            ApartmentId = request.ApartmentId,
            ResidentId = resident.ResidentId,
            VehicleType = request.VehicleType.Trim(),
            LicensePlate = plate,
            BrandModel = Clean(request.BrandModel),
            Color = Clean(request.Color),
            Status = "Chờ duyệt",
            CreatedAt = DateTime.UtcNow
        };
        _db.Vehicles.Add(vehicle);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails { Title = "Biển số đã tồn tại.", Detail = plate, Status = 409 });
        }

        var saved = await VisibleVehicles().AsNoTracking().Include(x => x.Apartment)
            .Include(x => x.Resident).ThenInclude(x => x!.User)
            .SingleAsync(x => x.VehicleId == vehicle.VehicleId, cancellationToken);
        var response = ToResponse(saved);
        return CreatedAtAction(nameof(GetById), new { id = vehicle.VehicleId }, response);
    }

    private IQueryable<Vehicle> VisibleVehicles()
    {
        var query = _db.Vehicles.AsQueryable();
        if (_currentUser.IsInRole(AppRoles.SuperAdmin) || _currentUser.IsInRole(AppRoles.BuildingManager)) return query;
        var userId = _currentUser.UserId;
        return query.Where(x => x.Resident != null && x.Resident.UserId == userId);
    }

    private static VehicleResponse ToResponse(Vehicle x) => new(
        x.VehicleId, x.ApartmentId, x.Apartment!.ApartmentCode, x.ResidentId,
        x.Resident!.User.FullName, x.VehicleType, x.LicensePlate, x.BrandModel, x.Color,
        x.Status, x.CreatedAt,
        x.ParkingCard == null ? null : new ParkingCardSummary(
            x.ParkingCard.ParkingCardId, x.ParkingCard.CardCode, x.ParkingCard.Status, x.ParkingCard.IssuedAtUtc));

    internal static string Normalize(string value) => Regex.Replace(value.Trim().ToUpperInvariant(), "\\s+", string.Empty);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
