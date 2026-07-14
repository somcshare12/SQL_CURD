import { useEffect, useMemo, useRef, useState } from 'react';
import { GridToolbar } from './GridToolbar';
import { Pagination } from './Pagination';
import { operatorsFor } from './filterOperators';
import { useRows } from '../hooks/queries';
import { useObjectLayout } from '../hooks/useObjectLayout';
import { useDebounce } from '../hooks/useDebounce';
import type {
  ColumnMetadata,
  FilterOperator,
  FilterSpec,
  ModuleInfo,
  Row,
  SortSpec,
  TableMetadata,
} from '../models';
import { formatValue, isNumericColumn } from '../utilities/format';

interface DataGridProps {
  metadata: TableMetadata;
  info: ModuleInfo;
  onCreate: () => void;
  onRowSelected: (row: Row) => void;
}

interface FilterState {
  operator: FilterOperator;
  value: string;
  secondValue: string;
}

/**
 * The server-side data grid. Pagination, sorting and filtering are all executed
 * by SQL Server (the grid only ever holds one page). Column visibility, sort
 * and page size are persisted per object via the settings store.
 */
export function DataGrid({ metadata, info, onCreate, onRowSelected }: DataGridProps) {
  const { layout, saveLayout, isLoaded } = useObjectLayout(metadata.fullName);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(info.defaultPageSize);
  const [sort, setSort] = useState<SortSpec[]>([]);
  const [hiddenColumns, setHiddenColumns] = useState<Set<string>>(new Set());
  const [filters, setFilters] = useState<Record<string, FilterState>>({});
  const [globalSearchInput, setGlobalSearchInput] = useState('');

  // Load persisted layout once it arrives (only when switching objects).
  const appliedLayoutFor = useRef<string | null>(null);
  useEffect(() => {
    if (!isLoaded || appliedLayoutFor.current === metadata.fullName) return;
    appliedLayoutFor.current = metadata.fullName;
    setHiddenColumns(new Set(layout?.hiddenColumns ?? []));
    setSort(layout?.sort ?? []);
    setPageSize(layout?.pageSize ?? info.defaultPageSize);
    setPage(1);
    setFilters({});
    setGlobalSearchInput('');
  }, [isLoaded, layout, metadata.fullName, info.defaultPageSize]);

  // Debounce text-driven inputs so we do not query on every keystroke (§20).
  const debouncedFilters = useDebounce(filters, 350);
  const debouncedSearch = useDebounce(globalSearchInput, 350);

  const filterSpecs = useMemo(() => buildFilterSpecs(metadata.columns, debouncedFilters), [metadata.columns, debouncedFilters]);

  const query = useRows(metadata.schema, metadata.name, {
    page,
    pageSize,
    sort,
    filters: filterSpecs,
    globalSearch: debouncedSearch || undefined,
    includeTotalCount: true,
  });

  const visibleColumns = metadata.columns.filter((c) => !hiddenColumns.has(c.name) && c.sensitivity !== 'Hidden');

  function persist(next: Partial<{ hiddenColumns: Set<string>; sort: SortSpec[]; pageSize: number }>) {
    saveLayout({
      hiddenColumns: [...(next.hiddenColumns ?? hiddenColumns)],
      sort: next.sort ?? sort,
      pageSize: next.pageSize ?? pageSize,
    });
  }

  function toggleColumn(name: string) {
    const next = new Set(hiddenColumns);
    if (next.has(name)) next.delete(name);
    else if (visibleColumns.length > 1) next.add(name); // keep at least one visible
    setHiddenColumns(next);
    persist({ hiddenColumns: next });
  }

  function handleSort(column: ColumnMetadata, multi: boolean) {
    if (!column.isSortable) return;
    const next = cycleSort(sort, column.name, multi);
    setSort(next);
    setPage(1);
    persist({ sort: next });
  }

  function changePageSize(size: number) {
    setPageSize(size);
    setPage(1);
    persist({ pageSize: size });
  }

  function setFilter(column: string, patch: Partial<FilterState>) {
    setFilters((prev) => {
      const existing = prev[column] ?? { operator: operatorsFor(byName(metadata, column))[0].value, value: '', secondValue: '' };
      return { ...prev, [column]: { ...existing, ...patch } };
    });
    setPage(1);
  }

  const rows = query.data?.rows ?? [];

  return (
    <div className="sdm-grid">
      <GridToolbar
        metadata={metadata}
        columns={metadata.columns}
        hiddenColumns={hiddenColumns}
        activeFilterCount={filterSpecs.length}
        totalCount={query.data?.totalCount ?? null}
        rowCount={rows.length}
        globalSearch={globalSearchInput}
        fetching={query.isFetching}
        onCreate={onCreate}
        onRefresh={() => query.refetch()}
        onToggleColumn={toggleColumn}
        onShowAllColumns={() => {
          setHiddenColumns(new Set());
          persist({ hiddenColumns: new Set() });
        }}
        onClearFilters={() => setFilters({})}
        onResetLayout={() => {
          setHiddenColumns(new Set());
          setSort([]);
          setPageSize(info.defaultPageSize);
          persist({ hiddenColumns: new Set(), sort: [], pageSize: info.defaultPageSize });
        }}
        onGlobalSearch={setGlobalSearchInput}
      />

      {query.data?.unstableOrdering && (
        <p className="sdm-grid__warning" role="status">
          ⚠ This object has no stable key; page results may shift between requests.
        </p>
      )}

      <div className={`sdm-grid__scroll ${query.isFetching ? 'is-fetching' : ''}`}>
        <table className="sdm-grid__table">
          <thead>
            <tr>
              {visibleColumns.map((column) => {
                const sortEntry = sort.find((s) => s.column === column.name);
                return (
                  <th
                    key={column.name}
                    aria-sort={sortEntry ? (sortEntry.direction === 'asc' ? 'ascending' : 'descending') : 'none'}
                  >
                    <button
                      type="button"
                      className="sdm-grid__header"
                      onClick={(e) => handleSort(column, e.shiftKey)}
                      title={column.isSortable ? 'Sort' : 'Not sortable'}
                    >
                      <span>{column.displayName ?? column.name}</span>
                      {column.isPrimaryKey && <span className="sdm-grid__pk" title="Primary key">🔑</span>}
                      {sortEntry && <span aria-hidden>{sortEntry.direction === 'asc' ? '▲' : '▼'}</span>}
                    </button>
                  </th>
                );
              })}
            </tr>
            <tr className="sdm-grid__filter-row">
              {visibleColumns.map((column) => (
                <th key={column.name}>
                  {column.isFilterable && <FilterCell column={column} state={filters[column.name]} onChange={(p) => setFilter(column.name, p)} />}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={rowKey(metadata, row, index)} onClick={() => onRowSelected(row)} tabIndex={0}
                  onKeyDown={(e) => e.key === 'Enter' && onRowSelected(row)}>
                {visibleColumns.map((column) => {
                  const value = row[column.name];
                  const isNull = value === null || value === undefined;
                  return (
                    <td key={column.name} className={`${isNumericColumn(column) ? 'is-numeric' : ''} ${isNull ? 'is-null' : ''}`}>
                      {formatValue(value, column)}
                    </td>
                  );
                })}
              </tr>
            ))}
            {rows.length === 0 && !query.isLoading && (
              <tr>
                <td colSpan={visibleColumns.length} className="sdm-grid__empty">
                  No rows to display.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination
        page={page}
        pageSize={pageSize}
        rowCount={rows.length}
        totalCount={query.data?.totalCount ?? null}
        allowedPageSizes={info.allowedPageSizes}
        onPageChange={setPage}
        onPageSizeChange={changePageSize}
      />
    </div>
  );
}

function FilterCell({
  column,
  state,
  onChange,
}: {
  column: ColumnMetadata;
  state?: FilterState;
  onChange: (patch: Partial<FilterState>) => void;
}) {
  const ops = operatorsFor(column);
  const operator = state?.operator ?? ops[0].value;
  const opDef = ops.find((o) => o.value === operator);

  return (
    <div className="sdm-grid__filter">
      <select value={operator} onChange={(e) => onChange({ operator: e.target.value as FilterOperator })} aria-label={`Filter operator for ${column.name}`}>
        {ops.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
      {!opDef?.unary && (
        <input
          value={state?.value ?? ''}
          onChange={(e) => onChange({ value: e.target.value })}
          placeholder="value"
          aria-label={`Filter value for ${column.name}`}
        />
      )}
      {opDef?.range && (
        <input
          value={state?.secondValue ?? ''}
          onChange={(e) => onChange({ secondValue: e.target.value })}
          placeholder="and"
          aria-label={`Second filter value for ${column.name}`}
        />
      )}
    </div>
  );
}

// ---- helpers --------------------------------------------------------------

function buildFilterSpecs(columns: ColumnMetadata[], filters: Record<string, FilterState>): FilterSpec[] {
  const specs: FilterSpec[] = [];
  for (const [column, state] of Object.entries(filters)) {
    const colMeta = columns.find((c) => c.name === column);
    if (!colMeta) continue;
    const opDef = operatorsFor(colMeta).find((o) => o.value === state.operator);
    if (!opDef) continue;

    if (opDef.unary) {
      specs.push({ column, operator: state.operator });
    } else if (opDef.range) {
      if (state.value !== '' && state.secondValue !== '') {
        specs.push({ column, operator: state.operator, value: state.value, secondValue: state.secondValue });
      }
    } else if (state.value !== '') {
      specs.push({ column, operator: state.operator, value: state.value });
    }
  }
  return specs;
}

/** Cycles a column through asc → desc → (removed). Shift enables multi-sort. */
function cycleSort(sort: SortSpec[], column: string, multi: boolean): SortSpec[] {
  const existing = sort.find((s) => s.column === column);
  const nextDirection = !existing ? 'asc' : existing.direction === 'asc' ? 'desc' : null;

  if (!multi) {
    return nextDirection ? [{ column, direction: nextDirection }] : [];
  }

  const others = sort.filter((s) => s.column !== column);
  return nextDirection ? [...others, { column, direction: nextDirection }] : others;
}

function byName(metadata: TableMetadata, column: string): ColumnMetadata {
  return metadata.columns.find((c) => c.name === column)!;
}

function rowKey(metadata: TableMetadata, row: Row, index: number): string {
  if (metadata.keyColumns.length > 0) {
    return metadata.keyColumns.map((k) => String(row[k])).join('|');
  }
  return String(index);
}
