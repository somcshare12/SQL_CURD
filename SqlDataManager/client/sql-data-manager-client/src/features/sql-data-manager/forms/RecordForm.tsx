import { useMemo, useState } from 'react';
import { Modal } from '../dialogs/Modal';
import { ApiError } from '../api/apiClient';
import { useCreateRow, useUpdateRow } from '../hooks/queries';
import type { ColumnMetadata, FieldValue, RecordKeyValue, Row, TableMetadata } from '../models';
import { formatValue, inputTypeFor } from '../utilities/format';

interface RecordFormProps {
  metadata: TableMetadata;
  mode: 'create' | 'edit';
  initialRecord?: Row;
  onClose: () => void;
  onCompleted: (message: string) => void;
}

interface FieldState {
  value: string;
  checked: boolean;
  isNull: boolean;
  touched: boolean;
}

/**
 * The metadata-driven create/edit form. It generates inputs from column
 * metadata, validates on the client, then shows a confirmation step (a values
 * summary for create, a field-level before/after diff for update) before
 * calling the API. Server validation errors and concurrency conflicts are
 * surfaced back onto the form.
 */
export function RecordForm({ metadata, mode, initialRecord, onClose, onCompleted }: RecordFormProps) {
  const editableColumns = useMemo(
    () => metadata.columns.filter((c) => (mode === 'create' ? c.isInsertable : c.isUpdatable)),
    [metadata, mode],
  );

  const [fields, setFields] = useState<Record<string, FieldState>>(() =>
    buildInitialState(editableColumns, initialRecord),
  );
  const [clientErrors, setClientErrors] = useState<Record<string, string>>({});
  const [serverErrors, setServerErrors] = useState<Record<string, string[]>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [step, setStep] = useState<'edit' | 'confirm'>('edit');

  const createMutation = useCreateRow(metadata.schema, metadata.name);
  const updateMutation = useUpdateRow(metadata.schema, metadata.name);
  const busy = createMutation.isPending || updateMutation.isPending;

  const changedFields = useMemo(
    () => computeChangedFields(editableColumns, fields, mode, initialRecord),
    [editableColumns, fields, mode, initialRecord],
  );

  function update(column: string, patch: Partial<FieldState>) {
    setFields((prev) => ({ ...prev, [column]: { ...prev[column], ...patch, touched: true } }));
  }

  function validate(): boolean {
    const errors: Record<string, string> = {};
    for (const column of editableColumns) {
      const state = fields[column.name];
      const supplied = state.isNull || state.value !== '' || column.clrType === 'Boolean';

      // Required = not nullable, no DB default, and (create) not supplied.
      if (mode === 'create' && !column.isNullable && !column.hasDefault && !supplied) {
        errors[column.name] = 'A value is required.';
      }
      if (state.isNull && !column.isNullable) {
        errors[column.name] = 'This column cannot be null.';
      }
      if (!state.isNull && column.maxLength && column.maxLength > 0 && state.value.length > column.maxLength) {
        errors[column.name] = `Maximum length is ${column.maxLength}.`;
      }
    }
    setClientErrors(errors);
    return Object.keys(errors).length === 0;
  }

  function goToConfirm() {
    setFormError(null);
    if (!validate()) return;
    if (mode === 'edit' && changedFields.length === 0) {
      setFormError('No changes to save.');
      return;
    }
    setStep('confirm');
  }

  async function submit() {
    setServerErrors({});
    setFormError(null);
    try {
      if (mode === 'create') {
        await createMutation.mutateAsync({ fields: changedFields });
        onCompleted('Record created.');
      } else {
        const keys = keyValues(metadata, initialRecord!);
        const token = metadata.rowVersionColumn ? (initialRecord![metadata.rowVersionColumn] as string) : null;
        await updateMutation.mutateAsync({ keys, changedFields, concurrencyToken: token });
        onCompleted('Record updated.');
      }
      onClose();
    } catch (error) {
      handleError(error);
      setStep('edit');
    }
  }

  function handleError(error: unknown) {
    if (error instanceof ApiError) {
      if (error.code === 'CONCURRENCY_CONFLICT') {
        setFormError('This record was changed by another user. Please close and reload before editing.');
        return;
      }
      if (error.validationErrors) {
        setServerErrors(error.validationErrors);
      }
      setFormError(error.message);
    } else {
      setFormError('An unexpected error occurred.');
    }
  }

  const title = mode === 'create' ? `Create ${metadata.name} record` : `Edit ${metadata.name} record`;

  return (
    <Modal
      title={step === 'confirm' ? `Confirm ${mode}` : title}
      onClose={onClose}
      width={640}
      footer={
        step === 'edit' ? (
          <>
            <button type="button" className="sdm-btn" onClick={onClose}>
              Cancel
            </button>
            <button type="button" className="sdm-btn sdm-btn--primary" onClick={goToConfirm}>
              Review &amp; save
            </button>
          </>
        ) : (
          <>
            <button type="button" className="sdm-btn" onClick={() => setStep('edit')} disabled={busy}>
              Back
            </button>
            <button type="button" className="sdm-btn sdm-btn--primary" onClick={submit} disabled={busy}>
              {busy ? 'Saving…' : 'Confirm'}
            </button>
          </>
        )
      }
    >
      {formError && (
        <p className="sdm-form__error" role="alert">
          {formError}
        </p>
      )}

      {step === 'edit' ? (
        <div className="sdm-form">
          {editableColumns.map((column) => (
            <Field
              key={column.name}
              column={column}
              state={fields[column.name]}
              error={clientErrors[column.name] ?? serverErrors[column.name]?.[0]}
              onChange={(patch) => update(column.name, patch)}
            />
          ))}
          {editableColumns.length === 0 && <p>This object has no editable columns.</p>}
        </div>
      ) : (
        <ConfirmSummary
          mode={mode}
          metadata={metadata}
          changedFields={changedFields}
          initialRecord={initialRecord}
        />
      )}
    </Modal>
  );
}

