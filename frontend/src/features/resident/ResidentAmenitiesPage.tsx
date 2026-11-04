import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarCheck,
  Clock,
  MapPin,
  PlusCircle,
  Users,
  XCircle,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { formatDateVi, formatVnd } from '../../lib/formatters';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';
import { useToast } from '../../components/ui/Toast';
import type { ResidentAmenity } from '../../types/resident';

function getDefaultTomorrowDate(): string {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}

export function ResidentAmenitiesPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [selectedAmenity, setSelectedAmenity] = useState<ResidentAmenity | null>(null);
  const [bookingDate, setBookingDate] = useState<string>(getDefaultTomorrowDate());
  const [startTime, setStartTime] = useState<string>('08:00');
  const [endTime, setEndTime] = useState<string>('10:00');
  const [numberOfGuests, setNumberOfGuests] = useState<number>(2);
  const [notes, setNotes] = useState<string>('');
  const [feeFilter, setFeeFilter] = useState<'all' | 'free' | 'paid'>('all');

  const amenitiesQuery = useQuery({
    queryKey: ['resident', 'amenities'],
    queryFn: () => residentApi.getAmenities(),
  });

  useEffect(() => {
    if (!selectedAmenity && amenitiesQuery.data && amenitiesQuery.data.amenities.length > 0) {
      const firstActive =
        amenitiesQuery.data.amenities.find((a) => a.isActive) ??
        amenitiesQuery.data.amenities[0];
      setSelectedAmenity(firstActive);
      setStartTime(firstActive.openTime);
    }
  }, [amenitiesQuery.data, selectedAmenity]);

  const bookedSlotsQuery = useQuery({
    queryKey: ['resident', 'amenityBookedSlots', selectedAmenity?.amenityId, bookingDate],
    queryFn: () =>
      residentApi.getAmenityBookedSlots(selectedAmenity!.amenityId, bookingDate),
    enabled: Boolean(selectedAmenity && bookingDate),
  });

  const bookMutation = useMutation({
    mutationFn: () =>
      residentApi.createAmenityBooking({
        amenityId: selectedAmenity!.amenityId,
        bookingDate,
        startTime,
        endTime,
        numberOfGuests,
        notes: notes.trim() || undefined,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đặt lịch tiện ích thành công',
        message: res.message,
      });
      setNotes('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'amenities'] });
      void queryClient.invalidateQueries({
        queryKey: ['resident', 'amenityBookedSlots'],
      });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể đặt lịch tiện ích',
        message: err instanceof Error ? err.message : 'Vui lòng kiểm tra lại khung giờ.',
      });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (bookingId: number) => residentApi.cancelAmenityBooking(bookingId),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã hủy lịch đặt',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'amenities'] });
      void queryClient.invalidateQueries({
        queryKey: ['resident', 'amenityBookedSlots'],
      });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể hủy lịch đặt',
        message: err instanceof Error ? err.message : 'Vui lòng thử lại.',
      });
    },
  });

  if (amenitiesQuery.isLoading) {
    return <LoadingState label="Đang tải danh mục tiện ích nội khu..." />;
  }

  if (amenitiesQuery.isError || !amenitiesQuery.data) {
    return (
      <ErrorState
        title="Không thể tải danh mục tiện ích"
        description={
          amenitiesQuery.error instanceof Error
            ? amenitiesQuery.error.message
            : 'Đã xảy ra lỗi khi tải dữ liệu tiện ích.'
        }
        onRetry={() => void amenitiesQuery.refetch()}
      />
    );
  }

  const { amenities, myBookings } = amenitiesQuery.data;
  const visibleAmenities = amenities.filter((a) => {
    if (feeFilter === 'free') return a.isFree;
    if (feeFilter === 'paid') return !a.isFree;
    return true;
  });

  const calculateEstimatedCost = () => {
    if (!selectedAmenity || selectedAmenity.pricePerHour <= 0) return 0;
    const [sh, sm] = startTime.split(':').map(Number);
    const [eh, em] = endTime.split(':').map(Number);
    if (Number.isNaN(sh) || Number.isNaN(eh)) return 0;
    const hours = eh + (em || 0) / 60 - (sh + (sm || 0) / 60);
    return hours > 0 ? Math.round(hours * selectedAmenity.pricePerHour) : 0;
  };

  const handleSubmitBooking = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedAmenity) return;
    bookMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Đặt lịch tiện ích nội khu
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Đặt chỗ sử dụng các tiện ích miễn phí và có phí trong khuôn viên khu đô thị (Hồ bơi, khu nướng BBQ, sân cầu lông, tennis, bóng rổ, phòng Gym, phòng sinh hoạt cộng đồng...).
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Amenity Catalog */}
        <Card className="p-5 lg:col-span-7 space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-[#E2E8F0] pb-3">
            <div className="flex items-center gap-2">
              <CalendarCheck className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                Danh mục tiện ích ({visibleAmenities.length})
              </h2>
            </div>

            <div className="flex items-center gap-1 rounded-lg bg-[#F1F5F9] p-1 text-xs">
              <button
                type="button"
                onClick={() => setFeeFilter('all')}
                className={`px-2.5 py-1 rounded-md font-medium ${
                  feeFilter === 'all' ? 'bg-white text-[#0F172A] shadow-2xs' : 'text-[#475569]'
                }`}
              >
                Tất cả
              </button>
              <button
                type="button"
                onClick={() => setFeeFilter('free')}
                className={`px-2.5 py-1 rounded-md font-medium ${
                  feeFilter === 'free' ? 'bg-white text-[#0F172A] shadow-2xs' : 'text-[#475569]'
                }`}
              >
                Miễn phí
              </button>
              <button
                type="button"
                onClick={() => setFeeFilter('paid')}
                className={`px-2.5 py-1 rounded-md font-medium ${
                  feeFilter === 'paid' ? 'bg-white text-[#0F172A] shadow-2xs' : 'text-[#475569]'
                }`}
              >
                Có phí
              </button>
            </div>
          </div>

          {visibleAmenities.length === 0 ? (
            <EmptyState
              title="Chưa có tiện ích phù hợp"
              description="Không tìm thấy tiện ích nào khớp với bộ lọc hiện tại."
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              {visibleAmenities.map((item) => {
                const isSelected = selectedAmenity?.amenityId === item.amenityId;
                return (
                  <button
                    key={item.amenityId}
                    type="button"
                    onClick={() => setSelectedAmenity(item)}
                    className={`text-left rounded-xl border p-4 transition-all flex flex-col justify-between ${
                      isSelected
                        ? 'border-[#2563EB] bg-blue-50/50 shadow-xs'
                        : 'border-[#E2E8F0] bg-white hover:border-slate-300'
                    }`}
                  >
                    <div>
                      <div className="flex items-start justify-between gap-2">
                        <h3 className="text-sm font-bold text-[#0F172A]">
                          {item.name}
                        </h3>
                        <Badge
                          variant={
                            !item.isActive
                              ? 'neutral'
                              : item.isFree
                              ? 'success'
                              : 'info'
                          }
                        >
                          {!item.isActive
                            ? 'Tạm ngưng'
                            : item.isFree
                            ? 'Miễn phí'
                            : `${formatVnd(item.pricePerHour)}/giờ`}
                        </Badge>
                      </div>

                      {item.description && (
                        <p className="text-xs text-[#475569] mt-1.5 line-clamp-2">
                          {item.description}
                        </p>
                      )}
                    </div>

                    <div className="mt-3 pt-2.5 border-t border-[#E2E8F0] space-y-1 text-xs text-[#64748B]">
                      {item.location && (
                        <div className="flex items-center gap-1.5">
                          <MapPin className="w-3.5 h-3.5 shrink-0" />
                          <span className="truncate">{item.location}</span>
                        </div>
                      )}
                      <div className="flex items-center justify-between">
                        <span className="inline-flex items-center gap-1">
                          <Clock className="w-3.5 h-3.5" />
                          {item.openTime} – {item.closeTime}
                        </span>
                        <span className="inline-flex items-center gap-1">
                          <Users className="w-3.5 h-3.5" />
                          Tối đa {item.capacity} người
                        </span>
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          )}
        </Card>

        {/* Booking Form & Real-time Slot Checker */}
        <Card className="p-5 lg:col-span-5">
          <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3 mb-4">
            <PlusCircle className="w-4 h-4 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Đăng ký khung giờ sử dụng
            </h2>
          </div>

          {selectedAmenity ? (
            <form onSubmit={handleSubmitBooking} className="space-y-4">
              <div className="rounded-xl bg-[#F8FAFC] p-3.5 border border-[#E2E8F0]">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-bold text-[#0F172A]">
                    {selectedAmenity.name}
                  </span>
                  <Badge variant={selectedAmenity.isFree ? 'success' : 'info'}>
                    {selectedAmenity.isFree
                      ? 'Miễn phí'
                      : `${formatVnd(selectedAmenity.pricePerHour)}/giờ`}
                  </Badge>
                </div>
                <p className="text-xs text-[#475569] mt-1">
                  Giờ mở cửa: <strong>{selectedAmenity.openTime}</strong> –{' '}
                  <strong>{selectedAmenity.closeTime}</strong> • Sức chứa tối đa:{' '}
                  <strong>{selectedAmenity.capacity} người</strong>
                </p>
              </div>

              <Input
                label="Ngày sử dụng"
                type="date"
                value={bookingDate}
                onChange={(e) => setBookingDate(e.target.value)}
                required
              />

              {/* Booked Slots in Selected Date */}
              <div className="rounded-xl border border-[#E2E8F0] p-3 bg-white">
                <p className="text-xs font-semibold text-[#0F172A] mb-2">
                  Các khung giờ đã có người đặt trong ngày {formatDateVi(bookingDate)}:
                </p>
                {bookedSlotsQuery.isLoading ? (
                  <p className="text-xs text-[#64748B]">Đang kiểm tra lịch trống...</p>
                ) : bookedSlotsQuery.data && bookedSlotsQuery.data.length > 0 ? (
                  <div className="flex flex-wrap gap-1.5">
                    {bookedSlotsQuery.data.map((slot) => (
                      <span
                        key={slot.bookingId}
                        className="inline-flex items-center gap-1 rounded-md bg-amber-50 border border-amber-200 px-2 py-1 text-[11px] font-medium text-amber-800"
                      >
                        <Clock className="w-3 h-3" />
                        {slot.startTime}–{slot.endTime} ({slot.guests} khách, {slot.status})
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-[#15803D]">
                    Tất cả khung giờ trong ngày hiện đang trống.
                  </p>
                )}
              </div>

              <div className="grid grid-cols-2 gap-3">
                <Input
                  label="Giờ bắt đầu"
                  type="time"
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                  required
                />
                <Input
                  label="Giờ kết thúc"
                  type="time"
                  value={endTime}
                  onChange={(e) => setEndTime(e.target.value)}
                  required
                />
              </div>

              <Input
                label={`Số lượng người tham gia (Tối đa ${selectedAmenity.capacity})`}
                type="number"
                min={1}
                max={selectedAmenity.capacity}
                value={numberOfGuests}
                onChange={(e) => setNumberOfGuests(Number(e.target.value) || 1)}
                required
              />

              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Ghi chú / Nhu cầu sử dụng (Tùy chọn)
                </label>
                <textarea
                  rows={2}
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="Ví dụ: Đặt sân cầu lông đôi / Đặt bếp nướng BBQ gia đình..."
                  className="w-full rounded-lg border border-[#CBD5E1] px-3 py-2 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                />
              </div>

              <div className="rounded-xl bg-blue-50/60 border border-blue-200 p-3 flex items-center justify-between text-xs">
                <span className="font-medium text-[#1E3A8A]">Chi phí dự kiến:</span>
                <span className="text-sm font-bold text-[#172554] tabular-nums">
                  {selectedAmenity.isFree ? 'Miễn phí (0 ₫)' : formatVnd(calculateEstimatedCost())}
                </span>
              </div>

              <Button
                type="submit"
                variant="primary"
                className="w-full"
                disabled={!selectedAmenity.isActive}
                isLoading={bookMutation.isPending}
              >
                <CalendarCheck className="w-4 h-4 mr-1.5" />
                {selectedAmenity.isActive
                  ? 'Xác nhận đặt lịch tiện ích'
                  : 'Tiện ích đang tạm ngưng'}
              </Button>
            </form>
          ) : (
            <EmptyState
              title="Chọn tiện ích để đặt lịch"
              description="Vui lòng chọn một tiện ích ở danh sách bên trái."
            />
          )}
        </Card>
      </div>

      {/* My Bookings */}
      <Card className="p-6">
        <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3.5 mb-4">
          <div className="flex items-center gap-2">
            <Clock className="w-5 h-5 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Lịch đặt tiện ích của tôi ({myBookings.length})
            </h2>
          </div>
        </div>

        {myBookings.length === 0 ? (
          <EmptyState
            title="Chưa có lịch đặt tiện ích nào"
            description="Lịch sử đặt chỗ sân thể thao, BBQ, hồ bơi và phòng sinh hoạt sẽ hiển thị tại đây."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-[#E2E8F0] text-xs uppercase text-[#64748B]">
                  <th className="py-3 pr-4">Tiện ích</th>
                  <th className="py-3 px-3">Ngày sử dụng</th>
                  <th className="py-3 px-3">Khung giờ</th>
                  <th className="py-3 px-3">Số khách</th>
                  <th className="py-3 px-3">Phí dự kiến</th>
                  <th className="py-3 px-3">Trạng thái</th>
                  <th className="py-3 pl-3 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E2E8F0]">
                {myBookings.map((b) => (
                  <tr key={b.amenityBookingId}>
                    <td className="py-3 pr-4">
                      <p className="font-semibold text-[#0F172A]">{b.amenityName}</p>
                      {b.notes && (
                        <p className="text-xs text-[#64748B] mt-0.5">{b.notes}</p>
                      )}
                    </td>
                    <td className="py-3 px-3 text-[#475569]">
                      {formatDateVi(b.bookingDate)}
                    </td>
                    <td className="py-3 px-3 font-mono text-xs text-[#0F172A]">
                      {b.startTime} – {b.endTime}
                    </td>
                    <td className="py-3 px-3 text-[#475569]">
                      {b.numberOfGuests} người
                    </td>
                    <td className="py-3 px-3 font-semibold text-[#0F172A] tabular-nums">
                      {b.estimatedCost > 0 ? formatVnd(b.estimatedCost) : 'Miễn phí'}
                    </td>
                    <td className="py-3 px-3">
                      <Badge
                        variant={
                          b.status === 'Đã xác nhận'
                            ? 'success'
                            : b.status === 'Chờ duyệt'
                            ? 'warning'
                            : b.status === 'Từ chối'
                            ? 'danger'
                            : 'neutral'
                        }
                      >
                        {b.status}
                      </Badge>
                    </td>
                    <td className="py-3 pl-3 text-right">
                      {b.canCancel && (
                        <Button
                          variant="danger"
                          size="sm"
                          isLoading={cancelMutation.isPending}
                          onClick={() => cancelMutation.mutate(b.amenityBookingId)}
                        >
                          <XCircle className="w-3.5 h-3.5 mr-1" />
                          Hủy lịch
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

