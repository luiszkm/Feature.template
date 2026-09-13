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
import { Permissions } from '../../core/permissions';
import { UsersList } from './users-list';

const USERS = '/api/v1/identity/users';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  data: [
    {
      id: 'user-1',
      email: 'admin@producttemplate.com',
      firstName: 'System',
      lastName: 'Administrator',
      createdAt: '2026-01-02T10:00:00Z',
      lastLoginAt: '2026-02-03T11:00:00Z',
    },
  ],
};

describe('UsersList', () => {
  it('carrega a primeira pagina com as quatro colunas', async () => {
    const requests = recorder();
    server.use(api.get(USERS, () => HttpResponse.json(PAGE)));
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(UsersList);
    await settle(fixture);

    const query = requests.at(-1)?.url.searchParams;
    expect(query?.get('pageNumber')).toBe('1');
    expect(query?.get('pageSize')).toBe('20');

    const headers = [...fixture.nativeElement.querySelectorAll('th')].map((th: HTMLElement) =>
      th.textContent?.trim(),
    );
    expect(headers).toEqual(['Email', 'Nome', 'Criado em', 'Último login', '']);
    expect(text(fixture, 'row-user-1')).toContain('admin@producttemplate.com');
  });

  it('sincroniza query params', async () => {
    const requests = recorder();
    server.use(api.get(USERS, () => HttpResponse.json(PAGE)));
    provideRouteStub({}, { pageNumber: '2', pageSize: '10', searchTerm: 'ana' });
    authenticate();

    const fixture = TestBed.createComponent(UsersList);
    await settle(fixture);

    const query = requests.at(-1)?.url.searchParams;
    expect(query?.get('pageNumber')).toBe('2');
    expect(query?.get('pageSize')).toBe('10');
    expect(query?.get('searchTerm')).toBe('ana');
  });

  it('204 remove a linha', async () => {
    server.use(
      api.get(USERS, () => HttpResponse.json(PAGE)),
      api.delete(`${USERS}/user-1`, () => new HttpResponse(null, { status: 204 })),
    );
    provideRouteStub();
    stubConfirm(true);
    authenticate();

    const fixture = TestBed.createComponent(UsersList);
    await settle(fixture);

    el(fixture, 'delete-user-1').click();
    await settle(fixture);

    expect(maybeEl(fixture, 'row-user-1')).toBeNull();
  });

  it('cancelar nao emite requisicao', async () => {
    const requests = recorder();
    server.use(api.get(USERS, () => HttpResponse.json(PAGE)));
    provideRouteStub();
    stubConfirm(false);
    authenticate();

    const fixture = TestBed.createComponent(UsersList);
    await settle(fixture);
    const before = requests.length;

    el(fixture, 'delete-user-1').click();
    await settle(fixture);

    expect(requests.length).toBe(before);
    expect(maybeEl(fixture, 'row-user-1')).not.toBeNull();
  });

  it('esconde as acoes sem identity.user.manage', async () => {
    server.use(api.get(USERS, () => HttpResponse.json(PAGE)));
    provideRouteStub();
    authenticate([Permissions.userRead]);

    const fixture = TestBed.createComponent(UsersList);
    await settle(fixture);

    expect(maybeEl(fixture, 'delete-user-1')).toBeNull();
    expect(maybeEl(fixture, 'edit-user-1')).toBeNull();
  });
});
