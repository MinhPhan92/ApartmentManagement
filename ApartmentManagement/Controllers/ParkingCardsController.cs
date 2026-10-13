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
[Route("api/parking-cards")]
[Authorize(Roles = "SuperAdmin,BuildingManager,Resident")]
public sealed class ParkingCardsController : ControllerBase
{
    private static readonly Regex CodePattern = new("^[A-Z0-9-]{3,32}$", RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ParkingCardsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParkingCardResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok((await VisibleCards().AsNoTracking().Include(x => x.Vehicle).ThenInclude(x => x.Apartment)
            .OrderByDescending(x => x.IssuedAtUtc).ToListAsync(cancellationToken)).Select(ToResponse).ToList());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ParkingCardResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var card = await VisibleCards().AsNoTracking().Include(x => x.Vehicle).ThenInclude(x => x.Apartment)
            .SingleOrDefaultAsync(x => x.ParkingCardId == id, cancellationToken);
        return card is null ? NotFound() : Ok(ToResponse(card));
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.RequireManagement)]
    public async Task<ActionResult<ParkingCardResponse>> Issue(
        IssueParkingCardRequest request,
        CancellationToken cancellationToken)
    {
        var code = Regex.Replace(request.CardCode.Trim().ToUpperInvariant(), "\\s+", string.Empty);
        if (!CodePattern.IsMatch(code))
        {
            ModelState.AddModelError(nameof(request.CardCode), "Mã thẻ chỉ được chứa chữ, số và dấu gạch ngang.");
            return ValidationProblem(ModelState);
        }

        var vehicle = await _db.Vehicles.Include(x => x.ParkingCard)
            .SingleOrDefaultAsync(x => x.VehicleId == request.VehicleId, cancellationToken);
        if (vehicle is null) return NotFound("Không tìm thấy phương tiện.");
        if (vehicle.ParkingCard is not null)
            return Conflict(new ProblemDetails { Title = "Phương tiện đã có thẻ xe.", Status = 409 });
        if (await _db.ParkingCards.AnyAsync(x => x.CardCode == code, cancellationToken))
            return Conflict(new ProblemDetails { Title = "Mã thẻ đã tồn tại.", Detail = code, Status = 409 });

        var card = new ParkingCard { VehicleId = vehicle.VehicleId, CardCode = code };
        _db.ParkingCards.Add(card);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new ProblemDetails { Title = "Mã thẻ hoặc phương tiện đã được sử dụng.", Status = 409 });
        }

        var saved = await _db.ParkingCards.AsNoTracking().Include(x => x.Vehicle).ThenInclude(x => x.Apartment)
            .SingleAsync(x => x.ParkingCardId == card.ParkingCardId, cancellationToken);
        var response = ToResponse(saved);
        return CreatedAtAction(nameof(GetById), new { id = card.ParkingCardId }, response);
    }

    private IQueryable<ParkingCard> VisibleCards()
    {
        var query = _db.ParkingCards.AsQueryable();
        if (_currentUser.IsInRole(AppRoles.SuperAdmin) || _currentUser.IsInRole(AppRoles.BuildingManager)) return query;
        var userId = _currentUser.UserId;
        return query.Where(x => x.Vehicle.Resident != null && x.Vehicle.Resident.UserId == userId);
    }

    private static ParkingCardResponse ToResponse(ParkingCard x) => new(
        x.ParkingCardId, x.CardCode, x.Status, x.IssuedAtUtc, x.VehicleId,
        x.Vehicle.LicensePlate, x.Vehicle.ApartmentId, x.Vehicle.Apartment!.ApartmentCode);
}
