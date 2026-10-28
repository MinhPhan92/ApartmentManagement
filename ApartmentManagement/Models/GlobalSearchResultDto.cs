namespace ApartmentManagement.Models;

public sealed record GlobalSearchResultItemDto(
    string Category,
    string Title,
    string Subtitle,
    string Url,
    string? Badge = null,
    string? BadgeClass = null
);

public sealed record GlobalSearchGroupDto(
    string Category,
    IReadOnlyList<GlobalSearchResultItemDto> Items
);

public sealed record GlobalSearchResponseDto(
    string Query,
    int TotalCount,
    IReadOnlyList<GlobalSearchGroupDto> Groups
);

