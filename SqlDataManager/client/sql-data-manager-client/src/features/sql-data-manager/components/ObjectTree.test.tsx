import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ObjectTree } from './ObjectTree';
import type { DatabaseObject } from '../models';

const perms = { canRead: true, canCreate: true, canUpdate: true, canDelete: true };
const objects: DatabaseObject[] = [
  { schema: 'dbo', name: 'Customers', objectType: 'Table', fullName: 'dbo.Customers', permissions: perms },
  { schema: 'sales', name: 'Orders', objectType: 'Table', fullName: 'sales.Orders', permissions: perms },
  { schema: 'reporting', name: 'MonthlySales', objectType: 'View', fullName: 'reporting.MonthlySales', permissions: { ...perms, canCreate: false, canUpdate: false, canDelete: false } },
];

describe('ObjectTree', () => {
  it('renders Tables and Views grouped by schema', () => {
    render(<ObjectTree objects={objects} onSelect={() => {}} />);
    expect(screen.getByText('Tables')).toBeDefined();
    expect(screen.getByText('Views')).toBeDefined();
    expect(screen.getByText('Customers')).toBeDefined();
    expect(screen.getByText('MonthlySales')).toBeDefined();
  });

  it('invokes onSelect when an object is clicked', () => {
    const onSelect = vi.fn();
    render(<ObjectTree objects={objects} onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Customers'));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ fullName: 'dbo.Customers' }));
  });

  it('filters objects by search term across schema and name', () => {
    render(<ObjectTree objects={objects} onSelect={() => {}} />);
    fireEvent.change(screen.getByLabelText('Search database objects'), { target: { value: 'order' } });
    expect(screen.getByText('Orders')).toBeDefined();
    expect(screen.queryByText('Customers')).toBeNull();
  });
});
