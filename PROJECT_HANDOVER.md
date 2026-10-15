# ApartmentHub — Project Handover

**Handover date:** 2026-10-15

**Status:** Completed core 12 thesis specification modules at checkpoint `0ed233e`; verified EF Core migration chain and SQL Server concurrency tests against isolated test database; Dashboard integration tests implemented and passing.

**Repository:** `D:\ApartmentManagement`
**Evidence basis:** Full local build, 159 unit/integration tests passing (100%), 11/11 SQL Server integration tests passing on isolated temporary databases, and complete 12-migration DDL verified on `ApartmentManagementDb_Test`.

## 1. Project overview

ApartmentHub is a comprehensive Vietnamese apartment-management application built with .NET 10, ASP.NET Core MVC/Razor, EF Core 10, SQL Server, ASP.NET Core Identity, Bootstrap, and jQuery. The solution consists of `ApartmentManagement/` (web application) and `ApartmentManagement.Tests/` (test suite) under `ApartmentManagement.slnx`.

Roles: `SuperAdmin`, `BuildingManager`, `Accountant`, `Technician`, `Resident`.

## 2. Completed 12 Thesis Specification Modules

| No. | Module / Area | Implementation State | Verification & Notes |
| :--- | :--- | :--- | :--- |
| 1 | **Building Management** | Completed | Full CRUD, duplicate building code validation, floor counts, RBAC authorization. |
| 2 | **Apartment Management** | Completed | Apartment allocation, status tracking (Đang sử dụng / Trống), building linkage. |
| 3 | **Resident & Occupancy** | Completed | Citizen ID validation (9/12 digits), DOB check, active lease constraints, move-in/move-out history. |
| 4 | **Lease Contracts** | Completed | Draft/Active/Completed/Terminated workflows, parties (Lessor/Lessee), rowversion optimistic concurrency. |
| 5 | **Incident Ticketing** | Completed | Resident reports issues (Electricity, Water, Elevator, CommonArea, Other); technician claiming/resolving; resident 1-5 star ratings; status history. |
| 6 | **Billing & Tariffs** | Completed | Tiered progressive electricity/water tariffs by building, monthly management fee calculation, meter readings, one invoice per apartment/period. |
| 7 | **Payments / VietQR** | Completed | Simulated Napas VietQR payload generation, one transaction per issued invoice, staff confirmation with optimistic lock. |
| 8 | **Vehicles** | Completed | Resident vehicle registration, license plate uniqueness per active status, manager approval/rejection workflow. |
| 9 | **Complaints** | Completed | Resident complaint submission, apartment spoofing prevention, management feedback and response workflow. |
| 10 | **Amenities & Bookings** | Completed | Amenity operational hours check, capacity limit enforcement, concurrent booking prevention. |
| 11 | **Announcements** | Completed | Global vs. building-specific announcement targeting, pinned notices, role-based publish. |
| 12 | **Visitor Passes** | Completed | Resident visitor registration, security verification, actual check-in/check-out timestamps. |

## 3. Automated Verification Status (2026-10-15)

- **Default Test Suite:** **159 passed, 0 failed, 11 skipped (170 total)**.
- **SQL Server Integration Suite:** **11 passed, 0 failed (11 total)**, executed via dynamic isolated databases (`ApartmentHubTests_*`) and fully cleaned up.
- **Dashboard Integration Suite:** **8 passed, 0 failed** in [DashboardIntegrationTests.cs](file:///d:/ApartmentManagement/ApartmentManagement.Tests/DashboardIntegrationTests.cs), covering SuperAdmin, BuildingManager, Resident data isolation, deterministic KPI calculations, and empty business data scenarios.
- **Solution Build:** **0 warnings, 0 errors**.
- **Model Snapshot Consistency:** Current EF Core snapshot matches the 12-migration chain.

## 4. Migration Chain and Database Architecture

The verified migration chain at checkpoint `0ed233e` consists of 12 sequential migrations:

1. `20260926140942_InitialCreate`
2. `20260930144452_UpdateBuildingModel`
3. `20260930151344_ApartmentModule`
4. `20261009042745_ResidentValidationAndIntegrity`
5. `20261009043803_OccupancyHistory`
6. `20261009063555_ApartmentContracts`
7. `20261009065119_ContractStatusHistoryReason`
8. `20261009080111_IncidentTicketing`
9. `20261009083116_ApartmentBilling`
10. `20261009085514_SimulatedPayments`
11. `20261011160833_CompleteDomainModules`
12. `20261012200447_RefineDomainModuleConstraints`

**Database Safety Rules:**
- Startup automatic migrations are guarded by `Database:ApplyMigrationsOnStartup` (default `false`) in [IdentityDataSeeder.cs](file:///d:/ApartmentManagement/ApartmentManagement/Data/IdentityDataSeeder.cs).
- Migration chain was validated cleanly on `ApartmentManagementDb_Test` with 29 tables, 45 foreign keys, 33 check constraints, and 253 indexes created without errors.
- Do not execute unverified migrations on the operational database without taking a full backup first.

## 5. Security & Resident Data Isolation

- **Authentication & RBAC:** Handled via ASP.NET Core Identity with role-based policies (`RequireManagement`, `RequireResident`, `RequireSuperAdmin`, `RequireTechnician`).
- **Data Isolation:** Enforced at query and service level:
  - Residents cannot view or modify contracts, tickets, complaints, or vehicles belonging to other residents or other apartments.
  - Management endpoints strictly forbid resident access (HTTP 403).
  - Unauthenticated requests are redirected to `/Identity/Account/Login` with proper return URLs.

## 6. How to Run & Verify

1. **Environment:** .NET SDK 10, SQL Server (LocalDB or SQLEXPRESS).
2. **Build:**
   ```powershell
   dotnet build .\ApartmentManagement.slnx
   ```
3. **Run Default Tests:**
   ```powershell
   dotnet test .\ApartmentManagement.Tests\ApartmentManagement.Tests.csproj
   ```
4. **Run SQL Server Concurrency Tests:**
   ```powershell
   $env:APARTMENTHUB_SQLSERVER_TEST_CONNECTION = "Server=localhost\SQLEXPRESS;Database=ApartmentManagementDb_Test;Trusted_Connection=True;TrustServerCertificate=True;"
   dotnet test .\ApartmentManagement.Tests\ApartmentManagement.Tests.csproj --filter "FullyQualifiedName~ApartmentManagement.Tests.SqlServer"
   ```
5. **Run Application:**
   ```powershell
   dotnet run --project .\ApartmentManagement
   ```

## 7. Acceptance & Handoff Summary

The application domain at checkpoint `0ed233e` preserves all 12 modules required by the thesis specification, including Incident Ticketing and its technician assignment workflows. All automated tests pass with zero regressions.
