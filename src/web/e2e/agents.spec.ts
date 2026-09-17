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

test('cria, lista e apaga um ficheiro no form', async ({ page }) => {
  const name = unique('agente-ficheiros');
  const fileName = unique('notas') + '.txt';

  await login(page);
  await page.getByTestId('nav-agents').click();
  await page.getByTestId('create-agent').click();
  await page.getByTestId('name').fill(name);
  await page.getByTestId('instructions').fill('Agente e2e com ficheiros de texto.');
  await page.getByTestId('submit').click();
  await expect(page).toHaveURL(/\/ai\/agents$/);

  await page.getByTestId('search').fill(name);
  const row = page.locator('tr', { hasText: name });
  await expect(row).toHaveCount(1);
  await row.getByRole('link', { name }).click();

  await expect(page.getByTestId('agent-files')).toBeVisible();
  await expect(page.getByTestId('file-empty')).toHaveText('Nenhum ficheiro');

  await page.getByTestId('file-name').fill(fileName);
  await page.getByTestId('file-content').fill('conteúdo e2e');
  await page.getByTestId('file-add').click();

  const fileRow = page.locator('[data-testid="agent-files"] li', { hasText: fileName });
  await expect(fileRow).toHaveCount(1);
  await expect(page.getByTestId('file-empty')).toHaveCount(0);

  await fileRow.getByRole('button', { name: fileName }).click();
  await expect(page.getByTestId('file-preview')).toHaveText('conteúdo e2e');

  await fileRow.getByRole('button', { name: 'Apagar' }).click();
  await expect(page.getByRole('dialog')).toContainText('Apagar ficheiro');
  await page.getByTestId('confirm-accept').click();
  await expect(fileRow).toHaveCount(0);
  await expect(page.getByTestId('file-empty')).toHaveText('Nenhum ficheiro');
});

test('mostra erro 500 e Tentar de novo', async ({ page }) => {
  await login(page);
  let calls = 0;
  await page.route('**/api/v1/ai/agents?*', async (route) => {
    calls += 1;
    if (calls === 1) {
      await route.fulfill({
        status: 500,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Unexpected error', status: 500 }),
      });
      return;
    }
    await route.continue();
  });

  await page.goto('/ai/agents');
  await expect(page.getByTestId('list-error-title')).toHaveText('Unexpected error');
  await expect(page.getByTestId('list-retry')).toHaveText('Tentar de novo');
  await page.getByTestId('list-retry').click();
  await expect(page.getByTestId('agents-table').or(page.getByTestId('list-empty'))).toBeVisible();
});

test('flag AI off esconde a navegacao', async ({ page }) => {
  await login(page);
  await page.route('**/api/v1/ai/**', async (route) => {
    await route.fulfill({
      status: 404,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: 'Feature disabled', status: 404 }),
    });
  });

  await page.goto('/ai/agents');
  await expect(page.getByTestId('nav-ai')).toHaveCount(0);
  await expect(page.getByTestId('nav-agents')).toHaveCount(0);
});

test('pesquisa sem resultados mostra o estado vazio', async ({ page }) => {
  await login(page);
  await page.goto('/ai/agents');
  await expect(page.getByTestId('agents-table')).toBeVisible();
  await searchUntilEmpty(page, 'Nenhum agente');
  await expect(page.getByTestId('list-empty').getByRole('link', { name: 'Criar agente' })).toBeVisible();
});

test('mostra o loading da lista enquanto a API responde', async ({ page }) => {
  await login(page);
  await delayApi(page, '**/api/v1/ai/agents?*');
  await page.goto('/ai/agents');
  await expect(page.getByTestId('list-loading')).toBeVisible();
  await expect(page.getByTestId('agents-table').or(page.getByTestId('list-empty'))).toBeVisible({
    timeout: 20_000,
  });
});
