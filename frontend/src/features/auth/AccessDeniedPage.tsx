import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../app/providers/AuthProvider';
import { AccessDeniedState } from '../../components/ui/States';

export function AccessDeniedPage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const targetRoute = user?.defaultRoute ?? '/login';
  const label = user ? 'Quay về bảng điều khiển của bạn' : 'Quay về trang đăng nhập';

  return (
    <AccessDeniedState
      title="Truy cập bị từ chối (403)"
      description="Tài khoản hiện tại của bạn không có quyền truy cập vào phân hệ hoặc trang này."
      backLabel={label}
      onBack={() => navigate(targetRoute, { replace: true })}
    />
  );
}
