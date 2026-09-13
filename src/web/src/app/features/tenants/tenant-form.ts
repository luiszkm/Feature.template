import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { disabled, form, FormField, required, submit } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE } from '../../core/api';
import { NotFound } from '../../shared/screens';
import { Problem, fieldError, parseProblem, problemKind } from '../../shared/problem-details';
import {
  CreateTenantRequest,
  TENANT_ISOLATION_MODE_LABELS,
  TenantIsolationMode,
  TenantOutput,
  UpdateTenantRequest,
} from './tenants.contracts';

@Component({
  selector: 'app-tenant-form',
  imports: [DatePipe, FormField, MatButtonModule, MatFormFieldModule, MatInputModule, NotFound],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <app-not-found [title]="problem()!.title" />
    } @else {
      <h1>{{ tenantId ? 'Editar tenant' : 'Criar tenant' }}</h1>

      <form (submit)="onSubmit($event)">
        <mat-form-field>
          <mat-label>Chave</mat-label>
          <input matInput data-testid="tenantKey" [formField]="tenantForm.tenantKey" />
        </mat-form-field>
        @if (message('tenantKey'); as text) {
          <p class="field-error" data-testid="tenantKey-error">{{ text }}</p>
        }

        <mat-form-field>
          <mat-label>Nome</mat-label>
          <input matInput data-testid="displayName" [formField]="tenantForm.displayName" />
        </mat-form-field>
        @if (message('displayName'); as text) {
          <p class="field-error" data-testid="displayName-error">{{ text }}</p>
        }

        <mat-form-field>
          <mat-label>Email de contacto</mat-label>
          <input matInput data-testid="contactEmail" [formField]="tenantForm.contactEmail" />
        </mat-form-field>
        @if (message('contactEmail'); as text) {
          <p class="field-error" data-testid="contactEmail-error">{{ text }}</p>
        }

        <button mat-flat-button type="submit" data-testid="submit" [disabled]="pending()">
          Guardar
        </button>
      </form>

      @if (tenant(); as loaded) {
        <dl data-testid="tenant-fields">
          <dd data-testid="field-tenantId">{{ loaded.tenantId }}</dd>
          <dd data-testid="field-tenantKey">{{ loaded.tenantKey }}</dd>
          <dd data-testid="field-displayName">{{ loaded.displayName }}</dd>
          <dd data-testid="field-contactEmail">{{ loaded.contactEmail }}</dd>
          <dd data-testid="field-isActive">{{ loaded.isActive ? 'Sim' : 'Não' }}</dd>
          <dd data-testid="field-isolationMode">{{ isolationModeLabel(loaded.isolationMode) }}</dd>
          <dd data-testid="field-createdAt">{{ loaded.createdAt | date: 'short' }}</dd>
        </dl>
      }
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
export class TenantForm {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tenantId = this.route.snapshot.paramMap.get('id');
  readonly pending = signal(false);
  readonly problem = signal<Problem | null>(null);
  readonly tenant = signal<TenantOutput | null>(null);

  readonly model = signal({ tenantKey: '', displayName: '', contactEmail: '' });

  readonly tenantForm = form(this.model, (path) => {
    required(path.displayName);
    // The API keys a tenant by `tenantKey` and has no rename route.
    disabled(path.tenantKey, () => this.tenantId !== null);
  });

  constructor() {
    if (this.tenantId) {
      void this.load(this.tenantId);
    }
  }

  isolationModeLabel(mode: TenantIsolationMode): string {
    return TENANT_ISOLATION_MODE_LABELS[mode];
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
    if (problem.status === 409 && field === 'tenantKey') {
      return problem.detail;
    }
    return fieldError(problem, field);
  }

  async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    await submit(this.tenantForm, async () => {
      await this.save();
      return undefined;
    });
  }

  private async load(tenantId: string): Promise<void> {
    try {
      const tenant = await firstValueFrom(
        this.http.get<TenantOutput>(`${API_BASE}/tenants/${tenantId}`),
      );
      this.apply(tenant);
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    }
  }

  private async save(): Promise<void> {
    this.pending.set(true);
    this.problem.set(null);
    const value = this.model();

    try {
      if (this.tenantId) {
        const updated = await firstValueFrom(
          this.http.put<TenantOutput>(`${API_BASE}/tenants/${this.tenantId}`, {
            displayName: value.displayName,
            contactEmail: value.contactEmail || null,
          } satisfies UpdateTenantRequest),
        );
        this.apply(updated);
      } else {
        await firstValueFrom(
          this.http.post<TenantOutput>(`${API_BASE}/tenants`, {
            tenantKey: value.tenantKey,
            displayName: value.displayName,
            contactEmail: value.contactEmail || null,
            isolationMode: TenantIsolationMode.SharedDb,
          } satisfies CreateTenantRequest),
        );
        await this.router.navigateByUrl('/tenants');
      }
    } catch (error: unknown) {
      this.problem.set(parseProblem(error));
    } finally {
      this.pending.set(false);
    }
  }

  private apply(tenant: TenantOutput): void {
    this.tenant.set(tenant);
    this.model.set({
      tenantKey: tenant.tenantKey,
      displayName: tenant.displayName,
      contactEmail: tenant.contactEmail ?? '',
    });
  }
}
