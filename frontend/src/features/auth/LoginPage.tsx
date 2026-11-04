import React, { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { AlertCircle, Building2, Eye, EyeOff, Lock, User } from 'lucide-react';
import { useAuth } from '../../app/providers/AuthProvider';
import { ApiClientError } from '../../lib/api/errors';
import {
  validateLoginForm,
  type LoginFormErrors,
} from '../../lib/validation/loginValidation';
import { Button } from '../../components/ui/Button';
import { Input } from '../../components/ui/Input';

export function LoginPage() {
  const { status, user, login, sessionExpired, clearSessionExpiredNotice } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [userNameOrEmail, setUserNameOrEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<LoginFormErrors>({});
  const [authError, setAuthError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (status === 'authenticated' && user) {
    return <Navigate to={user.defaultRoute || '/resident/dashboard'} replace />;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAuthError(null);
    if (sessionExpired) {
      clearSessionExpiredNotice();
    }

    const validation = validateLoginForm({
      userNameOrEmail,
      password,
      rememberMe,
    });

    if (validation.userNameOrEmail || validation.password) {
      setFieldErrors(validation);
      return;
    }

    setFieldErrors({});
    setIsSubmitting(true);

    try {
      const authenticatedUser = await login({
        userNameOrEmail: userNameOrEmail.trim(),
        password,
        rememberMe,
      });

      const stateFrom = (location.state as { from?: string } | null)?.from;
      const canUseStateFrom =
        stateFrom &&
        ((authenticatedUser.portal === 'resident' && stateFrom.startsWith('/resident')) ||
          (authenticatedUser.portal === 'management' &&
            stateFrom.startsWith('/management')));

      navigate(canUseStateFrom ? stateFrom : authenticatedUser.defaultRoute, {
        replace: true,
      });
    } catch (err) {
      if (err instanceof ApiClientError) {
        const userFieldErrors = err.getFieldErrors('UserNameOrEmail');
        const passwordFieldErrors = err.getFieldErrors('Password');
        if (userFieldErrors.length > 0 || passwordFieldErrors.length > 0) {
          setFieldErrors({
            userNameOrEmail: userFieldErrors[0],
            password: passwordFieldErrors[0],
          });
        }
        setAuthError(err.message);
      } else {
        setAuthError('Đăng nhập thất bại. Vui lòng kiểm tra thông tin và thử lại.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-sm p-6 sm:p-8">
      <div className="flex flex-col items-center text-center mb-6">
        <div className="h-12 w-12 rounded-xl bg-[#172554] text-white flex items-center justify-center mb-3">
          <Building2 className="h-6 w-6" aria-hidden="true" />
        </div>
        <h1 className="text-xl font-bold text-slate-900">
          Đăng nhập ApartmentManagement
        </h1>
        <p className="text-xs sm:text-sm text-slate-500 mt-1">
          Sử dụng tài khoản Cư dân hoặc Ban quản lý để tiếp tục
        </p>
      </div>

      {sessionExpired && !authError && (
        <div
          role="alert"
          data-testid="session-expired-alert"
          className="mb-5 p-3.5 rounded-lg bg-amber-50 border border-amber-200 flex items-start gap-2.5 text-xs text-[#D97706]"
        >
          <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" aria-hidden="true" />
          <span>Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục.</span>
        </div>
      )}

      {authError && (
        <div
          role="alert"
          data-testid="login-error-alert"
          className="mb-5 p-3.5 rounded-lg bg-red-50 border border-red-200 flex items-start gap-2.5 text-xs font-medium text-[#DC2626]"
        >
          <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" aria-hidden="true" />
          <span>{authError}</span>
        </div>
      )}

      <form onSubmit={(e) => void handleSubmit(e)} noValidate className="space-y-4">
        <Input
          label="Tài khoản hoặc Email"
          name="userNameOrEmail"
          type="text"
          autoComplete="username"
          placeholder="Nhập tên đăng nhập hoặc email"
          value={userNameOrEmail}
          onChange={(e) => {
            setUserNameOrEmail(e.target.value);
            if (fieldErrors.userNameOrEmail) {
              setFieldErrors((prev) => ({ ...prev, userNameOrEmail: undefined }));
            }
          }}
          error={fieldErrors.userNameOrEmail}
          disabled={isSubmitting}
          leftAdornment={<User className="h-4 w-4" aria-hidden="true" />}
        />

        <Input
          label="Mật khẩu"
          name="password"
          type={showPassword ? 'text' : 'password'}
          autoComplete="current-password"
          placeholder="Nhập mật khẩu"
          value={password}
          onChange={(e) => {
            setPassword(e.target.value);
            if (fieldErrors.password) {
              setFieldErrors((prev) => ({ ...prev, password: undefined }));
            }
          }}
          error={fieldErrors.password}
          disabled={isSubmitting}
          leftAdornment={<Lock className="h-4 w-4" aria-hidden="true" />}
          rightAdornment={
            <button
              type="button"
              aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              onClick={() => setShowPassword((prev) => !prev)}
              className="p-1 text-slate-400 hover:text-slate-600 rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-[#2563EB]"
            >
              {showPassword ? (
                <EyeOff className="h-4 w-4" aria-hidden="true" />
              ) : (
                <Eye className="h-4 w-4" aria-hidden="true" />
              )}
            </button>
          }
        />

        <div className="flex items-center justify-between pt-1">
          <label className="inline-flex items-center gap-2 text-xs sm:text-sm text-slate-700 cursor-pointer select-none">
            <input
              type="checkbox"
              name="rememberMe"
              checked={rememberMe}
              onChange={(e) => setRememberMe(e.target.checked)}
              disabled={isSubmitting}
              className="h-4 w-4 rounded border-slate-300 text-[#2563EB] focus:ring-[#2563EB]"
            />
            <span>Ghi nhớ đăng nhập</span>
          </label>
        </div>

        <Button
          type="submit"
          variant="primary"
          size="lg"
          isLoading={isSubmitting}
          className="w-full mt-2"
        >
          {isSubmitting ? 'Đang xác thực...' : 'Đăng nhập'}
        </Button>
      </form>
    </div>
  );
}

