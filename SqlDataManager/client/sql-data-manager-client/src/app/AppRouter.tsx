import { Navigate, useRoutes } from 'react-router-dom';
import { createSqlDataManagerRoutes } from '../features/sql-data-manager';
import { BASE_PATH } from './AppProviders';

/**
 * The standalone router. It mounts the module's own routes (generated from the
 * configurable base path) and redirects the site root to the module.
 */
export function AppRouter() {
  const moduleRoutes = createSqlDataManagerRoutes(BASE_PATH, {
    showHeader: true,
    showNavigationRail: true,
    showObjectTree: true,
    showConnectionStatus: true,
  });

  return useRoutes([
    { path: '/', element: <Navigate to={BASE_PATH} replace /> },
    ...moduleRoutes,
    { path: '*', element: <Navigate to={BASE_PATH} replace /> },
  ]);
}
