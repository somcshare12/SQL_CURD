import type { ColumnMetadata, FilterOperator } from '../models';

export interface OperatorOption {
  value: FilterOperator;
  label: string;
  /** Unary operators need no value input. */
  unary?: boolean;
  /** BETWEEN needs a second value input. */
  range?: boolean;
}

const TEXT_OPS: OperatorOption[] = [
  { value: 'contains', label: 'contains' },
  { value: 'doesNotContain', label: 'does not contain' },
  { value: 'startsWith', label: 'starts with' },
  { value: 'endsWith', label: 'ends with' },
  { value: 'equals', label: 'equals' },
  { value: 'doesNotEqual', label: 'not equal' },
  { value: 'isEmpty', label: 'is empty', unary: true },
  { value: 'isNotEmpty', label: 'is not empty', unary: true },
  { value: 'isNull', label: 'is null', unary: true },
  { value: 'isNotNull', label: 'is not null', unary: true },
];

const NUMERIC_OPS: OperatorOption[] = [
  { value: 'equals', label: '=' },
  { value: 'doesNotEqual', label: '≠' },
  { value: 'greaterThan', label: '>' },
  { value: 'greaterThanOrEqual', label: '≥' },
  { value: 'lessThan', label: '<' },
  { value: 'lessThanOrEqual', label: '≤' },
  { value: 'between', label: 'between', range: true },
  { value: 'isNull', label: 'is null', unary: true },
  { value: 'isNotNull', label: 'is not null', unary: true },
];

const BOOL_OPS: OperatorOption[] = [
  { value: 'isTrue', label: 'is true', unary: true },
  { value: 'isFalse', label: 'is false', unary: true },
  { value: 'isNull', label: 'is null', unary: true },
  { value: 'isNotNull', label: 'is not null', unary: true },
];

const IDENTIFIER_OPS: OperatorOption[] = [
  { value: 'equals', label: 'equals' },
  { value: 'doesNotEqual', label: 'not equal' },
  { value: 'isNull', label: 'is null', unary: true },
  { value: 'isNotNull', label: 'is not null', unary: true },
];

/** Returns the operators appropriate for a column's type. */
export function operatorsFor(column: ColumnMetadata): OperatorOption[] {
  switch (column.clrType) {
    case 'Boolean':
      return BOOL_OPS;
    case 'Guid':
      return IDENTIFIER_OPS;
    case 'Int32':
    case 'Int64':
    case 'Int16':
    case 'Byte':
    case 'Decimal':
    case 'Double':
    case 'Single':
    case 'DateTime':
    case 'DateTimeOffset':
      return NUMERIC_OPS;
    default:
      return TEXT_OPS;
  }
}

export function findOperator(column: ColumnMetadata, op: FilterOperator): OperatorOption | undefined {
  return operatorsFor(column).find((o) => o.value === op);
}
