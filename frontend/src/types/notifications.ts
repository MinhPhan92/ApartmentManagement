export interface NotificationItem {
  id: number;
  title: string;
  content: string;
  type: string;
  isRead: boolean;
  targetUrl?: string | null;
  createdAtUtc: string;
  displayDate: string;
}

export interface RecentNotificationsResponse {
  unreadCount: number;
  items: NotificationItem[];
}

export interface MarkNotificationReadResponse {
  success: boolean;
  unreadCount: number;
}

