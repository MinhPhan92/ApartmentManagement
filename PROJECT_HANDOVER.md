# ApartmentHub — Project Handover

**Handover date:** 2026-10-09

**Status:** Development paused after P3.1 implementation; not production-ready or formally accepted.

**Repository (developer workstation):** `C:\Users\ATTS\ApartmentManagement`
**Evidence basis:** Repository source review and a fresh local build/test/EF snapshot check on 2026-10-09. SQL Server, UAT and deployment were not performed.

## 1. Project overview

ApartmentHub is a Vietnamese apartment-management application built with .NET 10, ASP.NET Core MVC/Razor, EF Core 10, SQL Server, ASP.NET Core Identity, Bootstrap and jQuery. The solution includes `ApartmentManagement/` (application) and `ApartmentManagement.Tests/` (tests), with an `.slnx` solution. The current product surface is a server-rendered web application; a resident iOS/Android application is not included in this handover.

Roles: `SuperAdmin`, `BuildingManager`, `Accountant`, `Technician`, `Resident`. **Business decision:** BuildingManager may administer billing tariffs and invoices across *all buildings*, not just assigned buildings.

## 2. Reported feature progress

| Area | Reported state | Important notes |
| --- | --- | --- |
| P0 Identity/security | Source implemented | ASP.NET Core Identity, role/policy authorization, account protection and antiforgery. Verify configured providers and operational settings before deployment. |
| P1 Residents / occupancy | Source implemented | Resident/account management, validation, occupancy history and move-in/move-out. |
| P1.5 SQL verification | Pending final verification | SQL Server migration/constraint tests require authorized test environment. |
| P2.1 Lease contracts | Source implementation reported complete; **not accepted** | Draft/Active/Completed/Terminated/Cancelled workflows, parties, history, resident read-only portal. P2.1.5 SQL verification pending. |
| Incident Ticketing MVP | Source implementation reported complete; **not accepted** | Resident submits text tickets for active occupancy; staff claim/complete; resident rates once; no media uploads. UAT: 0/22. |
| Apartment Billing MVP | Source implementation reported complete; **not accepted** | Tariffs by building, progressive electricity/water charges, monthly management fee, meter readings, one invoice per apartment/period assigned to active head of household, Draft/Issued, resident portal. UAT: 0/28. |
| P3.1 Simulated payments / VietQR | Source implemented; **not accepted** | One full-amount transaction per issued invoice; locally generated VietQR; staff simulated confirmation; no real bank transfer or webhook. Own SQL Server and UAT verification remain outstanding. |

## 3. Latest reported automated verification

- **109 passed, 0 failed, 10 skipped (119 total)**. All SQL Server tests were skipped by explicitly clearing `APARTMENTHUB_SQLSERVER_TEST_CONNECTION` in the test process.
- Solution build: **0 warnings, 0 errors**.
- EF `has-pending-model-changes`: **No changes have been made to the model since the last migration.**
- `git diff --check`: **passed** (Git emitted existing LF-to-CRLF working-copy notices for several files).
- These checks were run locally on 2026-10-09. They do not verify SQL Server behavior.
- SQL Server migration execution, constraints, uniqueness under contention, `RowVersion` concurrency and test-database cleanup remain **unverified**.
- The test/build results do not constitute UAT, business acceptance or production readiness.

## 4. Migrations and database safety

Migration source files present in the repository, in order:

- `20260926140942_InitialCreate`
- `20260930144452_UpdateBuildingModel`
- `20260930151344_ApartmentModule`
- `20261009042745_ResidentValidationAndIntegrity`
- `20261009043803_OccupancyHistory`
- `20261009063555_ApartmentContracts`
- `20261009065119_ContractStatusHistoryReason`
- `20261009080111_IncidentTicketing`
- `20261009083116_ApartmentBilling`
- `20261009085514_SimulatedPayments`

**Important:** The source contains these migrations, but the current migration state of any database was not inspected. The latest reports specifically state that `ApartmentBilling` and `SimulatedPayments` have not been applied; prior SQL Server verification for contracts and incident tickets is also pending. Verify the target database's migration history before any migration execution. Do not edit or remove old migrations casually.

**Application startup applies migrations automatically:** `Program.cs` calls `IdentityDataSeeder.InitializeAsync`, which invokes `Database.MigrateAsync()` for relational providers. Do not run the web application with a connection string that might point at a shared, UAT or production database unless applying all pending migrations is explicitly approved. Do not run `dotnet ef database update` without explicit environment approval, backup and authorization.

