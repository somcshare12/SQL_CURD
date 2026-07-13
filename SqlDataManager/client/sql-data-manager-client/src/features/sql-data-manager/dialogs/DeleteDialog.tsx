import { useState } from 'react';
import { Modal } from './Modal';
import { ApiError } from '../api/apiClient';
import { useDeleteRow } from '../hooks/queries';
import type { RecordKeyValue, Row, TableMetadata } from '../models';
import { formatValue } from '../utilities/format';

interface DeleteDialogProps {
  metadata: TableMetadata;
  record: Row;
  requireTyped?: boolean;
  onClose: () => void;
  onCompleted: (message: string) => void;
}

/**
 * The always-required delete confirmation. It shows the schema/table, the
 * unique key, a safe row summary and a strong irreversible-action warning, and
 * uses a destructive style on the final button. Optionally the user must type
 * DELETE to enable it.
 */
export function DeleteDialog({ metadata, record, requireTyped, onClose, onCompleted }: DeleteDialogProps) {
  const [typed, setTyped] = useState('');
  const [error, setError] = useState<string | null>(null);
  const mutation = useDeleteRow(metadata.schema, metadata.name);

  const keys: RecordKeyValue[] = metadata.keyColumns.map((column) => ({ column, value: record[column] }));
  const typedOk = !requireTyped || typed === 'DELETE';

  async function confirm() {
    setError(null);
    try {
      const token = metadata.rowVersionColumn ? (record[metadata.rowVersionColumn] as string) : null;
      await mutation.mutateAsync({ keys, concurrencyToken: token });
      onCompleted('Record deleted.');
      onClose();
    } catch (e) {
      if (e instanceof ApiError) {
        setError(
          e.code === 'CONCURRENCY_CONFLICT'
            ? 'This record was changed by another user. Please reload before deleting.'
            : e.message,
        );
      } else {
        setError('An unexpected error occurred.');
      }
    }
  }

  return (
    <Modal
      title="Delete record"
      destructive
      onClose={onClose}
      footer={
        <>
          <button type="button" className="sdm-btn" onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </button>
          <button
            type="button"
            className="sdm-btn sdm-btn--danger"
            onClick={confirm}
            disabled={mutation.isPending || !typedOk}
          >
            {mutation.isPending ? 'Deleting…' : 'Delete permanently'}
          </button>
        </>
      }
    >
      {error && (
        <p className="sdm-form__error" role="alert">
          {error}
        </p>
      )}

      <p className="sdm-delete__warning" role="alert">
        This action is permanent and cannot be undone.
      </p>

      <p>
        Deleting from <strong>{metadata.fullName}</strong>
      </p>

      <table className="sdm-confirm__table">
        <tbody>
          {keys.map((k) => (
            <tr key={k.column}>
              <th>{k.column}</th>
              <td>{formatValue(k.value)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {requireTyped && (
        <label className="sdm-delete__typed">
          Type <code>DELETE</code> to confirm:
          <input value={typed} onChange={(e) => setTyped(e.target.value)} aria-label="Type DELETE to confirm" />
        </label>
      )}
    </Modal>
  );
}
