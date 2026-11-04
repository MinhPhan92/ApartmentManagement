import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Edit3,
  MessageSquareWarning,
  PlusCircle,
  Star,
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
import type { ResidentComplaint } from '../../types/resident';

const CATEGORIES = [
  'An ninh',
  'Vệ sinh',
  'Tiếng ồn',
  'Tiện ích / Thiết bị',
  'Dịch vụ khác',
];

const PRIORITIES = ['Thấp', 'Bình thường', 'Cao', 'Khẩn cấp'];

export function ResidentComplaintsPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();

  const [editingId, setEditingId] = useState<number | null>(null);
  const [title, setTitle] = useState<string>('');
  const [category, setCategory] = useState<string>('Vệ sinh');
  const [priority, setPriority] = useState<string>('Bình thường');
  const [apartmentId, setApartmentId] = useState<number | ''>('');
  const [content, setContent] = useState<string>('');

  const [ratingTargetId, setRatingTargetId] = useState<number | null>(null);
  const [ratingScore, setRatingScore] = useState<number>(5);
  const [ratingFeedback, setRatingFeedback] = useState<string>('');

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'complaints'],
    queryFn: () => residentApi.getComplaints(),
  });

  const saveMutation = useMutation({
    mutationFn: () => {
      const aptValue = apartmentId === '' ? null : Number(apartmentId);
      if (editingId) {
        return residentApi.updateComplaint(editingId, {
          complaintId: editingId,
          title: title.trim(),
          category,
          priority,
          apartmentId: aptValue,
          content: content.trim(),
        });
      }
      return residentApi.createComplaint({
        title: title.trim(),
        category,
        priority,
        apartmentId: aptValue,
        content: content.trim(),
      });
    },
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: editingId ? 'Đã cập nhật phản ánh' : 'Đã gửi phản ánh thành công',
        message: res.message,
      });
      resetForm();
      void queryClient.invalidateQueries({ queryKey: ['resident', 'complaints'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể lưu phản ánh',
        message: err instanceof Error ? err.message : 'Vui lòng kiểm tra lại dữ liệu.',
      });
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (id: number) => residentApi.cancelComplaint(id),
    onSuccess: (res) => {
      pushToast({
        type: 'info',
        title: 'Đã rút phản ánh',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'complaints'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể rút phản ánh',
        message: err instanceof Error ? err.message : 'Thao tác không thành công.',
      });
    },
  });

  const rateMutation = useMutation({
    mutationFn: (id: number) =>
      residentApi.rateComplaint(id, {
        rating: ratingScore,
        feedback: ratingFeedback.trim() || undefined,
      }),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã gửi đánh giá phản ánh',
        message: res.message,
      });
      setRatingTargetId(null);
      setRatingFeedback('');
      void queryClient.invalidateQueries({ queryKey: ['resident', 'complaints'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể đánh giá',
        message: err instanceof Error ? err.message : 'Thao tác không thành công.',
      });
    },
  });

  const resetForm = () => {
    setEditingId(null);
    setTitle('');
    setCategory('Vệ sinh');
    setPriority('Bình thường');
    setApartmentId('');
    setContent('');
  };

  const startEdit = (item: ResidentComplaint) => {
    setEditingId(item.complaintId);
    setTitle(item.title);
    setCategory(item.category);
    setPriority(item.priority);
    setApartmentId(item.apartmentId ?? '');
    setContent(item.content);
  };

  if (isLoading) {
    return <LoadingState label="Đang tải danh sách phản ánh và góp ý..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải dữ liệu phản ánh"
        description={error instanceof Error ? error.message : 'Vui lòng thử lại.'}
        onRetry={() => void refetch()}
      />
    );
  }

  const { complaints, apartments } = data;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !content.trim()) return;
    saveMutation.mutate();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Phản ánh & Góp ý dịch vụ Ban Quản lý
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Gửi phản ánh, góp ý về an ninh, vệ sinh, tiếng ồn, tiện ích nội khu trực tiếp tới Ban Quản lý và đánh giá chất lượng phản hồi.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Create / Edit Form */}
        <Card className="p-5 lg:col-span-4 h-fit">
          <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3 mb-4">
            <div className="flex items-center gap-2">
              <PlusCircle className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                {editingId ? `Chỉnh sửa phản ánh #${editingId}` : 'Gửi phản ánh mới'}
              </h2>
            </div>
            {editingId && (
              <button
                type="button"
                onClick={resetForm}
                className="text-xs font-medium text-[#64748B] hover:text-[#0F172A]"
              >
                Hủy sửa
              </button>
            )}
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <Input
              label="Tiêu đề phản ánh"
              placeholder="Ví dụ: Đèn hành lang tầng 8 bị nhấp nháy"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
            />

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Danh mục
                </label>
                <select
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A]"
                >
                  {CATEGORIES.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                  Mức độ ưu tiên
                </label>
                <select
                  value={priority}
                  onChange={(e) => setPriority(e.target.value)}
                  className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A]"
                >
                  {PRIORITIES.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                Căn hộ liên quan (Tùy chọn)
              </label>
              <select
                value={apartmentId}
                onChange={(e) =>
                  setApartmentId(e.target.value ? Number(e.target.value) : '')
                }
                className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-2 text-sm text-[#0F172A]"
              >
                <option value="">Khu vực chung / Toàn tòa nhà</option>
                {apartments.map((apt) => (
                  <option key={apt.apartmentId} value={apt.apartmentId}>
                    {apt.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-[#0F172A] mb-1.5">
                Nội dung chi tiết
              </label>
              <textarea
                rows={4}
                value={content}
                onChange={(e) => setContent(e.target.value)}
                placeholder="Mô tả chi tiết sự việc, thời điểm và vị trí để Ban Quản lý kiểm tra xử lý..."
                required
                className="w-full rounded-lg border border-[#CBD5E1] px-3 py-2 text-sm text-[#0F172A] focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
              />
            </div>

            <Button
              type="submit"
              variant="primary"
              className="w-full"
              isLoading={saveMutation.isPending}
            >
              <MessageSquareWarning className="w-4 h-4 mr-1.5" />
              {editingId ? 'Lưu cập nhật phản ánh' : 'Gửi phản ánh tới Ban Quản lý'}
            </Button>
          </form>
        </Card>

        {/* Complaints List */}
        <Card className="p-5 lg:col-span-8 space-y-4">
          <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3">
            <div className="flex items-center gap-2">
              <MessageSquareWarning className="w-4 h-4 text-[#2563EB]" />
              <h2 className="text-base font-bold text-[#0F172A]">
                Lịch sử phản ánh của tôi ({complaints.length})
              </h2>
            </div>
          </div>

          {complaints.length === 0 ? (
            <EmptyState
              title="Chưa có phản ánh nào"
              description="Mọi ý kiến đóng góp và phản ánh của bạn với Ban Quản lý sẽ được lưu trữ và theo dõi phản hồi tại đây."
            />
          ) : (
            <div className="space-y-4">
              {complaints.map((item) => (
                <div
                  key={item.complaintId}
                  className="rounded-xl border border-[#E2E8F0] p-4 space-y-3"
                >
                  <div className="flex flex-wrap items-start justify-between gap-2">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-xs font-bold text-[#2563EB]">
                          #{item.complaintId}
                        </span>
                        <h3 className="text-sm font-bold text-[#0F172A]">
                          {item.title}
                        </h3>
                        <Badge variant="neutral">{item.category}</Badge>
                        <Badge
                          variant={
                            item.status === 'Đã xử lý'
                              ? 'success'
                              : item.status === 'Đang xử lý'
                              ? 'info'
                              : item.status === 'Từ chối' || item.status === 'Đã hủy'
                              ? 'danger'
                              : 'warning'
                          }
                        >
                          {item.status}
                        </Badge>
                      </div>
                      <p className="text-xs text-[#64748B] mt-1">
                        Mức độ: {item.priority}
                        {item.apartmentCode ? ` • Căn hộ ${item.apartmentCode}` : ''} •
                        Ngày gửi: {formatDateTimeVi(item.createdAt)}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      {item.canEdit && (
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => startEdit(item)}
                        >
                          <Edit3 className="w-3.5 h-3.5 mr-1" />
                          Sửa
                        </Button>
                      )}
                      {item.canCancel && (
                        <Button
                          variant="danger"
                          size="sm"
                          isLoading={cancelMutation.isPending}
                          onClick={() => cancelMutation.mutate(item.complaintId)}
                        >
                          <XCircle className="w-3.5 h-3.5 mr-1" />
                          Rút phản ánh
                        </Button>
                      )}
                    </div>
                  </div>

                  <p className="text-xs text-[#334155] whitespace-pre-line bg-[#F8FAFC] p-3 rounded-lg border border-[#E2E8F0]">
                    {item.content}
                  </p>

                  {item.responseNotes && (
                    <div className="rounded-lg bg-emerald-50/70 border border-emerald-200 p-3 text-xs">
                      <p className="font-bold text-emerald-900">
                        Phản hồi từ Ban Quản lý:
                      </p>
                      <p className="text-emerald-950 whitespace-pre-line mt-1">
                        {item.responseNotes}
                      </p>
                    </div>
                  )}

                  {item.rating && (
                    <div className="flex items-center gap-2 text-xs text-amber-700 font-medium">
                      <Star className="w-4 h-4 fill-amber-400 text-amber-500" />
                      <span>
                        Đánh giá của bạn: <strong>{item.rating}/5 sao</strong>
                        {item.residentFeedback ? ` — "${item.residentFeedback}"` : ''}
                      </span>
                    </div>
                  )}

                  {item.canRate && (
                    <div className="pt-2 border-t border-[#E2E8F0]">
                      {ratingTargetId === item.complaintId ? (
                        <div className="space-y-2.5 bg-amber-50/40 p-3 rounded-xl border border-amber-200">
                          <div className="flex items-center justify-between">
                            <span className="text-xs font-bold text-[#0F172A]">
                              Đánh giá kết quả xử lý phản ánh:
                            </span>
                            <div className="flex items-center gap-1">
                              {[1, 2, 3, 4, 5].map((star) => (
                                <button
                                  key={star}
                                  type="button"
                                  onClick={() => setRatingScore(star)}
                                  className={`p-1 rounded ${
                                    ratingScore >= star
                                      ? 'text-amber-500'
                                      : 'text-slate-300'
                                  }`}
                                >
                                  <Star className="w-4 h-4 fill-current" />
                                </button>
                              ))}
                            </div>
                          </div>
                          <input
                            type="text"
                            value={ratingFeedback}
                            onChange={(e) => setRatingFeedback(e.target.value)}
                            placeholder="Nhận xét thêm về kết quả giải quyết (tùy chọn)..."
                            className="w-full rounded-lg border border-[#CBD5E1] bg-white px-3 py-1.5 text-xs"
                          />
                          <div className="flex justify-end gap-2">
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() => setRatingTargetId(null)}
                            >
                              Đóng
                            </Button>
                            <Button
                              variant="primary"
                              size="sm"
                              isLoading={rateMutation.isPending}
                              onClick={() => rateMutation.mutate(item.complaintId)}
                            >
                              Gửi đánh giá
                            </Button>
                          </div>
                        </div>
                      ) : (
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => {
                            setRatingTargetId(item.complaintId);
                            setRatingScore(5);
                          }}
                        >
                          <Star className="w-3.5 h-3.5 mr-1 text-amber-500" />
                          Đánh giá chất lượng xử lý
                        </Button>
                      )}
                    </div>
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

