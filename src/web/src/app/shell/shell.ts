import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { API_BASE } from '../core/api';
import { HasPermissionDirective } from '../core/session/permission.directive';
import { Permissions } from '../core/permissions';
import { SessionStore } from '../core/session/session.store';
import { ConfirmService } from '../shared/confirm';
import { AiAvailability } from '../features/ai/ai-availability';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-shell',
  imports: [
    HasPermissionDirective,
    MatButtonModule,
    MatIconModule,
    MatToolbarModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar>
      <nav>
        <a routerLink="/users" routerLinkActive="active" data-testid="nav-users">Utilizadores</a>
        <a
          *appHasPermission="permissions.roleRead"
          routerLink="/roles"
          routerLinkActive="active"
          data-testid="nav-roles"
          >Roles</a
        >
        <a
          *appHasPermission="permissions.permissionRead"
          routerLink="/permissions"
          routerLinkActive="active"
          data-testid="nav-permissions"
          >Permissões</a
        >
        <a
          *appHasPermission="permissions.tenantsRead"
          routerLink="/tenants"
          routerLinkActive="active"
          data-testid="nav-tenants"
          >Tenants</a
        >
        @if (ai.available()) {
          <a routerLink="/ai" routerLinkActive="active" data-testid="nav-ai">AI</a>
          <a routerLink="/ai/conversations" routerLinkActive="active" data-testid="nav-conversations"
            >Conversas</a
          >
          <a
            *appHasPermission="permissions.agentRead"
            routerLink="/ai/agents"
            routerLinkActive="active"
            data-testid="nav-agents"
            >Agentes</a
          >
          <a
            *appHasPermission="permissions.agentRead"
            routerLink="/ai/usage"
            routerLinkActive="active"
            data-testid="nav-ai-usage"
            >Uso</a
          >
        }
      </nav>

      <span class="spacer"></span>

      <span data-testid="session-tenant">{{ session.tenantKey() }}</span>
      <span data-testid="session-user">{{ session.user()?.firstName }}</span>
      <button mat-button type="button" data-testid="logout" (click)="logout()">Sair</button>
    </mat-toolbar>

    <main>
      <router-outlet />
    </main>
  `,
  styles: `
    nav {
      display: flex;
      gap: 1rem;
    }
    .spacer {
      flex: 1 1 auto;
    }
    main {
      padding: 1.5rem;
    }
  `,
})
export class Shell {
  readonly session = inject(SessionStore);
  readonly ai = inject(AiAvailability);
  readonly permissions = Permissions;

  private readonly confirm = inject(ConfirmService);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  async logout(): Promise<void> {
    const confirmed = await this.confirm.ask({
      title: 'Terminar sessão',
      message: 'Quer sair da aplicação?',
      confirmLabel: 'Sair',
    });
    if (!confirmed) {
      return;
    }

    // Revoke first: clearing only the local state would leave the refresh cookie usable.
    try {
      await firstValueFrom(this.http.post<void>(`${API_BASE}/identity/logout`, {}));
    } finally {
      this.session.clear();
      await this.router.navigateByUrl('/login');
    }
  }
}
