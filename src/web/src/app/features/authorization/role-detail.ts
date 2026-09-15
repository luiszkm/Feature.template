import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE, DEFAULT_PAGE_SIZE, PaginatedList, listParams } from '../../core/api';
import { ConfirmService } from '../../shared/confirm';
import { NotFound } from '../../shared/screens';
import { Problem, parseProblem, problemKind } from '../../shared/problem-details';
import { PermissionOutput, RoleOutput, RoleWithPermissionsOutput } from './authorization.contracts';

@Component({
  selector: 'app-role-detail',
  imports: [MatButtonModule, MatCardModule, MatSelectModule, NotFound],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <app-not-found [title]="problem()!.title" />
    } @else {
      <mat-card data-testid="role-card">
        <h1 data-testid="role-name">{{ role()?.name }}</h1>
        <p data-testid="role-description">{{ role()?.description }}</p>
      </mat-card>

      <mat-card>
        <h2>Permissões</h2>
        @if (permissions().length === 0) {
          <p data-testid="permissions-empty">Sem permissões atribuídas</p>
        }
        <ul data-testid="role-permissions">
          @for (permission of permissions(); track permission.id) {
            <li [attr.data-testid]="'permission-' + permission.id">
              {{ permission.name }}
              <button
                mat-button
                type="button"
                [attr.data-testid]="'revoke-' + permission.id"
                (click)="revoke(permission)"
              >
                Revogar
              </button>
            </li>
          }
        </ul>

        <select
          data-testid="permission-picker"
          [value]="selected()"
          (change)="select($any($event.target).value)"
        >
          <option value="">Escolher permissão</option>
          @for (permission of catalog(); track permission.id) {
            <option [value]="permission.id">{{ permission.name }}</option>
          }
        </select>
        <button mat-flat-button type="button" data-testid="assign" (click)="assign()">
          Atribuir
        </button>
      </mat-card>
    }
  `,
})
export class RoleDetail {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly confirm = inject(ConfirmService);

  readonly roleId = this.route.snapshot.paramMap.get('roleId') ?? '';
  readonly role = signal<RoleOutput | null>(null);
  readonly permissions = signal<readonly PermissionOutput[]>([]);
  readonly catalog = signal<readonly PermissionOutput[]>([]);
  readonly selected = signal('');
  readonly problem = signal<Problem | null>(null);

  constructor() {
    void this.load();
  }

  notFound(): boolean {
    const problem = this.problem();
    return problem !== null && problemKind(problem) === 'not-found';
  }

  select(permissionId: string): void {
    this.selected.set(permissionId);
  }

  async assign(): Promise<void> {
    const permissionId = this.selected();
    const permission = this.catalog().find((candidate) => candidate.id === permissionId);
    if (!permission) {
      return;
    }

    await firstValueFrom(
      this.http.post<void>(`${API_BASE}/authorization/roles/${this.roleId}/permissions`, {
        permissionId,
      }),
    );
    this.permissions.update((current) => [...current, permission]);
    this.selected.set('');
  }

  async revoke(permission: PermissionOutput): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Revogar permissão',
      message: `Revogar ${permission.name} deste role?`,
      confirmLabel: 'Revogar',
    });
    if (!confirmed) {
      return;
    }

    await firstValueFrom(
      this.http.delete<void>(
        `${API_BASE}/authorization/roles/${this.roleId}/permissions/${permission.id}`,
      ),
    );
    this.permissions.update((current) =>
      current.filter((candidate) => candidate.id !== permission.id),
    );
  }

  private async load(): Promise<void> {
    try {
      const withPermissions = await firstValueFrom(
        this.http.get<RoleWithPermissionsOutput>(
          `${API_BASE}/authorization/roles/${this.roleId}/permissions`,
        ),
      );
      this.role.set(withPermissions);
      this.permissions.set(withPermissions.permissions);

      const catalog = await firstValueFrom(
        this.http.get<PaginatedList<PermissionOutput>>(`${API_BASE}/authorization/permissions`, {
          params: listParams({ pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE }),
        }),
      );
      this.catalog.set(catalog.data);
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    }
  }
}
