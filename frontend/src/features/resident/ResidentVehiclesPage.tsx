import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Car,
  CreditCard,
  Lock,
  PlusCircle,
  PowerOff,
  ShieldAlert,
  XCircle,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { formatDateTimeVi } from '../../lib/formatters';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';
import { useToast } from '../../components/ui/Toast';

export function ResidentVehiclesPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [vehicleType, setVehicleType] = useState<string>('Xe máy');
  const [licensePlate, setLicensePlate] = useState<string>('');
  const [brandModel, setBrandModel] = useState<string>('');
  const [color, setColor] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<string>('');

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'vehicles', statusFilter],
    queryFn: () =>
      residentApi.getVehicles({
        status: statusFilter || undefined,
      }),
  });

  const registerMutation = useMutation({
    mutationFn: () =>
      residentApi.registerVehicle({
        vehicleType,
        licensePlate: licensePlate.trim().toUpperCase(),
        brandModel: brandModel.trim() || undefined,
        color: color.trim() || undefined,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đăng ký phương tiện thành công',
        message: res.message,
      });
      setLicensePlate('');
      setBrandModel('');
      setColor('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'vehicles'] });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể đăng ký phương tiện',
        message: err instanceof Error ? err.message : 'Vui lòng kiểm tra thông tin xe.',
      });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (id: number) => residentApi.cancelVehicle(id),
    onSuccess: (res) => {
      pushToast({ type: 'success', title: 'Đã hủy đơn đăng ký', message: res.message });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'vehicles'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Lỗi hủy đăng ký',
        message: err instanceof Error ? err.message : 'Thao tác không thành công.',
      });
    },
  });

  const lockCardMutation = useMutation({
    mutationFn: (id: number) =>
      residentApi.lockVehicleCard(id, 'Cư dân báo mất / khóa thẻ khẩn cấp qua Cổng Cư Dân'),
    onSuccess: (res) => {
      pushToast({
        type: 'warning',
        title: 'Đã khóa thẻ gửi xe khẩn cấp',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'vehicles'] });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể khóa thẻ',
        message: err instanceof Error ? err.message : 'Vui lòng thử lại.',
      });
    },
  });

  const decommissionMutation = useMutation({
    mutationFn: (id: number) =>
      residentApi.decommissionVehicle(id, 'Cư dân ngừng sử dụng phương tiện'),
    onSuccess: (res) => {
      pushToast({
        type: 'info',
        title: 'Đã ngừng sử dụng phương tiện',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'vehicles'] });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể ngừng sử dụng',
        message: err instanceof Error ? err.message : 'Vui lòng thử lại.',
      });
    },
  });

  if (isLoading) {
    return <LoadingState label="Đang tải danh sách phương tiện và thẻ gửi xe..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải danh sách phương tiện"
        description={error instanceof Error ? error.message : 'Vui lòng thử lại.'}
        onRetry={() => void refetch()}
      />
    );
  }

  const vehicles = data.vehicles;
  const approvedCount = vehicles.filter((v) => v.status === 'Đã duyệt').length;
  const activeCardsCount = vehicles.filter(
    (v) => v.parkingCard?.status === 'Active'
  ).length;
  const lockedCardsCount = vehicles.filter(
    (v) =>
      v.parkingCard &&
      (v.parkingCard.status === 'Lost' || v.parkingCard.status === 'Locked')
  ).length;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!licensePlate.trim()) return;
    registerMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Phương tiện & Thẻ gửi xe cư dân
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Đăng ký phương tiện mới cho hộ gia đình, tra cứu trạng thái thẻ từ bãi đỗ xe và báo khóa thẻ khẩn cấp khi thất lạc.
        </p>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Phương tiện đang hoạt động
          </p>
          <p className="text-xl font-bold text-[#0F172A] mt-1">
            {approvedCount} / {vehicles.length} phương tiện
          </p>
        </Card>
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Thẻ gửi xe đang kích hoạt (Active)
          </p>
          <p className="text-xl font-bold text-[#15803D] mt-1">
            {activeCardsCount} thẻ từ RFID
          </p>
        </Card>
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Thẻ đang báo mất / Đã khóa
          </p>
          <p className="text-xl font-bold text-[#B91C1C] mt-1">
            {lockedCardsCount} thẻ
          </p>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Registration Form */}
        <Card className="p-5 lg:col-span-4 h-fit">
          <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3 mb-4">
            <PlusCircle className="w-4 h-4 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Đăng ký phương tiện mới
            </h2>
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                Loại phương tiện
              </label>
              <select
                value={vehicleType}
                onChange={(e) => setVehicleType(e.target.value)}
                className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
              >
                <option value="Xe máy">Xe máy</option>
                <option value="Ô tô">Ô tô</option>
                <option value="Xe đạp điện">Xe đạp điện</option>
              </select>
            </div>

            <Input
              label="Biển số xe"
              placeholder="Ví dụ: 30A-123.45 hoặc 29B1-678.90"
              value={licensePlate}
              onChange={(e) => setLicensePlate(e.target.value)}
              required
            />

            <Input
              label="Hãng / Dòng xe (Tùy chọn)"
              placeholder="Ví dụ: Honda SH 150i, Mazda CX-5..."
              value={brandModel}
              onChange={(e) => setBrandModel(e.target.value)}
            />

            <Input
              label="Màu sơn (Tùy chọn)"
              placeholder="Ví dụ: Trắng, Đen, Xám..."
              value={color}
              onChange={(e) => setColor(e.target.value)}
            />

            <Button
              type="submit"
              variant="primary"
              className="w-full"
              isLoading={registerMutation.isPending}
            >
              <Car className="w-4 h-4 mr-1.5" />
              Gửi đăng ký xe & Cấp thẻ
            </Button>
          </form>
        </Card>

        {/* Vehicle & Parking Card List */}
        <Card className="p-5 lg:col-span-8 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[#E2E8F0] pb-3">
            <div className="flex items-center gap-2">
              <Car className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                Danh sách xe & Thẻ gửi xe của hộ ({vehicles.length})
              </h2>
            </div>

            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="rounded-lg border border-[#CBD5E1] bg-white px-3 py-1.5 text-xs font-medium text-[#0F172A]"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Chờ duyệt">Chờ duyệt</option>
              <option value="Đã duyệt">Đã duyệt</option>
              <option value="Từ chối">Từ chối</option>
              <option value="Ngừng sử dụng">Ngừng sử dụng</option>
              <option value="Đã hủy">Đã hủy</option>
            </select>
          </div>

          {vehicles.length === 0 ? (
            <EmptyState
              title="Chưa có phương tiện nào"
              description="Điền biểu mẫu bên trái để đăng ký phương tiện và cấp thẻ gửi xe cho căn hộ của bạn."
            />
          ) : (
            <div className="space-y-3">
              {vehicles.map((v) => (
                <div
                  key={v.vehicleId}
                  className="rounded-xl border border-[#E2E8F0] p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4"
                >
                  <div className="space-y-1.5">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-base font-bold text-[#0F172A]">
                        {v.licensePlate}
                      </span>
                      <Badge variant="neutral">{v.vehicleType}</Badge>
                      <Badge
                        variant={
                          v.status === 'Đã duyệt'
                            ? 'success'
                            : v.status === 'Chờ duyệt'
                            ? 'warning'
                            : v.status === 'Từ chối'
                            ? 'danger'
                            : 'neutral'
                        }
                      >
                        {v.status}
                      </Badge>
                    </div>

                    <p className="text-xs text-[#475569]">
                      Căn hộ <strong>{v.apartmentCode}</strong>
                      {v.brandModel ? ` • ${v.brandModel}` : ''}
                      {v.color ? ` (${v.color})` : ''} • Đăng ký:{' '}
                      {formatDateTimeVi(v.createdAt)}
                    </p>

                    {v.parkingCard ? (
                      <div className="inline-flex items-center gap-2 rounded-lg bg-[#F8FAFC] border border-[#E2E8F0] px-3 py-1.5 text-xs mt-1">
                        <CreditCard className="w-3.5 h-3.5 text-[#2563EB]" />
                        <span>
                          Mã thẻ từ:{' '}
                          <strong className="font-mono text-[#0F172A]">
                            {v.parkingCard.cardCode}
                          </strong>
                        </span>
                        <Badge
                          variant={
                            v.parkingCard.status === 'Active' ? 'success' : 'danger'
                          }
                        >
                          {v.parkingCard.status === 'Active'
                            ? 'Đang hoạt động'
                            : 'Đã khóa / Báo mất'}
                        </Badge>
                      </div>
                    ) : (
                      <p className="text-xs text-[#64748B]">
                        Thẻ gửi xe: Chưa cấp (sẽ được cấp khi Ban Quản lý phê duyệt)
                      </p>
                    )}

                    {v.reviewNotes && (
                      <p className="text-xs text-[#475569] italic">
                        Ghi chú BQL: {v.reviewNotes}
                      </p>
                    )}
                  </div>

                  <div className="flex flex-wrap items-center gap-2 shrink-0">
                    {v.status === 'Chờ duyệt' && (
                      <Button
                        variant="secondary"
                        size="sm"
                        isLoading={cancelMutation.isPending}
                        onClick={() => cancelMutation.mutate(v.vehicleId)}
                      >
                        <XCircle className="w-3.5 h-3.5 mr-1" />
                        Hủy đơn
                      </Button>
                    )}

                    {v.parkingCard && v.parkingCard.status === 'Active' && (
                      <Button
                        variant="danger"
                        size="sm"
                        isLoading={lockCardMutation.isPending}
                        onClick={() => lockCardMutation.mutate(v.vehicleId)}
                      >
                        <Lock className="w-3.5 h-3.5 mr-1" />
                        Khóa thẻ khẩn cấp
                      </Button>
                    )}

                    {v.status === 'Đã duyệt' && (
                      <Button
                        variant="secondary"
                        size="sm"
                        isLoading={decommissionMutation.isPending}
                        onClick={() => decommissionMutation.mutate(v.vehicleId)}
                      >
                        <PowerOff className="w-3.5 h-3.5 mr-1" />
                        Ngừng sử dụng
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="rounded-xl bg-amber-50/70 border border-amber-200 p-3.5 flex items-start gap-2.5 text-xs text-amber-900">
            <ShieldAlert className="w-4 h-4 text-amber-700 shrink-0 mt-0.5" />
            <div>
              <strong>Lưu ý an ninh thẻ gửi xe:</strong> Khi phát hiện mất thẻ gửi xe, vui lòng nhấn ngay nút{' '}
              <strong>&ldquo;Khóa thẻ khẩn cấp&rdquo;</strong> để vô hiệu hóa thẻ tại cổng kiểm soát bãi đỗ xe, sau đó liên hệ Ban Quản lý để được cấp đổi thẻ mới.
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}

