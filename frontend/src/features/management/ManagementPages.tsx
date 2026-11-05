import { ManagementDashboardPage } from './ManagementDashboardPage';
import { Card } from '../../components/ui/Card';
import { Badge } from '../../components/ui/Badge';
import { NavIcon } from '../../components/shared/NavIcon';
import { MANAGEMENT_NAV_GROUPS } from '../../lib/auth/permissions';
import { ArrowUpRight, ExternalLink } from 'lucide-react';

const ALL_MANAGEMENT_ITEMS = MANAGEMENT_NAV_GROUPS.flatMap((g) => g.items);

// Mapping từ React Item sang MVC Route có sẵn dữ liệu và tính năng thực tế
const MANAGEMENT_MVC_ROUTE_MAP: Record<string, { mvcUrl: string; mvcLabel: string; features: string[] }> = {
  'mgmt-buildings-apartments': {
    mvcUrl: '/Apartments',
    mvcLabel: 'Quản lý Căn hộ & Tòa nhà (MVC)',
    features: ['Danh mục 125 căn hộ', 'Lọc theo Tòa tháp & Tầng', 'Trạng thái Đang ở / Trống', 'Thêm mới & Cập nhật diện tích']
  },
  'mgmt-residents': {
    mvcUrl: '/Residents',
    mvcLabel: 'Quản lý Cư dân & Cư trú (MVC)',
    features: ['Hồ sơ công dân & CCCD', 'Liên kết phòng cư trú', 'Lưu trú & Hộ gia đình (/Occupancies)', 'Cấp tài khoản Cư dân']
  },
  'mgmt-contracts': {
    mvcUrl: '/Contracts',
    mvcLabel: 'Quản lý Hợp đồng (MVC)',
    features: ['Vòng đời Hợp đồng căn hộ', 'Trạng thái Nháp / Hiệu lực / Kết thúc', 'Các bên ký kết & Điều khoản', 'Kiểm soát RowVersion']
  },
  'mgmt-billing': {
    mvcUrl: '/Billing',
    mvcLabel: 'Quản lý Thu phí & Hóa đơn (MVC)',
    features: ['Biểu giá điện, nước, dịch vụ', 'Ghi chỉ số đồng hồ theo lô', 'Phát hành 210 hóa đơn dịch vụ', 'Hủy & lập lại hóa đơn']
  },
  'mgmt-payments': {
    mvcUrl: '/Payments',
    mvcLabel: 'Quản lý Thanh toán & Công nợ (MVC)',
    features: ['327 giao dịch chuyển khoản', 'Xác nhận & Từ chối thanh toán', 'Đối soát công nợ theo căn hộ', 'Tích hợp VietQR EMVCo']
  },
  'mgmt-reports': {
    mvcUrl: '/Reports',
    mvcLabel: 'Báo cáo & Thống kê Tài chính (MVC)',
    features: ['Sao kê tài chính hàng tháng', 'Biểu đồ cơ cấu doanh thu & chi phí', 'Tỷ lệ thu hồi công nợ', 'Xuất file CSV UTF-8 BOM']
  },
  'mgmt-repairs': {
    mvcUrl: '/RepairQueue',
    mvcLabel: 'Hàng đợi Sửa chữa & Phân công (MVC)',
    features: ['80 yêu cầu sửa chữa căn hộ', 'Phân công Kỹ thuật viên phụ trách', 'Cập nhật tiến độ & Vật tư', 'Cư dân nghiệm thu / Đánh giá']
  },
  'mgmt-technical-incidents': {
    mvcUrl: '/TechnicalIncidents',
    mvcLabel: 'Sự cố Kỹ thuật Tòa nhà (MVC)',
    features: ['Tiếp nhận sự cố hạ tầng chung', 'Phân công đội kỹ thuật tòa nhà', 'Báo cáo hoàn thành & Nghiệm thu', 'Lịch sử nhật ký xử lý']
  },
  'mgmt-vehicles': {
    mvcUrl: '/Vehicle',
    mvcLabel: 'Duyệt Phương tiện & Cấp thẻ xe (MVC)',
    features: ['105 phương tiện đăng ký', 'Duyệt hồ sơ xe cư dân', 'Cấp mới thẻ bãi đỗ CARD-001', 'Khóa & Cấp đổi thẻ xe']
  },
  'mgmt-visitors': {
    mvcUrl: '/Visitors',
    mvcLabel: 'Kiểm soát Khách thăm (MVC)',
    features: ['Danh sách khách đăng ký', 'Thực hiện Check-in an ninh', 'Thực hiện Check-out khi rời đi', 'Kiểm tra căn hộ đón khách']
  },
  'mgmt-complaints': {
    mvcUrl: '/Complaint',
    mvcLabel: 'Tiếp nhận Phản ánh Cư dân (MVC)',
    features: ['Theo dõi khiếu nại cư dân', 'Phản hồi & Cập nhật tiến độ', 'Chuyển thành Phiếu sửa chữa', 'Đánh giá mức độ hài lòng']
  },
  'mgmt-amenities': {
    mvcUrl: '/AmenityManagement',
    mvcLabel: 'Quản lý Đặt Tiện ích (MVC)',
    features: ['Danh mục 13 tiện ích tòa nhà', 'Hồ bơi, BBQ, Tennis, Phòng Gym', 'Duyệt lịch đặt chỗ của cư dân', 'Kiểm soát khung giờ vận hành']
  },
  'mgmt-notifications': {
    mvcUrl: '/Notifications',
    mvcLabel: 'Thông báo & Bản tin Chung cư (MVC)',
    features: ['Trung tâm thông báo toàn khu', 'Gửi bản tin PCCC, cấp nước', 'Đẩy thông báo Realtime SignalR', 'Theo dõi tỷ lệ đọc thông báo']
  },
  'mgmt-accounts': {
    mvcUrl: '/Accounts',
    mvcLabel: 'Quản trị Tài khoản Hệ thống (MVC)',
    features: ['121 tài khoản người dùng', 'Phân quyền 5 vai trò hệ thống', 'Khóa & Mở khóa tài khoản', 'Bảo vệ SuperAdmin cuối cùng']
  }
};

