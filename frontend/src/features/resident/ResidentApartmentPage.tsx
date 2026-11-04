import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Building2,
  Calendar,
  FileText,
  Home,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { residentApi } from '../../lib/resident/residentApi';
import { formatDateTimeVi, formatDateVi, formatVnd } from '../../lib/formatters';
import { Badge } from '../../components/ui/Badge';
import { Card } from '../../components/ui/Card';
import { EmptyState, ErrorState, LoadingState } from '../../components/ui/States';
import type { ResidentContractItem } from '../../types/resident';

export function ResidentApartmentPage() {
  const [selectedContract, setSelectedContract] = useState<ResidentContractItem | null>(null);

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['resident', 'apartment'],
    queryFn: () => residentApi.getApartmentAndContracts(),
  });

  if (isLoading) {
    return <LoadingState label="Đang tải thông tin căn hộ và hợp đồng..." />;
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Không thể tải dữ liệu căn hộ"
        description={error instanceof Error ? error.message : 'Vui lòng thử lại sau.'}
        onRetry={() => void refetch()}
      />
    );
  }

  const apt = data.activeApartment;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-[#0F172A]">Căn hộ của tôi & Hợp đồng</h1>
        <p className="text-sm text-[#475569] mt-1">
          Thông tin căn hộ đang cư trú, danh sách thành viên hộ gia đình và hợp đồng căn hộ liên quan.
        </p>
      </div>

      {/* Active Apartment Details */}
      {apt ? (
        <Card className="p-6">
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-[#E2E8F0] pb-4 mb-5">
            <div className="flex items-center gap-3.5">
              <div className="w-12 h-12 rounded-xl bg-[#172554] text-white flex items-center justify-center">
                <Home className="w-6 h-6" />
              </div>
              <div>
                <div className="flex items-center gap-2.5">
                  <h2 className="text-lg font-bold text-[#0F172A]">
                    Căn hộ {apt.apartmentCode} — {apt.buildingName}
                  </h2>
                  <Badge variant="success">{apt.status}</Badge>
                </div>
                <p className="text-xs text-[#475569] mt-0.5">
                  {apt.buildingAddress || 'Khu đô thị Vinhomes'}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Badge variant={apt.isOwner ? 'info' : 'neutral'}>
                {apt.isOwner ? 'Chủ hộ' : apt.relationship}
              </Badge>
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <div className="rounded-xl bg-[#F8FAFC] p-3.5 border border-[#E2E8F0]">
              <p className="text-xs text-[#64748B]">Tòa nhà</p>
              <p className="text-sm font-bold text-[#0F172A] mt-1">
                {apt.buildingName} ({apt.buildingCode})
              </p>
            </div>
            <div className="rounded-xl bg-[#F8FAFC] p-3.5 border border-[#E2E8F0]">
              <p className="text-xs text-[#64748B]">Tầng</p>
              <p className="text-sm font-bold text-[#0F172A] mt-1">Tầng {apt.floor}</p>
            </div>
            <div className="rounded-xl bg-[#F8FAFC] p-3.5 border border-[#E2E8F0]">
              <p className="text-xs text-[#64748B]">Diện tích thông thủy</p>
              <p className="text-sm font-bold text-[#0F172A] mt-1">{apt.area} m²</p>
            </div>
            <div className="rounded-xl bg-[#F8FAFC] p-3.5 border border-[#E2E8F0]">
              <p className="text-xs text-[#64748B]">Ngày bắt đầu cư trú</p>
              <p className="text-sm font-bold text-[#0F172A] mt-1">
                {formatDateVi(apt.moveInDate)}
              </p>
            </div>
          </div>
        </Card>
      ) : (
        <Card className="p-6">
          <EmptyState
            title="Chưa có thông tin căn hộ đang cư trú"
            description="Tài khoản của bạn hiện chưa được gán vào căn hộ đang hoạt động. Vui lòng liên hệ Ban Quản lý để cập nhật hồ sơ cư trú."
          />
        </Card>
      )}

      {/* Household Members */}
      <Card className="p-6">
        <div className="flex items-center gap-2 border-b border-[#E2E8F0] pb-3.5 mb-4">
          <Users className="w-5 h-5 text-[#2563EB]" />
          <h2 className="text-base font-bold text-[#0F172A]">
            Thành viên hộ gia đình đang cư trú ({data.householdMembers.length})
          </h2>
        </div>

        {data.householdMembers.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-[#E2E8F0] text-xs uppercase text-[#64748B]">
                  <th className="py-3 pr-4">Họ và tên</th>
                  <th className="py-3 px-4">Quan hệ với chủ hộ</th>
                  <th className="py-3 px-4">Vai trò</th>
                  <th className="py-3 px-4">Email</th>
                  <th className="py-3 pl-4">Ngày chuyển vào</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E2E8F0]">
                {data.householdMembers.map((member) => (
                  <tr key={member.apartmentResidentId}>
                    <td className="py-3 pr-4 font-semibold text-[#0F172A]">
                      {member.fullName}
                    </td>
                    <td className="py-3 px-4 text-[#475569]">{member.relationship}</td>
                    <td className="py-3 px-4">
                      <Badge variant={member.isOwner ? 'info' : 'neutral'}>
                        {member.isOwner ? 'Chủ hộ' : 'Thành viên'}
                      </Badge>
                    </td>
                    <td className="py-3 px-4 text-[#475569]">{member.email || '—'}</td>
                    <td className="py-3 pl-4 text-[#475569]">
                      {formatDateVi(member.moveInDate)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            title="Chưa có dữ liệu thành viên hộ gia đình"
            description="Danh sách các thành viên đăng ký tạm trú/thường trú tại căn hộ sẽ hiển thị tại đây."
          />
        )}
      </Card>

      {/* Apartment Contracts */}
      <Card className="p-6">
        <div className="flex items-center justify-between border-b border-[#E2E8F0] pb-3.5 mb-4">
          <div className="flex items-center gap-2">
            <FileText className="w-5 h-5 text-[#2563EB]" />
            <h2 className="text-base font-bold text-[#0F172A]">
              Hợp đồng căn hộ của tôi ({data.contracts.length})
            </h2>
          </div>
        </div>

        {data.contracts.length > 0 ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {data.contracts.map((contract) => {
                const isSelected = selectedContract?.contractId === contract.contractId;
                return (
                  <div
                    key={contract.contractId}
                    className={`rounded-xl border p-4 transition-all ${
                      isSelected
                        ? 'border-[#2563EB] bg-blue-50/40'
                        : 'border-[#E2E8F0] bg-white hover:border-slate-300'
                    }`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-mono text-sm font-bold text-[#0F172A]">
                        {contract.contractCode}
                      </span>
                      <Badge
                        variant={
                          contract.status === 'Active'
                            ? 'success'
                            : contract.status === 'Draft'
                            ? 'warning'
                            : 'neutral'
                        }
                      >
                        {contract.status}
                      </Badge>
                    </div>
                    <p className="text-xs text-[#475569] mt-1 flex items-center gap-1.5">
                      <Building2 className="w-3.5 h-3.5 text-[#64748B]" />
                      <span>
                        {contract.buildingName} — Căn hộ {contract.apartmentCode}
                      </span>
                    </p>
                    <p className="text-xs text-[#475569] mt-1 flex items-center gap-1.5">
                      <Calendar className="w-3.5 h-3.5 text-[#64748B]" />
                      <span>
                        Hiệu lực: {formatDateVi(contract.startDate)} →{' '}
                        {formatDateVi(contract.endDate)}
                      </span>
                    </p>
                    <div className="mt-3 pt-3 border-t border-[#E2E8F0] flex items-center justify-between text-xs">
                      <div>
                        <span className="text-[#64748B]">Tiền thuê/tháng: </span>
                        <span className="font-semibold text-[#0F172A]">
                          {formatVnd(contract.monthlyRent)}
                        </span>
                      </div>
                      <button
                        type="button"
                        onClick={() =>
                          setSelectedContract(isSelected ? null : contract)
                        }
                        className="font-semibold text-[#2563EB] hover:underline"
                      >
                        {isSelected ? 'Ẩn chi tiết' : 'Xem chi tiết'}
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>

            {selectedContract && (
              <div className="rounded-xl border border-[#2563EB]/30 bg-[#F8FAFC] p-5 space-y-4">
                <div className="flex items-center justify-between">
                  <h3 className="text-sm font-bold text-[#0F172A]">
                    Chi tiết hợp đồng {selectedContract.contractCode}
                  </h3>
                  <button
                    type="button"
                    onClick={() => setSelectedContract(null)}
                    className="text-xs font-medium text-[#64748B] hover:text-[#0F172A]"
                  >
                    Đóng
                  </button>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
                  <div className="rounded-lg bg-white p-3 border border-[#E2E8F0]">
                    <span className="text-[#64748B]">Tiền thuê hàng tháng</span>
                    <p className="text-sm font-bold text-[#0F172A] mt-0.5">
                      {formatVnd(selectedContract.monthlyRent)}
                    </p>
                  </div>
                  <div className="rounded-lg bg-white p-3 border border-[#E2E8F0]">
                    <span className="text-[#64748B]">Tiền đặt cọc</span>
                    <p className="text-sm font-bold text-[#0F172A] mt-0.5">
                      {formatVnd(selectedContract.depositAmount)}
                    </p>
                  </div>
                  <div className="rounded-lg bg-white p-3 border border-[#E2E8F0]">
                    <span className="text-[#64748B]">Các bên tham gia</span>
                    <p className="text-sm font-semibold text-[#0F172A] mt-0.5">
                      {selectedContract.parties
                        .map((p) => `${p.fullName} (${p.role})`)
                        .join(', ') || '—'}
                    </p>
                  </div>
                </div>

                {selectedContract.termsSummary && (
                  <div className="rounded-lg bg-white p-3 border border-[#E2E8F0] text-xs">
                    <p className="font-semibold text-[#0F172A] mb-1">Tóm tắt điều khoản:</p>
                    <p className="text-[#475569] whitespace-pre-line">
                      {selectedContract.termsSummary}
                    </p>
                  </div>
                )}

                {selectedContract.statusHistory.length > 0 && (
                  <div className="rounded-lg bg-white p-3 border border-[#E2E8F0] text-xs">
                    <p className="font-semibold text-[#0F172A] mb-2">
                      Lịch sử trạng thái hợp đồng:
                    </p>
                    <div className="space-y-1.5">
                      {selectedContract.statusHistory.map((h, idx) => (
                        <div
                          key={idx}
                          className="flex items-center justify-between text-[#475569]"
                        >
                          <span>
                            <ShieldCheck className="w-3.5 h-3.5 inline mr-1 text-[#2563EB]" />
                            {h.previousStatus ? `${h.previousStatus} → ` : ''}
                            <strong className="text-[#0F172A]">{h.newStatus}</strong>
                            {h.reason ? ` (${h.reason})` : ''}
                          </span>
                          <span className="text-[#64748B]">
                            {formatDateTimeVi(h.changedAtUtc)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        ) : (
          <EmptyState
            title="Chưa có hợp đồng căn hộ"
            description="Khi bạn đứng tên trên hợp đồng thuê hoặc sở hữu căn hộ, thông tin hợp đồng sẽ xuất hiện tại đây."
          />
        )}
      </Card>
    </div>
  );
}