No production deployment or real payment processing is claimed.

## 5. P3.1 payment design and boundaries

Reported files/areas:

- `Models/PaymentTransaction.cs` — payment entity, status, row version.
- `Services/PaymentService.cs` — business flow and idempotency.
- `Services/VietQrCodeGenerator.cs` and `Services/VietQrOptions.cs` — local QR generation/configuration.
- `Controllers/PaymentsController.cs` — authorized simulated confirmation.
- `Controllers/MyInvoicesController.cs` and invoice Razor views — resident payment flow.
- `Migrations/20261009085514_SimulatedPayments.cs` — schema-only migration.
- `docs/billing/Apartment-Billing-MVP.md` — existing billing/payment documentation.

Business constraints: **one transaction for the entire invoice**, no partial payments; only an authorized simulation can confirm a transaction; generating or scanning a QR **must not** mark an invoice as paid. No bank integration, no real webhook, no transfer of funds. VietQR recipient configuration keys: `Payments:VietQr:BankBin`, `AccountNumber`, `AccountName`; values must be supplied through approved configuration and must not be committed if sensitive.

## 6. Open risks and follow-up work

1. **SQL Server tests:** 10 skipped tests must be executed on an authorized disposable SQL Server test database; verify migration compatibility, constraints, unique indexes, `RowVersion`, concurrency and cleanup of test databases.
2. **UAT:** P2.1, Incident Ticketing (0/22) and Billing (0/28) have not been formally accepted. P3.1 needs its own test/acceptance evidence.
3. **Payment security:** Confirm authorization, antiforgery, owner isolation, idempotency, single-transaction uniqueness and stale `RowVersion` behavior on SQL Server.
4. **Data safety:** Verify existing residents, occupancy, contracts, tickets and invoices survive migration on a disposable test database.
5. **Banking scope:** Real VietQR payment confirmation, reconciliation, webhook signature verification and provider integration are **future work**, not part of P3.1.
6. **Deployment:** Production readiness, secrets management, backups, monitoring and operational runbooks remain separate work.

## 7. Safe setup for the next developer

1. Clone the repository and inspect `PROJECT_HANDOVER.md`, `docs/billing/Apartment-Billing-MVP.md`, `docs/uat/Incident-Ticketing-MVP-UAT.md`, `Program.cs`, `.slnx`, current Git status and migrations.
2. Use .NET SDK 10 and restore packages: `dotnet restore .\ApartmentManagement.slnx`.
3. Build: `dotnet build .\ApartmentManagement.slnx --no-restore`.
4. Run tests only when the process environment is understood. To reproduce the reported non-SQL run in PowerShell:
   ```powershell
   $env:APARTMENTHUB_SQLSERVER_TEST_CONNECTION = $null
   dotnet test .\ApartmentManagement.slnx --no-build --no-restore
   ```
   The SQL tests will be skipped; a skipped test is not a passing SQL Server verification.
5. Check model consistency: `dotnet ef migrations has-pending-model-changes --project .\ApartmentManagement --startup-project .\ApartmentManagement`.
6. To run the web app, first configure an explicitly disposable local database using approved local configuration. Startup applies migrations automatically. Use `dotnet run --project .\ApartmentManagement` only after verifying the effective connection target.
7. VietQR is disabled with empty defaults. For local simulation only, configure `Payments:VietQr:BankBin`, `AccountNumber` and `AccountName` using environment/user secrets; do not put real credentials in source control. These identify a receiving account and are not bank login credentials.
8. For SQL tests, use only the approved test connection and confirm all created `ApartmentHubTests_*` databases are cleaned up.

## 8. Handover / Git checklist

- [ ] Review `git status --short` and `git diff --stat` before staging. The handoff commit should include only the source, migrations, tests and documentation reviewed for this delivery.
- [ ] Inspect untracked files; exclude `.env`, credentials, database backups, logs and local config.
- [ ] Ensure this document and intended source/tests/migrations are staged.
- [ ] Review staged changes with `git diff --cached --stat` and `git diff --cached --check`.
- [ ] Commit with a descriptive message, e.g. `feat: add simulated VietQR payments and project handover`.
- [ ] Push the approved branch to its configured remote; verify the remote and branch first.
- [ ] Share the branch, commit SHA, repository URL and this document with the receiving developer.

## 9. Acceptance statement

This is a **development handover**, not a release approval, UAT sign-off, confirmation of successful SQL Server migration, or proof that a payment occurred. The next developer should validate the repository state and testing evidence independently before deployment.
