import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useRef } from 'react';
import { useSqlDataManager } from '../providers/SqlDataManagerProvider';
import type { ObjectLayout, UserSettings } from '../models';

/**
 * Loads and persists per-object grid layout (hidden columns, column order,
 * widths, page size, sort) to the backend settings store. Persisting here is
 * what makes column hiding / ordering survive pagination, sorting, refresh and
 * even a backend restart (acceptance criteria §36).
 *
 * Writes are debounced so rapid interactions do not spam the settings endpoint.
 */
export function useObjectLayout(objectFullName: string | undefined) {
  const { api } = useSqlDataManager();
  const queryClient = useQueryClient();
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const settingsQuery = useQuery({
    queryKey: ['sdm', 'settings'],
    queryFn: () => api.getSettings(),
  });

  const saveMutation = useMutation({
    mutationFn: (settings: UserSettings) => api.saveSettings(settings),
    onSuccess: (_, settings) => {
      queryClient.setQueryData(['sdm', 'settings'], settings);
    },
  });

  const layout: ObjectLayout | undefined =
    objectFullName && settingsQuery.data?.objectLayouts
      ? settingsQuery.data.objectLayouts[objectFullName]
      : undefined;

  const saveLayout = useCallback(
    (next: ObjectLayout) => {
      if (!objectFullName) return;
      const current = settingsQuery.data ?? {};
      const merged: UserSettings = {
        ...current,
        lastSelectedObject: objectFullName,
        objectLayouts: {
          ...(current.objectLayouts ?? {}),
          [objectFullName]: next,
        },
      };

      // Optimistically update the cache so the UI stays responsive…
      queryClient.setQueryData(['sdm', 'settings'], merged);

      // …then debounce the network write.
      if (debounceRef.current) clearTimeout(debounceRef.current);
      debounceRef.current = setTimeout(() => saveMutation.mutate(merged), 600);
    },
    [objectFullName, settingsQuery.data, queryClient, saveMutation],
  );

  return { layout, saveLayout, isLoaded: settingsQuery.isSuccess };
}
