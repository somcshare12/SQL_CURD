import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { HttpSqlDataManagerApiClient, type SqlDataManagerApiClient } from '../api/apiClient';
import type { RecordMutationResult, SqlDataManagerError } from '../models';

/**
 * The values every part of the feature can reach: the API client, the
 * configurable base path (for routing) and the optional parent callbacks.
 */
export interface SqlDataManagerContextValue {
  api: SqlDataManagerApiClient;
  basePath: string;
  embedded: boolean;
  onMutationCompleted?: (result: RecordMutationResult) => void;
  onError?: (error: SqlDataManagerError) => void;
}

const SqlDataManagerContext = createContext<SqlDataManagerContextValue | null>(null);

export interface SqlDataManagerProviderProps {
  /** Base URL of the module API (defaults to the standalone prefix). */
  apiBaseUrl?: string;
  /** Route base path for links (defaults to `/sql-data-manager`). */
  basePath?: string;
  embedded?: boolean;
  /** Allows a parent app to inject a pre-authenticated API client. */
  apiClient?: SqlDataManagerApiClient;
  onMutationCompleted?: (result: RecordMutationResult) => void;
  onError?: (error: SqlDataManagerError) => void;
  children: ReactNode;
}

/**
 * Provides the feature's shared services. In standalone mode the default HTTP
 * client is created from `apiBaseUrl`; in embedded mode the parent can pass a
 * custom `apiClient`.
 */
export function SqlDataManagerProvider({
  apiBaseUrl = '/api/sql-data-manager',
  basePath = '/sql-data-manager',
  embedded = false,
  apiClient,
  onMutationCompleted,
  onError,
  children,
}: SqlDataManagerProviderProps) {
  const value = useMemo<SqlDataManagerContextValue>(
    () => ({
      api: apiClient ?? new HttpSqlDataManagerApiClient(apiBaseUrl),
      basePath,
      embedded,
      onMutationCompleted,
      onError,
    }),
    [apiClient, apiBaseUrl, basePath, embedded, onMutationCompleted, onError],
  );

  return <SqlDataManagerContext.Provider value={value}>{children}</SqlDataManagerContext.Provider>;
}

/** Hook to access the feature context; throws if used outside the provider. */
export function useSqlDataManager(): SqlDataManagerContextValue {
  const context = useContext(SqlDataManagerContext);
  if (!context) {
    throw new Error('useSqlDataManager must be used within a SqlDataManagerProvider.');
  }
  return context;
}
