import { ChangeDetectionStrategy, Component, Injectable, computed, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { API_BASE, ListQuery, PaginatedList, listParams } from '../../core/api';
import { Permissions } from '../../core/permissions';
import { SessionStore } from '../../core/session/session.store';
import { ConfirmService } from '../../shared/confirm';
import { ListState } from '../../shared/list-state';
import { ListStore } from '../../shared/list-store';
import { handleMutationError } from '../../shared/problem-details';
import { AiAvailability } from './ai-availability';
import {
  WorkflowOutput,
  WorkflowRequest,
  WorkflowRunOutput,
  WorkflowRunSummaryOutput,
  WorkflowSummaryOutput,
} from './ai.contracts';

/** HTTP client for workflows and their runs; the editor in `workflow-editor.ts` uses it too. */
@Injectable({ providedIn: 'root' })
export class WorkflowsClient {
  private readonly http = inject(HttpClient);

  list(query: ListQuery): Observable<PaginatedList<WorkflowSummaryOutput>> {
    return this.http.get<PaginatedList<WorkflowSummaryOutput>>(`${API_BASE}/ai/workflows`, {
      params: listParams(query),
    });
  }

  get(workflowId: string): Observable<WorkflowOutput> {
    return this.http.get<WorkflowOutput>(`${API_BASE}/ai/workflows/${workflowId}`);
  }

  create(request: WorkflowRequest): Observable<WorkflowOutput> {
    return this.http.post<WorkflowOutput>(`${API_BASE}/ai/workflows`, request);
  }

  update(workflowId: string, request: WorkflowRequest): Observable<WorkflowOutput> {
    return this.http.put<WorkflowOutput>(`${API_BASE}/ai/workflows/${workflowId}`, request);
  }

  deactivate(workflowId: string): Observable<void> {
    return this.http.delete<void>(`${API_BASE}/ai/workflows/${workflowId}`);
  }

  run(workflowId: string, input: string): Observable<WorkflowRunOutput> {
    return this.http.post<WorkflowRunOutput>(`${API_BASE}/ai/workflows/${workflowId}/runs`, {
      input,
    });
  }

  listRuns(
    workflowId: string,
    pageNumber = 1,
    pageSize = 20,
  ): Observable<PaginatedList<WorkflowRunSummaryOutput>> {
    const params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
    return this.http.get<PaginatedList<WorkflowRunSummaryOutput>>(
      `${API_BASE}/ai/workflows/${workflowId}/runs`,
      {
        params,
      },
    );
  }

  getRun(workflowId: string, runId: string): Observable<WorkflowRunOutput> {
    return this.http.get<WorkflowRunOutput>(`${API_BASE}/ai/workflows/${workflowId}/runs/${runId}`);
  }
}

@Injectable()
export class WorkflowsStore extends ListStore<WorkflowSummaryOutput> {
  private readonly client = inject(WorkflowsClient);
  private readonly ai = inject(AiAvailability);

  protected fetch(query: ListQuery): Observable<PaginatedList<WorkflowSummaryOutput>> {
    return this.client.list(query);
  }

  override async load(patch: Partial<ListQuery> = {}): Promise<void> {
    await super.load(patch);
    this.ai.learnFrom(this.problem());
  }

  /** The list shows active workflows only, so a deactivated one leaves it. */
  async deactivate(workflowId: string): Promise<void> {
    await firstValueFrom(this.client.deactivate(workflowId));
    this.removeWhere((workflow) => workflow.workflowId === workflowId);
  }
}

@Component({
  selector: 'app-workflows-list',
  providers: [WorkflowsStore],
  imports: [DatePipe, ListState, MatButtonModule, MatPaginatorModule, MatTableModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <h1>Workflows</h1>
      @if (canManage) {
        <a mat-flat-button routerLink="/ai/workflows/new" data-testid="create-workflow"
          >Novo workflow</a
        >
      }
    </header>

    <app-list-state
      [status]="store.status()"
      [problem]="store.problem()"
      emptyMessage="Nenhum workflow ainda"
      (retry)="store.retry()"
    >
      @if (canManage) {
        <a
          mat-stroked-button
          emptyAction
          routerLink="/ai/workflows/new"
          data-testid="empty-create-workflow"
          >Novo workflow</a
        >
      }

      <table mat-table [dataSource]="rows()" data-testid="workflows-table">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Nome</th>
          <td mat-cell *matCellDef="let workflow" [attr.data-testid]="'row-' + workflow.workflowId">
            <a [routerLink]="['/ai/workflows', workflow.workflowId]">{{ workflow.name }}</a>
          </td>
        </ng-container>

        <ng-container matColumnDef="nodeCount">
          <th mat-header-cell *matHeaderCellDef>Nós</th>
          <td mat-cell *matCellDef="let workflow">{{ workflow.nodeCount }}</td>
        </ng-container>

        <ng-container matColumnDef="updatedAt">
          <th mat-header-cell *matHeaderCellDef>Atualizado</th>
          <td mat-cell *matCellDef="let workflow">{{ workflow.updatedAt | date: 'short' }}</td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef>Ações</th>
          <td mat-cell *matCellDef="let workflow">
            <a mat-button [routerLink]="['/ai/workflows', workflow.workflowId]">Abrir</a>
            @if (canManage) {
              <button
                mat-button
                type="button"
                [attr.data-testid]="'deactivate-' + workflow.workflowId"
                (click)="confirmDeactivate(workflow)"
              >
                Desativar
              </button>
            }
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    </app-list-state>

    <mat-paginator
      data-testid="paginator"
      [disabled]="store.status() === 'loading'"
      [length]="store.total()"
      [pageIndex]="store.query().pageNumber - 1"
      [pageSize]="store.query().pageSize"
      (page)="changePage($event)"
    />
  `,
  styles: `
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    table {
      width: 100%;
    }
  `,
})
export class WorkflowsList {
  readonly store = inject(WorkflowsStore);
  readonly columns = ['name', 'nodeCount', 'updatedAt', 'actions'];
  readonly rows = computed(() => [...this.store.items()]);
  readonly canManage = inject(SessionStore).hasPermission(Permissions.agentManage);

  private readonly confirm = inject(ConfirmService);
  private readonly snackBar = inject(MatSnackBar);

  constructor() {
    void this.store.load();
  }

  changePage(event: PageEvent): Promise<void> {
    return this.store.load({ pageNumber: event.pageIndex + 1, pageSize: event.pageSize });
  }

  async confirmDeactivate(workflow: WorkflowSummaryOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Desativar workflow',
      message: `Desativar o workflow "${workflow.name}"?`,
      confirmLabel: 'Desativar',
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.deactivate(workflow.workflowId);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }
}
