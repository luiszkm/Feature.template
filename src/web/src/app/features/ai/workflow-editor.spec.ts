import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { DatePipe } from '@angular/common';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
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
  type,
  waitUntil,
} from '../../../testing';
import { ConfirmService } from '../../shared/confirm';
import {
  WorkflowNode,
  WorkflowOutput,
  WorkflowRunOutput,
  WorkflowRunStepOutput,
  WorkflowStepStatus,
} from './ai.contracts';
import {
  CYCLE_MESSAGE,
  DUPLICATE_EDGE_MESSAGE,
  WORKFLOW_RUN_POLL_MS,
  WorkflowEditor,
} from './workflow-editor';

const WORKFLOWS = '/api/v1/ai/workflows';
const WF = `${WORKFLOWS}/wf-1`;
const RUNS = `${WF}/runs`;
const MANAGE = ['ai.agent.read', 'ai.agent.manage'];

const AGENTS = {
  pageNumber: 1,
  pageSize: 100,
  totalCount: 2,
  data: [
    {
      agentId: 'ag-1',
      name: 'Triagem',
      instructions: 'i',
      toolNames: [],
      isActive: true,
      isDefault: false,
      createdAt: '2026-01-01T00:00:00Z',
      model: null,
    },
    {
      agentId: 'ag-2',
      name: 'Redator',
      instructions: 'i',
      toolNames: [],
      isActive: true,
      isDefault: false,
      createdAt: '2026-01-01T00:00:00Z',
      model: null,
    },
  ],
};

function node(
  key: string,
  x = 0,
  y = 0,
  agentId = 'ag-1',
  instruction: string | null = null,
): WorkflowNode {
  return { key, agentId, instruction, x, y };
}

function workflow(overrides: Partial<WorkflowOutput> = {}): WorkflowOutput {
  return {
    workflowId: 'wf-1',
    name: 'Esteira',
    description: null,
    isActive: true,
    nodes: [node('a', 10, 20), node('b', 300, 40, 'ag-2')],
    edges: [{ from: 'a', to: 'b' }],
    createdAt: '2026-09-23T10:00:00Z',
    updatedAt: '2026-09-23T10:00:00Z',
    ...overrides,
  };
}

function step(
  nodeKey: string,
  status: WorkflowStepStatus,
  overrides: Partial<WorkflowRunStepOutput> = {},
): WorkflowRunStepOutput {
  return {
    nodeKey,
    agentId: 'ag-1',
    status,
    output: null,
    inputTokens: 0,
    outputTokens: 0,
    cost: null,
    latencyMs: 0,
    iterationsUsed: 0,
    errorCode: null,
    startedAt: null,
    finishedAt: null,
    ...overrides,
  };
}

function runOf(overrides: Partial<WorkflowRunOutput> = {}): WorkflowRunOutput {
  return {
    runId: 'run-1',
    workflowId: 'wf-1',
    status: 'Succeeded',
    input: 'olá',
    errorCode: null,
    createdAt: '2026-09-23T10:00:00Z',
    startedAt: '2026-09-23T10:00:00.000Z',
    finishedAt: '2026-09-23T10:00:03.200Z',
    createdByUserId: 'user-1',
    totalCost: 0.003,
    nodes: [node('a', 10, 20), node('b', 300, 40, 'ag-2')],
    edges: [{ from: 'a', to: 'b' }],
    steps: [step('a', 'Succeeded'), step('b', 'Succeeded')],
    ...overrides,
  };
}

function summaryOf(run: WorkflowRunOutput) {
  return {
    runId: run.runId,
    status: run.status,
    inputPreview: run.input,
    totalCost: run.totalCost,
    createdAt: run.createdAt,
    finishedAt: run.finishedAt,
  };
}

const EMPTY_RUNS = { pageNumber: 1, pageSize: 20, totalCount: 0, data: [] };

interface Options {
  existing?: WorkflowOutput | null;
  permissions?: string[];
  runs?: WorkflowRunOutput[];
  ask?: ConfirmService['ask'];
  pollMs?: number;
}

