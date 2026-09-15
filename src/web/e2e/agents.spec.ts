import { expect, test } from '@playwright/test';
import { delayApi, login, searchUntilEmpty, unique } from './fixtures';

test('lista o agente seed e cria um segundo', async ({ page }) => {
  const name = unique('agente');

  await login(page);
  await page.getByTestId('nav-agents').click();
  await expect(page.getByTestId('agents-table')).toBeVisible();
  await expect(page.locator('tr', { hasText: 'Default' })).toHaveCount(1);

  await page.getByTestId('create-agent').click();
  await expect(page).toHaveURL(/\/ai\/agents\/new$/);
  await page.getByTestId('name').fill(name);
  await page.getByTestId('instructions').fill('Instruções de e2e para o segundo agente.');
  await page.getByTestId('submit').click();

  await expect(page).toHaveURL(/\/ai\/agents$/);
  await page.getByTestId('search').fill(name);
  const row = page.locator('tr', { hasText: name });
  await expect(row).toHaveCount(1);

  await row.getByRole('button', { name: 'Desativar' }).click();
  await expect(page.getByRole('dialog')).toContainText('Desativar agente');
  await page.getByTestId('confirm-accept').click();
  await expect(row).toContainText('Não');
});

test('pesquisa sem resultados mostra o estado vazio', async ({ page }) => {
  await login(page);
  await page.goto('/ai/agents');
  await expect(page.getByTestId('agents-table')).toBeVisible();
  await searchUntilEmpty(page, 'Nenhum agente');
  await expect(page.getByRole('link', { name: 'Criar agente' })).toBeVisible();
});

test('mostra o loading da lista enquanto a API responde', async ({ page }) => {
  await login(page);
  await delayApi(page, '**/api/v1/ai/agents?*');
  await page.goto('/ai/agents');
  await expect(page.getByTestId('list-loading')).toBeVisible();
  await expect(page.getByTestId('paginator')).toBeDisabled();
  await expect(page.getByTestId('agents-table')).toBeVisible({ timeout: 20_000 });
});
