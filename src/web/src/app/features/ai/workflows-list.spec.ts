import { TestBed } from '@angular/core/testing';
import { HttpResponse, delay } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { server } from '../../../test-setup';
import {
  api,
  authenticate,
  click,
  el,
  maybeEl,
  problem,
  provideRouteStub,
  recorder,
  render,
  settle,
  text,
} from '../../../testing';
import { ConfirmService } from '../../shared/confirm';
import { WorkflowsList } from './workflows-list';

const WORKFLOWS = '/api/v1/ai/workflows';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 2,
  data: [
    {
      workflowId: 'wf-2',
      name: 'Triagem',
      nodeCount: 3,
      isActive: true,
      updatedAt: '2026-09-23T10:00:00Z',
    },
    {
      workflowId: 'wf-1',
      name: 'Resumo',
      nodeCount: 1,
      isActive: true,
      updatedAt: '2026-09-22T10:00:00Z',
    },
  ],
};

const EMPTY = { pageNumber: 1, pageSize: 20, totalCount: 0, data: [] };

async function renderList(permissions: string[] = [], confirm?: ConfirmService['ask']) {
  provideRouteStub();
  if (confirm) {
    TestBed.overrideProvider(ConfirmService, { useValue: { ask: confirm } });
  }
  authenticate(permissions);
  const fixture = TestBed.createComponent(WorkflowsList);
  await settle(fixture);
  return fixture;
}

describe('WorkflowsList', () => {
  it('mostra colunas e linhas na ordem da API', async () => {
    server.use(api.get(WORKFLOWS, () => HttpResponse.json(PAGE)));
    const fixture = await renderList();
    const root = fixture.nativeElement as HTMLElement;

    const headers = [...root.querySelectorAll('th')].map((th) => th.textContent?.trim());
    expect(headers).toEqual(['Nome', 'Nós', 'Atualizado', 'Ações']);
    const names = [...root.querySelectorAll('tbody tr, tr.mat-mdc-row')]
      .map((row) => row.querySelector('td')?.textContent?.trim())
      .filter(Boolean);
    expect(names).toEqual(['Triagem', 'Resumo']);
    expect(text(fixture, 'row-wf-2')).toBe('Triagem');
  });

  it('mostra o estado de carregamento', async () => {
    server.use(
      api.get(WORKFLOWS, async () => {
        await delay('infinite');
        return HttpResponse.json(PAGE);
      }),
    );
    provideRouteStub();
    authenticate();
    const fixture = TestBed.createComponent(WorkflowsList);
    await render(fixture);

    expect(maybeEl(fixture, 'list-loading')).not.toBeNull();
    expect(maybeEl(fixture, 'workflows-table')).toBeNull();
  });

  it('lista vazia mostra mensagem e novo workflow', async () => {
    server.use(api.get(WORKFLOWS, () => HttpResponse.json(EMPTY)));
    const fixture = await renderList(['ai.agent.read', 'ai.agent.manage']);

    expect(text(fixture, 'list-empty')).toContain('Nenhum workflow ainda');
    expect(text(fixture, 'empty-create-workflow')).toBe('Novo workflow');
    expect(el(fixture, 'empty-create-workflow').getAttribute('href')).toBe('/ai/workflows/new');
  });

  it('erro mostra tentar de novo e repete o pedido', async () => {
    let calls = 0;
    server.use(
      api.get(WORKFLOWS, () => {
        calls++;
        return calls === 1
          ? problem(500, { title: 'Erro inesperado', status: 500 })
          : HttpResponse.json(PAGE);
      }),
    );
    const fixture = await renderList();

    expect(maybeEl(fixture, 'list-error')).not.toBeNull();
    expect(text(fixture, 'list-retry')).toBe('Tentar de novo');
    await click(fixture, 'list-retry');

    expect(calls).toBe(2);
    expect(maybeEl(fixture, 'workflows-table')).not.toBeNull();
  });

  it('desativar confirma com o nome antes do DELETE', async () => {
    const requests = recorder();
    server.use(
      api.get(WORKFLOWS, () => HttpResponse.json(PAGE)),
      api.delete(`${WORKFLOWS}/wf-2`, () => new HttpResponse(null, { status: 204 })),
    );

    // First answer cancels, second confirms; the dialog sees the name both times.
    const ask = vi
      .fn<ConfirmService['ask']>()
      .mockResolvedValueOnce(false)
      .mockResolvedValueOnce(true);
    const fixture = await renderList(['ai.agent.read', 'ai.agent.manage'], ask);

    await click(fixture, 'deactivate-wf-2');
    expect(ask).toHaveBeenLastCalledWith(
      expect.objectContaining({ message: 'Desativar o workflow "Triagem"?' }),
    );
    expect(requests.filter((r) => r.method === 'DELETE')).toHaveLength(0);

    await click(fixture, 'deactivate-wf-2');
    await settle(fixture);
    expect(ask).toHaveBeenCalledTimes(2);
    expect(ask).toHaveBeenLastCalledWith(
      expect.objectContaining({ message: 'Desativar o workflow "Triagem"?' }),
    );
    const deletes = requests.filter((r) => r.method === 'DELETE');
    expect(deletes.map((r) => r.url.pathname)).toEqual([`${WORKFLOWS}/wf-2`]);
    expect(maybeEl(fixture, 'row-wf-2')).toBeNull();
  });

  it('sem manage esconde novo e desativar', async () => {
    server.use(api.get(WORKFLOWS, () => HttpResponse.json(PAGE)));
    const fixture = await renderList(['ai.agent.read']);

    expect(maybeEl(fixture, 'create-workflow')).toBeNull();
    expect(maybeEl(fixture, 'deactivate-wf-2')).toBeNull();
    expect(maybeEl(fixture, 'deactivate-wf-1')).toBeNull();
    expect(maybeEl(fixture, 'row-wf-2')).not.toBeNull();
  });
});
