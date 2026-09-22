import {
  api,
  authenticate,
  click,
  el,
  maybeEl,
  problem,
  provideRouteStub,
  recorder,
  settle,
  stubConfirm,
  text,
  type,
  waitFor,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { AgentForm } from './agent-form';

const AGENTS = '/api/v1/ai/agents';
const AGENT_ID = 'agent-1';
const FILE_ID = 'file-1';

const AGENT = {
  agentId: AGENT_ID,
  name: 'Support',
  instructions: 'help',
  toolNames: ['get_tenant_info'],
  isActive: true,
  isDefault: false,
  createdAt: '2026-01-02T10:00:00Z',
};

const FILE = { fileId: FILE_ID, name: 'notes.txt' };

const MODELS = '/api/v1/ai/models';
const CATALOG = [
  { id: 'a/model', name: 'A', contextLength: 8192, inputPricePerToken: 0.000001, outputPricePerToken: 0.000002 },
  { id: 'b/model', name: 'B', contextLength: 8192, inputPricePerToken: 0.000001, outputPricePerToken: 0.000002 },
];

async function selectModel(fixture: Parameters<typeof el>[0], value: string): Promise<void> {
  const select = el<HTMLSelectElement>(fixture, 'model');
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
  await settle(fixture);
}

function stubEdit(files: typeof FILE[] = []): void {
  server.use(
    api.get(`${AGENTS}/${AGENT_ID}`, () => HttpResponse.json(AGENT)),
    api.get(`${AGENTS}/${AGENT_ID}/files`, () => HttpResponse.json(files)),
  );
}

describe('AgentForm', () => {
  beforeEach(() => {
    server.use(api.get(MODELS, () => HttpResponse.json(CATALOG)));
  });

  it('envia o modelo escolhido', async () => {
    let created: unknown = null;
    let updated: unknown = null;
    stubEdit();
    server.use(
      api.post(AGENTS, async ({ request }) => {
        created = await request.json();
        return HttpResponse.json(AGENT, { status: 201 });
      }),
      api.put(`${AGENTS}/${AGENT_ID}`, async ({ request }) => {
        updated = await request.json();
        return HttpResponse.json(AGENT);
      }),
    );

    provideRouteStub();
    authenticate();
    const createFixture = TestBed.createComponent(AgentForm);
    await settle(createFixture, 2);
    await type(createFixture, 'name', 'Support');
    await type(createFixture, 'instructions', 'help');
    await selectModel(createFixture, 'b/model');
    await click(createFixture, 'submit');
    await waitFor(createFixture, () => expect(created).not.toBeNull());
    expect((created as { model: string }).model).toBe('b/model');

    TestBed.resetTestingModule();
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();
    const editFixture = TestBed.createComponent(AgentForm);
    await settle(editFixture);
    await selectModel(editFixture, 'a/model');
    await click(editFixture, 'submit');
    await waitFor(editFixture, () => expect(updated).not.toBeNull());
    expect((updated as { model: string }).model).toBe('a/model');
  });

  it('envia model null com Padrão do sistema', async () => {
    let updated: Record<string, unknown> | null = null;
    server.use(
      api.get(`${AGENTS}/${AGENT_ID}`, () => HttpResponse.json({ ...AGENT, model: 'a/model' })),
      api.get(`${AGENTS}/${AGENT_ID}/files`, () => HttpResponse.json([])),
      api.put(`${AGENTS}/${AGENT_ID}`, async ({ request }) => {
        updated = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(AGENT);
      }),
    );
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);
    await selectModel(fixture, '');
    await click(fixture, 'submit');

    await waitFor(fixture, () => expect(updated).not.toBeNull());
    expect(updated).toHaveProperty('model', null);
  });

  it('mostra o modelo do agente', async () => {
    server.use(
      api.get(`${AGENTS}/${AGENT_ID}`, () => HttpResponse.json({ ...AGENT, model: 'b/model' })),
      api.get(`${AGENTS}/${AGENT_ID}/files`, () => HttpResponse.json([])),
    );
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();
    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);

    const select = el<HTMLSelectElement>(fixture, 'model');
    expect(select.value).toBe('b/model');
    expect(select.selectedOptions[0].textContent?.trim()).toBe('b/model');

    TestBed.resetTestingModule();
    stubEdit();
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();
    const defaultFixture = TestBed.createComponent(AgentForm);
    await settle(defaultFixture);

    const defaultSelect = el<HTMLSelectElement>(defaultFixture, 'model');
    expect(defaultSelect.value).toBe('');
    expect(defaultSelect.selectedOptions[0].textContent?.trim()).toBe('Padrão do sistema');
  });

  it('catálogo indisponível mantém o modelo', async () => {
    let updated: Record<string, unknown> | null = null;
    server.use(
      api.get(MODELS, () => problem(503, { title: 'Service unavailable', status: 503 })),
      api.get(`${AGENTS}/${AGENT_ID}`, () => HttpResponse.json({ ...AGENT, model: 'a/model' })),
      api.get(`${AGENTS}/${AGENT_ID}/files`, () => HttpResponse.json([])),
      api.put(`${AGENTS}/${AGENT_ID}`, async ({ request }) => {
        updated = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(AGENT);
      }),
    );
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);

    expect(text(fixture, 'model-catalog-error')).toBe('Catálogo de modelos indisponível');
    await click(fixture, 'submit');
    await waitFor(fixture, () => expect(updated).not.toBeNull());
    expect(updated).toHaveProperty('model', 'a/model');
  });

  it('201 navega para agents', async () => {
    server.use(
      api.post(AGENTS, () => HttpResponse.json(AGENT, { status: 201 })),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture, 2);
    await type(fixture, 'name', 'Support');
    await type(fixture, 'instructions', 'help');
    await click(fixture, 'submit');

    await waitFor(fixture, () => expect(TestBed.inject(Router).url).toBe('/ai/agents'));
  });

  it('400 mostra errors fora de mat-error', async () => {
    server.use(
      api.post(AGENTS, () =>
        problem(400, {
          title: 'Validation failed',
          status: 400,
          errors: { name: ['Nome é obrigatório.'] },
        }),
      ),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture, 2);
    await type(fixture, 'name', 'x');
    await type(fixture, 'instructions', 'y');
    await click(fixture, 'submit');

    expect(text(fixture, 'name-error')).toBe('Nome é obrigatório.');
    expect(el(fixture, 'name-error').closest('mat-error')).toBeNull();
  });

  it('409 marca o campo nome', async () => {
    stubEdit();
    server.use(
      api.put(`${AGENTS}/${AGENT_ID}`, () =>
        problem(409, {
          title: 'Business rule violation',
          detail: "Agent with name 'Default' already exists.",
          status: 409,
        }),
      ),
    );
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);
    await click(fixture, 'submit');

    expect(text(fixture, 'name-error')).toBe("Agent with name 'Default' already exists.");
    expect(el(fixture, 'name-error').closest('mat-error')).toBeNull();
  });

  it('lista ficheiros e vazio', async () => {
    stubEdit();
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);

    const section = el(fixture, 'agent-files');
    const form = fixture.nativeElement.querySelector('form') as HTMLElement;
    expect(form.compareDocumentPosition(section) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(section.querySelector('h2')?.textContent).toBe('Ficheiros');
    expect(text(fixture, 'file-empty')).toBe('Nenhum ficheiro');
    expect(section.compareDocumentPosition(el(fixture, 'file-name')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('adiciona ficheiro', async () => {
    let received: unknown = null;
    stubEdit();
    server.use(
      api.post(`${AGENTS}/${AGENT_ID}/files`, async ({ request }) => {
        received = await request.json();
        return HttpResponse.json(FILE, { status: 201 });
      }),
    );
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);
    await type(fixture, 'file-name', 'notes.txt');
    await type(fixture, 'file-content', 'hello file');
    await click(fixture, 'file-add');

    expect(received).toEqual({ name: 'notes.txt', content: 'hello file' });
    expect(text(fixture, `file-${FILE_ID}`)).toContain('notes.txt');
    expect(maybeEl(fixture, 'file-empty')).toBeNull();
  });

  it('apagar cancelado', async () => {
    const requests = recorder();
    stubEdit([FILE]);
    stubConfirm(false);
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);
    el(fixture, `file-delete-${FILE_ID}`).click();
    await settle(fixture);

    expect(requests.filter((request) => request.method === 'DELETE')).toHaveLength(0);
    expect(text(fixture, `file-${FILE_ID}`)).toContain('notes.txt');
  });

  it('apagar com confirm', async () => {
    stubEdit([FILE]);
    stubConfirm(true);
    server.use(
      api.delete(`${AGENTS}/${AGENT_ID}/files/${FILE_ID}`, () => new HttpResponse(null, { status: 204 })),
    );
    provideRouteStub({ agentId: AGENT_ID });
    authenticate();

    const fixture = TestBed.createComponent(AgentForm);
    await settle(fixture);
    el(fixture, `file-delete-${FILE_ID}`).click();
    await settle(fixture);

    expect(maybeEl(fixture, `file-${FILE_ID}`)).toBeNull();
    expect(text(fixture, 'file-empty')).toBe('Nenhum ficheiro');
  });
});
