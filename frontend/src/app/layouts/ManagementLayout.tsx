import React, { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { Building2, Menu, X } from 'lucide-react';
import { useAuth } from '../providers/AuthProvider';
import {
  getRoleDisplayLabel,
  getVisibleManagementNavGroups,
} from '../../lib/auth/permissions';
import { NavIcon } from '../../components/shared/NavIcon';
import { Breadcrumb } from '../../components/shared/Breadcrumb';
import { NotificationBell } from '../../components/shared/NotificationBell';
import { UserMenu } from '../../components/shared/UserMenu';
import { Badge } from '../../components/ui/Badge';

export function ManagementLayout({
  children,
  enableSignalR = true,
}: {
  children?: React.ReactNode;
  enableSignalR?: boolean;
}) {
  const { user, logout } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const navGroups = getVisibleManagementNavGroups(user);
  const roleLabel = getRoleDisplayLabel(user);

  return (
    <div
      data-testid="management-portal-layout"
      className="min-h-screen bg-[#F8FAFC] flex flex-col lg:flex-row"
    >
      {/* Mobile Backdrop */}
      {mobileMenuOpen && (
        <div
          aria-hidden="true"
          onClick={() => setMobileMenuOpen(false)}
          className="fixed inset-0 z-30 bg-slate-900/50 lg:hidden"
        />
      )}

      {/* Management Deep Navy Sidebar */}
      <aside
        aria-label="Điều hướng Ban Quản Lý"
        className={`fixed inset-y-0 left-0 z-40 w-64 bg-[#172554] text-slate-100 flex flex-col transition-transform duration-200 lg:static lg:translate-x-0 ${
          mobileMenuOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="h-16 px-5 border-b border-blue-900/70 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="h-9 w-9 rounded-lg bg-[#2563EB] text-white flex items-center justify-center shrink-0">
              <Building2 className="h-5 w-5" aria-hidden="true" />
            </div>
            <div>
              <div className="text-sm font-bold text-white leading-tight">
                Management Portal
              </div>
              <div className="text-[11px] text-blue-200">
                Điều hành & Quản trị Tòa nhà
              </div>
            </div>
          </div>

          <button
            type="button"
            aria-label="Đóng menu"
            onClick={() => setMobileMenuOpen(false)}
            className="p-1.5 rounded-md text-blue-200 hover:bg-blue-900/60 lg:hidden"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-3 py-4 space-y-5">
          {navGroups.map((group) => (
            <div key={group.id}>
              <div className="px-3 mb-1.5 text-[11px] font-semibold uppercase tracking-wider text-blue-300/80">
                {group.title}
              </div>
              <div className="space-y-1">
                {group.items.map((item) => (
                  <NavLink
                    key={item.id}
                    to={item.path}
                    onClick={() => setMobileMenuOpen(false)}
                    className={({ isActive }) =>
                      `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                        isActive
                          ? 'bg-[#2563EB] text-white'
                          : 'text-blue-100 hover:bg-blue-900/60 hover:text-white'
                      }`
                    }
                  >
                    <NavIcon name={item.iconName} className="h-4 w-4 shrink-0" />
                    <span>{item.label}</span>
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </div>

        <div className="p-4 border-t border-blue-900/70 bg-slate-950/30 text-xs text-blue-200 flex items-center justify-between">
          <span className="truncate">{roleLabel}</span>
          <span className="px-2 py-0.5 rounded bg-blue-900/70 text-[11px] font-semibold text-white">
            v2.6
          </span>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0">
        <header className="h-16 bg-white border-b border-slate-200 px-4 sm:px-6 flex items-center justify-between gap-4 sticky top-0 z-20">
          <div className="flex items-center gap-3 min-w-0">
            <button
              type="button"
              aria-label="Mở menu điều hướng"
              onClick={() => setMobileMenuOpen(true)}
              className="p-2 rounded-md text-slate-600 hover:bg-slate-100 lg:hidden"
            >
              <Menu className="h-5 w-5" aria-hidden="true" />
            </button>
            <Breadcrumb portal="management" />
          </div>

          <div className="flex items-center gap-2 sm:gap-3">
            <div className="hidden sm:block">
              <Badge variant="navy">{roleLabel}</Badge>
            </div>
            <NotificationBell portal="management" enableSignalR={enableSignalR} />
            {user && <UserMenu user={user} onLogout={logout} />}
          </div>
        </header>

        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto">
          {children ?? <Outlet />}
        </main>
      </div>
    </div>
  );
}

