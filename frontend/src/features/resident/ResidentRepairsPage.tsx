import React, { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Clock,
  History,
  PlusCircle,
  RotateCcw,
  Star,
  UserCog,
  Wrench,
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

function getRepairStatusBadge(status: string) {
  switch (status) {
    case 'Completed':
      return <Badge variant="success">Đã hoàn thành</Badge>;
    case 'PendingConfirmation':
      return <Badge variant="warning">Chờ cư dân nghiệm thu</Badge>;
    case 'InProgress':
      return <Badge variant="info">Đang sửa chữa</Badge>;
    case 'Assigned':
      return <Badge variant="info">Đã phân công KTV</Badge>;
    case 'Reopened':
      return <Badge variant="danger">Yêu cầu xử lý lại</Badge>;
    case 'Cancelled':
      return <Badge variant="neutral">Đã hủy</Badge>;
    default:
      return <Badge variant="warning">Chờ tiếp nhận</Badge>;
  }
}

export function ResidentRepairsPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [apartmentId, setApartmentId] = useState<number>(0);
  const [title, setTitle] = useState<string>('');
  const [description, setDescription] = useState<string>('');
  const [priority, setPriority] = useState<string>('Normal');

  const [selectedRepairId, setSelectedRepairId] = useState<number | null>(null);
  const [rating, setRating] = useState<number>(5);
  const [feedback, setFeedback] = useState<string>('');
  const [redoReason, setRedoReason] = useState<string>('');
  const [cancelReason, setCancelReason] = useState<string>('');

  const listQuery = useQuery({
    queryKey: ['resident', 'repairs'],
    queryFn: () => residentApi.getRepairs(),
  });

  useEffect(() => {
    if (!apartmentId && listQuery.data && listQuery.data.apartments.length > 0) {
      setApartmentId(listQuery.data.apartments[0].apartmentId);
    }
    if (!selectedRepairId && listQuery.data && listQuery.data.repairs.length > 0) {
      setSelectedRepairId(listQuery.data.repairs[0].repairRequestId);
    }
  }, [listQuery.data, apartmentId, selectedRepairId]);

  const detailsQuery = useQuery({
    queryKey: ['resident', 'repairDetails', selectedRepairId],
    queryFn: () => residentApi.getRepairDetails(selectedRepairId!),
    enabled: Boolean(selectedRepairId && selectedRepairId > 0),
  });

  const createMutation = useMutation({
    mutationFn: () =>
      residentApi.createRepair({
        apartmentId,
        title: title.trim(),
        description: description.trim(),
        priority,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã gửi yêu cầu sửa chữa',
        message: res.message,
      });
      setTitle('');
      setDescription('');
      setPriority('Normal');
      if (res.requestId) {
        setSelectedRepairId(res.requestId);
      }
      void queryClient.invalidateQueries({ queryKey: ['resident', 'repairs'] });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể gửi yêu cầu sửa chữa',
        message: err instanceof Error ? err.message : 'Vui lòng kiểm tra lại.',
      });
    },
  });

  const confirmMutation = useMutation({
    mutationFn: (payload: { id: number; rowVersionBase64: string }) =>
      residentApi.confirmRepair(payload.id, {
        rating,
        feedback: feedback.trim() || undefined,
        rowVersionBase64: payload.rowVersionBase64,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã nghiệm thu hoàn tất',
        message: res.message,
      });
      setFeedback('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'repairs'] });
      void queryClient.invalidateQueries({
        queryKey: ['resident', 'repairDetails', selectedRepairId],
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Lỗi nghiệm thu',
        message: err instanceof Error ? err.message : 'Vui lòng tải lại trang.',
      });
    },
  });

  const redoMutation = useMutation({
    mutationFn: (payload: { id: number; rowVersionBase64: string }) =>
      residentApi.redoRepair(payload.id, {
        redoReason: redoReason.trim(),
        rowVersionBase64: payload.rowVersionBase64,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'warning',
        title: 'Đã gửi yêu cầu xử lý lại',
        message: res.message,
      });
      setRedoReason('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'repairs'] });
      void queryClient.invalidateQueries({
        queryKey: ['resident', 'repairDetails', selectedRepairId],
      });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể yêu cầu làm lại',
        message: err instanceof Error ? err.message : 'Vui lòng nhập lý do cụ thể.',
      });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (payload: { id: number; rowVersionBase64: string }) =>
      residentApi.cancelRepair(payload.id, {
        reason: cancelReason.trim() || 'Cư dân chủ động hủy yêu cầu',
        rowVersionBase64: payload.rowVersionBase64,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'info',
        title: 'Đã hủy yêu cầu sửa chữa',
        message: res.message,
      });
      setCancelReason('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'repairs'] });
      void queryClient.invalidateQueries({
        queryKey: ['resident', 'repairDetails', selectedRepairId],
      });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể hủy yêu cầu',
        message: err instanceof Error ? err.message : 'Thao tác không thành công.',
      });
    },
  });

  if (listQuery.isLoading) {
    return <LoadingState label="Đang tải danh sách yêu cầu sửa chữa kỹ thuật..." />;
  }

  if (listQuery.isError || !listQuery.data) {
    return (
      <ErrorState
        title="Không thể tải dữ liệu sửa chữa"
        description={
          listQuery.error instanceof Error ? listQuery.error.message : 'Vui lòng thử lại.'
        }
        onRetry={() => void listQuery.refetch()}
      />
    );
  }

  const { repairs, apartments } = listQuery.data;
  const details = detailsQuery.data;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !description.trim() || !apartmentId) return;
    createMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Yêu cầu sửa chữa kỹ thuật căn hộ
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Gửi yêu cầu hỗ trợ sửa chữa điện, nước, điều hòa, thiết bị kỹ thuật trong căn hộ; theo dõi phân công kỹ thuật viên và nghiệm thu kết quả.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Create Repair Request Form */}
        <Card className="p-5 lg:col-span-4 h-fit">
          <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3 mb-4">
            <PlusCircle className="w-4 h-4 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Tạo yêu cầu sửa chữa mới
            </h2>
          </div>

          {apartments.length === 0 ? (
            <EmptyState
              title="Chưa có căn hộ đang cư trú"
              description="Bạn cần có căn hộ đang cư trú để gửi phiếu yêu cầu sửa chữa."
            />
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Căn hộ cần sửa chữa
                </label>
                <select
                  value={apartmentId}
                  onChange={(e) => setApartmentId(Number(e.target.value))}
                  className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A]"
                >
                  {apartments.map((apt) => (
                    <option key={apt.apartmentId} value={apt.apartmentId}>
                      {apt.label}
                    </option>
                  ))}
                </select>
              </div>

              <Input
                label="Tiêu đề sự cố"
                placeholder="Ví dụ: Rò rỉ đường ống nước bồn rửa bếp"
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
              />

              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Mức độ ưu tiên
                </label>
                <select
                  value={priority}
                  onChange={(e) => setPriority(e.target.value)}
                  className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A]"
                >
                  <option value="Low">Thấp (Low)</option>
                  <option value="Normal">Bình thường (Normal)</option>
                  <option value="High">Cao (High)</option>
                  <option value="Urgent">Khẩn cấp (Urgent)</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Mô tả chi tiết tình trạng
                </label>
                <textarea
                  rows={4}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Mô tả rõ vị trí hỏng hóc, biểu hiện sự cố và khung giờ thuận tiện để kỹ thuật viên đến kiểm tra..."
                  required
                  className="w-full rounded-lg border border-[#CBD5E1] px-3 py-2 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
                />
              </div>

              <Button
                type="submit"
                variant="primary"
                className="w-full"
                isLoading={createMutation.isPending}
              >
                <Wrench className="w-4 h-4 mr-1.5" />
                Gửi phiếu yêu cầu sửa chữa
              </Button>
            </form>
          )}
        </Card>

        {/* Repairs List & Detail Inspector */}
        <div className="lg:col-span-8 space-y-6">
          <Card className="p-5 space-y-3">
            <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3">
              <div className="flex items-center gap-2">
                <Wrench className="w-4 h-4 text-[#2563EB]" />
                <h2 className="text-base font-bold text-[#0F172A]">
                  Danh sách phiếu sửa chữa của tôi ({repairs.length})
                </h2>
              </div>
            </div>

            {repairs.length === 0 ? (
              <EmptyState
                title="Chưa có yêu cầu sửa chữa nào"
                description="Khi bạn gửi phiếu yêu cầu hỗ trợ kỹ thuật, tiến độ xử lý sẽ hiển thị tại đây."
              />
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {repairs.map((item) => {
                  const active = item.repairRequestId === selectedRepairId;
                  return (
                    <button
                      key={item.repairRequestId}
                      type="button"
                      onClick={() => setSelectedRepairId(item.repairRequestId)}
                      className={`text-left rounded-xl border p-3.5 transition-all ${
                        active
                          ? 'border-[#2563EB] bg-blue-50/50 shadow-2xs'
                          : 'border-[#E2E8F0] bg-white hover:border-slate-300'
                      }`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-xs font-mono font-bold text-[#2563EB]">
                          #RR-{item.repairRequestId}
                        </span>
                        {getRepairStatusBadge(item.status)}
                      </div>
                      <p className="text-sm font-bold text-[#0F172A] mt-1 line-clamp-1">
                        {item.title}
                      </p>
                      <p className="text-xs text-[#64748B] mt-1">
                        Căn hộ {item.apartmentCode} • Ưu tiên: {item.priority} •{' '}
                        {formatDateTimeVi(item.createdAt)}
                      </p>
                    </button>
                  );
                })}
              </div>
            )}
          </Card>

          {/* Selected Repair Detail Card */}
          {selectedRepairId && details && (
            <Card className="p-6 space-y-5">
              <div className="flex flex-wrap items-start justify-between gap-3 border-b border-[#E2E8F0] pb-4">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-sm font-bold text-[#2563EB]">
                      #RR-{details.repairRequestId}
                    </span>
                    <h3 className="text-lg font-bold text-[#0F172A]">{details.title}</h3>
                    {getRepairStatusBadge(details.status)}
                  </div>
                  <p className="text-xs text-[#475569] mt-1">
                    Căn hộ <strong>{details.apartmentCode}</strong> ({details.buildingName}) •
                    Mức độ ưu tiên: <strong>{details.priority}</strong> • Ngày gửi:{' '}
                    {formatDateTimeVi(details.createdAt)}
                  </p>
                </div>
              </div>

              <div className="rounded-xl bg-[#F8FAFC] p-4 border border-[#E2E8F0] text-sm">
                <p className="text-xs font-semibold uppercase text-[#64748B] mb-1">
                  Mô tả sự cố từ cư dân:
                </p>
                <p className="text-[#0F172A] whitespace-pre-line">{details.description}</p>
              </div>

              {details.result && (
                <div className="rounded-xl bg-emerald-50/60 p-4 border border-emerald-200 text-sm">
                  <p className="text-xs font-semibold uppercase text-emerald-800 mb-1">
                    Báo cáo kết quả xử lý từ Kỹ thuật viên:
                  </p>
                  <p className="text-[#0F172A] whitespace-pre-line">{details.result}</p>
                </div>
              )}

              {/* Action Box when PendingConfirmation */}
              {details.status === 'PendingConfirmation' && (
                <div className="rounded-2xl border border-amber-300 bg-amber-50/50 p-5 space-y-4">
                  <div className="flex items-center gap-2 text-amber-900 font-bold text-sm">
                    <CheckCircle2 className="w-4 h-4 text-amber-700" />
                    <span>Nghiệm thu kết quả sửa chữa hoặc Yêu cầu xử lý lại</span>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {/* Confirm Box */}
                    <div className="rounded-xl bg-white p-4 border border-[#E2E8F0] space-y-3">
                      <p className="text-xs font-bold text-[#0F172A]">
                        1. Hài lòng & Nghiệm thu hoàn tất
                      </p>
                      <div className="flex items-center gap-1.5">
                        {[1, 2, 3, 4, 5].map((star) => (
                          <button
                            key={star}
                            type="button"
                            onClick={() => setRating(star)}
                            className={`p-1.5 rounded-lg border transition-colors ${
                              rating >= star
                                ? 'bg-amber-50 border-amber-300 text-amber-500'
                                : 'bg-white border-slate-200 text-slate-300'
                            }`}
                          >
                            <Star className="w-4 h-4 fill-current" />
                          </button>
                        ))}
                        <span className="text-xs font-semibold text-[#0F172A] ml-1">
                          {rating}/5 sao
                        </span>
                      </div>
                      <textarea
                        rows={2}
                        value={feedback}
                        onChange={(e) => setFeedback(e.target.value)}
                        placeholder="Nhận xét về thái độ và chất lượng sửa chữa..."
                        className="w-full rounded-lg border border-[#CBD5E1] px-3 py-2 text-xs"
                      />
                      <Button
                        variant="primary"
                        size="sm"
                        className="w-full"
                        isLoading={confirmMutation.isPending}
                        onClick={() =>
                          confirmMutation.mutate({
                            id: details.repairRequestId,
                            rowVersionBase64: details.rowVersionBase64,
                          })
                        }
                      >
                        <CheckCircle2 className="w-3.5 h-3.5 mr-1.5" />
                        Xác nhận nghiệm thu
                      </Button>
                    </div>

                    {/* Redo Box */}
                    <div className="rounded-xl bg-white p-4 border border-[#E2E8F0] space-y-3 flex flex-col justify-between">
                      <div className="space-y-2">
                        <p className="text-xs font-bold text-[#B91C1C]">
                          2. Chưa đạt — Yêu cầu kỹ thuật viên xử lý lại
                        </p>
                        <textarea
                          rows={3}
                          value={redoReason}
                          onChange={(e) => setRedoReason(e.target.value)}
                          placeholder="Nhập lý do chưa đạt để Ban Quản lý phân công xử lý lại..."
                          className="w-full rounded-lg border border-[#CBD5E1] px-3 py-2 text-xs"
                        />
                      </div>
                      <Button
                        variant="danger"
                        size="sm"
                        className="w-full"
                        disabled={!redoReason.trim()}
                        isLoading={redoMutation.isPending}
                        onClick={() =>
                          redoMutation.mutate({
                            id: details.repairRequestId,
                            rowVersionBase64: details.rowVersionBase64,
                          })
                        }
                      >
                        <RotateCcw className="w-3.5 h-3.5 mr-1.5" />
                        Gửi yêu cầu làm lại
                      </Button>
                    </div>
                  </div>
                </div>
              )}

              {/* Cancel Box when Pending / Assigned */}
              {(details.status === 'Pending' || details.status === 'Assigned') && (
                <div className="rounded-xl border border-[#E2E8F0] p-4 flex flex-col sm:flex-row sm:items-center gap-3">
                  <input
                    type="text"
                    value={cancelReason}
                    onChange={(e) => setCancelReason(e.target.value)}
                    placeholder="Lý do hủy yêu cầu (tùy chọn)..."
                    className="flex-1 rounded-lg border border-[#CBD5E1] px-3 py-1.5 text-xs"
                  />
                  <Button
                    variant="danger"
                    size="sm"
                    isLoading={cancelMutation.isPending}
                    onClick={() =>
                      cancelMutation.mutate({
                        id: details.repairRequestId,
                        rowVersionBase64: details.rowVersionBase64,
                      })
                    }
                  >
                    <XCircle className="w-3.5 h-3.5 mr-1" />
                    Hủy yêu cầu sửa chữa
                  </Button>
                </div>
              )}

              {/* Technician Assignments & Status History */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
                <div className="rounded-xl border border-[#E2E8F0] p-4">
                  <h4 className="text-xs font-bold uppercase text-[#64748B] mb-2.5 flex items-center gap-1.5">
                    <UserCog className="w-3.5 h-3.5 text-[#2563EB]" />
                    <span>Kỹ thuật viên phụ trách ({details.assignments.length})</span>
                  </h4>
                  {details.assignments.length === 0 ? (
                    <p className="text-xs text-[#64748B]">
                      Đang chờ Ban Quản lý phân công kỹ thuật viên.
                    </p>
                  ) : (
                    <div className="space-y-2 text-xs">
                      {details.assignments.map((a) => (
                        <div
                          key={a.assignmentId}
                          className="rounded-lg bg-[#F8FAFC] p-2.5 border border-[#E2E8F0]"
                        >
                          <div className="flex items-center justify-between font-semibold text-[#0F172A]">
                            <span>{a.technicianName}</span>
                            <Badge variant="info">{a.status}</Badge>
                          </div>
                          <p className="text-[#64748B] mt-1">
                            Giao lúc: {formatDateTimeVi(a.assignedAt)}
                          </p>
                          {a.note && <p className="text-[#475569] mt-1">{a.note}</p>}
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="rounded-xl border border-[#E2E8F0] p-4">
                  <h4 className="text-xs font-bold uppercase text-[#64748B] mb-2.5 flex items-center gap-1.5">
                    <History className="w-3.5 h-3.5 text-[#2563EB]" />
                    <span>Lịch sử tiến độ ({details.statusHistory.length})</span>
                  </h4>
                  <div className="space-y-2 text-xs max-h-48 overflow-y-auto">
                    {details.statusHistory.map((h) => (
                      <div
                        key={h.historyId}
                        className="rounded-lg bg-[#F8FAFC] p-2.5 border border-[#E2E8F0]"
                      >
                        <div className="flex items-center justify-between">
                          <span className="font-semibold text-[#0F172A]">
                            <Clock className="w-3 h-3 inline mr-1 text-[#2563EB]" />
                            {h.previousStatus ? `${h.previousStatus} → ` : ''}
                            {h.newStatus}
                          </span>
                          <span className="text-[11px] text-[#64748B]">
                            {formatDateTimeVi(h.changedAt)}
                          </span>
                        </div>
                        {h.note && <p className="text-[#475569] mt-1">{h.note}</p>}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}

