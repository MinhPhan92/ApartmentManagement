import { Link, useLocation } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { MANAGEMENT_NAV_GROUPS, RESIDENT_NAV_ITEMS } from '../../lib/auth/permissions';
import type { PortalType } from '../../types/auth';

export interface BreadcrumbProps {
  portal: PortalType;
}

export function Breadcrumb({ portal }: BreadcrumbProps) {
  const location = useLocation();
  const rootPath = portal === 'resident' ? '/resident/dashboard' : '/management/dashboard';
  const rootLabel = portal === 'resident' ? 'Cổng Cư Dân' : 'Ban Quản Lý';

  const allItems =
    portal === 'resident'
      ? RESIDENT_NAV_ITEMS
      : MANAGEMENT_NAV_GROUPS.flatMap((group) => group.items);

  const currentItem = allItems.find(
    (item) =>
      location.pathname === item.path || location.pathname.startsWith(`${item.path}/`)
  );

  return (
    <nav aria-label="Breadcrumb" className="flex items-center gap-1.5 text-xs sm:text-sm text-slate-600">
      <Link
        to={rootPath}
        className="font-semibold text-slate-800 hover:text-[#2563EB] transition-colors"
      >
        {rootLabel}
      </Link>
      {currentItem && (
        <>
          <ChevronRight className="h-3.5 w-3.5 text-slate-400 shrink-0" aria-hidden="true" />
          <span className="font-medium text-slate-700 truncate max-w-[180px] sm:max-w-xs">
            {currentItem.label}
          </span>
        </>
      )}
    </nav>
  );
}
