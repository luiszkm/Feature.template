import { ChangeDetectionStrategy, Component, Injectable, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { API_BASE, ListQuery, PaginatedList, SortDirection, listParams } from '../../core/api';
import { ConfirmService } from '../../shared/confirm';
import { ListState } from '../../shared/list-state';
import { ListStore } from '../../shared/list-store';
import { handleMutationError } from '../../shared/problem-details';
import { AgentOutput } from './ai.contracts';

@Injectable()
export class AgentsStore extends ListStore<AgentOutput> {
  private readonly http = inject(HttpClient);

  protected fetch(query: ListQuery): Observable<PaginatedList<AgentOutput>> {
    return this.http.get<PaginatedList<AgentOutput>>(`${API_BASE}/ai/agents`, {
      params: listParams(query),
    });
  }

  async deactivate(agentId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE}/ai/agents/${agentId}`));
    this.items.update((agents) =>
      agents.map((agent) => (agent.agentId === agentId ? { ...agent, isActive: false } : agent)),
    );
  }
}

@Component({
  selector: 'app-agents-list',
  providers: [AgentsStore],
  imports: [
    ListState,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatSortModule,
    MatTableModule,
    RouterLink,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <h1>Agentes</h1>
      <a mat-flat-button routerLink="/ai/agents/new" data-testid="create-agent">Criar agente</a>
    </header>

    <mat-form-field>
      <mat-label>Pesquisar</mat-label>
      <input
        matInput
        data-testid="search"
        [value]="store.query().searchTerm ?? ''"
        (input)="search($any($event.target).value)"
      />
    </mat-form-field>

    <app-list-state
      [status]="store.status()"
      [problem]="store.problem()"
      emptyMessage="Nenhum agente"
      (retry)="store.retry()"
    >
      <a mat-stroked-button emptyAction routerLink="/ai/agents/new">Criar agente</a>

      <table
        mat-table
        matSort
        [dataSource]="rows()"
        data-testid="agents-table"
        (matSortChange)="changeSort($event)"
      >
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="name">Nome</th>
          <td mat-cell *matCellDef="let agent" [attr.data-testid]="'row-' + agent.agentId">
            <a [routerLink]="['/ai/agents', agent.agentId]">{{ agent.name }}</a>
          </td>
        </ng-container>

        <ng-container matColumnDef="isActive">
          <th mat-header-cell *matHeaderCellDef>Ativo</th>
          <td mat-cell *matCellDef="let agent" [attr.data-testid]="'active-' + agent.agentId">
            {{ agent.isActive ? 'Sim' : 'Não' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let agent">
            <a
              mat-button
              [attr.data-testid]="'edit-' + agent.agentId"
              [routerLink]="['/ai/agents', agent.agentId]"
              >Editar</a
            >
            <button
              mat-button
              type="button"
              [attr.data-testid]="'deactivate-' + agent.agentId"
              (click)="confirmDeactivate(agent)"
            >
              Desativar
            </button>
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
export class AgentsList {
  readonly store = inject(AgentsStore);
  readonly columns = ['name', 'isActive', 'actions'];
  readonly rows = computed(() => [...this.store.items()]);

  private readonly confirm = inject(ConfirmService);
  private readonly snackBar = inject(MatSnackBar);

  constructor() {
    void this.store.load();
  }

  changeSort(sort: Sort): void {
    void this.store.load({
      pageNumber: 1,
      sortBy: sort.direction ? sort.active : undefined,
      sortDirection: sort.direction ? (sort.direction as SortDirection) : undefined,
    });
  }

  search(searchTerm: string): Promise<void> {
    return this.store.load({ pageNumber: 1, searchTerm: searchTerm || undefined });
  }

  changePage(event: PageEvent): Promise<void> {
    return this.store.load({ pageNumber: event.pageIndex + 1, pageSize: event.pageSize });
  }

  async confirmDeactivate(agent: AgentOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Desativar agente',
      message: `Desativar ${agent.name}?`,
      confirmLabel: 'Desativar',
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.deactivate(agent.agentId);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }
}
