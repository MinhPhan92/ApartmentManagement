import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { onUnauthorizedResponse } from '../../lib/api/client';
import { ApiClientError } from '../../lib/api/errors';
import {
  fetchCurrentUser,
  initializeCsrfToken,
  loginWithCredentials,
  logoutCurrentSession,
} from '../../lib/auth/authApi';
import type {
  CurrentUser,
  LoginPayload,
  SessionStatus,
} from '../../types/auth';

export interface AuthContextValue {
  status: SessionStatus;
  user: CurrentUser | null;
  errorMessage: string | null;
  sessionExpired: boolean;
  login: (payload: LoginPayload) => Promise<CurrentUser>;
  logout: () => Promise<void>;
  refreshSession: () => Promise<void>;
  clearSessionExpiredNotice: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<SessionStatus>('initializing');
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [sessionExpired, setSessionExpired] = useState<boolean>(false);

  const refreshSession = useCallback(async () => {
    setStatus('initializing');
    setErrorMessage(null);

    try {
      await initializeCsrfToken();
    } catch (csrfErr) {
      if (csrfErr instanceof ApiClientError && (csrfErr.isNetworkError || csrfErr.status >= 500)) {
        setUser(null);
        setStatus('network_error');
        setErrorMessage(csrfErr.message);
        return;
      }
    }

    try {
      const currentUser = await fetchCurrentUser();
      setUser(currentUser);
      setStatus('authenticated');
      setSessionExpired(false);
    } catch (err) {
      if (err instanceof ApiClientError) {
        if (err.status === 401) {
          setUser(null);
          setStatus('unauthenticated');
          return;
        }
        if (err.isNetworkError || err.status >= 500) {
          setUser(null);
          setStatus('network_error');
          setErrorMessage(err.message);
          return;
        }
      }
      setUser(null);
      setStatus('unauthenticated');
    }
  }, []);

  useEffect(() => {
    void refreshSession();
  }, [refreshSession]);

  useEffect(() => {
    onUnauthorizedResponse(() => {
      setUser((prevUser) => {
        if (prevUser !== null) {
          setSessionExpired(true);
        }
        return null;
      });
      setStatus('unauthenticated');
      queryClient.clear();
    });

    return () => {
      onUnauthorizedResponse(null);
    };
  }, [queryClient]);

  const login = useCallback(
    async (payload: LoginPayload): Promise<CurrentUser> => {
      const loggedInUser = await loginWithCredentials(payload);
      setUser(loggedInUser);
      setStatus('authenticated');
      setSessionExpired(false);
      setErrorMessage(null);
      queryClient.clear();
      return loggedInUser;
    },
    [queryClient]
  );

  const logout = useCallback(async (): Promise<void> => {
    try {
      await logoutCurrentSession();
    } finally {
      setUser(null);
      setStatus('unauthenticated');
      setSessionExpired(false);
      setErrorMessage(null);
      queryClient.clear();
    }
  }, [queryClient]);

  const clearSessionExpiredNotice = useCallback(() => {
    setSessionExpired(false);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      errorMessage,
      sessionExpired,
      login,
      logout,
      refreshSession,
      clearSessionExpiredNotice,
    }),
    [
      status,
      user,
      errorMessage,
      sessionExpired,
      login,
      logout,
      refreshSession,
      clearSessionExpiredNotice,
    ]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return ctx;
}

