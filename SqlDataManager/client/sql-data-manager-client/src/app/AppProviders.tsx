import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { SqlDataManagerProvider } from '../features/sql-data-manager';

/** The API base URL and route base path for the standalone host. */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api/sql-data-manager';
export const BASE_PATH = '/sql-data-manager';

/**
 * Wraps the app in the providers the module needs: TanStack Query for server
 * state and the module's own provider (API client + config + callbacks).
 */
export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // Metadata/objects change rarely; rows are refetched on demand.
            staleTime: 30_000,
            retry: 1,
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      <SqlDataManagerProvider apiBaseUrl={API_BASE_URL} basePath={BASE_PATH}>
        {children}
      </SqlDataManagerProvider>
    </QueryClientProvider>
  );
}
