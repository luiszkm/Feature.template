import {
  ChangeDetectionStrategy,
  Component,
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
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
import {
  Problem,
  fieldError,
  handleMutationError,
  parseProblem,
} from '../../shared/problem-details';
import { NamedRequest, RoleOutput } from './authorization.contracts';

@Injectable()
export class RolesStore extends ListStore<RoleOutput> {
  private readonly http = inject(HttpClient);

  protected fetch(query: ListQuery): Observable<PaginatedList<RoleOutput>> {
    return this.http.get<PaginatedList<RoleOutput>>(`${API_BASE}/authorization/roles`, {
      params: listParams(query),
    });
  }

  async create(body: NamedRequest): Promise<RoleOutput> {
    const role = await firstValueFrom(
      this.http.post<RoleOutput>(`${API_BASE}/authorization/roles`, body),
    );
    this.append(role);
    return role;
  }

  async update(roleId: string, body: NamedRequest): Promise<RoleOutput> {
    const role = await firstValueFrom(
      this.http.put<RoleOutput>(`${API_BASE}/authorization/roles/${roleId}`, body),
    );
    this.replace(role, (candidate) => candidate.id === roleId);
    return role;
  }

  async remove(roleId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE}/authorization/roles/${roleId}`));
    this.removeWhere((candidate) => candidate.id === roleId);
  }
}

export interface NamedDialogData {
  title: string;
  value: NamedRequest;
  save: (body: NamedRequest) => Promise<unknown>;
}

/** Shared by roles and permissions: both are `{ name, description }` behind the same statuses. */
@Component({
  selector: 'app-named-dialog',
  imports: [FormField, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form (submit)="onSubmit($event)">
      <mat-dialog-content>
        <mat-form-field>
          <mat-label>Nome</mat-label>
          <input matInput data-testid="name" [formField]="namedForm.name" />
        </mat-form-field>
        @if (message('name'); as text) {
          <p class="field-error" data-testid="name-error">{{ text }}</p>
        }
        <mat-form-field>
          <mat-label>Descrição</mat-label>
          <input matInput data-testid="description" [formField]="namedForm.description" />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" data-testid="dialog-cancel" (click)="dialogRef.close()">
          Cancelar
        </button>
        <button mat-flat-button type="submit" data-testid="dialog-save">Guardar</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .field-error {
      color: var(--mat-sys-error, #b3261e);
      margin: -0.5rem 0 0.5rem;
      font-size: 0.75rem;
    }
  `,
})
export class NamedDialog {
  readonly data = inject<NamedDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject<MatDialogRef<NamedDialog, boolean>>(MatDialogRef);

  readonly model = signal<NamedRequest>({ ...this.data.value });
  readonly namedForm = form(this.model, (path) => {
    required(path.name);
  });
  readonly problem = signal<Problem | null>(null);

  message(field: string): string | null {
    const problem = this.problem();
    if (!problem) {
      return null;
    }
    return (
      fieldError(problem, field) ??
      (problem.status === 409 && field === 'name' ? problem.detail : null)
    );
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.namedForm, async () => {
      this.problem.set(null);
      try {
        await this.data.save(this.model());
        this.dialogRef.close(true);
      } catch (error: unknown) {
        this.problem.set(parseProblem(error));
      }
      return undefined;
    });
  }
}

@Component({
  selector: 'app-roles-list',
  providers: [RolesStore],
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
      <h1>Roles</h1>
      <button mat-flat-button type="button" data-testid="create-role" (click)="openCreate()">
        Criar role
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
      emptyMessage="Nenhum role"
      (retry)="store.retry()"
    >
      <button mat-stroked-button emptyAction type="button" (click)="openCreate()">
        Criar role
      </button>

      <table
        mat-table
        matSort
        [dataSource]="rows()"
        data-testid="roles-table"
        (matSortChange)="changeSort($event)"
      >
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef mat-sort-header="name">Nome</th>
          <td mat-cell *matCellDef="let role" [attr.data-testid]="'row-' + role.id">
            <a [routerLink]="['/roles', role.id]">{{ role.name }}</a>
          </td>
        </ng-container>

        <ng-container matColumnDef="description">
          <th mat-header-cell *matHeaderCellDef>Descrição</th>
          <td mat-cell *matCellDef="let role" [attr.data-testid]="'description-' + role.id">
            {{ role.description }}
          </td>
        </ng-container>

        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let role">
            <button
              mat-button
              type="button"
              [attr.data-testid]="'edit-' + role.id"
              (click)="openEdit(role)"
            >
              Editar
            </button>
            <button
              mat-button
              type="button"
              [attr.data-testid]="'delete-' + role.id"
              (click)="confirmDelete(role)"
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
export class RolesList {
  readonly store = inject(RolesStore);
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
      title: 'Criar role',
      value: { name: '', description: '' },
      save: (body) => this.store.create(body),
    });
  }

  openEdit(role: RoleOutput): void {
    openNamedDialog(this.dialog, {
      title: 'Editar role',
      value: { name: role.name, description: role.description },
      save: (body) => this.store.update(role.id, body),
    });
  }

  async confirmDelete(role: RoleOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Eliminar role',
      message: `Eliminar ${role.name}?`,
      confirmLabel: 'Eliminar',
    });
    if (!confirmed) {
      return;
    }
    try {
      await this.store.remove(role.id);
    } catch (error: unknown) {
      const message = handleMutationError(error, () => void this.store.load());
      if (message) {
        this.snackBar.open(message, undefined, { duration: 4000 });
      }
    }
  }
}

export function openNamedDialog(
  dialog: MatDialog,
  data: NamedDialogData,
): MatDialogRef<NamedDialog, boolean> {
  return dialog.open<NamedDialog, NamedDialogData, boolean>(NamedDialog, { data });
}
