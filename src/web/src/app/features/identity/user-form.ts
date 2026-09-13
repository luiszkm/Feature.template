import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { email, form, FormField, minLength, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE } from '../../core/api';
import { NotFound } from '../../shared/screens';
import { Problem, fieldError, parseProblem, problemKind } from '../../shared/problem-details';
import { RegisterUserRequest, UpdateUserRequest, UserOutput } from './identity.contracts';

export const USER_CREATED_MESSAGE = 'Utilizador criado';

@Component({
  selector: 'app-user-form',
  imports: [
    FormField,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    NotFound,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <app-not-found [title]="problem()!.title" />
    } @else {
      <h1>{{ userId() ? 'Editar utilizador' : 'Criar utilizador' }}</h1>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" data-testid="form-loading" />
      }

      <form (submit)="onSubmit($event)">
        @if (!userId()) {
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput data-testid="email" [formField]="userForm.email" />
          </mat-form-field>
          @if (message('email'); as text) {
            <p class="field-error" data-testid="email-error">{{ text }}</p>
          }

          <mat-form-field>
            <mat-label>Password</mat-label>
            <input
              matInput
              type="password"
              data-testid="password"
              [formField]="userForm.password"
            />
          </mat-form-field>
          @if (message('password'); as text) {
            <p class="field-error" data-testid="password-error">{{ text }}</p>
          }
        }

        <mat-form-field>
          <mat-label>Nome</mat-label>
          <input matInput data-testid="firstName" [formField]="userForm.firstName" />
        </mat-form-field>
        @if (message('firstName'); as text) {
          <p class="field-error" data-testid="firstName-error">{{ text }}</p>
        }

        <mat-form-field>
          <mat-label>Apelido</mat-label>
          <input matInput data-testid="lastName" [formField]="userForm.lastName" />
        </mat-form-field>
        @if (message('lastName'); as text) {
          <p class="field-error" data-testid="lastName-error">{{ text }}</p>
        }

        <button mat-flat-button type="submit" data-testid="submit" [disabled]="pending()">
          Guardar
        </button>
      </form>
    }
  `,
  styles: `
    .field-error {
      color: var(--mat-sys-error, #b3261e);
      margin: -0.5rem 0 0.5rem;
      font-size: 0.75rem;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: 26rem;
    }
  `,
})
export class UserForm {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly userId = signal<string | null>(this.route.snapshot.paramMap.get('userId'));
  readonly loading = signal(false);
  readonly pending = signal(false);
  readonly problem = signal<Problem | null>(null);
  readonly saved = signal<UserOutput | null>(null);

  readonly model = signal({ email: '', password: '', firstName: '', lastName: '' });

  // Mirrors RegisterUserValidator: the API is the authority, this only saves a round trip.
  readonly userForm = form(this.model, (path) => {
    required(path.firstName);
    minLength(path.firstName, 2);
    required(path.lastName);
    minLength(path.lastName, 2);
    if (!this.userId()) {
      required(path.email);
      email(path.email);
      required(path.password);
      minLength(path.password, 8);
    }
  });

  constructor() {
    const userId = this.userId();
    if (userId) {
      void this.loadUser(userId);
    }
  }

  notFound(): boolean {
    const problem = this.problem();
    return problem !== null && problemKind(problem) === 'not-found';
  }

  message(field: string): string | null {
    const problem = this.problem();
    if (!problem) {
      return null;
    }
    // A duplicate email comes back as a business-rule 409 with no `errors` map.
    if (problem.status === 409 && field === 'email') {
      return problem.detail;
    }
    return fieldError(problem, field);
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.userForm, async () => {
      await this.save();
      return undefined;
    });
  }

  private async loadUser(userId: string): Promise<void> {
    this.loading.set(true);
    try {
      const user = await firstValueFrom(
        this.http.get<UserOutput>(`${API_BASE}/identity/users/${userId}`),
      );
      this.apply(user);
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    } finally {
      this.loading.set(false);
    }
  }

  private async save(): Promise<void> {
    this.pending.set(true);
    this.problem.set(null);
    const userId = this.userId();
    const value = this.model();

    try {
      if (userId) {
        const updated = await firstValueFrom(
          this.http.put<UserOutput>(`${API_BASE}/identity/users/${userId}`, {
            firstName: value.firstName,
            lastName: value.lastName,
          } satisfies UpdateUserRequest),
        );
        this.apply(updated);
      } else {
        await firstValueFrom(
          this.http.post<UserOutput>(`${API_BASE}/identity/register`, {
            email: value.email,
            password: value.password,
            firstName: value.firstName,
            lastName: value.lastName,
          } satisfies RegisterUserRequest),
        );
        this.snackBar.open(USER_CREATED_MESSAGE, undefined, { duration: 4000 });
        await this.router.navigateByUrl('/users');
      }
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    } finally {
      this.pending.set(false);
    }
  }

  private apply(user: UserOutput): void {
    this.saved.set(user);
    this.model.update((current) => ({
      ...current,
      email: user.email,
      firstName: user.firstName,
      lastName: user.lastName,
    }));
  }
}
