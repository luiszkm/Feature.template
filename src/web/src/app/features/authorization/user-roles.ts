import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE, DEFAULT_PAGE_SIZE, PaginatedList, listParams } from '../../core/api';
import { RoleOutput } from './authorization.contracts';

@Component({
  selector: 'app-user-roles',
  imports: [MatButtonModule, MatCardModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card>
      <h1>Roles do utilizador</h1>
      <ul data-testid="assigned-roles">
        @for (role of assigned(); track role.id) {
          <li [attr.data-testid]="'assigned-' + role.id">
            {{ role.name }}
            <button
              mat-button
              type="button"
              [attr.data-testid]="'revoke-' + role.id"
              (click)="revoke(role)"
            >
              Revogar
            </button>
          </li>
        }
      </ul>

      <select
        data-testid="role-picker"
        [value]="selected()"
        (change)="select($any($event.target).value)"
      >
        <option value="">Escolher role</option>
        @for (role of catalog(); track role.id) {
          <option [value]="role.id">{{ role.name }}</option>
        }
      </select>
      <button mat-flat-button type="button" data-testid="assign" (click)="assign()">
        Atribuir
      </button>
    </mat-card>
  `,
})
export class UserRoles {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  readonly userId = this.route.snapshot.paramMap.get('userId') ?? '';
  readonly assigned = signal<readonly RoleOutput[]>([]);
  readonly catalog = signal<readonly RoleOutput[]>([]);
  readonly selected = signal('');

  constructor() {
    void this.load();
  }

  select(roleId: string): void {
    this.selected.set(roleId);
  }

  async assign(): Promise<void> {
    const roleId = this.selected();
    if (!roleId) {
      return;
    }
    await firstValueFrom(
      this.http.post<void>(`${API_BASE}/authorization/users/${this.userId}/roles`, { roleId }),
    );
    this.selected.set('');
    await this.loadAssigned();
  }

  async revoke(role: RoleOutput): Promise<void> {
    await firstValueFrom(
      this.http.delete<void>(`${API_BASE}/authorization/users/${this.userId}/roles/${role.id}`),
    );
    await this.loadAssigned();
  }

  private async load(): Promise<void> {
    await this.loadAssigned();
    const catalog = await firstValueFrom(
      this.http.get<PaginatedList<RoleOutput>>(`${API_BASE}/authorization/roles`, {
        params: listParams({ pageNumber: 1, pageSize: DEFAULT_PAGE_SIZE }),
      }),
    );
    this.catalog.set(catalog.data);
  }

  private async loadAssigned(): Promise<void> {
    this.assigned.set(
      await firstValueFrom(
        this.http.get<RoleOutput[]>(`${API_BASE}/authorization/users/${this.userId}/roles`),
      ),
    );
  }
}
