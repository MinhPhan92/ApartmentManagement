import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { apiClient, resolveApiUrl } from '../lib/api/client';
import { API_ENDPOINTS } from '../lib/api/endpoints';
import { ensureCsrfToken } from '../lib/auth/authApi';
import type {
  MarkNotificationReadResponse,
  NotificationItem,
  RecentNotificationsResponse,
} from '../types/notifications';

export type RealtimeConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';

export interface UseNotificationsOptions {
  enabled: boolean;
  enableSignalR?: boolean;
  onRealtimeNotification?: (item: NotificationItem) => void;
}

export function useNotificationsSignalR({
  enabled,
  enableSignalR = true,
  onRealtimeNotification,
}: UseNotificationsOptions) {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState<number>(0);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [connectionState, setConnectionState] =
    useState<RealtimeConnectionState>('disconnected');

  const callbackRef = useRef(onRealtimeNotification);
  callbackRef.current = onRealtimeNotification;

  const syncNotifications = useCallback(async () => {
    if (!enabled) return;
    setIsLoading(true);
    setError(null);
    try {
      const response = await apiClient.get<RecentNotificationsResponse>(
        API_ENDPOINTS.notifications.recent
      );
      setItems(Array.isArray(response?.items) ? response.items : []);
      setUnreadCount(
        typeof response?.unreadCount === 'number'
          ? Math.max(0, response.unreadCount)
          : 0
      );
    } catch {
      setError('Không thể tải danh sách thông báo.');
    } finally {
      setIsLoading(false);
    }
  }, [enabled]);

  const markAllAsRead = useCallback(async () => {
    if (!enabled) return;
    await ensureCsrfToken();
    const res = await apiClient.post<MarkNotificationReadResponse>(
      API_ENDPOINTS.notifications.markAllRead
    );
    if (res?.success) {
      setUnreadCount(0);
      setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) {
      setItems([]);
      setUnreadCount(0);
      setConnectionState('disconnected');
      return;
    }

    void syncNotifications();
  }, [enabled, syncNotifications]);

  useEffect(() => {
    if (!enabled || !enableSignalR) {
      return;
    }

    let isMounted = true;
    const hubUrl = resolveApiUrl(API_ENDPOINTS.hubs.notifications);

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        withCredentials: true,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('ReceiveNotification', (incoming: NotificationItem) => {
      if (!isMounted || !incoming) return;
      setUnreadCount((prev) => prev + 1);
      setItems((prev) => [incoming, ...prev].slice(0, 8));
      callbackRef.current?.(incoming);
    });

    connection.onreconnecting(() => {
      if (isMounted) {
        setConnectionState('reconnecting');
      }
    });

    connection.onreconnected(() => {
      if (!isMounted) return;
      setConnectionState('connected');
      // Reconnect alone does not guarantee missed messages were delivered;
      // always re-synchronize notification state via the REST endpoint.
      void syncNotifications();
    });

    connection.onclose(() => {
      if (isMounted) {
        setConnectionState('disconnected');
      }
    });

    setConnectionState('connecting');
    connection
      .start()
      .then(() => {
        if (isMounted) {
          setConnectionState('connected');
        }
      })
      .catch(() => {
        if (isMounted) {
          setConnectionState('disconnected');
        }
      });

    return () => {
      isMounted = false;
      void connection.stop();
    };
  }, [enabled, enableSignalR, syncNotifications]);

  return {
    items,
    unreadCount,
    isLoading,
    error,
    connectionState,
    syncNotifications,
    markAllAsRead,
  };
}

