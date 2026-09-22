import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Location } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE, PaginatedList } from '../../core/api';
import { fieldError, parseProblem } from '../../shared/problem-details';
import { AgentOutput, ConversationOutput } from './ai.contracts';
import { AI_DISABLED_TITLE, AiAvailability } from './ai-availability';

export { AI_DISABLED_TITLE } from './ai-availability';

export interface LlmMessage {
  role: string;
  content: string;
}

export interface ChatAiResponse {
  conversationId: string;
  reply: string;
  iterationsUsed: number;
}

export const AI_UNAVAILABLE_MESSAGE = 'O chat AI não está ativo neste ambiente';
export const CONVERSATION_NOT_FOUND = 'Conversa não encontrada';

@Component({
  selector: 'app-chat',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card>
      <h1>Chat AI</h1>

      @if (!ai.available()) {
        <p data-testid="chat-unavailable">{{ unavailableMessage }}</p>
      } @else {
        <div class="toolbar">
          <mat-form-field class="agent-picker">
            <mat-label>Agente</mat-label>
            <mat-select
              data-testid="agent-picker"
              [value]="agentId()"
              [disabled]="started()"
              (selectionChange)="agentId.set($event.value)"
            >
              @for (agent of agents(); track agent.agentId) {
                <mat-option [value]="agent.agentId">{{ agent.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <button
            mat-stroked-button
            type="button"
            data-testid="chat-new"
            [disabled]="!started()"
            (click)="newConversation()"
          >
            Nova conversa
          </button>
        </div>

        @if (loading()) {
          <mat-progress-bar mode="indeterminate" data-testid="chat-loading" />
        }

        @if (notice(); as text) {
          <p data-testid="chat-notice">{{ text }}</p>
        }

        @if (history().length === 0 && !loading()) {
          <p data-testid="chat-empty">Faça uma pergunta</p>
        }

        <ul data-testid="chat-history">
          @for (message of history(); track $index) {
            <li [attr.data-role]="message.role">{{ message.content }}</li>
          }
        </ul>

        @if (pending()) {
          <p data-testid="chat-typing">A escrever…</p>
        }

        @if (error(); as text) {
          <p data-testid="chat-error">{{ text }}</p>
        }

        <mat-form-field>
          <mat-label>Mensagem</mat-label>
          <input
            matInput
            data-testid="chat-input"
            [value]="draft()"
            (input)="draft.set($any($event.target).value)"
          />
        </mat-form-field>
        <button
          mat-flat-button
          type="button"
          data-testid="chat-send"
          [disabled]="pending() || loading()"
          (click)="send()"
        >
          Enviar
        </button>
      }
    </mat-card>
  `,
  styles: `
    .toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }
    .agent-picker {
      flex: 1;
    }
  `,
})
export class Chat {
  readonly ai = inject(AiAvailability);
  readonly unavailableMessage = AI_UNAVAILABLE_MESSAGE;

  readonly history = signal<readonly LlmMessage[]>([]);
  readonly draft = signal('');
  readonly pending = signal(false);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly agents = signal<AgentOutput[]>([]);
  readonly agentId = signal<string | null>(null);
  readonly conversationId = signal<string | null>(null);
  /** A conversation with items is bound to its agent; the picker only chooses for a new one. */
  readonly started = computed(() => this.history().length > 0);

  private readonly http = inject(HttpClient);
  private readonly location = inject(Location);
  private readonly router = inject(Router);

  constructor() {
    const conversationId = inject(ActivatedRoute).snapshot.paramMap.get('conversationId');
    if (conversationId) {
      this.conversationId.set(conversationId);
      void this.loadConversation(conversationId);
    }
    void this.loadAgents();
  }

  async send(): Promise<void> {
    const message = this.draft().trim();
    if (!message) {
      return;
    }

    const optimistic: LlmMessage = { role: 'user', content: message };
    this.history.update((current) => [...current, optimistic]);
    this.draft.set('');
    this.pending.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const response = await firstValueFrom(
        this.http.post<ChatAiResponse>(`${API_BASE}/ai/chat`, {
          message,
          conversationId: this.conversationId(),
          agentId: this.agentId(),
        }),
      );
      this.history.update((current) => [
        ...current,
        { role: 'assistant', content: response.reply },
      ]);
      if (!this.conversationId()) {
        // The transcript on screen is already the server's; only the address changes.
        this.conversationId.set(response.conversationId);
        this.location.replaceState(`/ai/conversations/${response.conversationId}`);
      }
    } catch (error: unknown) {
      // Nothing was stored server-side: the turn leaves the list and goes back to the input.
      this.history.update((current) => current.filter((item) => item !== optimistic));
      this.draft.set(message);
      const problem = parseProblem(error);
      this.ai.learnFrom(problem);
      if (problem.status === 404 && problem.title === AI_DISABLED_TITLE) {
        return;
      }
      this.error.set(fieldError(problem, 'message') ?? (problem.detail || problem.title));
    } finally {
      this.pending.set(false);
    }
  }

  newConversation(): void {
    this.history.set([]);
    this.conversationId.set(null);
    this.error.set(null);
    this.notice.set(null);
    this.draft.set('');
    this.selectDefaultAgent();
    void this.router.navigateByUrl('/ai');
  }

  private async loadConversation(conversationId: string): Promise<void> {
    this.loading.set(true);
    try {
      const conversation = await firstValueFrom(
        this.http.get<ConversationOutput>(`${API_BASE}/ai/conversations/${conversationId}`),
      );
      this.agentId.set(conversation.agentId);
      this.history.set(
        conversation.items
          .filter((item) => item.role === 'user' || item.role === 'assistant')
          .map((item) => ({ role: item.role, content: item.content })),
      );
    } catch (error: unknown) {
      const problem = parseProblem(error);
      this.ai.learnFrom(problem);
      if (problem.status === 404 && problem.title !== AI_DISABLED_TITLE) {
        this.notice.set(CONVERSATION_NOT_FOUND);
        this.conversationId.set(null);
        this.selectDefaultAgent();
      } else if (problem.title !== AI_DISABLED_TITLE) {
        this.error.set(problem.detail || problem.title);
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async loadAgents(): Promise<void> {
    try {
      const page = await firstValueFrom(
        this.http.get<PaginatedList<AgentOutput>>(`${API_BASE}/ai/agents`, {
          params: { pageNumber: 1, pageSize: 100 },
        }),
      );
      this.agents.set(page.data.filter((agent) => agent.isActive));
      if (!this.conversationId()) {
        this.selectDefaultAgent();
      }
    } catch (error: unknown) {
      this.ai.learnFrom(parseProblem(error));
    }
  }

  private selectDefaultAgent(): void {
    const active = this.agents();
    const seed = active.find((agent) => agent.isDefault) ?? active[0];
    this.agentId.set(seed?.agentId ?? null);
  }
}