function serve({ existing = workflow(), runs = [] }: Options = {}) {
  server.use(
    api.get('/api/v1/ai/agents', () => HttpResponse.json(AGENTS)),
    api.get(RUNS, () =>
      HttpResponse.json({ ...EMPTY_RUNS, totalCount: runs.length, data: runs.map(summaryOf) }),
    ),
  );
  if (existing) {
    server.use(api.get(WF, () => HttpResponse.json(existing)));
  }
  for (const run of runs) {
    server.use(api.get(`${RUNS}/${run.runId}`, () => HttpResponse.json(run)));
  }
}

async function renderEditor(options: Options = {}): Promise<ComponentFixture<WorkflowEditor>> {
  const { existing = workflow(), permissions = MANAGE } = options;
  provideRouteStub(existing ? { workflowId: existing.workflowId } : {});
  if (options.ask) {
    TestBed.overrideProvider(ConfirmService, { useValue: { ask: options.ask } });
  }
  TestBed.overrideProvider(WORKFLOW_RUN_POLL_MS, { useValue: options.pollMs ?? 60_000 });
  authenticate(permissions);
  const fixture = TestBed.createComponent(WorkflowEditor);
  await settle(fixture);
  return fixture;
}

function edgeCount(fixture: ComponentFixture<unknown>): number {
  return (fixture.nativeElement as HTMLElement).querySelectorAll('path.edge').length;
}

async function clickSvg(fixture: ComponentFixture<unknown>, testId: string): Promise<void> {
  el(fixture, testId).dispatchEvent(new MouseEvent('click', { bubbles: true }));
  await settle(fixture);
}

async function openRun(fixture: ComponentFixture<unknown>, runId = 'run-1'): Promise<void> {
  await click(fixture, `run-row-${runId}`);
  await settle(fixture);
}

