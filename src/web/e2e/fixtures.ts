import { Page, expect } from '@playwright/test';

export const ADMIN_EMAIL = process.env['SEED_ADMIN_EMAIL'] ?? 'admin@producttemplate.com';
export const ADMIN_PASSWORD = process.env['SEED_ADMIN_PASSWORD'] ?? 'Admin@123';
export const TENANT_KEY = process.env['E2E_TENANT'] ?? 'dev';
export const USER_PASSWORD = 'Str0ng@Pass1';

export function unique(prefix: string): string {
  return `${prefix}-${Date.now()}`;
}

export async function login(
  page: Page,
  options: { email?: string; password?: string; expectUrl?: RegExp } = {},
): Promise<void> {
  await page.goto('/login');
  await page.getByTestId('tenant').fill(TENANT_KEY);
  await page.getByTestId('email').fill(options.email ?? ADMIN_EMAIL);
  await page.getByTestId('password').fill(options.password ?? ADMIN_PASSWORD);
  await page.getByTestId('submit').click();
  await expect(page).toHaveURL(options.expectUrl ?? /\/users/);
}

export async function logout(page: Page): Promise<void> {
  await page.getByTestId('logout').click();
  await page.getByTestId('confirm-accept').click();
  await expect(page).toHaveURL(/\/login/);
}

export async function createUserFromUi(
  page: Page,
  fields: { email: string; password?: string; firstName?: string; lastName?: string },
): Promise<void> {
  await page.getByTestId('create-user').click();
  await page.getByTestId('email').fill(fields.email);
  await page.getByTestId('password').fill(fields.password ?? USER_PASSWORD);
  await page.getByTestId('firstName').fill(fields.firstName ?? 'E2E');
  await page.getByTestId('lastName').fill(fields.lastName ?? 'Tester');
  await page.getByTestId('submit').click();
  await expect(page).toHaveURL(/\/users$/);
}

export async function searchUntilEmpty(page: Page, emptyMessage: string): Promise<void> {
  await page.getByTestId('search').fill(`zz-e2e-empty-${Date.now()}`);
  await expect(page.getByTestId('list-empty')).toContainText(emptyMessage);
}

/** Holds the real API response long enough for `list-loading` to paint. */
export async function delayApi(page: Page, glob: string, ms = 1_500): Promise<void> {
  await page.route(glob, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, ms));
    await route.continue();
  });
}
