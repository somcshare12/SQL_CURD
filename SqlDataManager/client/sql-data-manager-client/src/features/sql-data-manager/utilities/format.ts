import type { ColumnMetadata } from '../models';

/** A sentinel used to render SQL NULL distinctly from an empty string. */
export const NULL_DISPLAY = '(null)';

/** Formats a raw cell value for display in the grid / details view. */
export function formatValue(value: unknown, column?: ColumnMetadata): string {
  if (value === null || value === undefined) {
    return NULL_DISPLAY;
  }

  if (typeof value === 'boolean') {
    return value ? 'true' : 'false';
  }

  if (typeof value === 'string') {
    if (value === '') {
      return '(empty)';
    }
    return value;
  }

  if (typeof value === 'number') {
    return String(value);
  }

  // Dates arrive as ISO strings; keep them readable and consistent.
  if (column && column.clrType === 'DateTime' && typeof value === 'string') {
    return value;
  }

  return String(value);
}

/** True when a column represents a numeric type (right-aligned in the grid). */
export function isNumericColumn(column: ColumnMetadata): boolean {
  return ['Int32', 'Int64', 'Int16', 'Byte', 'Decimal', 'Double', 'Single'].includes(column.clrType);
}

/** Picks the HTML input type best suited to a column for the record form. */
export function inputTypeFor(column: ColumnMetadata): 'text' | 'number' | 'date' | 'datetime-local' | 'checkbox' {
  switch (column.clrType) {
    case 'Boolean':
      return 'checkbox';
    case 'Int32':
    case 'Int64':
    case 'Int16':
    case 'Byte':
    case 'Decimal':
    case 'Double':
    case 'Single':
      return 'number';
    case 'DateTime':
    case 'DateTimeOffset':
      return column.sqlType === 'date' ? 'date' : 'datetime-local';
    default:
      return 'text';
  }
}
