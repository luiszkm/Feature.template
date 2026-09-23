import {
  api,
  authToken,
  el,
  maybeEl,
  recorder,
  settle,
  stubConfirm,
  text,
  tokenWith,
  waitFor,
} from '../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test-setup';
import { AUTH_STORAGE_KEY, SessionStore } from '../core/session/session.store';
import { AiAvailability } from '../features/ai/ai-availability';
import { Shell } from './shell';

describe('Shell', () => {
  it('mostra firstName e tenantKey', async () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: ['Admin'] }), accessToken: tokenWith([]) });

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    expect(text(fixture, 'session-user')).toBe('System');
    expect(text(fixture, 'session-tenant')).toBe('dev');
  });

  it('logout chama a API', async () => {
    const requests = recorder();
    server.use(api.post('/api/v1/identity/logout', () => new HttpResponse(null, { status: 204 })));
    stubConfirm(true);

    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: ['Admin'] }), accessToken: tokenWith([]) });

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    el(fixture, 'logout').click();
    await settle(fixture);

    expect(
      requests.filter((request) => request.url.pathname === '/api/v1/identity/logout'),
    ).toHaveLength(1);
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    await waitFor(fixture, () => expect(TestBed.inject(Router).url).toBe('/login'));
  });

  it('cancelar o dialogo nao termina a sessao', async () => {
    const requests = recorder();
    stubConfirm(false);

    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: ['Admin'] }), accessToken: tokenWith([]) });

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    el(fixture, 'logout').click();
    await settle(fixture);

    expect(requests).toHaveLength(0);
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();
    expect(session.user()).not.toBeNull();
  });

  it('esconde AI, Agentes e Conversas quando a flag esta off', async () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: ['Admin'] }), accessToken: tokenWith([]) });
    TestBed.inject(AiAvailability).disable();

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'nav-ai')).toBeNull();
    expect(maybeEl(fixture, 'nav-agents')).toBeNull();
    expect(maybeEl(fixture, 'nav-conversations')).toBeNull();
  });

  it('mostra Conversas quando a flag esta on', async () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: ['Admin'] }), accessToken: tokenWith([]) });

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    expect(text(fixture, 'nav-conversations')).toBe('Conversas');
  });
  it('mostra Uso quando ai.agent.read e a flag esta on', async () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: [] }), accessToken: tokenWith(['ai.agent.read']) });

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    expect(text(fixture, 'nav-ai-usage')).toBe('Uso');
    expect(el<HTMLAnchorElement>(fixture, 'nav-ai-usage').getAttribute('href')).toBe('/ai/usage');
  });

  it('esconde Uso quando a flag esta off', async () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: [] }), accessToken: tokenWith(['ai.agent.read']) });
    TestBed.inject(AiAvailability).disable();

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'nav-ai-usage')).toBeNull();
  });

  it('esconde Uso sem ai.agent.read', async () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply({ ...authToken({ roles: [] }), accessToken: tokenWith([]) });

    const fixture = TestBed.createComponent(Shell);
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'nav-ai-usage')).toBeNull();
  });
});
