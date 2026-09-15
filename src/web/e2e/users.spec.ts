import { expect, test } from '@playwright/test';
import { createUserFromUi, delayApi, login, searchUntilEmpty, unique } from './fixtures';

test('cria e elimina um utilizador', async ({ page }) => {
  const email = `e2e-${Date.now()}@example.com`;

  await login(page);

  await createUserFromUi(page, { email });

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

  await createUserFromUi(page, { email, firstName: 'Antes', lastName: 'Edicao' });

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

test('pesquisa sem resultados mostra o estado vazio', async ({ page }) => {
  await login(page);
  await expect(page.getByTestId('users-table')).toBeVisible();
  await searchUntilEmpty(page, 'Nenhum utilizador');
  await expect(page.getByTestId('list-empty').getByRole('link', { name: 'Criar utilizador' })).toBeVisible();
});

test('mostra o loading da lista enquanto a API responde', async ({ page }) => {
  await login(page);
  await delayApi(page, '**/api/v1/identity/users?*');
  await page.goto(`/users?searchTerm=${unique('load')}`);
  await expect(page.getByTestId('list-loading')).toBeVisible();
  await expect(page.getByTestId('list-loading')).toHaveCount(0, { timeout: 20_000 });
});
