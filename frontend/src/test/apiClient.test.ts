import { describe, expect, it, vi } from 'vitest';
import {
  apiClient,
  clearCsrfToken,
  onUnauthorizedResponse,
  setCsrfToken,
} from '../lib/api/client';
import { ApiClientError } from '../lib/api/errors';

describe('API Client & CSRF handling', () => {
  it('sends credentials: include and attaches CSRF token only on mutating requests (POST/PUT/PATCH/DELETE)', async () => {
    setCsrfToken('test-csrf-token-123', 'RequestVerificationToken');

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => {
      return new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    });

    await apiClient.get('/api/auth/me');
    const getCallInit = fetchSpy.mock.calls[0][1] as RequestInit;
    const getHeaders = getCallInit.headers as Record<string, string>;
    expect(getCallInit.credentials).toBe('include');
    expect(getHeaders['RequestVerificationToken']).toBeUndefined();

    await apiClient.post('/api/auth/logout', { reason: 'manual' });
    const postCallInit = fetchSpy.mock.calls[1][1] as RequestInit;
    const postHeaders = postCallInit.headers as Record<string, string>;
    expect(postCallInit.credentials).toBe('include');
    expect(postHeaders['RequestVerificationToken']).toBe('test-csrf-token-123');
    expect(postHeaders['Content-Type']).toBe('application/json');

    await apiClient.put('/api/test/1', { name: 'A' });
    const putHeaders = (fetchSpy.mock.calls[2][1] as RequestInit).headers as Record<
      string,
      string
    >;
    expect(putHeaders['RequestVerificationToken']).toBe('test-csrf-token-123');

    await apiClient.delete('/api/test/1');
    const deleteHeaders = (fetchSpy.mock.calls[3][1] as RequestInit).headers as Record<
      string,
      string
    >;
    expect(deleteHeaders['RequestVerificationToken']).toBe('test-csrf-token-123');

    clearCsrfToken();
  });

  it('handles 401 responses and respects skipUnauthorizedHandler during session initialization', async () => {
    const unauthorizedCallback = vi.fn();
    onUnauthorizedResponse(unauthorizedCallback);

    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => {
      return new Response(
        JSON.stringify({
          title: 'Unauthorized',
          detail: 'Phiên đăng nhập không hợp lệ.',
          status: 401,
        }),
        {
          status: 401,
          headers: { 'Content-Type': 'application/problem+json' },
        }
      );
    });

    // 1. Initial session probe with skipUnauthorizedHandler = true should NOT fire callback
    await expect(
      apiClient.get('/api/auth/me', { skipUnauthorizedHandler: true })
    ).rejects.toMatchObject({
      status: 401,
      code: 'UNAUTHORIZED',
    });
    expect(unauthorizedCallback).not.toHaveBeenCalled();

    // 2. Subsequent regular request returning 401 SHOULD fire unauthorized callback
    await expect(apiClient.get('/api/notifications/recent')).rejects.toMatchObject({
      status: 401,
      code: 'UNAUTHORIZED',
    });
    expect(unauthorizedCallback).toHaveBeenCalledTimes(1);
  });

  it('maps 403, 404, 409, and 500 responses to structured ApiClientError without retrying mutations', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch');

    fetchSpy.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          title: 'Forbidden',
          detail: 'Không đủ quyền truy cập.',
          status: 403,
        }),
        { status: 403, headers: { 'Content-Type': 'application/problem+json' } }
      )
    );

    await expect(apiClient.post('/api/restricted')).rejects.toMatchObject({
      status: 403,
      code: 'FORBIDDEN',
      message: 'Không đủ quyền truy cập.',
    });
    expect(fetchSpy).toHaveBeenCalledTimes(1);

    fetchSpy.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          title: 'Conflict',
          detail: 'Phiên bản dữ liệu đã thay đổi.',
          status: 409,
        }),
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } }
      )
    );

    await expect(apiClient.post('/api/conflict')).rejects.toMatchObject({
      status: 409,
      code: 'CONFLICT',
      message: 'Phiên bản dữ liệu đã thay đổi.',
    });
    expect(fetchSpy).toHaveBeenCalledTimes(2);
  });

  it('distinguishes network failures from HTTP 401 errors', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new TypeError('Failed to fetch'));

    try {
      await apiClient.get('/api/auth/me', { skipUnauthorizedHandler: true });
      expect.fail('Expected network error');
    } catch (err) {
      expect(err).toBeInstanceOf(ApiClientError);
      const apiErr = err as ApiClientError;
      expect(apiErr.isNetworkError).toBe(true);
      expect(apiErr.code).toBe('NETWORK_ERROR');
      expect(apiErr.status).toBe(0);
    }
  });
});

