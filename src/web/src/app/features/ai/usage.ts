import { ChangeDetectionStrategy, Component, Injectable, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { Observable } from 'rxjs';
import { API_BASE, ListQuery, PaginatedList } from '../../core/api';
import { ListState } from '../../shared/list-state';
import { ListStore } from '../../shared/list-store';
import { AiAvailability } from './ai-availability';
import { AiUsageRow } from './ai.contracts';

@Injectable()
export class UsageStore extends ListStore<AiUsageRow> {
  private readonly http = inject(HttpClient);
  private readonly ai = inject(AiAvailability);

  protected fetch(query: ListQuery): Observable<PaginatedList<AiUsageRow>> {
    return this.http.get<PaginatedList<AiUsageRow>>(`${API_BASE}/ai/usage`, {
      params: { pageNumber: query.pageNumber, pageSize: query.pageSize },
    });
  }

  override async load(patch: Partial<ListQuery> = {}): Promise<void> {
    await super.load(patch);
    this.ai.learnFrom(this.problem());
  }
}

@Component({
  selector: 'app-usage',
  providers: [UsageStore],
  imports: [DatePipe, DecimalPipe, ListState, MatPaginatorModule, MatTableModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <h1>Uso do AI</h1>
    </header>

    <app-list-state
      [status]="store.status()"
      [problem]="store.problem()"
      emptyMessage="Sem utilização registada"
      (retry)="store.retry()"
    >
      <table mat-table [dataSource]="rows()" data-testid="usage-table">
        <ng-container matColumnDef="agentName">
          <th mat-header-cell *matHeaderCellDef>Agente</th>
          <td mat-cell *matCellDef="let row" [attr.data-testid]="'row-' + row.agentId">{{ row.agentName }}</td>
        </ng-container>
        <ng-container matColumnDef="calls">
          <th mat-header-cell *matHeaderCellDef>Chamadas</th>
          <td mat-cell *matCellDef="let row">{{ row.calls | number }}</td>
        </ng-container>
        <ng-container matColumnDef="failures">
          <th mat-header-cell *matHeaderCellDef>Falhas</th>
          <td mat-cell *matCellDef="let row">{{ row.failures | number }}</td>
        </ng-container>
        <ng-container matColumnDef="inputTokens">
          <th mat-header-cell *matHeaderCellDef>Tokens entrada</th>
          <td mat-cell *matCellDef="let row">{{ row.inputTokens | number }}</td>
        </ng-container>
        <ng-container matColumnDef="outputTokens">
          <th mat-header-cell *matHeaderCellDef>Tokens saída</th>
          <td mat-cell *matCellDef="let row">{{ row.outputTokens | number }}</td>
        </ng-container>
        <ng-container matColumnDef="totalTokens">
          <th mat-header-cell *matHeaderCellDef>Total</th>
          <td mat-cell *matCellDef="let row">{{ row.totalTokens | number }}</td>
        </ng-container>
        <ng-container matColumnDef="lastUsedAt">
          <th mat-header-cell *matHeaderCellDef>Último uso</th>
          <td mat-cell *matCellDef="let row">{{ row.lastUsedAt | date: 'short' }}</td>
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
    table {
      width: 100%;
    }
  `,
})
export class Usage {
  readonly store = inject(UsageStore);
  readonly columns = [
    'agentName',
    'calls',
    'failures',
    'inputTokens',
    'outputTokens',
    'totalTokens',
    'lastUsedAt',
  ];
  readonly rows = computed(() => [...this.store.items()]);

  constructor() {
    void this.store.load();
  }

  changePage(event: PageEvent): Promise<void> {
    return this.store.load({ pageNumber: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
