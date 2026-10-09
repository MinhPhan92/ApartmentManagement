# ApartmentHub — UAT Incident Ticketing MVP

**Phân hệ:** Quản lý phiếu sự cố trên web\
**Phiên bản tài liệu:** 1.0\
**Ngày lập:** 09/10/2026\
**Môi trường UAT:** Chưa xác định\
**Người kiểm thử:** Chưa phân công\
**Trạng thái:** Chuẩn bị UAT — chưa thực hiện

> Tài liệu này là kế hoạch và biểu mẫu ghi nhận UAT, không phải bằng chứng nghiệm thu. Không đánh dấu Passed nếu chưa thực hiện kịch bản và lưu bằng chứng. Chỉ chạy UAT trên môi trường được phê duyệt với dữ liệu giả lập; không dùng tài khoản, dữ liệu hoặc database production.

## 1. Phạm vi

### Bao gồm

- Cư dân gửi phiếu sự cố văn bản cho căn hộ đang cư trú, xem danh sách/chi tiết phiếu của mình.
- Nhân viên Technician, BuildingManager hoặc SuperAdmin mở hàng đợi, nhận phiếu và hoàn tất phiếu được giao cho mình.
- Cư dân gửi đánh giá 1–5 sao, kèm nhận xét tùy chọn, cho phiếu đã hoàn tất.
- Kiểm soát xác thực, phân quyền, quyền sở hữu, antiforgery, cập nhật đồng thời và lịch sử trạng thái.
- Kiểm tra migration và các ràng buộc trên SQL Server trong database kiểm thử riêng, nếu được cấp phép.

### Ngoài phạm vi

Ứng dụng iOS/Android, push notification, đính kèm hình ảnh/video, thanh toán, mã QR, đặt tiện ích, xuất báo cáo và triển khai production.

## 2. Vai trò và dữ liệu kiểm thử

Chỉ dùng tài khoản và dữ liệu tổng hợp do người phụ trách môi trường UAT cấp.

| Mã | Vai trò / dữ liệu | Dùng để |
|---|---|---|
| RES-A | Resident A, có hồ sơ cư dân và căn hộ A đang cư trú | Gửi và xem ticket của mình |
| RES-B | Resident B, có hồ sơ cư dân và căn hộ B đang cư trú | Kiểm tra phân tách dữ liệu |
| RES-NO-OCC | Resident có tài khoản nhưng không có căn hộ đang cư trú | Kiểm tra điều kiện tạo ticket |
| TECH-A | Technician | Nhận và hoàn tất ticket |
| TECH-B | Technician khác | Kiểm tra quyền hoàn tất/nhận ticket |
| MGR-A | BuildingManager | Truy cập hàng đợi theo quyền hiện tại |
| ADM-A | SuperAdmin | Truy cập hàng đợi theo quyền hiện tại |
| ACC-A | Accountant | Xác nhận bị từ chối hàng đợi |
| ANON | Chưa đăng nhập | Kiểm tra authentication |
| APT-A / APT-B | Hai căn hộ tổng hợp, mỗi căn hộ thuộc một Resident khác nhau | Kiểm tra giới hạn căn hộ |

Không ghi mật khẩu hoặc connection string vào biên bản, ảnh chụp, log chia sẻ hay tài liệu UAT.

## 3. Luồng thao tác và URL dự kiến

1. Đăng nhập bằng `RES-A`, mở `/MyIncidents`, chọn **Gửi phiếu sự cố**, chọn căn hộ đang cư trú, loại sự cố, tiêu đề và mô tả; gửi phiếu.
2. Ghi mã ticket được tạo. Xác nhận ticket hiển thị trong danh sách và trạng thái ban đầu là **Tiếp nhận**.
3. Đăng xuất; đăng nhập bằng `TECH-A`, mở `/IncidentQueue`, vào ticket và chọn **Tiếp nhận phiếu**.
4. Xác nhận trạng thái **Đang xử lý** và ticket được giao cho `TECH-A`.
5. Từ `TECH-A`, chọn **Đánh dấu hoàn thành**. Xác nhận trạng thái **Hoàn thành**.
6. Đăng nhập lại bằng `RES-A`, mở ticket trong `/MyIncidents`; gửi đánh giá từ 1 đến 5 sao và nhận xét tùy chọn.
7. Xác nhận đánh giá được hiển thị và không thể gửi đánh giá thứ hai.

Ứng dụng cần được cấu hình theo HTTPS trong môi trường UAT để kiểm tra cookie và antiforgery đúng như cấu hình triển khai.

## 4. Ma trận UAT chức năng và bảo mật

Trạng thái ban đầu của mọi trường hợp là **Not Run**.

