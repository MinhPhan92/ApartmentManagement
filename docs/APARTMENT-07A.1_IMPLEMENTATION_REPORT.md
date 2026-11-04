# APARTMENT-07A.1 — IMPLEMENTATION REPORT: REACT FOUNDATION, AUTHENTICATION & PORTAL LAYOUTS

> **Phase:** Phase 1 — React Foundation, Authentication, Session, CSRF & Portal Layouts
> **Ngày thực hiện:** 05/11/2026
> **Nhánh Git:** `main`
> **Trạng thái nghiệm thu:** **PASS** (12/12 Frontend Unit Tests PASS, `tsc -b && vite build` PASS, `dotnet build` PASS, 905/905 Non-SQL Backend Tests PASS bao gồm 11/11 `AuthApiIntegrationTests` mới).

---

## 1. Kiến trúc Frontend Mới & Cấu trúc Thư mục

Dự án React + TypeScript + Vite được khởi tạo tại thư mục `d:\ApartmentManagement\frontend` và tích hợp trực tiếp với ASP.NET Core (.NET 10) hiện tại:

```text
frontend/
├── index.html
├── package.json
├── postcss.config.js
├── tailwind.config.js
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── vite.config.ts
├── vitest.config.ts
└── src/
    ├── main.tsx
    ├── App.tsx
    ├── vite-env.d.ts
    ├── app/
    │   ├── layouts/
    │   │   ├── AuthLayout.tsx             # Layout độc lập cho Login & AccessDenied (không có sidebar)
    │   │   ├── ResidentLayout.tsx         # Layout Cổng Cư Dân (/resident/*)
    │   │   └── ManagementLayout.tsx       # Layout Cổng Ban Quản Lý (/management/*)
    │   ├── providers/
    │   │   ├── AppProviders.tsx           # QueryClientProvider + ToastProvider + AuthProvider
    │   │   └── AuthProvider.tsx           # Quản lý vòng đời Session, CSRF & 401 Expiry
    │   └── router/
    │       ├── AppRouter.tsx              # Định tuyến toàn hệ thống
    │       └── ProtectedRoute.tsx         # Route Guard kiểm tra Auth, Portal, Policy & Role
    ├── components/
    │   ├── shared/
    │   │   ├── Breadcrumb.tsx             # Breadcrumb ngữ cảnh theo Portal
    │   │   ├── NavIcon.tsx                # Ánh xạ biểu tượng Lucide React (không dùng emoji)
    │   │   ├── NotificationBell.tsx       # Chuông thông báo + đồng bộ SignalR
    │   │   ├── PortalPlaceholderPage.tsx  # Khung trang chuẩn & Dashboard Skeleton
    │   │   └── UserMenu.tsx               # Menu tài khoản, hiển thị Role & nút Đăng xuất
    │   └── ui/
    │       ├── Avatar.tsx
    │       ├── Badge.tsx
    │       ├── Button.tsx
    │       ├── Card.tsx
    │       ├── Dropdown.tsx
    │       ├── Input.tsx
    │       ├── States.tsx                 # LoadingState, EmptyState, ErrorState, AccessDeniedState
    │       └── Toast.tsx
    ├── features/
    │   ├── auth/
    │   │   ├── AccessDeniedPage.tsx
    │   │   └── LoginPage.tsx
    │   ├── management/
    │   │   └── ManagementPages.tsx
    │   └── resident/
    │       └── ResidentPages.tsx
    ├── hooks/
    │   └── useNotificationsSignalR.ts     # Hook kết nối /hubs/notifications & đồng bộ lại khi reconnect
    ├── lib/
    │   ├── api/
    │   │   ├── client.ts                  # API Client thống nhất (credentials: include + CSRF header)
    │   │   ├── endpoints.ts
    │   │   ├── errors.ts                  # ApiClientError & chuẩn hóa ProblemDetails
    │   │   └── types.ts
    │   ├── auth/
    │   │   ├── authApi.ts
    │   │   └── permissions.ts             # Phân quyền Role/Policy & lọc menu cho từng vai trò
    │   └── validation/
    │       └── loginValidation.ts
    ├── styles/
    │   └── index.css
    ├── test/
    │   ├── setup.ts
    │   ├── apiClient.test.ts
    │   └── authAndLayouts.test.tsx
    └── types/
        ├── auth.ts
        └── notifications.ts
```

---

## 2. Danh sách Dependency & Phiên bản

| Nhóm | Package | Phiên bản | Mục đích |
| :--- | :--- | :--- | :--- |
| **Runtime** | `react` / `react-dom` | `^18.3.1` | Thư viện UI cốt lõi |
| **Routing** | `react-router-dom` | `^6.28.0` | Định tuyến SPA (`/login`, `/resident/*`, `/management/*`) |
| **Data Fetching** | `@tanstack/react-query` | `^5.62.8` | Quản lý server state & cache |
| **Icons** | `lucide-react` | `^0.468.0` | Hệ thống icon thống nhất (không sử dụng emoji) |
| **Realtime** | `@microsoft/signalr` | `^8.0.7` | Kết nối Hub `/hubs/notifications` |
| **Styling** | `tailwindcss`, `clsx`, `tailwind-merge` | `^3.4.17`, `^2.1.1`, `^2.5.5` | Design System theo bảng màu chuẩn (`#172554`, `#2563EB`, `#F8FAFC`) |
| **Build & Type Check** | `typescript`, `vite`, `@vitejs/plugin-react` | `~5.6.3`, `^6.0.5`, `^4.3.4` | Kiểm tra kiểu dữ liệu tĩnh & đóng gói production |
| **Testing** | `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom` | `^2.1.8`, `^25.0.1`, `^16.1.0`, `^14.5.2`, `^6.6.3` | Kiểm thử đơn vị & tích hợp giao diện |

