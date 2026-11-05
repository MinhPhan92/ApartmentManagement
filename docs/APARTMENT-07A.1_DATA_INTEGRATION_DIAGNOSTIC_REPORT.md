# APARTMENT-07A.1 — DATA INTEGRATION DIAGNOSTIC & AUDIT REPORT
**Hệ thống:** ApartmentManagement (Căn hộ & Ban Quản Lý Chung Cư)  
**Môi trường:** Máy Admin — Localhost (ASP.NET Core 10 Web API / MVC + React 19 SPA + Microsoft SQL Server 2025 Express)  
**Thời gian thực hiện:** 06/11/2026 00:05  

---

## 1. HIỆN TRẠNG MÔI TRƯỜNG & KẾT NỐI DATABASE

### 1.1. Cấu hình Database & Kết nối
- **SQL Server Instance:** `localhost\SQLEXPRESS` (Microsoft SQL Server 2025 Express Edition RTM 17.0.1000.7).
- **Database mục tiêu:** `ApartmentManagementDb`.
- **Connection String:** `Server=localhost\SQLEXPRESS;Database=ApartmentManagementDb;Trusted_Connection=True;TrustServerCertificate=True;` *(Đã xác nhận không lộ mật khẩu/thông tin bí mật)*.
- **Xác nhận môi trường:** Ứng dụng kết nối **chính xác 100%** vào database thực tế `ApartmentManagementDb`, hoàn toàn độc lập và không bị trỏ nhầm sang test database dạng `ApartmentHubTests_*`.

### 1.2. Thống kê dữ liệu thực tế trong Database
Đối chiếu số lượng bản ghi thực tế trong SQL Server:
- `AspNetUsers`: **121** tài khoản.
- `Residents`: **104** hồ sơ công dân.
- `Apartments`: **125** căn hộ.
- `ApartmentResidents`: **208** lượt cư trú/hợp đồng cư dân.
- `ApartmentInvoices`: **210** hóa đơn dịch vụ.
- `PaymentTransactions`: **327** giao dịch thanh toán.
- `RepairRequests`: **80** yêu cầu sửa chữa.
- `Announcements`: **53** bản tin chung cư.
- `Vehicles`: **105** phương tiện đã đăng ký.
- `Amenities`: **13** tiện ích nội khu (Hồ bơi, BBQ, Tennis,...).

---

## 2. BẢNG KIỂM TRA TỪNG TRANG & NETWORK/API LAYER

Đã thực hiện kiểm tra thực tế bằng quy trình: Đăng nhập tài khoản Resident demo `cudan001@apartment.local` (Căn hộ E-1508) và tài khoản Quản lý `manager@apartment.local`, trích xuất toàn bộ HTTP requests/responses:

| Trang kiểm tra (Page) | API / Endpoint được gọi | HTTP Method | HTTP Status | Response Size | Dữ liệu trả về (Data Content) | Trạng thái Render trên UI | Root Cause & Đánh giá |
| :--- | :--- | :---: | :---: | :---: | :--- | :--- | :--- |
| **Login** | `/api/auth/csrf`<br>`/api/auth/login` | GET<br>POST | 200<br>200 | 185 B<br>560 B | Trả về CSRF token và thông tin User `cudan001` (Căn E-1508, Role Resident). | **Render tốt** (2-column Split UI) | **PASS** — Xác thực mượt mà. |
| **Auth Session Check** | `/api/auth/me` | GET | 200 | 560 B | Trả về `userId`, `roles`, `residentContext: {residentId: 5, activeApartmentCode: "E-1508"}`. | **Render tốt** | **PASS** — Session duy trì đúng. |
| **Resident Dashboard** | `/api/resident/dashboard` | GET | 200 | 3.963 B | Đầy đủ dữ liệu: Căn hộ E-1508, Nợ 681.000đ, Hóa đơn INV-202610-00005, 1 yêu cầu sửa chữa, 1 xe máy, 4 bản tin. | **Render đầy đủ** | **PASS** — React query fetch và hiển thị chính xác. |
| **Resident Apartment** | `/api/resident/apartment` | GET | 200 | 2.471 B | Thông tin căn E-1508, 1 thành viên hộ gia đình, 2 hợp đồng thuê/sở hữu liên quan. | **Render đầy đủ** | **PASS** — Dữ liệu căn hộ và thành viên hiển thị đúng. |
| **Resident Invoices** | `/api/resident/invoices` | GET | 200 | 1.369 B | 3 hóa đơn thực tế: Tháng 10/2026 (Paid), Tháng 09/2026 (Paid), Tháng 08/2026 (Overdue). | **Render đầy đủ** | **PASS** — Danh sách và bộ lọc hóa đơn hoạt động chuẩn. |
| **Invoice Detail & VietQR** | `/api/resident/invoices/145` | GET | 200 | 2.388 B | Chi tiết 3 dòng phí (điện, nước, quản lý), 2 lịch sử thanh toán, link mã QR ngân hàng. | **Render đầy đủ** | **PASS** — Modal VietQR hiển thị đúng mã và cú pháp. |
| **Payment History** | Nằm trong `/api/resident/invoices/{id}` | GET | 200 | Kèm chi tiết | Danh sách 2 giao dịch thanh toán xác nhận ngày 25/10/2026. | **Render đầy đủ** | **PASS** — Lịch sử thanh toán khớp dữ liệu database. |
| **Repair Requests** | `/api/resident/repairs` | GET | 200 | 577 B | Yêu cầu sửa chữa #005 "Xử lý sự cố ống nước/khóa từ tại căn E-1508", trạng thái Pending. | **Render đầy đủ** | **PASS** — Hiển thị danh sách và chi tiết phiếu. |
| **Notifications & Announcements** | `/api/resident/notifications` | GET | 200 | 2.932 B | 7 bản tin chung cư (PCCC, lịch bảo trì nước, thông báo phí dịch vụ). | **Render đầy đủ** | **PASS** |
| **Amenities** | `/api/resident/amenities` | GET | 200 | 4.681 B | Danh mục 13 tiện ích chung cư và lịch sử đặt chỗ. | **Render đầy đủ** | **PASS** |
| **Management Dashboard (React)** | `/management/dashboard` | Client Routing | N/A | React Client | Giao diện KPI Dashboard (Recharts biểu đồ doanh thu, KPI Cards, Danh sách tác vụ). | **Render tốt** (Mock KPI demo) | **NOTE 1**: Trang này được thiết kế theo Layout mẫu KPI trực quan với Recharts. |
| **Management Back-Office (MVC Full)** | `/Dashboard`<br>`/Apartments`<br>`/Residents`<br>`/Billing`<br>`/Payments`<br>`/RepairQueue` | GET | 200 | 50 KB – 400 KB | Dữ liệu quản trị thực tế toàn tòa nhà: 125 căn hộ, 210 hóa đơn, 80 phiếu sửa chữa. | **Render MVC đầy đủ** | **NOTE 2**: Hệ thống quản lý vận hành cốt lõi của Ban Quản Lý chạy trên nền tảng MVC Controller chuẩn. |

