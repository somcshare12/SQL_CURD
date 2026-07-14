import type { RouteObject } from 'react-router-dom';
import { SqlDataManagerPage } from '../components/SqlDataManagerPage';
import type { SqlDataManagerPageProps } from '../models';

/**
 * Produces the module's routes for a given base path. Centralising route
 * definitions here (rather than scattering paths through components) keeps the
 * base path configurable and lets a parent router mount the module easily.
 */
export function createSqlDataManagerRoutes(
  basePath = '/sql-data-manager',
  pageProps: SqlDataManagerPageProps = {},
): RouteObject[] {
  const base = basePath.replace(/\/$/, '');
  const element = <SqlDataManagerPage {...pageProps} />;

  return [
    { path: base || '/', element },
    { path: `${base}/tables/:schema/:objectName`, element },
    { path: `${base}/views/:schema/:objectName`, element },
    { path: `${base}/settings`, element },
  ];
}
