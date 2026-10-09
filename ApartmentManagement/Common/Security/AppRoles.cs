namespace ApartmentManagement.Common.Security;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string BuildingManager = "BuildingManager";
    public const string Accountant = "Accountant";
    public const string Technician = "Technician";
    public const string Resident = "Resident";

    public static readonly IReadOnlyList<string> AllRoles =
    [
        SuperAdmin,
        BuildingManager,
        Accountant,
        Technician,
        Resident
    ];
}

public static class AppPolicies
{
    public const string RequireSuperAdmin = "RequireSuperAdmin";
    public const string RequireManagement = "RequireManagement";
    public const string RequireAccountantOrManager = "RequireAccountantOrManager";
    public const string RequireResident = "RequireResident";
    public const string RequireTicketStaff = "RequireTicketStaff";
}
