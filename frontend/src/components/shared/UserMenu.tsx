import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ChevronDown, LogOut, UserCog } from 'lucide-react';
import { Avatar } from '../ui/Avatar';
import { Badge } from '../ui/Badge';
import { Dropdown } from '../ui/Dropdown';
import { getRoleDisplayLabel } from '../../lib/auth/permissions';
import type { CurrentUser } from '../../types/auth';

export interface UserMenuProps {
  user: CurrentUser;
  onLogout: () => Promise<void>;
}

export function UserMenu({ user, onLogout }: UserMenuProps) {
  const navigate = useNavigate();
  const [isLoggingOut, setIsLoggingOut] = useState(false);

  const roleLabel = getRoleDisplayLabel(user);
  const accountPath =
    user.portal === 'resident' ? '/resident/account' : '/management/dashboard';

  async function handleLogout(close: () => void) {
    if (isLoggingOut) return;
    setIsLoggingOut(true);
    try {
      await onLogout();
      close();
      navigate('/login', { replace: true });
    } finally {
      setIsLoggingOut(false);
    }
  }

  return (
    <Dropdown
      triggerLabel="Tài khoản người dùng"
      widthClass="w-64"
      trigger={
        <div className="flex items-center gap-2.5 px-2 py-1.5 rounded-md hover:bg-slate-100 transition-colors">
          <Avatar
            name={user.displayName}
            size="sm"
            tone={user.portal === 'resident' ? 'blue' : 'navy'}
          />
          <div className="hidden md:block text-left">
            <div className="text-xs font-semibold text-slate-900 leading-tight max-w-[140px] truncate">
              {user.displayName}
            </div>
            <div className="text-[11px] text-slate-500 leading-tight mt-0.5">
              {roleLabel}
            </div>
          </div>
          <ChevronDown className="h-3.5 w-3.5 text-slate-400 hidden sm:block" aria-hidden="true" />
        </div>
      }
    >
      {({ close }) => (
        <div className="py-1">
          <div className="px-4 py-3 border-b border-slate-100">
            <p className="text-sm font-semibold text-slate-900 truncate">
              {user.displayName}
            </p>
            {user.email && (
              <p className="text-xs text-slate-500 truncate mt-0.5">{user.email}</p>
            )}
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              <Badge variant="primary">{roleLabel}</Badge>
              {user.residentContext?.activeApartmentCode && (
                <Badge variant="default">
                  Căn hộ {user.residentContext.activeApartmentCode}
                </Badge>
              )}
            </div>
          </div>

          {user.portal === 'resident' && (
            <div className="py-1">
              <Link
                to={accountPath}
                onClick={close}
                className="flex items-center gap-2.5 px-4 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50"
              >
                <UserCog className="h-4 w-4 text-slate-400" aria-hidden="true" />
                <span>Thông tin tài khoản</span>
              </Link>
            </div>
          )}

          <div className="border-t border-slate-100 pt-1 px-2 pb-1">
            <button
              type="button"
              disabled={isLoggingOut}
              onClick={() => void handleLogout(close)}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-xs font-semibold text-[#DC2626] hover:bg-red-50 rounded-md transition-colors disabled:opacity-50"
            >
              <LogOut className="h-4 w-4" aria-hidden="true" />
              <span>{isLoggingOut ? 'Đang đăng xuất...' : 'Đăng xuất'}</span>
            </button>
          </div>
        </div>
      )}
    </Dropdown>
  );
}
