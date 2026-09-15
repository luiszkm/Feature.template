import { expect, test } from '@playwright/test';
import { USER_PASSWORD, createUserFromUi, login, logout, unique } from './fixtures';

test('utilizador sem permissões vê o ecrã forbidden nas rotas de roles e agentes', async ({
  page,
}) => {
  const email = `e2e-denied-${Date.now()}@example.com`;

  await login(page);
  await createUserFromUi(page, {
    email,
    password: USER_PASSWORD,
    firstName: 'Sem',
    lastName: unique('perm').slice(0, 12),
  });
  await logout(page);

  await page.goto('/roles');
  await expect(page).toHaveURL(/\/login/);
  await page.getByTestId('tenant').fill('dev');
  await page.getByTestId('email').fill(email);
  await page.getByTestId('password').fill(USER_PASSWORD);
  await page.getByTestId('submit').click();

  await expect(page.getByTestId('forbidden')).toBeVisible();
  await expect(page.getByTestId('forbidden')).toContainText('Sem permissão para esta operação');

  await page.goto('/ai/agents');
  await expect(page.getByTestId('forbidden')).toBeVisible();
});
