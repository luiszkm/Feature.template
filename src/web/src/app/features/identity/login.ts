import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { email, form, FormField, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE } from '../../core/api';
import { AuthToken, DEFAULT_TENANT_KEY, SessionStore } from '../../core/session/session.store';
import { Problem, fieldError, parseProblem } from '../../shared/problem-details';

export const RATE_LIMIT_MESSAGE = 'Demasiadas tentativas, tente dentro de um minuto';
export const INVALID_TENANT_MESSAGE = 'Tenant inválido';
export const EXPIRED_SESSION_MESSAGE = 'A sessão expirou, entre de novo';

@Component({
  selector: 'app-login',
  imports: [FormField, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-card class="login">
      <h1>Entrar</h1>
      <form (submit)="onSubmit($event)">
        <mat-form-field>
          <mat-label>Tenant</mat-label>
          <input matInput data-testid="tenant" [formField]="loginForm.tenantKey" />
        </mat-form-field>
        @if (tenantMessage(); as message) {
          <p class="field-error" data-testid="tenant-error">{{ message }}</p>
        }

        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" data-testid="email" [formField]="loginForm.email" />
        </mat-form-field>
        @if (fieldMessage('email'); as message) {
          <p class="field-error" data-testid="email-error">{{ message }}</p>
        }

        <mat-form-field>
          <mat-label>Password</mat-label>
          <input matInput type="password" data-testid="password" [formField]="loginForm.password" />
        </mat-form-field>
        @if (fieldMessage('password'); as message) {
          <p class="field-error" data-testid="password-error">{{ message }}</p>
        }

        @if (message(); as text) {
          <p class="message" data-testid="login-message">{{ text }}</p>
        }

        <button mat-flat-button type="submit" data-testid="submit" [disabled]="pending()">
          Entrar
        </button>
      </form>
    </mat-card>
  `,
  styles: `
    .field-error {
      color: var(--mat-sys-error, #b3261e);
      margin: -0.5rem 0 0.5rem;
      font-size: 0.75rem;
    }
    .login {
      display: block;
      margin: 4rem auto;
      max-width: 24rem;
      padding: 2rem;
    }
    form {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .message {
      color: var(--mat-sys-error, #b3261e);
    }
  `,
})
export class Login {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly model = signal({
    tenantKey: this.session.tenantKey() || DEFAULT_TENANT_KEY,
    email: '',
    password: '',
  });

  readonly loginForm = form(this.model, (path) => {
    required(path.tenantKey);
    required(path.email);
    email(path.email);
    required(path.password);
  });

  readonly pending = signal(false);
  readonly problem = signal<Problem | null>(null);
  readonly message = signal<string | null>(
    reasonMessage(this.route.snapshot.queryParamMap.get('reason')),
  );

  tenantMessage(): string | null {
    if (this.problem()?.status === 409) {
      return INVALID_TENANT_MESSAGE;
    }
    return this.fieldMessage('tenantKey');
  }

  fieldMessage(field: string): string | null {
    const problem = this.problem();
    return problem ? fieldError(problem, field) : null;
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.loginForm, async () => {
      await this.authenticate();
      return undefined;
    });
  }

  private async authenticate(): Promise<void> {
    const { tenantKey, email: address, password } = this.model();
    this.pending.set(true);
    this.problem.set(null);
    this.message.set(null);
    this.session.setTenantKey(tenantKey.trim().toLowerCase());

    try {
      const token = await firstValueFrom(
        this.http.post<AuthToken>(`${API_BASE}/identity/login`, { email: address, password }),
      );
      this.session.apply(token);
      const redirectTo = this.route.snapshot.queryParamMap.get('redirectTo') ?? '/users';
      await this.router.navigateByUrl(redirectTo);
    } catch (error: unknown) {
      const problem = parseProblem(error);
      this.problem.set(problem);
      this.message.set(messageFor(problem));
      this.model.update((current) => ({ ...current, password: '' }));
    } finally {
      this.pending.set(false);
    }
  }
}

function messageFor(problem: Problem): string | null {
  switch (problem.status) {
    case 429:
      return RATE_LIMIT_MESSAGE;
    case 409:
      return INVALID_TENANT_MESSAGE;
    case 400:
      return null;
    default:
      return problem.detail || problem.title;
  }
}

function reasonMessage(reason: string | null): string | null {
  if (reason === 'tenant') {
    return INVALID_TENANT_MESSAGE;
  }
  return reason === 'expired' ? EXPIRED_SESSION_MESSAGE : null;
}
