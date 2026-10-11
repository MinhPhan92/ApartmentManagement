namespace ApartmentManagement.Services
{
    public interface ICurrentUserService
    {
        string? UserId { get; }

        string? UserName { get; }

        bool IsAuthenticated { get; }

        bool IsInRole(string role);
    }
}
