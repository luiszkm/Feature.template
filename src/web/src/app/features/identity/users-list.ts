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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import {
  API_BASE,
  DEFAULT_PAGE_SIZE,
  ListQuery,
  PaginatedList,
  SortDirection,
  listParams,
} from '../../core/api';
import { Permissions } from '../../core/permissions';
import { HasPermissionDirective } from '../../core/session/permission.directive';
import { ConfirmService } from '../../shared/confirm';
import { ListState } from '../../shared/list-state';
import { ListStore } from '../../shared/list-store';
import { handleMutationError } from '../../shared/problem-details';
import { UserOutput } from './identity.contracts';

@Injectable()
export class UsersStore extends ListStore<UserOutput> {
  private readonly http = inject(HttpClient);

  protected fetch(query: ListQuery): Observable<PaginatedList<UserOutput>> {
    return this.http.get<PaginatedList<UserOutput>>(`${API_BASE}/identity/users`, {
      params: listParams(query),
    });
  }

  async deleteUser(userId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE}/identity/users/${userId}`));
    this.removeWhere((user) => user.id === userId);
  }
}

@Component({
  selector: 'app-users-list',
  providers: [UsersStore],
  imports: [
    DatePipe,
    HasPermissionDirective,
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
      <h1>Utilizadores</h1>
      <a mat-flat-button routerLink="/users/new" data-testid="create-user">Criar utilizador</a>
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
      emptyMessage="Nenhum utilizador"
      (retry)="store.retry()"
    >
      <a mat-stroked-button emptyAction routerLink="/users/new">Criar utilizador</a>

      <table
        mat-table
        matSort
        [dataSource]="rows()"
        data-testid="users-table"
        (matSortChange)="changeSort($event)"
      >
        <ng-container matColumnDef="email">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="email">Email</th>
          <td mat-cell *matCellDef="let user" [attr.data-testid]="'row-' + user.id">
            <a [routerLink]="['/users', user.id]">{{ user.email }}</a>
          </td>
        </ng-container>

        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="firstName">Nome</th>
          <td mat-cell *matCellDef="let user">{{ user.firstName }} {{ user.lastName }}</td>
        </ng-container>

        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="createdAt">Criado em</th>
          <td mat-cell *matCellDef="let user">{{ user.createdAt | date: 'short' }}</td>
        </ng-container>

        <ng-container matColumnDef="lastLoginAt">
          <th mat-header-cell *matHeaderCellDef>Último login</th>
          <td mat-cell *matCellDef="let user">{{ user.lastLoginAt | date: 'short' }}</td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let user">
            <a
              *appHasPermission="permissions.userManage"
              mat-button
              [attr.data-testid]="'edit-' + user.id"
              [routerLink]="['/users', user.id, 'edit']"
              >Editar</a
            >
            <button
              *appHasPermission="permissions.userManage"
              mat-button
              type="button"
              [attr.data-testid]="'delete-' + user.id"
              (click)="confirmDelete(user)"
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
      [pageSizeOptions]="[10, 20, 50]"
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
export class UsersList {
  readonly store = inject(UsersStore);
  readonly permissions = Permissions;
  readonly columns = ['email', 'name', 'createdAt', 'lastLoginAt', 'actions'];
  readonly rows = computed(() => [...this.store.items()]);

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmService);
  private readonly snackBar = inject(MatSnackBar);

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    void this.store.load({
      pageNumber: Number(params.get('pageNumber') ?? 1),
      pageSize: Number(params.get('pageSize') ?? DEFAULT_PAGE_SIZE),
      searchTerm: params.get('searchTerm') ?? undefined,
    });
  }

  async search(searchTerm: string): Promise<void> {
    await this.apply({ pageNumber: 1, searchTerm: searchTerm || undefined });
  }

  changeSort(sort: Sort): void {
    void this.store.load({
      pageNumber: 1,
      sortBy: sort.direction ? sort.active : undefined,
      sortDirection: sort.direction ? (sort.direction as SortDirection) : undefined,
    });
  }

  async changePage(event: PageEvent): Promise<void> {
    await this.apply({ pageNumber: event.pageIndex + 1, pageSize: event.pageSize });
  }

  async confirmDelete(user: UserOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Eliminar utilizador',
      message: `Eliminar ${user.email}? Esta ação não é reversível.`,
      confirmLabel: 'Eliminar',
    });
    if (!confirmed) {
      return;
    }

    try {
      await this.store.deleteUser(user.id);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }

  private async apply(patch: Partial<ListQuery>): Promise<void> {
    const query = { ...this.store.query(), ...patch };
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        pageNumber: query.pageNumber,
        pageSize: query.pageSize,
        searchTerm: query.searchTerm ?? null,
      },
      queryParamsHandling: 'merge',
    });
    await this.store.load(patch);
  }
}
