import React from 'react';
import { AlertTriangle, Inbox, Loader2, ShieldAlert } from 'lucide-react';
import { Button } from './Button';

export function LoadingState({
  label = 'Đang tải dữ liệu hệ thống...',
  fullScreen = false,
}: {
  label?: string;
  fullScreen?: boolean;
}) {
  return (
    <div
      role="status"
      aria-live="polite"
      className={`flex flex-col items-center justify-center text-center p-8 ${
        fullScreen ? 'min-h-screen bg-[#F8FAFC]' : 'min-h-[240px]'
      }`}
    >
      <Loader2 className="h-8 w-8 animate-spin text-[#2563EB] mb-3" aria-hidden="true" />
      <p className="text-sm font-medium text-slate-700">{label}</p>
    </div>
  );
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="bg-white border border-slate-200 rounded-lg p-8 text-center flex flex-col items-center justify-center">
      <div className="h-11 w-11 rounded-full bg-slate-100 text-slate-600 flex items-center justify-center mb-3">
        <Inbox className="h-5 w-5" aria-hidden="true" />
      </div>
      <h3 className="text-base font-semibold text-slate-900">{title}</h3>
      <p className="text-sm text-slate-600 max-w-md mt-1">{description}</p>
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

export function ErrorState({
  title = 'Không thể tải dữ liệu',
  description,
  onRetry,
}: {
  title?: string;
  description: string;
  onRetry?: () => void;
}) {
  return (
    <div
      role="alert"
      className="bg-white border border-red-200 rounded-lg p-6 text-center flex flex-col items-center justify-center"
    >
      <div className="h-11 w-11 rounded-full bg-red-50 text-[#DC2626] flex items-center justify-center mb-3">
        <AlertTriangle className="h-5 w-5" aria-hidden="true" />
      </div>
      <h3 className="text-base font-semibold text-slate-900">{title}</h3>
      <p className="text-sm text-slate-600 max-w-md mt-1">{description}</p>
      {onRetry && (
        <div className="mt-4">
          <Button variant="outline" size="sm" onClick={onRetry}>
            Thử lại
          </Button>
        </div>
      )}
    </div>
  );
}

export function AccessDeniedState({
  title = 'Không đủ quyền truy cập',
  description = 'Tài khoản của bạn không có quyền truy cập vào khu vực hoặc chức năng này.',
  onBack,
  backLabel = 'Quay về trang tổng quan',
}: {
  title?: string;
  description?: string;
  onBack?: () => void;
  backLabel?: string;
}) {
  return (
    <div
      role="alert"
      className="bg-white border border-slate-200 rounded-lg p-8 text-center max-w-lg mx-auto my-8 shadow-sm"
    >
      <div className="h-12 w-12 rounded-full bg-red-50 text-[#DC2626] flex items-center justify-center mx-auto mb-4">
        <ShieldAlert className="h-6 w-6" aria-hidden="true" />
      </div>
      <h2 className="text-lg font-bold text-slate-900">{title}</h2>
      <p className="text-sm text-slate-600 mt-1.5">{description}</p>
      {onBack && (
        <div className="mt-5">
          <Button variant="primary" onClick={onBack}>
            {backLabel}
          </Button>
        </div>
      )}
    </div>
  );
}

