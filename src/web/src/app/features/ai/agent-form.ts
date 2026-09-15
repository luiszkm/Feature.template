import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE } from '../../core/api';
import { NotFound } from '../../shared/screens';
import { Problem, fieldError, parseProblem, problemKind } from '../../shared/problem-details';
import {
  AgentFileOutput,
  AgentOutput,
  CreateAgentRequest,
  KNOWN_AGENT_TOOLS,
  UpdateAgentRequest,
} from './ai.contracts';

@Component({
  selector: 'app-agent-form',
  imports: [
    FormField,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    NotFound,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <app-not-found [title]="problem()!.title" />
    } @else {
      <h1>{{ agentId ? 'Editar agente' : 'Criar agente' }}</h1>

      <form (submit)="onSubmit($event)">
        <mat-form-field>
          <mat-label>Nome</mat-label>
          <input matInput data-testid="name" [formField]="agentForm.name" />
        </mat-form-field>
        @if (message('name'); as text) {
          <p class="field-error" data-testid="name-error">{{ text }}</p>
        }

        <mat-form-field>
          <mat-label>Instruções</mat-label>
          <textarea matInput rows="6" data-testid="instructions" [formField]="agentForm.instructions"></textarea>
        </mat-form-field>
        @if (message('instructions'); as text) {
          <p class="field-error" data-testid="instructions-error">{{ text }}</p>
        }

        <fieldset>
          <legend>Ferramentas</legend>
          @for (tool of tools; track tool.name) {
            <mat-checkbox
              [checked]="selectedTools().includes(tool.name)"
              (change)="toggleTool(tool.name, $event.checked)"
              [attr.data-testid]="'tool-' + tool.name"
            >
              {{ tool.label }}
            </mat-checkbox>
          }
        </fieldset>
        @if (message('toolNames'); as text) {
          <p class="field-error" data-testid="toolNames-error">{{ text }}</p>
        }

        <button mat-flat-button type="submit" data-testid="submit" [disabled]="pending()">
          Guardar
        </button>
      </form>

      @if (agentId) {
        <section data-testid="agent-files">
          <h2>Ficheiros</h2>
          <mat-form-field>
            <mat-label>Nome do ficheiro</mat-label>
            <input matInput data-testid="file-name" [value]="fileName()" (input)="fileName.set($any($event.target).value)" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Conteúdo</mat-label>
            <textarea
              matInput
              rows="4"
              data-testid="file-content"
              [value]="fileContent()"
              (input)="fileContent.set($any($event.target).value)"
            ></textarea>
          </mat-form-field>
          <button mat-stroked-button type="button" data-testid="file-add" (click)="addFile()">
            Adicionar ficheiro
          </button>
          <ul>
            @for (file of files(); track file.fileId) {
              <li [attr.data-testid]="'file-' + file.fileId">
                <button type="button" mat-button (click)="viewFile(file.fileId)">{{ file.name }}</button>
                <button
                  type="button"
                  mat-button
                  [attr.data-testid]="'file-delete-' + file.fileId"
                  (click)="removeFile(file.fileId)"
                >
                  Apagar
                </button>
              </li>
            }
          </ul>
          @if (filePreview(); as preview) {
            <pre data-testid="file-preview">{{ preview }}</pre>
          }
        </section>
      }
    }
  `,
  styles: `
    .field-error {
      color: var(--mat-sys-error, #b3261e);
      margin: -0.5rem 0 0.5rem;
      font-size: 0.75rem;
    }
    form,
    section {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: 36rem;
    }
  `,
})
export class AgentForm {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly agentId = this.route.snapshot.paramMap.get('agentId');
  readonly pending = signal(false);
  readonly problem = signal<Problem | null>(null);
  readonly selectedTools = signal<string[]>(['get_users_summary', 'get_tenant_info']);
  readonly files = signal<AgentFileOutput[]>([]);
  readonly fileName = signal('');
  readonly fileContent = signal('');
  readonly filePreview = signal<string | null>(null);
  readonly tools = KNOWN_AGENT_TOOLS;

  readonly model = signal({ name: '', instructions: '' });
  readonly agentForm = form(this.model, (path) => {
    required(path.name);
    required(path.instructions);
  });

  constructor() {
    if (this.agentId) {
      void this.load(this.agentId);
    }
  }

  notFound(): boolean {
    const problem = this.problem();
    return problem !== null && problemKind(problem) === 'not-found';
  }

  message(field: string): string | null {
    const problem = this.problem();
    if (!problem) {
      return null;
    }
    return fieldError(problem, field);
  }

  toggleTool(name: string, checked: boolean): void {
    this.selectedTools.update((current) => {
      if (checked) {
        return current.includes(name) ? current : [...current, name];
      }
      return current.filter((tool) => tool !== name);
    });
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.agentForm, async () => {
      await this.save();
      return undefined;
    });
  }

  private async load(agentId: string): Promise<void> {
    try {
      const agent = await firstValueFrom(
        this.http.get<AgentOutput>(`${API_BASE}/ai/agents/${agentId}`),
      );
      this.model.set({ name: agent.name, instructions: agent.instructions });
      this.selectedTools.set([...agent.toolNames]);
      this.files.set(
        await firstValueFrom(this.http.get<AgentFileOutput[]>(`${API_BASE}/ai/agents/${agentId}/files`)),
      );
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    }
  }

  private async save(): Promise<void> {
    this.pending.set(true);
    this.problem.set(null);
    const value = this.model();
    const body = {
      name: value.name,
      instructions: value.instructions,
      toolNames: this.selectedTools(),
    } satisfies CreateAgentRequest & UpdateAgentRequest;

    try {
      if (this.agentId) {
        await firstValueFrom(
          this.http.put<AgentOutput>(`${API_BASE}/ai/agents/${this.agentId}`, body),
        );
      } else {
        await firstValueFrom(this.http.post<AgentOutput>(`${API_BASE}/ai/agents`, body));
        await this.router.navigateByUrl('/ai/agents');
      }
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    } finally {
      this.pending.set(false);
    }
  }

  async addFile(): Promise<void> {
    if (!this.agentId || !this.fileName().trim()) {
      return;
    }
    const created = await firstValueFrom(
      this.http.post<AgentFileOutput>(`${API_BASE}/ai/agents/${this.agentId}/files`, {
        name: this.fileName().trim(),
        content: this.fileContent(),
      }),
    );
    this.files.update((current) => [...current, created]);
    this.fileName.set('');
    this.fileContent.set('');
  }

  async viewFile(fileId: string): Promise<void> {
    if (!this.agentId) {
      return;
    }
    const file = await firstValueFrom(
      this.http.get<AgentFileOutput>(`${API_BASE}/ai/agents/${this.agentId}/files/${fileId}`),
    );
    this.filePreview.set(file.content ?? '');
  }

  async removeFile(fileId: string): Promise<void> {
    if (!this.agentId) {
      return;
    }
    await firstValueFrom(
      this.http.delete<void>(`${API_BASE}/ai/agents/${this.agentId}/files/${fileId}`),
    );
    this.files.update((current) => current.filter((file) => file.fileId !== fileId));
  }
}
