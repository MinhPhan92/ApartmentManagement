import { Card } from '../ui/Card';
import { Badge } from '../ui/Badge';
import { NavIcon } from './NavIcon';
import type { PortalNavItem } from '../../lib/auth/permissions';

export interface PortalPlaceholderPageProps {
  title: string;
  description: string;
  iconName: PortalNavItem['iconName'];
  portalLabel: string;
  policyLabel?: string;
  isDashboardSkeleton?: boolean;
}

export function PortalPlaceholderPage({
  title,
  description,
  iconName,
  portalLabel,
  policyLabel,
  isDashboardSkeleton = false,
}: PortalPlaceholderPageProps) {
  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-slate-200 pb-4">
        <div className="flex items-start gap-3">
          <div className="h-10 w-10 rounded-lg bg-blue-50 text-[#2563EB] flex items-center justify-center shrink-0 mt-0.5">
            <NavIcon name={iconName} className="h-5 w-5" />
          </div>
          <div>
            <div className="flex items-center gap-2 flex-wrap">
              <h1 className="text-xl font-bold text-slate-900">{title}</h1>
              <Badge variant="primary">{portalLabel}</Badge>
              {policyLabel && <Badge variant="default">{policyLabel}</Badge>}
            </div>
            <p className="text-sm text-slate-600 mt-1">{description}</p>
          </div>
        </div>
      </div>

      {isDashboardSkeleton ? (
        <div
          data-testid="dashboard-skeleton-layout"
          className="space-y-6"
          aria-label="Khung bố cục bảng điều khiển"
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            {[1, 2, 3, 4].map((slot) => (
              <Card key={slot}>
                <div className="space-y-3">
                  <div className="h-3.5 w-28 bg-slate-200 rounded animate-pulse" />
                  <div className="h-7 w-20 bg-slate-100 rounded animate-pulse" />
                  <div className="h-3 w-36 bg-slate-100 rounded animate-pulse" />
                </div>
              </Card>
            ))}
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <Card
              title="Khu vực tổng hợp nghiệp vụ chính"
              subtitle="Khung hiển thị dữ liệu sẽ được kết nối API trong Phase tiếp theo."
              className="lg:col-span-2"
            >
              <div className="space-y-3 py-2">
                <div className="h-4 w-full bg-slate-100 rounded animate-pulse" />
                <div className="h-4 w-5/6 bg-slate-100 rounded animate-pulse" />
                <div className="h-4 w-4/6 bg-slate-100 rounded animate-pulse" />
              </div>
            </Card>

            <Card
              title="Hoạt động & Thông báo gần đây"
              subtitle="Khung hiển thị danh sách rút gọn."
            >
              <div className="space-y-3 py-2">
                <div className="h-4 w-full bg-slate-100 rounded animate-pulse" />
                <div className="h-4 w-3/4 bg-slate-100 rounded animate-pulse" />
                <div className="h-4 w-2/3 bg-slate-100 rounded animate-pulse" />
              </div>
            </Card>
          </div>
        </div>
      ) : (
        <Card
          title={`Phân hệ: ${title}`}
          subtitle="Khung giao diện nền tảng (Phase 1 Foundation & Layout)"
        >
          <p className="text-sm text-slate-600 leading-relaxed">{description}</p>
        </Card>
      )}
    </div>
  );
}
