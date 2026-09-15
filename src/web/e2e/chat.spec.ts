import { expect, test } from '@playwright/test';
import { delayApi, login } from './fixtures';

test('chat mostra o picker, o vazio e responde com o stub', async ({ page }) => {
  await login(page);
  await page.getByTestId('nav-ai').click();

  await expect(page.getByTestId('agent-picker')).toBeVisible();
  await expect(page.getByTestId('chat-empty')).toHaveText('Faça uma pergunta');

  await page.getByTestId('chat-input').fill('olá e2e');
  await page.getByTestId('chat-send').click();

  await expect(page.getByTestId('chat-history')).toContainText('olá e2e');
  await expect(page.getByTestId('chat-history')).toContainText('Recebi sua mensagem: olá e2e');
  await expect(page.getByTestId('chat-empty')).toHaveCount(0);
});

test('mostra A escrever… enquanto o chat está pendente', async ({ page }) => {
  await login(page);
  await delayApi(page, '**/api/v1/ai/chat', 2_000);
  await page.goto('/ai');
  await expect(page.getByTestId('chat-empty')).toBeVisible();

  await page.getByTestId('chat-input').fill('ping');
  await page.getByTestId('chat-send').click();
  await expect(page.getByTestId('chat-typing')).toHaveText('A escrever…');
  await expect(page.getByTestId('chat-history')).toContainText('Recebi sua mensagem: ping', {
    timeout: 20_000,
  });
});
