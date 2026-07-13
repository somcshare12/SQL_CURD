import { describe, it, expect } from 'vitest';
import { formatValue, inputTypeFor, isNumericColumn, NULL_DISPLAY } from './format';
import { operatorsFor } from '../grid/filterOperators';
import type { ColumnMetadata } from '../models';

function column(partial: Partial<ColumnMetadata>): ColumnMetadata {
  return {
    name: 'C', ordinalPosition: 1, sqlType: 'nvarchar', clrType: 'String', isNullable: true,
    maxLength: null, numericPrecision: null, numericScale: null, hasDefault: false, isIdentity: false,
    isComputed: false, isPrimaryKey: false, isRowVersion: false, isInsertable: true, isUpdatable: true,
    isFilterable: true, isSortable: true, isSearchable: true, sensitivity: 'Visible', displayName: null,
    ...partial,
  };
}

describe('formatValue', () => {
  it('renders null distinctly from empty string', () => {
    expect(formatValue(null)).toBe(NULL_DISPLAY);
    expect(formatValue('')).toBe('(empty)');
  });

  it('renders booleans as words', () => {
    expect(formatValue(true)).toBe('true');
    expect(formatValue(false)).toBe('false');
  });
});

describe('column helpers', () => {
  it('detects numeric columns', () => {
    expect(isNumericColumn(column({ clrType: 'Decimal' }))).toBe(true);
    expect(isNumericColumn(column({ clrType: 'String' }))).toBe(false);
  });

  it('picks checkbox for boolean and number for numeric', () => {
    expect(inputTypeFor(column({ clrType: 'Boolean' }))).toBe('checkbox');
    expect(inputTypeFor(column({ clrType: 'Int32' }))).toBe('number');
    expect(inputTypeFor(column({ clrType: 'DateTime', sqlType: 'date' }))).toBe('date');
  });
});

describe('operatorsFor', () => {
  it('offers text operators for strings and boolean operators for bit', () => {
    expect(operatorsFor(column({ clrType: 'String' })).some((o) => o.value === 'contains')).toBe(true);
    expect(operatorsFor(column({ clrType: 'Boolean' })).some((o) => o.value === 'isTrue')).toBe(true);
  });
});
