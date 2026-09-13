import {
  api,
  authenticate,
  el,
  maybeEl,
  provideRouteStub,
  recorder,
  settle,
  stubConfirm,
  text,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { RoleDetail } from './role-detail';

const ROLE = { id: 'role-1', name: 'Auditor', description: 'Leitura' };
const READ = { id: 'perm-1', name: 'identity.user.read', description: 'Ler utilizadores' };
const MANAGE = { id: 'perm-2', name: 'identity.user.manage', description: 'Gerir utilizadores' };

function serveRole(assigned = [READ]) {
  server.use(
    api.get('/api/v1/authorization/roles/role-1', () => HttpResponse.json(ROLE)),
    api.get('/api/v1/authorization/roles/role-1/permissions', () =>
      HttpResponse.json({ ...ROLE, permissions: assigned }),
    ),
    api.get('/api/v1/authorization/permissions', () =>
      HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 2, data: [READ, MANAGE] }),
    ),
  );
}

async function renderDetail() {
  provideRouteStub({ roleId: 'role-1' });
  authenticate();
  const fixture = TestBed.createComponent(RoleDetail);
  await settle(fixture);
  return fixture;
}

describe('RoleDetail', () => {
  it('carrega o role', async () => {
    serveRole();
    const fixture = await renderDetail();

    expect(text(fixture, 'role-name')).toBe('Auditor');
    expect(text(fixture, 'role-description')).toBe('Leitura');
  });

  it('lista as permissoes do role', async () => {
    serveRole();
    const fixture = await renderDetail();

    expect(text(fixture, 'role-permissions')).toContain('identity.user.read');
    expect(maybeEl(fixture, 'permissions-empty')).toBeNull();
  });

  it('204 acrescenta a permissao', async () => {
    const requests = recorder();
    serveRole();
    server.use(
      api.post(
        '/api/v1/authorization/roles/role-1/permissions',
        () => new HttpResponse(null, { status: 204 }),
      ),
    );

    const fixture = await renderDetail();
    const picker = el<HTMLSelectElement>(fixture, 'permission-picker');
    picker.value = MANAGE.id;
    picker.dispatchEvent(new Event('change', { bubbles: true }));
    await settle(fixture, 2);

    const reads = requests.filter((request) => request.method === 'GET').length;
    el(fixture, 'assign').click();
    await settle(fixture);

    expect(maybeEl(fixture, `permission-${MANAGE.id}`)).not.toBeNull();
    expect(requests.filter((request) => request.method === 'GET').length).toBe(reads);
  });

  it('204 remove a permissao', async () => {
    serveRole();
    server.use(
      api.delete(
        '/api/v1/authorization/roles/role-1/permissions/perm-1',
        () => new HttpResponse(null, { status: 204 }),
      ),
    );
    stubConfirm(true);

    const fixture = await renderDetail();
    el(fixture, 'revoke-perm-1').click();
    await settle(fixture);

    expect(maybeEl(fixture, 'permission-perm-1')).toBeNull();
    expect(text(fixture, 'permissions-empty')).toBe('Sem permissões atribuídas');
  });
});
