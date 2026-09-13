import { ApplicationRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { ConfirmService } from './app/shared/confirm';
import { SessionStore } from './app/core/session/session.store';
import { http, HttpResponse } from 'msw';
import { server } from './test-setup';

export const API_ORIGIN = 'http://localhost:3000';

export function el<T extends HTMLElement>(fixture: ComponentFixture<unknown>, testId: string): T {
  const found = fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as T | null;
  if (!found) {
    throw new Error(`no element with data-testid="${testId}"`);
  }
  return found;
}

export function maybeEl<T extends HTMLElement>(
  fixture: ComponentFixture<unknown>,
  testId: string,
): T | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`) as T | null;
}

export function text(fixture: ComponentFixture<unknown>, testId: string): string {
  return el(fixture, testId).textContent?.trim() ?? '';
}

export async function type(
  fixture: ComponentFixture<unknown>,
  testId: string,
  value: string,
): Promise<void> {
  const input = el<HTMLInputElement>(fixture, testId);
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('change', { bubbles: true }));
  await settle(fixture);
}

export async function click(fixture: ComponentFixture<unknown>, testId: string): Promise<void> {
  el(fixture, testId).click();
  await settle(fixture);
}

/**
 * Renders without waiting for stability - the only way to observe a screen while a request is
 * still in flight, since `whenStable()` waits for exactly that request.
 */
export async function render(fixture: ComponentFixture<unknown>, ticks = 3): Promise<void> {
  for (let i = 0; i < ticks; i++) {
    fixture.detectChanges();
    tickApp();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
  tickApp();
}

/** Flushes microtasks, the XHR round trip through MSW, and the resulting render. */
export async function settle(fixture: ComponentFixture<unknown>, ticks = 6): Promise<void> {
  for (let i = 0; i < ticks; i++) {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
    tickApp();
  }
  await fixture.whenStable();
  fixture.detectChanges();
  tickApp();
}

/** Renders views attached to the ApplicationRef but not to the fixture - dialogs, snackbars. */
export function tickApp(): void {
  TestBed.inject(ApplicationRef).tick();
}

export function overlayEl<T extends HTMLElement>(testId: string): T {
  const found = document.body.querySelector(`[data-testid="${testId}"]`) as T | null;
  if (!found) {
    throw new Error(`no overlay element with data-testid="${testId}"`);
  }
  return found;
}

export async function typeOverlay(testId: string, value: string): Promise<void> {
  const input = overlayEl<HTMLInputElement>(testId);
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

/** Records every request MSW served, so a test can assert what was sent - and what was not. */
export interface RecordedRequest {
  method: string;
  url: URL;
  headers: Headers;
  body: unknown;
}

export function recorder(): RecordedRequest[] {
  const requests: RecordedRequest[] = [];
  server.events.removeAllListeners('request:start');
  server.events.on('request:start', async ({ request }) => {
    const clone = request.clone();
    let body: unknown = null;
    try {
      body = clone.method === 'GET' || clone.method === 'DELETE' ? null : await clone.json();
    } catch {
      body = null;
    }
    requests.push({
      method: request.method,
      url: new URL(request.url),
      headers: request.headers,
      body,
    });
  });
  return requests;
}

export function problem(status: number, body: Record<string, unknown>) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}

export const api = {
  get: (path: string, resolver: Parameters<typeof http.get>[1]) =>
    http.get(`${API_ORIGIN}${path}`, resolver),
  post: (path: string, resolver: Parameters<typeof http.post>[1]) =>
    http.post(`${API_ORIGIN}${path}`, resolver),
  put: (path: string, resolver: Parameters<typeof http.put>[1]) =>
    http.put(`${API_ORIGIN}${path}`, resolver),
  delete: (path: string, resolver: Parameters<typeof http.delete>[1]) =>
    http.delete(`${API_ORIGIN}${path}`, resolver),
};

/** An access token carrying the given `permission` claims - payload only, never verified. */
export function tokenWith(permissions: string[]): string {
  const payload = btoa(JSON.stringify({ permission: permissions, sub: 'user-1' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
  return `header.${payload}.signature`;
}

export function authToken(overrides: Partial<{ roles: string[]; permissions: string[] }> = {}) {
  return {
    accessToken: tokenWith(overrides.permissions ?? []),
    tokenType: 'Bearer',
    expiresIn: 3600,
    user: {
      id: 'user-1',
      email: 'admin@producttemplate.com',
      firstName: 'System',
      lastLoginAt: null,
      roles: overrides.roles ?? ['Admin'],
    },
  };
}

/** A route snapshot for components that read `paramMap` / `queryParamMap` on construction. */
export function provideRouteStub(
  params: Record<string, string> = {},
  queryParams: Record<string, string> = {},
): void {
  TestBed.overrideProvider(ActivatedRoute, {
    useValue: {
      snapshot: {
        paramMap: convertToParamMap(params),
        queryParamMap: convertToParamMap(queryParams),
      },
      params: { subscribe: () => ({ unsubscribe: () => undefined }) },
    },
  });
}

/** Replaces the confirmation dialog with a fixed answer. Call before the first `TestBed.inject`. */
export function stubConfirm(answer: boolean): void {
  TestBed.overrideProvider(ConfirmService, { useValue: { ask: async () => answer } });
}

/** A session holding the given permissions, or the `Admin` role when none are listed. */
export function authenticate(permissions: string[] = []): SessionStore {
  const session = TestBed.inject(SessionStore);
  session.setTenantKey('dev');
  session.apply({
    ...authToken({ roles: permissions.length === 0 ? ['Admin'] : [] }),
    accessToken: tokenWith(permissions),
  });
  return session;
}

/**
 * Retries an assertion while the UI settles. Material's dialog and snackbar close over real time,
 * which a microtask flush cannot reach.
 */
export async function waitFor(
  fixture: ComponentFixture<unknown>,
  assertion: () => void,
  timeoutMs = 1500,
): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let lastError: unknown;
  while (Date.now() < deadline) {
    try {
      assertion();
      return;
    } catch (error: unknown) {
      lastError = error;
      await new Promise((resolve) => setTimeout(resolve, 25));
      fixture.detectChanges();
      tickApp();
    }
  }
  throw lastError;
}

/** Polls an assertion that depends on real time but not on a component - e.g. a navigation. */
export async function waitUntil(assertion: () => void, timeoutMs = 2000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  let lastError: unknown;
  while (Date.now() < deadline) {
    try {
      assertion();
      return;
    } catch (error: unknown) {
      lastError = error;
      await new Promise((resolve) => setTimeout(resolve, 25));
    }
  }
  throw lastError;
}