| ID | Ưu tiên | Vai trò | Kịch bản | Kết quả mong đợi | Trạng thái |
|---|---|---|---|---|---|
| IT-UAT-01 | P0 | ANON | Mở `/MyIncidents` | Không xem được; chuyển tới đăng nhập | Not Run |
| IT-UAT-02 | P0 | ANON | Mở `/IncidentQueue` | Không xem được; chuyển tới đăng nhập | Not Run |
| IT-UAT-03 | P0 | ACC-A | Mở `/IncidentQueue` | Bị từ chối ở server; không xem dữ liệu ticket | Not Run |
| IT-UAT-04 | P0 | RES-A | Mở `/IncidentQueue` | Bị từ chối ở server | Not Run |
| IT-UAT-05 | P0 | RES-A | Gửi ticket hợp lệ cho APT-A | Tạo đúng một ticket, trạng thái Tiếp nhận, mã ticket được hiển thị | Not Run |
| IT-UAT-06 | P0 | RES-A | Gửi ticket cho APT-B hoặc sửa ApartmentId trong request | Bị từ chối; không tạo ticket cho căn hộ không thuộc occupancy đang hoạt động | Not Run |
| IT-UAT-07 | P0 | RES-NO-OCC | Mở form/tự gửi ticket khi không có occupancy đang hoạt động | Không tạo ticket; hiển thị thông báo phù hợp | Not Run |
| IT-UAT-08 | P1 | RES-A | Bỏ trống tiêu đề/mô tả hoặc vượt giới hạn ký tự | Validation từ chối; dữ liệu không được lưu | Not Run |
| IT-UAT-09 | P1 | RES-A | Gửi category không thuộc danh sách hợp lệ bằng cách sửa request | Bị từ chối; không tạo ticket | Not Run |
| IT-UAT-10 | P0 | RES-A | Mở danh sách và chi tiết ticket của mình | Chỉ thấy ticket thuộc tài khoản RES-A; thông tin căn hộ và trạng thái chính xác | Not Run |
| IT-UAT-11 | P0 | RES-A | Đổi ID trên URL thành ticket của RES-B; thử thêm `userId`/`residentId` trên query string | Trả 404 hoặc kết quả không tiết lộ ticket của RES-B | Not Run |
| IT-UAT-12 | P0 | TECH-A | Mở hàng đợi và nhận ticket Submitted | Ticket chuyển sang Đang xử lý, được gán cho TECH-A, lịch sử ghi người/thời điểm | Not Run |
| IT-UAT-13 | P0 | TECH-B | Hoàn tất ticket đang được giao cho TECH-A | Bị từ chối; ticket vẫn Đang xử lý và assignee không đổi | Not Run |
| IT-UAT-14 | P0 | TECH-A và TECH-B | Hai nhân viên đồng thời nhận cùng một ticket Submitted | Chỉ một thao tác thành công; thao tác còn lại nhận xung đột/thông báo tải lại; không có hai assignee | Not Run |
| IT-UAT-15 | P0 | TECH-A | Hoàn tất ticket đang được giao cho TECH-A | Chuyển sang Hoàn thành, ghi CompletedAt và lịch sử trạng thái | Not Run |
| IT-UAT-16 | P0 | TECH-A | Gửi lại request hoàn tất với RowVersion cũ sau khi ticket đã đổi | Không ghi đè trạng thái; báo xung đột hoặc ticket không còn đủ điều kiện | Not Run |
| IT-UAT-17 | P0 | RES-A | Đánh giá ticket chưa hoàn tất | Bị từ chối; rating/feedback không được lưu | Not Run |
| IT-UAT-18 | P0 | RES-A | Đánh giá ticket đã hoàn tất với 1 và 5 sao, nhận xét tùy chọn | Giá trị biên hợp lệ được lưu đúng một lần; nhận xét tùy chọn được giữ nguyên | Not Run |
| IT-UAT-19 | P0 | RES-A | Đánh giá ticket của RES-B hoặc gửi đánh giá lần thứ hai | Không thay đổi ticket khác; đánh giá thứ hai không ghi đè đánh giá ban đầu | Not Run |
| IT-UAT-20 | P0 | RES-A / TECH-A | Thử POST tạo/nhận/hoàn tất/đánh giá không có antiforgery token | Request bị từ chối; không có thay đổi dữ liệu | Not Run |
| IT-UAT-21 | P1 | RES-A | Kiểm tra chuỗi trạng thái và lịch sử ticket từ tạo đến đánh giá | Hiển thị đúng các lần chuyển trạng thái, thời điểm và người thực hiện | Not Run |
| IT-UAT-22 | P0 | RES-A / nhân viên | Hoàn tất toàn bộ luồng ticket | Không có thay đổi ngoài ý muốn lên ApartmentResident, MoveOutDate hoặc trạng thái căn hộ | Not Run |

Với các request bị từ chối, xác minh cả phản hồi giao diện lẫn dữ liệu sau thao tác. Ẩn nút trên giao diện không được tính là kiểm chứng phân quyền.

## 5. Checklist kỹ thuật SQL Server

Chỉ thực hiện trên SQL Server và database kiểm thử riêng đã được phê duyệt. Không chạy `database update` với connection mặc định không được xác định rõ là môi trường test. Không lấy hoặc in connection string từ Docker/configuration.

