import { useState } from 'react';
import type { ColumnMetadata, TableMetadata } from '../models';

interface GridToolbarProps {
  metadata: TableMetadata;
  columns: ColumnMetadata[];
  hiddenColumns: Set<string>;
  activeFilterCount: number;
  totalCount: number | null;
  rowCount: number;
  globalSearch: string;
  fetching: boolean;
  onCreate: () => void;
  onRefresh: () => void;
  onToggleColumn: (name: string) => void;
  onShowAllColumns: () => void;
  onClearFilters: () => void;
  onResetLayout: () => void;
  onGlobalSearch: (value: string) => void;
}

/** The grid toolbar: create, refresh, column selector, filters and search. */
export function GridToolbar(props: GridToolbarProps) {
  const [columnMenuOpen, setColumnMenuOpen] = useState(false);
  const { metadata } = props;

  return (
    <div className="sdm-toolbar">
      <div className="sdm-toolbar__left">
        <button
          type="button"
          className="sdm-btn sdm-btn--primary"
          onClick={props.onCreate}
          disabled={!metadata.permissions.canCreate}
          title={metadata.permissions.canCreate ? 'Create a new record' : 'Create is not permitted for this object'}
        >
          + Create record
        </button>

        <button type="button" className="sdm-btn" onClick={props.onRefresh} title="Refresh">
          ⟳ Refresh
        </button>

        <div className="sdm-toolbar__menu">
          <button
            type="button"
            className="sdm-btn"
            aria-expanded={columnMenuOpen}
            onClick={() => setColumnMenuOpen((v) => !v)}
          >
            ☰ Columns
          </button>
          {columnMenuOpen && (
            <div className="sdm-toolbar__dropdown" role="menu">
              <button type="button" className="sdm-link" onClick={props.onShowAllColumns}>
                Show all
              </button>
              <div className="sdm-toolbar__dropdown-list">
                {props.columns.map((column) => (
                  <label key={column.name} className="sdm-toolbar__column-toggle">
                    <input
                      type="checkbox"
                      checked={!props.hiddenColumns.has(column.name)}
                      onChange={() => props.onToggleColumn(column.name)}
                    />
                    {column.name}
                  </label>
                ))}
              </div>
            </div>
          )}
        </div>

        <button
          type="button"
          className="sdm-btn"
          onClick={props.onClearFilters}
          disabled={props.activeFilterCount === 0}
        >
          Clear filters
          {props.activeFilterCount > 0 && <span className="sdm-toolbar__badge">{props.activeFilterCount}</span>}
        </button>

        <button type="button" className="sdm-btn" onClick={props.onResetLayout} title="Reset column layout">
          Reset layout
        </button>
      </div>

      <div className="sdm-toolbar__right">
        {props.fetching && <span className="sdm-toolbar__fetching" aria-live="polite">Loading…</span>}
        <span className="sdm-toolbar__count">
          {props.totalCount !== null ? `${props.totalCount} rows` : `${props.rowCount} shown`}
        </span>
        <input
          type="search"
          className="sdm-toolbar__search"
          placeholder="Search all columns…"
          value={props.globalSearch}
          onChange={(e) => props.onGlobalSearch(e.target.value)}
          aria-label="Global search"
        />
      </div>
    </div>
  );
}
