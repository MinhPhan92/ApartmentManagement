using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface IAmenityService
{
    Task<List<Amenity>> GetAllAmenitiesAsync();
    Task<Amenity?> GetAmenityByIdAsync(int id);
    Task<(bool Success, string? ErrorMessage)> CreateAmenityAsync(Amenity amenity);
    Task<(bool Success, string? ErrorMessage)> UpdateAmenityAsync(Amenity amenity);
    Task<(bool Success, string? ErrorMessage)> DeleteAmenityAsync(int id);

    Task<List<AmenityBooking>> GetBookingsAsync(int? amenityId = null, DateTime? date = null);
    Task<List<AmenityBooking>> GetResidentBookingsAsync(string userId);
    Task<(bool Success, string? ErrorMessage)> BookAmenityAsync(string userId, int amenityId, DateTime bookingDate, TimeSpan startTime, TimeSpan endTime, int guests, string? notes);
    Task<(bool Success, string? ErrorMessage)> CancelBookingAsync(string userId, int bookingId, bool isManager = false);
}
