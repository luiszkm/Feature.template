import {
  api,
  authToken,
  authenticate,
  click,
  el,
  maybeEl,
  problem,
  settle,
  text,
  tokenWith,
  type,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { SessionStore } from '../../core/session/session.store';
import { AI_DISABLED_TITLE, AI_UNAVAILABLE_MESSAGE, Chat } from './chat';
import { AiAvailability } from './ai-availability';

const CHAT = '/api/v1/ai/chat';

describe('Chat', () => {
  it('envia e renderiza o reply', async () => {
    let received: unknown = null;
    server.use(
      api.post(CHAT, async ({ request }) => {
        received = await request.json();
        return HttpResponse.json({ reply: 'Olá!', iterationsUsed: 1 });
      }),
    );
    authenticate();

    const fixture = TestBed.createComponent(Chat);
    await settle(fixture, 2);

    expect(maybeEl(fixture, 'chat-empty')).not.toBeNull();

    await type(fixture, 'chat-input', 'olá');
    await click(fixture, 'chat-send');

    expect(received).toEqual({ message: 'olá', history: [] });
    expect(text(fixture, 'chat-history')).toContain('Olá!');
  });

  it('pendente desativa o envio', async () => {
    let release!: () => void;
    const pending = new Promise<void>((resolve) => (release = resolve));
    server.use(
      api.post(CHAT, async () => {
        await pending;
        return HttpResponse.json({ reply: 'pronto', iterationsUsed: 1 });
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

    release();
    await settle(fixture);
  });

  it('404 esconde o item AI', async () => {
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
    server.use(
      api.post(CHAT, () => {
        chatCalls += 1;
        return chatCalls === 1
          ? problem(401, { title: 'Unauthorized', status: 401 })
          : HttpResponse.json({ reply: 'renovado', iterationsUsed: 1 });
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
});
