export interface ProblemDetailsResponse {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export interface ApiRequestOptions<TBody = unknown> {
  method?: HttpMethod;
  body?: TBody;
  headers?: Record<string, string>;
  signal?: AbortSignal;
  /**
   * When true, a 401 response will not trigger global session expiry handlers.
   * Used during initial `/api/auth/me` session probe or explicit login calls.
   */
  skipUnauthorizedHandler?: boolean;
}

