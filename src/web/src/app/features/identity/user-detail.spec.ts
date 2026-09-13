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
});
