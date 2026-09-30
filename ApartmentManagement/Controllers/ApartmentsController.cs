using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagement.Data;
using ApartmentManagement.Models;

namespace ApartmentManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApartmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ApartmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/apartments
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Apartment>>> GetApartments()
        {
            return await _context.Apartments
                .Include(a => a.ApartmentResidents)
                .ToListAsync();
        }

        // GET: api/apartments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Apartment>> GetApartment(int id)
        {
            var apartment = await _context.Apartments
                .Include(a => a.ApartmentResidents)
                .FirstOrDefaultAsync(a => a.ApartmentId == id);

            if (apartment == null)
            {
                return NotFound();
            }

            return apartment;
        }

        // POST: api/apartments
        [HttpPost]
        public async Task<ActionResult<Apartment>> CreateApartment(Apartment apartment)
        {
            // Kiểm tra tòa nhà có tồn tại không trước khi gán
            var buildingExists = await _context.Buildings.AnyAsync(b => b.BuildingId == apartment.BuildingId);
            if (!buildingExists)
            {
                return BadRequest("BuildingId không tồn tại trong hệ thống.");
            }

            _context.Apartments.Add(apartment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetApartment), new { id = apartment.ApartmentId }, apartment);
        }

        // PUT: api/apartments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateApartment(int id, Apartment apartment)
        {
            if (id != apartment.ApartmentId)
            {
                return BadRequest("ID không khớp.");
            }

            _context.Entry(apartment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Apartments.AnyAsync(e => e.ApartmentId == id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }
    }
}