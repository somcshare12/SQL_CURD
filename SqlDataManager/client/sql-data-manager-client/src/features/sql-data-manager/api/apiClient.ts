import type {
  CreateRecordRequest,
  DatabaseObject,
  DeleteRecordRequest,
  ModuleInfo,
  QueryRequest,
  QueryResult,
  RecordKeyValue,
  RecordMutationResult,
  Row,
  TableMetadata,
  UpdateRecordRequest,
  UserSettings,
} from '../models';

/**
 * The API surface the UI depends on. UI components NEVER call `fetch`
 * directly — they always go through this interface. A parent application can
 * therefore inject its own authenticated implementation without touching any
 * component.
 */
export interface SqlDataManagerApiClient {
  getInfo(): Promise<ModuleInfo>;
  getObjects(): Promise<DatabaseObject[]>;
  getMetadata(schema: string, objectName: string): Promise<TableMetadata>;
  queryRows(schema: string, objectName: string, request: QueryRequest): Promise<QueryResult>;
  readRow(schema: string, objectName: string, keys: RecordKeyValue[]): Promise<Row>;
  createRow(schema: string, objectName: string, request: CreateRecordRequest): Promise<RecordMutationResult>;
  updateRow(schema: string, objectName: string, request: UpdateRecordRequest): Promise<RecordMutationResult>;
  deleteRow(schema: string, objectName: string, request: DeleteRecordRequest): Promise<RecordMutationResult>;
  getSettings(): Promise<UserSettings>;
  saveSettings(settings: UserSettings): Promise<void>;
  resetSettings(): Promise<void>;
  refreshMetadata(): Promise<void>;
}

/** Error thrown for every non-OK API response, carrying the coded error body. */
export class ApiError extends Error {
  readonly code: string;
  readonly httpStatus: number;
  readonly traceId?: string;
  readonly validationErrors?: Record<string, string[]> | null;

  constructor(
    code: string,
    message: string,
    httpStatus: number,
    traceId?: string,
    validationErrors?: Record<string, string[]> | null,
  ) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.httpStatus = httpStatus;
    this.traceId = traceId;
    this.validationErrors = validationErrors;
  }
}

/**
 * The default HTTP client. All requests are relative to `baseUrl` (the module's
 * configurable API prefix) so the same client works standalone or embedded
 * behind a different route.
 */
export class HttpSqlDataManagerApiClient implements SqlDataManagerApiClient {
  private readonly baseUrl: string;

  constructor(baseUrl: string) {
    // Normalise so we never produce double slashes.
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  getInfo() {
    return this.request<ModuleInfo>('GET', '/info');
  }

  getObjects() {
    return this.request<DatabaseObject[]>('GET', '/objects');
  }

  getMetadata(schema: string, objectName: string) {
    return this.request<TableMetadata>('GET', `/objects/${enc(schema)}/${enc(objectName)}/metadata`);
  }

  queryRows(schema: string, objectName: string, request: QueryRequest) {
    return this.request<QueryResult>('POST', `/objects/${enc(schema)}/${enc(objectName)}/rows/query`, request);
  }

  readRow(schema: string, objectName: string, keys: RecordKeyValue[]) {
    return this.request<{ record: Row }>('POST', `/objects/${enc(schema)}/${enc(objectName)}/rows/read`, { keys })
      .then((r) => r.record);
  }

  createRow(schema: string, objectName: string, request: CreateRecordRequest) {
    return this.request<RecordMutationResult>('POST', `/objects/${enc(schema)}/${enc(objectName)}/rows`, request);
  }

  updateRow(schema: string, objectName: string, request: UpdateRecordRequest) {
    return this.request<RecordMutationResult>('PUT', `/objects/${enc(schema)}/${enc(objectName)}/rows`, request);
  }

  deleteRow(schema: string, objectName: string, request: DeleteRecordRequest) {
    return this.request<RecordMutationResult>('DELETE', `/objects/${enc(schema)}/${enc(objectName)}/rows`, request);
  }

  getSettings() {
    return this.request<UserSettings>('GET', '/settings');
  }

  async saveSettings(settings: UserSettings) {
    await this.request<void>('PUT', '/settings', settings);
  }

  async resetSettings() {
    await this.request<void>('DELETE', '/settings');
  }

  async refreshMetadata() {
    await this.request<void>('POST', '/metadata/refresh');
  }

  /** Single choke-point for HTTP: builds the request and maps error bodies. */
  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    const response = await fetch(this.baseUrl + path, {
      method,
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      let code = 'UNEXPECTED_ERROR';
      let message = `Request failed with status ${response.status}.`;
      let traceId: string | undefined;
      let validationErrors: Record<string, string[]> | null | undefined;

      try {
        const errorBody = await response.json();
        code = errorBody.code ?? code;
        message = errorBody.message ?? message;
        traceId = errorBody.traceId;
        validationErrors = errorBody.validationErrors;
      } catch {
        // Non-JSON error body; keep the defaults.
      }

      throw new ApiError(code, message, response.status, traceId, validationErrors);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }
}

const enc = encodeURIComponent;
