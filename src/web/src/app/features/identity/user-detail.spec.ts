import {
  api,
  authenticate,
  maybeEl,
  problem,
  provideRouteStub,
  settle,
  text,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { NO_ROLE_ACCESS_MESSAGE, UserDetail } from './user-detail';

const USER = {
  id: 'user-1',
  email: 'ana@example.com',
  firstName: 'Ana',
  lastName: 'Silva',
  createdAt: '2026-01-02T10:00:00Z',
  lastLoginAt: null,
};

describe('UserDetail', () => {
  it('carrega utilizador e roles', async () => {
    server.use(
      api.get('/api/v1/identity/users/user-1', () => HttpResponse.json(USER)),
      api.get('/api/v1/identity/users/user-1/roles', () => HttpResponse.json(['Admin', 'Auditor'])),
    );
    provideRouteStub({ userId: 'user-1' });
    authenticate();

    const fixture = TestBed.createComponent(UserDetail);
    await settle(fixture);

    expect(text(fixture, 'user-email')).toBe('ana@example.com');
    expect(text(fixture, 'roles-list')).toContain('Auditor');
  });

  it('403 nos roles mantem o ecra', async () => {
    server.use(
      api.get('/api/v1/identity/users/user-1', () => HttpResponse.json(USER)),
      api.get('/api/v1/identity/users/user-1/roles', () =>
        problem(403, { title: 'Forbidden', status: 403 }),
      ),
    );
    provideRouteStub({ userId: 'user-1' });
    authenticate();

    const fixture = TestBed.createComponent(UserDetail);
    await settle(fixture);

    expect(text(fixture, 'roles-denied')).toBe(NO_ROLE_ACCESS_MESSAGE);
    expect(text(fixture, 'user-email')).toBe('ana@example.com');
    expect(maybeEl(fixture, 'forbidden')).toBeNull();
  });

  it('a propria pessoa ve Editar sem identity.user.manage', async () => {
    server.use(
      api.get('/api/v1/identity/users/user-1', () => HttpResponse.json(USER)),
      api.get('/api/v1/identity/users/user-1/roles', () => HttpResponse.json([])),
    );
    provideRouteStub({ userId: 'user-1' });
    // `authenticate()`'s token always carries `sub: 'user-1'`, matching `USER.id` here - a
    // non-empty, non-`userManage` permission list is what keeps this session off the `Admin`
    // role default (which would bypass `hasPermission` and prove nothing about the self branch).
    authenticate(['identity.user.read']);

    const fixture = TestBed.createComponent(UserDetail);
    await settle(fixture);

    expect(maybeEl(fixture, 'detail-edit')).not.toBeNull();
  });

  it('nem manager nem a propria pessoa nao ve Editar', async () => {
    const other = { ...USER, id: 'user-2', email: 'outro@example.com' };
    server.use(
      api.get('/api/v1/identity/users/user-2', () => HttpResponse.json(other)),
      api.get('/api/v1/identity/users/user-2/roles', () => HttpResponse.json([])),
    );
    provideRouteStub({ userId: 'user-2' });
    // Same session as above (`sub: 'user-1'`) viewing a *different* user's detail (`user-2`).
    authenticate(['identity.user.read']);

    const fixture = TestBed.createComponent(UserDetail);
    await settle(fixture);

    expect(text(fixture, 'user-email')).toBe('outro@example.com');
    expect(maybeEl(fixture, 'detail-edit')).toBeNull();
  });
});