function Field({
  column,
  state,
  error,
  onChange,
}: {
  column: ColumnMetadata;
  state: FieldState;
  error?: string;
  onChange: (patch: Partial<FieldState>) => void;
}) {
  const inputType = inputTypeFor(column);
  const required = !column.isNullable && !column.hasDefault;
  const id = `field-${column.name}`;

  return (
    <div className={`sdm-form__field ${error ? 'has-error' : ''}`}>
      <label htmlFor={id}>
        {column.displayName ?? column.name}
        {required && <span className="sdm-form__required" aria-hidden> *</span>}
        <span className="sdm-form__type">{column.sqlType}</span>
      </label>

      {inputType === 'checkbox' ? (
        <input
          id={id}
          type="checkbox"
          checked={state.checked}
          disabled={state.isNull}
          onChange={(e) => onChange({ checked: e.target.checked })}
        />
      ) : (
        <input
          id={id}
          type={inputType}
          value={state.value}
          disabled={state.isNull}
          maxLength={column.maxLength && column.maxLength > 0 ? column.maxLength : undefined}
          onChange={(e) => onChange({ value: e.target.value })}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? `${id}-error` : undefined}
        />
      )}

      {column.isNullable && (
        <label className="sdm-form__null">
          <input type="checkbox" checked={state.isNull} onChange={(e) => onChange({ isNull: e.target.checked })} />
          NULL
        </label>
      )}

      {error && (
        <p id={`${id}-error`} className="sdm-form__field-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

function ConfirmSummary({
  mode,
  metadata,
  changedFields,
  initialRecord,
}: {
  mode: 'create' | 'edit';
  metadata: TableMetadata;
  changedFields: FieldValue[];
  initialRecord?: Row;
}) {
  return (
    <div className="sdm-confirm">
      <p>
        Target: <strong>{metadata.fullName}</strong>
      </p>
      {mode === 'create' ? (
        <>
          <p>The following values will be inserted:</p>
          <table className="sdm-confirm__table">
            <tbody>
              {changedFields.map((f) => (
                <tr key={f.column}>
                  <th>{f.column}</th>
                  <td>{formatValue(f.value)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="sdm-confirm__note">Columns not listed will use their database defaults.</p>
        </>
      ) : (
        <>
          <p>
            {changedFields.length} field(s) will change:
          </p>
          <table className="sdm-confirm__table">
            <thead>
              <tr>
                <th>Field</th>
                <th>Before</th>
                <th>After</th>
              </tr>
            </thead>
            <tbody>
              {changedFields.map((f) => (
                <tr key={f.column}>
                  <th>{f.column}</th>
                  <td className="sdm-confirm__before">{formatValue(initialRecord?.[f.column])}</td>
                  <td className="sdm-confirm__after">{formatValue(f.value)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}

// ---- helpers --------------------------------------------------------------

function buildInitialState(columns: ColumnMetadata[], record?: Row): Record<string, FieldState> {
  const state: Record<string, FieldState> = {};
  for (const column of columns) {
    const raw = record?.[column.name];
    state[column.name] = {
      value: raw === null || raw === undefined || typeof raw === 'boolean' ? '' : String(raw),
      checked: raw === true,
      isNull: record ? raw === null : false,
      touched: false,
    };
  }
  return state;
}

/** Produces the field values to send: for create, all supplied; for edit, changed only. */
function computeChangedFields(
  columns: ColumnMetadata[],
  fields: Record<string, FieldState>,
  mode: 'create' | 'edit',
  initialRecord?: Row,
): FieldValue[] {
  const result: FieldValue[] = [];
  for (const column of columns) {
    const state = fields[column.name];
    const value = readValue(column, state);

    if (mode === 'create') {
      // Only send fields the user actually supplied so DB defaults apply.
      if (state.isNull || state.value !== '' || column.clrType === 'Boolean') {
        result.push({ column: column.name, value });
      }
    } else {
      const original = initialRecord?.[column.name] ?? null;
      if (!valuesEqual(original, value)) {
        result.push({ column: column.name, value });
      }
    }
  }
  return result;
}

function readValue(column: ColumnMetadata, state: FieldState): unknown {
  if (state.isNull) return null;
  if (column.clrType === 'Boolean') return state.checked;
  if (['Int32', 'Int64', 'Int16', 'Byte', 'Decimal', 'Double', 'Single'].includes(column.clrType)) {
    return state.value === '' ? null : Number(state.value);
  }
  return state.value;
}

function valuesEqual(a: unknown, b: unknown): boolean {
  if (a === null || a === undefined) return b === null || b === undefined;
  return String(a) === String(b);
}

function keyValues(metadata: TableMetadata, record: Row): RecordKeyValue[] {
  return metadata.keyColumns.map((column) => ({ column, value: record[column] }));
}
