using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Common.Validation;
using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApartmentManagement.Controllers;

[ApiController]
[Route("api/resident")]
[Produces("application/json")]
[Authorize(Policy = AppPolicies.RequireResident)]
[AutoValidateAntiforgeryToken]
public sealed class ResidentPortalApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IResidentService _residentService;
    private readonly IContractService _contractService;
    private readonly IBillingService _billingService;
    private readonly IPaymentService _paymentService;
    private readonly IVietQrCodeGenerator _qrCodeGenerator;
    private readonly VietQrOptions _vietQrOptions;
    private readonly IAmenityService _amenityService;
    private readonly IVehicleService _vehicleService;
    private readonly IVisitorService _visitorService;
    private readonly IRepairRequestService _repairService;
    private readonly IComplaintService _complaintService;
    private readonly INotificationService _notificationService;
    private readonly IAnnouncementService _announcementService;

    public ResidentPortalApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IResidentService residentService,
        IContractService contractService,
        IBillingService billingService,
        IPaymentService paymentService,
        IVietQrCodeGenerator qrCodeGenerator,
        IOptions<VietQrOptions> vietQrOptions,
        IAmenityService amenityService,
        IVehicleService vehicleService,
        IVisitorService visitorService,
        IRepairRequestService repairService,
        IComplaintService complaintService,
        INotificationService notificationService,
        IAnnouncementService announcementService)
    {
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
        _residentService = residentService;
        _contractService = contractService;
        _billingService = billingService;
        _paymentService = paymentService;
        _qrCodeGenerator = qrCodeGenerator;
        _vietQrOptions = vietQrOptions.Value;
        _amenityService = amenityService;
        _vehicleService = vehicleService;
        _visitorService = visitorService;
        _repairService = repairService;
        _complaintService = complaintService;
        _notificationService = notificationService;
        _announcementService = announcementService;
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // =========================================================================
    // 1. DASHBOARD (/api/resident/dashboard)
    // =========================================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var resident = await _context.Residents
            .AsNoTracking()
            .Include(r => r.ApartmentResidents)
                .ThenInclude(ar => ar.Apartment)
                    .ThenInclude(a => a.Building)
            .FirstOrDefaultAsync(r => r.UserId == user.Id);

        var activeOccupancy = resident?.ApartmentResidents
            .OrderByDescending(ar => ar.IsOwner)
            .ThenByDescending(ar => ar.MoveInDate)
            .FirstOrDefault(ar => ar.MoveOutDate == null);

        var activeApartment = activeOccupancy?.Apartment;
        var householdCount = 0;

        if (activeApartment != null)
        {
            householdCount = await _context.ApartmentResidents
                .AsNoTracking()
                .CountAsync(ar => ar.ApartmentId == activeApartment.ApartmentId && ar.MoveOutDate == null);
        }

        var invoices = await _billingService.GetResidentInvoicesAsync(user.Id);
        decimal totalOutstandingDebt = 0m;
        int unpaidInvoiceCount = 0;
        ResidentInvoiceSummaryDto? latestInvoiceDto = null;

        foreach (var inv in invoices)
        {
            var dto = MapInvoiceSummary(inv);
            totalOutstandingDebt += dto.RemainingAmount;
            if (dto.RemainingAmount > 0) unpaidInvoiceCount++;
            latestInvoiceDto ??= dto;
        }

        var recentRepairs = new List<ResidentRepairSummaryDto>();
        var activeRepairsCount = 0;
        var vehicles = new List<ResidentVehicleDto>();
        var activeVisitors = new List<ResidentVisitorDto>();

        if (resident != null)
        {
            var allRepairs = (await _repairService.GetResidentRequestsAsync(resident.ResidentId)).ToList();
            activeRepairsCount = allRepairs.Count(r =>
                r.Status != RepairRequestStatus.Completed &&
                r.Status != RepairRequestStatus.Cancelled);
            recentRepairs = allRepairs.Take(5).Select(MapRepairSummary).ToList();

            var allVehicles = await _vehicleService.GetVehiclesAsync();
            vehicles = allVehicles.Select(MapVehicle).ToList();

            var allVisitors = await _visitorService.GetResidentVisitorPassesAsync(user.Id);
            activeVisitors = allVisitors.Take(5).Select(MapVisitor).ToList();
        }

        var unreadNotificationsCount = await _notificationService.GetUnreadCountAsync(user.Id);
        var recentNotifications = await _notificationService.GetUserNotificationsAsync(user.Id, page: 1, pageSize: 5);
        var recentAnnouncements = await _announcementService.GetAnnouncementsForResidentAsync(user.Id, take: 4);

        return Ok(new
        {
            userFullName = user.FullName,
            userEmail = user.Email ?? string.Empty,
            residentId = resident?.ResidentId,
            activeApartment = activeApartment == null ? null : new
            {
                apartmentId = activeApartment.ApartmentId,
                apartmentCode = activeApartment.ApartmentCode,
                buildingId = activeApartment.BuildingId,
                buildingCode = activeApartment.Building?.BuildingCode ?? string.Empty,
                buildingName = activeApartment.Building?.BuildingName ?? string.Empty,
                floor = activeApartment.Floor,
                area = activeApartment.Area,
                status = activeApartment.Status,
                relationship = activeOccupancy?.Relationship ?? string.Empty,
                isOwner = activeOccupancy?.IsOwner ?? false,
                moveInDate = activeOccupancy?.MoveInDate.ToString("yyyy-MM-dd")
            },
            householdCount,
            totalOutstandingDebt,
            unpaidInvoiceCount,
            latestInvoice = latestInvoiceDto,
            activeRepairsCount,
            registeredVehiclesCount = vehicles.Count,
            activeParkingCardsCount = vehicles.Count(v => v.ParkingCard?.Status == "Active"),
            unreadNotificationsCount,
            recentRepairs,
            vehicles = vehicles.Take(4).ToList(),
            recentVisitors = activeVisitors,
            recentAnnouncements = recentAnnouncements.Select(MapAnnouncement).ToList(),
            recentNotifications = recentNotifications.Select(MapNotification).ToList()
        });
    }

    // =========================================================================
    // 2. MY APARTMENT & CONTRACTS (/api/resident/apartment)
    // =========================================================================

    [HttpGet("apartment")]
    public async Task<IActionResult> GetMyApartmentAndContracts()
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _context.Residents
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.ApartmentResidents)
                .ThenInclude(ar => ar.Apartment)
                    .ThenInclude(a => a.Building)
            .FirstOrDefaultAsync(r => r.UserId == userId);

        var activeOccupancies = (resident?.ApartmentResidents ?? [])
            .Where(ar => ar.MoveOutDate == null)
            .OrderByDescending(ar => ar.IsOwner)
            .ThenByDescending(ar => ar.MoveInDate)
            .ToList();

        var primaryOccupancy = activeOccupancies.FirstOrDefault();
        var activeApartment = primaryOccupancy?.Apartment;

        var householdMembers = new List<object>();
        if (activeApartment != null)
        {
            var members = await _context.ApartmentResidents
                .AsNoTracking()
                .Include(ar => ar.Resident)
                    .ThenInclude(r => r.User)
                .Where(ar => ar.ApartmentId == activeApartment.ApartmentId && ar.MoveOutDate == null)
                .OrderByDescending(ar => ar.IsOwner)
                .ThenBy(ar => ar.MoveInDate)
                .ToListAsync();

            householdMembers = members.Select(m => (object)new
            {
                apartmentResidentId = m.ApartmentResidentId,
                residentId = m.ResidentId,
                fullName = m.Resident?.User?.FullName ?? string.Empty,
                email = m.Resident?.User?.Email ?? string.Empty,
                relationship = m.Relationship,
                isOwner = m.IsOwner,
                moveInDate = m.MoveInDate.ToString("yyyy-MM-dd")
            }).ToList();
        }

        var occupancyHistory = (resident?.ApartmentResidents ?? [])
            .OrderByDescending(ar => ar.MoveOutDate == null)
            .ThenByDescending(ar => ar.MoveInDate)
            .Select(ar => new
            {
                apartmentResidentId = ar.ApartmentResidentId,
                apartmentId = ar.ApartmentId,
                apartmentCode = ar.Apartment?.ApartmentCode ?? string.Empty,
                buildingName = ar.Apartment?.Building?.BuildingName ?? string.Empty,
                floor = ar.Apartment?.Floor ?? 0,
                area = ar.Apartment?.Area ?? 0m,
                relationship = ar.Relationship,
                isOwner = ar.IsOwner,
                moveInDate = ar.MoveInDate.ToString("yyyy-MM-dd"),
                moveOutDate = ar.MoveOutDate?.ToString("yyyy-MM-dd"),
                isActive = ar.MoveOutDate == null
            })
            .ToList();

        var contracts = await _contractService.GetForResidentUserAsync(userId);
        var contractDtos = contracts.Select(c => new
        {
            contractId = c.ApartmentContractId,
            contractCode = c.ContractCode,
            apartmentId = c.ApartmentId,
            apartmentCode = c.Apartment?.ApartmentCode ?? string.Empty,
            buildingName = c.Apartment?.Building?.BuildingName ?? string.Empty,
            startDate = c.StartDate.ToString("yyyy-MM-dd"),
            endDate = c.EndDate?.ToString("yyyy-MM-dd"),
            monthlyRent = c.MonthlyRent,
            depositAmount = c.DepositAmount,
            status = c.Status.ToString(),
            termsSummary = (string?)null,
            parties = c.Parties.Select(p => new
            {
                residentId = p.ResidentId,
                fullName = p.Resident?.User?.FullName ?? string.Empty,
                role = p.Role.ToString()
            }).ToList(),
            statusHistory = c.StatusHistory
                .OrderByDescending(h => h.ChangedAtUtc)
                .Select(h => new
                {
                    previousStatus = h.PreviousStatus?.ToString(),
                    newStatus = h.NewStatus.ToString(),
                    changedAtUtc = h.ChangedAtUtc.ToString("o"),
                    reason = h.Reason
                }).ToList()
        }).ToList();

        return Ok(new
        {
            hasResidentProfile = resident != null,
            activeApartment = activeApartment == null ? null : new
            {
                apartmentId = activeApartment.ApartmentId,
                apartmentCode = activeApartment.ApartmentCode,
                buildingId = activeApartment.BuildingId,
                buildingCode = activeApartment.Building?.BuildingCode ?? string.Empty,
                buildingName = activeApartment.Building?.BuildingName ?? string.Empty,
                buildingAddress = activeApartment.Building?.Address ?? string.Empty,
                floor = activeApartment.Floor,
                area = activeApartment.Area,
                status = activeApartment.Status,
                relationship = primaryOccupancy?.Relationship ?? string.Empty,
                isOwner = primaryOccupancy?.IsOwner ?? false,
                moveInDate = primaryOccupancy?.MoveInDate.ToString("yyyy-MM-dd")
            },
            householdMembers,
            occupancyHistory,
            contracts = contractDtos
        });
    }

    // =========================================================================
    // 3. INVOICES & VIETQR PAYMENTS (/api/resident/invoices)
    // =========================================================================

    [HttpGet("invoices")]
    public async Task<IActionResult> GetMyInvoices()
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var invoices = await _billingService.GetResidentInvoicesAsync(userId);
        var items = invoices.Select(MapInvoiceSummary).ToList();

        return Ok(new
        {
            totalCount = items.Count,
            totalBilledAmount = items.Sum(x => x.TotalAmount),
            totalConfirmedAmount = items.Sum(x => x.ConfirmedAmount),
            totalRemainingAmount = items.Sum(x => x.RemainingAmount),
            vietQrConfigured = _vietQrOptions.IsConfigured,
            items
        });
    }

    [HttpGet("invoices/{id:int}")]
    public async Task<IActionResult> GetInvoiceDetails(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var invoice = await _billingService.GetResidentInvoiceAsync(userId, id);
        if (invoice == null) return NotFound(new { message = "Không tìm thấy hóa đơn." });

        return Ok(await BuildInvoiceDetailsResponseAsync(userId, invoice));
    }

    [HttpPost("invoices/{id:int}/payment-attempts")]
    public async Task<IActionResult> CreatePaymentAttempt(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (!_vietQrOptions.IsConfigured)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "VietQR chưa khả dụng",
                Detail = "VietQR chưa khả dụng vì chưa cấu hình tài khoản ngân hàng nhận thanh toán.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _paymentService.CreateResidentPaymentAttemptAsync(userId, id);
        if (!result.Success || result.Payment == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể tạo giao dịch thanh toán",
                Detail = result.ErrorMessage ?? "Không thể tạo giao dịch. Hãy xác nhận hóa đơn còn hiệu lực và thuộc tài khoản của bạn.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var invoice = await _billingService.GetResidentInvoiceAsync(userId, id);
        if (invoice == null) return NotFound();

        var details = await BuildInvoiceDetailsResponseAsync(userId, invoice);
        return Ok(new
        {
            message = "Đã tạo lượt thanh toán VietQR mới. Vui lòng quét mã QR hoặc chuyển khoản đúng số tiền và nội dung tham chiếu.",
            invoice = details
        });
    }

    [HttpGet("invoices/transactions/{id:int}/qr")]
    public async Task<IActionResult> GetTransactionQr(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!_vietQrOptions.IsConfigured) return NotFound();

        var payment = await _paymentService.GetResidentPaymentAsync(userId, id);
        if (payment == null ||
            payment.Status != PaymentTransactionStatus.Pending ||
            payment.Amount is <= 0 or > VietQrCodeGenerator.MaximumVndAmount)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return File(_qrCodeGenerator.GeneratePng(payment.Reference, payment.Amount), "image/png");
    }

    // =========================================================================
    // 4. AMENITIES & BOOKING (/api/resident/amenities)
    // =========================================================================

    [HttpGet("amenities")]
    public async Task<IActionResult> GetAmenitiesAndMyBookings()
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var amenities = await _amenityService.GetAllAmenitiesAsync();
        var myBookings = await _amenityService.GetResidentBookingsAsync(userId);

        return Ok(new
        {
            amenities = amenities.Select(a => new
            {
                amenityId = a.AmenityId,
                name = a.Name,
                description = a.Description,
                location = a.Location,
                capacity = a.Capacity,
                openTime = a.OpenTime.ToString(@"hh\:mm"),
                closeTime = a.CloseTime.ToString(@"hh\:mm"),
                isActive = a.IsActive,
                pricePerHour = a.PricePerHour,
                isFree = a.PricePerHour <= 0
            }).ToList(),
            myBookings = myBookings.Select(b =>
            {
                var hours = Math.Max(0, (decimal)(b.EndTime - b.StartTime).TotalHours);
                var pricePerHour = b.Amenity?.PricePerHour ?? 0m;
                return new
                {
                    amenityBookingId = b.AmenityBookingId,
                    amenityId = b.AmenityId,
                    amenityName = b.Amenity?.Name ?? string.Empty,
                    amenityLocation = b.Amenity?.Location ?? string.Empty,
                    bookingDate = b.BookingDate.ToString("yyyy-MM-dd"),
                    startTime = b.StartTime.ToString(@"hh\:mm"),
                    endTime = b.EndTime.ToString(@"hh\:mm"),
                    numberOfGuests = b.NumberOfGuests,
                    status = b.Status,
                    notes = b.Notes,
                    estimatedCost = Math.Round(hours * pricePerHour, 0),
                    createdAtUtc = b.CreatedAtUtc.ToString("o"),
                    canCancel = (b.Status == "Chờ duyệt" || b.Status == "Đã xác nhận") &&
                                b.BookingDate.Date.Add(b.StartTime) > DateTime.Now
                };
            }).ToList()
        });
    }

    [HttpGet("amenities/{id:int}/booked-slots")]
    public async Task<IActionResult> GetAmenityBookedSlots(int id, [FromQuery] DateTime? date)
    {
        if (id <= 0) return BadRequest(new { message = "Mã tiện ích không hợp lệ." });
        var targetDate = (date ?? DateTime.Today.AddDays(1)).Date;

        var bookings = await _amenityService.GetBookingsAsync(id, targetDate);
        var activeSlots = bookings
            .Where(b => b.Status == "Chờ duyệt" || b.Status == "Đã xác nhận")
            .OrderBy(b => b.StartTime)
            .Select(b => new
            {
                bookingId = b.AmenityBookingId,
                startTime = b.StartTime.ToString(@"hh\:mm"),
                endTime = b.EndTime.ToString(@"hh\:mm"),
                guests = b.NumberOfGuests,
                status = b.Status
            })
            .ToList();

        return Ok(activeSlots);
    }

    [HttpPost("amenities/bookings")]
    public async Task<IActionResult> CreateAmenityBooking([FromBody] CreateAmenityBookingDto request)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Giờ bắt đầu không hợp lệ",
                Detail = "Vui lòng chọn giờ bắt đầu theo định dạng HH:mm.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Giờ kết thúc không hợp lệ",
                Detail = "Vui lòng chọn giờ kết thúc theo định dạng HH:mm.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _amenityService.BookAmenityAsync(
            userId,
            request.AmenityId,
            request.BookingDate.Date,
            startTime,
            endTime,
            request.NumberOfGuests,
            request.Notes);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể đặt lịch tiện ích",
                Detail = result.ErrorMessage ?? "Không thể hoàn tất đăng ký tiện ích.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new
        {
            message = "Đăng ký sử dụng tiện ích thành công! Yêu cầu của bạn đang chờ Ban quản lý xác nhận."
        });
    }

    [HttpPost("amenities/bookings/{id:int}/cancel")]
    public async Task<IActionResult> CancelAmenityBooking(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _amenityService.CancelBookingAsync(userId, id, isManager: false);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể hủy lịch đặt tiện ích",
                Detail = result.ErrorMessage ?? "Thao tác hủy lịch đặt không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã hủy lịch đặt chỗ tiện ích thành công." });
    }

    // =========================================================================
    // 5. VEHICLES & PARKING CARDS (/api/resident/vehicles)
    // =========================================================================

    [HttpGet("vehicles")]
    public async Task<IActionResult> GetMyVehicles([FromQuery] string? searchTerm, [FromQuery] string? status)
    {
        var vehicles = await _vehicleService.GetVehiclesAsync(searchTerm, status);
        var apartments = await _residentService.GetApartmentsForCurrentResidentAsync();

        return Ok(new
        {
            vehicles = vehicles.Select(MapVehicle).ToList(),
            apartments = apartments.Select(MapApartmentOption).ToList()
        });
    }

    [HttpPost("vehicles")]
    public async Task<IActionResult> RegisterVehicle([FromBody] VehicleRegisterViewModel model)
    {
        var result = await _vehicleService.RegisterVehicleAsync(model);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể đăng ký phương tiện",
                Detail = result.ErrorMessage ?? "Đăng ký phương tiện không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new
        {
            message = "Đăng ký phương tiện thành công! Đơn đang chờ Ban quản lý xét duyệt và cấp thẻ gửi xe."
        });
    }

    [HttpPost("vehicles/{id:int}/cancel")]
    public async Task<IActionResult> CancelVehicleRegistration(int id)
    {
        if (id <= 0) return NotFound();
        var result = await _vehicleService.CancelRegistrationAsync(id);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể hủy đơn đăng ký xe",
                Detail = result.ErrorMessage ?? "Không thể hủy đơn đăng ký phương tiện.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã hủy đơn đăng ký phương tiện." });
    }

    [HttpPost("vehicles/{id:int}/lock-card")]
    public async Task<IActionResult> LockParkingCard(int id, [FromBody] VehicleActionReasonDto? request)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var vehicle = await _vehicleService.GetVehicleByIdAsync(id);
        if (vehicle == null) return NotFound(new { message = "Không tìm thấy phương tiện." });
        if (vehicle.Resident?.UserId != userId) return Forbid();

        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? "Cư dân báo mất / khóa thẻ khẩn cấp" : request.Reason.Trim();
        var result = await _vehicleService.ReportLostOrLockCardAsync(id, reason, userId);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể khóa thẻ gửi xe",
                Detail = result.ErrorMessage ?? "Không thể khóa thẻ gửi xe.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã khóa thẻ bãi đỗ xe khẩn cấp thành công." });
    }

    [HttpPost("vehicles/{id:int}/decommission")]
    public async Task<IActionResult> DecommissionVehicle(int id, [FromBody] VehicleActionReasonDto? request)
    {
        if (id <= 0) return NotFound();
        var result = await _vehicleService.DecommissionVehicleAsync(id, request?.Reason);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể ngừng sử dụng phương tiện",
                Detail = result.ErrorMessage ?? "Thao tác không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã chuyển phương tiện sang trạng thái Ngừng sử dụng." });
    }

    // =========================================================================
    // 6. VISITORS (/api/resident/visitors)
    // =========================================================================

    [HttpGet("visitors")]
    public async Task<IActionResult> GetMyVisitors()
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var passes = await _visitorService.GetResidentVisitorPassesAsync(userId);
        var apartments = await _residentService.GetApartmentsForCurrentResidentAsync();

        return Ok(new
        {
            visitors = passes.Select(MapVisitor).ToList(),
            apartments = apartments.Select(MapApartmentOption).ToList()
        });
    }

    [HttpPost("visitors")]
    public async Task<IActionResult> RegisterVisitor([FromBody] RegisterVisitorDto request)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _visitorService.RegisterVisitorAsync(
            userId,
            request.ApartmentId,
            request.VisitorName,
            request.PhoneNumber,
            request.IdCardNumber,
            request.ExpectedArrivalDate,
            request.Purpose);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể đăng ký khách thăm",
                Detail = result.ErrorMessage ?? "Đăng ký khách đến thăm không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new
        {
            message = "Đăng ký khách đến thăm thành công! Bạn sẽ nhận thông báo tức thời khi khách làm thủ tục Check-in tại sảnh."
        });
    }

    [HttpPost("visitors/{id:int}/cancel")]
    public async Task<IActionResult> CancelVisitor(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _visitorService.CancelVisitorPassAsync(userId, id, isManager: false);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể hủy đăng ký khách",
                Detail = result.ErrorMessage ?? "Không thể hủy lịch hẹn khách thăm.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã hủy lịch hẹn khách thăm thành công." });
    }

    // =========================================================================
    // 7. REPAIR REQUESTS (/api/resident/repairs)
    // =========================================================================

    [HttpGet("repairs")]
    public async Task<IActionResult> GetMyRepairs()
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _residentService.GetByUserIdAsync(userId);
        if (resident == null)
        {
            return Ok(new
            {
                repairs = Array.Empty<ResidentRepairSummaryDto>(),
                apartments = Array.Empty<ResidentApartmentOptionDto>()
            });
        }

        var requests = await _repairService.GetResidentRequestsAsync(resident.ResidentId);
        var apartments = await _repairService.GetActiveApartmentsForResidentAsync(resident.ResidentId);

        return Ok(new
        {
            repairs = requests.Select(MapRepairSummary).ToList(),
            apartments = apartments.Select(MapApartmentOption).ToList()
        });
    }

    [HttpGet("repairs/{id:int}")]
    public async Task<IActionResult> GetRepairDetails(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _residentService.GetByUserIdAsync(userId);
        if (resident == null) return NotFound();

        var request = await _repairService.GetRequestByIdForResidentAsync(id, resident.ResidentId);
        if (request == null) return NotFound(new { message = "Không tìm thấy yêu cầu sửa chữa." });

        return Ok(MapRepairDetails(request));
    }

    [HttpPost("repairs")]
    public async Task<IActionResult> CreateRepairRequest([FromBody] CreateRepairRequestDto request)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _residentService.GetByUserIdAsync(userId);
        if (resident == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chưa có hồ sơ cư dân",
                Detail = "Không tìm thấy hồ sơ cư dân liên kết với tài khoản của bạn.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _repairService.CreateRequestAsync(
            resident.ResidentId,
            request.ApartmentId,
            request.Title,
            request.Description,
            request.Priority);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể gửi yêu cầu sửa chữa",
                Detail = result.ErrorMessage ?? "Gửi yêu cầu sửa chữa không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new
        {
            requestId = result.RequestId,
            message = "Gửi yêu cầu sửa chữa thành công! Ban Quản Lý sẽ sớm tiếp nhận và phân công kỹ thuật viên."
        });
    }

    [HttpPost("repairs/{id:int}/cancel")]
    public async Task<IActionResult> CancelRepairRequest(int id, [FromBody] CancelRepairRequestDto request)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _residentService.GetByUserIdAsync(userId);
        if (resident == null) return NotFound();

        if (!TryDecodeRowVersion(request.RowVersionBase64, out var rowVersion))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Phiên bản dữ liệu không hợp lệ",
                Detail = "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _repairService.CancelRequestAsync(id, resident.ResidentId, request.Reason, rowVersion);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể hủy yêu cầu sửa chữa",
                Detail = result.ErrorMessage ?? "Không thể hủy yêu cầu sửa chữa.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã hủy yêu cầu sửa chữa thành công." });
    }

    [HttpPost("repairs/{id:int}/confirm")]
    public async Task<IActionResult> ConfirmRepairCompletion(int id, [FromBody] ConfirmRepairCompletionDto request)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _residentService.GetByUserIdAsync(userId);
        if (resident == null) return NotFound();

        if (!TryDecodeRowVersion(request.RowVersionBase64, out var rowVersion))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Phiên bản dữ liệu không hợp lệ",
                Detail = "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _repairService.ConfirmCompletionAsync(
            id,
            resident.ResidentId,
            request.Rating,
            request.Feedback,
            rowVersion);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể nghiệm thu yêu cầu sửa chữa",
                Detail = result.ErrorMessage ?? "Thao tác nghiệm thu không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Cảm ơn bạn đã nghiệm thu và đánh giá chất lượng sửa chữa!" });
    }

    [HttpPost("repairs/{id:int}/request-redo")]
    public async Task<IActionResult> RequestRepairRedo(int id, [FromBody] RedoRepairRequestDto request)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var resident = await _residentService.GetByUserIdAsync(userId);
        if (resident == null) return NotFound();

        if (!TryDecodeRowVersion(request.RowVersionBase64, out var rowVersion))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Phiên bản dữ liệu không hợp lệ",
                Detail = "Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _repairService.RequestRedoAsync(
            id,
            resident.ResidentId,
            request.RedoReason,
            rowVersion);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể gửi yêu cầu xử lý lại",
                Detail = result.ErrorMessage ?? "Thao tác yêu cầu xử lý lại không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã gửi yêu cầu xử lý lại tới Ban Quản Lý." });
    }

    // =========================================================================
    // 8. COMPLAINTS (/api/resident/complaints)
    // =========================================================================

    [HttpGet("complaints")]
    public async Task<IActionResult> GetMyComplaints(
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? category)
    {
        var complaints = await _complaintService.GetComplaintsAsync(searchTerm, status, category);
        var apartments = await _residentService.GetApartmentsForCurrentResidentAsync();

        return Ok(new
        {
            complaints = complaints.Select(MapComplaint).ToList(),
            apartments = apartments.Select(MapApartmentOption).ToList()
        });
    }

    [HttpGet("complaints/{id:int}")]
    public async Task<IActionResult> GetComplaintDetails(int id)
    {
        if (id <= 0) return NotFound();
        var complaint = await _complaintService.GetComplaintByIdAsync(id);
        if (complaint == null) return NotFound(new { message = "Không tìm thấy phản ánh." });
        return Ok(MapComplaint(complaint));
    }

    [HttpPost("complaints")]
    public async Task<IActionResult> CreateComplaint([FromBody] ComplaintCreateViewModel model)
    {
        var result = await _complaintService.CreateComplaintAsync(model);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể gửi phản ánh",
                Detail = result.ErrorMessage ?? "Gửi phản ánh không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Gửi phản ánh thành công! Ban Quản Lý sẽ sớm tiếp nhận và phản hồi." });
    }

    [HttpPut("complaints/{id:int}")]
    public async Task<IActionResult> UpdateComplaint(int id, [FromBody] ComplaintEditViewModel model)
    {
        if (id <= 0) return NotFound();
        model.ComplaintId = id;

        var result = await _complaintService.UpdateComplaintAsync(model);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể cập nhật phản ánh",
                Detail = result.ErrorMessage ?? "Cập nhật phản ánh không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Cập nhật nội dung phản ánh thành công!" });
    }

    [HttpPost("complaints/{id:int}/cancel")]
    public async Task<IActionResult> CancelComplaint(int id)
    {
        if (id <= 0) return NotFound();
        var result = await _complaintService.CancelComplaintAsync(id);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể rút phản ánh",
                Detail = result.ErrorMessage ?? "Rút phản ánh không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Đã rút phản ánh thành công." });
    }

    [HttpPost("complaints/{id:int}/rate")]
    public async Task<IActionResult> RateComplaint(int id, [FromBody] RateComplaintDto request)
    {
        if (id <= 0) return NotFound();
        var result = await _complaintService.RateComplaintAsync(id, request.Rating, request.Feedback);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể gửi đánh giá",
                Detail = result.ErrorMessage ?? "Đánh giá phản ánh không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Cảm ơn bạn đã đánh giá kết quả xử lý phản ánh!" });
    }

    // =========================================================================
    // 9. NOTIFICATIONS & ANNOUNCEMENTS (/api/resident/notifications)
    // =========================================================================

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotificationsAndAnnouncements(
        [FromQuery] int page = 1,
        [FromQuery] bool? unreadOnly = null)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var safePage = Math.Max(1, page);
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, safePage, pageSize: 20, unreadOnly);
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        var announcements = await _announcementService.GetAnnouncementsForResidentAsync(userId, take: 20);

        return Ok(new
        {
            page = safePage,
            unreadCount,
            notifications = notifications.Select(MapNotification).ToList(),
            announcements = announcements.Select(MapAnnouncement).ToList()
        });
    }

    [HttpDelete("notifications/{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        if (id <= 0) return NotFound();
        var userId = CurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _notificationService.DeleteNotificationAsync(id, userId);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể xóa thông báo",
                Detail = result.ErrorMessage ?? "Xóa thông báo không thành công.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { message = "Đã xóa thông báo.", unreadCount });
    }

    // =========================================================================
    // 10. PROFILE & ACCOUNT SECURITY (/api/resident/profile)
    // =========================================================================

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var resident = await _context.Residents
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == user.Id);

        if (resident == null)
        {
            return NotFound(new { message = "Không tìm thấy hồ sơ cư dân." });
        }

        return Ok(new
        {
            residentId = resident.ResidentId,
            fullName = user.FullName,
            email = user.Email ?? string.Empty,
            userName = user.UserName ?? string.Empty,
            phoneNumber = user.PhoneNumber ?? string.Empty,
            citizenId = resident.CitizenId,
            dateOfBirth = resident.DateOfBirth?.ToString("yyyy-MM-dd"),
            gender = resident.Gender ?? string.Empty,
            address = resident.Address ?? string.Empty,
            emergencyContact = resident.EmergencyContact ?? string.Empty
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateResidentProfileDto request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var editModel = new ResidentProfileEditViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address,
            EmergencyContact = request.EmergencyContact
        };

        var result = await _residentService.UpdateProfileAsync(user.Id, editModel);
        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể cập nhật hồ sơ",
                Detail = result.ErrorMessage ?? "Dữ liệu hồ sơ không hợp lệ.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = "Cập nhật thông tin hồ sơ cư dân thành công." });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ResidentChangePasswordDto request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var changeResult = await _userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!changeResult.Succeeded)
        {
            var errorDetail = string.Join(" ", changeResult.Errors.Select(e => e.Description));
            return BadRequest(new ProblemDetails
            {
                Title = "Không thể đổi mật khẩu",
                Detail = string.IsNullOrWhiteSpace(errorDetail)
                    ? "Mật khẩu hiện tại không đúng hoặc mật khẩu mới chưa đạt yêu cầu bảo mật."
                    : errorDetail,
                Status = StatusCodes.Status400BadRequest
            });
        }

        await _signInManager.RefreshSignInAsync(user);
        return Ok(new { message = "Đổi mật khẩu thành công!" });
    }

    // =========================================================================
    // MAPPING HELPERS
    // =========================================================================

    private static ResidentInvoiceSummaryDto MapInvoiceSummary(ApartmentInvoice inv)
    {
        var transactions = inv.PaymentTransactions ?? [];
        var confirmed = transactions
            .Where(t => t.Status == PaymentTransactionStatus.Confirmed)
            .Sum(t => t.Amount);
        var remaining = inv.Status == ApartmentInvoiceStatus.Void
            ? 0m
            : Math.Max(0m, inv.TotalAmount - confirmed);
        var hasPending = transactions.Any(t => t.Status == PaymentTransactionStatus.Pending);

        string paymentState;
        if (inv.Status == ApartmentInvoiceStatus.Void)
        {
            paymentState = "Void";
        }
        else if (remaining <= 0 && inv.TotalAmount > 0)
        {
            paymentState = "Paid";
        }
        else if (confirmed > 0)
        {
            paymentState = "Partial";
        }
        else if (inv.DueDate.Date < DateTime.Today)
        {
            paymentState = "Overdue";
        }
        else
        {
            paymentState = "Unpaid";
        }

        return new ResidentInvoiceSummaryDto
        {
            ApartmentInvoiceId = inv.ApartmentInvoiceId,
            InvoiceCode = inv.InvoiceCode,
            ApartmentId = inv.ApartmentId,
            ApartmentCode = inv.Apartment?.ApartmentCode ?? string.Empty,
            BuildingName = inv.Apartment?.Building?.BuildingName ?? string.Empty,
            BillingYear = inv.BillingYear,
            BillingMonth = inv.BillingMonth,
            PeriodLabel = $"Tháng {inv.BillingMonth:D2}/{inv.BillingYear}",
            DueDate = inv.DueDate.ToString("yyyy-MM-dd"),
            TotalAmount = inv.TotalAmount,
            ConfirmedAmount = confirmed,
            RemainingAmount = remaining,
            Status = inv.Status.ToString(),
            PaymentState = paymentState,
            HasPendingTransaction = hasPending,
            IssuedAtUtc = inv.IssuedAtUtc?.ToString("o")
        };
    }

    private async Task<object> BuildInvoiceDetailsResponseAsync(string userId, ApartmentInvoice invoice)
    {
        var transactions = (invoice.PaymentTransactions ?? [])
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.PaymentTransactionId)
            .ToList();

        var activePayment = await _paymentService.GetResidentPaymentForInvoiceAsync(userId, invoice.ApartmentInvoiceId)
            ?? transactions
                .OrderByDescending(x => x.Status == PaymentTransactionStatus.Confirmed)
                .ThenByDescending(x => x.Status == PaymentTransactionStatus.Pending)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.PaymentTransactionId)
                .FirstOrDefault();

        var confirmedAmount = transactions
            .Where(x => x.Status == PaymentTransactionStatus.Confirmed)
            .Sum(x => x.Amount);

        var remainingAmount = invoice.Status == ApartmentInvoiceStatus.Void
            ? 0m
            : Math.Max(0m, invoice.TotalAmount - confirmedAmount);

        var vietQrAmountSupported = invoice.TotalAmount is > 0 and <= VietQrCodeGenerator.MaximumVndAmount &&
                                    (remainingAmount <= 0 || remainingAmount <= VietQrCodeGenerator.MaximumVndAmount);

        var canCreatePaymentAttempt = invoice.Status == ApartmentInvoiceStatus.Issued &&
                                      remainingAmount > 0 &&
                                      _vietQrOptions.IsConfigured &&
                                      vietQrAmountSupported;

        var summary = MapInvoiceSummary(invoice);

        return new
        {
            summary,
            apartmentArea = invoice.ApartmentArea,
            lines = (invoice.Lines ?? [])
                .OrderBy(l => l.ChargeType)
                .Select(l => new
                {
                    lineId = l.ApartmentInvoiceLineId,
                    chargeType = l.ChargeType.ToString(),
                    chargeTypeLabel = l.ChargeType switch
                    {
                        FeeChargeType.Management => "Phí quản lý & gửi xe",
                        FeeChargeType.Electricity => "Tiền điện sinh hoạt",
                        FeeChargeType.Water => "Tiền nước sinh hoạt",
                        _ => l.ChargeType.ToString()
                    },
                    description = l.Description,
                    quantity = l.Quantity,
                    unitRate = l.UnitRate,
                    amount = l.Amount,
                    previousReading = l.PreviousReading,
                    currentReading = l.CurrentReading,
                    rateBreakdown = l.RateBreakdown
                }).ToList(),
            transactions = transactions.Select(t => new
            {
                paymentTransactionId = t.PaymentTransactionId,
                reference = t.Reference,
                amount = t.Amount,
                status = t.Status.ToString(),
                createdAtUtc = t.CreatedAtUtc.ToString("o"),
                confirmedAtUtc = t.ConfirmedAtUtc?.ToString("o"),
                rejectedAtUtc = t.RejectedAtUtc?.ToString("o"),
                rejectionReason = t.RejectionReason,
                isAutoClosedRejection = t.Status == PaymentTransactionStatus.Rejected &&
                                        PaymentDisplayHelper.IsAutoClosedRejectionReason(t.RejectionReason),
                qrImageUrl = t.Status == PaymentTransactionStatus.Pending && _vietQrOptions.IsConfigured
                    ? $"/api/resident/invoices/transactions/{t.PaymentTransactionId}/qr"
                    : null
            }).ToList(),
            activePayment = activePayment == null ? null : new
            {
                paymentTransactionId = activePayment.PaymentTransactionId,
                reference = activePayment.Reference,
                amount = activePayment.Amount,
                status = activePayment.Status.ToString(),
                createdAtUtc = activePayment.CreatedAtUtc.ToString("o"),
                qrImageUrl = activePayment.Status == PaymentTransactionStatus.Pending && _vietQrOptions.IsConfigured
                    ? $"/api/resident/invoices/transactions/{activePayment.PaymentTransactionId}/qr"
                    : null
            },
            vietQr = new
            {
                configured = _vietQrOptions.IsConfigured,
                amountSupported = vietQrAmountSupported,
                canCreatePaymentAttempt,
                bankBin = _vietQrOptions.BankBin,
                accountNumber = _vietQrOptions.AccountNumber,
                accountName = _vietQrOptions.AccountName
            }
        };
    }

    private static ResidentVehicleDto MapVehicle(Vehicle v) => new()
    {
        VehicleId = v.VehicleId,
        ApartmentId = v.ApartmentId,
        ApartmentCode = v.Apartment?.ApartmentCode ?? string.Empty,
        VehicleType = v.VehicleType,
        LicensePlate = v.LicensePlate,
        BrandModel = v.BrandModel,
        Color = v.Color,
        Status = v.Status,
        CreatedAt = v.CreatedAt.ToString("o"),
        ReviewedAt = v.ReviewedAt?.ToString("o"),
        ReviewNotes = v.ReviewNotes,
        ParkingCard = v.ParkingCard == null ? null : new ResidentParkingCardDto
        {
            ParkingCardId = v.ParkingCard.ParkingCardId,
            CardCode = v.ParkingCard.CardCode,
            Status = v.ParkingCard.Status,
            IssuedAtUtc = v.ParkingCard.IssuedAtUtc.ToString("o")
        }
    };

    private static ResidentVisitorDto MapVisitor(VisitorPass v) => new()
    {
        VisitorPassId = v.VisitorPassId,
        ApartmentId = v.ApartmentId,
        ApartmentCode = v.Apartment?.ApartmentCode ?? string.Empty,
        VisitorName = v.VisitorName,
        PhoneNumber = v.PhoneNumber,
        IdCardNumber = v.IdCardNumber,
        ExpectedArrivalDate = v.ExpectedArrivalDate.ToString("yyyy-MM-dd"),
        ActualCheckInUtc = v.ActualCheckInUtc?.ToString("o"),
        ActualCheckOutUtc = v.ActualCheckOutUtc?.ToString("o"),
        Status = v.Status,
        Purpose = v.Purpose,
        CreatedAtUtc = v.CreatedAtUtc.ToString("o"),
        CanCancel = v.Status == "Đã đăng ký"
    };

    private static ResidentRepairSummaryDto MapRepairSummary(RepairRequest r) => new()
    {
        RepairRequestId = r.RepairRequestId,
        ApartmentId = r.ApartmentId,
        ApartmentCode = r.Apartment?.ApartmentCode ?? string.Empty,
        BuildingName = r.Apartment?.Building?.BuildingName ?? string.Empty,
        Title = r.Title,
        Description = r.Description,
        Priority = r.Priority,
        Status = r.Status,
        Result = r.Result,
        CreatedAt = r.CreatedAt.ToString("o"),
        CompletedAt = r.CompletedAt?.ToString("o"),
        ConfirmedAt = r.ConfirmedAt?.ToString("o"),
        Rating = r.Rating,
        ResidentFeedback = r.ResidentFeedback,
        RowVersionBase64 = Convert.ToBase64String(r.RowVersion ?? [])
    };

    private static object MapRepairDetails(RepairRequest r) => new
    {
        repairRequestId = r.RepairRequestId,
        apartmentId = r.ApartmentId,
        apartmentCode = r.Apartment?.ApartmentCode ?? string.Empty,
        buildingName = r.Apartment?.Building?.BuildingName ?? string.Empty,
        title = r.Title,
        description = r.Description,
        priority = r.Priority,
        status = r.Status,
        result = r.Result,
        createdAt = r.CreatedAt.ToString("o"),
        completedAt = r.CompletedAt?.ToString("o"),
        confirmedAt = r.ConfirmedAt?.ToString("o"),
        rating = r.Rating,
        residentFeedback = r.ResidentFeedback,
        rowVersionBase64 = Convert.ToBase64String(r.RowVersion ?? []),
        assignments = (r.Assignments ?? [])
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new
            {
                assignmentId = a.AssignmentId,
                technicianName = a.TechnicianUser?.FullName ?? a.TechnicianUser?.UserName ?? "Kỹ thuật viên",
                assignedAt = a.AssignedAt.ToString("o"),
                completedAt = a.CompletedAt?.ToString("o"),
                status = a.Status,
                note = a.Note
            }).ToList(),
        statusHistory = (r.StatusHistory ?? [])
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new
            {
                historyId = h.RepairStatusHistoryId,
                previousStatus = h.PreviousStatus,
                newStatus = h.NewStatus,
                changedAt = h.ChangedAt.ToString("o"),
                changedByName = h.ChangedByUser?.FullName ?? "Hệ thống",
                note = h.Note
            }).ToList()
    };

    private static object MapComplaint(Complaint c) => new
    {
        complaintId = c.ComplaintId,
        apartmentId = c.ApartmentId,
        apartmentCode = c.Apartment?.ApartmentCode,
        buildingName = c.Apartment?.Building?.BuildingName,
        title = c.Title,
        content = c.Content,
        category = c.Category,
        priority = c.Priority,
        status = c.Status,
        createdAt = c.CreatedAt.ToString("o"),
        resolvedAt = c.ResolvedAt?.ToString("o"),
        responseNotes = c.ResponseNotes,
        rating = c.Rating,
        residentFeedback = c.ResidentFeedback,
        ratedAt = c.RatedAt?.ToString("o"),
        canEdit = c.Status == "Chờ tiếp nhận",
        canCancel = c.Status == "Chờ tiếp nhận",
        canRate = c.Status == "Đã xử lý" && !c.Rating.HasValue
    };

    private static object MapNotification(Notification n) => new
    {
        id = n.NotificationId,
        title = n.Title,
        content = n.Content,
        type = n.Type,
        isRead = n.IsRead,
        targetUrl = n.TargetUrl,
        createdAtUtc = n.CreatedAtUtc.ToString("o"),
        displayDate = n.CreatedAtUtc.ToString("dd/MM/yyyy HH:mm")
    };

    private static object MapAnnouncement(Announcement a) => new
    {
        announcementId = a.AnnouncementId,
        title = a.Title,
        content = a.Content,
        category = a.Category,
        isPinned = a.IsPinned,
        buildingId = a.BuildingId,
        buildingName = a.Building?.BuildingName ?? "Toàn khu đô thị",
        createdAtUtc = a.CreatedAtUtc.ToString("o"),
        displayDate = a.CreatedAtUtc.ToString("dd/MM/yyyy HH:mm")
    };

    private static ResidentApartmentOptionDto MapApartmentOption(Apartment a) => new()
    {
        ApartmentId = a.ApartmentId,
        ApartmentCode = a.ApartmentCode,
        BuildingName = a.Building?.BuildingName ?? string.Empty,
        Label = a.Building != null
            ? $"{a.ApartmentCode} — {a.Building.BuildingName}"
            : a.ApartmentCode
    };

    private static bool TryDecodeRowVersion(string? token, out byte[]? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            rowVersion = null;
            return true;
        }

        try
        {
            rowVersion = Convert.FromBase64String(token.Trim());
            return true;
        }
        catch (FormatException)
        {
            rowVersion = null;
            return false;
        }
    }
}

