import {
  API_ORIGIN,
  authToken,
  el,
  maybeEl,
  problem,
  provideRouteStub,
  render,
  settle,
  text,
  tokenWith,
} from '../../testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatPaginator } from '@angular/material/paginator';
import { By } from '@angular/platform-browser';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../test-setup';
import { SessionStore } from '../core/session/session.store';
import { PermissionsList } from '../features/authorization/permissions-list';
import { RolesList } from '../features/authorization/roles-list';
import { UsersList } from '../features/identity/users-list';
import { TenantsList } from '../features/tenants/tenants-list';
import { AgentsList } from '../features/ai/agents-list';
import { ConversationsList } from '../features/ai/conversations-list';
import { Usage } from '../features/ai/usage';

interface ListScreen {
  readonly name: string;
  readonly component: Type<unknown>;
  readonly path: string;
}

/** Every list screen renders the same three states through `app-list-state`. */
const LIST_SCREENS: ListScreen[] = [
  { name: 'users', component: UsersList, path: `${API_ORIGIN}/api/v1/identity/users` },
  { name: 'roles', component: RolesList, path: `${API_ORIGIN}/api/v1/authorization/roles` },
  {
    name: 'permissions',
    component: PermissionsList,
    path: `${API_ORIGIN}/api/v1/authorization/permissions`,
  },
  { name: 'tenants', component: TenantsList, path: `${API_ORIGIN}/api/v1/tenants` },
  { name: 'agents', component: AgentsList, path: `${API_ORIGIN}/api/v1/ai/agents` },
  {
    name: 'conversations',
    component: ConversationsList,
    path: `${API_ORIGIN}/api/v1/ai/conversations`,
  },
  { name: 'usage', component: Usage, path: `${API_ORIGIN}/api/v1/ai/usage` },
];

function authenticate(): void {
  TestBed.inject(SessionStore).apply({
    ...authToken({ roles: ['Admin'] }),
    accessToken: tokenWith([]),
  });
}

describe('estados partilhados das listas', () => {
  it.each(LIST_SCREENS)('estado de carregamento: $name', async (screen) => {
    let release!: () => void;
    const pending = new Promise<void>((resolve) => (release = resolve));
    server.use(
      http.get(screen.path, async () => {
        await pending;
        return HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 0, data: [] });
      }),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(screen.component);
    await render(fixture);

    expect(maybeEl(fixture, 'list-loading')).not.toBeNull();
    expect(fixture.debugElement.query(By.directive(MatPaginator)).componentInstance.disabled).toBe(
      true,
    );

    release();
    await settle(fixture);
  });

  it.each(LIST_SCREENS)('estado vazio: $name', async (screen) => {
    server.use(
      http.get(screen.path, () =>
        HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 0, data: [] }),
      ),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(screen.component);
    await settle(fixture);

    expect(maybeEl(fixture, 'list-empty')).not.toBeNull();
  });

  it.each(LIST_SCREENS)('estado de erro repete a query: $name', async (screen) => {
    let calls = 0;
    server.use(
      http.get(screen.path, () => {
        calls += 1;
        return calls === 1
          ? problem(500, { title: 'Unexpected error', status: 500 })
          : HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 0, data: [] });
      }),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(screen.component);
    await settle(fixture);

    expect(text(fixture, 'list-error-title')).toBe('Unexpected error');
    expect(text(fixture, 'list-retry')).toBe('Tentar de novo');

    el(fixture, 'list-retry').click();
    await settle(fixture);

    expect(calls).toBe(2);
    expect(maybeEl(fixture, 'list-error')).toBeNull();
  });
});
