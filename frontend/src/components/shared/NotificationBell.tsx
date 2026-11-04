import { Link } from 'react-router-dom';
import { Bell, CheckCheck, RefreshCw } from 'lucide-react';
import { Dropdown } from '../ui/Dropdown';
import { Badge } from '../ui/Badge';
import { useNotificationsSignalR } from '../../hooks/useNotificationsSignalR';
import { useToast } from '../ui/Toast';
import type { PortalType } from '../../types/auth';

export interface NotificationBellProps {
  portal: PortalType;
  enableSignalR?: boolean;
}

export function NotificationBell({ portal, enableSignalR = true }: NotificationBellProps) {
  const { pushToast } = useToast();
  const notificationsPath =
    portal === 'resident' ? '/resident/notifications' : '/management/notifications';

  const {
    items,
    unreadCount,
    isLoading,
    error,
    connectionState,
    syncNotifications,
    markAllAsRead,
  } = useNotificationsSignalR({
    enabled: true,
    enableSignalR,
    onRealtimeNotification: (item) => {
      pushToast({
        title: item.title,
        description: item.content,
        tone: 'info',
      });
    },
  });

  return (
    <Dropdown
      triggerLabel="Thông báo hệ thống"
      widthClass="w-80 sm:w-96"
      trigger={
        <div className="relative p-2 rounded-md text-slate-600 hover:text-slate-900 hover:bg-slate-100 transition-colors">
          <Bell className="h-5 w-5" aria-hidden="true" />
          {unreadCount > 0 && (
            <span
              data-testid="notification-unread-badge"
              className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-[#DC2626] text-white text-[10px] font-bold flex items-center justify-center"
            >
              {unreadCount > 99 ? '99+' : unreadCount}
            </span>
          )}
        </div>
      }
    >
      {({ close }) => (
        <div>
          <div className="px-4 py-3 border-b border-slate-200 flex items-center justify-between bg-slate-50 rounded-t-lg">
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-slate-900">Thông báo</span>
              <Badge variant={unreadCount > 0 ? 'primary' : 'default'}>
                {unreadCount > 0 ? `${unreadCount} chưa đọc` : '0 mới'}
              </Badge>
            </div>
            <div className="flex items-center gap-1">
              <button
                type="button"
                title="Làm mới thông báo"
                aria-label="Làm mới thông báo"
                onClick={() => void syncNotifications()}
                className="p-1.5 text-slate-500 hover:text-slate-800 rounded"
              >
                <RefreshCw
                  className={`h-3.5 w-3.5 ${isLoading ? 'animate-spin' : ''}`}
                  aria-hidden="true"
                />
              </button>
              {unreadCount > 0 && (
                <button
                  type="button"
                  title="Đánh dấu tất cả đã đọc"
                  aria-label="Đánh dấu tất cả đã đọc"
                  onClick={() => void markAllAsRead()}
                  className="p-1.5 text-slate-500 hover:text-[#2563EB] rounded"
                >
                  <CheckCheck className="h-4 w-4" aria-hidden="true" />
                </button>
              )}
            </div>
          </div>

          <div className="max-h-80 overflow-y-auto divide-y divide-slate-100">
            {isLoading && items.length === 0 ? (
              <div className="p-6 text-center text-xs text-slate-500">
                Đang tải thông báo...
              </div>
            ) : error ? (
              <div className="p-4 text-center text-xs text-[#DC2626]">{error}</div>
            ) : items.length === 0 ? (
              <div className="p-6 text-center text-xs text-slate-500">
                Chưa có thông báo mới.
              </div>
            ) : (
              items.map((item) => (
                <div
                  key={item.id}
                  className={`p-3.5 text-left transition-colors ${
                    !item.isRead ? 'bg-blue-50/40' : 'bg-white'
                  }`}
                >
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <span className="text-xs font-semibold text-slate-900 line-clamp-1">
                      {item.title}
                    </span>
                    <span className="text-[11px] text-slate-400 shrink-0">
                      {item.displayDate}
                    </span>
                  </div>
                  <p className="text-xs text-slate-600 line-clamp-2">{item.content}</p>
                </div>
              ))
            )}
          </div>

          <div className="px-4 py-2.5 border-t border-slate-200 bg-slate-50 rounded-b-lg flex items-center justify-between">
            <span className="text-[11px] text-slate-500">
              Realtime:{' '}
              {connectionState === 'connected'
                ? 'Đã kết nối'
                : connectionState === 'reconnecting'
                ? 'Đang kết nối lại...'
                : 'Tiêu chuẩn'}
            </span>
            <Link
              to={notificationsPath}
              onClick={close}
              className="text-xs font-semibold text-[#2563EB] hover:underline"
            >
              Xem tất cả
            </Link>
          </div>
        </div>
      )}
    </Dropdown>
  );
}
