import {
  api,
  authToken,
  authenticate,
  click,
  el,
  maybeEl,
  problem,
  provideRouteStub,
  recorder,
  settle,
  text,
  tokenWith,
  type,
} from '../../../testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { MatSelect } from '@angular/material/select';
import { By } from '@angular/platform-browser';
import { SessionStore } from '../../core/session/session.store';
import { AI_DISABLED_TITLE, AI_UNAVAILABLE_MESSAGE, Chat } from './chat';
import { AiAvailability } from './ai-availability';

const CHAT = '/api/v1/ai/chat';
const AGENTS = '/api/v1/ai/agents';
const SEED_ID = 'seed-agent-1';
const OTHER_ID = 'other-agent-2';
const CONV_ID = 'conv-1';
const CONVERSATION = `/api/v1/ai/conversations/${CONV_ID}`;

const AGENTS_PAGE = {
  pageNumber: 1,
  pageSize: 100,
  totalCount: 3,
  data: [
    {
      agentId: SEED_ID,
      name: 'Default',
      instructions: 'seed',
      toolNames: ['get_users_summary', 'get_tenant_info'],
      isActive: true,
      isDefault: true,
      createdAt: '2026-01-01T10:00:00Z',
    },
    {
      agentId: OTHER_ID,
      name: 'Support',
      instructions: 'help',
      toolNames: ['get_tenant_info'],
      isActive: true,
      isDefault: false,
      createdAt: '2026-01-02T10:00:00Z',
    },
    {
      agentId: 'inactive-3',
      name: 'Parked',
      instructions: 'idle',
      toolNames: ['get_tenant_info'],
      isActive: false,
      isDefault: false,
      createdAt: '2026-01-03T10:00:00Z',
    },
  ],
};

function conversation(items: [string, string][]) {
  return {
    conversationId: CONV_ID,
    title: 'a',
    agentId: OTHER_ID,
    createdAt: '2026-09-22T10:00:00Z',
    lastActivityAt: '2026-09-22T10:00:00Z',
    items: items.map(([role, content], index) => ({
      itemId: `item-${index}`,
      role,
      content,
      sequence: index + 1,
      createdAt: '2026-09-22T10:00:00Z',
    })),
  };
}

function pickerDisabled(fixture: ComponentFixture<Chat>): boolean {
  return fixture.debugElement.query(By.directive(MatSelect)).componentInstance.disabled;
}

function stubAgents(): void {
  server.use(api.get(AGENTS, () => HttpResponse.json(AGENTS_PAGE)));
}

