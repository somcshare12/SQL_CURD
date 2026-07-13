import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { useSqlDataManager } from '../providers/SqlDataManagerProvider';
import type {
  CreateRecordRequest,
  DeleteRecordRequest,
  QueryRequest,
  RecordKeyValue,
  UpdateRecordRequest,
} from '../models';

/**
 * All server-state access goes through TanStack Query so the feature gets
 * caching, request de-duplication, cancellation of obsolete requests and a
 * subtle loading overlay (via `keepPreviousData`) for free.
 */

export function useModuleInfo() {
  const { api } = useSqlDataManager();
  return useQuery({ queryKey: ['sdm', 'info'], queryFn: () => api.getInfo() });
}

export function useObjects() {
  const { api } = useSqlDataManager();
  return useQuery({ queryKey: ['sdm', 'objects'], queryFn: () => api.getObjects() });
}

export function useMetadata(schema?: string, objectName?: string) {
  const { api } = useSqlDataManager();
  return useQuery({
    queryKey: ['sdm', 'metadata', schema, objectName],
    queryFn: () => api.getMetadata(schema!, objectName!),
    enabled: Boolean(schema && objectName),
  });
}

export function useRows(schema: string | undefined, objectName: string | undefined, request: QueryRequest) {
  const { api } = useSqlDataManager();
  return useQuery({
    queryKey: ['sdm', 'rows', schema, objectName, request],
    queryFn: () => api.queryRows(schema!, objectName!, request),
    enabled: Boolean(schema && objectName),
    // Keep the previous page visible while the next loads (spec §37).
    placeholderData: keepPreviousData,
  });
}

export function useReadRow() {
  const { api } = useSqlDataManager();
  return useMutation({
    mutationFn: (vars: { schema: string; objectName: string; keys: RecordKeyValue[] }) =>
      api.readRow(vars.schema, vars.objectName, vars.keys),
  });
}

/** Invalidates the affected row/metadata caches after any mutation. */
function useInvalidateAfterMutation(schema: string, objectName: string) {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['sdm', 'rows', schema, objectName] });
  };
}

export function useCreateRow(schema: string, objectName: string) {
  const { api, onMutationCompleted } = useSqlDataManager();
  const invalidate = useInvalidateAfterMutation(schema, objectName);
  return useMutation({
    mutationFn: (request: CreateRecordRequest) => api.createRow(schema, objectName, request),
    onSuccess: (result) => {
      invalidate();
      onMutationCompleted?.(result);
    },
  });
}

export function useUpdateRow(schema: string, objectName: string) {
  const { api, onMutationCompleted } = useSqlDataManager();
  const invalidate = useInvalidateAfterMutation(schema, objectName);
  return useMutation({
    mutationFn: (request: UpdateRecordRequest) => api.updateRow(schema, objectName, request),
    onSuccess: (result) => {
      invalidate();
      onMutationCompleted?.(result);
    },
  });
}

export function useDeleteRow(schema: string, objectName: string) {
  const { api, onMutationCompleted } = useSqlDataManager();
  const invalidate = useInvalidateAfterMutation(schema, objectName);
  return useMutation({
    mutationFn: (request: DeleteRecordRequest) => api.deleteRow(schema, objectName, request),
    onSuccess: (result) => {
      invalidate();
      onMutationCompleted?.(result);
    },
  });
}
