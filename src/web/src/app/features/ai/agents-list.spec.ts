import {
  api,
  authenticate,
  el,
  maybeEl,
  problem,
  provideRouteStub,
  recorder,
  settle,
  stubConfirm,
  text,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { SessionStore } from '../../core/session/session.store';
import { authToken, tokenWith } from '../../../testing';
import { AgentsList } from './agents-list';
import { AiAvailability } from './ai-availability';
import { Forbidden } from '../../shared/screens';

const AGENTS = '/api/v1/ai/agents';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 1,
  data: [
    {
      agentId: 'agent-1',
      name: 'Default',
      instructions: 'hi',
      toolNames: ['get_tenant_info'],
      isActive: true,
      isDefault: true,
      createdAt: '2026-01-02T10:00:00Z',
    },
  ],
};

async function renderList() {
  provideRouteStub();
  authenticate();
  const fixture = TestBed.createComponent(AgentsList);
  await settle(fixture);
  return fixture;
}

describe('AgentsList', () => {
  it('estado vazio', async () => {
    server.use(
      api.get(AGENTS, () =>
        HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 0, data: [] }),
      ),
    );
    const fixture = await renderList();

    expect(text(fixture, 'list-empty')).toContain('Nenhum agente');
    const action = fixture.nativeElement.querySelector('[emptyAction], a[routerLink="/ai/agents/new"]');
    expect(action?.textContent).toContain('Criar agente');
    expect(action?.getAttribute('routerLink') ?? action?.getAttribute('ng-reflect-router-link')).toContain(
      '/ai/agents/new',
    );
  });

  it('arranjo igual a tenants', async () => {
    server.use(api.get(AGENTS, () => HttpResponse.json(PAGE)));
    const fixture = await renderList();
    const root = fixture.nativeElement as HTMLElement;
    const header = root.querySelector('header');
    const search = root.querySelector('mat-form-field');
    const state = root.querySelector('app-list-state');
    const table = root.querySelector('table');
    const paginator = root.querySelector('mat-paginator');

    expect(header).not.toBeNull();
    expect(header!.querySelector('h1')?.textContent).toBe('Agentes');
    expect(header!.querySelector('[data-testid="create-agent"]')?.textContent).toContain('Criar agente');
    expect(search!.compareDocumentPosition(state!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(state!.compareDocumentPosition(table!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(table!.compareDocumentPosition(paginator!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('desativar com confirm', async () => {
    server.use(
      api.get(AGENTS, () => HttpResponse.json(PAGE)),
      api.delete(`${AGENTS}/agent-1`, () => new HttpResponse(null, { status: 204 })),
    );
    stubConfirm(true);

    const fixture = await renderList();
    el(fixture, 'deactivate-agent-1').click();
    await settle(fixture);

    expect(text(fixture, 'active-agent-1')).toBe('Não');
  });

  it('desativar cancelado', async () => {
    const requests = recorder();
    server.use(api.get(AGENTS, () => HttpResponse.json(PAGE)));
    stubConfirm(false);

    const fixture = await renderList();
    el(fixture, 'deactivate-agent-1').click();
    await settle(fixture);

    expect(requests.filter((request) => request.method === 'DELETE')).toHaveLength(0);
    expect(text(fixture, 'active-agent-1')).toBe('Sim');
  });

  it('sem permissao vai para forbidden', async () => {
    TestBed.inject(SessionStore).apply({
      ...authToken({ roles: [], permissions: [] }),
      accessToken: tokenWith([]),
    });
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/ai/agents');
    expect(router.url).toBe('/forbidden');

    const fixture = TestBed.createComponent(Forbidden);
    await settle(fixture, 2);
    expect(text(fixture, 'forbidden')).toContain('Sem permissão para esta operação');
  });

  it('404 Feature disabled desliga a disponibilidade', async () => {
    server.use(
      api.get(AGENTS, () => problem(404, { title: 'Feature disabled', status: 404 })),
    );
    const fixture = await renderList();

    expect(text(fixture, 'list-error-title')).toBe('Feature disabled');
    expect(TestBed.inject(AiAvailability).available()).toBe(false);
  });
});