public sealed class ResidentApartmentOptionDto
{
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class ResidentInvoiceSummaryDto
{
    public int ApartmentInvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public int BillingYear { get; set; }
    public int BillingMonth { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentState { get; set; } = string.Empty;
    public bool HasPendingTransaction { get; set; }
    public string? IssuedAtUtc { get; set; }
}

public sealed class ResidentVehicleDto
{
    public int VehicleId { get; set; }
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string? BrandModel { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public ResidentParkingCardDto? ParkingCard { get; set; }
}

public sealed class ResidentParkingCardDto
{
    public int ParkingCardId { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string IssuedAtUtc { get; set; } = string.Empty;
}

public sealed class ResidentVisitorDto
{
    public int VisitorPassId { get; set; }
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public string VisitorName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? IdCardNumber { get; set; }
    public string ExpectedArrivalDate { get; set; } = string.Empty;
    public string? ActualCheckInUtc { get; set; }
    public string? ActualCheckOutUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string CreatedAtUtc { get; set; } = string.Empty;
    public bool CanCancel { get; set; }
}

public sealed class ResidentRepairSummaryDto
{
    public int RepairRequestId { get; set; }
    public int ApartmentId { get; set; }
    public string ApartmentCode { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
    public string? ConfirmedAt { get; set; }
    public byte? Rating { get; set; }
    public string? ResidentFeedback { get; set; }
    public string RowVersionBase64 { get; set; } = string.Empty;
}

public sealed class CreateAmenityBookingDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tiện ích.")]
    public int AmenityId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày sử dụng.")]
    public DateTime BookingDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    public string StartTime { get; set; } = "08:00";

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    public string EndTime { get; set; } = "10:00";

    [Range(1, 1000, ErrorMessage = "Số lượng khách tham gia phải từ 1 người trở lên.")]
    public int NumberOfGuests { get; set; } = 1;

    [StringLength(500, ErrorMessage = "Ghi chú không quá 500 ký tự.")]
    public string? Notes { get; set; }
}

public sealed class VehicleActionReasonDto
{
    [StringLength(500)]
    public string? Reason { get; set; }
}

public sealed class RegisterVisitorDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn căn hộ.")]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên khách.")]
    [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự.")]
    public string VisitorName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(20)]
    public string? IdCardNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày dự kiến đến.")]
    public DateTime ExpectedArrivalDate { get; set; } = DateTime.Today;

    [StringLength(500, ErrorMessage = "Mục đích không quá 500 ký tự.")]
    public string? Purpose { get; set; }
}

public sealed class CreateRepairRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn căn hộ.")]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề yêu cầu.")]
    [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết sự cố.")]
    [StringLength(4000, ErrorMessage = "Mô tả không quá 4000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn mức độ ưu tiên.")]
    [StringLength(30)]
    public string Priority { get; set; } = RepairPriority.Normal;
}

