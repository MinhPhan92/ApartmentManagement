using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagement.Data;
using ApartmentManagement.Models;

namespace ApartmentManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResidentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ResidentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/residents
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Resident>>> GetResidents()
        {
            return await _context.Residents
                .Include(r => r.ApartmentResidents)
                .ToListAsync();
        }

        // GET: api/residents/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Resident>> GetResident(int id)
        {
            var resident = await _context.Residents
                .Include(r => r.ApartmentResidents)
                .FirstOrDefaultAsync(r => r.ResidentId == id);

            if (resident == null)
            {
                return NotFound();
            }

            return resident;
        }

        // POST: api/residents
        [HttpPost]
        public async Task<ActionResult<Resident>> CreateResident(Resident resident)
        {
            _context.Residents.Add(resident);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetResident), new { id = resident.ResidentId }, resident);
        }
    }
}