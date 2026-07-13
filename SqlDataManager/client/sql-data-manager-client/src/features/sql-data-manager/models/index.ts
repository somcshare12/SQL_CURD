// ---------------------------------------------------------------------------
// Public model types for the SQL Data Manager feature module.
//
// These mirror the backend Contracts DTOs one-to-one. Keeping them in a single
// `models` folder means both the feature internals and any parent application
// (which imports them through the feature's index.ts) share the exact same
// vocabulary.
// ---------------------------------------------------------------------------

export type DatabaseObjectType = 'Table' | 'View';

export interface Permissions {
  canRead: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

export interface DatabaseObject {
  schema: string;
  name: string;
  objectType: DatabaseObjectType;
  fullName: string;
  permissions: Permissions;
}

export type SensitivityBehavior = 'Visible' | 'Masked' | 'Hidden' | 'ReadOnly';

export interface ColumnMetadata {
  name: string;
  ordinalPosition: number;
  sqlType: string;
  clrType: string;
  isNullable: boolean;
  maxLength: number | null;
  numericPrecision: number | null;
  numericScale: number | null;
  hasDefault: boolean;
  isIdentity: boolean;
  isComputed: boolean;
  isPrimaryKey: boolean;
  isRowVersion: boolean;
  isInsertable: boolean;
  isUpdatable: boolean;
  isFilterable: boolean;
  isSortable: boolean;
  isSearchable: boolean;
  sensitivity: SensitivityBehavior;
  displayName: string | null;
}

export interface TableMetadata {
  schema: string;
  name: string;
  objectType: DatabaseObjectType;
  fullName: string;
  permissions: Permissions;
  columns: ColumnMetadata[];
  keyColumns: string[];
  rowVersionColumn: string | null;
  hasStableKey: boolean;
}

// Alias kept for the public interface names required by the specification.
export type ObjectMetadata = TableMetadata;

export interface ModuleInfo {
  enabled: boolean;
  displayName: string;
  databaseDisplayName: string;
  maximumPageSize: number;
  defaultPageSize: number;
  allowedPageSizes: number[];
  supportedFeatures: string[];
  requireAuthentication: boolean;
}

// ---- Query ----------------------------------------------------------------

export type SortDirection = 'asc' | 'desc';

export interface SortSpec {
  column: string;
  direction: SortDirection;
}

export type FilterOperator =
  | 'contains' | 'doesNotContain' | 'startsWith' | 'endsWith'
  | 'equals' | 'doesNotEqual'
  | 'greaterThan' | 'greaterThanOrEqual' | 'lessThan' | 'lessThanOrEqual' | 'between'
  | 'isEmpty' | 'isNotEmpty' | 'isNull' | 'isNotNull'
  | 'isTrue' | 'isFalse';

export interface FilterSpec {
  column: string;
  operator: FilterOperator;
  value?: unknown;
  secondValue?: unknown;
}

export interface QueryRequest {
  page: number;
  pageSize: number;
  selectedColumns?: string[];
  sort?: SortSpec[];
  filters?: FilterSpec[];
  globalSearch?: string;
  includeTotalCount?: boolean;
}

export type Row = Record<string, unknown>;

export interface QueryResult {
  rows: Row[];
  page: number;
  pageSize: number;
  totalCount: number | null;
  columns: string[];
  unstableOrdering: boolean;
}

// ---- Mutations ------------------------------------------------------------

export interface RecordKeyValue {
  column: string;
  value: unknown;
}

export interface FieldValue {
  column: string;
  value: unknown;
}

export interface CreateRecordRequest {
  fields: FieldValue[];
}

export interface UpdateRecordRequest {
  keys: RecordKeyValue[];
  changedFields: FieldValue[];
  concurrencyToken?: string | null;
  originalValues?: FieldValue[];
}

export interface DeleteRecordRequest {
  keys: RecordKeyValue[];
  concurrencyToken?: string | null;
}

export interface RecordMutationResult {
  success: boolean;
  affectedRows: number;
  generatedKeys?: Record<string, unknown> | null;
  record?: Record<string, unknown> | null;
}

// ---- Errors ---------------------------------------------------------------

export interface SqlDataManagerError {
  code: string;
  message: string;
  traceId?: string;
  validationErrors?: Record<string, string[]> | null;
  httpStatus?: number;
}

// ---- Settings -------------------------------------------------------------

export interface ObjectLayout {
  pageSize?: number;
  hiddenColumns?: string[];
  columnOrder?: string[];
  columnWidths?: Record<string, number>;
  sort?: SortSpec[];
  density?: 'comfortable' | 'compact';
}

export interface UserSettings {
  lastSelectedObject?: string | null;
  treeCollapsed?: boolean;
  treePanelWidth?: number | null;
  expandedTreeNodes?: string[];
  theme?: 'light' | 'dark' | 'system' | null;
  recentObjects?: string[];
  objectLayouts?: Record<string, ObjectLayout>;
}

// ---- Component props (public) ---------------------------------------------

export interface SqlDataManagerOptions {
  apiBaseUrl?: string;
  basePath?: string;
  embedded?: boolean;
}

export interface SqlDataManagerPageProps {
  apiBaseUrl?: string;
  basePath?: string;
  initialSchema?: string;
  initialObject?: string;
  showHeader?: boolean;
  showNavigationRail?: boolean;
  showObjectTree?: boolean;
  showConnectionStatus?: boolean;
  embedded?: boolean;
  onObjectSelected?: (object: DatabaseObject) => void;
  onMutationCompleted?: (result: RecordMutationResult) => void;
  onError?: (error: SqlDataManagerError) => void;
}
