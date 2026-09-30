using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagement.Data;
using ApartmentManagement.Models;

namespace ApartmentManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApartmentResidentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ApartmentResidentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/apartmentresidents
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApartmentResident>>> GetApartmentResidents()
        {
            return await _context.ApartmentResidents
                .Include(ar => ar.Apartment)
                .Include(ar => ar.Resident)
                .ToListAsync();
        }

        // POST: api/apartmentresidents (Gán cư dân vào căn hộ)
        [HttpPost]
        public async Task<ActionResult> AssignResidentToApartment(ApartmentResident model)
        {
            var aptExists = await _context.Apartments.AnyAsync(a => a.ApartmentId == model.ApartmentId);
            var resExists = await _context.Residents.AnyAsync(r => r.ResidentId == model.ResidentId);

            if (!aptExists || !resExists)
            {
                return BadRequest("Căn hộ hoặc cư dân không tồn tại.");
            }

            var alreadyAssigned = await _context.ApartmentResidents
                .AnyAsync(ar => ar.ApartmentId == model.ApartmentId && ar.ResidentId == model.ResidentId);

            if (alreadyAssigned)
            {
                return BadRequest("Cư dân này đã được gán vào căn hộ rồi.");
            }

            _context.ApartmentResidents.Add(model);
            await _context.SaveChangesAsync();

            return Ok(model);
        }
    }
}