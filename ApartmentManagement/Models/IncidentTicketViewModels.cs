using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ApartmentManagement.Models;

public sealed class IncidentTicketListViewModel
{
    public IReadOnlyList<IncidentTicket> Tickets { get; init; } = [];
}

public sealed class IncidentTicketCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn căn hộ.")]
    public int ApartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại sự cố.")]
    public IncidentCategory? Category { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(120, ErrorMessage = "Tiêu đề không được vượt quá 120 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả sự cố.")]
    [StringLength(4000, ErrorMessage = "Mô tả không được vượt quá 4000 ký tự.")]
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> Apartments { get; set; } = [];
}

public sealed class IncidentTicketReviewViewModel
{
    public int Id { get; set; }

    [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao.")]
    public byte Rating { get; set; }

    [StringLength(1000, ErrorMessage = "Nhận xét không được vượt quá 1000 ký tự.")]
    public string? Feedback { get; set; }

    public string RowVersionToken { get; set; } = string.Empty;
}

public sealed class StaffIncidentTicketViewModel
{
    public IncidentTicket Ticket { get; init; } = null!;
    public string RowVersionToken { get; init; } = string.Empty;
}
