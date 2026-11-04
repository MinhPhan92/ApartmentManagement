import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowRight,
  Bell,
  CalendarCheck,
  Car,
  CreditCard,
  Home,
  MessageSquareWarning,
  Receipt,
  ShieldCheck,
  UserCheck,
  Users,
  Wrench,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { formatDateTimeVi, formatDateVi, formatVnd } from '../../lib/formatters';
import { Badge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';

function getInvoiceBadgeVariant(state: string) {
  switch (state) {
    case 'Paid':
      return 'success' as const;
    case 'Partial':
      return 'info' as const;
    case 'Overdue':
      return 'danger' as const;
    default:
      return 'warning' as const;
  }
}

function getInvoiceStateLabel(state: string) {
  switch (state) {
    case 'Paid':
      return 'Đã thanh toán';
    case 'Partial':
      return 'Thanh toán một phần';
    case 'Overdue':
      return 'Quá hạn';
    case 'Void':
      return 'Đã hủy';
    default:
      return 'Chưa thanh toán';
  }
}

export function ResidentDashboardPage() {
  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'dashboard'],
    queryFn: () => residentApi.getDashboard(),
  });

  if (isLoading) {
    return <LoadingState label="Đang tải bảng điều khiển cư dân..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải thông tin tổng quan"
        description={
          error instanceof Error
            ? error.message
            : 'Đã xảy ra lỗi khi tải bảng điều khiển cư dân.'
        }
        onRetry={() => void refetch()}
      />
    );
  }

  const recentAnnouncements = data.recentAnnouncements ?? [];
  const recentRepairs = data.recentRepairs ?? [];
  const recentVisitors = data.recentVisitors ?? [];

  const quickServices = [
    {
      label: 'Đặt lịch tiện ích',
      desc: 'BBQ, Hồ bơi, Sân thể thao, Phòng sinh hoạt',
      to: '/resident/amenities',
      icon: CalendarCheck,
      accent: 'bg-blue-50 text-[#2563EB]',
    },
    {
      label: 'Thanh toán hóa đơn',
      desc: 'Tra cứu phí điện, nước, quản lý & quét VietQR',
      to: '/resident/invoices',
      icon: CreditCard,
      accent: 'bg-emerald-50 text-emerald-700',
    },
    {
      label: 'Phương tiện & Thẻ xe',
      desc: 'Đăng ký xe, quản lý thẻ từ & khóa thẻ khẩn cấp',
      to: '/resident/vehicles',
      icon: Car,
      accent: 'bg-indigo-50 text-indigo-700',
    },
    {
      label: 'Đăng ký khách thăm',
      desc: 'Đăng ký khách đến căn hộ & nhận báo khách đến',
      to: '/resident/visitors',
      icon: UserCheck,
      accent: 'bg-amber-50 text-amber-700',
    },
    {
      label: 'Yêu cầu sửa chữa',
      desc: 'Báo sửa chữa kỹ thuật căn hộ & nghiệm thu',
      to: '/resident/repairs',
      icon: Wrench,
      accent: 'bg-sky-50 text-sky-700',
    },
    {
      label: 'Gửi phản ánh / Góp ý',
      desc: 'Tương tác trực tiếp với Ban Quản lý khu đô thị',
      to: '/resident/complaints',
      icon: MessageSquareWarning,
      accent: 'bg-rose-50 text-rose-700',
    },
  ];

  return (
    <div className="space-y-6">
      {/* Hero Banner */}
      <div className="rounded-2xl bg-gradient-to-r from-[#172554] via-[#1E3A8A] to-[#2563EB] p-6 text-white shadow-sm">
        <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-medium text-blue-100 mb-3">
              <ShieldCheck className="w-3.5 h-3.5" />
              <span>Cổng dịch vụ cư dân trực tuyến</span>
            </div>
            <h1 className="text-2xl font-bold tracking-tight">
              Xin chào, {data.userFullName}
            </h1>
            <p className="text-sm text-blue-100 mt-1">
              {data.activeApartment
                ? `Căn hộ ${data.activeApartment.apartmentCode} — ${data.activeApartment.buildingName} (Tầng ${data.activeApartment.floor}, ${data.activeApartment.area} m²)`
                : 'Tài khoản cư dân của bạn hiện chưa có bản ghi căn hộ đang cư trú.'}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <Link
              to="/resident/amenities"
              className="inline-flex items-center gap-2 rounded-xl bg-white px-4 py-2.5 text-sm font-semibold text-[#172554] shadow-sm hover:bg-blue-50 transition-colors"
            >
              <CalendarCheck className="w-4 h-4 text-[#2563EB]" />
              <span>Đặt tiện ích ngay</span>
            </Link>
            <Link
              to="/resident/invoices"
              className="inline-flex items-center gap-2 rounded-xl bg-white/15 hover:bg-white/25 px-4 py-2.5 text-sm font-semibold text-white transition-colors"
            >
              <Receipt className="w-4 h-4" />
              <span>Xem hóa đơn</span>
            </Link>
          </div>
        </div>
      </div>

      {/* KPI Summary Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
        <Card className="p-5">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-[#64748B]">
                Công nợ hiện tại
              </p>
              <p className="mt-2 text-2xl font-bold text-[#0F172A] tabular-nums">
                {formatVnd(data.totalOutstandingDebt)}
              </p>
              <p className="mt-1 text-xs text-[#475569]">
                {data.unpaidInvoiceCount > 0
                  ? `${data.unpaidInvoiceCount} hóa đơn chờ thanh toán`
                  : 'Đã thanh toán đầy đủ'}
              </p>
            </div>
            <div className="w-10 h-10 rounded-xl bg-blue-50 text-[#2563EB] flex items-center justify-center">
              <Receipt className="w-5 h-5" />
            </div>
          </div>
        </Card>

        <Card className="p-5">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-[#64748B]">
                Căn hộ & Hộ gia đình
              </p>
              <p className="mt-2 text-2xl font-bold text-[#0F172A]">
                {data.activeApartment ? data.activeApartment.apartmentCode : 'Chưa gán'}
              </p>
              <p className="mt-1 text-xs text-[#475569]">
                {data.householdCount} thành viên đang cư trú
              </p>
            </div>
            <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-700 flex items-center justify-center">
              <Home className="w-5 h-5" />
            </div>
          </div>
        </Card>

        <Card className="p-5">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-[#64748B]">
                Phương tiện & Thẻ xe
              </p>
              <p className="mt-2 text-2xl font-bold text-[#0F172A] tabular-nums">
                {data.registeredVehiclesCount} xe
              </p>
              <p className="mt-1 text-xs text-[#475569]">
                {data.activeParkingCardsCount} thẻ gửi xe đang hoạt động
              </p>
            </div>
            <div className="w-10 h-10 rounded-xl bg-indigo-50 text-indigo-700 flex items-center justify-center">
              <Car className="w-5 h-5" />
            </div>
          </div>
        </Card>

        <Card className="p-5">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-[#64748B]">
                Sửa chữa & Thông báo
              </p>
              <p className="mt-2 text-2xl font-bold text-[#0F172A] tabular-nums">
                {data.activeRepairsCount} yêu cầu
              </p>
              <p className="mt-1 text-xs text-[#475569]">
                {data.unreadNotificationsCount} thông báo chưa đọc
              </p>
            </div>
            <div className="w-10 h-10 rounded-xl bg-amber-50 text-amber-700 flex items-center justify-center">
              <Wrench className="w-5 h-5" />
            </div>
          </div>
        </Card>
      </div>

      {/* Quick Access Grid */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-bold text-[#0F172A]">
            Tiện ích & Dịch vụ Cư dân
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {quickServices.map((srv) => {
            const Icon = srv.icon;
            return (
              <Link
                key={srv.to}
                to={srv.to}
                className="group rounded-xl border border-[#E2E8F0] bg-white p-4 shadow-xs hover:border-[#2563EB] hover:shadow-md transition-all flex items-start gap-3.5"
              >
                <div
                  className={`w-11 h-11 rounded-xl flex items-center justify-center shrink-0 ${srv.accent}`}
                >
                  <Icon className="w-5 h-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between gap-2">
                    <h3 className="text-sm font-semibold text-[#0F172A] group-hover:text-[#2563EB] transition-colors">
                      {srv.label}
                    </h3>
                    <ArrowRight className="w-4 h-4 text-[#94A3B8] group-hover:text-[#2563EB] group-hover:translate-x-0.5 transition-all" />
                  </div>
                  <p className="text-xs text-[#475569] mt-1 line-clamp-2">{srv.desc}</p>
                </div>
              </Link>
            );
          })}
        </div>
      </div>

      {/* Main Content Columns */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Latest Invoice */}
        <Card className="p-5 lg:col-span-2 flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3.5 mb-4">
              <div className="flex items-center gap-2">
                <Receipt className="w-4 h-4 text-[#2563EB]" />
                <h2 className="text-base font-bold text-[#0F172A]">
                  Hóa đơn dịch vụ kỳ gần nhất
                </h2>
              </div>
              <Link
                to="/resident/invoices"
                className="text-xs font-semibold text-[#2563EB] hover:underline"
              >
                Xem tất cả hóa đơn
              </Link>
            </div>

            {data.latestInvoice ? (
              <div className="space-y-4">
                <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[#F8FAFC] p-4 border border-[#E2E8F0]">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-bold text-[#0F172A]">
                        {data.latestInvoice.periodLabel}
                      </span>
                      <Badge variant={getInvoiceBadgeVariant(data.latestInvoice.paymentState)}>
                        {getInvoiceStateLabel(data.latestInvoice.paymentState)}
                      </Badge>
                    </div>
                    <p className="text-xs text-[#475569] mt-1">
                      Mã hóa đơn: <span className="font-mono">{data.latestInvoice.invoiceCode}</span>{' '}
                      • Hạn thanh toán: {formatDateVi(data.latestInvoice.dueDate)}
                    </p>
                  </div>
                  <Link
                    to={`/resident/invoices?invoiceId=${data.latestInvoice.apartmentInvoiceId}`}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-[#2563EB] px-3.5 py-2 text-xs font-semibold text-white hover:bg-[#1E40AF] transition-colors"
                  >
                    <CreditCard className="w-3.5 h-3.5" />
                    <span>Chi tiết & Thanh toán VietQR</span>
                  </Link>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div className="rounded-lg border border-[#E2E8F0] p-3">
                    <p className="text-xs text-[#64748B]">Tổng phát hành</p>
                    <p className="text-base font-bold text-[#0F172A] mt-0.5 tabular-nums">
                      {formatVnd(data.latestInvoice.totalAmount)}
                    </p>
                  </div>
                  <div className="rounded-lg border border-[#E2E8F0] p-3">
                    <p className="text-xs text-[#64748B]">Đã xác nhận thu</p>
                    <p className="text-base font-bold text-[#15803D] mt-0.5 tabular-nums">
                      {formatVnd(data.latestInvoice.confirmedAmount)}
                    </p>
                  </div>
                  <div className="rounded-lg border border-[#E2E8F0] p-3">
                    <p className="text-xs text-[#64748B]">Còn phải thanh toán</p>
                    <p className="text-base font-bold text-[#B91C1C] mt-0.5 tabular-nums">
                      {formatVnd(data.latestInvoice.remainingAmount)}
                    </p>
                  </div>
                </div>
              </div>
            ) : (
              <EmptyState
                title="Chưa có hóa đơn phát hành"
                description="Hiện chưa có hóa đơn dịch vụ nào được phát hành cho căn hộ của bạn."
              />
            )}
          </div>
        </Card>

        {/* Announcements from Management */}
        <Card className="p-5">
          <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3.5 mb-4">
            <div className="flex items-center gap-2">
              <Bell className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">Bản tin Ban Quản lý</h2>
            </div>
            <Link
              to="/resident/notifications"
              className="text-xs font-semibold text-[#2563EB] hover:underline"
            >
              Tất cả
            </Link>
          </div>

          {recentAnnouncements.length > 0 ? (
            <div className="space-y-3">
              {recentAnnouncements.map((ann) => (
                <Link
                  key={ann.announcementId}
                  to="/resident/notifications?tab=announcements"
                  className="block rounded-lg border border-[#E2E8F0] p-3 hover:border-[#2563EB] transition-colors"
                >
                  <div className="flex items-center justify-between gap-2">
                    <Badge variant={ann.isPinned ? 'warning' : 'info'}>{ann.category}</Badge>
                    <span className="text-[11px] text-[#64748B]">{ann.displayDate}</span>
                  </div>
                  <p className="text-sm font-semibold text-[#0F172A] mt-1.5 line-clamp-1">
                    {ann.title}
                  </p>
                  <p className="text-xs text-[#475569] mt-1 line-clamp-2">{ann.content}</p>
                </Link>
              ))}
            </div>
          ) : (
            <EmptyState
              title="Chưa có bản tin mới"
              description="Các thông báo vận hành từ Ban Quản lý sẽ hiển thị tại đây."
            />
          )}
        </Card>
      </div>

      {/* Bottom Row: Repairs & Visitors */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card className="p-5">
          <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3.5 mb-4">
            <div className="flex items-center gap-2">
              <Wrench className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                Yêu cầu sửa chữa gần đây
              </h2>
            </div>
            <Link
              to="/resident/repairs"
              className="text-xs font-semibold text-[#2563EB] hover:underline"
            >
              Quản lý sửa chữa
            </Link>
          </div>

          {recentRepairs.length > 0 ? (
            <div className="divide-y divide-[#E2E8F0]">
              {recentRepairs.map((rep) => (
                <div key={rep.repairRequestId} className="py-3 flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-[#0F172A] truncate">
                      #{rep.repairRequestId} — {rep.title}
                    </p>
                    <p className="text-xs text-[#64748B] mt-0.5">
                      Căn hộ {rep.apartmentCode} • {formatDateTimeVi(rep.createdAt)}
                    </p>
                  </div>
                  <Badge
                    variant={
                      rep.status === 'Completed'
                        ? 'success'
                        : rep.status === 'PendingConfirmation'
                        ? 'warning'
                        : rep.status === 'Cancelled'
                        ? 'neutral'
                        : 'info'
                    }
                  >
                    {rep.status}
                  </Badge>
                </div>
              ))}
            </div>
          ) : (
            <EmptyState
              title="Chưa có yêu cầu sửa chữa"
              description="Bạn có thể gửi yêu cầu hỗ trợ kỹ thuật bất cứ lúc nào."
            />
          )}
        </Card>

        <Card className="p-5">
          <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3.5 mb-4">
            <div className="flex items-center gap-2">
              <Users className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                Lịch hẹn khách thăm gần đây
              </h2>
            </div>
            <Link
              to="/resident/visitors"
              className="text-xs font-semibold text-[#2563EB] hover:underline"
            >
              Đăng ký khách mới
            </Link>
          </div>

          {recentVisitors.length > 0 ? (
            <div className="divide-y divide-[#E2E8F0]">
              {recentVisitors.map((vis) => (
                <div key={vis.visitorPassId} className="py-3 flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-[#0F172A] truncate">
                      {vis.visitorName}
                    </p>
                    <p className="text-xs text-[#64748B] mt-0.5">
                      Ngày đến: {formatDateVi(vis.expectedArrivalDate)}
                      {vis.purpose ? ` • ${vis.purpose}` : ''}
                    </p>
                  </div>
                  <Badge
                    variant={
                      vis.status === 'Đã vào'
                        ? 'success'
                        : vis.status === 'Đã đăng ký'
                        ? 'info'
                        : 'neutral'
                    }
                  >
                    {vis.status}
                  </Badge>
                </div>
              ))}
            </div>
          ) : (
            <EmptyState
              title="Chưa có lịch hẹn khách"
              description="Đăng ký trước thông tin khách để lễ tân hỗ trợ Check-in nhanh chóng."
            />
          )}
        </Card>
      </div>
    </div>
  );
}
