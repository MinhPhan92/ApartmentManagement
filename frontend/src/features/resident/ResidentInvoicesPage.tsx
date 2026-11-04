import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Clock,
  CreditCard,
  FileText,
  QrCode,
  Receipt,
  RefreshCw,
  XCircle,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { resolveApiUrl } from '../../lib/api/client';
import { formatDateTimeVi, formatDateVi, formatVnd } from '../../lib/formatters';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';
import { useToast } from '../../components/ui/Toast';

function getPaymentBadge(state: string) {
  switch (state) {
    case 'Paid':
      return <Badge variant="success">Đã thanh toán</Badge>;
    case 'Partial':
      return <Badge variant="info">Thanh toán một phần</Badge>;
    case 'Overdue':
      return <Badge variant="danger">Quá hạn</Badge>;
    case 'Void':
      return <Badge variant="neutral">Đã hủy</Badge>;
    default:
      return <Badge variant="warning">Chưa thanh toán</Badge>;
  }
}

export function ResidentInvoicesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [filter, setFilter] = useState<'all' | 'unpaid' | 'paid'>('all');

  const initialInvoiceId = Number(searchParams.get('invoiceId') || 0);
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<number | null>(
    initialInvoiceId > 0 ? initialInvoiceId : null
  );

  const listQuery = useQuery({
    queryKey: ['resident', 'invoices'],
    queryFn: () => residentApi.getInvoices(),
  });

  useEffect(() => {
    if (!selectedInvoiceId && listQuery.data && listQuery.data.items.length > 0) {
      setSelectedInvoiceId(listQuery.data.items[0].apartmentInvoiceId);
    }
  }, [listQuery.data, selectedInvoiceId]);

  const detailsQuery = useQuery({
    queryKey: ['resident', 'invoiceDetails', selectedInvoiceId],
    queryFn: () => residentApi.getInvoiceDetails(selectedInvoiceId!),
    enabled: Boolean(selectedInvoiceId && selectedInvoiceId > 0),
  });

  const createPaymentMutation = useMutation({
    mutationFn: (invoiceId: number) => residentApi.createPaymentAttempt(invoiceId),
    onSuccess: (res) => {
      pushToast({
        type: 'success',
        title: 'Đã khởi tạo mã VietQR',
        message: res.message,
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'invoices'] });
      void queryClient.invalidateQueries({
        queryKey: ['resident', 'invoiceDetails', selectedInvoiceId],
      });
      void queryClient.invalidateQueries({ queryKey: ['resident', 'dashboard'] });
    },
    onError: (err) => {
      pushToast({
        type: 'error',
        title: 'Không thể tạo lượt thanh toán',
        message: err instanceof Error ? err.message : 'Vui lòng thử lại sau.',
      });
    },
  });

  if (listQuery.isLoading) {
    return <LoadingState label="Đang tải danh sách hóa đơn dịch vụ..." />;
  }

  if (listQuery.isError || !listQuery.data) {
    return (
      <ErrorState
        title="Không thể tải hóa đơn"
        description={
          listQuery.error instanceof Error
            ? listQuery.error.message
            : 'Đã xảy ra lỗi khi tải hóa đơn.'
        }
        onRetry={() => void listQuery.refetch()}
      />
    );
  }

  const data = listQuery.data;
  const filteredInvoices = data.items.filter((inv) => {
    if (filter === 'unpaid') return inv.remainingAmount > 0;
    if (filter === 'paid') return inv.paymentState === 'Paid';
    return true;
  });

  const details = detailsQuery.data;

  const handleSelectInvoice = (id: number) => {
    setSelectedInvoiceId(id);
    setSearchParams({ invoiceId: String(id) }, { replace: true });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">
          Thanh toán phí dịch vụ & Hóa đơn
        </h1>
        <p className="text-sm text-[#475569] mt-1">
          Tra cứu chi tiết tiền điện, nước, phí quản lý định kỳ, phí gửi xe và thanh toán trực tuyến qua mã VietQR.
        </p>
      </div>

      {/* Summary KPIs */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Tổng phí đã phát hành ({data.totalCount} kỳ)
          </p>
          <p className="text-xl font-bold text-[#0F172A] mt-1 tabular-nums">
            {formatVnd(data.totalBilledAmount)}
          </p>
        </Card>
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Tổng đã xác nhận thanh toán
          </p>
          <p className="text-xl font-bold text-[#15803D] mt-1 tabular-nums">
            {formatVnd(data.totalConfirmedAmount)}
          </p>
        </Card>
        <Card className="p-4">
          <p className="text-xs font-semibold uppercase text-[#64748B]">
            Tổng dư nợ cần thanh toán
          </p>
          <p className="text-xl font-bold text-[#B91C1C] mt-1 tabular-nums">
            {formatVnd(data.totalRemainingAmount)}
          </p>
        </Card>
      </div>

      {data.items.length === 0 ? (
        <Card className="p-6">
          <EmptyState
            title="Chưa có hóa đơn dịch vụ"
            description="Khi Ban Quản lý phát hành hóa đơn định kỳ cho căn hộ của bạn, bạn có thể tra cứu và thanh toán trực tuyến tại đây."
          />
        </Card>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column: Invoice List */}
          <Card className="p-4 lg:col-span-5 space-y-4">
            <div className="flex items-center justify-between gap-2 border-b border-[#E2E8F0] pb-3">
              <div className="flex items-center gap-2">
                <Receipt className="w-4 h-4 text-[#2563EB]" />
                <h2 className="text-sm font-bold text-[#0F172A]">Kỳ hóa đơn</h2>
              </div>
              <div className="flex items-center gap-1 rounded-lg bg-[#F1F5F9] p-1 text-xs">
                <button
                  type="button"
                  onClick={() => setFilter('all')}
                  className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
                    filter === 'all'
                      ? 'bg-white text-[#0F172A] shadow-2xs'
                      : 'text-[#475569]'
                  }`}
                >
                  Tất cả
                </button>
                <button
                  type="button"
                  onClick={() => setFilter('unpaid')}
                  className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
                    filter === 'unpaid'
                      ? 'bg-white text-[#0F172A] shadow-2xs'
                      : 'text-[#475569]'
                  }`}
                >
                  Còn nợ
                </button>
                <button
                  type="button"
                  onClick={() => setFilter('paid')}
                  className={`px-2.5 py-1 rounded-md font-medium transition-colors ${
                    filter === 'paid'
                      ? 'bg-white text-[#0F172A] shadow-2xs'
                      : 'text-[#475569]'
                  }`}
                >
                  Đã trả
                </button>
              </div>
            </div>

            {filteredInvoices.length === 0 ? (
              <EmptyState
                title="Không có hóa đơn phù hợp"
                description="Không có hóa đơn nào khớp với bộ lọc hiện tại."
              />
            ) : (
              <div className="space-y-2.5 max-h-[640px] overflow-y-auto pr-1">
                {filteredInvoices.map((inv) => {
                  const active = inv.apartmentInvoiceId === selectedInvoiceId;
                  return (
                    <button
                      key={inv.apartmentInvoiceId}
                      type="button"
                      onClick={() => handleSelectInvoice(inv.apartmentInvoiceId)}
                      className={`w-full text-left rounded-xl border p-3.5 transition-all ${
                        active
                          ? 'border-[#2563EB] bg-blue-50/50 shadow-2xs'
                          : 'border-[#E2E8F0] bg-white hover:border-slate-300'
                      }`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-sm font-bold text-[#0F172A]">
                          {inv.periodLabel}
                        </span>
                        {getPaymentBadge(inv.paymentState)}
                      </div>
                      <p className="text-xs text-[#64748B] font-mono mt-1">
                        {inv.invoiceCode}
                      </p>
                      <div className="mt-2.5 pt-2.5 border-t border-[#E2E8F0] flex items-center justify-between text-xs">
                        <span className="text-[#475569]">
                          Hạn: {formatDateVi(inv.dueDate)}
                        </span>
                        <span className="font-bold text-[#0F172A] tabular-nums">
                          Còn nợ: {formatVnd(inv.remainingAmount)}
                        </span>
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </Card>

          {/* Right Column: Selected Invoice Details & VietQR */}
          <Card className="p-6 lg:col-span-7">
            {detailsQuery.isLoading ? (
              <LoadingState label="Đang tải chi tiết hóa đơn..." />
            ) : detailsQuery.isError || !details ? (
              <ErrorState
                title="Không thể tải chi tiết hóa đơn"
                description="Vui lòng chọn lại hóa đơn hoặc thử tải lại."
                onRetry={() => void detailsQuery.refetch()}
              />
            ) : (
              <div className="space-y-6">
                {/* Header */}
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-[#E2E8F0] pb-4">
                  <div>
                    <div className="flex items-center gap-2.5">
                      <h2 className="text-lg font-bold text-[#0F172A]">
                        Hóa đơn {details.summary.periodLabel}
                      </h2>
                      {getPaymentBadge(details.summary.paymentState)}
                    </div>
                    <p className="text-xs text-[#475569] mt-1">
                      Mã: <span className="font-mono">{details.summary.invoiceCode}</span> • Căn hộ{' '}
                      <strong>{details.summary.apartmentCode}</strong> ({details.apartmentArea} m²) •
                      Hạn thanh toán: <strong>{formatDateVi(details.summary.dueDate)}</strong>
                    </p>
                  </div>

                  {details.vietQr.canCreatePaymentAttempt && (
                    <Button
                      variant="primary"
                      size="sm"
                      isLoading={createPaymentMutation.isPending}
                      onClick={() =>
                        createPaymentMutation.mutate(details.summary.apartmentInvoiceId)
                      }
                    >
                      <QrCode className="w-4 h-4 mr-1.5" />
                      {details.activePayment?.status === 'Pending'
                        ? 'Tạo lại mã VietQR'
                        : 'Tạo mã thanh toán VietQR'}
                    </Button>
                  )}
                </div>

                {/* Line Items Table */}
                <div>
                  <h3 className="text-sm font-bold text-[#0F172A] mb-3 flex items-center gap-2">
                    <FileText className="w-4 h-4 text-[#2563EB]" />
                    <span>Chi tiết các khoản phí dịch vụ</span>
                  </h3>
                  <div className="overflow-x-auto rounded-xl border border-[#E2E8F0]">
                    <table className="w-full text-sm text-left">
                      <thead className="bg-[#F8FAFC] text-xs uppercase text-[#64748B] border-b border-[#E2E8F0]">
                        <tr>
                          <th className="py-3 px-4">Khoản phí</th>
                          <th className="py-3 px-3 text-right">Chỉ số / SL</th>
                          <th className="py-3 px-3 text-right">Đơn giá TB</th>
                          <th className="py-3 px-4 text-right">Thành tiền</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[#E2E8F0]">
                        {details.lines.map((line) => (
                          <tr key={line.lineId}>
                            <td className="py-3 px-4">
                              <p className="font-semibold text-[#0F172A]">
                                {line.chargeTypeLabel}
                              </p>
                              <p className="text-xs text-[#475569] whitespace-pre-line mt-0.5">
                                {line.description}
                              </p>
                              {line.rateBreakdown && (
                                <p className="text-[11px] text-[#64748B] mt-1">
                                  Diễn giải: {line.rateBreakdown}
                                </p>
                              )}
                            </td>
                            <td className="py-3 px-3 text-right tabular-nums text-xs text-[#475569]">
                              <div>{line.quantity}</div>
                              {line.previousReading !== null &&
                                line.currentReading !== null && (
                                  <div className="text-[11px] text-[#64748B]">
                                    ({line.previousReading} → {line.currentReading})
                                  </div>
                                )}
                            </td>
                            <td className="py-3 px-3 text-right tabular-nums text-xs text-[#475569]">
                              {formatVnd(line.unitRate)}
                            </td>
                            <td className="py-3 px-4 text-right font-bold text-[#0F172A] tabular-nums">
                              {formatVnd(line.amount)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                      <tfoot className="bg-[#F8FAFC] border-t border-[#E2E8F0] text-xs">
                        <tr>
                          <td colSpan={3} className="py-2.5 px-4 text-right font-semibold text-[#475569]">
                            Tổng cộng hóa đơn:
                          </td>
                          <td className="py-2.5 px-4 text-right text-sm font-bold text-[#0F172A] tabular-nums">
                            {formatVnd(details.summary.totalAmount)}
                          </td>
                        </tr>
                        <tr>
                          <td colSpan={3} className="py-2 px-4 text-right font-semibold text-[#15803D]">
                            Đã thanh toán (Đã xác nhận):
                          </td>
                          <td className="py-2 px-4 text-right text-sm font-bold text-[#15803D] tabular-nums">
                            {formatVnd(details.summary.confirmedAmount)}
                          </td>
                        </tr>
                        <tr>
                          <td colSpan={3} className="py-2.5 px-4 text-right font-bold text-[#B91C1C]">
                            Số tiền còn phải thanh toán:
                          </td>
                          <td className="py-2.5 px-4 text-right text-base font-bold text-[#B91C1C] tabular-nums">
                            {formatVnd(details.summary.remainingAmount)}
                          </td>
                        </tr>
                      </tfoot>
                    </table>
                  </div>
                </div>

                {/* Active Pending VietQR Box */}
                {details.activePayment &&
                  details.activePayment.status === 'Pending' &&
                  details.activePayment.qrImageUrl && (
                    <div className="rounded-2xl border border-[#2563EB]/30 bg-blue-50/40 p-5">
                      <div className="flex flex-col md:flex-row items-center gap-5">
                        <div className="bg-white p-3 rounded-xl border border-[#E2E8F0] shadow-xs shrink-0">
                          <img
                            src={resolveApiUrl(details.activePayment.qrImageUrl)}
                            alt="Mã thanh toán VietQR"
                            className="w-44 h-44 object-contain"
                          />
                        </div>
                        <div className="space-y-2 text-xs flex-1">
                          <div className="inline-flex items-center gap-1.5 text-[#2563EB] font-bold text-sm">
                            <QrCode className="w-4 h-4" />
                            <span>Quét mã VietQR để chuyển khoản tự động</span>
                          </div>
                          <p className="text-[#475569]">
                            Sử dụng ứng dụng Ngân hàng (Mobile Banking) quét mã QR bên cạnh để điền sẵn chính xác số tiền và nội dung chuyển khoản:
                          </p>
                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1">
                            <div className="rounded-lg bg-white p-2.5 border border-[#E2E8F0]">
                              <span className="text-[#64748B]">Chủ tài khoản:</span>
                              <p className="font-bold text-[#0F172A] mt-0.5">
                                {details.vietQr.accountName}
                              </p>
                            </div>
                            <div className="rounded-lg bg-white p-2.5 border border-[#E2E8F0]">
                              <span className="text-[#64748B]">Số tài khoản (BIN {details.vietQr.bankBin}):</span>
                              <p className="font-mono font-bold text-[#0F172A] mt-0.5">
                                {details.vietQr.accountNumber}
                              </p>
                            </div>
                            <div className="rounded-lg bg-white p-2.5 border border-[#E2E8F0]">
                              <span className="text-[#64748B]">Số tiền chuyển khoản:</span>
                              <p className="font-bold text-[#2563EB] mt-0.5 tabular-nums">
                                {formatVnd(details.activePayment.amount)}
                              </p>
                            </div>
                            <div className="rounded-lg bg-white p-2.5 border border-[#E2E8F0]">
                              <span className="text-[#64748B]">Nội dung chuyển khoản (Bắt buộc):</span>
                              <p className="font-mono font-bold text-[#B91C1C] mt-0.5">
                                {details.activePayment.reference}
                              </p>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}

                {/* Payment Transactions History */}
                <div>
                  <div className="flex items-center justify-between mb-3">
                    <h3 className="text-sm font-bold text-[#0F172A] flex items-center gap-2">
                      <CreditCard className="w-4 h-4 text-[#2563EB]" />
                      <span>
                        Lịch sử giao dịch thanh toán ({details.transactions.length})
                      </span>
                    </h3>
                    <button
                      type="button"
                      onClick={() => void detailsQuery.refetch()}
                      className="inline-flex items-center gap-1 text-xs font-medium text-[#2563EB] hover:underline"
                    >
                      <RefreshCw className="w-3.5 h-3.5" />
                      <span>Làm mới</span>
                    </button>
                  </div>

                  {details.transactions.length === 0 ? (
                    <p className="text-xs text-[#64748B] bg-[#F8FAFC] rounded-xl p-4 border border-[#E2E8F0]">
                      Chưa có lượt giao dịch thanh toán nào cho hóa đơn này. Nhấn &ldquo;Tạo mã thanh toán VietQR&rdquo; để bắt đầu thanh toán.
                    </p>
                  ) : (
                    <div className="space-y-2.5">
                      {details.transactions.map((tx) => (
                        <div
                          key={tx.paymentTransactionId}
                          className="rounded-xl border border-[#E2E8F0] p-3.5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 text-xs"
                        >
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="font-mono font-bold text-[#0F172A]">
                                {tx.reference}
                              </span>
                              {tx.status === 'Confirmed' ? (
                                <Badge variant="success">
                                  <CheckCircle2 className="w-3 h-3 mr-1" />
                                  Đã xác nhận thu
                                </Badge>
                              ) : tx.status === 'Pending' ? (
                                <Badge variant="warning">
                                  <Clock className="w-3 h-3 mr-1" />
                                  Chờ đối soát
                                </Badge>
                              ) : (
                                <Badge variant="danger">
                                  <XCircle className="w-3 h-3 mr-1" />
                                  {tx.isAutoClosedRejection ? 'Tự động đóng' : 'Từ chối'}
                                </Badge>
                              )}
                            </div>
                            <p className="text-[#64748B] mt-1">
                              Tạo lúc: {formatDateTimeVi(tx.createdAtUtc)}
                              {tx.confirmedAtUtc
                                ? ` • Xác nhận: ${formatDateTimeVi(tx.confirmedAtUtc)}`
                                : ''}
                            </p>
                            {tx.rejectionReason && (
                              <p className="text-[#B91C1C] mt-1">
                                Lý do: {tx.rejectionReason}
                              </p>
                            )}
                          </div>
                          <div className="text-right font-bold text-sm text-[#0F172A] tabular-nums">
                            {formatVnd(tx.amount)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}
          </Card>
        </div>
      )}
    </div>
  );
}

