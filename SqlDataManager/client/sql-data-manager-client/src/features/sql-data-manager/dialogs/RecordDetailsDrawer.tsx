import type { Row, TableMetadata } from '../models';
import { formatValue, NULL_DISPLAY } from '../utilities/format';

interface RecordDetailsDrawerProps {
  metadata: TableMetadata;
  record: Row;
  onClose: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
}

/**
 * A side drawer showing every permitted column of a single record. NULL is
 * rendered distinctly from an empty string, primary-key columns are marked, and
 * Edit/Delete actions appear only when permitted. Works in both standalone and
 * embedded modes.
 */
export function RecordDetailsDrawer({ metadata, record, onClose, onEdit, onDelete }: RecordDetailsDrawerProps) {
  return (
    <aside className="sdm-drawer" role="dialog" aria-label={`${metadata.name} record details`}>
      <header className="sdm-drawer__header">
        <h2>Record details</h2>
        <button type="button" className="sdm-modal__close" aria-label="Close details" onClick={onClose}>
          ×
        </button>
      </header>

      <div className="sdm-drawer__body">
        <dl className="sdm-details">
          {metadata.columns
            .filter((c) => c.sensitivity !== 'Hidden')
            .map((column) => {
              const value = record[column.name];
              const isNull = value === null || value === undefined;
              return (
                <div key={column.name} className="sdm-details__row">
                  <dt>
                    {column.name}
                    {column.isPrimaryKey && (
                      <span className="sdm-details__pk" title="Primary key">
                        🔑
                      </span>
                    )}
                  </dt>
                  <dd className={isNull ? 'is-null' : ''}>
                    {isNull ? NULL_DISPLAY : formatValue(value, column)}
                    {!isNull && typeof value === 'string' && (
                      <button
                        type="button"
                        className="sdm-details__copy"
                        title="Copy to clipboard"
                        onClick={() => navigator.clipboard?.writeText(String(value))}
                      >
                        ⧉
                      </button>
                    )}
                  </dd>
                </div>
              );
            })}
        </dl>
      </div>

      <footer className="sdm-drawer__footer">
        {onEdit && metadata.permissions.canUpdate && (
          <button type="button" className="sdm-btn" onClick={onEdit}>
            Edit
          </button>
        )}
        {onDelete && metadata.permissions.canDelete && (
          <button type="button" className="sdm-btn sdm-btn--danger" onClick={onDelete}>
            Delete
          </button>
        )}
      </footer>
    </aside>
  );
}
