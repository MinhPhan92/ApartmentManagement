import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from '../providers/AuthProvider';
import { AuthLayout } from '../layouts/AuthLayout';
import { ResidentLayout } from '../layouts/ResidentLayout';
import { ManagementLayout } from '../layouts/ManagementLayout';
import { ProtectedRoute } from './ProtectedRoute';
import { LoginPage } from '../../features/auth/LoginPage';
import { AccessDeniedPage } from '../../features/auth/AccessDeniedPage';
import { ResidentRoutePage } from '../../features/resident/ResidentPages';
import { ManagementRoutePage } from '../../features/management/ManagementPages';
import { LoadingState, ErrorState } from '../../components/ui/States';

function RootIndexRedirect() {
  const { status, user, errorMessage, refreshSession } = useAuth();

  if (status === 'initializing') {
    return <LoadingState fullScreen label="Đang khởi tạo ứng dụng..." />;
  }

  if (status === 'network_error') {
    return (
      <div className="min-h-screen bg-[#F8FAFC] flex items-center justify-center p-4">
        <div className="w-full max-w-md">
          <ErrorState
            title="Lỗi kết nối máy chủ"
            description={errorMessage ?? 'Không thể kết nối đến hệ thống.'}
            onRetry={() => void refreshSession()}
          />
        </div>
      </div>
    );
  }

  if (status === 'authenticated' && user) {
    return <Navigate to={user.defaultRoute} replace />;
  }

  return <Navigate to="/login" replace />;
}

export function AppRouter({ enableSignalR = true }: { enableSignalR?: boolean }) {
  return (
    <Routes>
      <Route path="/" element={<RootIndexRedirect />} />

      {/* Auth Layout Routes */}
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/access-denied" element={<AccessDeniedPage />} />
      </Route>

      {/* Resident Portal Routes (/resident/*) */}
      <Route
        path="/resident"
        element={
          <ProtectedRoute portal="resident" policy="RequireResident">
            <ResidentLayout enableSignalR={enableSignalR} />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/resident/dashboard" replace />} />
        <Route
          path="dashboard"
          element={<ResidentRoutePage itemId="resident-dashboard" />}
        />
        <Route
          path="apartment"
          element={<ResidentRoutePage itemId="resident-apartment" />}
        />
        <Route
          path="invoices"
          element={<ResidentRoutePage itemId="resident-invoices" />}
        />
        <Route
          path="repairs"
          element={<ResidentRoutePage itemId="resident-repairs" />}
        />
        <Route
          path="complaints"
          element={<ResidentRoutePage itemId="resident-complaints" />}
        />
        <Route
          path="vehicles"
          element={<ResidentRoutePage itemId="resident-vehicles" />}
        />
        <Route
          path="visitors"
          element={<ResidentRoutePage itemId="resident-visitors" />}
        />
        <Route
          path="amenities"
          element={<ResidentRoutePage itemId="resident-amenities" />}
        />
        <Route
          path="notifications"
          element={<ResidentRoutePage itemId="resident-notifications" />}
        />
        <Route
          path="account"
          element={<ResidentRoutePage itemId="resident-account" />}
        />
      </Route>

      {/* Management Portal Routes (/management/*) */}
      <Route
        path="/management"
        element={
          <ProtectedRoute portal="management">
            <ManagementLayout enableSignalR={enableSignalR} />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/management/dashboard" replace />} />
        <Route
          path="dashboard"
          element={
            <ProtectedRoute
              portal="management"
              allowedRoles={[
                'SuperAdmin',
                'BuildingManager',
                'Accountant',
                'Technician',
              ]}
            >
              <ManagementRoutePage itemId="mgmt-dashboard" />
            </ProtectedRoute>
          }
        />
        <Route
          path="buildings"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-buildings-apartments" />
            </ProtectedRoute>
          }
        />
        <Route
          path="residents"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-residents" />
            </ProtectedRoute>
          }
        />
        <Route
          path="contracts"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-contracts" />
            </ProtectedRoute>
          }
        />
        <Route
          path="billing"
          element={
            <ProtectedRoute
              portal="management"
              policy="RequireAccountantOrManager"
            >
              <ManagementRoutePage itemId="mgmt-billing" />
            </ProtectedRoute>
          }
        />
        <Route
          path="payments"
          element={
            <ProtectedRoute
              portal="management"
              policy="RequireAccountantOrManager"
            >
              <ManagementRoutePage itemId="mgmt-payments" />
            </ProtectedRoute>
          }
        />
        <Route
          path="reports"
          element={
            <ProtectedRoute
              portal="management"
              policy="RequireAccountantOrManager"
            >
              <ManagementRoutePage itemId="mgmt-reports" />
            </ProtectedRoute>
          }
        />
        <Route
          path="repairs"
          element={
            <ProtectedRoute
              portal="management"
              policy="RequireTechnicianOrManagement"
            >
              <ManagementRoutePage itemId="mgmt-repairs" />
            </ProtectedRoute>
          }
        />
        <Route
          path="technical-incidents"
          element={
            <ProtectedRoute
              portal="management"
              policy="RequireTechnicianOrManagement"
            >
              <ManagementRoutePage itemId="mgmt-technical-incidents" />
            </ProtectedRoute>
          }
        />
        <Route
          path="vehicles"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-vehicles" />
            </ProtectedRoute>
          }
        />
        <Route
          path="visitors"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-visitors" />
            </ProtectedRoute>
          }
        />
        <Route
          path="complaints"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-complaints" />
            </ProtectedRoute>
          }
        />
        <Route
          path="amenities"
          element={
            <ProtectedRoute portal="management" policy="RequireManagement">
              <ManagementRoutePage itemId="mgmt-amenities" />
            </ProtectedRoute>
          }
        />
        <Route
          path="notifications"
          element={
            <ProtectedRoute
              portal="management"
              allowedRoles={[
                'SuperAdmin',
                'BuildingManager',
                'Accountant',
                'Technician',
              ]}
            >
              <ManagementRoutePage itemId="mgmt-notifications" />
            </ProtectedRoute>
          }
        />
        <Route
          path="accounts"
          element={
            <ProtectedRoute portal="management" policy="RequireSuperAdmin">
              <ManagementRoutePage itemId="mgmt-accounts" />
            </ProtectedRoute>
          }
        />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
