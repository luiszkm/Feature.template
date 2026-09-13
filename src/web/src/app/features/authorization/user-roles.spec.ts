import {
  api,
  authenticate,
  el,
  maybeEl,
  provideRouteStub,
  recorder,
  settle,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { UserRoles } from './user-roles';

const ASSIGNED = '/api/v1/authorization/users/user-1/roles';
const AUDITOR = { id: 'role-2', name: 'Auditor', description: 'Leitura' };
const ADMIN = { id: 'role-1', name: 'Admin', description: 'Tudo' };

describe('UserRoles', () => {
  it('atribui e revoga recarregando', async () => {
    const requests = recorder();
    let assigned = [ADMIN];
    server.use(
      api.get(ASSIGNED, () => HttpResponse.json(assigned)),
      api.get('/api/v1/authorization/roles', () =>
        HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 2, data: [ADMIN, AUDITOR] }),
      ),
      api.post(ASSIGNED, () => {
        assigned = [ADMIN, AUDITOR];
        return new HttpResponse(null, { status: 204 });
      }),
      api.delete(`${ASSIGNED}/role-1`, () => {
        assigned = [AUDITOR];
        return new HttpResponse(null, { status: 204 });
      }),
    );
    provideRouteStub({ userId: 'user-1' });
    authenticate();

    const fixture = TestBed.createComponent(UserRoles);
    await settle(fixture);

    const picker = el<HTMLSelectElement>(fixture, 'role-picker');
    picker.value = AUDITOR.id;
    picker.dispatchEvent(new Event('change', { bubbles: true }));
    await settle(fixture, 2);

    el(fixture, 'assign').click();
    await settle(fixture);

    expect(maybeEl(fixture, 'assigned-role-2')).not.toBeNull();
    expect(requests.filter((r) => r.method === 'POST' && r.url.pathname === ASSIGNED)).toHaveLength(
      1,
    );

    el(fixture, 'revoke-role-1').click();
    await settle(fixture);

    expect(maybeEl(fixture, 'assigned-role-1')).toBeNull();
    expect(requests.filter((r) => r.method === 'GET' && r.url.pathname === ASSIGNED)).toHaveLength(
      3,
    );
  });
});
