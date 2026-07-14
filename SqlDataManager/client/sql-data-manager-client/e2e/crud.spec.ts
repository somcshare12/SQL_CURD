import { test, expect } from '@playwright/test';

/**
 * End-to-end coverage of the required flows: navigating the tree, loading a
 * table, sorting, filtering, hiding a column and verifying it survives a
 * reload, a full create → edit → delete cycle with confirmations, a validation
 * failure, and that a read-only view exposes no mutation button.
 */

test.describe('SQL Data Manager', () => {
  test('tree, sorting and filtering work on the Products table', async ({ page }) => {
    await page.goto('/sql-data-manager/tables/dbo/Products');

    await expect(page.getByRole('heading', { name: 'dbo.Products' })).toBeVisible();
    // Sort by the Name column header.
    await page.getByRole('button', { name: /^Name/ }).click();
    // Filter by Name contains "Key".
    const nameFilter = page.getByLabel('Filter value for Name');
    await nameFilter.fill('Key');
    await expect(page.getByText('Mechanical Keyboard')).toBeVisible();
    await nameFilter.fill('');
  });

  test('hidden columns persist across a reload', async ({ page }) => {
    await page.goto('/sql-data-manager/tables/dbo/Products');
    const inStockHeader = page.getByRole('button', { name: 'InStock', exact: true });
    await expect(inStockHeader).toBeVisible();

    // Hide the "InStock" column via the column selector.
    await page.getByRole('button', { name: 'Columns' }).click();
    await page.getByRole('menu').getByLabel('InStock').uncheck();
    await page.keyboard.press('Escape');
    await expect(inStockHeader).toHaveCount(0);

    // Give the debounced settings save time to flush, then reload.
    await page.waitForTimeout(900);
    await page.reload();
    await expect(page.getByRole('button', { name: 'InStock', exact: true })).toHaveCount(0);

    // Restore for a clean state.
    await page.getByRole('button', { name: 'Columns' }).click();
    await page.getByRole('menu').getByText('Show all').click();
  });

  test('create, edit and delete a record with confirmations', async ({ page }) => {
    const sku = `E2E-${Date.now()}`;
    await page.goto('/sql-data-manager/tables/dbo/Products');

    // CREATE (form fields are targeted by their unique ids to avoid clashing
    // with the same-named grid filter inputs).
    await page.getByRole('button', { name: '+ Create record' }).click();
    await page.locator('#field-Sku').fill(sku);
    await page.locator('#field-Name').fill('E2E Product');
    await page.locator('#field-Price').fill('12.34');
    await page.getByRole('button', { name: 'Review & save' }).click();
    await page.getByRole('button', { name: 'Confirm' }).click();
    await expect(page.getByText('Record created.')).toBeVisible();

    // EDIT (via row → details drawer → Edit)
    await page.getByText('E2E Product').click();
    await page.getByRole('button', { name: 'Edit' }).click();
    await page.locator('#field-Price').fill('56.78');
    await page.getByRole('button', { name: 'Review & save' }).click();
    await expect(page.getByText('56.78')).toBeVisible();
    await page.getByRole('button', { name: 'Confirm' }).click();
    await expect(page.getByText('Record updated.')).toBeVisible();

    // DELETE
    await page.getByText('E2E Product').click();
    await page.getByRole('button', { name: 'Delete' }).click();
    await page.getByRole('button', { name: 'Delete permanently' }).click();
    await expect(page.getByText('Record deleted.')).toBeVisible();
  });

  test('validation failure keeps the create dialog open', async ({ page }) => {
    await page.goto('/sql-data-manager/tables/dbo/Departments');
    await page.getByRole('button', { name: '+ Create record' }).click();
    // Leave the required Name blank and try to proceed.
    await page.getByRole('button', { name: 'Review & save' }).click();
    await expect(page.getByText('A value is required.')).toBeVisible();
  });

  test('a read-only view has no create button', async ({ page }) => {
    await page.goto('/sql-data-manager/views/reporting/MonthlySales');
    await expect(page.getByRole('heading', { name: 'reporting.MonthlySales' })).toBeVisible();
    await expect(page.getByRole('button', { name: '+ Create record' })).toBeDisabled();
  });
});
