import { ChangeDetectionStrategy, Component, Injectable, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Observable, firstValueFrom } from 'rxjs';
import { API_BASE, ListQuery, PaginatedList, SortDirection, listParams } from '../../core/api';
import { ConfirmService } from '../../shared/confirm';
import { ListState } from '../../shared/list-state';
import { ListStore } from '../../shared/list-store';
import { handleMutationError } from '../../shared/problem-details';
import { NamedRequest, PermissionOutput } from './authorization.contracts';
import { openNamedDialog } from './roles-list';

@Injectable()
export class PermissionsStore extends ListStore<PermissionOutput> {
  private readonly http = inject(HttpClient);

  protected fetch(query: ListQuery): Observable<PaginatedList<PermissionOutput>> {
    return this.http.get<PaginatedList<PermissionOutput>>(`${API_BASE}/authorization/permissions`, {
      params: listParams(query),
    });
  }

  async create(body: NamedRequest): Promise<PermissionOutput> {
    const permission = await firstValueFrom(
      this.http.post<PermissionOutput>(`${API_BASE}/authorization/permissions`, body),
    );
    this.append(permission);
    return permission;
  }

  async update(permissionId: string, body: NamedRequest): Promise<PermissionOutput> {
    const permission = await firstValueFrom(
      this.http.put<PermissionOutput>(
        `${API_BASE}/authorization/permissions/${permissionId}`,
        body,
      ),
    );
    this.replace(permission, (candidate) => candidate.id === permissionId);
    return permission;
  }

  async remove(permissionId: string): Promise<void> {
    await firstValueFrom(
      this.http.delete<void>(`${API_BASE}/authorization/permissions/${permissionId}`),
    );
    this.removeWhere((candidate) => candidate.id === permissionId);
  }
}

@Component({
  selector: 'app-permissions-list',
  providers: [PermissionsStore],
  imports: [
    ListState,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatSortModule,
    MatTableModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header>
      <h1>Permissões</h1>
      <button mat-flat-button type="button" data-testid="create-permission" (click)="openCreate()">
        Criar permissão
      </button>
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
      emptyMessage="Nenhuma permissão"
      (retry)="store.retry()"
    >
      <button mat-stroked-button emptyAction type="button" (click)="openCreate()">
        Criar permissão
      </button>

      <table
        mat-table
        matSort
        [dataSource]="rows()"
        data-testid="permissions-table"
        (matSortChange)="changeSort($event)"
      >
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="name">Nome</th>
          <td mat-cell *matCellDef="let permission" [attr.data-testid]="'row-' + permission.id">
            {{ permission.name }}
          </td>
        </ng-container>

        <ng-container matColumnDef="description">
          <th mat-header-cell *matHeaderCellDef>Descrição</th>
          <td
            mat-cell
            *matCellDef="let permission"
            [attr.data-testid]="'description-' + permission.id"
          >
            {{ permission.description }}
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let permission">
            <button
              mat-button
              type="button"
              [attr.data-testid]="'edit-' + permission.id"
              (click)="openEdit(permission)"
            >
              Editar
            </button>
            <button
              mat-button
              type="button"
              [attr.data-testid]="'delete-' + permission.id"
              (click)="confirmDelete(permission)"
            >
              Eliminar
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
export class PermissionsList {
  readonly store = inject(PermissionsStore);
  readonly columns = ['name', 'description', 'actions'];
  readonly rows = computed(() => [...this.store.items()]);

  private readonly dialog = inject(MatDialog);
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

  openCreate(): void {
    openNamedDialog(this.dialog, {
      title: 'Criar permissão',
      value: { name: '', description: '' },
      save: (body) => this.store.create(body),
    });
  }

  openEdit(permission: PermissionOutput): void {
    openNamedDialog(this.dialog, {
      title: 'Editar permissão',
      value: { name: permission.name, description: permission.description },
      save: (body) => this.store.update(permission.id, body),
    });
  }

  async confirmDelete(permission: PermissionOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Eliminar permissão',
      message: `Eliminar ${permission.name}?`,
      confirmLabel: 'Eliminar',
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.remove(permission.id);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }
}
