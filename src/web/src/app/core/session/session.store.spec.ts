import { authToken, tokenWith } from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { AUTH_STORAGE_KEY, SessionStore, TENANT_STORAGE_KEY } from './session.store';

describe('SessionStore', () => {
  it('hasPermission le as claims permission', () => {
    const session = TestBed.inject(SessionStore);
    session.apply({
      ...authToken({ roles: [] }),
      accessToken: tokenWith(['identity.user.read']),
    });

    expect(session.hasPermission('identity.user.read')).toBe(true);
    expect(session.hasPermission('identity.user.manage')).toBe(false);
  });

  it('a role Admin vale por qualquer permissao', () => {
    const session = TestBed.inject(SessionStore);
    session.apply({ ...authToken({ roles: ['Admin'] }), accessToken: tokenWith([]) });

    expect(session.hasPermission('tenants.manage')).toBe(true);
  });

  it('nao guarda nenhum token em storage', () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply(authToken());

    const stored = localStorage.getItem(AUTH_STORAGE_KEY) ?? '';

    expect(JSON.parse(stored)).toEqual({
      tenantKey: 'dev',
      user: authToken().user,
    });
    expect(stored).not.toContain(session.accessToken());
    expect(stored.toLowerCase()).not.toContain('token');
    expect(sessionStorage.length).toBe(0);
  });

  it('logout limpa pt.auth e o token em memoria', () => {
    const session = TestBed.inject(SessionStore);
    session.setTenantKey('dev');
    session.apply(authToken());

    expect(localStorage.getItem(AUTH_STORAGE_KEY)).not.toBeNull();

    session.clear();

    expect(session.accessToken()).toBeNull();
    expect(session.user()).toBeNull();
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(localStorage.getItem(TENANT_STORAGE_KEY)).toBe('dev');
  });
});
