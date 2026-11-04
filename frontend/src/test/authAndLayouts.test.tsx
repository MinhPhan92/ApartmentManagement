import { act } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppProviders } from '../app/providers/AppProviders';
import { AppRouter } from '../app/router/AppRouter';
import { apiClient } from '../lib/api/client';
import type { CurrentUser } from '../types/auth';

function createMockUser(overrides: Partial<CurrentUser>): CurrentUser {
  return {
    userId: 'user-1',
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
      activeApartmentCode: 'A-1204',
      buildingName: 'Tòa A - Lotus',
    },
    ...overrides,
  };
}

function mockBackendSession(user: CurrentUser | null, status = 200) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = typeof input === 'string' ? input : input.toString();

    if (url.includes('/api/auth/csrf')) {
      return new Response(
        JSON.stringify({
          headerName: 'RequestVerificationToken',
          requestToken: 'csrf-token-abc',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      );
    }

    if (url.includes('/api/auth/me')) {
      if (!user || status === 401) {
        return new Response(
          JSON.stringify({ title: 'Unauthorized', status: 401 }),
          { status: 401, headers: { 'Content-Type': 'application/problem+json' } }
        );
      }
      return new Response(JSON.stringify(user), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    if (url.includes('/api/notifications/recent')) {
      return new Response(JSON.stringify({ unreadCount: 2, items: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    return new Response(JSON.stringify({}), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    });
  });
}

function renderWithRouter(initialEntries: string[] = ['/']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <AppProviders>
        <AppRouter enableSignalR={false} />
      </AppProviders>
    </MemoryRouter>
  );
}

describe('Authentication, Protected Routes & Role-Based Portal Layouts', () => {
  it('redirects unauthenticated users from protected routes to /login and validates login form inputs', async () => {
    mockBackendSession(null, 401);
    const user = userEvent.setup();

    renderWithRouter(['/resident/dashboard']);

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /Đăng nhập ApartmentManagement/i })
      ).toBeInTheDocument();
    });

    const submitBtn = screen.getByRole('button', { name: 'Đăng nhập' });
    await user.click(submitBtn);

    expect(
      await screen.findByText('Vui lòng nhập Tên đăng nhập hoặc Email.')
    ).toBeInTheDocument();
    expect(screen.getByText('Vui lòng nhập Mật khẩu.')).toBeInTheDocument();
  });

  it('performs login with CSRF token header, supports password visibility toggle, and navigates to portal', async () => {
    const residentUser = createMockUser({
      roles: ['Resident'],
      portal: 'resident',
      defaultRoute: '/resident/dashboard',
    });

    let isSignedIn = false;
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = typeof input === 'string' ? input : input.toString();

      if (url.includes('/api/auth/csrf')) {
        return new Response(
          JSON.stringify({
            headerName: 'RequestVerificationToken',
            requestToken: 'csrf-login-token-999',
          }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        );
      }

      if (url.includes('/api/auth/me')) {
        if (!isSignedIn) {
          return new Response(JSON.stringify({ status: 401 }), {
            status: 401,
            headers: { 'Content-Type': 'application/problem+json' },
          });
        }
        return new Response(JSON.stringify(residentUser), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        });
      }

      if (url.includes('/api/auth/login')) {
        isSignedIn = true;
        return new Response(JSON.stringify(residentUser), {
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

      return new Response(JSON.stringify({}), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    const user = userEvent.setup();
    renderWithRouter(['/login']);

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /Đăng nhập ApartmentManagement/i })
      ).toBeInTheDocument();
    });

    const usernameInput = screen.getByLabelText('Tài khoản hoặc Email');
    const passwordInput = screen.getByLabelText('Mật khẩu') as HTMLInputElement;
    expect(passwordInput.type).toBe('password');

    // Toggle password visibility
    await user.click(screen.getByRole('button', { name: 'Hiện mật khẩu' }));
    expect(passwordInput.type).toBe('text');

    await user.type(usernameInput, 'resident01@apartment.local');
    await user.type(passwordInput, 'Resident@123');
    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }));

    await waitFor(() => {
      expect(screen.getByTestId('resident-portal-layout')).toBeInTheDocument();
    });

    const loginCall = fetchSpy.mock.calls.find((c) =>
      String(c[0]).includes('/api/auth/login')
    );
    expect(loginCall).toBeDefined();
    const loginHeaders = (loginCall![1] as RequestInit).headers as Record<string, string>;
    expect(loginHeaders['RequestVerificationToken']).toBe('csrf-login-token-999');
  });

  it('does NOT redirect to /login when session initialization fails due to network error', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'));

    renderWithRouter(['/resident/dashboard']);

    await waitFor(() => {
      expect(screen.getByText('Lỗi kết nối máy chủ')).toBeInTheDocument();
    });
    expect(
      screen.queryByRole('heading', { name: /Đăng nhập ApartmentManagement/i })
    ).not.toBeInTheDocument();
  });

  it('renders Resident Portal Layout for Resident role and blocks Resident from accessing /management/* routes', async () => {
    const residentUser = createMockUser({
      roles: ['Resident'],
      portal: 'resident',
      defaultRoute: '/resident/dashboard',
    });
    mockBackendSession(residentUser);

    const { unmount } = renderWithRouter(['/resident/dashboard']);

    await waitFor(() => {
      expect(screen.getByTestId('resident-portal-layout')).toBeInTheDocument();
    });

    // Verify Resident menu items
    expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Căn hộ của tôi' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Hóa đơn & thanh toán' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Yêu cầu sửa chữa' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Phương tiện' })).toBeInTheDocument();
    expect(screen.getByText('A-1204')).toBeInTheDocument();

    unmount();

    // Now attempt to navigate Resident to /management/dashboard
    renderWithRouter(['/management/dashboard']);

    await waitFor(() => {
      expect(
        screen.getByText('Không được phép truy cập cổng này')
      ).toBeInTheDocument();
    });
    expect(
      screen.queryByTestId('management-portal-layout')
    ).not.toBeInTheDocument();
  });

  it('filters Management navigation for Technician and blocks Technician from Billing or SuperAdmin routes', async () => {
    const technicianUser = createMockUser({
      userName: 'tech01',
      displayName: 'Trần Kỹ Thuật',
      roles: ['Technician'],
      portal: 'management',
      defaultRoute: '/management/dashboard',
      permissions: {
        requireSuperAdmin: false,
        requireManagement: false,
        requireAccountantOrManager: false,
        requireTechnician: true,
        requireTechnicianOrManagement: true,
        requireResident: false,
      },
      residentContext: null,
    });
    mockBackendSession(technicianUser);

    const { unmount } = renderWithRouter(['/management/dashboard']);

    await waitFor(() => {
      expect(screen.getByTestId('management-portal-layout')).toBeInTheDocument();
    });

    // Technician SHOULD see allowed items
    expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sửa chữa' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sự cố kỹ thuật' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Thông báo' })).toBeInTheDocument();

    // Technician MUST NOT see unauthorized items
    expect(screen.queryByRole('link', { name: 'Tòa nhà & căn hộ' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Cư dân' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Hợp đồng' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Hóa đơn' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Thanh toán & công nợ' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Quản trị tài khoản' })).not.toBeInTheDocument();

    unmount();

    // Direct navigation by Technician to /management/billing must be blocked by policy guard
    renderWithRouter(['/management/billing']);
    await waitFor(() => {
      expect(
        screen.getByText('Không đủ quyền thực hiện chức năng')
      ).toBeInTheDocument();
    });
  });

  it('filters Management navigation for Accountant and blocks Accountant from SuperAdmin Accounts route', async () => {
    const accountantUser = createMockUser({
      userName: 'accountant01',
      displayName: 'Lê Kế Toán',
      roles: ['Accountant'],
      portal: 'management',
      defaultRoute: '/management/dashboard',
      permissions: {
        requireSuperAdmin: false,
        requireManagement: false,
        requireAccountantOrManager: true,
        requireTechnician: false,
        requireTechnicianOrManagement: false,
        requireResident: false,
      },
      residentContext: null,
    });
    mockBackendSession(accountantUser);

    const { unmount } = renderWithRouter(['/management/dashboard']);

    await waitFor(() => {
      expect(screen.getByTestId('management-portal-layout')).toBeInTheDocument();
    });

    // Accountant SHOULD see financial & overview items
    expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Hóa đơn' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Thanh toán & công nợ' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Báo cáo' })).toBeInTheDocument();

    // Accountant MUST NOT see Property, Repairs, or SuperAdmin Accounts
    expect(screen.queryByRole('link', { name: 'Tòa nhà & căn hộ' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Sửa chữa' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Quản trị tài khoản' })).not.toBeInTheDocument();

    unmount();

    // Direct URL access to /management/accounts must be denied
    renderWithRouter(['/management/accounts']);
    await waitFor(() => {
      expect(
        screen.getByText('Không đủ quyền thực hiện chức năng')
      ).toBeInTheDocument();
    });
  });

  it('grants SuperAdmin access to all management modules including System Accounts', async () => {
    const superAdminUser = createMockUser({
      userName: 'admin',
      displayName: 'Quản Trị Hệ Thống',
      roles: ['SuperAdmin'],
      portal: 'management',
      defaultRoute: '/management/dashboard',
      permissions: {
        requireSuperAdmin: true,
        requireManagement: true,
        requireAccountantOrManager: true,
        requireTechnician: false,
        requireTechnicianOrManagement: true,
        requireResident: false,
      },
      residentContext: null,
    });
    mockBackendSession(superAdminUser);

    renderWithRouter(['/management/accounts']);

    await waitFor(() => {
      expect(screen.getByTestId('management-portal-layout')).toBeInTheDocument();
    });

    expect(
      screen.getByRole('heading', { name: 'Quản trị tài khoản' })
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tòa nhà & căn hộ' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Hóa đơn' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Quản trị tài khoản' })).toBeInTheDocument();
  });

  it('transitions to /login with session-expired alert when an API request returns 401 during an active session', async () => {
    const residentUser = createMockUser({
      roles: ['Resident'],
      portal: 'resident',
      defaultRoute: '/resident/dashboard',
    });
    const fetchSpy = mockBackendSession(residentUser);

    renderWithRouter(['/resident/dashboard']);

    await waitFor(() => {
      expect(screen.getByTestId('resident-portal-layout')).toBeInTheDocument();
    });

    // Simulate session expiration on the next API call
    fetchSpy.mockResolvedValueOnce(
      new Response(JSON.stringify({ title: 'Unauthorized', status: 401 }), {
        status: 401,
        headers: { 'Content-Type': 'application/problem+json' },
      })
    );

    await act(async () => {
      await expect(
        apiClient.get('/api/notifications/unread-count')
      ).rejects.toMatchObject({
        status: 401,
      });
    });

    await waitFor(() => {
      expect(screen.getByTestId('session-expired-alert')).toBeInTheDocument();
    });
  });
});
