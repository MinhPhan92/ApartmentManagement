import { 
  DoorOpen, 
  Users, 
  Receipt, 
  Wrench, 
  Copy, 
  ArrowUpRight,
  ShieldCheck
} from 'lucide-react';
import { 
  LineChart, 
  Line, 
  XAxis, 
  YAxis, 
  Tooltip, 
  ResponsiveContainer, 
  CartesianGrid 
} from 'recharts';
import { useAuth } from '../../app/providers/AuthProvider';
import { useToast } from '../../components/ui/Toast';

const monthlyBillingRevenueData = [
  { period: 'Th05', billed: 420, collected: 395 },
  { period: 'Th06', billed: 450, collected: 430 },
  { period: 'Th07', billed: 480, collected: 460 },
  { period: 'Th08', billed: 510, collected: 492 },
  { period: 'Th09', billed: 530, collected: 508 },
  { period: 'Th10', billed: 560, collected: 545 },
  { period: 'Th11', billed: 590, collected: 572 },
];

export function ManagementDashboardPage() {
  const { user } = useAuth();
  const { pushToast } = useToast();

  const handleCopyPortalLink = () => {
    navigator.clipboard.writeText(window.location.origin + '/resident/dashboard');
    pushToast({
      type: 'success',
      title: 'Đã sao chép liên kết',
      message: 'Đường dẫn Cổng Cư Dân đã được sao chép vào clipboard.',
    });
  };

  return (
    <div className="space-y-6">
      {/* 1. Header Banner & Filter */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-xl">👋</span>
            <h1 className="text-xl font-bold text-slate-900">
              Xin chào, {user?.displayName || 'Ban Quản Lý'}!
            </h1>
          </div>
          <p className="text-xs sm:text-sm text-slate-500 mt-1">
            Hệ thống quản trị vận hành chung cư Enterprise — Giám sát tổng quan tình trạng căn hộ, hóa đơn và yêu cầu kỹ thuật.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <select className="border border-slate-200 rounded-lg px-3 py-1.5 text-xs font-semibold text-slate-700 bg-slate-50 hover:bg-slate-100 transition">
            <option>Tháng 11/2026 (Kỳ hiện tại)</option>
            <option>Quý 4/2026</option>
            <option>Cả năm 2026</option>
          </select>
        </div>
      </div>

      {/* 2. Top 4 KPI Metrics */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {[
          {
            title: "Căn hộ cư trú",
            val: "104 / 125",
            sub: "Tỷ lệ lấp đầy 83.2%",
            grow: "↑ 2.4%",
            color: "bg-blue-600",
            icon: DoorOpen
          },
          {
            title: "Doanh thu kỳ này",
            val: "572.8 tr",
            sub: "Đã thu 96.9% kế hoạch",
            grow: "↑ 4.8%",
            color: "bg-emerald-600",
            icon: Receipt
          },
          {
            title: "Sự cố & Sửa chữa",
            val: "18 đang xử lý",
            sub: "62/80 đã hoàn thành",
            grow: "↓ 12%",
            color: "bg-amber-600",
            icon: Wrench
          },
          {
            title: "Phương tiện đăng ký",
            val: "105 xe",
            sub: "68 ô tô / 37 xe máy",
            grow: "↑ 5.1%",
            color: "bg-indigo-600",
            icon: Users
          }
        ].map((item, idx) => {
          const IconComponent = item.icon;
          return (
            <div key={idx} className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex flex-col justify-between hover:border-slate-300 transition">
              <div className="flex items-center justify-between mb-3">
                <div className={`w-8 h-8 rounded-lg ${item.color} text-white flex items-center justify-center`}>
                  <IconComponent size={16} />
                </div>
                <span className="text-[11px] font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded-full">
                  {item.grow}
                </span>
              </div>
              <div>
                <p className="text-xs text-slate-500 font-medium">{item.title}</p>
                <p className="text-xl font-bold text-slate-900 mt-0.5">{item.val}</p>
                <p className="text-[11px] text-slate-400 mt-1">{item.sub}</p>
              </div>
            </div>
          );
        })}
      </div>

      {/* 3. Main Content: Biểu đồ + Danh sách phòng / Yêu cầu & Cột phụ bên phải */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Cột trái (8/12) */}
        <div className="lg:col-span-8 space-y-6">
          {/* Chart + Recent Requests */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Biểu đồ Doanh thu & Thu ngân */}
            <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between mb-1">
                  <h3 className="font-bold text-sm text-slate-900">Tiến độ Thu phí Dịch vụ</h3>
                  <span className="text-[10px] text-slate-400 font-medium">Đơn vị: Triệu VNĐ</span>
                </div>
                <p className="text-xs text-slate-500 mb-4">So sánh Hóa đơn phát hành và Thực thu</p>
              </div>

              <div className="h-44 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={monthlyBillingRevenueData}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                    <XAxis dataKey="period" stroke="#94a3b8" fontSize={11} tickLine={false} />
                    <YAxis stroke="#94a3b8" fontSize={11} tickLine={false} axisLine={false} />
                    <Tooltip 
                      contentStyle={{ backgroundColor: '#0f172a', borderColor: '#334155', borderRadius: '8px', color: '#fff', fontSize: '11px' }} 
                      itemStyle={{ color: '#fff' }}
                    />
                    <Line type="monotone" name="Phát hành" dataKey="billed" stroke="#3b82f6" strokeWidth={2.5} dot={{ r: 3 }} />
                    <Line type="monotone" name="Đã thu" dataKey="collected" stroke="#10b981" strokeWidth={2.5} dot={{ r: 3 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>

              <div className="flex items-center justify-center gap-6 pt-3 border-t border-slate-100 text-xs">
                <div className="flex items-center gap-1.5">
                  <span className="w-2.5 h-2.5 rounded-full bg-blue-500"></span>
                  <span className="text-slate-600 font-medium">Phát hành</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="w-2.5 h-2.5 rounded-full bg-emerald-500"></span>
                  <span className="text-slate-600 font-medium">Đã thu</span>
                </div>
              </div>
            </div>

            {/* Yêu cầu sửa chữa & Hỗ trợ mới nhất */}
            <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between mb-3">
                <div>
                  <h3 className="font-bold text-sm text-slate-900">Yêu cầu cư dân mới nhất</h3>
                  <p className="text-xs text-slate-500">Phiếu dịch vụ & sửa chữa trong ngày</p>
                </div>
                <span className="text-[11px] font-semibold text-blue-600 bg-blue-50 px-2 py-0.5 rounded-md">8 mới</span>
              </div>

              <div className="space-y-3">
                {[
                  { name: 'Trần Thanh Cư Dân 001', apt: 'E-1508', issue: 'Rò rỉ van nước cấp máy giặt ban công', time: '5 phút trước', status: 'Chờ duyệt', priority: 'Cao' },
                  { name: 'Lê Hoài Cư Dân 002', apt: 'P0202-001', issue: 'Đăng ký cấp thẻ xe máy phụ thứ 2', time: '20 phút trước', status: 'Đang xử lý', priority: 'Thường' },
                  { name: 'Phạm Quốc Cư Dân 003', apt: 'P0203-002', issue: 'Khóa thẻ xe khẩn cấp (thẻ xe CARD-003)', time: '45 phút trước', status: 'Hoàn tất', priority: 'Khẩn' },
                ].map((item, idx) => (
                  <div key={idx} className="p-2.5 rounded-xl bg-slate-50 border border-slate-100 space-y-1">
                    <div className="flex items-center justify-between text-xs">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-800">{item.name}</span>
                        <span className="bg-slate-200 text-slate-700 font-mono text-[10px] px-1.5 py-0.2 rounded font-semibold">
                          {item.apt}
                        </span>
                      </div>
                      <span className="text-[10px] text-slate-400">{item.time}</span>
                    </div>
                    <p className="text-xs text-slate-600 truncate">{item.issue}</p>
                    <div className="flex items-center justify-between pt-1">
                      <span className={`text-[10px] font-semibold px-2 py-0.5 rounded ${
                        item.priority === 'Khẩn' ? 'bg-rose-100 text-rose-700' : 'bg-slate-200 text-slate-700'
                      }`}>
                        Ưu tiên: {item.priority}
                      </span>
                      <span className="text-[10px] font-medium text-blue-600">{item.status}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Bảng danh sách Căn hộ & Hóa đơn tiêu biểu */}
          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-bold text-sm text-slate-900">Danh sách Căn hộ & Trạng thái Công nợ</h3>
                <p className="text-xs text-slate-500">Dữ liệu từ phân hệ căn hộ và hóa đơn thực tế</p>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs text-slate-500 font-medium">Tổng 125 căn hộ</span>
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left text-slate-600">
                <thead className="bg-slate-50 text-slate-400 uppercase text-[10px] border-b border-slate-100">
                  <tr>
                    <th className="py-2.5 px-3">Mã Căn hộ</th>
                    <th className="py-2.5 px-3">Tòa nhà</th>
                    <th className="py-2.5 px-3">Chủ hộ / Cư dân</th>
                    <th className="py-2.5 px-3">Diện tích</th>
                    <th className="py-2.5 px-3">Hóa đơn kỳ này</th>
                    <th className="py-2.5 px-3">Tình trạng</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-medium">
                  {[
                    { apt: "E-1508", bld: "Tòa BLOCK-E", owner: "Trần Thanh Cư Dân 001", area: "75 m²", amount: "1.850.000 đ", status: "Đã thanh toán", statusColor: "text-emerald-700 bg-emerald-50 border-emerald-200" },
                    { apt: "P0202-001", bld: "Tòa BLOCK-A", owner: "Lê Hoài Cư Dân 002", area: "68 m²", amount: "1.420.000 đ", status: "Đã thanh toán", statusColor: "text-emerald-700 bg-emerald-50 border-emerald-200" },
                    { apt: "P0203-002", bld: "Tòa BLOCK-A", owner: "Phạm Quốc Cư Dân 003", area: "82 m²", amount: "2.100.000 đ", status: "Chờ thanh toán", statusColor: "text-amber-700 bg-amber-50 border-amber-200" },
                    { apt: "P0204-003", bld: "Tòa BLOCK-B", owner: "Hoàng Gia Cư Dân 004", area: "90 m²", amount: "2.450.000 đ", status: "Chờ thanh toán", statusColor: "text-amber-700 bg-amber-50 border-amber-200" },
                  ].map((row, i) => (
                    <tr key={i} className="hover:bg-slate-50/80 transition">
                      <td className="py-3 px-3 font-bold text-slate-800">{row.apt}</td>
                      <td className="py-3 px-3 text-slate-600">{row.bld}</td>
                      <td className="py-3 px-3 text-slate-900">{row.owner}</td>
                      <td className="py-3 px-3 text-slate-500">{row.area}</td>
                      <td className="py-3 px-3 font-semibold text-slate-800">{row.amount}</td>
                      <td className="py-3 px-3">
                        <span className={`text-[10px] px-2 py-0.5 rounded-full border font-semibold ${row.statusColor}`}>
                          {row.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        {/* Cột phải (4/12): Sidebar phụ thông tin liên kết & Tiện ích nhanh */}
        <div className="lg:col-span-4 space-y-6">
          {/* Cổng Cư Dân Access Card */}
          <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs space-y-4">
            <h3 className="font-bold text-sm text-slate-900">Liên kết Cổng Cư Dân (Resident Portal)</h3>
            <p className="text-xs text-slate-500">
              Đường dẫn truy cập trực tuyến dành cho cư dân thực hiện thanh toán VietQR và gửi phản ánh.
            </p>
            <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-xl p-2.5 text-xs">
              <span className="text-slate-600 truncate flex-1 font-mono">
                {window.location.origin}/resident/dashboard
              </span>
              <button 
                type="button" 
                onClick={handleCopyPortalLink}
                title="Sao chép liên kết"
                className="text-slate-400 hover:text-slate-700 p-1 rounded hover:bg-slate-200/60 transition"
              >
                <Copy size={15} />
              </button>
            </div>

            {/* Preview Card Mockup */}
            <div className="border border-slate-200 rounded-xl overflow-hidden bg-gradient-to-br from-slate-900 to-blue-950 p-4 text-white text-center space-y-3">
              <div className="flex items-center justify-between text-xs text-slate-300 border-b border-slate-800 pb-2">
                <span className="font-semibold text-white">Resident Portal</span>
                <span className="text-[10px] bg-emerald-500/20 text-emerald-300 px-2 py-0.5 rounded">Active</span>
              </div>
              <p className="text-xs text-slate-300 text-left">
                Giao diện cư dân hiện đại với 10 phân hệ: Căn hộ, Hóa đơn VietQR, Đặt tiện ích, Thẻ xe, Sửa chữa & Phản ánh.
              </p>
              <a 
                href="/resident/dashboard" 
                target="_blank" 
                rel="noreferrer" 
                className="inline-flex items-center justify-center gap-2 w-full bg-blue-600 hover:bg-blue-700 text-white text-xs py-2.5 rounded-lg font-semibold transition"
              >
                <span>Mở Cổng Cư Dân</span>
                <ArrowUpRight size={14} />
              </a>
            </div>

            {/* Alert / Notice Box */}
            <div className="bg-blue-50 border border-blue-200/80 text-blue-900 text-xs p-3.5 rounded-xl space-y-1.5">
              <div className="flex items-center gap-2 font-bold text-blue-950">
                <ShieldCheck size={16} className="text-blue-600 shrink-0" />
                <span>Bảo mật & Phân quyền vận hành:</span>
              </div>
              <ul className="list-disc list-inside space-y-1 text-blue-800 text-[11px] leading-relaxed">
                <li>Quyền Kế toán chỉ mở mục Hóa đơn, Thanh toán & Báo cáo.</li>
                <li>Quyền Kỹ thuật viên phụ trách tiếp nhận phiếu sửa chữa.</li>
                <li>Cư dân chỉ truy cập được dữ liệu thuộc quyền sở hữu căn hộ của mình.</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
