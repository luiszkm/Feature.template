import { api, authenticate, el, maybeEl, provideRouteStub, recorder, settle, text } from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { ConfirmRequest, ConfirmService } from '../../shared/confirm';
import { ConversationsList } from './conversations-list';

const CONVERSATIONS = '/api/v1/ai/conversations';

const PAGE = {
  pageNumber: 1,
  pageSize: 20,
  totalCount: 2,
  data: [
    { conversationId: 'conv-1', title: 'Quantos utilizadores?', agentId: 'a', lastActivityAt: '2026-09-22T10:00:00Z', itemCount: 2 },
    { conversationId: 'conv-2', title: 'Outra', agentId: 'a', lastActivityAt: '2026-09-21T10:00:00Z', itemCount: 4 },
  ],
};

/** Records what the screen asks the user before answering, so the copy is part of the proof. */
function answerConfirm(answer: boolean): ConfirmRequest[] {
  const asked: ConfirmRequest[] = [];
  TestBed.overrideProvider(ConfirmService, {
    useValue: {
      ask: async (request: ConfirmRequest) => {
        asked.push(request);
        return answer;
      },
    },
  });
  return asked;
}

describe('ConversationsList', () => {
  it('estado vazio: nenhuma conversa', async () => {
    server.use(
      api.get(CONVERSATIONS, () => HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 0, data: [] })),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(ConversationsList);
    await settle(fixture);

    expect(text(fixture, 'list-empty')).toContain('Nenhuma conversa');
    const action = el<HTMLAnchorElement>(fixture, 'empty-new-conversation');
    expect(action.textContent?.trim()).toBe('Nova conversa');
    expect(action.getAttribute('href')).toBe('/ai');
  });

  it('apagar com confirm remove a linha', async () => {
    const asked = answerConfirm(true);
    const deleted: string[] = [];
    server.use(
      api.get(CONVERSATIONS, () => HttpResponse.json(PAGE)),
      api.delete(`${CONVERSATIONS}/:conversationId`, ({ params }) => {
        deleted.push(params['conversationId'] as string);
        return new HttpResponse(null, { status: 204 });
      }),
    );
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(ConversationsList);
    await settle(fixture);
    el(fixture, 'delete-conv-1').click();
    await settle(fixture);

    expect(asked).toEqual([
      {
        title: 'Apagar conversa',
        message: 'Apagar Quantos utilizadores?? Esta ação não pode ser anulada.',
        confirmLabel: 'Apagar',
      },
    ]);
    expect(deleted).toEqual(['conv-1']);
    expect(maybeEl(fixture, 'row-conv-1')).toBeNull();
    expect(maybeEl(fixture, 'row-conv-2')).not.toBeNull();
  });

  it('apagar cancelado nao emite pedido', async () => {
    answerConfirm(false);
    server.use(api.get(CONVERSATIONS, () => HttpResponse.json(PAGE)));
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(ConversationsList);
    await settle(fixture);
    const requests = recorder();
    el(fixture, 'delete-conv-1').click();
    await settle(fixture);

    expect(requests).toHaveLength(0);
    expect(maybeEl(fixture, 'row-conv-1')).not.toBeNull();
  });
  it('arranjo e copy: cabecalho, pesquisa, tabela e paginador', async () => {
    server.use(api.get(CONVERSATIONS, () => HttpResponse.json(PAGE)));
    provideRouteStub();
    authenticate();

    const fixture = TestBed.createComponent(ConversationsList);
    await settle(fixture);

    const root = fixture.nativeElement as HTMLElement;
    const regions = [...root.children].map((child) => child.tagName.toLowerCase());
    expect(regions).toEqual(['header', 'mat-form-field', 'app-list-state', 'mat-paginator']);

    const header = root.querySelector('header') as HTMLElement;
    expect([...header.children].map((child) => child.tagName.toLowerCase())).toEqual(['h1', 'a']);
    expect(header.querySelector('h1')?.textContent?.trim()).toBe('Conversas');
    expect(el(fixture, 'new-conversation').textContent?.trim()).toBe('Nova conversa');
    expect(el(fixture, 'new-conversation').getAttribute('href')).toBe('/ai');
    expect(getComputedStyle(header).justifyContent).toBe('space-between');

    expect(root.querySelector('mat-form-field mat-label')?.textContent?.trim()).toBe('Pesquisar');
    expect(root.querySelector('app-list-state table')).not.toBeNull();

    const headers = [...root.querySelectorAll('th')].map((th) => th.textContent?.trim());
    expect(headers).toEqual(['Título', 'Atualizada', '']);
    const sortable = [...root.querySelectorAll('th[mat-sort-header]')].map((th) => th.getAttribute('mat-sort-header'));
    expect(sortable).toEqual(['title', 'lastActivityAt']);

    expect(el(fixture, 'delete-conv-1').textContent?.trim()).toBe('Apagar');
    expect(el<HTMLAnchorElement>(fixture, 'row-conv-1').querySelector('a')?.getAttribute('href')).toBe(
      '/ai/conversations/conv-1',
    );
  });
});
