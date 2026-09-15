import { expect, test } from '@playwright/test';
import {
  USER_PASSWORD,
  createUserFromUi,
  delayApi,
  login,
  searchUntilEmpty,
  unique,
} from './fixtures';

test('cria um role e encontra-o na lista', async ({ page }) => {
  const name = unique('role');

  await login(page);
  await page.getByTestId('nav-roles').click();
  await expect(page.getByTestId('roles-table')).toBeVisible();

  await page.getByTestId('create-role').click();
  const dialog = page.getByRole('dialog');
  await dialog.getByTestId('name').fill(name);
  await dialog.getByTestId('description').fill('Role de e2e');
  await dialog.getByTestId('dialog-save').click();

  await expect(dialog).toHaveCount(0);
  await page.getByTestId('search').fill(name);
  const row = page.locator('tr', { hasText: name });
  await expect(row).toHaveCount(1);

  await row.getByRole('button', { name: 'Eliminar' }).click();
  await page.getByTestId('confirm-accept').click();
  await expect(page.locator('tr', { hasText: name })).toHaveCount(0);
});

test('atribui um role a um utilizador', async ({ page }) => {
  const email = `e2e-roles-${Date.now()}@example.com`;
  const roleName = unique('assign');

  await login(page);
  await createUserFromUi(page, { email, password: USER_PASSWORD });

  await page.getByTestId('nav-roles').click();
  await page.getByTestId('create-role').click();
  const dialog = page.getByRole('dialog');
  await dialog.getByTestId('name').fill(roleName);
  await dialog.getByTestId('description').fill('Atribuir');
  await dialog.getByTestId('dialog-save').click();
  await expect(dialog).toHaveCount(0);

  await page.goto('/users');
  await page.getByTestId('search').fill(email);
  const userRow = page.locator('tr', { hasText: email });
  await expect(userRow).toHaveCount(1);
  await userRow.getByRole('link', { name: email }).click();
  await page.getByRole('link', { name: 'Gerir roles' }).click();

  await expect(page).toHaveURL(/\/users\/[0-9a-f-]+\/roles$/);
  await page.getByTestId('role-picker').selectOption({ label: roleName });
  await page.getByTestId('assign').click();
  await expect(page.getByTestId('assigned-roles')).toContainText(roleName);
});

test('pesquisa sem resultados mostra o estado vazio', async ({ page }) => {
  await login(page);
  await page.goto('/roles');
  await expect(page.getByTestId('roles-table')).toBeVisible();
  await searchUntilEmpty(page, 'Nenhum role');
});

test('mostra o loading da lista enquanto a API responde', async ({ page }) => {
  await login(page);
  await delayApi(page, '**/api/v1/authorization/roles?*');
  await page.goto('/roles');
  await expect(page.getByTestId('list-loading')).toBeVisible();
  await expect(page.getByTestId('roles-table').or(page.getByTestId('list-empty'))).toBeVisible({
    timeout: 20_000,
  });
});
