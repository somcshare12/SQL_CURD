import type { DatabaseObject } from '../models';

/**
 * Centralised route generation. Every link in the feature is produced here so
 * that hard-coded paths never leak into components and the base path stays
 * configurable (important for embedded mounting under a parent router).
 */
export function makeRoutes(basePath: string) {
  const base = basePath.replace(/\/$/, '');
  return {
    root: () => base || '/',
    object: (o: Pick<DatabaseObject, 'schema' | 'name' | 'objectType'>) =>
      o.objectType === 'View'
        ? `${base}/views/${encodeURIComponent(o.schema)}/${encodeURIComponent(o.name)}`
        : `${base}/tables/${encodeURIComponent(o.schema)}/${encodeURIComponent(o.name)}`,
    settings: () => `${base}/settings`,
  };
}
