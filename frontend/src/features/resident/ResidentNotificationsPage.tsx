import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Bell,
  CheckCheck,
  Megaphone,
  Pin,
  Trash2,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';
import { useToast } from '../../components/ui/Toast';

export function ResidentNotificationsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const initialTab =
    searchParams.get('tab') === 'announcements' ? 'announcements' : 'notifications';
  const [activeTab, setActiveTab] = useState<'notifications' | 'announcements'>(
    initialTab
  );
  const [unreadOnly, setUnreadOnly] = useState<boolean>(false);

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'notifications', unreadOnly],
    queryFn: () => residentApi.getNotifications(1, unreadOnly ? true : undefined),
  });

  const markReadMutation = useMutation({
    mutationFn: (id: number) => residentApi.markNotificationRead(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['resident', 'notifications'] });
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'recent'] });
    },
  });

  const markAllReadMutation = useMutation({
    mutationFn: () => residentApi.markAllNotificationsRead(),
    onSuccess: () => {
      pushToast({
        type: 'success',
        title: 'Đã đánh dấu đã đọc',
        message: 'Tất cả thông báo cá nhân đã được đánh dấu là đã đọc.',
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'notifications'] });
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'recent'] });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => residentApi.deleteNotification(id),
    onSuccess: (res) => {
      pushToast({
        type: 'info',
        title: 'Đã xóa thông báo',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'notifications'] });
      void queryClient.invalidateQueries({ queryKey: ['notifications', 'recent'] });
    },
  });

  if (isLoading) {
    return <LoadingState label="Đang tải thông báo và bảng tin Ban Quản lý..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải thông báo"
        description={error instanceof Error ? error.message : 'Vui lòng thử lại.'}
        onRetry={() => void refetch()}
      />
    );
  }

  const handleSwitchTab = (tab: 'notifications' | 'announcements') => {
    setActiveTab(tab);
    setSearchParams({ tab }, { replace: true });
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-[#0F172A]">
            Thông báo & Bản tin Ban Quản lý
          </h1>
          <p className="text-sm text-[#475569] mt-1">
            Nhận thông báo thời gian thực về hóa đơn, khách thăm, sửa chữa, thẻ xe và các thông báo vận hành từ Ban Quản lý.
          </p>
        </div>

        <div className="flex items-center gap-1 rounded-xl bg-[#F1F5F9] p-1 text-xs font-semibold">
          <button
            type="button"
            onClick={() => handleSwitchTab('notifications')}
            className={`inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg transition-all ${
              activeTab === 'notifications'
                ? 'bg-white text-[#0F172A] shadow-2xs'
                : 'text-[#475569]'
            }`}
          >
            <Bell className="w-4 h-4 text-[#2563EB]" />
            <span>Thông báo của tôi ({data.unreadCount} chưa đọc)</span>
          </button>
          <button
            type="button"
            onClick={() => handleSwitchTab('announcements')}
            className={`inline-flex items-center gap-1.5 px-3.5 py-2 rounded-lg transition-all ${
              activeTab === 'announcements'
                ? 'bg-white text-[#0F172A] shadow-2xs'
                : 'text-[#475569]'
            }`}
          >
            <Megaphone className="w-4 h-4 text-[#2563EB]" />
            <span>Bản tin chung cư ({data.announcements.length})</span>
          </button>
        </div>
      </div>

      {activeTab === 'notifications' ? (
        <Card className="p-6 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#E2E8F0] pb-3.5">
            <div className="flex items-center gap-2">
              <label className="inline-flex items-center gap-2 text-xs font-medium text-[#475569] cursor-pointer">
                <input
                  type="checkbox"
                  checked={unreadOnly}
                  onChange={(e) => setUnreadOnly(e.target.checked)}
                  className="rounded border-slate-300 text-[#2563EB] focus:ring-[#2563EB]"
                />
                <span>Chỉ hiển thị thông báo chưa đọc</span>
              </label>
            </div>

            {data.unreadCount > 0 && (
              <Button
                variant="secondary"
                size="sm"
                isLoading={markAllReadMutation.isPending}
                onClick={() => markAllReadMutation.mutate()}
              >
                <CheckCheck className="w-4 h-4 mr-1.5" />
                Đánh dấu tất cả đã đọc
              </Button>
            )}
          </div>

          {data.notifications.length === 0 ? (
            <EmptyState
              title="Không có thông báo nào"
              description="Khi có cập nhật mới về hóa đơn, khách đến thăm hoặc yêu cầu dịch vụ, thông báo sẽ xuất hiện tại đây."
            />
          ) : (
            <div className="divide-y divide-[#E2E8F0]">
              {data.notifications.map((n) => (
                <div
                  key={n.id}
                  className={`py-4 px-3 rounded-xl transition-colors flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 ${
                    !n.isRead ? 'bg-blue-50/40' : ''
                  }`}
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      {!n.isRead && (
                        <span className="w-2 h-2 rounded-full bg-[#2563EB] shrink-0" />
                      )}
                      <Badge variant="info">{n.type}</Badge>
                      <span className="text-sm font-bold text-[#0F172A]">
                        {n.title}
                      </span>
                      <span className="text-xs text-[#64748B]">{n.displayDate}</span>
                    </div>
                    <p className="text-xs text-[#334155] whitespace-pre-line">
                      {n.content}
                    </p>
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    {!n.isRead && (
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => markReadMutation.mutate(n.id)}
                      >
                        Đã đọc
                      </Button>
                    )}
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => deleteMutation.mutate(n.id)}
                      aria-label="Xóa thông báo"
                    >
                      <Trash2 className="w-4 h-4 text-[#64748B] hover:text-[#B91C1C]" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      ) : (
        <Card className="p-6 space-y-4">
          {data.announcements.length === 0 ? (
            <EmptyState
              title="Chưa có bản tin chung cư"
              description="Các thông báo vận hành, lịch bảo trì thang máy, điện nước từ Ban Quản lý sẽ hiển thị tại đây."
            />
          ) : (
            <div className="space-y-4">
              {data.announcements.map((ann) => (
                <div
                  key={ann.announcementId}
                  className={`rounded-xl border p-5 space-y-2 ${
                    ann.isPinned
                      ? 'border-amber-300 bg-amber-50/30'
                      : 'border-[#E2E8F0] bg-white'
                  }`}
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                      {ann.isPinned && (
                        <Badge variant="warning">
                          <Pin className="w-3 h-3 mr-1" />
                          Tin ghim quan trọng
                        </Badge>
                      )}
                      <Badge variant="info">{ann.category}</Badge>
                      <span className="text-xs font-medium text-[#64748B]">
                        Phạm vi: {ann.buildingName}
                      </span>
                    </div>
                    <span className="text-xs text-[#64748B]">{ann.displayDate}</span>
                  </div>
                  <h3 className="text-base font-bold text-[#0F172A]">{ann.title}</h3>
                  <p className="text-sm text-[#334155] whitespace-pre-line leading-relaxed">
                    {ann.content}
                  </p>
                </div>
              ))}
            </div>
          )}
        </Card>
      )}
    </div>
  );
}

