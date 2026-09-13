import { expect, test } from '@playwright/test';
import { login } from './fixtures';

test('renova o token expirado', async ({ page }) => {
  await login(page);

  // The access token only ever lives in memory, so a reload leaves the app holding nothing but
  // the refresh token - the same state an expired token produces on the next call.
  const refreshCalls: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/v1/identity/refresh')) {
      refreshCalls.push(request.url());
    }
  });

  await page.reload();

  await expect(page.getByTestId('users-table')).toBeVisible();
  await expect(page).toHaveURL(/\/users/);
  expect(refreshCalls.length).toBeGreaterThan(0);

  // The credential that renewed the session is the httpOnly cookie: nothing in web storage,
  // and nothing script-readable in document.cookie.
  const storage = await page.evaluate(() => ({
    local: JSON.stringify(localStorage),
    session: JSON.stringify(sessionStorage),
    cookies: document.cookie,
  }));

  expect(storage.local.toLowerCase()).not.toContain('token');
  expect(storage.session).toBe('{}');
  expect(storage.cookies).not.toContain('pt_refresh');
});
