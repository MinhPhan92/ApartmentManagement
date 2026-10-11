using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ApartmentManagement.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId =>
            _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        public string? UserName =>
            _httpContextAccessor.HttpContext?.User?.Identity?.Name;

        public bool IsAuthenticated =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

        public bool IsInRole(string role)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return false;
            if (user.IsInRole(role)) return true;
            if (role == "SystemAdmin" && user.IsInRole("SuperAdmin")) return true;
            if (role == "SuperAdmin" && user.IsInRole("SystemAdmin")) return true;
            if (role == "Manager" && user.IsInRole("BuildingManager")) return true;
            if (role == "BuildingManager" && user.IsInRole("Manager")) return true;
            return false;
        }
    }
}
