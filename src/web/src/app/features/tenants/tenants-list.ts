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
import {
  TENANT_ISOLATION_MODE_LABELS,
  TenantIsolationMode,
  TenantOutput,
} from './tenants.contracts';

@Injectable()
export class TenantsStore extends ListStore<TenantOutput> {
  private readonly http = inject(HttpClient);

  protected fetch(query: ListQuery): Observable<PaginatedList<TenantOutput>> {
    return this.http.get<PaginatedList<TenantOutput>>(`${API_BASE}/tenants`, {
      params: listParams(query),
    });
  }

  async deactivate(tenantId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE}/tenants/${tenantId}`));
    this.items.update((tenants) =>
      tenants.map((tenant) =>
        tenant.tenantId === tenantId ? { ...tenant, isActive: false } : tenant,
      ),
    );
  }
}

@Component({
  selector: 'app-tenants-list',
  providers: [TenantsStore],
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
      <h1>Tenants</h1>
      <a mat-flat-button routerLink="/tenants/new" data-testid="create-tenant">Criar tenant</a>
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
      emptyMessage="Nenhum tenant"
      (retry)="store.retry()"
    >
      <a mat-stroked-button emptyAction routerLink="/tenants/new">Criar tenant</a>

      <table
        mat-table
        matSort
        [dataSource]="rows()"
        data-testid="tenants-table"
        (matSortChange)="changeSort($event)"
      >
        <ng-container matColumnDef="tenantKey">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="tenantKey">Chave</th>
          <td mat-cell *matCellDef="let tenant" [attr.data-testid]="'row-' + tenant.tenantId">
            <a [routerLink]="['/tenants', tenant.tenantId]">{{ tenant.tenantKey }}</a>
          </td>
        </ng-container>

        <ng-container matColumnDef="displayName">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="displayName">Nome</th>
          <td mat-cell *matCellDef="let tenant" [attr.data-testid]="'name-' + tenant.tenantId">
            {{ tenant.displayName }}
          </td>
        </ng-container>

        <ng-container matColumnDef="isActive">
          <th mat-header-cell *matHeaderCellDef>Ativo</th>
          <td mat-cell *matCellDef="let tenant" [attr.data-testid]="'active-' + tenant.tenantId">
            {{ tenant.isActive ? 'Sim' : 'Não' }}
          </td>
        </ng-container>

        <ng-container matColumnDef="isolationMode">
          <th mat-header-cell *matHeaderCellDef>Isolamento</th>
          <td mat-cell *matCellDef="let tenant" [attr.data-testid]="'isolation-' + tenant.tenantId">
            {{ isolationModeLabel(tenant.isolationMode) }}
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let tenant">
            <a
              mat-button
              [attr.data-testid]="'edit-' + tenant.tenantId"
              [routerLink]="['/tenants', tenant.tenantId]"
              >Editar</a
            >
            <button
              mat-button
              type="button"
              [attr.data-testid]="'deactivate-' + tenant.tenantId"
              (click)="confirmDeactivate(tenant)"
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
export class TenantsList {
  readonly store = inject(TenantsStore);
  readonly columns = ['tenantKey', 'displayName', 'isActive', 'isolationMode', 'actions'];
  readonly rows = computed(() => [...this.store.items()]);

  isolationModeLabel(mode: TenantIsolationMode): string {
    return TENANT_ISOLATION_MODE_LABELS[mode];
  }

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

  async confirmDeactivate(tenant: TenantOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Desativar tenant',
      message: `Desativar ${tenant.tenantKey}? Pode reativar pela edição.`,
      confirmLabel: 'Desativar',
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.deactivate(tenant.tenantId);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }
}
