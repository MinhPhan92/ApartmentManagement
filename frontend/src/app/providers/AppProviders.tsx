import React, { useState } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './AuthProvider';
import { ToastProvider } from '../../components/ui/Toast';

export function createAppQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        refetchOnWindowFocus: false,
        staleTime: 30_000,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

export function AppProviders({
  children,
  queryClient: externalQueryClient,
}: {
  children: React.ReactNode;
  queryClient?: QueryClient;
}) {
  const [defaultClient] = useState(() => createAppQueryClient());
  const client = externalQueryClient ?? defaultClient;

  return (
    <QueryClientProvider client={client}>
      <ToastProvider>
        <AuthProvider>{children}</AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
}