| ID | Kiểm tra | Bằng chứng yêu cầu | Trạng thái |
|---|---|---|---|
| SQL-01 | Xác nhận target server/database là môi trường test được duyệt trước khi migration | Tên môi trường/database đã che thông tin nhạy cảm; phê duyệt người phụ trách | Not Run |
| SQL-02 | Migration `20261009080111_IncidentTicketing` tạo đúng hai bảng ticket và status history | Log migration hoặc schema diff | Not Run |
| SQL-03 | Unique index trên TicketCode ngăn mã trùng | Kết quả thử insert trùng bị từ chối | Not Run |
| SQL-04 | Foreign keys tới Apartment, Resident, assignee/actor user và IncidentTicketStatusHistory hoạt động đúng | Kết quả kiểm tra FK/delete behavior | Not Run |
| SQL-05 | Check constraints chặn category/status/rating không hợp lệ | Kết quả insert/update không hợp lệ bị từ chối | Not Run |
| SQL-06 | `RowVersion` được SQL Server quản lý và request stale bị phát hiện | Kết quả hai cập nhật cạnh tranh; chỉ một được chấp nhận | Not Run |
| SQL-07 | Ticket và status history cùng commit hoặc cùng rollback | Bằng chứng giao dịch không để lại trạng thái thiếu lịch sử | Not Run |
| SQL-08 | Database UAT được dọn theo quy trình sau khi được người phụ trách cho phép | Tên database test đã dọn; xác nhận thủ công | Not Run |

Không xoá database/container/volume hoặc thay đổi cấu hình dịch vụ nếu chưa được người phụ trách môi trường phê duyệt. Kiểm tra SQL trong suite tự động đang bị skip không được tính là Passed.

## 6. Quy tắc ghi nhận kết quả

- **Passed:** Đã chạy trên môi trường ghi rõ, kết quả đúng như mong đợi và có bằng chứng tham chiếu.
- **Failed:** Đã chạy nhưng kết quả sai; tạo defect với mức độ, bước tái hiện và bằng chứng.
- **Blocked:** Không thể chạy vì thiếu dữ liệu, quyền, môi trường hoặc phụ thuộc; ghi rõ lý do.
- **Not Run:** Chưa thực hiện. Không cộng vào Passed.

### Bảng tổng hợp phiên UAT

| Thông tin | Giá trị |
|---|---|
| Môi trường / build | Chưa xác định |
| Ngày, giờ thực hiện | Chưa thực hiện |
| Người kiểm thử | Chưa phân công |
| Tổng kịch bản | 22 |
| Passed | 0 |
| Failed | 0 |
| Blocked | 0 |
| Not Run | 22 |
| SQL Server verification | Pending |
| Kết luận | Chưa thực hiện UAT — chưa đủ điều kiện nghiệm thu |

### Nhật ký kết quả từng lần chạy

| ID | Trạng thái | Thời điểm | Người chạy | Bằng chứng (log/screenshot/test ID) | Defect / ghi chú |
|---|---|---|---|---|---|
| | | | | | |
| | | | | | |
| | | | | | |

## 7. Biểu mẫu ghi nhận lỗi

- **Defect ID:**
- **Test Case ID:**
- **Môi trường/build:**
- **Mức độ:** Critical / High / Medium / Low
- **Vai trò/tài khoản test:**
- **Điều kiện trước:**
- **Các bước tái hiện:**
- **Kết quả mong đợi:**
- **Kết quả thực tế:**
- **Bằng chứng tham chiếu:** Không đính kèm secret hoặc dữ liệu cá nhân
- **Người phụ trách:**
- **Trạng thái xử lý:**

## 8. Tiêu chí nghiệm thu

Chỉ đề xuất nghiệm thu khi:

1. Tất cả kịch bản P0 đã Passed; mọi Failed/Blocked được xử lý hoặc có ngoại lệ được phê duyệt.
2. Không còn lỗi Critical hoặc High liên quan tới lộ dữ liệu, vượt quyền, thao tác ticket sai hoặc ghi nhận trạng thái/lịch sử không nhất quán.
3. SQL-01 đến SQL-07 được thực hiện trên SQL Server test được duyệt; SQL-08 được xác nhận theo quy trình môi trường.
4. Có đầy đủ log hoặc bằng chứng có thể kiểm tra lại; không sử dụng kết quả skipped làm bằng chứng pass.
5. Người phụ trách kỹ thuật, kiểm thử và nghiệp vụ xem xét, ký xác nhận kết quả.

### Kết luận và phê duyệt

**Kết luận hiện tại:** CHƯA THỰC HIỆN UAT — CHƯA NGHIỆM THU INCIDENT TICKETING MVP.

- Đại diện phát triển:
- Đại diện kiểm thử:
- Đại diện nghiệp vụ:
- Ngày phê duyệt:

## 9. Baseline automated tests được báo cáo

Kết quả kiểm thử gần nhất được báo cáo trước khi lập tài liệu: **88 passed, 0 failed, 6 SQL Server tests skipped**; build **0 warnings, 0 errors**; EF Core không báo pending model changes. Đây là baseline automated test, không phải kết quả UAT thủ công hoặc xác minh migration trên SQL Server thật. Cần đính kèm log gốc của phiên chạy khi thực hiện nghiệm thu.
