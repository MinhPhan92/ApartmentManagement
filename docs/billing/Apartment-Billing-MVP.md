# ApartmentHub — Nền tảng tính phí và hóa đơn MVP

## Phạm vi hiện thực

- Accountant, BuildingManager và SuperAdmin cấu hình biểu phí theo tòa nhà, nhập chỉ số điện/nước, tính hóa đơn nháp và phát hành hóa đơn.
- Resident chỉ xem hóa đơn đã phát hành gắn với tài khoản của mình.
- Resident có thể yêu cầu tạo duy nhất một giao dịch thanh toán mô phỏng cho toàn bộ hóa đơn (không hỗ trợ trả một phần) và xem VietQR được sinh trong ứng dụng.
- Accountant, BuildingManager và SuperAdmin có thể xem giao dịch và xác nhận mô phỏng; việc quét QR không cập nhật trạng thái thanh toán.
- Mỗi căn hộ/kỳ có tối đa một hóa đơn. Hóa đơn gắn với cư dân đang được đánh dấu Chủ hộ và còn cư trú vào ngày cuối kỳ.
- Phí quản lý theo diện tích được tính đủ tháng theo mức phí có hiệu lực trong toàn bộ kỳ.
- Điện và nước dùng bậc lũy tiến theo lượng tiêu thụ `chỉ số cuối - chỉ số đầu`. Chỉ số đầu kỳ phải khớp lần đọc trước gần nhất; nếu có kỳ sau thì chỉ số cuối phải khớp chỉ số đầu kỳ sau.
- Từng khoản phí được làm tròn tới VND nguyên bằng `AwayFromZero`. Hóa đơn nháp giữ lại diện tích, chỉ số, biểu giá nguồn và lịch biểu giá để đối soát.
- Hóa đơn nháp chưa hiển thị cho Resident. Sau khi Accountant/BuildingManager/SuperAdmin phát hành, hóa đơn xuất hiện trong cổng cư dân.

## Thao tác

1. Cấu hình tài khoản nhận VietQR qua `Payments:VietQr:BankBin`, `Payments:VietQr:AccountNumber` và `Payments:VietQr:AccountName`. Có thể dùng biến môi trường với dấu gạch dưới kép; không lưu thông tin đăng nhập ngân hàng tại đây.
2. Vào **Phí & Hóa đơn → Thêm biểu phí** để tạo ba biểu phí hiệu lực cho tòa nhà: Phí quản lý, Điện và Nước.
3. Với biểu phí quản lý, nhập VND/m²/tháng và để trống bậc giá.
4. Với điện/nước, nhập từng bậc trên một dòng theo dạng `ngưỡng|đơn giá`, dùng `*|đơn giá` ở dòng cuối để chỉ bậc không giới hạn. Ví dụ:

   ```text
   50|2000
   100|2500
   *|3000
   ```

   Nghĩa là 0–50 đơn vị ở giá 2.000 VND, 50 đơn vị kế tiếp ở giá 2.500 VND, phần trên 100 ở giá 3.000 VND. Dùng dấu chấm cho phần thập phân; giá hỗ trợ tối đa 4 chữ số thập phân.

5. Vào **Ghi chỉ số**, chọn căn hộ, loại công tơ và kỳ. Ghi cả chỉ số đầu/cuối; không thể ghi trùng cùng công tơ/kỳ.
6. Vào **Lập hóa đơn nháp**, chọn căn hộ/kỳ và hạn thanh toán. Cần đủ biểu phí và hai chỉ số điện/nước; kỳ phí phải có một Chủ hộ cư trú tại ngày cuối kỳ.
7. Kiểm tra từng dòng, chỉ số, lịch giá và tổng tiền; sau đó chọn **Phát hành cho cư dân**. Cập nhật trạng thái phát hành được kiểm soát bằng RowVersion.
8. Resident mở **Hóa đơn của tôi** để xem các hóa đơn đã phát hành. Hóa đơn của tài khoản khác hoặc hóa đơn nháp không được trả về.
9. Resident có thể tạo một giao dịch mô phỏng cho toàn bộ số tiền hóa đơn và hiển thị QR. Nhân viên đối chiếu độc lập rồi xác nhận tại **Giao dịch**; trạng thái này chỉ là dữ liệu mô phỏng, không xác nhận từ ngân hàng.

## Ranh giới hiện tại

- Không có cổng thanh toán, webhook ngân hàng, chuyển tiền hoặc đối soát ngân hàng thật. Giao dịch và thao tác xác nhận là mô phỏng; QR chỉ mang thông tin thụ hưởng, số tiền và mã tham chiếu.
- Chưa tích hợp hóa đơn điện tử, phí gửi xe hoặc thông báo.
- Chưa có quy trình sửa/hủy biểu phí, hiệu chỉnh chỉ số đã ghi hoặc hủy hóa đơn. Hãy rà soát kỹ số liệu trước khi tạo và phát hành; thay đổi biểu phí cần tạo cấu hình với khoảng hiệu lực mới, không chỉnh sửa dữ liệu lịch sử trực tiếp.
- Đây là MVP thao tác theo từng căn hộ, chưa có chạy hàng loạt toàn tòa nhà, hàng đợi hoặc báo cáo công nợ tổng hợp.
- Migration `ApartmentBilling` đã được tạo trong source và EF snapshot đồng bộ; chưa được áp dụng lên bất kỳ database nào. Chỉ triển khai migration trên database test được phê duyệt sau khi có kế hoạch xác minh riêng.
- Migration `SimulatedPayments` cũng chỉ được sinh trong source; chưa áp dụng vào database. Cấu hình VietQR để trống theo mặc định; chỉ điền thông tin tài khoản nhận đã được phê duyệt trong cấu hình môi trường.
- Kiểm thử InMemory không xác nhận SQL Server constraints, transaction hay RowVersion thực tế. SQL Server verification và UAT thủ công vẫn cần được thực hiện riêng.
