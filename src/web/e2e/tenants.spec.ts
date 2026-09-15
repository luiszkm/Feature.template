import { expect, test } from '@playwright/test';
import { delayApi, login, searchUntilEmpty, unique } from './fixtures';

test('cria um tenant e encontra-o na lista', async ({ page }) => {
  const key = unique('e2e').replace(/-/g, '').slice(0, 24);

  await login(page);
  await page.getByTestId('nav-tenants').click();
  await expect(page.getByTestId('tenants-table')).toBeVisible();

  await page.getByTestId('create-tenant').click();
  await page.getByTestId('tenantKey').fill(key);
  await page.getByTestId('displayName').fill(`Tenant ${key}`);
  await page.getByTestId('contactEmail').fill(`${key}@example.com`);
  await page.getByTestId('submit').click();

  await expect(page).toHaveURL(/\/tenants$/);
  await page.getByTestId('search').fill(key);
  const row = page.locator('tr', { hasText: key });
  await expect(row).toHaveCount(1);
  await expect(row).toContainText(`Tenant ${key}`);
});

test('pesquisa sem resultados mostra o estado vazio', async ({ page }) => {
  await login(page);
  await page.goto('/tenants');
  await expect(page.getByTestId('tenants-table')).toBeVisible();
  await searchUntilEmpty(page, 'Nenhum tenant');
  await expect(page.getByTestId('list-empty').getByRole('link', { name: 'Criar tenant' })).toBeVisible();
});

test('mostra o loading da lista enquanto a API responde', async ({ page }) => {
  await login(page);
  await delayApi(page, '**/api/v1/tenants?*');
  await page.goto('/tenants');
  await expect(page.getByTestId('list-loading')).toBeVisible();
  await expect(page.getByTestId('tenants-table')).toBeVisible({ timeout: 20_000 });
});
