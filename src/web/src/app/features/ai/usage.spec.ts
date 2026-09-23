import { api, authToken, authenticate, provideRouteStub, settle, text, tokenWith } from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { SessionStore } from '../../core/session/session.store';
import { Forbidden } from '../../shared/screens';
import { Usage } from './usage';

const USAGE = '/api/v1/ai/usage';

describe('Usage', () => {
  it('estado vazio', async () => {
    server.use(api.get(USAGE, () => HttpResponse.json({ pageNumber: 1, pageSize: 20, totalCount: 0, data: [] })));
    provideRouteStub();
    authenticate(['ai.agent.read']);

    const fixture = TestBed.createComponent(Usage);
    await settle(fixture);

    expect(text(fixture, 'list-empty')).toBe('Sem utilização registada');
  });

  it('mostra uma linha por agente com os totais', async () => {
    server.use(
      api.get(USAGE, () =>
        HttpResponse.json({
          pageNumber: 1,
          pageSize: 20,
          totalCount: 1,
          data: [
            {
              agentId: 'a1',
              agentName: 'Default',
              calls: 3,
              failures: 1,
              inputTokens: 1200,
              outputTokens: 300,
              totalTokens: 1500,
              lastUsedAt: '2026-09-22T10:00:00Z',
            },
          ],
        }),
      ),
    );
    provideRouteStub();
    authenticate(['ai.agent.read']);

    const fixture = TestBed.createComponent(Usage);
    await settle(fixture);

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('h1')?.textContent?.trim()).toBe('Uso do AI');
    expect([...root.querySelectorAll('th')].map((th) => th.textContent?.trim())).toEqual([
      'Agente',
      'Chamadas',
      'Falhas',
      'Tokens entrada',
      'Tokens saída',
      'Total',
      'Último uso',
    ]);
    expect(text(fixture, 'row-a1')).toBe('Default');
    expect(root.querySelector('[data-testid="search"]')).toBeNull();
    expect(root.querySelector('[mat-sort-header]')).toBeNull();
  });

  it('sem permissao vai para forbidden', async () => {
    TestBed.inject(SessionStore).apply({
      ...authToken({ roles: [], permissions: [] }),
      accessToken: tokenWith([]),
    });
    const router = TestBed.inject(Router);
    await router.navigateByUrl('/ai/usage');
    expect(router.url).toBe('/forbidden');

    const fixture = TestBed.createComponent(Forbidden);
    await settle(fixture, 2);
    expect(text(fixture, 'forbidden')).toContain('Sem permissão para esta operação');
  });
});
