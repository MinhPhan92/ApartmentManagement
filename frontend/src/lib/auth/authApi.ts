import { apiClient, clearCsrfToken, getCsrfState, setCsrfToken } from '../api/client';
import { API_ENDPOINTS } from '../api/endpoints';
import type {
  CsrfTokenResponse,
  CurrentUser,
  LoginPayload,
  LogoutResponse,
} from '../../types/auth';

export async function initializeCsrfToken(): Promise<CsrfTokenResponse> {
  const tokenData = await apiClient.get<CsrfTokenResponse>(API_ENDPOINTS.auth.csrf, {
    skipUnauthorizedHandler: true,
  });
  setCsrfToken(tokenData.requestToken, tokenData.headerName);
  return tokenData;
}

export async function ensureCsrfToken(): Promise<void> {
  const current = getCsrfState();
  if (!current.requestToken) {
    await initializeCsrfToken();
  }
}

export async function fetchCurrentUser(): Promise<CurrentUser> {
  return apiClient.get<CurrentUser>(API_ENDPOINTS.auth.me, {
    skipUnauthorizedHandler: true,
  });
}

export async function loginWithCredentials(payload: LoginPayload): Promise<CurrentUser> {
  await ensureCsrfToken();
  const user = await apiClient.post<CurrentUser, LoginPayload>(
    API_ENDPOINTS.auth.login,
    payload,
    { skipUnauthorizedHandler: true }
  );
  // Refresh CSRF token so subsequent mutating requests match the newly authenticated identity
  try {
    await initializeCsrfToken();
  } catch {
    // Non-fatal: next mutating call can still use or re-fetch token
  }
  return user;
}

export async function logoutCurrentSession(): Promise<LogoutResponse> {
  await ensureCsrfToken();
  try {
    const response = await apiClient.post<LogoutResponse>(
      API_ENDPOINTS.auth.logout,
      undefined,
      { skipUnauthorizedHandler: true }
    );
    return response;
  } finally {
    clearCsrfToken();
    try {
      await initializeCsrfToken();
    } catch {
      // Ignore if offline during logout cleanup
    }
  }
}

