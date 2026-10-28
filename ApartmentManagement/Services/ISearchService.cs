using System.Security.Claims;
using ApartmentManagement.Models;

namespace ApartmentManagement.Services;

public interface ISearchService
{
    Task<GlobalSearchResponseDto> SearchAsync(string? query, ClaimsPrincipal user);
}

