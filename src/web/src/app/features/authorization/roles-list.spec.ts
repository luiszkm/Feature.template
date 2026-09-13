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
import { RolesList } from './roles-list';

const ROLES = '/api/v1/authorization/roles';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  data: [{ id: 'role-1', name: 'Admin', description: 'Acesso total' }],
};

async function renderList() {
  provideRouteStub();
  authenticate();
  const fixture = TestBed.createComponent(RolesList);
  await settle(fixture);
  return fixture;
}

describe('RolesList', () => {
  it('carrega a primeira pagina', async () => {
    server.use(api.get(ROLES, () => HttpResponse.json(PAGE)));

    const fixture = await renderList();

    expect(text(fixture, 'row-role-1')).toContain('Admin');
    expect(text(fixture, 'description-role-1')).toBe('Acesso total');
  });

  it('201 acrescenta o role', async () => {
    server.use(
      api.get(ROLES, () => HttpResponse.json(PAGE)),
      api.post(ROLES, () =>
        HttpResponse.json(
          { id: 'role-2', name: 'Auditor', description: 'Leitura' },
          { status: 201 },
        ),
      ),
    );

    const fixture = await renderList();
    el(fixture, 'create-role').click();
    await settle(fixture, 2);

    await typeOverlay('name', 'Auditor');
    await settle(fixture, 2);
    overlayEl('dialog-save').click();
    await settle(fixture);

    expect(text(fixture, 'row-role-2')).toContain('Auditor');
    await waitFor(fixture, () => expect(TestBed.inject(MatDialog).openDialogs).toHaveLength(0));
  });

  it('409 mantem o dialogo aberto', async () => {
    server.use(
      api.get(ROLES, () => HttpResponse.json(PAGE)),
      api.post(ROLES, () =>
        problem(409, {
          title: 'Business rule violation',
          detail: "Role 'Admin' already exists.",
          status: 409,
        }),
      ),
    );

    const fixture = await renderList();
    el(fixture, 'create-role').click();
    await settle(fixture, 2);

    await typeOverlay('name', 'Admin');
    await settle(fixture, 2);
    overlayEl('dialog-save').click();
    await settle(fixture);

    expect(overlayEl('name-error').textContent?.trim()).toBe("Role 'Admin' already exists.");
    expect(TestBed.inject(MatDialog).openDialogs).toHaveLength(1);
  });

  it('200 atualiza a linha', async () => {
    server.use(
      api.get(ROLES, () => HttpResponse.json(PAGE)),
      api.put(`${ROLES}/role-1`, () =>
        HttpResponse.json({ id: 'role-1', name: 'Administrador', description: 'Acesso total' }),
      ),
    );

    const fixture = await renderList();
    el(fixture, 'edit-role-1').click();
    await settle(fixture, 2);

    await typeOverlay('name', 'Administrador');
    await settle(fixture, 2);
    overlayEl('dialog-save').click();
    await settle(fixture);

    expect(text(fixture, 'row-role-1')).toContain('Administrador');
  });

  it('204 remove o role', async () => {
    server.use(
      api.get(ROLES, () => HttpResponse.json(PAGE)),
      api.delete(`${ROLES}/role-1`, () => new HttpResponse(null, { status: 204 })),
    );
    stubConfirm(true);

    const fixture = await renderList();
    el(fixture, 'delete-role-1').click();
    await settle(fixture);

    expect(maybeEl(fixture, 'row-role-1')).toBeNull();
  });
});
