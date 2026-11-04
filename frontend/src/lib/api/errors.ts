import type { ProblemDetailsResponse } from './types';

export type ApiErrorCode =
  | 'UNAUTHORIZED'
  | 'FORBIDDEN'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'LOCKED'
  | 'VALIDATION_ERROR'
  | 'SERVER_ERROR'
  | 'NETWORK_ERROR'
  | 'UNKNOWN_ERROR';

export class ApiClientError extends Error {
  public readonly status: number;
  public readonly code: ApiErrorCode;
  public readonly validationErrors: Record<string, string[]>;
  public readonly isNetworkError: boolean;

  constructor(params: {
    message: string;
    status: number;
    code: ApiErrorCode;
    validationErrors?: Record<string, string[]>;
    isNetworkError?: boolean;
  }) {
    super(params.message);
    this.name = 'ApiClientError';
    this.status = params.status;
    this.code = params.code;
    this.validationErrors = params.validationErrors ?? {};
    this.isNetworkError = params.isNetworkError ?? false;
  }

  public getFieldErrors(fieldName: string): string[] {
    const exact = this.validationErrors[fieldName];
    if (exact) return exact;

    const lowerKey = Object.keys(this.validationErrors).find(
      (k) => k.toLowerCase() === fieldName.toLowerCase()
    );
    return lowerKey ? this.validationErrors[lowerKey] : [];
  }
}

export function mapStatusToErrorCode(
  status: number,
  problem?: ProblemDetailsResponse | null
): ApiErrorCode {
  if (status === 401) return 'UNAUTHORIZED';
  if (status === 403) return 'FORBIDDEN';
  if (status === 404) return 'NOT_FOUND';
  if (status === 409) return 'CONFLICT';
  if (status === 423) return 'LOCKED';
  if (status === 400 || status === 422 || (problem?.errors && Object.keys(problem.errors).length > 0)) {
    return 'VALIDATION_ERROR';
  }
  if (status >= 500) return 'SERVER_ERROR';
  return 'UNKNOWN_ERROR';
}

export function extractErrorMessage(
  status: number,
  problem?: ProblemDetailsResponse | null
): string {
  if (problem?.detail && problem.detail.trim().length > 0) {
    return problem.detail.trim();
  }

  if (problem?.errors) {
    const firstKey = Object.keys(problem.errors)[0];
    const firstMsg = firstKey ? problem.errors[firstKey]?.[0] : undefined;
    if (firstMsg) return firstMsg;
  }

  if (problem?.title && problem.title.trim().length > 0) {
    return problem.title.trim();
  }

  switch (status) {
    case 401:
      return 'Phiên đăng nhập đã hết hạn hoặc chưa xác thực.';
    case 403:
      return 'Bạn không có quyền thực hiện thao tác này.';
    case 404:
      return 'Không tìm thấy dữ liệu yêu cầu.';
    case 409:
      return 'Dữ liệu đã bị thay đổi bởi người dùng khác hoặc xung đột trạng thái.';
    case 423:
      return 'Tài khoản đang bị tạm khóa.';
    default:
      return status >= 500
        ? 'Hệ thống đang gặp sự cố. Vui lòng thử lại sau.'
        : 'Yêu cầu không hợp lệ.';
  }
}