public sealed class CancelRepairRequestDto
{
    [StringLength(1000)]
    public string? Reason { get; set; }

    public string? RowVersionBase64 { get; set; }
}

public sealed class ConfirmRepairCompletionDto
{
    [Range(1, 5, ErrorMessage = "Vui lòng đánh giá từ 1 đến 5 sao.")]
    public byte Rating { get; set; } = 5;

    [StringLength(1000, ErrorMessage = "Nhận xét không quá 1000 ký tự.")]
    public string? Feedback { get; set; }

    public string? RowVersionBase64 { get; set; }
}

public sealed class RedoRepairRequestDto
{
    [Required(ErrorMessage = "Vui lòng nhập lý do yêu cầu xử lý lại.")]
    [StringLength(1000, ErrorMessage = "Lý do không quá 1000 ký tự.")]
    public string RedoReason { get; set; } = string.Empty;

    public string? RowVersionBase64 { get; set; }
}

public sealed class RateComplaintDto
{
    [Range(1, 5, ErrorMessage = "Vui lòng đánh giá từ 1 đến 5 sao.")]
    public int Rating { get; set; } = 5;

    [StringLength(1000, ErrorMessage = "Nhận xét không quá 1000 ký tự.")]
    public string? Feedback { get; set; }
}

public sealed class UpdateResidentProfileDto
{
    [DataType(DataType.Date), ValidDateOfBirth]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(ResidentValidation.GenderMaxLength)]
    public string? Gender { get; set; }

    [StringLength(ResidentValidation.AddressMaxLength)]
    public string? Address { get; set; }

    [StringLength(ResidentValidation.PhoneMaxLength)]
    [RegularExpression(ResidentValidation.PhonePattern, ErrorMessage = "Số điện thoại phải gồm 9-15 chữ số và có thể bắt đầu bằng dấu +.")]
    public string? EmergencyContact { get; set; }
}

public sealed class ResidentChangePasswordDto
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải từ 8 ký tự trở lên.")]
    public string NewPassword { get; set; } = string.Empty;
}
