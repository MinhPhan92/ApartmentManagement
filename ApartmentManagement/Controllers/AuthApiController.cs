using System.ComponentModel.DataAnnotations;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAntiforgery _antiforgery;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuthApiController> _logger;

    public AuthApiController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAntiforgery antiforgery,
        ApplicationDbContext db,
        ILogger<AuthApiController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _antiforgery = antiforgery;
        _db = db;
        _logger = logger;
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(
            tokens.HeaderName ?? "RequestVerificationToken",
            tokens.RequestToken ?? string.Empty));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Phiên đăng nhập không hợp lệ",
                Detail = "Vui lòng đăng nhập lại để tiếp tục.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var response = await BuildCurrentUserResponseAsync(user, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<CurrentUserResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var identifier = request.UserNameOrEmail.Trim();
        var user = await _userManager.FindByNameAsync(identifier)
                   ?? await _userManager.FindByEmailAsync(identifier);

        if (user == null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Đăng nhập thất bại",
                Detail = "Tài khoản hoặc mật khẩu không chính xác.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        if (!user.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Tài khoản bị khóa",
                Detail = "Tài khoản này đang bị khóa hoặc vô hiệu hóa. Vui lòng liên hệ ban quản lý.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Người dùng {UserName} đã đăng nhập qua API.", user.UserName);
            var response = await BuildCurrentUserResponseAsync(user, cancellationToken);
            return Ok(response);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Tài khoản {UserName} đã bị tạm khóa.", user.UserName);
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Title = "Tài khoản tạm khóa",
                Detail = "Tài khoản đã bị tạm khóa do nhập sai mật khẩu nhiều lần. Vui lòng thử lại sau 15 phút.",
                Status = StatusCodes.Status423Locked
            });
        }

        if (result.RequiresTwoFactor)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Yêu cầu xác thực hai bước",
                Detail = "Tài khoản này yêu cầu xác thực hai yếu tố.",
                Status = StatusCodes.Status403Forbidden
            });
        }

        return Unauthorized(new ProblemDetails
        {
            Title = "Đăng nhập thất bại",
            Detail = "Tài khoản hoặc mật khẩu không chính xác.",
            Status = StatusCodes.Status401Unauthorized
        });
    }

    [HttpPost("logout")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<LogoutResponse>> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Người dùng đã đăng xuất qua API.");
        return Ok(new LogoutResponse("Đã đăng xuất thành công."));
    }

    private async Task<CurrentUserResponse> BuildCurrentUserResponseAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var rawRoles = await _userManager.GetRolesAsync(user);
        var roles = NormalizeRoles(rawRoles);

        var isSuperAdmin = roles.Contains(AppRoles.SuperAdmin);
        var isBuildingManager = roles.Contains(AppRoles.BuildingManager);
        var isAccountant = roles.Contains(AppRoles.Accountant);
        var isTechnician = roles.Contains(AppRoles.Technician);
        var isResident = roles.Contains(AppRoles.Resident);

        var permissions = new UserPermissionsDto(
            RequireSuperAdmin: isSuperAdmin,
            RequireManagement: isSuperAdmin || isBuildingManager,
            RequireAccountantOrManager: isSuperAdmin || isBuildingManager || isAccountant,
            RequireTechnician: isTechnician,
            RequireTechnicianOrManagement: isSuperAdmin || isBuildingManager || isTechnician,
            RequireResident: isResident);

        var isManagementPortalUser = isSuperAdmin || isBuildingManager || isAccountant || isTechnician;
        var portal = isManagementPortalUser ? "management" : "resident";

        string defaultRoute;
        if (isSuperAdmin || isBuildingManager || isAccountant)
        {
            defaultRoute = "/management/dashboard";
        }
        else if (isTechnician)
        {
            defaultRoute = "/management/dashboard";
        }
        else
        {
            defaultRoute = "/resident/dashboard";
        }

        ResidentSessionContextDto? residentContext = null;
        if (isResident)
        {
            var resident = await _db.Residents
                .AsNoTracking()
                .Include(r => r.ApartmentResidents)
                    .ThenInclude(ar => ar.Apartment)
                        .ThenInclude(a => a.Building)
                .FirstOrDefaultAsync(r => r.UserId == user.Id, cancellationToken);

            if (resident != null)
            {
                var activeOccupancy = resident.ApartmentResidents
                    .OrderByDescending(ar => ar.IsOwner)
                    .ThenByDescending(ar => ar.MoveInDate)
                    .FirstOrDefault(ar => ar.MoveOutDate == null);

                residentContext = new ResidentSessionContextDto(
                    resident.ResidentId,
                    activeOccupancy?.ApartmentId,
                    activeOccupancy?.Apartment?.ApartmentCode,
                    activeOccupancy?.Apartment?.Building?.BuildingName);
            }
        }

        var displayName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName.Trim()
            : (user.UserName ?? string.Empty);

        return new CurrentUserResponse(
            UserId: user.Id,
            UserName: user.UserName ?? string.Empty,
            DisplayName: displayName,
            Email: user.Email,
            PhoneNumber: user.PhoneNumber,
            Roles: roles,
            Portal: portal,
            DefaultRoute: defaultRoute,
            Permissions: permissions,
            ResidentContext: residentContext);
    }

    private static IReadOnlyList<string> NormalizeRoles(IEnumerable<string> rawRoles)
    {
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in rawRoles)
        {
            if (string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add(AppRoles.SuperAdmin);
            }
            else if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                normalized.Add(AppRoles.BuildingManager);
            }
            else if (!string.IsNullOrWhiteSpace(role))
            {
                normalized.Add(role.Trim());
            }
        }

        return AppRoles.AllRoles.Where(normalized.Contains).Concat(normalized.Except(AppRoles.AllRoles)).ToList();
    }
}

public sealed record CsrfTokenResponse(
    string HeaderName,
    string RequestToken);

public sealed record LoginRequest(
    [Required(ErrorMessage = "Vui lòng nhập Tên đăng nhập hoặc Email.")]
    [StringLength(256, ErrorMessage = "Tên đăng nhập hoặc Email không được vượt quá 256 ký tự.")]
    string UserNameOrEmail,

    [Required(ErrorMessage = "Vui lòng nhập Mật khẩu.")]
    [StringLength(256, ErrorMessage = "Mật khẩu không hợp lệ.")]
    string Password,

    bool RememberMe = false);

public sealed record LogoutResponse(string Message);

public sealed record UserPermissionsDto(
    bool RequireSuperAdmin,
    bool RequireManagement,
    bool RequireAccountantOrManager,
    bool RequireTechnician,
    bool RequireTechnicianOrManagement,
    bool RequireResident);

public sealed record ResidentSessionContextDto(
    int ResidentId,
    int? ActiveApartmentId,
    string? ActiveApartmentCode,
    string? BuildingName);

public sealed record CurrentUserResponse(
    string UserId,
    string UserName,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    string Portal,
    string DefaultRoute,
    UserPermissionsDto Permissions,
    ResidentSessionContextDto? ResidentContext);