**Môi trường biên dịch thực tế trên máy:**
- `.NET SDK`: `10.0.401`
- `Node.js`: `v22.14.0`
- `npm`: `11.1.0`

---

## 3. Các File Đã Tạo Mới hoặc Chỉnh Sửa

### 3.1. Backend (`ApartmentManagement` & `ApartmentManagement.Tests`)
- **Chỉnh sửa:**
  - [Program.cs](file:///d:/ApartmentManagement/ApartmentManagement/Program.cs):
    - Cấu hình tường minh `builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");`.
    - Bổ sung phục vụ static files từ `frontend/dist` (khi đã build) và định tuyến SPA fallback có kiểm soát chỉ cho `/login`, `/access-denied`, `/lockout`, `/resident`, `/resident/{**slug}`, `/management`, `/management/{**slug}` (không ảnh hưởng `/api/*`, `/hubs/notifications` hay bất kỳ MVC route hiện có nào).
- **Tạo mới:**
  - [AuthApiController.cs](file:///d:/ApartmentManagement/ApartmentManagement/Controllers/AuthApiController.cs): Cung cấp các API xác thực, phiên đăng nhập và CSRF.
  - [AuthApiIntegrationTests.cs](file:///d:/ApartmentManagement/ApartmentManagement.Tests/AuthApiIntegrationTests.cs): 11 bài kiểm thử tích hợp xác minh `/api/auth/csrf`, `/api/auth/login`, `/api/auth/me`, `/api/auth/logout`, kiểm tra bắt buộc Antiforgery token, kiểm tra khóa tài khoản `IsActive`, phân quyền Role/Policy và tương thích song song MVC + SPA.

### 3.2. Frontend (`frontend/`)
- Tạo mới toàn bộ cấu hình và mã nguồn nền tảng trong `frontend/` (31 file mã nguồn và cấu hình).

*(Ghi nhận đối chiếu giữa Audit Phase 0 và Source Code thực tế: Trong `Program.cs` dòng 49, interface dịch vụ quản lý tài khoản thực tế tên là `IAccountManagementService` / `AccountManagementService` thay vì `IUserAccountService` như ghi trong bản nháp Audit Phase 0. Mã nguồn thực tế được lấy làm chuẩn).*

---

## 4. API Mới & API Tái Sử Dụng

| Endpoint | Method | Nguồn | Auth / Bảo mật | Mô tả |
| :--- | :---: | :--- | :--- | :--- |
| `/api/auth/csrf` | `GET` | Mới (`AuthApiController`) | `[AllowAnonymous]` | Sinh Antiforgery cookie (`HttpOnly`) và trả về `{ headerName: "RequestVerificationToken", requestToken }`. |
| `/api/auth/me` | `GET` | Mới (`AuthApiController`) | `[Authorize]` | Trả về `CurrentUserResponse` (UserId, UserName, DisplayName, Email, PhoneNumber, Roles, Portal, DefaultRoute, Permissions, ResidentContext). Không bao giờ trả `PasswordHash` hay `SecurityStamp`. |
| `/api/auth/login` | `POST` | Mới (`AuthApiController`) | `[AllowAnonymous]` + `[ValidateAntiForgeryToken]` | Xác thực qua `SignInManager.PasswordSignInAsync`, kiểm tra `IsActive`, tôn trọng chính sách Lockout, thiết lập cookie `HttpOnly` và trả về `CurrentUserResponse`. |
| `/api/auth/logout` | `POST` | Mới (`AuthApiController`) | `[Authorize]` + `[ValidateAntiForgeryToken]` | Đăng xuất qua `SignInManager.SignOutAsync()`, xóa cookie phiên đăng nhập. |
| `/api/notifications/recent` | `GET` | Tái sử dụng (`NotificationsController`) | `[Authorize]` | Lấy danh sách thông báo gần nhất và `unreadCount` cho `NotificationBell`. |
| `/api/notifications/mark-all-read` | `POST` | Tái sử dụng (`NotificationsController`) | `[Authorize]` + `[ValidateAntiForgeryToken]` | Đánh dấu tất cả thông báo là đã đọc. |
| `/hubs/notifications` | `WS/SSE` | Tái sử dụng (`NotificationHub`) | `[Authorize]` | Kênh SignalR nhận thông báo thời gian thực (`ReceiveNotification`). |

---

## 5. Luồng Authentication, Session & CSRF

### 5.1. Khởi tạo Phiên (Session Initialization)
Khi ứng dụng React khởi động (`AuthProvider.tsx`):
1. Gọi `GET /api/auth/csrf` (`skipUnauthorizedHandler: true`) để thiết lập Antiforgery cookie và lưu `requestToken` trong bộ nhớ RAM của `lib/api/client.ts` (không lưu vào `localStorage` hay `sessionStorage`).
2. Gọi `GET /api/auth/me` (`skipUnauthorizedHandler: true`).
3. Phân loại trạng thái:
   - `200 OK`: Chuyển sang `status = 'authenticated'`, lưu `CurrentUser` và chỉ khi đó mới render Portal.
   - `401 Unauthorized`: Chuyển sang `status = 'unauthenticated'` (không kích hoạt cảnh báo hết hạn phiên giả).
   - Lỗi mạng (`isNetworkError`) hoặc `5xx`: Chuyển sang `status = 'network_error'`, hiển thị màn hình `ErrorState` có nút **Thử lại**, tuyệt đối không nhầm lỗi mạng với `401`.

### 5.2. Luồng CSRF (Antiforgery)
- Backend cấu hình `options.HeaderName = "RequestVerificationToken"`.
- Mọi request thay đổi dữ liệu (`POST`, `PUT`, `PATCH`, `DELETE`) đi qua `apiClient` đều tự động đính kèm header `RequestVerificationToken: <requestToken>` cùng `credentials: 'include'`.
- Ngay sau khi `login` hoặc `logout` thành công, `authApi.ts` tự động gọi lại `GET /api/auth/csrf` để làm mới `requestToken` khớp với `ClaimsPrincipal` mới trong cookie.

### 5.3. Xử lý Hết hạn Phiên (Session Expiry)
- Khi người dùng đang ở trạng thái `authenticated` mà một request API bất kỳ trả về `401 Unauthorized`, listener `onUnauthorizedResponse` trong `AuthProvider` sẽ:
  1. Xóa thông tin `user` hiện tại và dọn sạch `QueryClient` cache.
  2. Đặt cờ `sessionExpired = true` và chuyển về `/login`, hiển thị thông báo cảnh báo rõ ràng cho người dùng.

---

## 6. Bảng Phân Quyền Role / Policy & Định Tuyến (Route Mapping)

### 6.1. Ma trận Role / Policy trên Giao diện & Route Guard

| Phân hệ / Menu | Route React | Policy / Role Yêu cầu | `Resident` | `Technician` | `Accountant` | `BuildingManager` | `SuperAdmin` |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **Auth — Đăng nhập** | `/login` | Public | Có | Có | Có | Có | Có |
| **Auth — Từ chối truy cập** | `/access-denied` | Public / Shared | Có | Có | Có | Có | Có |
| **Resident — Tổng quan** | `/resident/dashboard` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Căn hộ của tôi** | `/resident/apartment` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Hóa đơn & thanh toán** | `/resident/invoices` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Yêu cầu sửa chữa** | `/resident/repairs` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Phản ánh** | `/resident/complaints` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Phương tiện** | `/resident/vehicles` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Khách thăm** | `/resident/visitors` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Tiện ích** | `/resident/amenities` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Thông báo** | `/resident/notifications` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Resident — Tài khoản** | `/resident/account` | `RequireResident` | **Có** | Chặn | Chặn | Chặn | Chặn |
| **Mgmt — Tổng quan** | `/management/dashboard` | Management Roles | Chặn | **Có** | **Có** | **Có** | **Có** |
| **Mgmt — Tòa nhà & căn hộ** | `/management/buildings` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Cư dân** | `/management/residents` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Hợp đồng** | `/management/contracts` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Hóa đơn** | `/management/billing` | `RequireAccountantOrManager` | Chặn | Chặn | **Có** | **Có** | **Có** |
| **Mgmt — Thanh toán & công nợ** | `/management/payments` | `RequireAccountantOrManager` | Chặn | Chặn | **Có** | **Có** | **Có** |
| **Mgmt — Báo cáo** | `/management/reports` | `RequireAccountantOrManager` | Chặn | Chặn | **Có** | **Có** | **Có** |
| **Mgmt — Sửa chữa** | `/management/repairs` | `RequireTechnicianOrManagement` | Chặn | **Có** | Chặn | **Có** | **Có** |
| **Mgmt — Sự cố kỹ thuật** | `/management/technical-incidents` | `RequireTechnicianOrManagement` | Chặn | **Có** | Chặn | **Có** | **Có** |
| **Mgmt — Phương tiện** | `/management/vehicles` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Khách thăm** | `/management/visitors` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Phản ánh** | `/management/complaints` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Tiện ích** | `/management/amenities` | `RequireManagement` | Chặn | Chặn | Chặn | **Có** | **Có** |
| **Mgmt — Thông báo** | `/management/notifications` | Management Roles | Chặn | **Có** | **Có** | **Có** | **Có** |
| **Mgmt — Quản trị tài khoản** | `/management/accounts` | `RequireSuperAdmin` | Chặn | Chặn | Chặn | Chặn | **Có** |

---

## 7. Kết quả Build & Kiểm thử (Build & Test Results)

### 7.1. Kết quả Build
1. **Frontend Production Build (`npm run build` = `tsc -b && vite build`):** **PASS** (0 lỗi TypeScript, đóng gói thành công `dist/index.html`, `dist/assets/index-*.css`, `dist/assets/index-*.js`).
2. **Backend Solution Build (`dotnet build ApartmentManagement.slnx`):** **PASS** (0 Error).

### 7.2. Tổng hợp Kết quả Kiểm thử theo Phân loại

| Bộ kiểm thử | Tổng số | PASS | FAIL | SKIPPED | BLOCKED | NOT RUN | Chi tiết |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Frontend Unit & Component Tests (`vitest run`)** | **12** | **12** | 0 | 0 | 0 | 0 | `apiClient.test.ts` (4 tests) + `authAndLayouts.test.tsx` (8 tests) |
| **Backend Auth API Integration Tests (`AuthApiIntegrationTests`)** | **11** | **11** | 0 | 0 | 0 | 0 | Kiểm thử `/api/auth/csrf`, `/api/auth/login`, `/api/auth/me`, `/api/auth/logout`, CSRF enforcement, Role/Policy mapping, SPA + MVC routing |
| **Toàn bộ Backend Non-SQL Test Suite (`dotnet test`)** | **905** | **905** | 0 | 0 | 0 | 0 | Toàn bộ Unit & Integration Tests (InMemory/SQLite) đều PASS |
| **SQL Server Integration Tests (`[SqlServerFact]` — Mặc định)** | **23** | 0 | 0 | **23** | 0 | 0 | Tự động `SKIPPED` khi không đặt biến môi trường `APARTMENTHUB_SQLSERVER_TEST_CONNECTION` |
| **SQL Server Integration Tests (`[SqlServerFact]` — Chạy trên LocalDB cô lập)** | **23** | **21** | **2** | 0 | 0 | 0 | Chạy trên DB cô lập `ApartmentHubTests_<Guid>`; 2 bài FAIL có sẵn trên nhánh `main` (chi tiết tại Mục 9.4.6) |

*(Ghi chú kỹ thuật: Phân tích nguyên nhân gốc rễ chi tiết của 2 bài `[SqlServerFact]` có sẵn trên nhánh `main` — gồm `SqlServerParkingCardConcurrencyTests` do hex `Guid` vi phạm `CK_Residents_CitizenId_Format` và `SqlServerBatchBillingTests.TC_SQL_BATCH_04` do trigger SQL động xung đột với mệnh đề `OUTPUT` mặc định của EF Core 10 — được trình bày đầy đủ tại Mục 9.4.6 bên dưới).*

---

## 8. Mô tả Các Màn Hình Đã Dựng & Trạng Thái Git

### 8.1. Các Màn hình Đã Dựng trong Phase 1
1. **Auth Layout (`/login`, `/access-denied`):**
   - Giao diện thẻ trung tâm trên nền `#F8FAFC`, thương hiệu `ApartmentManagement` màu Navy `#172554`, ô nhập Tài khoản/Email, Mật khẩu có nút Hiện/Ẩn mật khẩu (`Eye`/`EyeOff`), tùy chọn Ghi nhớ đăng nhập, kiểm tra lỗi đầu vào và hiển thị cảnh báo khi hết hạn phiên hoặc sai thông tin đăng nhập.
2. **Resident Portal Layout (`/resident/*`):**
   - Sidebar sáng hiện đại với điểm nhấn Primary Blue `#2563EB`, hiển thị thẻ thông tin căn hộ đang cư trú (`ResidentContext`), 10 mục điều hướng dành riêng cho Cư dân, Header kèm Breadcrumb, Chuông thông báo (`NotificationBell` kết nối `/hubs/notifications`) và `UserMenu`.
   - Trang `/resident/dashboard` hiển thị khung bố cục Skeleton chuẩn bị cho Phase 2.
3. **Management Portal Layout (`/management/*`):**
   - Sidebar màu Navy đậm `#172554` chia thành 5 nhóm nghiệp vụ (`Điều hành chung`, `Tài sản & Cư trú`, `Tài chính & Kế toán`, `Kỹ thuật & Dịch vụ`, `Quản trị hệ thống`), tự động lọc chặt chẽ theo Role/Policy của người dùng đăng nhập (`SuperAdmin`, `BuildingManager`, `Accountant`, `Technician`).

### 8.2. Trạng thái Git
- Chỉ có các thay đổi thuộc đúng phạm vi Phase 0 & Phase 1 (`Program.cs`, `AuthApiController.cs`, `AuthApiIntegrationTests.cs`, thư mục `frontend/` và tài liệu báo cáo trong `docs/`). Không có bất kỳ file ngoài phạm vi nào bị sửa đổi.

---

## 9. APARTMENT-07A.1.1 — Final Verification & Acceptance Audit

Thực hiện kiểm toán độc lập toàn bộ kết quả `APARTMENT-07A.1` đối chiếu trực tiếp với mã nguồn thực tế, không chỉnh sửa bất kỳ file code nào trong lượt audit này.

### 9.1. Kiểm tra Authentication và CSRF (Đối chiếu Code thực tế)

| STT | Tiêu chí kiểm tra | Kết quả | Bằng chứng đối chiếu code & test |
|---|---|---|---|
| 1 | `GET /api/auth/csrf` cấp Antiforgery cookie và request token đúng cách | **PASS** | `AuthApiController.cs` (dòng 38–50): gọi `_antiforgery.GetAndStoreTokens(HttpContext)` và trả về `{ requestToken, headerName: "RequestVerificationToken" }`. Kiểm chứng bởi `AuthApiIntegrationTests.GetCsrfToken_ReturnsRequestTokenAndSetsAntiforgeryCookie`. |
| 2 | Request token giữ trong RAM; không lưu password, cookie hoặc token nhạy cảm vào `localStorage`/`sessionStorage` | **PASS** | `frontend/src/lib/api/client.ts` (dòng 6–35): lưu `csrfState` trong biến module RAM (`let csrfState: CsrfMemoryState`). Không có bất kỳ lời gọi `localStorage` hoặc `sessionStorage` nào lưu token, cookie hay mật khẩu trong toàn bộ `frontend/src/`. Kiểm chứng bởi `apiClient.test.ts`. |
| 3 | Login thành công cấp Identity authentication cookie đúng cấu hình | **PASS** | `AuthApiController.cs` (dòng 129–139): sau khi xác thực password và trạng thái tài khoản, gọi `_signInManager.SignInAsync(user, isPersistent: request.RememberMe)`. Kiểm chứng bởi `AuthApiIntegrationTests.Login_WithValidCredentials_SetsAuthCookieAndAllowsCurrentUserEndpoint`. |
| 4 | Login thất bại, tài khoản không hoạt động và lockout được xử lý đúng | **PASS** | `AuthApiController.cs`: tài khoản `!user.IsActive` bị chặn tại dòng 102–109 trả về `403 Forbidden`; tài khoản đang bị khóa hoặc vượt ngưỡng sai mật khẩu trả về `423 Locked` (dòng 111–127, `_signInManager.CheckPasswordSignInAsync(..., lockoutOnFailure: true)`); sai thông tin đăng nhập trả về `401 Unauthorized` (dòng 94–100, 141–145). Kiểm chứng bởi 3 bài test tương ứng trong `AuthApiIntegrationTests.cs`. |
| 5 | Logout thực sự kết thúc phiên đăng nhập | **PASS** | `AuthApiController.cs` (dòng 150–159): `[Authorize]` + `[ValidateAntiForgeryToken]` trên `POST /api/auth/logout` gọi `await _signInManager.SignOutAsync()`. Sau khi logout, gọi lại `GET /api/auth/me` trả về `401 Unauthorized`. Kiểm chứng bởi `AuthApiIntegrationTests.Logout_WithValidCsrfToken_SignsOutUser`. |
| 6 | `GET /api/auth/me` không tạo hoặc thay đổi dữ liệu ngoài ý muốn | **PASS** | `AuthApiController.cs` (dòng 53–75) và `BuildUserSessionDtoAsync` (dòng 161–208): chỉ đọc thông tin user, roles và `_context.Residents.AsNoTracking()` (dòng 173–176), không có bất kỳ lệnh `Add`/`Update`/`Remove`/`SaveChangesAsync` nào. |
| 7 | Unauthenticated trả `401`; Forbidden trả `403` | **PASS** | `Program.cs` (dòng 80–97): `OnRedirectToLogin` trả `401 Unauthorized` và `OnRedirectToAccessDenied` trả `403 Forbidden` cho mọi request `/api/*` hoặc `/hubs/*`. Kiểm chứng bởi `AuthApiIntegrationTests`. |
| 8 | Request `POST`/`PUT`/`PATCH`/`DELETE` thiếu CSRF token bị từ chối | **PASS** | `AuthApiController.cs` gắn `[AutoValidateAntiforgeryToken]` ở cấp controller (dòng 18) và `[ValidateAntiForgeryToken]` trên `Login` (dòng 79), `Logout` (dòng 151). Thiếu header `RequestVerificationToken` trả về `400 Bad Request`. Kiểm chứng bởi `Login_WithoutCsrfToken_ReturnsBadRequest` và `Logout_WithoutCsrfToken_ReturnsBadRequest`. |
| 9 | Request có token không hợp lệ hoặc Antiforgery cookie không khớp bị từ chối | **PASS** | `AuthApiIntegrationTests.Login_WithInvalidCsrfToken_ReturnsBadRequest` và `Login_WithMismatchedAntiforgeryCookie_ReturnsBadRequest` xác nhận ASP.NET Core Antiforgery từ chối với `400 Bad Request`. |
| 10 | Network error không bị nhầm với session expiration | **PASS** | `frontend/src/lib/api/client.ts` (dòng 76–91): lỗi `fetch` ném `ApiClientError` với `isNetworkError: true, status: 0, code: 'NETWORK_ERROR'` và không kích hoạt `unauthorizedListener`. `AuthProvider.tsx` (dòng 51–56, 71–76) chuyển `status` sang `'network_error'` thay vì `'unauthenticated'`. Kiểm chứng bởi `authAndLayouts.test.tsx`. |
| 11 | Cookie giữ đúng `HttpOnly`, `Secure` và `SameSite` theo thiết kế hiện tại | **PASS** | `Program.cs` (dòng 60–65, 73–79): Antiforgery cookie (`ApartmentManagement.Antiforgery`) và Identity cookie (`ApartmentManagement.Auth`) đều giữ nguyên `HttpOnly = true`, `SecurePolicy = CookieSecurePolicy.Always`, `SameSite = SameSiteMode.Strict`. Không sửa đổi cấu hình bảo mật cookie để chiều theo test. |

---

### 9.2. Kiểm tra Phân quyền theo Role và Policy (5 Roles)

Đối chiếu giữa `ApartmentManagement/Common/Security/AppRoles.cs` (dòng 3–29), `Program.cs` (dòng 99–126), `AuthApiController.cs` (dòng 178–208), `frontend/src/lib/auth/permissions.ts`, `ProtectedRoute.tsx` và `AppRouter.tsx`:

| Role | Default Route (`AuthApiController.cs`) | Portal được phép (`canAccessPortal`) | Các mục điều hướng hiển thị & Route được phép trong Frontend | Đối chiếu Backend Policy (`Program.cs`) | Kết quả |
|---|---|---|---|---|---|
| `Resident` | `/resident/dashboard` | `resident` (`RequireResident`) | 10 mục thuộc `/resident/*`. Bị chặn hoàn toàn khỏi `/management/*` (`ProtectedRoute` hiển thị `AccessDeniedState`). | Khớp `AppPolicies.RequireResident` (`RequireRole(Resident)`). | **PASS** |
| `BuildingManager` | `/management/dashboard` | `management` | 14/15 mục thuộc `/management/*` (Tổng quan, Tòa nhà & căn hộ, Cư dân, Hợp đồng, Hóa đơn, Thanh toán & công nợ, Báo cáo, Sửa chữa, Sự cố kỹ thuật, Phương tiện, Khách thăm, Phản ánh, Tiện ích, Thông báo). **Không** thấy và **không** vào được `/management/accounts` (`RequireSuperAdmin`). | Khớp `RequireManagement`, `RequireAccountantOrManager`, `RequireTechnicianOrManagement`. | **PASS** |
| `Accountant` | `/management/dashboard` | `management` | Chỉ thấy và truy cập được **5 mục**: Tổng quan (`/management/dashboard`), Hóa đơn (`/management/billing`), Thanh toán & công nợ (`/management/payments`), Báo cáo (`/management/reports`), Thông báo (`/management/notifications`). Bị chặn khỏi Tòa nhà, Cư dân, Hợp đồng, Sửa chữa, Sự cố kỹ thuật, Phương tiện, Khách thăm, Phản ánh, Tiện ích và Quản trị tài khoản. | Khớp `RequireAccountantOrManager` (`SuperAdmin`, `BuildingManager`, `Accountant`). Không cấp vượt quyền backend. | **PASS** |
| `Technician` | `/management/dashboard` | `management` | Chỉ thấy và truy cập được **4 mục**: Tổng quan (`/management/dashboard`), Sửa chữa (`/management/repairs`), Sự cố kỹ thuật (`/management/technical-incidents`), Thông báo (`/management/notifications`). Bị chặn khỏi toàn bộ mục Tài sản & Cư trú, Tài chính & Kế toán, Phương tiện/Khách thăm/Phản ánh/Tiện ích và Quản trị tài khoản. | Khớp `RequireTechnician` và `RequireTechnicianOrManagement`. Không cấp vượt quyền backend. | **PASS** |
| `SuperAdmin` | `/management/dashboard` | `management` | Toàn bộ **15/15 mục** thuộc `/management/*`, bao gồm mục duy nhất dành riêng cho `SuperAdmin`: Quản trị tài khoản (`/management/accounts` — `RequireSuperAdmin`). | Khớp `RequireSuperAdmin` và tất cả policy quản lý. | **PASS** |

---

### 9.3. Kiểm tra SPA Fallback và Tương thích MVC / API / SignalR

Đối chiếu `ApartmentManagement/Program.cs` (dòng 150–196):

1. **Thứ tự định tuyến (Routing Precedence):**
   - `app.MapStaticAssets()` và `app.UseStaticFiles()` (phục vụ `frontend/dist` khi tồn tại).
   - `app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}")`
   - `app.MapRazorPages()`
   - `app.MapHub<NotificationHub>("/hubs/notifications")`
   - Cuối cùng mới đăng ký SPA fallback có phạm vi hẹp (scoped endpoints): `/login`, `/access-denied`, `/lockout`, `/resident`, `/resident/{**slug:nonfile}`, `/management`, `/management/{**slug:nonfile}`.
2. **Xác minh không nuốt route:**
   - `/api/*`: Không khớp với bất kỳ pattern SPA fallback nào; các route tồn tại được xử lý bởi `AuthApiController` / `NotificationsApiController`, các route `/api/*` không tồn tại trả về HTTP `404` chuẩn (không trả `index.html`).
   - `/hubs/notifications`: Được map trực tiếp tới `NotificationHub`, không bị SPA fallback chặn.
   - Các MVC Controllers và Identity Razor Pages hiện hữu (`/Home/Index`, `/ApartmentInvoices`, `/ResidentPortal`, `/Identity/Account/Login`, v.v.): Giữ nguyên hoạt động bình thường.

---

### 9.4. Kiểm tra Build và Test (Kết quả Thực thi Thực tế)

#### 1. Frontend Production Build
- **Lệnh chạy:** `npm run build` (tại `d:\ApartmentManagement\frontend`)
- **Exit code:** `0`
- **Kết quả:** **PASS** (`tsc -b && vite build` biên dịch thành công 1683 modules, sinh `dist/index.html`, `dist/assets/index-cQSaMqVt.css`, `dist/assets/index-0MzlcEMg.js` trong `7.02s`).

#### 2. Frontend Vitest Suite
- **Lệnh chạy:** `npm test` (`vitest run` tại `d:\ApartmentManagement\frontend`)
- **Exit code:** `0`
- **Số lượng:** **12 PASS / 0 FAIL / 0 SKIPPED** (2 test files: `src/test/apiClient.test.ts` — 4 tests; `src/test/authAndLayouts.test.tsx` — 8 tests).
- **Kết quả:** **PASS**.

#### 3. Backend Solution Build
- **Lệnh chạy:** `dotnet build ApartmentManagement.slnx` (tại `d:\ApartmentManagement`)
- **Exit code:** `0`
- **Kết quả:** **PASS** (`0 Error(s)`, `8 Warning(s)` cảnh báo NuGet `NU1902`/`NU1903` có sẵn từ trước của gói transitive `System.Security.Cryptography.Pkcs`).

#### 4. Backend `AuthApiIntegrationTests`
- **Lệnh chạy:** `dotnet test ApartmentManagement.Tests\ApartmentManagement.Tests.csproj --no-build --filter "FullyQualifiedName~AuthApiIntegrationTests"`
- **Exit code:** `0`
- **Số lượng:** **11 PASS / 0 FAIL / 0 SKIPPED** (Tổng: 11 bài integration test kiểm tra CSRF, Login, Inactive, Lockout, Logout, Cookie, Role/Policy payload).
- **Kết quả:** **PASS**.

#### 5. Backend Non-SQL Test Suite
- **Lệnh chạy:** `dotnet test ApartmentManagement.Tests\ApartmentManagement.Tests.csproj --no-build` (không đặt biến môi trường `APARTMENTHUB_SQLSERVER_TEST_CONNECTION`)
- **Exit code:** `0`
- **Số lượng:** **905 PASS / 0 FAIL / 23 SKIPPED** (Tổng: 928 bài test; 23 bài `[SqlServerFact]` tự động skip an toàn khi không cấu hình biến môi trường SQL Server test).
- **Kết quả:** **PASS**.

#### 6. 23 SQL Server Integration Tests (`[SqlServerFact]`) trên môi trường SQL Server Test cô lập
- **Xác minh tính cô lập của môi trường test (`SqlServerTestDatabase.cs`):**
  - Đọc `ApartmentManagement.Tests/SqlServer/SqlServerTestDatabase.cs` (dòng 12–103): `SqlServerTestDatabase.Create()` luôn ghi đè `InitialCatalog = $"ApartmentHubTests_{Guid.NewGuid():N}"` (dòng 24) và kiểm tra bảo vệ nghiêm ngặt `if (!databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))` trước khi tạo hoặc xóa database (dòng 51–55, 92–96). Tuyệt đối không kết nối hoặc tác động tới database chính `ApartmentManagementDb`.
- **Lệnh chạy:**
  ```powershell
  $env:APARTMENTHUB_SQLSERVER_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  dotnet test ApartmentManagement.Tests\ApartmentManagement.Tests.csproj --no-build --filter "FullyQualifiedName~SqlServer"
  ```
- **Exit code:** `1` (Log lưu tại `task-338` / `steps/338/output.txt`)
- **Số lượng:** **21 PASS / 2 FAIL / 0 SKIPPED** (Tổng: 23 bài `[SqlServerFact]`).
- **Phân tích nguyên nhân gốc rễ (Root Cause Analysis) của 2 bài FAIL có sẵn trên nhánh `main` (ngoài phạm vi Phase 1):**
  1. **`SqlServerParkingCardConcurrencyTests.ConcurrentCardIssuance_WithIdenticalCardCode_EnforcesUniqueIndexAndAllowsOnlyOne` (`FAIL`):**
     - **Bằng chứng lỗi:** `Microsoft.Data.SqlClient.SqlException: The INSERT statement conflicted with the CHECK constraint "CK_Residents_CitizenId_Format". The conflict occurred in database "ApartmentHubTests_...", table "dbo.Residents", column 'CitizenId'.`
     - **Nguyên nhân gốc rễ:** Tại `ApartmentManagement.Tests/SqlServer/SqlServerParkingCardConcurrencyTests.cs` (dòng 57), dữ liệu seed test tạo `CitizenId = $"001{Guid.NewGuid():N}"[..12]`. Do `Guid.NewGuid():N` sinh chuỗi hệ thập lục phân (`0-9a-f`), khi 9 ký tự đầu của GUID chứa chữ cái `a-f` (xác suất ~99.8%), SQL Server từ chối lệnh `INSERT` vào bảng `Residents` vì vi phạm ràng buộc `CK_Residents_CitizenId_Format` (chỉ cho phép chữ số `0-9`).
  2. **`SqlServerBatchBillingTests.TC_SQL_BATCH_04_SqlServer_PersistenceFailureIsolation_AllowsSubsequentApartmentToSucceed` (`FAIL`):**
     - **Bằng chứng lỗi:** `Assert.Equal() Failure: Expected: 1, Actual: 0` tại `SqlServerBatchBillingTests.cs:line 465` (`Assert.Equal(1, result.CreatedCount)`).
     - **Nguyên nhân gốc rễ:** Tại `SqlServerBatchBillingTests.cs` (dòng 441–452), bài test chạy lệnh SQL động `CREATE TRIGGER TR_Test_SimulatePersistenceFailure_Apt1 ON ApartmentInvoices AFTER INSERT` để giả lập lỗi lưu hóa đơn ở căn hộ 1 (`apt1`). Tuy nhiên, trong EF Core 10 trên SQL Server, provider mặc định sinh câu lệnh `INSERT INTO [ApartmentInvoices] ... OUTPUT INSERTED.[ApartmentInvoiceId]` khi entity `ApartmentInvoice` không được khai báo `tb.HasTrigger(...)` trong `ApplicationDbContext`. Trên SQL Server, bất kỳ bảng nào có trigger đang kích hoạt đều không cho phép lệnh `INSERT` sử dụng mệnh đề `OUTPUT` trực tiếp mà không có `INTO` (SQL Error 334: *"The target table 'ApartmentInvoices' of the DML statement cannot have any enabled triggers if the statement contains an OUTPUT clause without INTO clause"*). Hệ quả là SQL Server từ chối lệnh `INSERT` của cả `apt1` **lẫn** `apt2`, làm `CreatedCount = 0` và `FailedCount = 2`.
  - **Xác nhận phạm vi:** Cả 2 file `SqlServerParkingCardConcurrencyTests.cs` và `SqlServerBatchBillingTests.cs` (cùng `BillingService.cs` và `ApplicationDbContext.cs`) đều không bị chạm tới trong `APARTMENT-07A.1`. Tuân thủ nguyên tắc Mục 1 của `APARTMENT-07A.1.1` (*"Không tự ý sửa code trong lượt audit này"*), Agent giữ nguyên hiện trạng code và ghi nhận đầy đủ bằng chứng kỹ thuật tại đây.

---

### 9.5. Kiểm tra Git (`git status`, `git diff`, Danh mục File)

- **Lệnh kiểm tra:**
  - `git status --short --branch`
  - `git diff --stat`
  - `git diff --check`
- **Kết quả `git diff --stat`:**
  ```text
  ApartmentManagement/Program.cs | 28 ++++++++++++++++++++++++++++
  1 file changed, 28 insertions(+)
  ```
- **Kết quả `git diff --check`:** Không có lỗi whitespace hay conflict marker.
- **Phân loại toàn bộ file trong working tree:**
  - **File đã sửa (Modified — 1 file):**
    - `ApartmentManagement/Program.cs` (+28 dòng: cấu hình `options.HeaderName = "RequestVerificationToken"` cho Antiforgery và phục vụ static files + scoped SPA fallback cho `frontend/dist`).
  - **File tạo mới (Untracked — thuộc đúng phạm vi Phase 0 & Phase 1):**
    - Backend & Tests (2 files):
      - `ApartmentManagement/Controllers/AuthApiController.cs`
      - `ApartmentManagement.Tests/AuthApiIntegrationTests.cs`
    - Tài liệu báo cáo (2 files):
      - `docs/REACT_FRONTEND_MIGRATION_AUDIT.md`
      - `docs/APARTMENT-07A.1_IMPLEMENTATION_REPORT.md`
    - Ứng dụng React Frontend (`frontend/` — 52 files mã nguồn, cấu hình và unit/component tests; `node_modules/` và `dist/` đã được loại trừ qua `frontend/.gitignore`).
  - **File đã xóa (Deleted):** `0` (Không có file nào bị xóa).
  - **File ngoài phạm vi (Out-of-scope):** `0` (Không có bất kỳ file ngoài phạm vi nào bị chỉnh sửa).

---

### 9.6. Bảng Tổng hợp Kết quả Kiểm toán (`APARTMENT-07A.1.1`)

| Hạng mục | Kết quả | Bằng chứng | Ghi chú |
|---|---|---|---|
| **1. Nguyên tắc & Bảo toàn hiện trạng** | **PASS** | `git status --short`, `git diff --stat` | Không tự ý sửa code trong lượt audit; không chạm DB/migration; không dùng lệnh Git phá hủy. |
| **2. Authentication & CSRF (11 tiêu chí)** | **PASS** | `AuthApiController.cs`, `Program.cs`, `client.ts`, `authApi.ts`, `AuthProvider.tsx`, `AuthApiIntegrationTests.cs` (11/11 PASS) | CSRF token chỉ lưu trong RAM; cookie giữ nguyên `HttpOnly=true`, `Secure=Always`, `SameSite=Strict`; phân biệt rõ `401`, `403`, `423`, `400` và `network_error`. |
| **3. Phân quyền Role & Policy (5 Roles)** | **PASS** | `AppRoles.cs`, `permissions.ts`, `ProtectedRoute.tsx`, `AppRouter.tsx`, `authAndLayouts.test.tsx` (8/8 PASS) | Kiểm chứng đủ 5 Role (`Resident`, `BuildingManager`, `Accountant`, `Technician`, `SuperAdmin`); `Accountant` (5 mục) và `Technician` (4 mục) bị giới hạn chặt chẽ đúng policy backend. |
| **4. SPA Fallback & Tương thích MVC/API/Hub** | **PASS** | `Program.cs` (dòng 150–196), `AuthApiIntegrationTests.cs` | SPA fallback chỉ bắt `/login`, `/access-denied`, `/lockout`, `/resident/*`, `/management/*`; không nuốt `/api/*`, `/hubs/notifications` hoặc MVC/Razor routes. |
| **5.1. Frontend Production Build (`npm run build`)** | **PASS** | Exit code `0` (`task-384`) | `tsc -b && vite build` thành công (1683 modules, `7.02s`). |
| **5.2. Frontend Vitest (`npm test`)** | **PASS** | Exit code `0` — `12 PASS / 0 FAIL / 0 SKIPPED` (`task-384`) | `apiClient.test.ts` (4 PASS) + `authAndLayouts.test.tsx` (8 PASS). |
| **5.3. Backend Build (`dotnet build`)** | **PASS** | Exit code `0` — `0 Error(s)` (`task-391`) | Biên dịch toàn bộ solution `.NET 10` thành công. |
| **5.4. `AuthApiIntegrationTests`** | **PASS** | Exit code `0` — `11 PASS / 0 FAIL / 0 SKIPPED` (`task-391`) | Kiểm thử tích hợp đầy đủ toàn bộ luồng `/api/auth/*` và CSRF. |
| **5.5. Backend Non-SQL Test Suite** | **PASS** | Exit code `0` — `905 PASS / 0 FAIL / 23 SKIPPED` (`task-391`) | Toàn bộ 905 bài test non-SQL (bao gồm 894 bài cũ + 11 bài mới) đều PASS. |
| **5.6. SQL Server Integration Tests (23 `[SqlServerFact]`)** | **FAIL** *(21 PASS / 2 FAIL)* | Exit code `1` — `21 PASS / 2 FAIL / 0 SKIPPED` (`task-338`) | Chạy trên DB cô lập `ApartmentHubTests_<Guid>`. 2 bài FAIL (`SqlServerParkingCardConcurrencyTests` và `TC_SQL_BATCH_04`) là lỗi có sẵn trên nhánh `main` ngoài phạm vi Phase 1 (xem Mục 9.4.6). |
| **6. Kiểm tra Git (`git status`, `git diff`)** | **PASS** | `git status --porcelain -uall`, `git diff --check` | Chỉ sửa 1 file tracked (`Program.cs`, +28 dòng) và thêm các file mới đúng phạm vi Phase 0 & Phase 1. |

---

### 9.7. Kết luận Nghiệm thu Cuối cùng (Final Acceptance Verdict)

- **Kết luận cho phạm vi `APARTMENT-07A.1`:** **CONDITIONALLY ACCEPTED** *(Nghiệm thu có điều kiện — toàn bộ hạng mục thuộc phạm vi `APARTMENT-07A.1` đạt **100% PASS**; ghi nhận 2 bài `[SqlServerFact]` có sẵn từ trước trên nhánh `main` bị FAIL khi biên dịch và chạy lại trên EF Core 10 + SQL Server LocalDB).*
- **Tóm tắt điều kiện & khuyến nghị trước/trong Phase 2:**
  1. Toàn bộ nền tảng React + TypeScript + Vite, Authentication/Session/CSRF (`AuthApiController`, `client.ts`, `AuthProvider`), phân quyền 5 Role/6 Policy, 3 nhóm Layout (`AuthLayout`, `ResidentLayout`, `ManagementLayout`), SignalR hook và tích hợp SPA fallback trong `Program.cs` đã hoàn thành đầy đủ, chính xác và sẵn sàng để chuyển sang **Phase 2**.
  2. Đối với 2 bài test SQL Server có sẵn ngoài phạm vi Phase 1 (`SqlServerParkingCardConcurrencyTests.cs` dòng 57 dùng hex GUID cho `CitizenId` và `SqlServerBatchBillingTests.cs` `TC_SQL_BATCH_04` tạo trigger động trên bảng `ApartmentInvoices` chưa khai báo `HasTrigger` trong EF Core), nếu người dùng cho phép khắc phục riêng trong một task bảo trì test/backend độc lập, chỉ cần:
     - Đổi cách sinh `CitizenId` trong `SqlServerParkingCardConcurrencyTests.cs` sang chuỗi 12 chữ số thuần (`0-9`).
     - Khai báo `tb.HasTrigger(...)` hoặc điều chỉnh cơ chế giả lập lỗi trong `TC_SQL_BATCH_04` để tương thích với mệnh đề `OUTPUT` của EF Core 10.


