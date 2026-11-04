import React from 'react';
import {
  AlertTriangle,
  BarChart3,
  Bell,
  Building2,
  CalendarCheck,
  Car,
  CreditCard,
  FileText,
  Home,
  LayoutDashboard,
  MessageSquareWarning,
  Receipt,
  ShieldCheck,
  UserCheck,
  UserCog,
  Users,
  Wrench,
} from 'lucide-react';
import type { PortalNavItem } from '../../lib/auth/permissions';

const ICON_MAP: Record<PortalNavItem['iconName'], React.ComponentType<{ className?: string }>> = {
  LayoutDashboard,
  Building2,
  Home,
  Users,
  FileText,
  Receipt,
  CreditCard,
  Wrench,
  MessageSquareWarning,
  AlertTriangle,
  Car,
  UserCheck,
  CalendarCheck,
  Bell,
  BarChart3,
  ShieldCheck,
  UserCog,
};

export function NavIcon({
  name,
  className = 'h-4 w-4 shrink-0',
}: {
  name: PortalNavItem['iconName'];
  className?: string;
}) {
  const IconComponent = ICON_MAP[name] ?? LayoutDashboard;
  return <IconComponent className={className} aria-hidden="true" />;
}

