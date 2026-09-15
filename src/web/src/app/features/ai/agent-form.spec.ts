import {
  api,
  authenticate,
  click,
  el,
  problem,
  provideRouteStub,
  settle,
  text,
  type,
  waitFor,
} from '../../../testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../../../test-setup';
import { AgentForm } from './agent-form';

const AGENTS = '/api/v1/ai/agents';

describe('AgentForm', () => {
  it('201 navega para agents', async () => {
    server.use(
      api.post(AGENTS, () =>
        HttpResponse.json(
          {
            agentId: 'agent-1',
            name: 'Support',
            instructions: 'help',
            toolNames: [],
            isActive: true,
            isDefault: false,
            createdAt: '2026-01-02T10:00:00Z',
          },
          { status: 201 },
        ),
      ),
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
});
