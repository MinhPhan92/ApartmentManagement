using System.Reflection;
using ApartmentManagement.Common.Security;
using ApartmentManagement.Controllers;
using ApartmentManagement.Models;
using ApartmentManagement.Services;
using ApartmentManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace ApartmentManagement.Tests;

public sealed class AmenityControllerTests
{
    private readonly Mock<IAmenityService> _amenityServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly AmenityBookingController _bookingController;
    private readonly AmenityManagementController _managementController;
    private readonly ITempDataDictionary _tempData;

    public AmenityControllerTests()
    {
        _amenityServiceMock = new Mock<IAmenityService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();

        var httpContext = new DefaultHttpContext();
        _tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        _bookingController = new AmenityBookingController(
            _amenityServiceMock.Object,
            _currentUserServiceMock.Object)
        {
            TempData = _tempData
        };

        _managementController = new AmenityManagementController(
            _amenityServiceMock.Object,
            _currentUserServiceMock.Object)
        {
            TempData = _tempData
        };
    }

    #region Security & Metadata Tests

    [Fact]
    public void AmenityBookingController_HasAuthorizeAttribute()
    {
        var attr = typeof(AmenityBookingController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AmenityManagementController_HasRequireManagementPolicy()
    {
        var attr = typeof(AmenityManagementController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(AppPolicies.RequireManagement, attr.Policy);
    }

    #endregion

    #region AmenityBookingController Tests

    [Fact]
    public async Task Index_ReturnsViewWithActiveAmenities()
    {
        var list = new List<Amenity>
        {
            new() { AmenityId = 1, Name = "Hồ bơi", IsActive = true },
            new() { AmenityId = 2, Name = "Sân Tennis", IsActive = true }
        };
        _amenityServiceMock.Setup(s => s.GetAllAmenitiesAsync()).ReturnsAsync(list);

        var result = await _bookingController.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Amenity>>(viewResult.Model);
        Assert.Equal(2, model.Count());
    }

    [Fact]
    public async Task Create_Get_WhenAmenityDoesNotExist_ReturnsNotFound()
    {
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(999)).ReturnsAsync((Amenity?)null);

        var result = await _bookingController.Create(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Create_Get_WhenAmenityInactive_SetsErrorAndRedirectsToIndex()
    {
        var amenity = new Amenity { AmenityId = 1, Name = "Hồ bơi", IsActive = false };
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(1)).ReturnsAsync(amenity);

        var result = await _bookingController.Create(1);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityBookingController.Index), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task Create_Get_WhenAmenityActive_ReturnsViewWithPrepopulatedViewModel()
    {
        var amenity = new Amenity
        {
            AmenityId = 1,
            Name = "Phòng Gym",
            PricePerHour = 50000,
            OpenTime = new TimeSpan(6, 0, 0),
            CloseTime = new TimeSpan(22, 0, 0),
            Capacity = 20,
            IsActive = true
        };
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(1)).ReturnsAsync(amenity);

        var result = await _bookingController.Create(1);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AmenityBookingCreateViewModel>(viewResult.Model);
        Assert.Equal(1, model.AmenityId);
        Assert.Equal("Phòng Gym", model.AmenityName);
        Assert.Equal(50000, model.PricePerHour);
        Assert.Equal(20, model.Capacity);
    }

    [Fact]
    public async Task Create_Post_InvalidModelState_ReturnsViewWithReloadedAmenity()
    {
        var amenity = new Amenity { AmenityId = 1, Name = "Phòng Gym", IsActive = true };
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(1)).ReturnsAsync(amenity);
        _currentUserServiceMock.Setup(u => u.UserId).Returns("resident-user-123");

        _bookingController.ModelState.AddModelError("BookingDate", "Vui lòng chọn ngày");

        var model = new AmenityBookingCreateViewModel { AmenityId = 1 };
        var result = await _bookingController.Create(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal(model, viewResult.Model);
        Assert.Equal("Phòng Gym", model.AmenityName);
    }

    [Fact]
    public async Task Create_Post_WhenCurrentUserNull_ReturnsForbid()
    {
        _currentUserServiceMock.Setup(u => u.UserId).Returns((string?)null);

        var model = new AmenityBookingCreateViewModel
        {
            AmenityId = 1,
            BookingDate = DateTime.Today.AddDays(1),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            NumberOfGuests = 2
        };

        var result = await _bookingController.Create(model);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_Post_WhenValid_CallsServiceAndRedirectsToMyBookings()
    {
        const string userId = "resident-user-123";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        var amenity = new Amenity { AmenityId = 1, Name = "Phòng Gym", IsActive = true };
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(1)).ReturnsAsync(amenity);

        _amenityServiceMock.Setup(s => s.BookAmenityAsync(
            userId,
            1,
            It.IsAny<DateTime>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<TimeSpan>(),
            2,
            "Ghi chú"))
            .ReturnsAsync((true, null));

        var model = new AmenityBookingCreateViewModel
        {
            AmenityId = 1,
            BookingDate = DateTime.Today.AddDays(1),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            NumberOfGuests = 2,
            Notes = "Ghi chú"
        };

        var result = await _bookingController.Create(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityBookingController.MyBookings), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task Create_Post_WhenServiceFails_AddsModelErrorAndReturnsView()
    {
        const string userId = "resident-user-123";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        var amenity = new Amenity { AmenityId = 1, Name = "Hồ bơi", IsActive = true };
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(1)).ReturnsAsync(amenity);

        _amenityServiceMock.Setup(s => s.BookAmenityAsync(
            userId,
            1,
            It.IsAny<DateTime>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<TimeSpan>(),
            10,
            null))
            .ReturnsAsync((false, "Khung giờ này đã vượt quá công suất phục vụ."));

        var model = new AmenityBookingCreateViewModel
        {
            AmenityId = 1,
            BookingDate = DateTime.Today.AddDays(1),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            NumberOfGuests = 10
        };

        var result = await _bookingController.Create(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(_bookingController.ModelState.IsValid);
        Assert.Contains(_bookingController.ModelState.Values, v => v.Errors.Any(e => e.ErrorMessage.Contains("vượt quá công suất")));
    }

    [Fact]
    public async Task MyBookings_WhenLoggedIn_ReturnsViewWithUserBookings()
    {
        const string userId = "resident-user-123";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        var bookings = new List<AmenityBooking>
        {
            new() { AmenityBookingId = 1, AmenityId = 1, Status = "Đã xác nhận" },
            new() { AmenityBookingId = 2, AmenityId = 2, Status = "Chờ duyệt" }
        };
        _amenityServiceMock.Setup(s => s.GetResidentBookingsAsync(userId)).ReturnsAsync(bookings);

        var result = await _bookingController.MyBookings();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<AmenityBooking>>(viewResult.Model);
        Assert.Equal(2, model.Count());
    }

    [Fact]
    public async Task Cancel_WhenValid_CallsServiceAndRedirectsToMyBookings()
    {
        const string userId = "resident-user-123";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        _amenityServiceMock.Setup(s => s.CancelBookingAsync(userId, 10, false, null))
            .ReturnsAsync((true, null));

        var result = await _bookingController.Cancel(10);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityBookingController.MyBookings), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.CancelBookingAsync(userId, 10, false, null), Times.Once);
    }

    [Fact]
    public async Task Cancel_WhenFailed_SetsErrorMessageAndRedirectsToMyBookings()
    {
        const string userId = "resident-user-123";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        _amenityServiceMock.Setup(s => s.CancelBookingAsync(userId, 10, false, null))
            .ReturnsAsync((false, "Không thể hủy lịch đã qua."));

        var result = await _bookingController.Cancel(10);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityBookingController.MyBookings), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task GetBookedSlots_ReturnsJsonResultWithSlots()
    {
        var targetDate = new DateTime(2026, 11, 10);
        var bookings = new List<AmenityBooking>
        {
            new()
            {
                AmenityBookingId = 1,
                AmenityId = 1,
                BookingDate = targetDate,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(10, 0, 0),
                NumberOfGuests = 4,
                Status = "Đã xác nhận"
            }
        };

        _amenityServiceMock.Setup(s => s.GetBookingsAsync(1, targetDate, null))
            .ReturnsAsync(bookings);

        var result = await _bookingController.GetBookedSlots(1, targetDate);

        var jsonResult = Assert.IsType<JsonResult>(result);
        Assert.NotNull(jsonResult.Value);
    }

    #endregion

    #region AmenityManagementController Tests

    [Fact]
    public async Task Management_Index_ReturnsViewWithFilteredBookingsAndMetrics()
    {
        var amenity1 = new Amenity { AmenityId = 1, Name = "Hồ bơi", PricePerHour = 50000 };
        var bookings = new List<AmenityBooking>
        {
            new() { AmenityBookingId = 1, AmenityId = 1, Status = "Chờ duyệt", StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(10, 0, 0), Amenity = amenity1 },
            new() { AmenityBookingId = 2, AmenityId = 1, Status = "Đã xác nhận", StartTime = new TimeSpan(14, 0, 0), EndTime = new TimeSpan(16, 0, 0), Amenity = amenity1 },
            new() { AmenityBookingId = 3, AmenityId = 1, Status = "Từ chối", StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(19, 0, 0), Amenity = amenity1 }
        };

        var amenities = new List<Amenity>
        {
            amenity1,
            new() { AmenityId = 2, Name = "Sân Tennis" }
        };

        _amenityServiceMock.Setup(s => s.GetBookingsAsync(1, null, "Chờ duyệt"))
            .ReturnsAsync(bookings);
        _amenityServiceMock.Setup(s => s.GetAllAmenitiesAsync())
            .ReturnsAsync(amenities);

        var result = await _managementController.Index(1, null, "Chờ duyệt");

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AmenityManagementViewModel>(viewResult.Model);
        Assert.Equal(1, model.SelectedAmenityId);
        Assert.Equal("Chờ duyệt", model.SelectedStatus);
        Assert.Equal(3, model.Bookings.Count);
        Assert.Equal(1, model.TotalPending);
        Assert.Equal(1, model.TotalConfirmed);
        Assert.Equal(2, model.Amenities.Count);
    }

    [Fact]
    public async Task Management_Approve_WhenSuccess_CallsServiceAndRedirects()
    {
        const string managerId = "manager-user-001";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(managerId);

        _amenityServiceMock.Setup(s => s.ApproveBookingAsync(managerId, 5))
            .ReturnsAsync((true, null));

        var result = await _managementController.Approve(5);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Index), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.ApproveBookingAsync(managerId, 5), Times.Once);
    }

    [Fact]
    public async Task Management_Approve_WhenFailed_SetsErrorAndRedirects()
    {
        const string managerId = "manager-user-001";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(managerId);

        _amenityServiceMock.Setup(s => s.ApproveBookingAsync(managerId, 5))
            .ReturnsAsync((false, "Trùng lịch với đặt chỗ đã được duyệt."));

        var result = await _managementController.Approve(5);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Index), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("ErrorMessage"));
    }

    [Fact]
    public async Task Management_Reject_WhenValid_CallsServiceAndRedirects()
    {
        const string managerId = "manager-user-001";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(managerId);

        _amenityServiceMock.Setup(s => s.RejectBookingAsync(managerId, 5, "Bảo trì đột xuất"))
            .ReturnsAsync((true, null));

        var result = await _managementController.Reject(5, "Bảo trì đột xuất");

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Index), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.RejectBookingAsync(managerId, 5, "Bảo trì đột xuất"), Times.Once);
    }

    [Fact]
    public async Task Management_Cancel_WhenValid_CallsServiceAndRedirects()
    {
        const string managerId = "manager-user-001";
        _currentUserServiceMock.Setup(u => u.UserId).Returns(managerId);

        _amenityServiceMock.Setup(s => s.CancelBookingAsync(managerId, 5, true, null))
            .ReturnsAsync((true, null));

        var result = await _managementController.Cancel(5);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Index), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.CancelBookingAsync(managerId, 5, true, null), Times.Once);
    }

    [Fact]
    public async Task Management_Amenities_ReturnsAllAmenitiesView()
    {
        var amenities = new List<Amenity>
        {
            new() { AmenityId = 1, Name = "Hồ bơi", IsActive = true },
            new() { AmenityId = 2, Name = "Khu BBQ", IsActive = false }
        };
        _amenityServiceMock.Setup(s => s.GetAllAmenitiesAsync()).ReturnsAsync(amenities);

        var result = await _managementController.Amenities();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Amenity>>(viewResult.Model);
        Assert.Equal(2, model.Count());
    }

    [Fact]
    public void Management_CreateAmenity_Get_ReturnsViewWithDefaultHours()
    {
        var result = _managementController.CreateAmenity();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AmenityDefinitionViewModel>(viewResult.Model);
        Assert.Equal(new TimeSpan(6, 0, 0), model.OpenTime);
        Assert.Equal(new TimeSpan(22, 0, 0), model.CloseTime);
        Assert.True(model.IsActive);
    }

    [Fact]
    public async Task Management_CreateAmenity_Post_Valid_CreatesAmenityAndRedirects()
    {
        var model = new AmenityDefinitionViewModel
        {
            Name = "Phòng Gym Tòa A",
            Location = "Tầng 3",
            Description = "Máy móc Technogym",
            Capacity = 25,
            PricePerHour = 0,
            OpenTime = new TimeSpan(6, 0, 0),
            CloseTime = new TimeSpan(22, 0, 0),
            IsActive = true
        };

        _amenityServiceMock.Setup(s => s.CreateAmenityAsync(It.IsAny<Amenity>()))
            .ReturnsAsync((true, null));

        var result = await _managementController.CreateAmenity(model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Amenities), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.CreateAmenityAsync(It.Is<Amenity>(a => a.Name == "Phòng Gym Tòa A")), Times.Once);
    }

    [Fact]
    public async Task Management_EditAmenity_Get_WhenNotFound_ReturnsNotFound()
    {
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(999)).ReturnsAsync((Amenity?)null);

        var result = await _managementController.EditAmenity(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Management_EditAmenity_Post_Valid_UpdatesAmenityAndRedirects()
    {
        var existing = new Amenity { AmenityId = 5, Name = "Hồ bơi cũ", Capacity = 10 };
        _amenityServiceMock.Setup(s => s.GetAmenityByIdAsync(5)).ReturnsAsync(existing);
        _amenityServiceMock.Setup(s => s.UpdateAmenityAsync(It.IsAny<Amenity>()))
            .ReturnsAsync((true, null));

        var model = new AmenityDefinitionViewModel
        {
            AmenityId = 5,
            Name = "Hồ bơi bốn mùa",
            Capacity = 30,
            PricePerHour = 20000,
            OpenTime = new TimeSpan(6, 0, 0),
            CloseTime = new TimeSpan(21, 0, 0),
            IsActive = true
        };

        var result = await _managementController.EditAmenity(5, model);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Amenities), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.UpdateAmenityAsync(It.Is<Amenity>(a => a.AmenityId == 5 && a.Name == "Hồ bơi bốn mùa")), Times.Once);
    }

    [Fact]
    public async Task Management_DeleteAmenity_CallsDeleteAndRedirects()
    {
        _amenityServiceMock.Setup(s => s.DeleteAmenityAsync(5)).ReturnsAsync((true, null));

        var result = await _managementController.DeleteAmenity(5);

        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AmenityManagementController.Amenities), redirectResult.ActionName);
        Assert.True(_tempData.ContainsKey("SuccessMessage"));
        _amenityServiceMock.Verify(s => s.DeleteAmenityAsync(5), Times.Once);
    }

    #endregion
}