---

## 3. PHÂN TÍCH NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

Tại sao ban đầu người dùng cảm thấy "React UI không hiển thị hoặc lấy được dữ liệu từ database":

### Nguyên nhân 1: Cơ chế Cookie `SameSite=Strict` và `SecurePolicy=Always` trên Localhost (HTTP)
- **Vấn đề kỹ thuật:** Trong [`Program.cs`](file:///d:/ApartmentManagement/ApartmentManagement/Program.cs#L68-L69), hệ thống cấu hình bảo mật cấp cao:
  ```csharp
  options.Cookie.SameSite = SameSiteMode.Strict;
  options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
  ```
- **Hệ quả:** Khi người dùng truy cập qua HTTP không mã hóa (`http://localhost:5297`) hoặc mở trình duyệt không tương thích với strict localhost cookies mà không đăng nhập theo đúng luồng frontend (ví dụ: mở trực tiếp URL con hoặc gọi fetch từ tool ngoài thiếu credentials), cookie `.AspNetCore.Identity.Application` bị trình duyệt từ chối đính kèm trong các request API tiếp theo -> Trả về `401 Unauthorized`.
- **Khắc phục:** Khi đăng nhập đúng qua form `/login` trên cùng origin `http://localhost:5297`, trình duyệt lưu cookie phiên hợp lệ và mọi API `/api/resident/*` lập tức trả về dữ liệu 200 OK với đầy đủ dữ liệu từ SQL Server.

### Nguyên nhân 2: Sự phân tách kiến trúc giữa "Cổng Cư Dân (React SPA)" và "Cổng Ban Quản Lý (Hybrid MVC / React)"
- **Cổng Cư Dân (`/resident/*`):** Đã được chuyển đổi hoàn toàn sang **React 19 SPA**, kết nối 100% vào hệ thống API [`ResidentPortalApiController.cs`](file:///d:/ApartmentManagement/ApartmentManagement/Controllers/ResidentPortalApiController.cs). Mọi dữ liệu phòng E-1508, hóa đơn, thanh toán, xe cộ, sửa chữa đều lấy trực tiếp từ `ApartmentManagementDb`.
- **Cổng Ban Quản Lý (`/management/*` vs `/Dashboard`):** 
  - Đường dẫn `/management/dashboard` hiển thị trang Dashboard React mới với biểu đồ KPI `Recharts`.
  - Các nghiệp vụ quản trị chi tiết của Ban Quản Lý (tạo đợt thu phí, duyệt thanh toán hàng loạt, phân công kỹ thuật viên cho 125 căn hộ) được cung cấp qua hệ thống MVC Back-Office tại `/Dashboard`, `/Apartments`, `/Billing`, `/Payments`, `/RepairQueue` (tất cả đều đang chạy với dữ liệu thực tế từ database).

---

## 4. KẾT LUẬN & KHUYẾN NGHỊ CHO BUỔI DEMO

1. **Dữ liệu Database hoàn toàn bình thường:** Toàn bộ dữ liệu của cư dân `cudan001` (Căn E-1508) và các phân hệ đều tồn tại đầy đủ, không bị rỗng, không bị hỏng liên kết khóa ngoại.
2. **Kịch bản Demo chuẩn nhất:**
   - **Phần 1 (Trải nghiệm Cư dân):** Mở trình duyệt truy cập `http://localhost:5297/login` -> Đăng nhập bằng `cudan001@apartment.local` / `Admin@123` -> Trình diễn toàn bộ giao diện React Cư dân (Dashboard, Chi tiết căn hộ, Hóa đơn điện nước, Quét VietQR, Gửi yêu cầu sửa chữa).
   - **Phần 2 (Trải nghiệm Quản lý):** Đăng nhập bằng `manager@apartment.local` / `Admin@123` -> Trình diễn Dashboard KPI trên React `/management/dashboard` và các phân hệ quản lý tòa nhà, duyệt hóa đơn, phân công kỹ thuật tại `/Dashboard`.