describe('WorkflowEditor', () => {
  // ---- canvas ----------------------------------------------------------------------------

  it('mostra carregamento antes do canvas', async () => {
    server.use(
      api.get('/api/v1/ai/agents', () => HttpResponse.json(AGENTS)),
      api.get(WF, async () => {
        await delay('infinite');
        return HttpResponse.json(workflow());
      }),
    );
    provideRouteStub({ workflowId: 'wf-1' });
    authenticate(MANAGE);
    const fixture = TestBed.createComponent(WorkflowEditor);
    await render(fixture);

    expect(maybeEl(fixture, 'editor-loading')).not.toBeNull();
    expect(maybeEl(fixture, 'canvas')).toBeNull();
  });

  it('desenha nos nas posicoes gravadas e setas', async () => {
    serve();
    const fixture = await renderEditor();

    const a = el(fixture, 'node-a');
    expect(a.style.left).toBe('10px');
    expect(a.style.top).toBe('20px');
    expect(a.querySelector('[data-testid="node-agent"]')?.textContent?.trim()).toBe('Triagem');
    expect(a.querySelector('[data-testid="node-key"]')?.textContent?.trim()).toBe('a');
    const b = el(fixture, 'node-b');
    expect(b.style.left).toBe('300px');
    expect(b.style.top).toBe('40px');
    expect(b.querySelector('[data-testid="node-agent"]')?.textContent?.trim()).toBe('Redator');
    expect(maybeEl(fixture, 'edge-a-b')).not.toBeNull();
    expect(edgeCount(fixture)).toBe(1);
  });

  it('paleta acrescenta no com key unica', async () => {
    serve({ existing: null });
    const fixture = await renderEditor({ existing: null });

    await click(fixture, 'palette-ag-1');
    await click(fixture, 'palette-ag-1');

    expect(maybeEl(fixture, 'node-triagem')).not.toBeNull();
    expect(maybeEl(fixture, 'node-triagem-2')).not.toBeNull();
  });

  it('arrastar move o no e guardar envia a posicao', async () => {
    const requests = recorder();
    serve({ existing: workflow({ nodes: [node('a')], edges: [] }) });
    server.use(api.put(WF, () => HttpResponse.json(workflow())));
    const fixture = await renderEditor({ existing: workflow({ nodes: [node('a')], edges: [] }) });

    fixture.debugElement
      .query(By.css('[data-testid="node-a"]'))
      .triggerEventHandler('cdkDragEnded', {
        distance: { x: 200, y: 150 },
        source: { reset: () => undefined },
      });
    await settle(fixture);
    expect(el(fixture, 'node-a').style.left).toBe('200px');
    expect(el(fixture, 'node-a').style.top).toBe('150px');

    await click(fixture, 'save');
    const put = requests.find((r) => r.method === 'PUT')!;
    expect((put.body as { nodes: WorkflowNode[] }).nodes[0]).toMatchObject({
      key: 'a',
      x: 200,
      y: 150,
    });
  });

  it('arrastar move o no e guardar envia a posicao: setas acompanham', async () => {
    serve();
    const fixture = await renderEditor();
    const before = el(fixture, 'edge-a-b').getAttribute('d');
    const node = fixture.debugElement.query(By.css('[data-testid="node-a"]'));

    node.triggerEventHandler('cdkDragMoved', { distance: { x: 100, y: 30 } });
    await settle(fixture);
    const during = el(fixture, 'edge-a-b').getAttribute('d');
    expect(during).not.toBe(before);
    expect(during!.startsWith('M 290 82')).toBe(true);

    node.triggerEventHandler('cdkDragEnded', {
      distance: { x: 100, y: 30 },
      source: { reset: () => undefined },
    });
    await settle(fixture);
    expect(el(fixture, 'edge-a-b').getAttribute('d')).toBe(during);
  });

  it('arranjo: paleta, canvas e painel a direita, com os rotulos', async () => {
    serve();
    const fixture = await renderEditor();

    const regions = [
      ...(fixture.nativeElement as HTMLElement).querySelector('.workspace')!.children,
    ].map((child) => child.getAttribute('data-testid'));
    expect(regions).toEqual(['palette', 'canvas', 'panel']);
    expect(el(fixture, 'palette').querySelector('h2')?.textContent?.trim()).toBe(
      'Adicionar agente',
    );
    expect(text(fixture, 'save')).toBe('Guardar');
    expect(text(fixture, 'run-start')).toBe('Executar');

    await click(fixture, 'node-a');
    expect(text(fixture, 'remove-node')).toBe('Remover nó');
    await clickSvg(fixture, 'edge-a-b');
    expect(text(fixture, 'remove-edge')).toBe('Remover ligação');
  });

  it('ligar porta de saida a outro no cria aresta', async () => {
    serve({ existing: workflow({ edges: [] }) });
    const fixture = await renderEditor({ existing: workflow({ edges: [] }) });

    await click(fixture, 'port-out-a');
    await click(fixture, 'node-b');

    expect(maybeEl(fixture, 'edge-a-b')).not.toBeNull();
    expect(edgeCount(fixture)).toBe(1);
  });

  it('ligacao invalida mostra mensagem e nao e criada', async () => {
    serve();
    const fixture = await renderEditor();

    await click(fixture, 'port-out-b');
    await click(fixture, 'node-a');
    expect(text(fixture, 'canvas-message')).toBe(CYCLE_MESSAGE);
    expect(CYCLE_MESSAGE).toBe('Esta ligação criaria um ciclo');
    expect(edgeCount(fixture)).toBe(1);

    await click(fixture, 'port-out-a');
    await click(fixture, 'node-b');
    expect(text(fixture, 'canvas-message')).toBe(DUPLICATE_EDGE_MESSAGE);
    expect(DUPLICATE_EDGE_MESSAGE).toBe('Ligação já existe');
    expect(edgeCount(fixture)).toBe(1);
  });

  it('painel do no edita instrucao e remove no com arestas', async () => {
    const requests = recorder();
    serve();
    server.use(api.put(WF, () => HttpResponse.json(workflow())));
    const fixture = await renderEditor();

    await click(fixture, 'node-a');
    expect(el(fixture, 'node-panel').textContent).toContain('Triagem');
    await type(fixture, 'node-instruction', 'Resuma');
    await click(fixture, 'save');
    const put = requests.find((r) => r.method === 'PUT')!;
    expect((put.body as { nodes: WorkflowNode[] }).nodes[0]).toMatchObject({
      key: 'a',
      instruction: 'Resuma',
    });

    await click(fixture, 'node-a');
    await click(fixture, 'remove-node');
    expect(maybeEl(fixture, 'node-a')).toBeNull();
    expect(maybeEl(fixture, 'edge-a-b')).toBeNull();
    expect(edgeCount(fixture)).toBe(0);
  });

  it('remover ligacao apaga a aresta', async () => {
    const edges = [
      { from: 'a', to: 'b' },
      { from: 'a', to: 'c' },
    ];
    const existing = workflow({ nodes: [node('a'), node('b', 300), node('c', 300, 200)], edges });
    serve({ existing });
    const fixture = await renderEditor({ existing });

    await clickSvg(fixture, 'edge-a-b');
    await click(fixture, 'remove-edge');
    expect(maybeEl(fixture, 'edge-a-b')).toBeNull();
    expect(edgeCount(fixture)).toBe(1);

    await clickSvg(fixture, 'edge-a-c');
    el(fixture, 'canvas').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Delete', bubbles: true }),
    );
    await settle(fixture);
    expect(maybeEl(fixture, 'edge-a-c')).toBeNull();
    expect(edgeCount(fixture)).toBe(0);
  });

  it('guardar faz POST e navega ou PUT', async () => {
    const requests = recorder();
    serve({ existing: null });
    server.use(
      api.post(WORKFLOWS, () =>
        HttpResponse.json(workflow({ workflowId: 'wf-new' }), { status: 201 }),
      ),
    );
    const fixture = await renderEditor({ existing: null });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    await click(fixture, 'palette-ag-1');
    await click(fixture, 'palette-ag-2');
    await click(fixture, 'port-out-triagem');
    await click(fixture, 'node-redator');
    await click(fixture, 'save');

    const post = requests.find((r) => r.method === 'POST' && r.url.pathname === WORKFLOWS)!;
    const body = post.body as { nodes: WorkflowNode[]; edges: unknown[] };
    expect(body.nodes.map((n) => n.key)).toEqual(['triagem', 'redator']);
    expect(body.edges).toEqual([{ from: 'triagem', to: 'redator' }]);
    expect(navigate).toHaveBeenCalledWith(['/ai/workflows', 'wf-new']);
  });

  it('guardar faz POST e navega ou PUT (existente)', async () => {
    const requests = recorder();
    serve();
    server.use(api.put(WF, () => HttpResponse.json(workflow())));
    const fixture = await renderEditor();

    await type(fixture, 'workflow-name', 'Renomeado');
    await click(fixture, 'save');

    const put = requests.find((r) => r.method === 'PUT')!;
    expect(put.url.pathname).toBe(WF);
    expect((put.body as { name: string }).name).toBe('Renomeado');
    expect(requests.some((r) => r.method === 'POST')).toBe(false);
  });

  it('400 mostra erro do campo e mantem o canvas', async () => {
    serve();
    server.use(
      api.put(WF, () =>
        problem(400, {
          title: 'Validation failed',
          status: 400,
          errors: {
            name: ['O nome é obrigatório.'],
            nodes: ['Cada nó precisa de uma key única.'],
            edges: ['As ligações formam um ciclo.'],
          },
        }),
      ),
    );
    const fixture = await renderEditor();

    await type(fixture, 'workflow-name', 'x');
    await click(fixture, 'save');

    expect(text(fixture, 'error-edges')).toBe('As ligações formam um ciclo.');
    expect(text(fixture, 'error-name')).toBe('O nome é obrigatório.');
    expect(text(fixture, 'error-nodes')).toBe('Cada nó precisa de uma key única.');
    expect(maybeEl(fixture, 'node-a')).not.toBeNull();
    expect(maybeEl(fixture, 'node-b')).not.toBeNull();
    expect(maybeEl(fixture, 'edge-a-b')).not.toBeNull();
  });

  it('sem nos mostra mensagem e desativa guardar', async () => {
    serve({ existing: null });
    const fixture = await renderEditor({ existing: null });

    expect(text(fixture, 'canvas-empty')).toBe('Adicione um agente para começar');
    expect(el<HTMLButtonElement>(fixture, 'save').disabled).toBe(true);
  });

  it('sem manage mostra canvas so de leitura', async () => {
    serve();
    const fixture = await renderEditor({ permissions: ['ai.agent.read'] });

    expect(maybeEl(fixture, 'palette')).toBeNull();
    expect(maybeEl(fixture, 'save')).toBeNull();
    expect(maybeEl(fixture, 'run-start')).toBeNull();
    expect(maybeEl(fixture, 'port-out-a')).toBeNull();
    expect(el(fixture, 'node-a').classList.contains('cdk-drag-disabled')).toBe(true);
    expect(text(fixture, 'workflow-title')).toBe('Esteira');
  });

  it('sair com alteracoes pede confirmacao', async () => {
    serve();
    const ask = vi
      .fn<ConfirmService['ask']>()
      .mockResolvedValueOnce(false)
      .mockResolvedValueOnce(true);
    const fixture = await renderEditor({ ask });

    expect(await fixture.componentInstance.canLeave()).toBe(true);
    expect(ask).not.toHaveBeenCalled();

    await type(fixture, 'workflow-name', 'mudou');
    expect(await fixture.componentInstance.canLeave()).toBe(false);
    expect(ask).toHaveBeenLastCalledWith(
      expect.objectContaining({ message: 'Sair sem guardar as alterações?' }),
    );
    expect(await fixture.componentInstance.canLeave()).toBe(true);
    expect(ask).toHaveBeenCalledTimes(2);
  });

  it('404 mostra workflow nao encontrado', async () => {
    server.use(
      api.get('/api/v1/ai/agents', () => HttpResponse.json(AGENTS)),
      api.get(WF, () => problem(404, { title: 'Not found', status: 404 })),
    );
    const fixture = await renderEditor();

    expect(el(fixture, 'workflow-not-found').textContent).toContain('Workflow não encontrado');
    expect(el(fixture, 'back-to-workflows').getAttribute('href')).toBe('/ai/workflows');
    expect(maybeEl(fixture, 'canvas')).toBeNull();
  });

  // ---- runs ------------------------------------------------------------------------------

  it('executar envia POST e abre a vista do run', async () => {
    const requests = recorder();
    serve();
    server.use(api.post(RUNS, () => HttpResponse.json(runOf(), { status: 202 })));
    const fixture = await renderEditor();

    await type(fixture, 'run-input', 'olá');
    await click(fixture, 'run-start');

    const post = requests.find((r) => r.method === 'POST' && r.url.pathname === RUNS)!;
    expect(post.body).toEqual({ input: 'olá' });
    expect(maybeEl(fixture, 'run-view')).not.toBeNull();
  });

  it('alteracoes por guardar desativam executar', async () => {
    serve();
    const fixture = await renderEditor();
    await type(fixture, 'run-input', 'olá');
    expect(el<HTMLButtonElement>(fixture, 'run-start').disabled).toBe(false);

    await type(fixture, 'workflow-name', 'mudou');

    expect(el<HTMLButtonElement>(fixture, 'run-start').disabled).toBe(true);
    expect(text(fixture, 'run-hint')).toBe('Guarde antes de executar');
  });

  it('polling para no estado terminal e ao sair: intervalo por omissao', () => {
    expect(TestBed.inject(WORKFLOW_RUN_POLL_MS)).toBe(2000);
  });

  it('polling para no estado terminal e ao sair: para no terminal', async () => {
    const requests = recorder();
    serve();
    let reads = 0;
    server.use(
      api.post(RUNS, () =>
        HttpResponse.json(
          runOf({ status: 'Queued', steps: [step('a', 'Pending'), step('b', 'Pending')] }),
          { status: 202 },
        ),
      ),
      api.get(`${RUNS}/run-1`, () => {
        reads++;
        return HttpResponse.json(
          reads === 1
            ? runOf({ status: 'Running', steps: [step('a', 'Running'), step('b', 'Pending')] })
            : runOf({ status: 'Succeeded' }),
        );
      }),
    );
    const fixture = await renderEditor({ pollMs: 10 });

    await type(fixture, 'run-input', 'olá');
    await click(fixture, 'run-start');
    await waitUntil(() => expect(reads).toBe(2));
    await new Promise((resolve) => setTimeout(resolve, 100));
    await settle(fixture);

    expect(reads).toBe(2);
    expect(
      requests.filter((r) => r.method === 'GET' && r.url.pathname === `${RUNS}/run-1`),
    ).toHaveLength(2);
    expect(text(fixture, 'run-status')).toBe('Concluído');
  });

  it('polling para no estado terminal e ao sair: para em Failed', async () => {
    serve();
    let reads = 0;
    server.use(
      api.post(RUNS, () => HttpResponse.json(runOf({ status: 'Running' }), { status: 202 })),
      api.get(`${RUNS}/run-1`, () => {
        reads++;
        return HttpResponse.json(
          runOf({
            status: 'Failed',
            steps: [step('a', 'Failed', { errorCode: 'Timeout' }), step('b', 'Skipped')],
          }),
        );
      }),
    );
    const fixture = await renderEditor({ pollMs: 10 });

    await type(fixture, 'run-input', 'olá');
    await click(fixture, 'run-start');
    await waitUntil(() => expect(reads).toBe(1));
    await new Promise((resolve) => setTimeout(resolve, 100));
    await settle(fixture);

    expect(reads).toBe(1);
    expect(text(fixture, 'run-status')).toBe('Falhou');
  });

  it('polling para no estado terminal e ao sair: para ao destruir', async () => {
    serve();
    let reads = 0;
    server.use(
      api.post(RUNS, () => HttpResponse.json(runOf({ status: 'Running' }), { status: 202 })),
      api.get(`${RUNS}/run-1`, () => {
        reads++;
        return HttpResponse.json(runOf({ status: 'Running' }));
      }),
    );
    const fixture = await renderEditor({ pollMs: 10 });

    await type(fixture, 'run-input', 'olá');
    await click(fixture, 'run-start');
    await waitUntil(() => expect(reads).toBeGreaterThan(0));
    fixture.destroy();
    const seen = reads;
    await new Promise((resolve) => setTimeout(resolve, 100));

    expect(reads).toBeLessThanOrEqual(seen + 1);
    const after = reads;
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(reads).toBe(after);
  });

  it.each([
    ['Pending', 'Pendente'],
    ['Running', 'Em execução'],
    ['Succeeded', 'Concluído'],
    ['Failed', 'Falhou'],
    ['Skipped', 'Ignorado'],
  ] as [WorkflowStepStatus, string][])(
    'no do run mostra o texto do estado: %s',
    async (status, label) => {
      const run = runOf({ status: 'Failed', steps: [step('a', status), step('b', 'Pending')] });
      serve({ runs: [run] });
      const fixture = await renderEditor({ runs: [run] });
      await openRun(fixture);

      expect(text(fixture, 'node-status-a')).toBe(label);
      expect(el(fixture, 'node-a').contains(el(fixture, 'node-status-a'))).toBe(true);
      const spinner = el(fixture, 'node-status-a').querySelector('mat-progress-spinner');
      expect(spinner !== null).toBe(status === 'Running');
    },
  );

  it('selecionar no do run mostra detalhe do passo', async () => {
    const run = runOf({
      status: 'Failed',
      steps: [
        step('a', 'Succeeded', {
          output: 'resposta',
          inputTokens: 3,
          outputTokens: 4,
          cost: 0.001,
          latencyMs: 12,
        }),
        step('b', 'Failed', { errorCode: 'Timeout', latencyMs: 120000 }),
      ],
    });
    serve({ runs: [run] });
    const fixture = await renderEditor({ runs: [run] });
    await openRun(fixture);

    await click(fixture, 'node-a');
    expect(text(fixture, 'step-output')).toBe('resposta');
    expect(text(fixture, 'step-tokens')).toBe('Tokens: 3 in / 4 out');
    expect(text(fixture, 'step-cost')).toBe('Custo: $0.001');
    expect(text(fixture, 'step-latency')).toBe('Latência: 12 ms');

    await click(fixture, 'node-b');
    expect(text(fixture, 'step-error')).toBe('Timeout');
  });

  it('run terminado mostra estado custo e duracao', async () => {
    const run = runOf();
    serve({ runs: [run] });
    const fixture = await renderEditor({ runs: [run] });
    await openRun(fixture);

    expect(text(fixture, 'run-status')).toBe('Concluído');
    expect(text(fixture, 'run-cost')).toBe('$0.003');
    expect(text(fixture, 'run-duration')).toBe('3.2 s');
  });

  it('run terminado mostra estado custo e duracao (custo nulo)', async () => {
    const run = runOf({ totalCost: null });
    serve({ runs: [run] });
    const fixture = await renderEditor({ runs: [run] });
    await openRun(fixture);

    expect(text(fixture, 'run-cost')).toBe('—');
  });

  it('lista de execucoes: colunas e abrir', async () => {
    const run = runOf({ runId: 'run-9', input: 'triar o pedido' });
    serve({ runs: [run] });
    const fixture = await renderEditor({ runs: [run] });

    const headers = [...el(fixture, 'runs-table').querySelectorAll('th')].map((th) =>
      th.textContent?.trim(),
    );
    expect(headers).toEqual(['Estado', 'Input', 'Custo', 'Início']);
    const cells = [...el(fixture, 'run-row-run-9').querySelectorAll('td')].map((td) =>
      td.textContent?.trim(),
    );
    expect(cells.slice(0, 3)).toEqual(['Concluído', 'triar o pedido', '$0.003']);
    expect(maybeEl(fixture, 'run-view')).toBeNull();

    await openRun(fixture, 'run-9');
    expect(maybeEl(fixture, 'run-view')).not.toBeNull();
  });

  it('lista de execucoes: ordem da API e inicio', async () => {
    const newer = runOf({ runId: 'run-2', createdAt: '2026-09-23T12:00:00Z' });
    const older = runOf({ runId: 'run-1', createdAt: '2026-09-22T08:30:00Z' });
    serve({ runs: [newer, older] });
    const fixture = await renderEditor({ runs: [newer, older] });

    const rows = [...el(fixture, 'runs-table').querySelectorAll('tbody tr')].map((row) =>
      row.getAttribute('data-testid'),
    );
    expect(rows).toEqual(['run-row-run-2', 'run-row-run-1']);
    const started = el(fixture, 'run-row-run-2').querySelectorAll('td')[3].textContent?.trim();
    expect(started).toBe(new DatePipe('en-US').transform(newer.createdAt, 'short'));
  });

  it('lista de execucoes: vazia', async () => {
    serve();
    const fixture = await renderEditor();

    expect(text(fixture, 'runs-empty')).toBe('Nenhuma execução ainda');
  });

  it('429 ao executar mostra mensagem e mantem input', async () => {
    serve();
    server.use(
      api.post(RUNS, () =>
        problem(429, {
          title: 'AI rate limit exceeded',
          status: 429,
          detail: 'Limite de pedidos de IA do tenant atingido.',
        }),
      ),
    );
    const fixture = await renderEditor();

    await type(fixture, 'run-input', 'olá');
    await click(fixture, 'run-start');

    expect(text(fixture, 'run-error')).toBe('Limite de pedidos de IA do tenant atingido.');
    expect(el<HTMLTextAreaElement>(fixture, 'run-input').value).toBe('olá');
    expect(maybeEl(fixture, 'run-view')).toBeNull();
  });

  it('vista do run usa o grafo do run', async () => {
    const existing = workflow({ nodes: [node('a'), node('b', 300), node('c', 600)], edges: [] });
    const run = runOf();
    serve({ existing, runs: [run] });
    const fixture = await renderEditor({ existing, runs: [run] });
    expect(maybeEl(fixture, 'node-c')).not.toBeNull();

    await openRun(fixture);

    expect(maybeEl(fixture, 'node-a')).not.toBeNull();
    expect(maybeEl(fixture, 'node-b')).not.toBeNull();
    expect(maybeEl(fixture, 'node-c')).toBeNull();
    expect(maybeEl(fixture, 'edge-a-b')).not.toBeNull();
  });
});
