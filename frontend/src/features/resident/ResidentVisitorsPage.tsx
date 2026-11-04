import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BellRing,
  Calendar,
  PlusCircle,
  UserCheck,
  Users,
  XCircle,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { formatDateTimeVi, formatDateVi } from '../../lib/formatters';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';
import { useToast } from '../../components/ui/Toast';

function getTodayDateString(): string {
  return new Date().toISOString().slice(0, 10);
}

export function ResidentVisitorsPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [apartmentId, setApartmentId] = useState<number>(0);
  const [visitorName, setVisitorName] = useState<string>('');
  const [phoneNumber, setPhoneNumber] = useState<string>('');
  const [idCardNumber, setIdCardNumber] = useState<string>('');
  const [expectedArrivalDate, setExpectedArrivalDate] = useState<string>(
    getTodayDateString()
  );
  const [purpose, setPurpose] = useState<string>('');

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'visitors'],
    queryFn: () => residentApi.getVisitors(),
  });

  useEffect(() => {
    if (!apartmentId && data && data.apartments.length > 0) {
      setApartmentId(data.apartments[0].apartmentId);
    }
  }, [data, apartmentId]);

  const registerMutation = useMutation({
    mutationFn: () =>
      residentApi.registerVisitor({
        apartmentId,
        visitorName: visitorName.trim(),
        phoneNumber: phoneNumber.trim() || undefined,
        idCardNumber: idCardNumber.trim() || undefined,
        expectedArrivalDate,
        purpose: purpose.trim() || undefined,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đăng ký khách thăm thành công',
        message: res.message,
      });
      setVisitorName('');
      setPhoneNumber('');
      setIdCardNumber('');
      setPurpose('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'visitors'] });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể đăng ký khách',
        message: err instanceof Error ? err.message : 'Vui lòng kiểm tra lại thông tin.',
      });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (id: number) => residentApi.cancelVisitor(id),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã hủy lịch hẹn khách',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'visitors'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể hủy lịch hẹn',
        message: err instanceof Error ? err.message : 'Thao tác không thành công.',
      });
    },
  });

  if (isLoading) {
    return <LoadingState label="Đang tải danh sách khách đăng ký ra vào..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải dữ liệu khách thăm"
        description={error instanceof Error ? error.message : 'Vui lòng thử lại.'}
        onRetry={() => void refetch()}
      />
    );
  }

  const { visitors, apartments } = data;
  const registeredCount = visitors.filter((v) => v.status === 'Đã đăng ký').length;
  const checkedInCount = visitors.filter((v) => v.status === 'Đã vào').length;
  const checkedOutCount = visitors.filter((v) => v.status === 'Đã ra').length;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!visitorName.trim() || !apartmentId) return;
    registerMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Quản lý khách ra vào căn hộ
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Đăng ký trước thông tin cho khách đến thăm căn hộ và nhận thông báo tức thời qua ứng dụng khi khách làm thủ tục Check-in tại sảnh lễ tân.
        </p>
      </div>

      {/* Realtime Notification Banner */}
      <div className="rounded-xl bg-blue-50/70 border border-blue-200 p-4 flex items-start gap-3 text-xs text-[#1E3A8A]">
        <BellRing className="w-5 h-5 text-[#2563EB] shrink-0 mt-0.5" />
        <div>
          <strong>Thông báo khách đến tự động (SignalR Realtime):</strong> Khi khách của bạn đến sảnh tòa nhà và được bộ phận An ninh / Lễ tân xác nhận Check-in, hệ thống sẽ đẩy thông báo tức thời lên chuông thông báo trên màn hình của bạn.
        </div>
      </div>

      {/* Summary KPIs */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Khách sắp đến (Đã đăng ký)
          </p>
          <p className="text-xl font-bold text-[#2563EB] mt-1">{registeredCount} lượt</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Khách đang ở trong tòa nhà
          </p>
          <p className="text-xl font-bold text-[#15803D] mt-1">{checkedInCount} khách</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Khách đã hoàn tất Check-out
          </p>
          <p className="text-xl font-bold text-[#0F172A] mt-1">{checkedOutCount} lượt</p>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Visitor Registration Form */}
        <Card className="p-5 lg:col-span-4 h-fit">
          <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3 mb-4">
            <PlusCircle className="w-4 h-4 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Đăng ký khách đến thăm
            </h2>
          </div>

          {apartments.length === 0 ? (
            <EmptyState
              title="Chưa có căn hộ đang cư trú"
              description="Bạn cần có căn hộ đang cư trú để đăng ký khách đến thăm."
            />
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Căn hộ đón khách
                </label>
                <select
                  value={apartmentId}
                  onChange={(e) => setApartmentId(Number(e.target.value))}
                  className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                >
                  {apartments.map((apt) => (
                    <option key={apt.apartmentId} value={apt.apartmentId}>
                      {apt.label}
                    </option>
                  ))}
                </select>
              </div>

              <Input
                label="Họ và tên khách"
                placeholder="Nhập họ tên khách thăm"
                value={visitorName}
                onChange={(e) => setVisitorName(e.target.value)}
                required
              />

              <Input
                label="Số điện thoại khách (Tùy chọn)"
                placeholder="Ví dụ: 0901234567"
                value={phoneNumber}
                onChange={(e) => setPhoneNumber(e.target.value)}
              />

              <Input
                label="Số CCCD / CMND (Tùy chọn)"
                placeholder="Nhập số CCCD để đối chiếu tại sảnh"
                value={idCardNumber}
                onChange={(e) => setIdCardNumber(e.target.value)}
              />

              <Input
                label="Ngày dự kiến đến"
                type="date"
                value={expectedArrivalDate}
                onChange={(e) => setExpectedArrivalDate(e.target.value)}
                required
              />

              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Mục đích đến thăm (Tùy chọn)
                </label>
                <textarea
                  rows={2}
                  value={purpose}
                  onChange={(e) => setPurpose(e.target.value)}
                  placeholder="Ví dụ: Người thân đến chơi, Giao lắp nội thất..."
                  className="w-full rounded-lg border border-[#CBD5E1] px-3 py-2 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                />
              </div>

              <Button
                type="submit"
                variant="primary"
                className="w-full"
                isLoading={registerMutation.isPending}
              >
                <UserCheck className="w-4 h-4 mr-1.5" />
                Đăng ký khách đến căn hộ
              </Button>
            </form>
          )}
        </Card>

        {/* Visitor List */}
        <Card className="p-5 lg:col-span-8 space-y-4">
          <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3">
            <div className="flex items-center gap-2">
              <Users className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                Danh sách khách đã đăng ký ({visitors.length})
              </h2>
            </div>
          </div>

          {visitors.length === 0 ? (
            <EmptyState
              title="Chưa có lịch sử đăng ký khách"
              description="Khi bạn đăng ký khách đến thăm căn hộ, danh sách và trạng thái ra/vào của khách sẽ hiển thị tại đây."
            />
          ) : (
            <div className="space-y-3">
              {visitors.map((v) => (
                <div
                  key={v.visitorPassId}
                  className="rounded-xl border border-[#E2E8F0] p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3"
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-bold text-[#0F172A]">
                        {v.visitorName}
                      </span>
                      <Badge
                        variant={
                          v.status === 'Đã vào'
                            ? 'success'
                            : v.status === 'Đã đăng ký'
                            ? 'info'
                            : v.status === 'Đã hủy'
                            ? 'danger'
                            : 'neutral'
                        }
                      >
                        {v.status}
                      </Badge>
                    </div>
                    <p className="text-xs text-[#475569] flex flex-wrap items-center gap-x-3 gap-y-1">
                      <span>Căn hộ: <strong>{v.apartmentCode}</strong></span>
                      <span className="inline-flex items-center gap-1">
                        <Calendar className="w-3.5 h-3.5 text-[#64748B]" />
                        Ngày hẹn: {formatDateVi(v.expectedArrivalDate)}
                      </span>
                      {v.phoneNumber && <span>SĐT: {v.phoneNumber}</span>}
                      {v.idCardNumber && <span>CCCD: {v.idCardNumber}</span>}
                    </p>
                    {v.purpose && (
                      <p className="text-xs text-[#64748B]">Mục đích: {v.purpose}</p>
                    )}
                    {(v.actualCheckInUtc || v.actualCheckOutUtc) && (
                      <p className="text-xs text-[#15803D] font-medium">
                        {v.actualCheckInUtc
                          ? `Giờ vào (Check-in): ${formatDateTimeVi(v.actualCheckInUtc)}`
                          : ''}
                        {v.actualCheckOutUtc
                          ? ` • Giờ ra (Check-out): ${formatDateTimeVi(v.actualCheckOutUtc)}`
                          : ''}
                      </p>
                    )}
                  </div>

                  {v.canCancel && (
                    <Button
                      variant="danger"
                      size="sm"
                      isLoading={cancelMutation.isPending}
                      onClick={() => cancelMutation.mutate(v.visitorPassId)}
                    >
                      <XCircle className="w-3.5 h-3.5 mr-1" />
                      Hủy hẹn
                    </Button>
                  )}
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}

