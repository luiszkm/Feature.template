import {
  api,
  authenticate,
  el,
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
import { TenantsList } from './tenants-list';

const TENANTS = '/api/v1/tenants';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  data: [
    {
      tenantId: 'tenant-1',
      tenantKey: 'dev',
      displayName: 'Development',
      contactEmail: 'dev@example.com',
      isActive: true,
      isolationMode: 0,
      createdAt: '2026-01-02T10:00:00Z',
    },
  ],
};

async function renderList() {
  provideRouteStub();
  authenticate();
  const fixture = TestBed.createComponent(TenantsList);
  await settle(fixture);
  return fixture;
}

describe('TenantsList', () => {
  it('carrega a primeira pagina com as quatro colunas', async () => {
    const requests = recorder();
    server.use(api.get(TENANTS, () => HttpResponse.json(PAGE)));

    const fixture = await renderList();

    expect(requests.at(-1)?.url.searchParams.get('pageSize')).toBe('20');
    expect(text(fixture, 'row-tenant-1')).toContain('dev');
    expect(text(fixture, 'name-tenant-1')).toBe('Development');
    expect(text(fixture, 'active-tenant-1')).toBe('Sim');
    expect(text(fixture, 'isolation-tenant-1')).toBe('Partilhado');
  });

  it('204 marca a linha como inativa', async () => {
    server.use(
      api.get(TENANTS, () => HttpResponse.json(PAGE)),
      api.delete(`${TENANTS}/tenant-1`, () => new HttpResponse(null, { status: 204 })),
    );
    stubConfirm(true);

    const fixture = await renderList();
    el(fixture, 'deactivate-tenant-1').click();
    await settle(fixture);

    expect(text(fixture, 'active-tenant-1')).toBe('Não');
  });
});
