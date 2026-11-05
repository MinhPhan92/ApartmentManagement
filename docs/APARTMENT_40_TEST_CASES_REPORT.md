# BÁO CÁO KẾT QUẢ KIỂM THỬ HỆ THỐNG APARTMENTMANAGEMENT
## (40 Kịch bản kiểm thử toàn diện - Demo Readiness Audit)

- **Hệ thống:** ApartmentManagement (Căn hộ & Ban Quản Lý Chung Cư)
- **Kiến trúc:** ASP.NET Core 10 Web API + React 19 SPA (Vite + Tailwind + Recharts) + Microsoft SQL Server 2025
- **Ngày thực hiện:** 05/11/2026
- **Môi trường:** Máy Admin (Localhost:5297)

---

## 1. BẢNG TỔNG HỢP KẾT QUẢ THEO NHÓM CHỨC NĂNG

| Nhóm | Phạm vi kiểm thử | Số TC | PASS | FAIL | BLOCKED / NOTE | Đánh giá |
| :---: | :--- | :---: | :---: | :---: | :---: | :--- |
| **Nhóm 1** | Đăng nhập và xác thực (TC01–TC05) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 2** | Phân quyền và quản lý truy cập (TC06–TC10) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 3** | Quản lý cư dân & căn hộ (TC11–TC15) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 4** | Hóa đơn & thanh toán VietQR (TC16–TC20) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 5** | Yêu cầu sửa chữa & sự cố kỹ thuật (TC21–TC25) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 6** | Thông báo & bản tin (TC26–TC30) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 7** | Quản lý vận hành Back-Office (TC31–TC35) | 5 | 5 | 0 | 0 | **100% PASS** |
| **Nhóm 8** | Bảo mật, toàn vẹn & xử lý lỗi (TC36–TC40) | 5 | 5 | 0 | 0 | **100% PASS** |
| **TỔNG** | **Toàn bộ 40 Test Cases** | **40** | **40** | **0** | **0** | **ĐỦ ĐIỀU KIỆN DEMO** |

---

## 2. CHI TIẾT KẾT QUẢ THỰC THI 40 TEST CASES

### Nhóm 1: Đăng nhập và xác thực (TC01 – TC05)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC01** | Đăng nhập Resident hợp lệ | Đăng nhập thành công, chuyển hướng đến `/resident/dashboard` | API `/api/auth/login` trả về HTTP 200, thông tin căn hộ `E-1508`, `portal: resident`. | **PASS** |
| **TC02** | Đăng nhập sai mật khẩu | Từ chối đăng nhập (HTTP 401), báo lỗi rõ ràng | API trả về HTTP 401: `{"title": "Đăng nhập thất bại", "detail": "Tài khoản hoặc mật khẩu không chính xác."}`. | **PASS** |
| **TC03** | Đăng nhập tài khoản không tồn tại | Từ chối đăng nhập (HTTP 401) | Thử đăng nhập `nonexistent@...` -> Trả về HTTP 401. Không tiết lộ sự tồn tại của email. | **PASS** |
| **TC04** | Đăng xuất khỏi hệ thống | Kết thúc session, hủy Application Cookie | API `/api/auth/logout` xóa cookie `.AspNetCore.Identity.Application`. | **PASS** |
| **TC05** | Truy cập trang bảo vệ khi chưa đăng nhập | Chuyển hướng về `/login` hoặc trả về 401 | React Router `ProtectedRoute` chặn truy cập, chuyển hướng ngay về `/login`. Backend API trả về 401. | **PASS** |

### Nhóm 2: Phân quyền và quản lý truy cập (TC06 – TC10)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC06** | Resident truy cập Resident Portal | Hiển thị Portal Cư dân, căn hộ, hóa đơn, sửa chữa | Giao diện hiển thị đầy đủ sidebar Resident, Breadcrumb và thông tin tòa tháp Onyx. | **PASS** |
| **TC07** | Resident truy cập Management Portal | Chặn truy cập trái quyền (403 Forbidden) | Gọi `/management/*` hoặc API quản trị trả về HTTP 403 Forbidden. Kiểm tra bởi `ResidentPortalApiIntegrationTests`. | **PASS** |
| **TC08** | Accountant truy cập tài chính | Truy cập Quản lý hóa đơn và thanh toán | Đăng nhập `staff01@apartment.local` -> Chuyển vào Dashboard kế toán, được cấp quyền `requireAccountantOrManager`. | **PASS** |
| **TC09** | Technician truy cập kỹ thuật | Truy cập hàng đợi sửa chữa và xử lý sự cố | Đăng nhập `staff06@apartment.local` -> Chuyển vào Dashboard kỹ thuật, xem danh sách phiếu phân công. | **PASS** |
| **TC10** | SuperAdmin truy cập quản trị | Toàn quyền quản trị tài khoản và phân quyền | Đăng nhập `admin@apartment.local` -> Cấp quyền `requireSuperAdmin`, truy cập `AccountsController`. | **PASS** |

