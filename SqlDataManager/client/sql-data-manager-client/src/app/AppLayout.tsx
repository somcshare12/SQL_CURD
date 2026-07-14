import type { ReactNode } from 'react';

/**
 * The standalone application layout. It is intentionally minimal — the module
 * provides its own header/rail/tree — so this simply hosts the routed content
 * full-height. A parent application would supply its own layout instead.
 */
export function AppLayout({ children }: { children: ReactNode }) {
  return <div className="sdm-app-shell">{children}</div>;
}
