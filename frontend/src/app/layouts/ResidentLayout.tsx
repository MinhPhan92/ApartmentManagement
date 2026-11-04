import React, { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { Home, Menu, X } from 'lucide-react';
import { useAuth } from '../providers/AuthProvider';
import { getVisibleResidentNavItems } from '../../lib/auth/permissions';
import { NavIcon } from '../../components/shared/NavIcon';
import { Breadcrumb } from '../../components/shared/Breadcrumb';
import { NotificationBell } from '../../components/shared/NotificationBell';
import { UserMenu } from '../../components/shared/UserMenu';

export function ResidentLayout({
  children,
  enableSignalR = true,
}: {
  children?: React.ReactNode;
  enableSignalR?: boolean;
}) {
  const { user, logout } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const navItems = getVisibleResidentNavItems(user);

  return (
    <div
      data-testid="resident-portal-layout"
      className="min-h-screen bg-[#F8FAFC] flex flex-col lg:flex-row"
    >
      {/* Mobile Backdrop */}
      {mobileMenuOpen && (
        <div
          aria-hidden="true"
          onClick={() => setMobileMenuOpen(false)}
          className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden"
        />
      )}

      {/* Resident Sidebar */}
      <aside
        aria-label="Điều hướng Cổng Cư Dân"
        className={`fixed inset-y-0 left-0 z-40 w-64 bg-white border-r border-slate-200 flex flex-col transition-transform duration-200 lg:static lg:translate-x-0 ${
          mobileMenuOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="h-16 px-5 border-b border-slate-200 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="h-9 w-9 rounded-lg bg-[#2563EB] text-white flex items-center justify-center shrink-0">
              <Home className="h-5 w-5" aria-hidden="true" />
            </div>
            <div>
              <div className="text-sm font-bold text-slate-900 leading-tight">
                Resident Portal
              </div>
              <div className="text-[11px] font-medium text-[#2563EB]">
                Cổng Dịch vụ Cư dân
              </div>
            </div>
          </div>

          <button
            type="button"
            aria-label="Đóng menu"
            onClick={() => setMobileMenuOpen(false)}
            className="p-1.5 rounded-md text-slate-500 hover:bg-slate-100 lg:hidden"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        {user?.residentContext?.activeApartmentCode && (
          <div className="mx-4 mt-4 p-3 rounded-lg bg-blue-50/70 border border-blue-100">
            <div className="text-[11px] font-medium text-slate-500">
              Căn hộ đang cư trú
            </div>
            <div className="text-sm font-bold text-[#172554] mt-0.5">
              {user.residentContext.activeApartmentCode}
            </div>
            {user.residentContext.buildingName && (
              <div className="text-xs text-slate-600 mt-0.5 truncate">
                {user.residentContext.buildingName}
              </div>
            )}
          </div>
        )}

        <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-1">
          {navItems.map((item) => (
            <NavLink
              key={item.id}
              to={item.path}
              onClick={() => setMobileMenuOpen(false)}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-md text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-[#2563EB] text-white'
                    : 'text-slate-700 hover:bg-slate-100 hover:text-slate-900'
                }`
              }
            >
              <NavIcon name={item.iconName} className="h-4 w-4 shrink-0" />
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="p-4 border-t border-slate-200 bg-slate-50 text-xs text-slate-500">
          <div className="font-semibold text-slate-700">ApartmentManagement</div>
          <div className="mt-0.5">Hỗ trợ cư dân 24/7</div>
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
            <Breadcrumb portal="resident" />
          </div>

          <div className="flex items-center gap-2 sm:gap-3">
            <NotificationBell portal="resident" enableSignalR={enableSignalR} />
            {user && <UserMenu user={user} onLogout={logout} />}
          </div>
        </header>

        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-6xl w-full mx-auto">
          {children ?? <Outlet />}
        </main>
      </div>
    </div>
  );
}