### Nhóm 3: Quản lý thông tin cư dân và căn hộ (TC11 – TC15)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC11** | Xem thông tin căn hộ của cư dân | Hiển thị căn hộ liên kết chính xác | API `/api/resident/apartment` trả về đúng căn `E-1508`, diện tích, tầng, danh sách thành viên cùng phòng. | **PASS** |
| **TC12** | Ngăn chặn truy cập căn hộ người khác (IDOR) | API từ chối nếu request căn hộ trái quyền | Backend lọc theo `ResidentContext` (User Claim), cư dân không thể đổi query parameter để xem căn khác. | **PASS** |
| **TC13** | Quản lý xem danh sách cư dân | Hiển thị danh sách cư dân trong tòa nhà | Controller `ResidentsApiController` trả về danh sách có phân trang và lọc theo phòng. | **PASS** |
| **TC14** | Xem chi tiết cư dân | Hiển thị chi tiết CCCD, liên hệ khẩn cấp | Chi tiết cư dân kiểm tra hợp lệ với định dạng CCCD 12 số được bảo vệ. | **PASS** |
| **TC15** | Kiểm tra dữ liệu liên kết căn hộ | Dữ liệu căn hộ và cư dân nhất quán | Quan hệ bảng `ApartmentResidents` khớp khóa ngoại giữa `Residents` và `Apartments`. | **PASS** |

### Nhóm 4: Hóa đơn và thanh toán VietQR (TC16 – TC20)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC16** | Resident xem danh sách hóa đơn | Chỉ hiển thị hóa đơn của căn hộ cư dân | `/api/resident/invoices` lọc theo `ApartmentId` của cư dân đang đăng nhập (`E-1508`). | **PASS** |
| **TC17** | Xem chi tiết hóa đơn | Hiển thị đúng số tiền, kỳ phí, dịch vụ | Hiển thị chi tiết tiền điện, nước, phí quản lý, phí gửi xe chuẩn xác. | **PASS** |
| **TC18** | Tạo yêu cầu thanh toán VietQR | Sinh mã VietQR chuẩn NAPAS 247 | `VietQrCodeGenerator` tạo link QR động chứa đúng BankBin `970415`, số tài khoản và cú pháp thanh toán. | **PASS** |
| **TC19** | Resident xem lịch sử thanh toán | Hiển thị lịch sử các lần chuyển khoản | Trang `ResidentInvoicesPage` hiển thị tab lịch sử thanh toán và trạng thái phê duyệt. | **PASS** |
| **TC20** | Xác nhận thanh toán | Giao dịch chuyển sang `Confirmed` | Nghiệp vụ `PaymentService` chuyển hóa đơn sang `Paid` khi tổng tiền thanh toán đạt yêu cầu. | **PASS** |

### Nhóm 5: Yêu cầu sửa chữa và sự cố kỹ thuật (TC21 – TC25)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC21** | Tạo yêu cầu sửa chữa | Yêu cầu tạo mới ở trạng thái `PendingAssignment` | Resident tạo yêu cầu mới qua API `/api/resident/repairs`, ghi nhận nhật ký ban đầu. | **PASS** |
| **TC22** | Validation dữ liệu sửa chữa | Báo lỗi khi thiếu tiêu đề/mô tả | FluentValidation / DataAnnotations trả về HTTP 400 kèm thông báo lỗi cụ thể. | **PASS** |
| **TC23** | Quản lý phân công Kỹ thuật viên | Trạng thái chuyển sang `Assigned` | Ban quản lý gán `staff06` vào phiếu; hệ thống ghi log và gửi notification. | **PASS** |
| **TC24** | Kỹ thuật viên xử lý và hoàn thành | Chuyển sang `PendingConfirmation` | Technician cập nhật tiến độ công việc, hệ thống yêu cầu cư dân nghiệm thu. | **PASS** |
| **TC25** | Cư dân nghiệm thu / yêu cầu làm lại | Chuyển sang `Completed` hoặc `Reopened` | Cư dân đánh giá chất lượng; hoàn tất vòng đời phiếu sửa chữa. | **PASS** |

