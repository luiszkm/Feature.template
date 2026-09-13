import { expect, test } from '@playwright/test';
import { login } from './fixtures';

test('cria e elimina um utilizador', async ({ page }) => {
  const email = `e2e-${Date.now()}@example.com`;

  await login(page);

  await page.getByTestId('create-user').click();
  await page.getByTestId('email').fill(email);
  await page.getByTestId('password').fill('Str0ng@Pass1');
  await page.getByTestId('firstName').fill('E2E');
  await page.getByTestId('lastName').fill('Tester');
  await page.getByTestId('submit').click();

  await expect(page).toHaveURL(/\/users$/);

  await page.getByTestId('search').fill(email);
  const row = page.locator('tr', { hasText: email });
  await expect(row).toHaveCount(1);

  await row.getByRole('button', { name: 'Eliminar' }).click();
  await page.getByTestId('confirm-accept').click();

  await expect(page.locator('tr', { hasText: email })).toHaveCount(0);
});

test('edita um utilizador a partir da lista', async ({ page }) => {
  const email = `e2e-edit-${Date.now()}@example.com`;

  await login(page);

  await page.getByTestId('create-user').click();
  await page.getByTestId('email').fill(email);
  await page.getByTestId('password').fill('Str0ng@Pass1');
  await page.getByTestId('firstName').fill('Antes');
  await page.getByTestId('lastName').fill('Edicao');
  await page.getByTestId('submit').click();
  await expect(page).toHaveURL(/\/users$/);

  await page.getByTestId('search').fill(email);
  const row = page.locator('tr', { hasText: email });
  await expect(row).toHaveCount(1);

  // The reachability this test exists for: the list's Editar has to land on the form, not on
  // the read-only detail screen.
  await row.getByRole('link', { name: 'Editar' }).click();
  await expect(page).toHaveURL(/\/users\/[0-9a-f-]+\/edit$/);
  await expect(page.getByTestId('firstName')).toHaveValue('Antes');

  await page.getByTestId('firstName').fill('Depois');
  await page.getByTestId('submit').click();
  await expect(page.getByTestId('firstName')).toHaveValue('Depois');

  await page.goto('/users');
  await page.getByTestId('search').fill(email);
  const updated = page.locator('tr', { hasText: email });
  await expect(updated).toContainText('Depois');

  await updated.getByRole('button', { name: 'Eliminar' }).click();
  await page.getByTestId('confirm-accept').click();
  await expect(page.locator('tr', { hasText: email })).toHaveCount(0);
});
