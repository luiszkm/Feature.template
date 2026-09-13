import { api, authToken, click, problem, recorder, settle, text, type } from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import {
  AUTH_STORAGE_KEY,
  SessionStore,
  TENANT_STORAGE_KEY,
} from '../../core/session/session.store';
import { INVALID_TENANT_MESSAGE, Login, RATE_LIMIT_MESSAGE } from './login';

async function renderLogin() {
  const fixture = TestBed.createComponent(Login);
  await settle(fixture);
  return fixture;
}

async function fillCredentials(fixture: Awaited<ReturnType<typeof renderLogin>>) {
  await type(fixture, 'tenant', 'dev');
  await type(fixture, 'email', 'admin@producttemplate.com');
  await type(fixture, 'password', 'Admin@123');
}

describe('Login', () => {
  it('guarda a sessao e navega para users', async () => {
    const requests = recorder();
    server.use(api.post('/api/v1/identity/login', () => HttpResponse.json(authToken())));

    const fixture = await renderLogin();
    const router = TestBed.inject(Router);
    const session = TestBed.inject(SessionStore);

    await fillCredentials(fixture);
    await click(fixture, 'submit');

    const raw = localStorage.getItem(AUTH_STORAGE_KEY) ?? '{}';
    const stored = JSON.parse(raw);
    expect(Object.keys(stored).sort()).toEqual(['tenantKey', 'user']);
    expect(localStorage.getItem(TENANT_STORAGE_KEY)).toBe('dev');
    expect(raw).not.toContain(session.accessToken());
    expect(raw.toLowerCase()).not.toContain('token');
    expect(session.accessToken()).not.toBeNull();
    expect(router.url).toBe('/users');
    expect(requests.at(-1)?.headers.get('X-Tenant')).toBe('dev');
  });

  it('401 mantem o email e mostra o detail', async () => {
    server.use(
      api.post('/api/v1/identity/login', () =>
        problem(401, { title: 'Unauthorized', detail: 'Invalid email or password.', status: 401 }),
      ),
    );

    const fixture = await renderLogin();
    await fillCredentials(fixture);
    await click(fixture, 'submit');

    expect(text(fixture, 'login-message')).toBe('Invalid email or password.');
    expect(TestBed.inject(Router).url).not.toBe('/users');
    expect(
      (fixture.nativeElement.querySelector('[data-testid="email"]') as HTMLInputElement).value,
    ).toBe('admin@producttemplate.com');
  });

  it('429 mostra limite de tentativas', async () => {
    server.use(api.post('/api/v1/identity/login', () => new HttpResponse(null, { status: 429 })));

    const fixture = await renderLogin();
    await fillCredentials(fixture);
    await click(fixture, 'submit');

    expect(text(fixture, 'login-message')).toBe(RATE_LIMIT_MESSAGE);
  });

  it('409 mostra tenant invalido', async () => {
    server.use(
      api.post('/api/v1/identity/login', () =>
        problem(409, {
          title: 'Business rule violation',
          detail: 'Tenant must be resolved before login.',
          status: 409,
        }),
      ),
    );

    const fixture = await renderLogin();
    await fillCredentials(fixture);
    await click(fixture, 'submit');

    expect(text(fixture, 'tenant-error')).toBe(INVALID_TENANT_MESSAGE);
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });
});
