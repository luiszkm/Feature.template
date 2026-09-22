import {
  API_ORIGIN,
  authToken,
  el,
  provideRouteStub,
  recorder,
  settle,
  tokenWith,
} from '../../testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PageEvent } from '@angular/material/paginator';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test-setup';
import { SessionStore } from '../core/session/session.store';
import { PermissionsList } from '../features/authorization/permissions-list';
import { RolesList } from '../features/authorization/roles-list';
import { UsersList } from '../features/identity/users-list';
import { TenantsList } from '../features/tenants/tenants-list';
import { AgentsList } from '../features/ai/agents-list';

interface ListScreen {
  readonly name: string;
  readonly component: Type<unknown>;
  readonly path: string;
  /** The field the endpoint's `ApplySort` accepts for this screen's first sortable column. */
  readonly firstSortable: string;
}

const LIST_SCREENS: ListScreen[] = [
  {
    name: 'users',
    component: UsersList,
    path: `${API_ORIGIN}/api/v1/identity/users`,
    firstSortable: 'email',
  },
  {
    name: 'roles',
    component: RolesList,
    path: `${API_ORIGIN}/api/v1/authorization/roles`,
    firstSortable: 'name',
  },
  {
    name: 'permissions',
    component: PermissionsList,
    path: `${API_ORIGIN}/api/v1/authorization/permissions`,
    firstSortable: 'name',
  },
  {
    name: 'tenants',
    component: TenantsList,
    path: `${API_ORIGIN}/api/v1/tenants`,
    firstSortable: 'tenantKey',
  },
  {
    name: 'agents',
    component: AgentsList,
    path: `${API_ORIGIN}/api/v1/ai/agents`,
    firstSortable: 'name',
  },
];

/** One row covering every screen's columns: an empty page renders the empty state, not a table. */
const ROW = {
  id: 'row-1',
  tenantId: 'row-1',
  email: 'ana@example.com',
  firstName: 'Ana',
  lastName: 'Silva',
  name: 'Auditor',
  description: 'Leitura',
  tenantKey: 'dev',
  displayName: 'Development',
  isActive: true,
  isolationMode: 0,
  createdAt: '2026-01-02T10:00:00Z',
  lastLoginAt: null,
  agentId: 'row-1',
  instructions: 'hi',
  toolNames: [],
  isDefault: false,
};

const PAGE = { pageNumber: 1, pageSize: 20, totalCount: 1, data: [ROW] };

async function renderScreen(screen: ListScreen) {
  server.use(http.get(screen.path, () => HttpResponse.json(PAGE)));
  provideRouteStub();
  TestBed.inject(SessionStore).apply({
    ...authToken({ roles: ['Admin'] }),
    accessToken: tokenWith([]),
  });

  const fixture = TestBed.createComponent(screen.component);
  await settle(fixture);
  return fixture;
}

describe('query das listas', () => {
  it.each(LIST_SCREENS)(
    'pesquisar envia searchTerm e volta a primeira pagina: $name',
    async (screen) => {
      const requests = recorder();
      const fixture = await renderScreen(screen);

      // Land on page 2 first: fresh-mounted is always page 1 already, so searching from there
      // cannot tell "reset to 1" apart from "never left 1" - the mutant this once let through.
      await (fixture.componentInstance as { changePage(event: PageEvent): Promise<void> }).changePage({
        pageIndex: 1,
        pageSize: 20,
        length: 0,
      });
      await settle(fixture);
      expect(requests.at(-1)?.url.searchParams.get('pageNumber')).toBe('2');

      const search = el<HTMLInputElement>(fixture, 'search');
      search.value = 'ana';
      search.dispatchEvent(new Event('input', { bubbles: true }));
      await settle(fixture);

      const query = requests.at(-1)?.url.searchParams;
      expect(query?.get('searchTerm')).toBe('ana');
      expect(query?.get('pageNumber')).toBe('1');
    },
  );

  it.each(LIST_SCREENS)(
    'ordenar por coluna envia sortBy e sortDirection: $name',
    async (screen) => {
      const requests = recorder();
      const fixture = await renderScreen(screen);

      const header = fixture.nativeElement.querySelector(
        'th[mat-sort-header]',
      ) as HTMLElement | null;
      expect(header).not.toBeNull();

      header!.click();
      await settle(fixture);

      const ascending = requests.at(-1)?.url.searchParams;
      expect(ascending?.get('sortBy')).toBe(screen.firstSortable);
      expect(ascending?.get('sortDirection')).toBe('asc');

      header!.click();
      await settle(fixture);

      expect(requests.at(-1)?.url.searchParams.get('sortDirection')).toBe('desc');

      // MatSort's third click reverts to no sort (`disableClear` is unset, so the cycle is
      // none -> asc -> desc -> none) - the guard that omits `sortBy`/`sortDirection` on that
      // reversion is identical code to the "no choice yet" case below, but a fresh mount never
      // exercises the *transition* back to it, only the untouched starting state.
      header!.click();
      await settle(fixture);

      const reverted = requests.at(-1)?.url.searchParams;
      expect(reverted?.has('sortBy')).toBe(false);
      expect(reverted?.has('sortDirection')).toBe(false);
    },
  );

  it.each(LIST_SCREENS)('sem ordenacao escolhida nao envia sortBy: $name', async (screen) => {
    const requests = recorder();
    await renderScreen(screen);

    // The API owns the default order; the front must not invent one.
    expect(requests.at(-1)?.url.searchParams.has('sortBy')).toBe(false);
  });
});
