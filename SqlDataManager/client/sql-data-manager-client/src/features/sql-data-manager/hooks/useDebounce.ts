import { useEffect, useState } from 'react';

/**
 * Returns a debounced copy of `value`. Used to delay firing text-filter and
 * global-search queries by ~300-500ms (spec §20) so we do not hit the database
 * on every keystroke.
 */
export function useDebounce<T>(value: T, delayMs = 350): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}
