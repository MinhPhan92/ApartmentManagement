import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { KeyRound, Save, ShieldCheck, UserCog } from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { ErrorState, LoadingState } from '../../components/ui/States';
import { useToast } from '../../components/ui/Toast';

export function ResidentAccountPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [dateOfBirth, setDateOfBirth] = useState<string>('');
  const [gender, setGender] = useState<string>('');
  const [address, setAddress] = useState<string>('');
  const [emergencyContact, setEmergencyContact] = useState<string>('');

  const [currentPassword, setCurrentPassword] = useState<string>('');
  const [newPassword, setNewPassword] = useState<string>('');
  const [confirmPassword, setConfirmPassword] = useState<string>('');

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'profile'],
    queryFn: () => residentApi.getProfile(),
  });

  useEffect(() => {
    if (data) {
      setDateOfBirth(data.dateOfBirth ?? '');
      setGender(data.gender ?? '');
      setAddress(data.address ?? '');
      setEmergencyContact(data.emergencyContact ?? '');
    }
  }, [data]);

  const updateProfileMutation = useMutation({
    mutationFn: () =>
      residentApi.updateProfile({
        dateOfBirth: dateOfBirth || null,
        gender: gender.trim() || undefined,
        address: address.trim() || undefined,
        emergencyContact: emergencyContact.trim() || undefined,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã lưu hồ sơ cá nhân',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'profile'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể cập nhật hồ sơ',
        message: err instanceof Error ? err.message : 'Dữ liệu không hợp lệ.',
      });
    },
  });

  const changePasswordMutation = useMutation({
    mutationFn: () =>
      residentApi.changePassword({
        currentPassword,
        newPassword,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đổi mật khẩu thành công',
        message: res.message,
      });
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể đổi mật khẩu',
        message: err instanceof Error ? err.message : 'Vui lòng kiểm tra lại mật khẩu.',
      });
    },
  });

  if (isLoading) {
    return <LoadingState label="Đang tải thông tin hồ sơ cư dân..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải hồ sơ cá nhân"
        description={error instanceof Error ? error.message : 'Vui lòng thử lại.'}
        onRetry={() => void refetch()}
      />
    );
  }

  const handleProfileSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    updateProfileMutation.mutate();
  };

  const handlePasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      pushToast({
        type: 'error',
        title: 'Mật khẩu xác nhận không khớp',
        message: 'Mật khẩu mới và mật khẩu xác nhận phải giống nhau.',
      });
      return;
    }
    changePasswordMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Hồ sơ cá nhân & Bảo mật tài khoản
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Quản lý thông tin định danh cư dân, số điện thoại liên hệ khẩn cấp và mật khẩu đăng nhập Cổng Cư Dân.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Profile Form */}
        <Card className="p-6 lg:col-span-7">
          <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3.5 mb-5">
            <UserCog className="w-5 h-5 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Thông tin hồ sơ cư dân
            </h2>
          </div>

          <form onSubmit={handleProfileSubmit} className="space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                label="Họ và tên (Do Ban Quản lý định danh)"
                value={data.fullName}
                disabled
              />
              <Input
                label="Số CCCD / CMND"
                value={data.citizenId}
                disabled
              />
            </div>

            <Input
              label="Email tài khoản đăng nhập"
              value={data.email}
              disabled
            />

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                label="Ngày sinh"
                type="date"
                value={dateOfBirth}
                onChange={(e) => setDateOfBirth(e.target.value)}
              />

              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Giới tính
                </label>
                <select
                  value={gender}
                  onChange={(e) => setGender(e.target.value)}
                  className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A]"
                >
                  <option value="">-- Chọn giới tính --</option>
                  <option value="Nam">Nam</option>
                  <option value="Nữ">Nữ</option>
                  <option value="Khác">Khác</option>
                </select>
              </div>
            </div>

            <Input
              label="Số điện thoại liên hệ khẩn cấp (9–15 chữ số)"
              placeholder="Ví dụ: 0912345678"
              value={emergencyContact}
              onChange={(e) => setEmergencyContact(e.target.value)}
            />

            <Input
              label="Địa chỉ thường trú / Liên lạc"
              placeholder="Nhập địa chỉ liên lạc..."
              value={address}
              onChange={(e) => setAddress(e.target.value)}
            />

            <div className="pt-2">
              <Button
                type="submit"
                variant="primary"
                isLoading={updateProfileMutation.isPending}
              >
                <Save className="w-4 h-4 mr-1.5" />
                Lưu thông tin hồ sơ
              </Button>
            </div>
          </form>
        </Card>

        {/* Password Change Form */}
        <Card className="p-6 lg:col-span-5 h-fit">
          <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3.5 mb-5">
            <KeyRound className="w-5 h-5 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Đổi mật khẩu đăng nhập
            </h2>
          </div>

          <form onSubmit={handlePasswordSubmit} className="space-y-4">
            <Input
              label="Mật khẩu hiện tại"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
            />

            <Input
              label="Mật khẩu mới (Tối thiểu 8 ký tự, gồm hoa, thường, số & ký tự đặc biệt)"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
            />

            <Input
              label="Xác nhận mật khẩu mới"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
            />

            <div className="rounded-xl bg-[#F8FAFC] border border-[#E2E8F0] p-3 text-xs text-[#475569] flex items-start gap-2">
              <ShieldCheck className="w-4 h-4 text-[#2563EB] shrink-0 mt-0.5" />
              <span>
                Sau khi đổi mật khẩu thành công, phiên đăng nhập hiện tại của bạn sẽ tự động được làm mới an toàn.
              </span>
            </div>

            <Button
              type="submit"
              variant="primary"
              className="w-full"
              isLoading={changePasswordMutation.isPending}
            >
              <KeyRound className="w-4 h-4 mr-1.5" />
              Cập nhật mật khẩu mới
            </Button>
          </form>
        </Card>
      </div>
    </div>
  );
}

