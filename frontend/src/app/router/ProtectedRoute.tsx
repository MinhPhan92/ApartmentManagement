import React from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../providers/AuthProvider';
import {
  canAccessPortal,
  hasRole,
  satisfiesPolicy,
} from '../../lib/auth/permissions';
import {
  AccessDeniedState,
  ErrorState,
  LoadingState,
} from '../../components/ui/States';
import type { AppPolicy, AppRole, PortalType } from '../../types/auth';

export interface ProtectedRouteProps {
  portal?: PortalType;
  policy?: AppPolicy;
  allowedRoles?: AppRole[];
  children: React.ReactNode;
}

export function ProtectedRoute({
  portal,
  policy,
  allowedRoles,
  children,
}: ProtectedRouteProps) {
  const { status, user, errorMessage, refreshSession } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  if (status === 'initializing') {
    return <LoadingState fullScreen label="Đang xác thực phiên đăng nhập..." />;
  }

  if (status === 'network_error') {
    return (
      <div className="min-h-screen bg-[#F8FAFC] flex items-center justify-center p-4">
        <div className="w-full max-w-md">
          <ErrorState
            title="Lỗi kết nối máy chủ"
            description={
              errorMessage ??
              'Không thể kiểm tra phiên đăng nhập do sự cố kết nối. Vui lòng thử lại.'
            }
            onRetry={() => void refreshSession()}
          />
        </div>
      </div>
    );
  }

  if (status !== 'authenticated' || !user) {
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: `${location.pathname}${location.search}` }}
      />
    );
  }

  if (portal && !canAccessPortal(user, portal)) {
    return (
      <AccessDeniedState
        title="Không được phép truy cập cổng này"
        description={
          portal === 'management'
            ? 'Khu vực Ban Quản Lý chỉ dành cho nhân sự vận hành, kế toán, kỹ thuật viên hoặc quản trị viên.'
            : 'Khu vực Cổng Cư Dân chỉ dành cho tài khoản cư dân.'
        }
        backLabel="Về trang tổng quan của tôi"
        onBack={() => navigate(user.defaultRoute, { replace: true })}
      />
    );
  }

  if (policy && !satisfiesPolicy(user, policy)) {
    return (
      <AccessDeniedState
        title="Không đủ quyền thực hiện chức năng"
        description={`Tài khoản của bạn không thỏa mãn chính sách phân quyền (${policy}) cho phân hệ này.`}
        backLabel="Về trang tổng quan"
        onBack={() => navigate(user.defaultRoute, { replace: true })}
      />
    );
  }

  if (
    allowedRoles &&
    allowedRoles.length > 0 &&
    !allowedRoles.some((role) => hasRole(user, role))
  ) {
    return (
      <AccessDeniedState
        title="Vai trò không phù hợp"
        description="Chức năng này không được cấp cho vai trò hiện tại của bạn."
        backLabel="Về trang tổng quan"
        onBack={() => navigate(user.defaultRoute, { replace: true })}
      />
    );
  }

  return <>{children}</>;
}

