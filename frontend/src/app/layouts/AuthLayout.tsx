import React from 'react';
import { Outlet } from 'react-router-dom';
import { Building2, ShieldCheck, CheckCircle2, Headphones, BarChart2 } from 'lucide-react';

export function AuthLayout({ children }: { children?: React.ReactNode }) {
  return (
    <div
      data-testid="auth-layout"
      className="min-h-screen bg-gradient-to-br from-blue-50/80 via-slate-50 to-white flex flex-col justify-between"
    >
      <header className="w-full border-b border-slate-200/80 bg-white/80 backdrop-blur-md sticky top-0 z-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-[#172554] text-white flex items-center justify-center shadow-sm">
              <Building2 className="h-5 w-5 text-blue-400" aria-hidden="true" />
            </div>
            <div>
              <span className="text-base font-extrabold text-slate-900 tracking-tight block leading-tight">
                ApartmentManagement
              </span>
              <span className="text-[11px] text-slate-500 block">
                Nền tảng Vận hành Căn hộ & Cư dân Doanh nghiệp
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2 text-xs font-semibold text-slate-600 bg-slate-100/80 px-3 py-1.5 rounded-lg border border-slate-200/60">
            <ShieldCheck className="h-4 w-4 text-[#2563EB]" aria-hidden="true" />
            <span className="hidden sm:inline">Bảo mật phiên đa tầng</span>
            <span className="text-[10px] bg-blue-600 text-white px-1.5 py-0.5 rounded font-mono">v2.0</span>
          </div>
        </div>
      </header>

      <main className="flex-1 flex items-center justify-center px-4 py-8 sm:py-12">
        <div className="w-full max-w-6xl grid grid-cols-1 lg:grid-cols-12 gap-8 lg:gap-12 items-center">
          
          {/* Cột trái: Visual Branding & Giới thiệu tính năng (7/12) */}
          <div className="hidden lg:flex lg:col-span-7 flex-col space-y-6 pr-4">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100/70 border border-blue-200 text-blue-700 text-xs font-bold w-fit">
              <Building2 size={14} />
              <span>Enterprise Back Office & Resident Hub</span>
            </div>

            <div className="space-y-3">
              <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 leading-tight">
                Hệ thống Quản trị & Cổng Cư dân Toàn diện <br />
                <span className="text-[#2563EB]">ApartmentManagement</span>
              </h1>
              <p className="text-sm text-slate-600 leading-relaxed max-w-lg">
                Giải pháp số hóa toàn trình cho Ban Quản trị và Cư dân: quản lý hợp đồng, tự động hóa hóa đơn dịch vụ, thanh toán VietQR và phản ánh bảo trì tức thì.
              </p>
            </div>

            {/* Quick 3 Feature Highlights */}
            <div className="grid grid-cols-3 gap-3 max-w-lg pt-2">
              {[
                { title: 'Thanh toán VietQR', desc: 'Gạch nợ tức thời qua mã QR', icon: BarChart2 },
                { title: 'Bảo trì & Sự cố', desc: 'Phân công kỹ thuật viên 24/7', icon: Headphones },
                { title: 'Tiện ích & Thẻ xe', desc: 'Đặt sân & quản lý xe thông minh', icon: CheckCircle2 },
              ].map((item, idx) => {
                const IconComponent = item.icon;
                return (
                  <div key={idx} className="bg-white/90 backdrop-blur-sm border border-slate-200/80 rounded-2xl p-3.5 shadow-xs text-left space-y-1">
                    <div className="w-7 h-7 bg-blue-50 text-blue-600 rounded-lg flex items-center justify-center mb-1.5">
                      <IconComponent size={16} />
                    </div>
                    <p className="text-xs font-bold text-slate-800">{item.title}</p>
                    <p className="text-[10px] text-slate-500 leading-tight">{item.desc}</p>
                  </div>
                );
              })}
            </div>

            <div className="flex items-center gap-3 pt-2 text-xs text-slate-500">
              <span className="flex items-center gap-1 font-medium text-slate-700">
                <CheckCircle2 size={14} className="text-emerald-500" /> Phân quyền RBAC 5 Roles
              </span>
              <span>•</span>
              <span className="flex items-center gap-1 font-medium text-slate-700">
                <CheckCircle2 size={14} className="text-emerald-500" /> Xác thực CSRF Token RAM
              </span>
            </div>
          </div>

          {/* Cột phải: Form Đăng nhập (5/12) */}
          <div className="col-span-1 lg:col-span-5 flex justify-center">
            <div className="w-full max-w-md">{children ?? <Outlet />}</div>
          </div>

        </div>
      </main>

      <footer className="w-full border-t border-slate-200 bg-white py-4">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 flex flex-col sm:flex-row items-center justify-between gap-2 text-xs text-slate-500">
          <span>ApartmentManagement Enterprise Portal • Phiên bản 2.0</span>
          <span>Hỗ trợ Cổng Cư Dân & Cổng Ban Quản Lý</span>
        </div>
      </footer>
    </div>
  );
}

