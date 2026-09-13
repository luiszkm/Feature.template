import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE, LOCAL_403 } from '../../core/api';
import { Permissions } from '../../core/permissions';
import { SessionStore } from '../../core/session/session.store';
import { NotFound } from '../../shared/screens';
import { Problem, parseProblem, problemKind } from '../../shared/problem-details';
import { UserOutput } from './identity.contracts';

export const NO_ROLE_ACCESS_MESSAGE = 'Sem acesso aos roles';

@Component({
  selector: 'app-user-detail',
  imports: [DatePipe, MatButtonModule, MatCardModule, MatProgressBarModule, NotFound, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <app-not-found [title]="problem()!.title" />
    } @else {
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" data-testid="detail-loading" />
      }

      @if (user(); as detail) {
        <mat-card data-testid="user-card">
          <h1 data-testid="user-email">{{ detail.email }}</h1>
          <p data-testid="user-name">{{ detail.firstName }} {{ detail.lastName }}</p>
          <p data-testid="user-created">{{ detail.createdAt | date: 'short' }}</p>
          @if (canEdit(detail)) {
            <a
              mat-stroked-button
              data-testid="detail-edit"
              [routerLink]="['/users', detail.id, 'edit']"
              >Editar</a
            >
          }
          <a mat-stroked-button [routerLink]="['/users', detail.id, 'roles']">Gerir roles</a>
        </mat-card>
      }

      <mat-card data-testid="roles-card">
        <h2>Roles</h2>
        @if (rolesDenied()) {
          <p data-testid="roles-denied">{{ noAccessMessage }}</p>
        } @else {
          <ul data-testid="roles-list">
            @for (role of roles(); track role) {
              <li>{{ role }}</li>
            }
          </ul>
        }
      </mat-card>
    }
  `,
})
export class UserDetail {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(SessionStore);

  readonly noAccessMessage = NO_ROLE_ACCESS_MESSAGE;
  readonly user = signal<UserOutput | null>(null);
  readonly roles = signal<readonly string[]>([]);
  readonly rolesDenied = signal(false);
  readonly loading = signal(true);
  readonly problem = signal<Problem | null>(null);

  constructor() {
    const userId = this.route.snapshot.paramMap.get('userId') ?? '';
    void this.load(userId);
  }

  /** Mirrors the API's `UserManageOrSelf`: managers edit anyone, everyone edits themselves. */
  canEdit(detail: UserOutput): boolean {
    return (
      this.session.hasPermission(Permissions.userManage) || this.session.user()?.id === detail.id
    );
  }

  notFound(): boolean {
    const problem = this.problem();
    return problem !== null && problemKind(problem) === 'not-found';
  }

  private async load(userId: string): Promise<void> {
    this.loading.set(true);
    try {
      this.user.set(
        await firstValueFrom(this.http.get<UserOutput>(`${API_BASE}/identity/users/${userId}`)),
      );
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    } finally {
      this.loading.set(false);
    }

    try {
      this.roles.set(
        await firstValueFrom(
          this.http.get<string[]>(`${API_BASE}/identity/users/${userId}/roles`, {
            // A 403 here hides one card, not the screen: the user may read themselves
            // without holding UsersManage.
            context: new HttpContext().set(LOCAL_403, true),
          }),
        ),
      );
    } catch (error: unknown) {
      this.rolesDenied.set(parseProblem(error).status === 403);
    }
  }
}
