import React, { createContext, useCallback, useContext, useState } from 'react';
import { AlertCircle, CheckCircle2, Info, X } from 'lucide-react';

export type ToastTone = 'info' | 'success' | 'warning' | 'danger' | 'error';

export interface ToastMessage {
  id: string;
  title: string;
  description?: string;
  message?: string;
  tone?: ToastTone;
  type?: ToastTone;
}

interface ToastContextValue {
  toasts: ToastMessage[];
  pushToast: (toast: Omit<ToastMessage, 'id'>) => void;
  dismissToast: (id: string) => void;
}

const ToastContext = createContext<ToastContextValue | undefined>(undefined);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const dismissToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((item) => item.id !== id));
  }, []);

  const pushToast = useCallback(
    (toast: Omit<ToastMessage, 'id'>) => {
      const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      setToasts((prev) => [...prev.slice(-4), { ...toast, id }]);
      setTimeout(() => {
        dismissToast(id);
      }, 5000);
    },
    [dismissToast]
  );

  return (
    <ToastContext.Provider value={{ toasts, pushToast, dismissToast }}>
      {children}
      <div
        aria-live="polite"
        className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 w-full max-w-sm pointer-events-none px-4 sm:px-0"
      >
        {toasts.map((t) => {
          const rawTone = t.tone ?? t.type ?? 'info';
          const tone = rawTone === 'error' ? 'danger' : rawTone;
          const Icon =
            tone === 'success'
              ? CheckCircle2
              : tone === 'danger' || tone === 'warning'
              ? AlertCircle
              : Info;
          const iconColor =
            tone === 'success'
              ? 'text-[#16A34A]'
              : tone === 'warning'
              ? 'text-[#D97706]'
              : tone === 'danger'
              ? 'text-[#DC2626]'
              : 'text-[#2563EB]';

          return (
            <div
              key={t.id}
              role="status"
              className="pointer-events-auto bg-white border border-slate-200 rounded-lg shadow-md p-3.5 flex items-start gap-3"
            >
              <Icon className={`h-5 w-5 shrink-0 mt-0.5 ${iconColor}`} aria-hidden="true" />
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-slate-900">{t.title}</p>
                {(t.description || t.message) && (
                  <p className="text-xs text-slate-600 mt-0.5 break-words">
                    {t.description ?? t.message}
                  </p>
                )}
              </div>
              <button
                type="button"
                aria-label="Đóng thông báo"
                onClick={() => dismissToast(t.id)}
                className="text-slate-400 hover:text-slate-600 p-1 rounded"
              >
                <X className="h-4 w-4" aria-hidden="true" />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return ctx;
}

