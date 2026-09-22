import { ChangeDetectionStrategy, Component, Injectable, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
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
import { AiAvailability } from './ai-availability';
import { ConversationSummary } from './ai.contracts';

@Injectable()
export class ConversationsStore extends ListStore<ConversationSummary> {
  private readonly http = inject(HttpClient);
  private readonly ai = inject(AiAvailability);

  protected fetch(query: ListQuery): Observable<PaginatedList<ConversationSummary>> {
    return this.http.get<PaginatedList<ConversationSummary>>(`${API_BASE}/ai/conversations`, {
      params: listParams(query),
    });
  }

  override async load(patch: Partial<ListQuery> = {}): Promise<void> {
    await super.load(patch);
    this.ai.learnFrom(this.problem());
  }

  async delete(conversationId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE}/ai/conversations/${conversationId}`));
    this.removeWhere((conversation) => conversation.conversationId === conversationId);
  }
}

@Component({
  selector: 'app-conversations-list',
  providers: [ConversationsStore],
  imports: [
    DatePipe,
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
      <h1>Conversas</h1>
      <a mat-flat-button routerLink="/ai" data-testid="new-conversation">Nova conversa</a>
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
      emptyMessage="Nenhuma conversa"
      (retry)="store.retry()"
    >
      <a mat-stroked-button emptyAction routerLink="/ai" data-testid="empty-new-conversation"
        >Nova conversa</a
      >

      <table
        mat-table
        matSort
        [dataSource]="rows()"
        data-testid="conversations-table"
        (matSortChange)="changeSort($event)"
      >
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="title">Título</th>
          <td mat-cell *matCellDef="let conversation" [attr.data-testid]="'row-' + conversation.conversationId">
            <a [routerLink]="['/ai/conversations', conversation.conversationId]">{{ conversation.title }}</a>
          </td>
        </ng-container>

        <ng-container matColumnDef="lastActivityAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="lastActivityAt">Atualizada</th>
          <td mat-cell *matCellDef="let conversation">
            {{ conversation.lastActivityAt | date: 'short' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let conversation">
            <button
              mat-button
              type="button"
              [attr.data-testid]="'delete-' + conversation.conversationId"
              (click)="confirmDelete(conversation)"
            >
              Apagar
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
export class ConversationsList {
  readonly store = inject(ConversationsStore);
  readonly columns = ['title', 'lastActivityAt', 'actions'];
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

  async confirmDelete(conversation: ConversationSummary): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Apagar conversa',
      message: `Apagar ${conversation.title}? Esta ação não pode ser anulada.`,
      confirmLabel: 'Apagar',
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.delete(conversation.conversationId);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }
}
