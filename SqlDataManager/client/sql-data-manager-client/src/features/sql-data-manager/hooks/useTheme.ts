import { useCallback, useEffect, useState } from 'react';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'sdm-theme';

/**
 * Standalone-mode theme management. It writes a `data-sdm-theme` attribute onto
 * the module root element so all styling is scoped and never leaks into a
 * parent application. In embedded mode the parent theme is inherited instead
 * and this hook is simply not used.
 */
export function useTheme() {
  const [mode, setMode] = useState<ThemeMode>(
    () => (localStorage.getItem(STORAGE_KEY) as ThemeMode) ?? 'system',
  );

  const resolved = useResolvedTheme(mode);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, mode);
  }, [mode]);

  // Apply the resolved theme as a scoped attribute on the document root. All
  // module styling keys off this attribute, so nothing leaks into a parent app.
  useEffect(() => {
    document.documentElement.dataset.sdmTheme = resolved;
  }, [resolved]);

  const toggle = useCallback(() => {
    setMode(resolved === 'dark' ? 'light' : 'dark');
  }, [resolved]);

  return { mode, resolved, setMode, toggle };
}

function useResolvedTheme(mode: ThemeMode): 'light' | 'dark' {
  const [systemDark, setSystemDark] = useState(
    () => window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false,
  );

  useEffect(() => {
    const media = window.matchMedia?.('(prefers-color-scheme: dark)');
    if (!media) return;
    const listener = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    media.addEventListener('change', listener);
    return () => media.removeEventListener('change', listener);
  }, []);

  if (mode === 'system') {
    return systemDark ? 'dark' : 'light';
  }
  return mode;
}
