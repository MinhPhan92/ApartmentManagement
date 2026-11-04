import React from 'react';
import { Outlet } from 'react-router-dom';
import { Building2, ShieldCheck } from 'lucide-react';

export function AuthLayout({ children }: { children?: React.ReactNode }) {
  return (
    <div
      data-testid="auth-layout"
      className="min-h-screen bg-[#F8FAFC] flex flex-col justify-between"
    >
      <header className="w-full border-b border-slate-200 bg-white">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="h-9 w-9 rounded-lg bg-[#172554] text-white flex items-center justify-center">
              <Building2 className="h-5 w-5" aria-hidden="true" />
            </div>
            <div>
              <span className="text-base font-bold text-slate-900 tracking-tight block leading-none">
                ApartmentManagement
              </span>
              <span className="text-xs text-slate-500 block mt-0.5">
                Nền tảng Quản lý & Vận hành Chung cư
              </span>
            </div>
          </div>

          <div className="hidden sm:flex items-center gap-1.5 text-xs font-medium text-slate-600 bg-slate-100 px-3 py-1.5 rounded-md">
            <ShieldCheck className="h-4 w-4 text-[#2563EB]" aria-hidden="true" />
            <span>Bảo mật phiên đăng nhập đa tầng</span>
          </div>
        </div>
      </header>

      <main className="flex-1 flex items-center justify-center px-4 py-10">
        <div className="w-full max-w-md">{children ?? <Outlet />}</div>
      </main>

      <footer className="w-full border-t border-slate-200 bg-white py-4">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-slate-500">
          <span>ApartmentManagement Enterprise Portal</span>
          <span>Cổng Cư Dân & Cổng Ban Quản Lý</span>
        </div>
      </footer>
    </div>
  );
}

