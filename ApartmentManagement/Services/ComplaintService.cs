using ApartmentManagement.Common.Security;
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

        private static readonly string[] AllowedPriorities =
        {
            "Thấp",
            "Bình thường",
            "Cao",
            "Khẩn cấp"
        };

        private static readonly string[] AllowedProcessStatuses =
        {
            "Đang xử lý",
            "Đã xử lý",
            "Từ chối"
        };

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly INotificationService? _notificationService;

        public ComplaintService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            INotificationService? notificationService = null)
        {
            _context = context;
            _currentUserService = currentUserService;
            _notificationService = notificationService;
        }

        private bool IsManagementUser()
        {
            return _currentUserService.IsInRole(AppRoles.SuperAdmin) ||
                   _currentUserService.IsInRole(AppRoles.BuildingManager) ||
                   _currentUserService.IsInRole("SystemAdmin") ||
                   _currentUserService.IsInRole("Manager");
        }

        public async Task<List<Complaint>> GetComplaintsAsync(string? searchTerm = null, string? status = null, string? category = null)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return new List<Complaint>();
            }

            var query = _context.Complaints
                .AsNoTracking()
                .Include(c => c.Resident)
                    .ThenInclude(r => r!.User)
                .Include(c => c.Apartment)
                    .ThenInclude(a => a!.Building)
                .AsQueryable();

            // Phân quyền dữ liệu: Cư dân chỉ xem được phản ánh của chính mình
            if (!IsManagementUser())
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
            if (id <= 0)
            {
                return null;
            }

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
            if (IsManagementUser())
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
            if (model == null)
            {
                return (false, "Dữ liệu không hợp lệ.");
            }

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

            var priority = model.Priority?.Trim();
            if (string.IsNullOrWhiteSpace(priority))
            {
                priority = "Bình thường";
            }
            else if (!AllowedPriorities.Contains(priority))
            {
                return (false, "Mức độ ưu tiên không hợp lệ.");
            }

            var title = model.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, "Vui lòng nhập tiêu đề phản ánh.");
            }
            if (title.Length > 200)
            {
                return (false, "Tiêu đề không quá 200 ký tự.");
            }

            var content = model.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, "Vui lòng nhập nội dung chi tiết phản ánh.");
            }
            if (content.Length > 2000)
            {
                return (false, "Nội dung không quá 2000 ký tự.");
            }

            // Kiểm tra bảo vệ Apartment Spoofing: Nếu có chọn ApartmentId, phải thuộc cư trú hiện tại của Resident
            if (model.ApartmentId.HasValue)
            {
                if (model.ApartmentId.Value <= 0)
                {
                    return (false, "Căn hộ không hợp lệ.");
                }

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
                Title = title,
                Content = content,
                Category = category,
                Priority = priority,
                Status = "Chờ tiếp nhận",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.Complaints.Add(complaint);
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException)
            {
                return (false, "Có lỗi xảy ra khi lưu phản ánh vào hệ thống.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateComplaintAsync(ComplaintEditViewModel model)
        {
            if (model == null || model.ComplaintId <= 0)
            {
                return (false, "Dữ liệu không hợp lệ.");
            }

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Người dùng chưa được xác thực.");
            }

            if (!_currentUserService.IsInRole("Resident"))
            {
                return (false, "Chỉ cư dân mới có quyền chỉnh sửa phản ánh.");
            }

            var complaint = await _context.Complaints
                .Include(c => c.Resident)
                .FirstOrDefaultAsync(c => c.ComplaintId == model.ComplaintId);

            if (complaint == null)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

            // Chỉ chủ sở hữu phản ánh mới có quyền chỉnh sửa
            if (complaint.Resident == null || complaint.Resident.UserId != userId)
            {
                return (false, "Bạn không có quyền thao tác trên phản ánh này.");
            }

            // Nghiệp vụ: Chỉ cho phép chỉnh sửa khi đang ở trạng thái 'Chờ tiếp nhận'
            if (complaint.Status != "Chờ tiếp nhận")
            {
                return (false, $"Không thể chỉnh sửa phản ánh đang ở trạng thái '{complaint.Status}'.");
            }

            // Kiểm tra danh mục hợp lệ
            var category = model.Category?.Trim() ?? string.Empty;
            if (!AllowedCategories.Contains(category))
            {
                return (false, "Danh mục phản ánh không hợp lệ.");
            }

            var priority = model.Priority?.Trim();
            if (string.IsNullOrWhiteSpace(priority))
            {
                priority = "Bình thường";
            }
            else if (!AllowedPriorities.Contains(priority))
            {
                return (false, "Mức độ ưu tiên không hợp lệ.");
            }

            var title = model.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, "Vui lòng nhập tiêu đề phản ánh.");
            }
            if (title.Length > 200)
            {
                return (false, "Tiêu đề không quá 200 ký tự.");
            }

            var content = model.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, "Vui lòng nhập nội dung chi tiết phản ánh.");
            }
            if (content.Length > 2000)
            {
                return (false, "Nội dung không quá 2000 ký tự.");
            }

            // Kiểm tra bảo vệ Apartment Spoofing nếu có chọn căn hộ
            if (model.ApartmentId.HasValue)
            {
                if (model.ApartmentId.Value <= 0)
                {
                    return (false, "Căn hộ không hợp lệ.");
                }

                var isResidentOfApartment = await _context.ApartmentResidents
                    .AnyAsync(ar =>
                        ar.ResidentId == complaint.ResidentId &&
                        ar.ApartmentId == model.ApartmentId.Value &&
                        ar.MoveOutDate == null);

                if (!isResidentOfApartment)
                {
                    return (false, "Bạn chỉ có thể gắn phản ánh vào căn hộ mà bạn hiện đang cư trú.");
                }
            }

            // Cập nhật các trường được phép
            complaint.Title = title;
            complaint.Content = content;
            complaint.Category = category;
            complaint.Priority = priority;
            complaint.ApartmentId = model.ApartmentId;

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException)
            {
                return (false, "Có lỗi xảy ra khi cập nhật phản ánh.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> CancelComplaintAsync(int complaintId)
        {
            if (complaintId <= 0)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

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

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException)
            {
                return (false, "Có lỗi xảy ra khi cập nhật trạng thái hủy phản ánh.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ProcessComplaintAsync(ComplaintProcessViewModel model)
        {
            // Business Authorization: Cư dân không có quyền xử lý phản ánh
            if (!IsManagementUser())
            {
                return (false, "Bạn không có quyền xử lý phản ánh của cư dân.");
            }

            if (model == null || model.ComplaintId <= 0)
            {
                return (false, "Dữ liệu xử lý phản ánh không hợp lệ.");
            }

            var targetStatus = model.TargetStatus?.Trim() ?? string.Empty;
            if (!AllowedProcessStatuses.Contains(targetStatus))
            {
                return (false, "Trạng thái xử lý không hợp lệ.");
            }

            // Validate Category & Priority (UC33 — Phân loại phản ánh)
            var category = model.Category?.Trim() ?? string.Empty;
            if (!AllowedCategories.Contains(category))
            {
                return (false, "Danh mục phản ánh không hợp lệ.");
            }

            var priority = model.Priority?.Trim();
            if (string.IsNullOrWhiteSpace(priority))
            {
                return (false, "Vui lòng chọn mức độ ưu tiên.");
            }
            if (!AllowedPriorities.Contains(priority))
            {
                return (false, "Mức độ ưu tiên không hợp lệ.");
            }

            var complaint = await _context.Complaints.Include(c => c.Resident).FirstOrDefaultAsync(c => c.ComplaintId == model.ComplaintId);
            if (complaint == null)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

            if (complaint.Status == "Đã hủy")
            {
                return (false, "Không thể xử lý phản ánh đã bị cư dân hủy.");
            }

            var responseNotes = model.ResponseNotes?.Trim();
            if (responseNotes != null && responseNotes.Length > 1000)
            {
                return (false, "Nội dung phản hồi không quá 1000 ký tự.");
            }

            // Business Rule: ResponseNotes bắt buộc khi chuyển sang 'Đã xử lý' hoặc 'Từ chối'
            if (targetStatus == "Đã xử lý" || targetStatus == "Từ chối")
            {
                if (string.IsNullOrWhiteSpace(responseNotes))
                {
                    return (false, $"Vui lòng nhập nội dung phản hồi / giải thích khi chuyển sang trạng thái '{targetStatus}'.");
                }
            }

            complaint.Category = category;
            complaint.Priority = priority;
            complaint.Status = targetStatus;
            complaint.ResponseNotes = responseNotes;

            if (targetStatus == "Đã xử lý")
            {
                complaint.ResolvedAt ??= DateTime.UtcNow;
            }
            else
            {
                complaint.ResolvedAt = null;
            }

            try
            {
                await _context.SaveChangesAsync();
                if (_notificationService != null && complaint.Resident?.UserId != null)
                {
                    _ = _notificationService.CreateNotificationAsync(
                        complaint.Resident.UserId,
                        $"Phản ánh #{complaint.ComplaintId} đã được cập nhật",
                        $"Phản ánh của bạn đã chuyển sang trạng thái '{targetStatus}'. Phản hồi từ BQL: {responseNotes ?? "Không có"}",
                        "Phản ánh",
                        $"/Complaint/Details/{complaint.ComplaintId}");
                }
                return (true, null);
            }
            catch (DbUpdateException)
            {
                return (false, "Có lỗi xảy ra khi lưu trạng thái xử lý phản ánh.");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> RateComplaintAsync(int complaintId, int rating, string? feedback)
        {
            if (complaintId <= 0)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Người dùng chưa được xác thực.");
            }

            if (rating < 1 || rating > 5)
            {
                return (false, "Đánh giá mức độ hài lòng phải từ 1 đến 5 sao.");
            }

            var complaint = await _context.Complaints
                .Include(c => c.Resident)
                .FirstOrDefaultAsync(c => c.ComplaintId == complaintId);

            if (complaint == null)
            {
                return (false, "Không tìm thấy phản ánh.");
            }

            // Chỉ cư dân sở hữu phản ánh mới được quyền đánh giá
            if (complaint.Resident == null || complaint.Resident.UserId != userId)
            {
                return (false, "Bạn không có quyền đánh giá phản ánh này.");
            }

            // Chỉ được đánh giá khi đơn đã ở trạng thái 'Đã xử lý'
            if (complaint.Status != "Đã xử lý")
            {
                return (false, "Chỉ có thể đánh giá phản ánh đã được xử lý hoàn tất.");
            }

            // Mỗi phản ánh chỉ được đánh giá 1 lần duy nhất
            if (complaint.Rating.HasValue)
            {
                return (false, "Phản ánh này đã được đánh giá trước đó.");
            }

            var trimmedFeedback = feedback?.Trim();
            if (trimmedFeedback != null && trimmedFeedback.Length > 1000)
            {
                return (false, "Ý kiến đánh giá không được vượt quá 1000 ký tự.");
            }

            complaint.Rating = rating;
            complaint.ResidentFeedback = string.IsNullOrWhiteSpace(trimmedFeedback) ? null : trimmedFeedback;
            complaint.RatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException)
            {
                return (false, "Có lỗi xảy ra khi lưu đánh giá.");
            }
        }

        public async Task<(bool Success, int? RepairRequestId, string? ErrorMessage)> ConvertToRepairRequestAsync(int complaintId)
        {
            if (complaintId <= 0)
            {
                return (false, null, "Không tìm thấy phản ánh.");
            }

            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return (false, null, "Người dùng chưa được xác thực.");
            }

            if (!IsManagementUser())
            {
                return (false, null, "Bạn không có quyền chuyển tiếp phản ánh sang yêu cầu sửa chữa.");
            }

            var complaint = await _context.Complaints
                .Include(c => c.Resident)
                .FirstOrDefaultAsync(c => c.ComplaintId == complaintId);

            if (complaint == null)
            {
                return (false, null, "Không tìm thấy phản ánh.");
            }

            if (complaint.Status != "Chờ tiếp nhận" && complaint.Status != "Đang xử lý")
            {
                return (false, null, $"Không thể chuyển tiếp phản ánh đang ở trạng thái '{complaint.Status}'.");
            }

            if (!string.IsNullOrEmpty(complaint.ResponseNotes) &&
                complaint.ResponseNotes.Contains("Đã chuyển thành Yêu cầu sửa chữa mã #RR-"))
            {
                return (false, null, "Phản ánh này đã được chuyển tiếp sang Yêu cầu sửa chữa trước đó.");
            }

            var apartmentId = complaint.ApartmentId;
            if (!apartmentId.HasValue)
            {
                var activeApt = await _context.ApartmentResidents
                    .Where(ar => ar.ResidentId == complaint.ResidentId && ar.MoveOutDate == null)
                    .OrderByDescending(ar => ar.MoveInDate)
                    .FirstOrDefaultAsync();

                if (activeApt != null)
                {
                    apartmentId = activeApt.ApartmentId;
                }
            }

            if (!apartmentId.HasValue)
            {
                return (false, null, "Không thể chuyển tiếp vì phản ánh chưa được gán căn hộ và cư dân không có căn hộ đang cư trú.");
            }

            var repairPriority = complaint.Priority switch
            {
                "Khẩn cấp" => RepairPriority.Urgent,
                "Cao" => RepairPriority.High,
                "Thấp" => RepairPriority.Low,
                _ => RepairPriority.Normal
            };

            var repairRequest = new RepairRequest
            {
                ResidentId = complaint.ResidentId,
                ApartmentId = apartmentId.Value,
                Title = complaint.Title.Trim(),
                Description = complaint.Content.Trim(),
                Priority = repairPriority,
                Status = RepairRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            repairRequest.StatusHistory.Add(new RepairStatusHistory
            {
                PreviousStatus = null,
                NewStatus = RepairRequestStatus.Pending,
                ChangedByUserId = userId,
                ChangedAt = DateTime.UtcNow,
                Note = $"Tự động tạo từ Phản ánh #{complaint.ComplaintId} ({complaint.Title})."
            });

            try
            {
                _context.RepairRequests.Add(repairRequest);
                await _context.SaveChangesAsync();

                if (complaint.Status == "Chờ tiếp nhận")
                {
                    complaint.Status = "Đang xử lý";
                }

                var transferNote = $"Đã chuyển thành Yêu cầu sửa chữa mã #RR-{repairRequest.RepairRequestId} vào lúc {DateTime.UtcNow:dd/MM/yyyy HH:mm}";
                if (string.IsNullOrWhiteSpace(complaint.ResponseNotes))
                {
                    complaint.ResponseNotes = transferNote;
                }
                else
                {
                    complaint.ResponseNotes = $"{complaint.ResponseNotes}\n{transferNote}";
                }

                await _context.SaveChangesAsync();
                return (true, repairRequest.RepairRequestId, null);
            }
            catch (DbUpdateException)
            {
                return (false, null, "Có lỗi xảy ra khi tạo yêu cầu sửa chữa và cập nhật phản ánh.");
            }
        }
    }
}
