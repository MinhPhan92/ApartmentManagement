import '@testing-library/jest-dom/vitest';
import { afterEach, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import { clearCsrfToken, onUnauthorizedResponse } from '../lib/api/client';

afterEach(() => {
  cleanup();
  clearCsrfToken();
  onUnauthorizedResponse(null);
  vi.restoreAllMocks();
});

