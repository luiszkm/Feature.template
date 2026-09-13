import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { API_BASE } from '../../core/api';
import { parseProblem } from '../../shared/problem-details';
import { AiAvailability } from './ai-availability';

export interface LlmMessage {
  role: string;
  content: string;
}

export interface ChatAiResponse {
  reply: string;
  iterationsUsed: number;
}

export const AI_DISABLED_TITLE = 'Feature disabled';
export const AI_UNAVAILABLE_MESSAGE = 'O chat AI não está ativo neste ambiente';

@Component({
  selector: 'app-chat',
  imports: [MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card>
      <h1>Chat AI</h1>

      @if (!ai.available()) {
        <p data-testid="chat-unavailable">{{ unavailableMessage }}</p>
      } @else {
        @if (history().length === 0) {
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
          [disabled]="pending()"
          (click)="send()"
        >
          Enviar
        </button>
      }
    </mat-card>
  `,
})
export class Chat {
  readonly ai = inject(AiAvailability);
  readonly unavailableMessage = AI_UNAVAILABLE_MESSAGE;

  readonly history = signal<readonly LlmMessage[]>([]);
  readonly draft = signal('');
  readonly pending = signal(false);
  readonly error = signal<string | null>(null);

  private readonly http = inject(HttpClient);

  async send(): Promise<void> {
    const message = this.draft().trim();
    if (!message) {
      return;
    }

    const history = [...this.history()];
    this.history.update((current) => [...current, { role: 'user', content: message }]);
    this.draft.set('');
    this.pending.set(true);
    this.error.set(null);

    try {
      const response = await firstValueFrom(
        this.http.post<ChatAiResponse>(`${API_BASE}/ai/chat`, { message, history }),
      );
      this.history.update((current) => [
        ...current,
        { role: 'assistant', content: response.reply },
      ]);
    } catch (error: unknown) {
      const problem = parseProblem(error);
      if (problem.status === 404 && problem.title === AI_DISABLED_TITLE) {
        this.ai.disable();
        return;
      }
      this.error.set(problem.detail || problem.title);
    } finally {
      this.pending.set(false);
    }
  }
}