describe('Chat', () => {
  it('envia e renderiza o reply', async () => {
    let received: unknown = null;
    stubAgents();
    server.use(
      api.post(CHAT, async ({ request }) => {
        received = await request.json();
        return HttpResponse.json({ conversationId: CONV_ID, reply: 'Olá!', iterationsUsed: 1 });
      }),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'chat-empty')).not.toBeNull();
    expect(text(fixture, 'chat-empty')).toBe('Faça uma pergunta');

    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(received).toEqual({ message: 'olá', conversationId: null, agentId: SEED_ID });
    expect(text(fixture, 'chat-history')).toContain('Olá!');
  });

  it('envia agentId do picker', async () => {
    let received: unknown = null;
    stubAgents();
    server.use(
      api.post(CHAT, async ({ request }) => {
        received = await request.json();
        return HttpResponse.json({ conversationId: CONV_ID, reply: 'ok', iterationsUsed: 1 });
      }),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    expect(el<HTMLElement>(fixture, 'agent-picker')).not.toBeNull();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('mat-label')?.textContent).toContain('Agente');
    expect(fixture.componentInstance.agentId()).toBe(SEED_ID);

    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(received).toEqual({ message: 'olá', conversationId: null, agentId: SEED_ID });
  });

  it('picker omite agentes inactivos', async () => {
    stubAgents();
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    expect(fixture.componentInstance.agents().map((agent) => agent.agentId)).toEqual([
      SEED_ID,
      OTHER_ID,
    ]);
    expect(fixture.nativeElement.textContent).not.toContain('Parked');
  });

  it('picker acima do historico', async () => {
    stubAgents();
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    const picker = fixture.nativeElement.querySelector('.agent-picker') as HTMLElement | null;
    const history = el(fixture, 'chat-history');
    expect(picker).not.toBeNull();
    expect(picker!.compareDocumentPosition(history) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(getComputedStyle(picker!).width).not.toBe('0px');
  });

  it('pendente desativa o envio', async () => {
    let release!: () => void;
    const pending = new Promise<void>((resolve) => (release = resolve));
    stubAgents();
    server.use(
      api.post(CHAT, async () => {
        await pending;
        return HttpResponse.json({ conversationId: CONV_ID, reply: 'pronto', iterationsUsed: 1 });
      }),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');

    el(fixture, 'chat-send').click();
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(el<HTMLButtonElement>(fixture, 'chat-send').disabled).toBe(true);
    expect(maybeEl(fixture, 'chat-typing')).not.toBeNull();
    expect(text(fixture, 'chat-typing')).toBe('A escrever…');

    release();
    await settle(fixture);
  });

  it('404 esconde o item AI', async () => {
    stubAgents();
    server.use(
      api.post(CHAT, () =>
        problem(404, {
          title: AI_DISABLED_TITLE,
          detail: "The feature 'EnableAI' is not enabled in this environment.",
          status: 404,
        }),
      ),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(TestBed.inject(AiAvailability).available()).toBe(false);
    expect(text(fixture, 'chat-unavailable')).toBe(AI_UNAVAILABLE_MESSAGE);
  });

  it('401 renova antes de falhar', async () => {
    let chatCalls = 0;
    let refreshCalls = 0;
    stubAgents();
    server.use(
      api.post(CHAT, () => {
        chatCalls += 1;
        return chatCalls === 1
          ? problem(401, { title: 'Unauthorized', status: 401 })
          : HttpResponse.json({ conversationId: CONV_ID, reply: 'renovado', iterationsUsed: 1 });
      }),
      api.post('/api/v1/identity/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ ...authToken(), accessToken: tokenWith([]) });
      }),
    );
    TestBed.inject(SessionStore);
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(refreshCalls).toBe(1);
    expect(text(fixture, 'chat-history')).toContain('renovado');
    expect(maybeEl(fixture, 'chat-error')).toBeNull();
  });
  it('429 mostra o detail e repoe a mensagem no campo', async () => {
    stubAgents();
    server.use(
      api.post(CHAT, () =>
        problem(429, {
          title: 'AI rate limit exceeded',
          detail: 'Limite de pedidos de IA do tenant atingido. Tente novamente dentro de instantes.',
          status: 429,
        }),
      ),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(text(fixture, 'chat-error')).toBe(
      'Limite de pedidos de IA do tenant atingido. Tente novamente dentro de instantes.',
    );
    // Not stored server-side: the turn leaves the list and goes back to the input.
    expect(text(fixture, 'chat-history')).not.toContain('olá');
    expect(el<HTMLInputElement>(fixture, 'chat-input').value).toBe('olá');
  });

  it('400 mostra o erro de message', async () => {
    stubAgents();
    server.use(
      api.post(CHAT, () =>
        problem(400, {
          title: 'Validation failed',
          status: 400,
          errors: { Message: ['A mensagem foi bloqueada pela política de conteúdo.'] },
        }),
      ),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(text(fixture, 'chat-error')).toBe('A mensagem foi bloqueada pela política de conteúdo.');
    expect(text(fixture, 'chat-error')).not.toContain('Validation failed');
  });
  it('abre sem conversationId mostra pergunta picker ativo e nova conversa desativada', async () => {
    stubAgents();
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    expect(text(fixture, 'chat-empty')).toBe('Faça uma pergunta');
    expect(pickerDisabled(fixture)).toBe(false);
    expect(el<HTMLButtonElement>(fixture, 'chat-new').disabled).toBe(true);
    expect(text(fixture, 'chat-new')).toBe('Nova conversa');
  });

  it('carrega os itens da conversa e mostra so user e assistant', async () => {
    let release!: () => void;
    const pending = new Promise<void>((resolve) => (release = resolve));
    stubAgents();
    server.use(
      api.get(CONVERSATION, async () => {
        await pending;
        return HttpResponse.json(conversation([
          ['user', 'pergunta'],
          ['tool', '<tool_output>x</tool_output>'],
          ['assistant', 'resposta'],
        ]));
      }),
    );
    provideRouteStub({ conversationId: CONV_ID });
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
    expect(maybeEl(fixture, 'chat-loading')).not.toBeNull();

    release();
    await settle(fixture);

    expect(maybeEl(fixture, 'chat-loading')).toBeNull();
    const roles = [...el(fixture, 'chat-history').querySelectorAll('li')].map((li) => li.dataset['role']);
    expect(roles).toEqual(['user', 'assistant']);
    expect(text(fixture, 'chat-history')).not.toContain('tool_output');
    expect(fixture.componentInstance.agentId()).toBe(OTHER_ID);
  });

  it('404 mostra conversa nao encontrada e continua numa conversa nova', async () => {
    let received: unknown = null;
    stubAgents();
    server.use(
      api.get(CONVERSATION, () => problem(404, { title: 'Not found', status: 404 })),
      api.post(CHAT, async ({ request }) => {
        received = await request.json();
        return HttpResponse.json({ conversationId: 'conv-new', reply: 'ok', iterationsUsed: 1 });
      }),
    );
    provideRouteStub({ conversationId: CONV_ID });
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture);

    expect(text(fixture, 'chat-notice')).toBe('Conversa não encontrada');
    expect(fixture.componentInstance.conversationId()).toBeNull();

    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect((received as { conversationId: unknown }).conversationId).toBeNull();
  });

  it('primeira resposta guarda conversationId e navega sem recarregar', async () => {
    const requests = recorder();
    stubAgents();
    server.use(
      api.post(CHAT, () => HttpResponse.json({ conversationId: CONV_ID, reply: 'Olá!', iterationsUsed: 1 })),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(fixture.componentInstance.conversationId()).toBe(CONV_ID);
    expect(TestBed.inject(Location).path()).toBe(`/ai/conversations/${CONV_ID}`);
    expect(requests.filter((r) => r.url.pathname === CONVERSATION)).toHaveLength(0);
    expect(text(fixture, 'chat-history')).toContain('olá');
    expect(text(fixture, 'chat-history')).toContain('Olá!');
  });

  it('picker desativado com itens e nova conversa reativa o picker', async () => {
    stubAgents();
    server.use(api.get(CONVERSATION, () => HttpResponse.json(conversation([['user', 'a'], ['assistant', 'b']]))));
    provideRouteStub({ conversationId: CONV_ID });
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture);

    expect(pickerDisabled(fixture)).toBe(true);
    expect(el<HTMLButtonElement>(fixture, 'chat-new').disabled).toBe(false);

    await click(fixture, 'chat-new');
    await settle(fixture);

    expect(pickerDisabled(fixture)).toBe(false);
    expect(fixture.componentInstance.conversationId()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/ai');
  });

  it('falha no post remove o turno otimista e repoe o rascunho', async () => {
    stubAgents();
    server.use(
      api.post(CHAT, () => problem(500, { title: 'Unexpected error', detail: 'O modelo falhou.', status: 500 })),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(el(fixture, 'chat-history').querySelectorAll('li')).toHaveLength(0);
    expect(text(fixture, 'chat-error')).toBe('O modelo falhou.');
    expect(el<HTMLInputElement>(fixture, 'chat-input').value).toBe('olá');
  });

  it('deixa de enviar history no pedido', async () => {
    const bodies: Record<string, unknown>[] = [];
    stubAgents();
    server.use(
      api.post(CHAT, async ({ request }) => {
        bodies.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json({ conversationId: CONV_ID, reply: 'ok', iterationsUsed: 1 });
      }),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);
    await type(fixture, 'chat-input', 'um');
    await click(fixture, 'chat-send');
    await type(fixture, 'chat-input', 'dois');
    await click(fixture, 'chat-send');

    expect(bodies).toHaveLength(2);
    expect(bodies.every((body) => !('history' in body))).toBe(true);
    expect(bodies[1]).toEqual({ message: 'dois', conversationId: CONV_ID, agentId: SEED_ID });
  });
  it('arranjo: picker e nova conversa na mesma linha acima do historico', async () => {
    stubAgents();
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    const root = fixture.nativeElement as HTMLElement;
    const toolbar = root.querySelector('.toolbar') as HTMLElement;
    const children = [...toolbar.children];
    expect(children).toHaveLength(2);
    expect(children[0].classList.contains('agent-picker')).toBe(true);
    expect(children[1]).toBe(el(fixture, 'chat-new'));
    expect(getComputedStyle(toolbar).display).toBe('flex');
    expect(getComputedStyle(toolbar).justifyContent).toBe('space-between');
    expect(toolbar.compareDocumentPosition(el(fixture, 'chat-history')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(toolbar.parentElement?.tagName.toLowerCase()).toBe('mat-card');
  });
});