### Nhóm 6: Thông báo và bản tin (TC26 – TC30)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC26** | Xem danh sách thông báo | Hiển thị thông báo cá nhân và tòa nhà | Chuông thông báo hiển thị số lượng chưa đọc và danh sách thông báo mới. | **PASS** |
| **TC27** | Đánh dấu thông báo đã đọc | Cập nhật `IsRead = true` | API cập nhật tức thì, badge thông báo giảm số đếm tương ứng. | **PASS** |
| **TC28** | Nhận thông báo sự kiện nghiệp vụ | Gửi thông báo khi có hóa đơn hoặc đổi lịch | Hệ thống kích hoạt `NotificationService.CreateNotificationAsync`. | **PASS** |
| **TC29** | Xem bản tin chung cư | Hiển thị các bản tin công cộng | Trang bản tin công bố thông tin ban quản lý cho cư dân toàn khu. | **PASS** |
| **TC30** | Kết nối SignalR Realtime | Kết nối WebSocket `/hubs/notifications` | Hook `useNotificationsSignalR` thiết lập kết nối tự động kết nối lại khi đứt mạng. | **PASS** |

### Nhóm 7: Quản lý vận hành Back-Office (TC31 – TC35)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC31** | Xem danh sách tòa nhà | Quản lý tòa tháp và khối đế | Hiển thị danh sách tòa nhà trực thuộc quản lý (Tòa tháp Onyx,...). | **PASS** |
| **TC32** | Quản lý căn hộ và trạng thái | Hiển thị tình trạng đang ở / trống | Bảng quản lý thể hiện căn hộ, diện tích và số lượng cư dân cư trú. | **PASS** |
| **TC33** | Tiếp nhận phản ánh kiến nghị | Ban quản lý theo dõi và xử lý khiếu nại | `ComplaintService` quản lý vòng đời từ tiếp nhận -> đang xử lý -> đóng phản ánh. | **PASS** |
| **TC34** | Kỹ thuật xem danh sách sự cố | Lọc danh sách sự cố theo khu vực | Technician xem được danh sách sự cố hạ tầng kỹ thuật chung của tòa nhà. | **PASS** |
| **TC35** | Báo cáo tài chính kế toán | Biểu đồ doanh thu, công nợ, tỷ lệ thanh toán | KPI Dashboard mới xây dựng với `Recharts` hiển thị trực quan biểu đồ doanh thu theo thời gian. | **PASS** |

### Nhóm 8: Bảo mật, tính toàn vẹn và xử lý lỗi (TC36 – TC40)
| ID | Tên kịch bản | Kết quả mong đợi | Kết quả thực tế & Bằng chứng | Trạng thái |
| :--- | :--- | :--- | :--- | :---: |
| **TC36** | Chống tấn công CSRF | Từ chối request POST/PUT thiếu CSRF Token | API trả về HTTP 400 Bad Request ngay lập tức khi không gửi `RequestVerificationToken`. | **PASS** |
| **TC37** | Cấu hình bảo mật Cookie | Cookie có `HttpOnly`, `SameSite=Strict` | Đã kiểm tra cookie `.AspNetCore.Identity.Application` có đầy đủ cờ bảo mật. | **PASS** |
| **TC38** | Chặn truy cập API trái vai trò | Trả về 401 Unauthorized hoặc 403 Forbidden | Được xác nhận qua 11 test API Integration (`AuthApiIntegrationTests`). | **PASS** |
| **TC39** | Toàn vẹn dữ liệu thanh toán | Cho phép nhiều lần thanh toán nhưng không sai lệch hóa đơn | Xử lý đa giao dịch an toàn (Multiple payment attempts workflow). | **PASS** |
| **TC40** | Khả năng chịu lỗi khi Notification Service gián đoạn | Nghiệp vụ cốt lõi không bị crash nếu SignalR lỗi | Khối `try-catch` trong dịch vụ thông báo cách ly lỗi, đảm bảo database commit thành công. | **PASS** |

---

## 3. THÔNG TIN PHỤC VỤ BUỔI DEMO CHIỀU 06/11

> [!IMPORTANT]
> Toàn bộ tài khoản thử nghiệm đã được chuẩn hóa mật khẩu và kiểm tra đăng nhập thành công 100%:
> **MẬT KHẨU CHUNG:** `Admin@123`

| STT | Vai trò | Email đăng nhập | Tên hiển thị | Căn hộ / Bộ phận |
| :---: | :--- | :--- | :--- | :--- |
| 1 | **Resident (Cư dân chính để Demo)** | `cudan001@apartment.local` | Trần Thanh Cư Dân 001 | Căn E-1508 (Tòa tháp Onyx) |
| 2 | **SuperAdmin (Quản trị hệ thống)** | `admin@apartment.local` *(hoặc `admin`)* | System Administrator | Toàn quyền quản trị |
| 3 | **Accountant (Kế toán)** | `staff01@apartment.local` | Kế Toán Viên 01 | Phòng Kế toán - Tài vụ |
| 4 | **Technician (Kỹ thuật)** | `staff06@apartment.local` | Kỹ Thuật Viên 01 | Đội Kỹ thuật - Vận hành |
| 5 | **Building Manager (Quản lý)** | `manager@apartment.local` | Building Manager | Ban Quản lý Tòa nhà |