export function ManagementRoutePage({ itemId }: { itemId: string }) {
  if (itemId === 'mgmt-dashboard') {
    return <ManagementDashboardPage />;
  }

  const item =
    ALL_MANAGEMENT_ITEMS.find((entry) => entry.id === itemId) ??
    ALL_MANAGEMENT_ITEMS[0];

  const mvcMapping = MANAGEMENT_MVC_ROUTE_MAP[itemId];

  return (
    <div className="space-y-6">
      {/* 1. Header Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
        <div className="flex items-start gap-4">
          <div className="h-12 w-12 rounded-xl bg-blue-50 text-[#2563EB] flex items-center justify-center shrink-0 mt-0.5">
            <NavIcon name={item.iconName} className="h-6 w-6" />
          </div>
          <div>
            <div className="flex items-center gap-2 flex-wrap">
              <h1 className="text-2xl font-bold text-slate-900">{item.label}</h1>
              <Badge variant="navy">Cổng Ban Quản Lý</Badge>
              {item.policy && <Badge variant="primary">{item.policy}</Badge>}
            </div>
            <p className="text-sm text-slate-600 mt-1">{item.description}</p>
          </div>
        </div>

        {mvcMapping && (
          <a
            href={mvcMapping.mvcUrl}
            className="inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl bg-[#2563EB] hover:bg-blue-700 text-white text-sm font-semibold shadow-xs transition shrink-0"
          >
            <span>Mở giao diện vận hành đầy đủ</span>
            <ExternalLink size={16} />
          </a>
        )}
      </div>

      {/* 2. Content Card: Chi tiết phân hệ & Cầu nối dữ liệu thực tế */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <div className="lg:col-span-8 space-y-6">
          <Card
            title={`Phân hệ Vận hành: ${item.label}`}
            subtitle="Đang kết nối cơ sở dữ liệu ApartmentManagementDb (SQL Server)"
          >
            <div className="space-y-4">
              <p className="text-sm text-slate-600 leading-relaxed">
                {item.description} Toàn bộ cơ sở dữ liệu thực tế, các biểu mẫu nghiệp vụ, bộ lọc phân trang và tác vụ cập nhật đã được triển khai sẵn sàng trên nền tảng quản trị của hệ thống.
              </p>

              {mvcMapping && (
                <div className="bg-slate-50 border border-slate-200/80 rounded-xl p-4 space-y-3">
                  <div className="text-xs font-bold uppercase tracking-wider text-slate-500">
                    Các chức năng & Dữ liệu đang hoạt động trực tiếp:
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                    {mvcMapping.features.map((feat, idx) => (
                      <div key={idx} className="flex items-center gap-2 text-xs text-slate-700 font-medium">
                        <span className="w-1.5 h-1.5 rounded-full bg-blue-600"></span>
                        <span>{feat}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              <div className="pt-2 flex flex-wrap gap-3">
                {mvcMapping && (
                  <a
                    href={mvcMapping.mvcUrl}
                    className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-50 text-blue-700 hover:bg-blue-100 text-xs font-semibold transition border border-blue-200"
                  >
                    <span>Truy cập trực tiếp: {mvcMapping.mvcUrl}</span>
                    <ArrowUpRight size={14} />
                  </a>
                )}
                <a
                  href="/management/dashboard"
                  className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold transition"
                >
                  <span>Quay lại Dashboard KPI</span>
                </a>
              </div>
            </div>
          </Card>
        </div>

        {/* 3. Cột phụ: Hướng dẫn & Lối tắt Demo */}
        <div className="lg:col-span-4 space-y-6">
          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs space-y-4">
            <h3 className="font-bold text-sm text-slate-900">Thông tin Vận hành & Demo</h3>
            <p className="text-xs text-slate-500 leading-relaxed">
              Dự án sử dụng mô hình kết hợp (Hybrid Portal): Cổng Cư Dân được tối ưu bằng React SPA hiện đại, song hành cùng Cổng Ban Quản Lý chuyên sâu quản trị 125 căn hộ.
            </p>

            {mvcMapping && (
              <a
                href={mvcMapping.mvcUrl}
                className="w-full flex items-center justify-between p-3.5 rounded-xl border border-blue-200 bg-gradient-to-r from-blue-50 to-indigo-50 text-blue-900 hover:border-blue-300 transition"
              >
                <div>
                  <div className="text-xs font-bold">{mvcMapping.mvcLabel}</div>
                  <div className="text-[11px] text-blue-700 mt-0.5">Nhấp để mở với đầy đủ quyền hạn</div>
                </div>
                <ArrowUpRight size={18} className="text-blue-600" />
              </a>
            )}

            <div className="border-t border-slate-100 pt-3">
              <a
                href="/resident/dashboard"
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center justify-center gap-2 w-full py-2 rounded-lg bg-slate-900 hover:bg-slate-800 text-white text-xs font-semibold transition"
              >
                <span>Mở Cổng Cư Dân (E-1508)</span>
                <ArrowUpRight size={14} />
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
