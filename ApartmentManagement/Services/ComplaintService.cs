using ApartmentManagement.Data;
using ApartmentManagement.Models;
using ApartmentManagement.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ApartmentManagement.Services
{
    public class ComplaintService : IComplaintService
    {
        private static readonly string[] AllowedCategories =
        {
            "An ninh",
            "Vệ sinh",
            "Tiếng ồn",
            "Tiện ích / Thiết bị",
            "Dịch vụ khác"
        };

        private static readonly string[] AllowedProcessStatuses =
        {
            "Đang xử lý",
            "Đã xử lý",
            "Từ chối"
        };

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ComplaintService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<List<Complaint>> GetComplaintsAsync(string? searchTerm = null, string? status = null, string? category = null)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new List<Complaint>();
            }

            var query = _context.Complaints
                .Include(c => c.Resident)
                    .ThenInclude(r => r!.User)
                .Include(c => c.Apartment)
                    .ThenInclude(a => a!.Building)
                .AsQueryable();

            // Phân quyền dữ liệu: Cư dân chỉ xem được phản ánh của chính mình
            if (!_currentUserService.IsInRole("SystemAdmin") && !_currentUserService.IsInRole("Manager"))
            {
                query = query.Where(c => c.Resident != null && c.Resident.UserId == userId);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status))
            {
                status = status.Trim();
                query = query.Where(c => c.Status == status);
            }

            // Lọc theo danh mục
            if (!string.IsNullOrWhiteSpace(category))
            {
                category = category.Trim();
                query = query.Where(c => c.Category == category);
            }

            // Tìm kiếm theo tiêu đề, nội dung hoặc tên cư dân
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(term) ||
                    c.Content.ToLower().Contains(term) ||
                    (c.Resident != null && c.Resident.User != null && c.Resident.User.FullName.ToLower().Contains(term)) ||
                    (c.Apartment != null && c.Apartment.ApartmentCode.ToLower().Contains(term)));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<Complaint?> GetComplaintByIdAsync(int id)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var query = _context.Complaints
                .Include(c => c.Resident)
                    .ThenInclude(r => r!.User)
                .Include(c => c.Apartment)
                    .ThenInclude(a => a!.Building)
                .AsQueryable();

            // Quản trị viên và Ban quản lý được xem phản ánh bất kỳ
            if (_currentUserService.IsInRole("SystemAdmin") || _currentUserService.IsInRole("Manager"))
            {
                return await query.FirstOrDefaultAsync(c => c.ComplaintId == id);
            }

            // Cư dân chỉ được xem phản ánh của chính mình (chống IDOR)
            return await query.FirstOrDefaultAsync(c =>
                c.ComplaintId == id &&
                c.Resident != null &&
                c.Resident.UserId == userId);
        }

        public async Task<(bool Success, string? ErrorMessage)> CreateComplaintAsync(ComplaintCreateViewModel model)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Người dùng chưa được xác thực.");
            }

            // Tìm thông tin Resident dựa trên UserId đăng nhập (không tin cậy input từ client)
            var resident = await _context.Residents
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (resident == null)
            {
                return (false, "Tài khoản của bạn chưa được liên kết với hồ sơ cư dân.");
            }

            // Kiểm tra danh mục hợp lệ
            var category = model.Category?.Trim() ?? string.Empty;
            if (!AllowedCategories.Contains(category))
            {
                return (false, "Danh mục phản ánh không hợp lệ.");
            }

            // Kiểm tra bảo vệ Apartment Spoofing: Nếu có chọn ApartmentId, phải thuộc cư trú hiện tại của Resident
            if (model.ApartmentId.HasValue)
            {
                var isResidentOfApartment = await _context.ApartmentResidents
                    .AnyAsync(ar =>
                        ar.ResidentId == resident.ResidentId &&
                        ar.ApartmentId == model.ApartmentId.Value &&
                        ar.MoveOutDate == null);

                if (!isResidentOfApartment)
                {
                    return (false, "Bạn chỉ có thể gắn phản ánh vào căn hộ mà bạn hiện đang cư trú.");
                }
            }

            var complaint = new Complaint
            {
                ResidentId = resident.ResidentId,
                ApartmentId = model.ApartmentId,
                Title = model.Title.Trim(),
                Content = model.Content.Trim(),
                Category = category,
                Status = "Chờ tiếp nhận",
                CreatedAt = DateTime.UtcNow
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();

            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> CancelComplaintAsync(int complaintId)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Người dùng chưa được xác thực.");
            }

            var complaint = await _context.Complaints
                .Include(c => c.Resident)
                .FirstOrDefaultAsync(c => c.ComplaintId == complaintId);

            if (complaint == null)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

            // Chỉ chủ sở hữu phản ánh mới có quyền hủy
            if (complaint.Resident == null || complaint.Resident.UserId != userId)
            {
                return (false, "Bạn không có quyền thao tác trên phản ánh này.");
            }

            // Nghiệp vụ: Chỉ cho phép hủy khi đang ở trạng thái 'Chờ tiếp nhận'
            if (complaint.Status != "Chờ tiếp nhận")
            {
                return (false, $"Không thể hủy phản ánh đang ở trạng thái '{complaint.Status}'.");
            }

            complaint.Status = "Đã hủy";
            complaint.ResponseNotes = "Cư dân chủ động rút phản ánh.";

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ProcessComplaintAsync(ComplaintProcessViewModel model)
        {
            // Business Authorization: Cư dân không có quyền xử lý phản ánh
            if (!_currentUserService.IsInRole("SystemAdmin") && !_currentUserService.IsInRole("Manager"))
            {
                return (false, "Bạn không có quyền xử lý phản ánh của cư dân.");
            }

            var targetStatus = model.TargetStatus?.Trim() ?? string.Empty;
            if (!AllowedProcessStatuses.Contains(targetStatus))
            {
                return (false, "Trạng thái xử lý không hợp lệ.");
            }

            var complaint = await _context.Complaints.FirstOrDefaultAsync(c => c.ComplaintId == model.ComplaintId);
            if (complaint == null)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

            if (complaint.Status == "Đã hủy")
            {
                return (false, "Không thể xử lý phản ánh đã bị cư dân hủy.");
            }

            // Business Rule: ResponseNotes bắt buộc khi chuyển sang 'Đã xử lý' hoặc 'Từ chối'
            if (targetStatus == "Đã xử lý" || targetStatus == "Từ chối")
            {
                if (string.IsNullOrWhiteSpace(model.ResponseNotes))
                {
                    return (false, $"Vui lòng nhập nội dung phản hồi / giải thích khi chuyển sang trạng thái '{targetStatus}'.");
                }
            }

            complaint.Status = targetStatus;
            complaint.ResponseNotes = model.ResponseNotes?.Trim();

            if (targetStatus == "Đã xử lý")
            {
                complaint.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
