import { ApiClientError, extractErrorMessage, mapStatusToErrorCode } from './errors';
import type { ApiRequestOptions, HttpMethod, ProblemDetailsResponse } from './types';

const MUTATING_METHODS: ReadonlySet<HttpMethod> = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

interface CsrfMemoryState {
  headerName: string;
  requestToken: string;
}

let csrfState: CsrfMemoryState = {
  headerName: 'RequestVerificationToken',
  requestToken: '',
};

type UnauthorizedListener = () => void;
let unauthorizedListener: UnauthorizedListener | null = null;

export function setCsrfToken(token: string, headerName = 'RequestVerificationToken'): void {
  csrfState = {
    headerName: headerName || 'RequestVerificationToken',
    requestToken: token || '',
  };
}

export function clearCsrfToken(): void {
  csrfState = {
    headerName: 'RequestVerificationToken',
    requestToken: '',
  };
}

export function getCsrfState(): Readonly<CsrfMemoryState> {
  return csrfState;
}

export function onUnauthorizedResponse(listener: UnauthorizedListener | null): void {
  unauthorizedListener = listener;
}

export function getApiBaseUrl(): string {
  const envBase = import.meta.env?.VITE_API_BASE_URL;
  return typeof envBase === 'string' ? envBase.replace(/\/+$/, '') : '';
}

export function resolveApiUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }
  const base = getApiBaseUrl();
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${base}${normalizedPath}`;
}

export async function apiRequest<TResponse, TBody = unknown>(
  path: string,
  options: ApiRequestOptions<TBody> = {}
): Promise<TResponse> {
  const method: HttpMethod = options.method ?? 'GET';
  const url = resolveApiUrl(path);

  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...options.headers,
  };

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (MUTATING_METHODS.has(method) && csrfState.requestToken) {
    headers[csrfState.headerName] = csrfState.requestToken;
  }

  let response: Response;
  try {
    response = await fetch(url, {
      method,
      headers,
      credentials: 'include',
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: options.signal,
    });
  } catch {
    throw new ApiClientError({
      message: 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại.',
      status: 0,
      code: 'NETWORK_ERROR',
      isNetworkError: true,
    });
  }

  const contentType = response.headers.get('content-type') ?? '';
  const isJson = contentType.includes('application/json') || contentType.includes('application/problem+json');

  if (!response.ok) {
    let problem: ProblemDetailsResponse | null = null;
    if (isJson) {
      try {
        problem = (await response.json()) as ProblemDetailsResponse;
      } catch {
        problem = null;
      }
    }

    if (response.status === 401 && !options.skipUnauthorizedHandler && unauthorizedListener) {
      unauthorizedListener();
    }

    throw new ApiClientError({
      message: extractErrorMessage(response.status, problem),
      status: response.status,
      code: mapStatusToErrorCode(response.status, problem),
      validationErrors: problem?.errors ?? {},
      isNetworkError: false,
    });
  }

  if (response.status === 204) {
    return undefined as TResponse;
  }

  if (isJson) {
    return (await response.json()) as TResponse;
  }

  return undefined as TResponse;
}

export const apiClient = {
  get<TResponse>(path: string, options?: Omit<ApiRequestOptions<never>, 'method' | 'body'>) {
    return apiRequest<TResponse>(path, { ...options, method: 'GET' });
  },
  post<TResponse, TBody = unknown>(
    path: string,
    body?: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'method' | 'body'>
  ) {
    return apiRequest<TResponse, TBody>(path, { ...options, method: 'POST', body });
  },
  put<TResponse, TBody = unknown>(
    path: string,
    body?: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'method' | 'body'>
  ) {
    return apiRequest<TResponse, TBody>(path, { ...options, method: 'PUT', body });
  },
  patch<TResponse, TBody = unknown>(
    path: string,
    body?: TBody,
    options?: Omit<ApiRequestOptions<TBody>, 'method' | 'body'>
  ) {
    return apiRequest<TResponse, TBody>(path, { ...options, method: 'PATCH', body });
  },
  delete<TResponse>(path: string, options?: Omit<ApiRequestOptions<never>, 'method' | 'body'>) {
    return apiRequest<TResponse>(path, { ...options, method: 'DELETE' });
  },
};

