// ---------------------------------------------------------------------------
// Public interface of the SQL Data Manager feature module.
//
// Parent applications import ONLY from this file. Everything else under
// features/sql-data-manager is an internal implementation detail and may change
// without notice. This is what keeps the module cleanly embeddable.
// ---------------------------------------------------------------------------

import './styles/styles.css';

export { SqlDataManagerPage } from './components/SqlDataManagerPage';
export { SqlDataManagerProvider } from './providers/SqlDataManagerProvider';
export { createSqlDataManagerRoutes } from './routes';
export { createSqlDataManagerNavigationNodes } from './navigation';

export { HttpSqlDataManagerApiClient, ApiError } from './api/apiClient';
export type { SqlDataManagerApiClient } from './api/apiClient';

export type {
  SqlDataManagerPageProps,
  SqlDataManagerOptions,
  DatabaseObject,
  TableMetadata,
  ObjectMetadata,
  ColumnMetadata,
  QueryRequest,
  QueryResult,
  RecordMutationResult,
  ModuleInfo,
  SqlDataManagerError,
  UserSettings,
} from './models';
