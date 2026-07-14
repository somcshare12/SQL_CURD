interface PaginationProps {
  page: number;
  pageSize: number;
  rowCount: number;
  totalCount: number | null;
  allowedPageSizes: number[];
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

/** Server-side pagination controls with first/prev/next/last and a page jump. */
export function Pagination({
  page,
  pageSize,
  rowCount,
  totalCount,
  allowedPageSizes,
  onPageChange,
  onPageSizeChange,
}: PaginationProps) {
  const lastPage = totalCount !== null ? Math.max(1, Math.ceil(totalCount / pageSize)) : null;
  const from = rowCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = (page - 1) * pageSize + rowCount;

  return (
    <div className="sdm-pagination" role="navigation" aria-label="Pagination">
      <div className="sdm-pagination__range">
        {from}–{to}
        {totalCount !== null ? ` of ${totalCount}` : ''}
      </div>

      <div className="sdm-pagination__controls">
        <button type="button" className="sdm-btn" onClick={() => onPageChange(1)} disabled={page <= 1} aria-label="First page">
          «
        </button>
        <button type="button" className="sdm-btn" onClick={() => onPageChange(page - 1)} disabled={page <= 1} aria-label="Previous page">
          ‹
        </button>
        <span className="sdm-pagination__page">
          Page
          <input
            type="number"
            min={1}
            max={lastPage ?? undefined}
            value={page}
            onChange={(e) => {
              const next = Number(e.target.value);
              if (next >= 1) onPageChange(next);
            }}
            aria-label="Current page"
          />
          {lastPage !== null && <span> of {lastPage}</span>}
        </span>
        <button
          type="button"
          className="sdm-btn"
          onClick={() => onPageChange(page + 1)}
          disabled={rowCount < pageSize || (lastPage !== null && page >= lastPage)}
          aria-label="Next page"
        >
          ›
        </button>
        <button
          type="button"
          className="sdm-btn"
          onClick={() => lastPage && onPageChange(lastPage)}
          disabled={lastPage === null || page >= lastPage}
          aria-label="Last page"
        >
          »
        </button>
      </div>

      <label className="sdm-pagination__size">
        Rows
        <select value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))} aria-label="Rows per page">
          {allowedPageSizes.map((size) => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
