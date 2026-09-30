using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApartmentManagement.Data;
using ApartmentManagement.Models;

namespace ApartmentManagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuildingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BuildingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/buildings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Building>>> GetBuildings()
        {
            return await _context.Buildings
                .Include(b => b.Apartments)
                .ToListAsync();
        }

        // GET: api/buildings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Building>> GetBuilding(int id)
        {
            var building = await _context.Buildings
                .Include(b => b.Apartments)
                .FirstOrDefaultAsync(b => b.BuildingId == id);

            if (building == null)
            {
                return NotFound();
            }

            return building;
        }

        // POST: api/buildings
        [HttpPost]
        public async Task<ActionResult<Building>> CreateBuilding(Building building)
        {
            _context.Buildings.Add(building);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBuilding), new { id = building.BuildingId }, building);
        }
    }
}