import {
  api,
  authenticate,
  el,
  maybeEl,
  overlayEl,
  problem,
  provideRouteStub,
  settle,
  stubConfirm,
  text,
  typeOverlay,
  waitFor,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { PermissionsList } from './permissions-list';

const PERMISSIONS = '/api/v1/authorization/permissions';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  data: [{ id: 'perm-1', name: 'identity.user.read', description: 'Ler utilizadores' }],
};

async function renderList() {
  provideRouteStub();
  authenticate();
  const fixture = TestBed.createComponent(PermissionsList);
  await settle(fixture);
  return fixture;
}

describe('PermissionsList', () => {
  it('carrega a primeira pagina', async () => {
    server.use(api.get(PERMISSIONS, () => HttpResponse.json(PAGE)));

    const fixture = await renderList();

    expect(text(fixture, 'row-perm-1')).toBe('identity.user.read');
    expect(text(fixture, 'description-perm-1')).toBe('Ler utilizadores');
  });

  it('201 acrescenta a permissao', async () => {
    server.use(
      api.get(PERMISSIONS, () => HttpResponse.json(PAGE)),
      api.post(PERMISSIONS, () =>
        HttpResponse.json(
          { id: 'perm-2', name: 'tenants.read', description: 'Ler tenants' },
          { status: 201 },
        ),
      ),
    );

    const fixture = await renderList();
    el(fixture, 'create-permission').click();
    await settle(fixture, 2);
    await typeOverlay('name', 'tenants.read');
    await settle(fixture, 2);
    overlayEl('dialog-save').click();
    await settle(fixture);

    expect(text(fixture, 'row-perm-2')).toBe('tenants.read');
    await waitFor(fixture, () => expect(TestBed.inject(MatDialog).openDialogs).toHaveLength(0));
  });

  it('409 mantem o dialogo aberto', async () => {
    server.use(
      api.get(PERMISSIONS, () => HttpResponse.json(PAGE)),
      api.post(PERMISSIONS, () =>
        problem(409, {
          title: 'Business rule violation',
          detail: "Permission 'tenants.read' already exists.",
          status: 409,
        }),
      ),
    );

    const fixture = await renderList();
    el(fixture, 'create-permission').click();
    await settle(fixture, 2);
    await typeOverlay('name', 'tenants.read');
    await settle(fixture, 2);
    overlayEl('dialog-save').click();
    await settle(fixture);

    expect(overlayEl('name-error').textContent?.trim()).toBe(
      "Permission 'tenants.read' already exists.",
    );
    expect(TestBed.inject(MatDialog).openDialogs).toHaveLength(1);
  });

  it('200 atualiza a linha', async () => {
    server.use(
      api.get(PERMISSIONS, () => HttpResponse.json(PAGE)),
      api.put(`${PERMISSIONS}/perm-1`, () =>
        HttpResponse.json({ id: 'perm-1', name: 'identity.user.list', description: 'Listar' }),
      ),
    );

    const fixture = await renderList();
    el(fixture, 'edit-perm-1').click();
    await settle(fixture, 2);
    await typeOverlay('name', 'identity.user.list');
    await settle(fixture, 2);
    overlayEl('dialog-save').click();
    await settle(fixture);

    expect(text(fixture, 'row-perm-1')).toBe('identity.user.list');
  });

  it('204 remove a permissao', async () => {
    server.use(
      api.get(PERMISSIONS, () => HttpResponse.json(PAGE)),
      api.delete(`${PERMISSIONS}/perm-1`, () => new HttpResponse(null, { status: 204 })),
    );
    stubConfirm(true);

    const fixture = await renderList();
    el(fixture, 'delete-perm-1').click();
    await settle(fixture);

    expect(maybeEl(fixture, 'row-perm-1')).toBeNull();
  });
});
