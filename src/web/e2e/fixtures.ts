import { Page, expect } from '@playwright/test';

export const ADMIN_EMAIL = process.env['SEED_ADMIN_EMAIL'] ?? 'admin@producttemplate.com';
export const ADMIN_PASSWORD = process.env['SEED_ADMIN_PASSWORD'] ?? 'Admin@123';
export const TENANT_KEY = process.env['E2E_TENANT'] ?? 'dev';

export async function login(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByTestId('tenant').fill(TENANT_KEY);
  await page.getByTestId('email').fill(ADMIN_EMAIL);
  await page.getByTestId('password').fill(ADMIN_PASSWORD);
  await page.getByTestId('submit').click();
  await expect(page).toHaveURL(/\/users/);
}
