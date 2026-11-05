import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppProviders } from '../app/providers/AppProviders';
import { AppRouter } from '../app/router/AppRouter';
import type { CurrentUser } from '../types/auth';

const mockResident: CurrentUser = {
  userId: 'user-res-1',
  userName: 'resident01',
  displayName: 'Nguyễn Văn Cư Dân',
  email: 'resident01@apartment.local',
  phoneNumber: '0901234567',
  roles: ['Resident'],
  portal: 'resident',
  defaultRoute: '/resident/dashboard',
  permissions: {
    requireSuperAdmin: false,
    requireManagement: false,
    requireAccountantOrManager: false,
    requireTechnician: false,
    requireTechnicianOrManagement: false,
    requireResident: true,
  },
  residentContext: {
    residentId: 10,
    activeApartmentId: 101,
    activeApartmentCode: 'S1.02-1208',
    buildingName: 'Tòa S1.02 Grand Park',
  },
};

function renderResidentRoute(initialRoute: string) {
  return render(
    <MemoryRouter initialEntries={[initialRoute]}>
      <AppProviders>
        <AppRouter enableSignalR={false} />
      </AppProviders>
    </MemoryRouter>
  );
}

describe('Resident Portal Modules (Amenities, Invoices & VietQR, Vehicles, Visitors, Repairs & Complaints)', () => {
  it('renders Resident Amenities catalog, checks booked slots, and submits a booking', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.includes('/api/auth/csrf')) {
        return new Response(
          JSON.stringify({ headerName: 'RequestVerificationToken', requestToken: 'csrf-res-1' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/auth/me')) {
        return new Response(JSON.stringify(mockResident), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      if (url.includes('/api/notifications/recent')) {
        return new Response(JSON.stringify({ unreadCount: 1, items: [] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      if (url.includes('/api/resident/amenities') && url.includes('/booked-slots')) {
        return new Response(
          JSON.stringify([
            { bookingId: 50, startTime: '16:00', endTime: '18:00', guests: 4, status: 'Đã xác nhận' },
          ]),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/resident/amenities/bookings') && init?.method === 'POST') {
        return new Response(
          JSON.stringify({ message: 'Đăng ký sử dụng tiện ích thành công!' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/resident/amenities')) {
        return new Response(
          JSON.stringify({
            amenities: [
              {
                amenityId: 1,
                name: 'Khu nướng BBQ Vườn Nhật',
                description: 'Khu vực bếp nướng ngoài trời dành cho cư dân',
                location: 'Công viên nội khu S1',
                capacity: 15,
                openTime: '08:00',
                closeTime: '22:00',
                isActive: true,
                pricePerHour: 100000,
                isFree: false,
              },
              {
                amenityId: 2,
                name: 'Sân Cầu Lông Nội Khu',
                description: 'Sân cầu lông tiêu chuẩn miễn phí cho cư dân',
                location: 'Khu thể thao S1',
                capacity: 8,
                openTime: '06:00',
                closeTime: '21:00',
                isActive: true,
                pricePerHour: 0,
                isFree: true,
              },
            ],
            myBookings: [],
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }

      return new Response(JSON.stringify({}), { status: 200 });
    });

    const user = userEvent.setup();
    renderResidentRoute('/resident/amenities');

    expect(
      await screen.findByRole('heading', { name: 'Đặt lịch tiện ích nội khu' })
    ).toBeInTheDocument();
    expect((await screen.findAllByText('Khu nướng BBQ Vườn Nhật')).length).toBeGreaterThan(0);
    expect(screen.getByText('Sân Cầu Lông Nội Khu')).toBeInTheDocument();

    const submitBtn = screen.getByRole('button', { name: /Xác nhận đặt lịch tiện ích/i });
    await user.click(submitBtn);

    await waitFor(() => {
      expect(
        fetchSpy.mock.calls.some(
          (c) =>
            String(c[0]).includes('/api/resident/amenities/bookings') &&
            (c[1] as RequestInit)?.method === 'POST'
        )
      ).toBe(true);
    });
  });

  it('renders Resident Invoices, line breakdown, and VietQR payment details', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.includes('/api/auth/csrf')) {
        return new Response(
          JSON.stringify({ headerName: 'RequestVerificationToken', requestToken: 'csrf-res-2' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/auth/me')) {
        return new Response(JSON.stringify(mockResident), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      if (url.includes('/api/notifications/recent')) {
        return new Response(JSON.stringify({ unreadCount: 0, items: [] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      if (url.includes('/api/resident/invoices/101')) {
        return new Response(
          JSON.stringify({
            summary: {
              apartmentInvoiceId: 101,
              invoiceCode: 'INV-202611-000101',
              apartmentId: 101,
              apartmentCode: 'S1.02-1208',
              buildingName: 'Tòa S1.02 Grand Park',
              billingYear: 2026,
              billingMonth: 11,
              periodLabel: 'Tháng 11/2026',
              dueDate: '2026-12-05',
              totalAmount: 1850000,
              confirmedAmount: 0,
              remainingAmount: 1850000,
              status: 'Issued',
              paymentState: 'Unpaid',
              hasPendingTransaction: true,
              issuedAtUtc: '2026-11-01T00:00:00Z',
            },
            apartmentArea: 75,
            lines: [
              {
                lineId: 1,
                chargeType: 'Management',
                chargeTypeLabel: 'Phí quản lý & gửi xe',
                description: 'Phí quản lý theo diện tích',
                quantity: 75,
                unitRate: 15000,
                amount: 1125000,
                previousReading: null,
                currentReading: null,
                rateBreakdown: '75 m² × 15.000 VND/m²',
              },
              {
                lineId: 2,
                chargeType: 'Electricity',
                chargeTypeLabel: 'Tiền điện sinh hoạt',
                description: 'Điện sinh hoạt theo bậc lũy tiến',
                quantity: 250,
                unitRate: 2900,
                amount: 725000,
                previousReading: 1200,
                currentReading: 1450,
                rateBreakdown: 'Bậc 1..Bậc 4',
              },
            ],
            transactions: [
              {
                paymentTransactionId: 501,
                reference: 'VH-INV101-8899',
                amount: 1850000,
                status: 'Pending',
                createdAtUtc: '2026-11-04T10:00:00Z',
                confirmedAtUtc: null,
                rejectedAtUtc: null,
                rejectionReason: null,
                isAutoClosedRejection: false,
                qrImageUrl: '/api/resident/invoices/transactions/501/qr',
              },
            ],
            activePayment: {
              paymentTransactionId: 501,
              reference: 'VH-INV101-8899',
              amount: 1850000,
              status: 'Pending',
              createdAtUtc: '2026-11-04T10:00:00Z',
              qrImageUrl: '/api/resident/invoices/transactions/501/qr',
            },
            vietQr: {
              configured: true,
              amountSupported: true,
              canCreatePaymentAttempt: true,
              bankBin: '970436',
              accountNumber: '1023456789',
              accountName: 'BAN QUAN LY CHUNG CU',
            },
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/resident/invoices')) {
        return new Response(
          JSON.stringify({
            totalCount: 1,
            totalBilledAmount: 1850000,
            totalConfirmedAmount: 0,
            totalRemainingAmount: 1850000,
            vietQrConfigured: true,
            items: [
              {
                apartmentInvoiceId: 101,
                invoiceCode: 'INV-202611-000101',
                apartmentId: 101,
                apartmentCode: 'S1.02-1208',
                buildingName: 'Tòa S1.02 Grand Park',
                billingYear: 2026,
                billingMonth: 11,
                periodLabel: 'Tháng 11/2026',
                dueDate: '2026-12-05',
                totalAmount: 1850000,
                confirmedAmount: 0,
                remainingAmount: 1850000,
                status: 'Issued',
                paymentState: 'Unpaid',
                hasPendingTransaction: true,
                issuedAtUtc: '2026-11-01T00:00:00Z',
              },
            ],
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }

      return new Response(JSON.stringify({}), { status: 200 });
    });

    renderResidentRoute('/resident/invoices');

    expect(
      await screen.findByRole('heading', { name: /Thanh toán phí dịch vụ & Hóa đơn/i })
    ).toBeInTheDocument();

    expect(await screen.findByText('Phí quản lý & gửi xe')).toBeInTheDocument();
    expect(screen.getByText('Tiền điện sinh hoạt')).toBeInTheDocument();
    expect(screen.getByText('BAN QUAN LY CHUNG CU')).toBeInTheDocument();
    expect(screen.getAllByText('VH-INV101-8899').length).toBeGreaterThan(0);
  });

  it('renders Resident Vehicles & Parking Cards and triggers Emergency Card Lock', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.includes('/api/auth/csrf')) {
        return new Response(
          JSON.stringify({ headerName: 'RequestVerificationToken', requestToken: 'csrf-res-3' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/auth/me')) {
        return new Response(JSON.stringify(mockResident), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      if (url.includes('/api/notifications/recent')) {
        return new Response(JSON.stringify({ unreadCount: 0, items: [] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      if (url.includes('/api/resident/vehicles/77/lock-card') && init?.method === 'POST') {
        return new Response(
          JSON.stringify({ message: 'Đã khóa thẻ bãi đỗ xe khẩn cấp thành công.' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }
      if (url.includes('/api/resident/vehicles')) {
        return new Response(
          JSON.stringify({
            vehicles: [
              {
                vehicleId: 77,
                apartmentId: 101,
                apartmentCode: 'S1.02-1208',
                vehicleType: 'Ô tô',
                licensePlate: '30K-888.99',
                brandModel: 'VinFast VF8',
                color: 'Trắng',
                status: 'Đã duyệt',
                createdAt: '2026-10-01T00:00:00Z',
                reviewedAt: '2026-10-02T00:00:00Z',
                reviewNotes: null,
                parkingCard: {
                  parkingCardId: 12,
                  cardCode: 'RFID-VH-8899',
                  status: 'Active',
                  issuedAtUtc: '2026-10-02T00:00:00Z',
                },
              },
            ],
            apartments: [
              {
                apartmentId: 101,
                apartmentCode: 'S1.02-1208',
                buildingName: 'Tòa S1.02 Grand Park',
                label: 'S1.02-1208 — Tòa S1.02 Grand Park',
              },
            ],
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }

      return new Response(JSON.stringify({}), { status: 200 });
    });

    const user = userEvent.setup();
    renderResidentRoute('/resident/vehicles');

    expect(await screen.findByText('30K-888.99')).toBeInTheDocument();
    expect(screen.getByText('RFID-VH-8899')).toBeInTheDocument();

    const lockBtn = screen.getByRole('button', { name: /Khóa thẻ khẩn cấp/i });
    await user.click(lockBtn);

    await waitFor(() => {
      expect(
        fetchSpy.mock.calls.some(
          (c) =>
            String(c[0]).includes('/api/resident/vehicles/77/lock-card') &&
            (c[1] as RequestInit)?.method === 'POST'
        )
      ).toBe(true);
    });
  });
});
